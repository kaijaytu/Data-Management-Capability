using Grpc.Core;
using Grpc.Net.Client;
using DMC.Grpc;
using KeyValuePair = DMC.Grpc.KeyValuePair;

namespace DMC.Client
{
    /// <summary>
    /// Interactive gRPC client for DMC Server.
    /// V2: Schema (define-type) + Data (set, search, print, printall, batch).
    /// </summary>
    public class DMCClient
    {
        private readonly DMCService.DMCServiceClient _client;
        private readonly GrpcChannel _channel;
        private readonly string _clientId;

        public DMCClient(string serverAddress, string clientId = "")
        {
            _channel = GrpcChannel.ForAddress(serverAddress);
            _client = new DMCService.DMCServiceClient(_channel);
            _clientId = string.IsNullOrEmpty(clientId)
                ? $"client-{Environment.ProcessId}"
                : clientId;
        }

        private Metadata Headers => new Metadata { { "client-id", _clientId } };

        public async Task RunInteractive()
        {
            Console.WriteLine($"Connected to DMC Server. (client-id: {_clientId})");
            Console.WriteLine("Commands: define-type, update-schema, schema, set, search, print, printall, batch, count, contains, help, quit");
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
                        case "define-type":
                            await HandleDefineType();
                            break;
                        case "update-schema":
                            await HandleUpdateSchema();
                            break;
                        case "schema":
                            await HandleGetSchema();
                            break;
                        case "set":
                            await HandleSet();
                            break;
                        case "search":
                            await HandleSearch();
                            break;
                        case "print":
                            await HandlePrint();
                            break;
                        case "printall":
                            await HandlePrintAll();
                            break;
                        case "batch":
                            await HandleBatchSet();
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

        // =================================================================
        // Schema Commands
        // =================================================================

        private async Task HandleDefineType()
        {
            Console.Write("  Type: ");
            string? type = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(type)) return;

            Console.Write("  IdentityKeys (comma-separated, e.g. Make,Model): ");
            string? keys = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(keys)) return;

            var identityKeys = keys.Split(',').Select(k => k.Trim()).Where(k => k.Length > 0).ToList();
            if (identityKeys.Count == 0) return;

            var request = new DefineTypeRequest { Type = type };
            request.IdentityKeys.AddRange(identityKeys);

            var response = await _client.DefineTypeAsync(request, Headers);
            Console.WriteLine($"  {response.Message}");
        }

        private async Task HandleGetSchema()
        {
            Console.Write("  Type: ");
            string? type = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(type)) return;

