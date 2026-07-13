using System.Diagnostics;
using Grpc.Core;
using Grpc.Net.Client;
using DMC.Grpc;
using KeyValuePair = DMC.Grpc.KeyValuePair;

namespace DMC.Tests
{
    /// <summary>
    /// Automated integration tests for DMC gRPC Client-Server communication.
    /// Requires a running DMC Server (e.g., /opt/dmc/run.sh --server --port 5050).
    /// </summary>
    public class GrpcIntegrationTests
    {
        private readonly DMCService.DMCServiceClient _client;
        private readonly GrpcChannel _channel;
        private int _passed;
        private int _failed;

        public GrpcIntegrationTests(string serverAddress)
        {
            var handler = new SocketsHttpHandler
            {
                EnableMultipleHttp2Connections = true
            };

            _channel = GrpcChannel.ForAddress(serverAddress, new GrpcChannelOptions
            {
                HttpHandler = handler
            });
            _client = new DMCService.DMCServiceClient(_channel);
        }

        // =====================================================================
        // Test Cases
        // =====================================================================

        /// <summary>
        /// T-01: Register a new element, verify it returns success and correct key.
        /// </summary>
        public async Task T01_RegisterNewElement()
        {
            Console.WriteLine("=== T-01: Register New Element ===");

            var request = new RegisterRequest
            {
                Type = "Car",
                KeyProperty = "Make"
            };
            request.Properties.Add(new KeyValuePair { Key = "Make", Value = "Toyota" });
            request.Properties.Add(new KeyValuePair { Key = "Year", Value = "2024" });

            var response = await _client.RegisterAsync(request);

            Assert(response.Success, "Register should succeed");
            Assert(response.GeneratedKey == "Car:Toyota", $"Key should be 'Car:Toyota', got '{response.GeneratedKey}'");
            Console.WriteLine($"  Key: {response.GeneratedKey}");
            Console.WriteLine();
        }

        /// <summary>
        /// T-02: Register a duplicate key, verify it routes to update.
        /// </summary>
        public async Task T02_RegisterDuplicate()
        {
            Console.WriteLine("=== T-02: Register Duplicate (routes to Update) ===");

            var request = new RegisterRequest
            {
                Type = "Car",
                KeyProperty = "Make"
            };
            request.Properties.Add(new KeyValuePair { Key = "Make", Value = "Toyota" });
            request.Properties.Add(new KeyValuePair { Key = "Year", Value = "2025" });

            var response = await _client.RegisterAsync(request);

            // Should still succeed (routed to update)
            Assert(response.Success || response.GeneratedKey == "Car:Toyota",
                $"Duplicate register should route to update, got: {response.Message}");
            Console.WriteLine($"  Message: {response.Message}");
            Console.WriteLine();
        }

        /// <summary>
        /// T-03: Contains check for existing and non-existing keys.
        /// </summary>
        public async Task T03_Contains()
        {
            Console.WriteLine("=== T-03: Contains ===");

            var exists = await _client.ContainsAsync(new ContainsRequest { Key = "Car:Toyota" });
            Assert(exists.Exists, "Car:Toyota should exist");

            var notExists = await _client.ContainsAsync(new ContainsRequest { Key = "Car:Honda" });
            Assert(!notExists.Exists, "Car:Honda should not exist");

            Console.WriteLine();
        }

        /// <summary>
        /// T-04: GetCount returns correct number.
        /// </summary>
        public async Task T04_GetCount()
        {
            Console.WriteLine("=== T-04: GetCount ===");

            var response = await _client.GetCountAsync(new Empty());
            Assert(response.Count >= 1, $"Count should be >= 1, got {response.Count}");
            Console.WriteLine($"  Count: {response.Count}");
            Console.WriteLine();
        }

