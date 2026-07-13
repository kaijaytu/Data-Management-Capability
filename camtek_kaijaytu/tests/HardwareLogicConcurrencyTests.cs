using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DMC.Tests
{
    /// <summary>
    /// Concurrency stress tests for the native HardwareLogic library via HardwareBridge.
    /// Covers test cases C-01 through C-10 from the test matrix.
    /// </summary>
    public class HardwareLogicConcurrencyTests
    {
        private const int ConcurrentThreads = 100;
        private const int StressDurationSeconds = 10;
        private const int InitShutdownCycles = 1000;

        /// <summary>
        /// C-01: N threads write different values to the same elementId simultaneously.
        /// Expects: no crash/deadlock, final value is one complete write (no torn write).
        /// </summary>
        [HandleProcessCorruptedStateExceptions]
        public static void C01_ParallelWriteSameKey()
        {
            Console.WriteLine("=== C-01: Parallel Write Same Key ===");
            var bridge = new HardwareBridgeTestWrapper();
            bridge.Initialize();
            var sw = Stopwatch.StartNew();

            int successCount = 0;
            int failCount = 0;
            int aveCount = 0;

            try
            {
                Parallel.For(0, ConcurrentThreads, new ParallelOptions { MaxDegreeOfParallelism = ConcurrentThreads }, i =>
                {
                    try
                    {
                        bridge.WriteData("shared_key", $"value_from_thread_{i}");
                        Interlocked.Increment(ref successCount);
                    }
                    catch (AccessViolationException ex)
                    {
                        Interlocked.Increment(ref aveCount);
                        Console.WriteLine($"  [AVE] Thread {i}: {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref failCount);
                        Console.WriteLine($"  [ERROR] Thread {i}: {ex.Message}");
                    }
                });
            }
            catch (AggregateException ae)
            {
                foreach (var ex in ae.InnerExceptions)
                    Console.WriteLine($"  [AGG ERROR] {ex.Message}");
            }

            sw.Stop();
            string finalValue = bridge.ReadData("shared_key");
            bridge.Shutdown();

            Console.WriteLine($"  Success: {successCount}, Failures: {failCount}, AVE: {aveCount}");
            Console.WriteLine($"  Final value: {finalValue}");
            Console.WriteLine($"  Elapsed: {sw.ElapsedMilliseconds} ms");
            Console.WriteLine();
        }

        /// <summary>
        /// C-02: Half threads write, half threads read the same elementId.
        /// Expects: no data race, read results are always a complete value.
        /// </summary>
        [HandleProcessCorruptedStateExceptions]
        public static void C02_ParallelReadWriteSameKey()
        {
            Console.WriteLine("=== C-02: Parallel Read/Write Same Key ===");
            var bridge = new HardwareBridgeTestWrapper();
            bridge.Initialize();
            bridge.WriteData("rw_key", "initial_value");
            var sw = Stopwatch.StartNew();

            int writeSuccess = 0, readSuccess = 0, failCount = 0, aveCount = 0;
            var corruptedReads = new ConcurrentBag<string>();

            Parallel.For(0, ConcurrentThreads, new ParallelOptions { MaxDegreeOfParallelism = ConcurrentThreads }, i =>
            {
                try
                {
                    if (i % 2 == 0)
                    {
                        bridge.WriteData("rw_key", $"written_by_{i}");
                        Interlocked.Increment(ref writeSuccess);
                    }
                    else
                    {
                        string val = bridge.ReadData("rw_key");
                        Interlocked.Increment(ref readSuccess);
                        // Verify the value is a complete write (not torn)
                        if (!val.StartsWith("written_by_") && val != "initial_value")
                        {
                            corruptedReads.Add(val);
                        }
                    }
                }
                catch (AccessViolationException ex)
                {
                    Interlocked.Increment(ref aveCount);
                    Console.WriteLine($"  [AVE] Thread {i}: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref failCount);
                }
            });

            sw.Stop();
            bridge.Shutdown();

            Console.WriteLine($"  Writes: {writeSuccess}, Reads: {readSuccess}, Failures: {failCount}, AVE: {aveCount}");
            Console.WriteLine($"  Corrupted reads: {corruptedReads.Count}");
            Console.WriteLine($"  Elapsed: {sw.ElapsedMilliseconds} ms");
            Console.WriteLine();
        }

        /// <summary>
        /// C-03: N threads each write to their own unique elementId.
        /// Expects: all elements stored correctly, no interference.
        /// </summary>
        [HandleProcessCorruptedStateExceptions]
        public static void C03_ParallelWriteDifferentKeys()
        {
            Console.WriteLine("=== C-03: Parallel Write Different Keys ===");
            var bridge = new HardwareBridgeTestWrapper();
            bridge.Initialize();
            var sw = Stopwatch.StartNew();

            int successCount = 0, failCount = 0, aveCount = 0;

            Parallel.For(0, ConcurrentThreads, new ParallelOptions { MaxDegreeOfParallelism = ConcurrentThreads }, i =>
            {
                try
                {
                    bridge.WriteData($"element_{i}", $"data_{i}");
                    Interlocked.Increment(ref successCount);
                }
                catch (AccessViolationException ex)
                {
                    Interlocked.Increment(ref aveCount);
                    Console.WriteLine($"  [AVE] Thread {i}: {ex.Message}");
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref failCount);
                }
            });

            sw.Stop();

            // Verify all data
            int verifySuccess = 0;
            for (int i = 0; i < ConcurrentThreads; i++)
            {
                try
                {
                    string val = bridge.ReadData($"element_{i}");
                    if (val == $"data_{i}") verifySuccess++;
                }
                catch { }
            }

            bridge.Shutdown();

            Console.WriteLine($"  Write success: {successCount}, Failures: {failCount}, AVE: {aveCount}");
            Console.WriteLine($"  Verify correct: {verifySuccess}/{ConcurrentThreads}");
            Console.WriteLine($"  Elapsed: {sw.ElapsedMilliseconds} ms");
            Console.WriteLine();
        }

        /// <summary>
        /// C-04: Multiple threads race to call Initialize and Shutdown simultaneously.
        /// Expects: only one Init succeeds (returns 0), rest return -1; no UB.
        /// </summary>
        [HandleProcessCorruptedStateExceptions]
        public static void C04_ParallelInitShutdownRace()
        {
            Console.WriteLine("=== C-04: Parallel Init/Shutdown Race ===");
            var sw = Stopwatch.StartNew();

            int initSuccess = 0, shutdownSuccess = 0, failCount = 0, aveCount = 0;

            Parallel.For(0, ConcurrentThreads, new ParallelOptions { MaxDegreeOfParallelism = ConcurrentThreads }, i =>
            {
                var bridge = new HardwareBridgeTestWrapper();
                try
                {
                    if (i % 2 == 0)
                    {
                        bridge.TryInitialize();
                        Interlocked.Increment(ref initSuccess);
                    }
                    else
                    {
                        bridge.TryShutdown();
                        Interlocked.Increment(ref shutdownSuccess);
                    }
                }
                catch (AccessViolationException ex)
                {
                    Interlocked.Increment(ref aveCount);
                    Console.WriteLine($"  [AVE] Thread {i}: {ex.Message}");
                }
                catch (Exception)
                {
                    Interlocked.Increment(ref failCount);
                }
            });

            sw.Stop();
            // Clean up: ensure shutdown
            try { new HardwareBridgeTestWrapper().TryShutdown(); } catch { }

            Console.WriteLine($"  Init attempts: {initSuccess}, Shutdown attempts: {shutdownSuccess}");
            Console.WriteLine($"  Expected failures: {failCount}, AVE: {aveCount}");
            Console.WriteLine($"  Elapsed: {sw.ElapsedMilliseconds} ms");
            Console.WriteLine();
        }

        /// <summary>
        /// C-05: Pre-write data, then 100+ threads read simultaneously.
        /// Expects: all return correct data, no deadlock.
        /// </summary>
        [HandleProcessCorruptedStateExceptions]
        public static void C05_ReadStorm()
        {
            Console.WriteLine("=== C-05: High Concurrency Read Storm ===");
            var bridge = new HardwareBridgeTestWrapper();
            bridge.Initialize();
            bridge.WriteData("storm_key", "storm_value_payload_12345");
            var sw = Stopwatch.StartNew();

            int successCount = 0, failCount = 0, aveCount = 0;
            int correctCount = 0;

            Parallel.For(0, ConcurrentThreads, new ParallelOptions { MaxDegreeOfParallelism = ConcurrentThreads }, i =>
            {
                try
                {
                    string val = bridge.ReadData("storm_key");
                    Interlocked.Increment(ref successCount);
                    if (val == "storm_value_payload_12345")
                        Interlocked.Increment(ref correctCount);
                }
                catch (AccessViolationException ex)
                {
                    Interlocked.Increment(ref aveCount);
                    Console.WriteLine($"  [AVE] Thread {i}: {ex.Message}");
                }
                catch (Exception)
                {
                    Interlocked.Increment(ref failCount);
                }
            });

            sw.Stop();
            bridge.Shutdown();

            Console.WriteLine($"  Success: {successCount}, Correct: {correctCount}, Failures: {failCount}, AVE: {aveCount}");
            Console.WriteLine($"  Elapsed: {sw.ElapsedMilliseconds} ms");
            Console.WriteLine();
        }

        /// <summary>
        /// C-06: Random mixed Read/Write operations sustained for a duration.
        /// Expects: no crash, no deadlock, measurable throughput.
        /// </summary>
        [HandleProcessCorruptedStateExceptions]
        public static void C06_ReadWriteStressTest()
        {
            Console.WriteLine($"=== C-06: Read/Write Stress Test ({StressDurationSeconds}s) ===");
            var bridge = new HardwareBridgeTestWrapper();
            bridge.Initialize();
            // Seed some data
            for (int i = 0; i < 10; i++)
                bridge.WriteData($"stress_{i}", $"seed_{i}");

            var sw = Stopwatch.StartNew();
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(StressDurationSeconds));

            long totalOps = 0, readOps = 0, writeOps = 0, errors = 0, aveCount = 0;

            var tasks = new Task[ConcurrentThreads];
            for (int t = 0; t < ConcurrentThreads; t++)
            {
                int threadId = t;
                tasks[t] = Task.Run(() =>
                {
                    var rng = new Random(threadId);
                    while (!cts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            int key = rng.Next(0, 10);
                            if (rng.Next(2) == 0)
                            {
                                bridge.WriteData($"stress_{key}", $"thread_{threadId}_val_{rng.Next()}");
                                Interlocked.Increment(ref writeOps);
                            }
                            else
                            {
                                bridge.ReadData($"stress_{key}");
                                Interlocked.Increment(ref readOps);
                            }
                            Interlocked.Increment(ref totalOps);
                        }
                        catch (AccessViolationException)
                        {
                            Interlocked.Increment(ref aveCount);
                        }
                        catch
                        {
                            Interlocked.Increment(ref errors);
                        }
                    }
                });
            }

            Task.WaitAll(tasks);
            sw.Stop();
            bridge.Shutdown();

            Console.WriteLine($"  Total ops: {totalOps} (Reads: {readOps}, Writes: {writeOps})");
            Console.WriteLine($"  Errors: {errors}, AVE: {aveCount}");
            Console.WriteLine($"  Throughput: {totalOps / Math.Max(1, sw.Elapsed.TotalSeconds):F0} ops/sec");
            Console.WriteLine($"  Elapsed: {sw.ElapsedMilliseconds} ms");
            Console.WriteLine();
        }

        /// <summary>
        /// C-07: Multiple threads trigger errors then immediately call HW_GetLastError.
        /// Known risk: g_lastError pointer may be overwritten during read.
        /// </summary>
        [HandleProcessCorruptedStateExceptions]
        public static void C07_GetLastErrorThreadSafety()
        {
            Console.WriteLine("=== C-07: HW_GetLastError Thread Safety ===");
            var bridge = new HardwareBridgeTestWrapper();
            bridge.Initialize();
            var sw = Stopwatch.StartNew();

            int successCount = 0, aveCount = 0;
            var observedErrors = new ConcurrentBag<string>();

            Parallel.For(0, ConcurrentThreads, new ParallelOptions { MaxDegreeOfParallelism = ConcurrentThreads }, i =>
            {
                try
                {
                    // Trigger an error: read a non-existent key
                    try { bridge.ReadData($"nonexistent_{i}"); } catch { }
                    // Immediately read the error message
                    string err = bridge.GetLastError();
                    observedErrors.Add(err);
                    Interlocked.Increment(ref successCount);
                }
                catch (AccessViolationException ex)
                {
                    Interlocked.Increment(ref aveCount);
                    Console.WriteLine($"  [AVE] Thread {i}: {ex.Message}");
                }
            });

            sw.Stop();
            bridge.Shutdown();

            // Check for corrupted strings
            int corrupted = 0;
            foreach (var err in observedErrors)
            {
                if (string.IsNullOrEmpty(err)) continue;
                if (!err.Contains("Element not found") && !err.Contains("Hardware"))
                    corrupted++;
            }

            Console.WriteLine($"  Observed errors: {observedErrors.Count}, Corrupted: {corrupted}");
            Console.WriteLine($"  AVE: {aveCount}");
            Console.WriteLine($"  Elapsed: {sw.ElapsedMilliseconds} ms");
            Console.WriteLine();
        }

        /// <summary>
        /// C-08: Multiple threads call GetStatusMessage while another thread toggles state.
        /// Expects: returned values are valid static strings (literal constants).
        /// </summary>
        [HandleProcessCorruptedStateExceptions]
        public static void C08_GetStatusMessageRace()
        {
            Console.WriteLine("=== C-08: GetStatusMessage Race ===");
            var bridge = new HardwareBridgeTestWrapper();
            bridge.Initialize();
            var sw = Stopwatch.StartNew();

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            int readCount = 0, aveCount = 0;
            var validMessages = new HashSet<string> { "Uninitialized", "Ready", "Busy", "Error", "Unknown" };
            var invalidMessages = new ConcurrentBag<string>();

            // State toggler thread
            var toggler = Task.Run(() =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    try { bridge.TryShutdown(); } catch { }
                    try { bridge.TryInitialize(); } catch { }
                }
            });

            // Reader threads
            var readers = new Task[ConcurrentThreads];
            for (int t = 0; t < ConcurrentThreads; t++)
            {
                readers[t] = Task.Run(() =>
                {
                    while (!cts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            string msg = bridge.GetStatusMessage();
                            Interlocked.Increment(ref readCount);
                            if (!validMessages.Contains(msg))
                                invalidMessages.Add(msg);
                        }
                        catch (AccessViolationException)
                        {
                            Interlocked.Increment(ref aveCount);
                        }
                        catch { }
                    }
                });
            }

            Task.WaitAll(readers.Append(toggler).ToArray());
            sw.Stop();
            try { bridge.TryShutdown(); } catch { }

            Console.WriteLine($"  Status reads: {readCount}, Invalid messages: {invalidMessages.Count}, AVE: {aveCount}");
            Console.WriteLine($"  Elapsed: {sw.ElapsedMilliseconds} ms");
            Console.WriteLine();
        }

        /// <summary>
        /// C-09: One thread performs continuous read/write while another calls Shutdown.
        /// Expects: read/write returns -1 after shutdown, no crash.
        /// </summary>
        [HandleProcessCorruptedStateExceptions]
        public static void C09_ShutdownInterruptsReadWrite()
        {
            Console.WriteLine("=== C-09: Shutdown Interrupts Read/Write ===");
            var bridge = new HardwareBridgeTestWrapper();
            bridge.Initialize();
            bridge.WriteData("interrupt_key", "some_data");
            var sw = Stopwatch.StartNew();

            int opsBeforeShutdown = 0, opsAfterShutdown = 0, errors = 0, aveCount = 0;
            var shutdownEvent = new ManualResetEventSlim(false);

            // Worker thread: continuous read/write
            var worker = Task.Run(() =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    try
                    {
                        bridge.WriteData("interrupt_key", $"iteration_{i}");
                        bridge.ReadData("interrupt_key");
                        if (!shutdownEvent.IsSet)
                            Interlocked.Increment(ref opsBeforeShutdown);
                        else
                            Interlocked.Increment(ref opsAfterShutdown);
                    }
                    catch (AccessViolationException)
                    {
                        Interlocked.Increment(ref aveCount);
                    }
                    catch (InvalidOperationException)
                    {
                        Interlocked.Increment(ref errors);
                    }
                }
            });

            // Shutdown thread: waits briefly then shuts down
            var shutdowner = Task.Run(async () =>
            {
                await Task.Delay(50);
                shutdownEvent.Set();
                try { bridge.Shutdown(); } catch { }
            });

            Task.WaitAll(worker, shutdowner);
            sw.Stop();

            Console.WriteLine($"  Ops before shutdown: {opsBeforeShutdown}");
            Console.WriteLine($"  Ops after shutdown: {opsAfterShutdown}");
            Console.WriteLine($"  Expected errors (not ready): {errors}, AVE: {aveCount}");
            Console.WriteLine($"  Elapsed: {sw.ElapsedMilliseconds} ms");
            Console.WriteLine();
        }

        /// <summary>
        /// C-10: Repeated Init → Write → Shutdown cycle 1000 times across multiple threads.
        /// Expects: no memory leak, no crash.
        /// </summary>
        [HandleProcessCorruptedStateExceptions]
        public static void C10_ReInitializationCycle()
        {
            Console.WriteLine($"=== C-10: Re-Initialization Cycle ({InitShutdownCycles} iterations) ===");
            var sw = Stopwatch.StartNew();

            int successCycles = 0, failCycles = 0, aveCount = 0;

            Parallel.For(0, InitShutdownCycles, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 2 }, i =>
            {
                var bridge = new HardwareBridgeTestWrapper();
                try
                {
                    bridge.TryInitialize();
                    bridge.TryWriteData($"cycle_{i}", $"data_{i}");
                    bridge.TryShutdown();
                    Interlocked.Increment(ref successCycles);
                }
                catch (AccessViolationException)
                {
                    Interlocked.Increment(ref aveCount);
                }
                catch
                {
                    Interlocked.Increment(ref failCycles);
                }
            });

            sw.Stop();

            Console.WriteLine($"  Success cycles: {successCycles}, Failed: {failCycles}, AVE: {aveCount}");
            Console.WriteLine($"  Elapsed: {sw.ElapsedMilliseconds} ms");
            Console.WriteLine();
        }

        // =====================================================================
        // Entry point
        // =====================================================================
        public static void Main(string[] args)
        {
            Console.WriteLine("╔══════════════════════════════════════════════════════╗");
            Console.WriteLine("║  DMC HardwareLogic Concurrency Stress Tests         ║");
            Console.WriteLine("║  Threads: 100 | Native lib: libHardwareLogic.so     ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════╝");
            Console.WriteLine();

            var totalSw = Stopwatch.StartNew();

            C01_ParallelWriteSameKey();
            C02_ParallelReadWriteSameKey();
            C03_ParallelWriteDifferentKeys();
            C04_ParallelInitShutdownRace();
            C05_ReadStorm();
            C06_ReadWriteStressTest();
            C07_GetLastErrorThreadSafety();
            C08_GetStatusMessageRace();
            C09_ShutdownInterruptsReadWrite();
            C10_ReInitializationCycle();

            totalSw.Stop();
            Console.WriteLine("══════════════════════════════════════════════════════");
            Console.WriteLine($"All tests completed. Total elapsed: {totalSw.ElapsedMilliseconds} ms");
        }
    }

    // =========================================================================
    // Thin wrapper around native P/Invoke (mirrors HardwareBridge but adds
    // try-variants for tests that expect failures)
    // =========================================================================
    internal class HardwareBridgeTestWrapper
    {
        private const string LibName = "libHardwareLogic";

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int HW_Initialize();

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int HW_Shutdown();

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int HW_GetStatus();

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr HW_GetStatusMessage();

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int HW_ReadData(
            [MarshalAs(UnmanagedType.LPStr)] string elementId,
            [Out] byte[] buffer,
            int bufferSize);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern int HW_WriteData(
            [MarshalAs(UnmanagedType.LPStr)] string elementId,
            [MarshalAs(UnmanagedType.LPStr)] string data,
            int dataSize);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr HW_GetLastError();

        public void Initialize()
        {
            int result = HW_Initialize();
            if (result != 0)
                throw new InvalidOperationException($"Init failed: {GetLastError()}");
        }

        public bool TryInitialize()
        {
            return HW_Initialize() == 0;
        }

        public void Shutdown()
        {
            int result = HW_Shutdown();
            if (result != 0)
                throw new InvalidOperationException($"Shutdown failed: {GetLastError()}");
        }

        public bool TryShutdown()
        {
            return HW_Shutdown() == 0;
        }

        public string ReadData(string elementId)
        {
            byte[] buffer = new byte[4096];
            int bytesRead = HW_ReadData(elementId, buffer, buffer.Length);
            if (bytesRead < 0)
                throw new InvalidOperationException($"Read failed: {GetLastError()}");
            return Encoding.UTF8.GetString(buffer, 0, bytesRead);
        }

        public void WriteData(string elementId, string data)
        {
            int result = HW_WriteData(elementId, data, data.Length);
            if (result != 0)
                throw new InvalidOperationException($"Write failed: {GetLastError()}");
        }

        public bool TryWriteData(string elementId, string data)
        {
            return HW_WriteData(elementId, data, data.Length) == 0;
        }

        public string GetLastError()
        {
            IntPtr ptr = HW_GetLastError();
            return Marshal.PtrToStringAnsi(ptr) ?? "";
        }

        public string GetStatusMessage()
        {
            IntPtr ptr = HW_GetStatusMessage();
            return Marshal.PtrToStringAnsi(ptr) ?? "Unknown";
        }
    }
}