            var response = await _client.GetTypeSchemaAsync(new GetTypeSchemaRequest { Type = type }, Headers);
            if (response.Found)
                Console.WriteLine($"  {response.Type}: IdentityKeys=[{string.Join(", ", response.IdentityKeys)}]");
            else
                Console.WriteLine($"  Type '{type}' not defined.");
        }

        private async Task HandleUpdateSchema()
        {
            Console.Write("  Type: ");
            string? type = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(type)) return;

            Console.Write("  New IdentityKeys (comma-separated): ");
            string? keys = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(keys)) return;

            var newKeys = keys.Split(',').Select(k => k.Trim()).Where(k => k.Length > 0).ToList();
            if (newKeys.Count == 0) return;

            var request = new UpdateTypeSchemaRequest { Type = type };
            request.NewIdentityKeys.AddRange(newKeys);

            var response = await _client.UpdateTypeSchemaAsync(request, Headers);
            Console.WriteLine($"  {response.Message}");
            if (response.Conflicts.Count > 0)
            {
                Console.WriteLine("  Conflicts:");
                foreach (var c in response.Conflicts)
                    Console.WriteLine($"    {c.KeyA} ↔ {c.KeyB}");
            }
        }

        // =================================================================
        // Data Commands
        // =================================================================

        private async Task HandleSet()
        {
            Console.Write("  Key (empty for new): ");
            string? key = Console.ReadLine()?.Trim();

            Console.Write("  Type: ");
            string? type = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(type)) return;

            var props = ReadProperties();
            if (props.Count == 0) return;

            var request = new SetElementRequest { Type = type };
            if (!string.IsNullOrEmpty(key))
                request.Key = key;
            request.Properties.AddRange(props);

            var response = await _client.SetElementAsync(request, Headers);
            Console.WriteLine($"  Action: {response.Action}");
            Console.WriteLine($"  Key: {response.Key}");
            Console.WriteLine($"  Message: {response.Message}");
        }

        private async Task HandleSearch()
        {
            Console.Write("  Type (empty for all): ");
            string? type = Console.ReadLine()?.Trim();

            Console.WriteLine("  Filters (key=value, empty to finish):");
            var filters = ReadProperties();

            var request = new SearchRequest();
            if (!string.IsNullOrEmpty(type))
                request.Type = type;
            request.Filters.AddRange(filters);

            var response = await _client.SearchAsync(request, Headers);
            Console.WriteLine($"  --- Results ({response.TotalFound} found) ---");
            foreach (var msg in response.Results)
                Console.WriteLine($"  [{msg.Key}] {msg.Display}");
        }

        private async Task HandlePrint()
        {
            Console.Write("  Key: ");
            string? key = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(key)) return;

            var response = await _client.PrintAsync(new PrintRequest { Key = key }, Headers);
            if (response.Found)
                Console.WriteLine($"  {response.Display}");
            else
                Console.WriteLine($"  Element '{key}' not found.");
        }

        private async Task HandlePrintAll()
        {
            Console.WriteLine("  --- All Elements (streaming) ---");
            int count = 0;

            using var call = _client.PrintAll(new PrintAllRequest(), Headers);
            await foreach (var msg in call.ResponseStream.ReadAllAsync())
            {
                Console.WriteLine($"  {msg.Display}");
                count++;
            }

            Console.WriteLine($"  Total: {count} element(s)");
        }

        private async Task HandleBatchSet()
        {
            Console.WriteLine("  Batch mode: enter elements one by one. Type 'done' to send batch.");
            var requests = new List<SetElementRequest>();

            while (true)
            {
                Console.Write($"  [{requests.Count + 1}] Type (or 'done'): ");
                string? type = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(type) || type.ToLower() == "done")
                    break;

                var props = ReadProperties();
                if (props.Count == 0) continue;

                var req = new SetElementRequest { Type = type };
                req.Properties.AddRange(props);
                requests.Add(req);
            }

            if (requests.Count == 0)
            {
                Console.WriteLine("  No elements to send.");
                return;
            }

            Console.WriteLine($"  Sending {requests.Count} elements...");

            using var call = _client.BatchSet(Headers);
            foreach (var req in requests)
                await call.RequestStream.WriteAsync(req);
            await call.RequestStream.CompleteAsync();

            var response = await call.ResponseAsync;
            Console.WriteLine($"  Received: {response.TotalReceived}");
            Console.WriteLine($"  Created: {response.TotalCreated}");
            Console.WriteLine($"  Updated: {response.TotalUpdated}");
        }

        private async Task HandleCount()
        {
            var response = await _client.GetCountAsync(new Empty(), Headers);
            Console.WriteLine($"  Elements in system: {response.Count}");
        }

        private async Task HandleContains()
        {
            Console.Write("  Key: ");
            string? key = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(key)) return;

            var response = await _client.ContainsAsync(new ContainsRequest { Key = key }, Headers);
            Console.WriteLine($"  Exists: {response.Exists}");
        }

        // =================================================================
        // Helpers
        // =================================================================

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
            Console.WriteLine("  Schema Commands:");
            Console.WriteLine("    define-type   - Define IdentityKeys for a Type (one-time)");
            Console.WriteLine("    update-schema - Update IdentityKeys for a Type (validates collisions)");
            Console.WriteLine("    schema        - View IdentityKeys for a Type");
            Console.WriteLine("  Data Commands:");
            Console.WriteLine("    set         - Set a Data Element (Server decides create/update)");
            Console.WriteLine("    search      - Search elements by type and/or properties");
            Console.WriteLine("    print       - Print a single element by key");
            Console.WriteLine("    printall    - Stream all elements from server");
            Console.WriteLine("    batch       - Batch set multiple elements (client streaming)");
            Console.WriteLine("    count       - Get element count");
            Console.WriteLine("    contains    - Check if element exists by key");
            Console.WriteLine("    quit        - Disconnect");
        }
    }
}