        /// <summary>
        /// T-05: Print a single element, verify display string.
        /// </summary>
        public async Task T05_Print()
        {
            Console.WriteLine("=== T-05: Print Single Element ===");

            var response = await _client.PrintAsync(new PrintRequest { Key = "Car:Toyota" });
            Assert(response.Found, "Car:Toyota should be found");
            Assert(response.Display.Contains("Car") && response.Display.Contains("Toyota"),
                $"Display should contain 'Car' and 'Toyota', got: '{response.Display}'");
            Console.WriteLine($"  Display: {response.Display}");
            Console.WriteLine();
        }

        /// <summary>
        /// T-06: Print non-existing key returns not found.
        /// </summary>
        public async Task T06_PrintNotFound()
        {
            Console.WriteLine("=== T-06: Print Not Found ===");

            var response = await _client.PrintAsync(new PrintRequest { Key = "NoSuch:Element" });
            Assert(!response.Found, "Non-existing element should return Found=false");
            Console.WriteLine();
        }

        /// <summary>
        /// T-07: Update an existing element.
        /// </summary>
        public async Task T07_Update()
        {
            Console.WriteLine("=== T-07: Update Existing Element ===");

            var request = new UpdateRequest
            {
                Type = "Car",
                KeyProperty = "Make"
            };
            request.Properties.Add(new KeyValuePair { Key = "Make", Value = "Toyota" });
            request.Properties.Add(new KeyValuePair { Key = "Year", Value = "2026" });
            request.Properties.Add(new KeyValuePair { Key = "Color", Value = "Red" });

            var response = await _client.UpdateAsync(request);
            Assert(response.Success, $"Update should succeed, got: {response.Message}");

            // Verify the update
            var print = await _client.PrintAsync(new PrintRequest { Key = "Car:Toyota" });
            Assert(print.Display.Contains("2026"), "Updated element should show Year=2026");
            Assert(print.Display.Contains("Red"), "Updated element should show Color=Red");
            Console.WriteLine($"  Updated display: {print.Display}");
            Console.WriteLine();
        }

        /// <summary>
        /// T-08: Update non-existing element returns failure.
        /// </summary>
        public async Task T08_UpdateNotFound()
        {
            Console.WriteLine("=== T-08: Update Non-Existing Element ===");

            var request = new UpdateRequest
            {
                Type = "Ghost",
                KeyProperty = "Name"
            };
            request.Properties.Add(new KeyValuePair { Key = "Name", Value = "Nobody" });

            var response = await _client.UpdateAsync(request);
            Assert(!response.Success, "Update non-existing should return false");
            Console.WriteLine($"  Message: {response.Message}");
            Console.WriteLine();
        }

        /// <summary>
        /// T-09: PrintAll (server streaming) returns all elements.
        /// </summary>
        public async Task T09_PrintAll()
        {
            Console.WriteLine("=== T-09: PrintAll (Server Streaming) ===");

            int count = 0;
            using var call = _client.PrintAll(new PrintAllRequest());
            await foreach (var msg in call.ResponseStream.ReadAllAsync())
            {
                Console.WriteLine($"  [{count}] {msg.Display}");
                count++;
            }

            Assert(count >= 1, $"PrintAll should return >= 1 element, got {count}");
            Console.WriteLine($"  Total streamed: {count}");
            Console.WriteLine();
        }

        /// <summary>
        /// T-10: BatchRegister (client streaming) sends multiple elements.
        /// </summary>
        public async Task T10_BatchRegister()
        {
            Console.WriteLine("=== T-10: BatchRegister (Client Streaming) ===");

            using var call = _client.BatchRegister();

            // Send 3 elements
            for (int i = 1; i <= 3; i++)
            {
                var req = new RegisterRequest
                {
                    Type = "Sensor",
                    KeyProperty = "Id"
                };
                req.Properties.Add(new KeyValuePair { Key = "Id", Value = $"S{i:D3}" });
                req.Properties.Add(new KeyValuePair { Key = "Value", Value = $"{i * 10.5}" });

                await call.RequestStream.WriteAsync(req);
            }

            await call.RequestStream.CompleteAsync();
            var response = await call;

            Assert(response.TotalReceived == 3, $"Should receive 3, got {response.TotalReceived}");
            Assert(response.TotalRegistered + response.TotalUpdated == 3,
                $"Registered+Updated should be 3, got {response.TotalRegistered}+{response.TotalUpdated}");
            Console.WriteLine($"  Received: {response.TotalReceived}, Registered: {response.TotalRegistered}, Updated: {response.TotalUpdated}");
            Console.WriteLine($"  Keys: {string.Join(", ", response.Keys)}");
            Console.WriteLine();
        }

