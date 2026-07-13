using Grpc.Core;
using Grpc.Net.Client;
using DMC.Grpc;
using KeyValuePair = DMC.Grpc.KeyValuePair;

namespace DMC.Client
{
    /// <summary>
    /// Interactive gRPC client for DMC Server.
    /// Connects to the server and provides Register/Update/Print/PrintAll/BatchRegister commands.
    /// </summary>
    public class DMCClient
    {
        private readonly DMCService.DMCServiceClient _client;
        private readonly GrpcChannel _channel;

        public DMCClient(string serverAddress)
        {
            _channel = GrpcChannel.ForAddress(serverAddress);
            _client = new DMCService.DMCServiceClient(_channel);
        }

        public async Task RunInteractive()
        {
            Console.WriteLine($"Connected to DMC Server.");
            Console.WriteLine("Commands: register, update, print, printall, batch, count, contains, quit");
            Console.WriteLine();

            bool running = true;
            while (running)
            {
                Console.Write("CLIENT> ");
                string? input = Console.ReadLine()?.Trim().ToLower();

                if (string.IsNullOrEmpty(input))
                    continue;

                try
                {
                    switch (input)
                    {
                        case "register":
                            await HandleRegister();
                            break;
                        case "update":
                            await HandleUpdate();
                            break;
                        case "print":
                            await HandlePrint();
                            break;
                        case "printall":
                            await HandlePrintAll();
                            break;
                        case "batch":
                            await HandleBatchRegister();
                            break;
                        case "count":
                            await HandleCount();
                            break;
                        case "contains":
                            await HandleContains();
                            break;
                        case "quit":
                        case "exit":
                            running = false;
                            break;
                        case "help":
                            PrintHelp();
                            break;
                        default:
                            Console.WriteLine($"Unknown command: '{input}'. Type 'help' for commands.");
                            break;
                    }
                }
                catch (RpcException ex)
                {
                    Console.WriteLine($"  gRPC Error: {ex.Status.Detail}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  Error: {ex.Message}");
                }

                Console.WriteLine();
            }

            _channel.Dispose();
            Console.WriteLine("Client disconnected.");
        }

        private async Task HandleRegister()
        {
            Console.Write("  Type: ");
            string? type = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(type)) return;

            var props = ReadProperties();
            if (props.Count == 0) return;

            Console.Write($"  Key property [{props[0].Key}]: ");
            string? keyProp = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(keyProp))
                keyProp = props[0].Key;

            var request = new RegisterRequest { Type = type, KeyProperty = keyProp };
            request.Properties.AddRange(props);

            var response = await _client.RegisterAsync(request);
            Console.WriteLine($"  Result: {response.Message}");
            Console.WriteLine($"  Key: {response.GeneratedKey}");
        }

        private async Task HandleUpdate()
        {
            Console.Write("  Key to update: ");
            string? key = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(key)) return;

            Console.Write("  Type: ");
            string? type = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(type)) return;

            var props = ReadProperties();
            if (props.Count == 0) return;

            Console.Write($"  Key property [{props[0].Key}]: ");
            string? keyProp = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(keyProp))
                keyProp = props[0].Key;

            var request = new UpdateRequest { Type = type, KeyProperty = keyProp };
            request.Properties.AddRange(props);

            var response = await _client.UpdateAsync(request);
            Console.WriteLine($"  Result: {response.Message}");
            Console.WriteLine($"  Key: {response.Key}");
        }

        private async Task HandlePrint()
        {
            Console.Write("  Key: ");
            string? key = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(key)) return;

            var response = await _client.PrintAsync(new PrintRequest { Key = key });
            if (response.Found)
                Console.WriteLine($"  {response.Display}");
            else
                Console.WriteLine($"  Element '{key}' not found.");
        }

        private async Task HandlePrintAll()
        {
            Console.WriteLine("  --- All Elements (streaming) ---");
            int count = 0;

            using var call = _client.PrintAll(new PrintAllRequest());
            await foreach (var msg in call.ResponseStream.ReadAllAsync())
            {
                Console.WriteLine($"  {msg.Display}");
                count++;
            }

            Console.WriteLine($"  Total: {count} element(s)");
        }

        private async Task HandleBatchRegister()
        {
            Console.WriteLine("  Batch mode: enter elements one by one. Type 'done' to send batch.");
            var requests = new List<RegisterRequest>();

            while (true)
            {
                Console.Write($"  [{requests.Count + 1}] Type (or 'done'): ");
                string? type = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(type) || type.ToLower() == "done")
                    break;

                var props = ReadProperties();
                if (props.Count == 0) continue;

                Console.Write($"    Key property [{props[0].Key}]: ");
                string? keyProp = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(keyProp))
                    keyProp = props[0].Key;

                var req = new RegisterRequest { Type = type, KeyProperty = keyProp };
                req.Properties.AddRange(props);
                requests.Add(req);
            }

            if (requests.Count == 0)
            {
                Console.WriteLine("  No elements to send.");
                return;
            }

            Console.WriteLine($"  Sending {requests.Count} elements...");

            using var call = _client.BatchRegister();
            foreach (var req in requests)
            {
                await call.RequestStream.WriteAsync(req);
            }
            await call.RequestStream.CompleteAsync();

            var response = await call.ResponseAsync;
            Console.WriteLine($"  Received: {response.TotalReceived}");
            Console.WriteLine($"  Registered: {response.TotalRegistered}");
            Console.WriteLine($"  Updated: {response.TotalUpdated}");
        }

        private async Task HandleCount()
        {
            var response = await _client.GetCountAsync(new Empty());
            Console.WriteLine($"  Elements in system: {response.Count}");
        }

        private async Task HandleContains()
        {
            Console.Write("  Key: ");
            string? key = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(key)) return;

            var response = await _client.ContainsAsync(new ContainsRequest { Key = key });
            Console.WriteLine($"  Exists: {response.Exists}");
        }

        private List<KeyValuePair> ReadProperties()
        {
            Console.WriteLine("  Properties (key=value, empty to finish):");
            var props = new List<KeyValuePair>();
            while (true)
            {
                Console.Write("    ");
                string? line = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(line)) break;

                int eq = line.IndexOf('=');
                if (eq > 0)
                {
                    props.Add(new KeyValuePair
                    {
                        Key = line[..eq].Trim(),
                        Value = line[(eq + 1)..].Trim()
                    });
                }
                else
                {
                    Console.WriteLine("    Invalid format. Use: key=value");
                }
            }
            return props;
        }

        private void PrintHelp()
        {
            Console.WriteLine("  Commands:");
            Console.WriteLine("    register  - Register a new Data Element");
            Console.WriteLine("    update    - Update an existing Data Element");
            Console.WriteLine("    print     - Print a single element by key");
            Console.WriteLine("    printall  - Stream all elements from server");
            Console.WriteLine("    batch     - Batch register multiple elements (client streaming)");
            Console.WriteLine("    count     - Get element count");
            Console.WriteLine("    contains  - Check if element exists");
            Console.WriteLine("    quit      - Disconnect");
        }
    }
}
