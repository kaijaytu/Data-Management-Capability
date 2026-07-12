using DMC.Core;
using DMC.Bridge;
using DMC.Client;
using DMC.Common.DataElements;

namespace DMC
{
    class Program
    {
        static async Task Main(string[] args)
        {
            string mode = args.Length > 0 ? args[0].ToLower() : "--server";
            string address = "http://localhost:5000";

            // Parse --port option
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--port" && i + 1 < args.Length)
                    address = $"http://localhost:{args[i + 1]}";
                if (args[i] == "--address" && i + 1 < args.Length)
                    address = args[i + 1];
            }

            switch (mode)
            {
                case "--server":
                case "-s":
                    await RunServer(address);
                    break;

                case "--client":
                case "-c":
                    await RunClient(address);
                    break;

                case "--local":
                case "-l":
                    RunLocal();
                    break;

                default:
                    Console.WriteLine("Usage:");
                    Console.WriteLine("  DMC --server [--port 5000]                Start gRPC server");
                    Console.WriteLine("  DMC --client [--address http://host:port]  Connect as client");
                    Console.WriteLine("  DMC --local                               Local interactive mode (no network)");
                    break;
            }
        }

        static async Task RunServer(string address)
        {
            Console.WriteLine("=== DMC gRPC Server Starting ===");
            Console.WriteLine();

            // Initialize Hardware Bridge
            var hardware = new HardwareBridge();
            try
            {
                hardware.Initialize();
                Console.WriteLine($"Hardware Status: {hardware.GetStatusMessage()}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hardware init skipped: {ex.Message}");
            }

            // Initialize DMC Server
            var server = DMCServer.Instance;
            Console.WriteLine($"DMC Server initialized. Elements in system: {server.Count}");
            Console.WriteLine();

            // Build and start gRPC host
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls(address);
            builder.Services.AddGrpc();

            var app = builder.Build();
            app.MapGrpcService<DMCGrpcService>();

            Console.WriteLine($"gRPC Server listening on: {address}");
            Console.WriteLine("Press Ctrl+C to stop.");
            Console.WriteLine();

            // Handle graceful shutdown
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            await app.RunAsync(cts.Token);

            // Shutdown hardware
            try
            {
                hardware.Shutdown();
                Console.WriteLine("Hardware shutdown complete.");
            }
            catch { }

            Console.WriteLine("=== DMC Server Stopped ===");
        }

        static async Task RunClient(string address)
        {
            Console.WriteLine("=== DMC Client ===");
            Console.WriteLine($"Connecting to: {address}");
            Console.WriteLine();

            var client = new DMCClient(address);
            await client.RunInteractive();
        }

        static void RunLocal()
        {
            Console.WriteLine("=== DMC Local Mode ===");
            Console.WriteLine();

            var hardware = new HardwareBridge();
            try
            {
                hardware.Initialize();
                Console.WriteLine($"Hardware Status: {hardware.GetStatusMessage()}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hardware init skipped: {ex.Message}");
            }

            var server = DMCServer.Instance;
            Console.WriteLine($"DMC Server initialized. Elements in system: {server.Count}");
            Console.WriteLine();
            Console.WriteLine("Commands: register, update, print, printall, count, quit");
            Console.WriteLine();

            bool running = true;
            while (running)
            {
                Console.Write("DMC> ");
                string? input = Console.ReadLine()?.Trim().ToLower();

                if (string.IsNullOrEmpty(input))
                    continue;

                try
                {
                    switch (input)
                    {
                        case "register":
                            HandleRegister(server);
                            break;
                        case "update":
                            HandleUpdate(server);
                            break;
                        case "print":
                            HandlePrint(server);
                            break;
                        case "printall":
                            Console.WriteLine("--- All Elements in System ---");
                            server.PrintAll();
                            Console.WriteLine($"Total: {server.Count} element(s)");
                            break;
                        case "count":
                            Console.WriteLine($"Elements in system: {server.Count}");
                            break;
                        case "quit":
                        case "exit":
                            running = false;
                            break;
                        case "help":
                            Console.WriteLine("Commands: register, update, print, printall, count, quit");
                            break;
                        default:
                            Console.WriteLine($"Unknown: '{input}'. Type 'help'.");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
                Console.WriteLine();
            }

            try { hardware.Shutdown(); } catch { }
            Console.WriteLine("=== DMC Local Mode Stopped ===");
        }

        static Dictionary<string, string> ReadProperties()
        {
            Console.WriteLine("  Properties (key=value, empty to finish):");
            var props = new Dictionary<string, string>();
            while (true)
            {
                Console.Write("    ");
                string? line = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(line)) break;
                int eq = line.IndexOf('=');
                if (eq > 0)
                    props[line[..eq].Trim()] = line[(eq + 1)..].Trim();
                else
                    Console.WriteLine("    Invalid. Use: key=value");
            }
            return props;
        }

        static void HandleRegister(DMCServer server)
        {
            Console.Write("  Type: ");
            string? type = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(type)) return;

            var props = ReadProperties();
            if (props.Count == 0) { Console.WriteLine("  Need at least one property."); return; }

            Console.Write($"  Key property [{props.Keys.First()}]: ");
            string? keyProp = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(keyProp)) keyProp = props.Keys.First();
            if (!props.ContainsKey(keyProp)) { Console.WriteLine($"  '{keyProp}' not found."); return; }

            var element = new GenericDataElement(type, props, keyProp);
            server.Register(element);
            Console.WriteLine($"  Key: {element.GetKey()}");
            Console.Write("  "); element.Print();
        }

        static void HandleUpdate(DMCServer server)
        {
            Console.Write("  Key to update: ");
            string? key = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(key)) return;
            if (!server.Contains(key)) { Console.WriteLine($"  '{key}' not found."); return; }

            Console.Write("  Type: ");
            string? type = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(type)) return;

            var props = ReadProperties();
            Console.Write($"  Key property [{props.Keys.FirstOrDefault()}]: ");
            string? keyProp = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(keyProp)) keyProp = props.Keys.FirstOrDefault() ?? "";

            var element = new GenericDataElement(type, props, keyProp);
            if (element.GetKey() != key) { Console.WriteLine("  Key mismatch. Cancelled."); return; }

            bool result = server.Update(element);
            Console.WriteLine($"  Update: {result}");
            if (result) { Console.Write("  "); element.Print(); }
        }

        static void HandlePrint(DMCServer server)
        {
            Console.Write("  Key: ");
            string? key = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(key)) return;
            if (!server.Print(key)) Console.WriteLine($"  '{key}' not found.");
        }
    }
}