        /// <summary>
        /// T-11: Verify final count after all operations.
        /// </summary>
        public async Task T11_FinalCount()
        {
            Console.WriteLine("=== T-11: Final Count Verification ===");

            var response = await _client.GetCountAsync(new Empty());
            // Should have: Car:Toyota + 3 Sensors = at least 4
            Assert(response.Count >= 4, $"Final count should be >= 4, got {response.Count}");
            Console.WriteLine($"  Final count: {response.Count}");
            Console.WriteLine();
        }

        /// <summary>
        /// T-12: gRPC error handling — invalid request.
        /// </summary>
        public async Task T12_ErrorHandling()
        {
            Console.WriteLine("=== T-12: Error Handling ===");

            // Print with empty key
            var response = await _client.PrintAsync(new PrintRequest { Key = "" });
            Assert(!response.Found, "Empty key should return Found=false");

            // Contains with empty key
            var contains = await _client.ContainsAsync(new ContainsRequest { Key = "" });
            Assert(!contains.Exists, "Empty key should not exist");

            Console.WriteLine("  Empty key requests handled gracefully.");
            Console.WriteLine();
        }

        // =====================================================================
        // Runner
        // =====================================================================

        public async Task RunAll()
        {
            var sw = Stopwatch.StartNew();

            await T01_RegisterNewElement();
            await T02_RegisterDuplicate();
            await T03_Contains();
            await T04_GetCount();
            await T05_Print();
            await T06_PrintNotFound();
            await T07_Update();
            await T08_UpdateNotFound();
            await T09_PrintAll();
            await T10_BatchRegister();
            await T11_FinalCount();
            await T12_ErrorHandling();

            sw.Stop();

            Console.WriteLine("══════════════════════════════════════════════════════");
            Console.WriteLine($"  Results: {_passed} passed, {_failed} failed");
            Console.WriteLine($"  Elapsed: {sw.ElapsedMilliseconds} ms");
            Console.WriteLine("══════════════════════════════════════════════════════");
        }

        public void Dispose()
        {
            _channel.Dispose();
        }

        public int ExitCode => _failed > 0 ? 1 : 0;

        // =====================================================================
        // Helpers
        // =====================================================================

        private void Assert(bool condition, string message)
        {
            if (condition)
            {
                Console.WriteLine($"  [PASS] {message}");
                _passed++;
            }
            else
            {
                Console.WriteLine($"  [FAIL] {message}");
                _failed++;
            }
        }

        // =====================================================================
        // Entry Point
        // =====================================================================

        public static async Task<int> Main(string[] args)
        {
            // Allow HTTP/2 over plaintext (no TLS) — required for gRPC without HTTPS
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

            string address = "http://localhost:5050";
            if (args.Length > 0)
                address = args[0];

            Console.WriteLine("╔══════════════════════════════════════════════════════╗");
            Console.WriteLine("║  DMC gRPC Integration Tests                         ║");
            Console.WriteLine($"║  Server: {address,-42}║");
            Console.WriteLine("╚══════════════════════════════════════════════════════╝");
            Console.WriteLine();

            var tests = new GrpcIntegrationTests(address);

            try
            {
                await tests.RunAll();
            }
            catch (RpcException ex)
            {
                Console.WriteLine($"\n[FATAL] gRPC connection failed: {ex.Status.Detail}");
                Console.WriteLine("Is the DMC Server running?");
                return 2;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[FATAL] {ex.Message}");
                return 2;
            }
            finally
            {
                tests.Dispose();
            }

            return tests.ExitCode;
        }
    }
}
