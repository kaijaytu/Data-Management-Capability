using Grpc.Core;
using DMC.Common;
using DMC.Common.DataElements;
using DMC.Grpc;
using KeyValuePair = DMC.Grpc.KeyValuePair;

namespace DMC.Core
{
    /// <summary>
    /// gRPC service implementation wrapping DMCServer.
    /// V2: Schema layer (DefineType) + Data layer (SetElement/Search/Print).
    /// </summary>
    public class DMCGrpcService : DMCService.DMCServiceBase
    {
        private readonly DMCServer _server;

        public DMCGrpcService()
        {
            _server = DMCServer.Instance;
        }

        private static string Timestamp => DateTime.Now.ToString("HH:mm:ss.fff");
        private static string Peer(ServerCallContext ctx) => ctx.Peer ?? "unknown";

        private static string GetClientId(ServerCallContext ctx)
        {
            var entry = ctx.RequestHeaders.FirstOrDefault(h => h.Key == "client-id");
            return entry?.Value ?? "anonymous";
        }

        private static void Log(string action, string detail, ServerCallContext ctx)
        {
            Console.WriteLine($"  [{Timestamp}] [{GetClientId(ctx)}@{Peer(ctx)}] {action,-14} {detail}");
        }

        // =================================================================
        // Schema RPCs
        // =================================================================

        public override Task<DefineTypeResponse> DefineType(DefineTypeRequest request, ServerCallContext context)
        {
            try
            {
                bool result = _server.DefineType(request.Type, request.IdentityKeys.ToList());
                string msg = result
                    ? $"Type '{request.Type}' defined with IdentityKeys=[{string.Join(", ", request.IdentityKeys)}]"
                    : $"Type '{request.Type}' already defined";

                Log("DEFINE_TYPE", result ? $"OK  {request.Type}[{string.Join(",", request.IdentityKeys)}]" : $"DUP {request.Type}", context);

                return Task.FromResult(new DefineTypeResponse { Success = result, Message = msg });
            }
            catch (ArgumentException ex)
            {
                Log("DEFINE_TYPE", $"ERR {ex.Message}", context);
                return Task.FromResult(new DefineTypeResponse { Success = false, Message = ex.Message });
            }
        }

        public override Task<GetTypeSchemaResponse> GetTypeSchema(GetTypeSchemaRequest request, ServerCallContext context)
        {
            var schema = _server.GetTypeSchema(request.Type);
            Log("GET_SCHEMA", schema != null ? $"{request.Type}[{string.Join(",", schema)}]" : $"{request.Type} not found", context);

            var response = new GetTypeSchemaResponse { Found = schema != null, Type = request.Type };
            if (schema != null) response.IdentityKeys.AddRange(schema);
            return Task.FromResult(response);
        }

        // =================================================================
        // Data RPCs
        // =================================================================

        public override Task<SetElementResponse> SetElement(SetElementRequest request, ServerCallContext context)
        {
            try
            {
                var props = request.Properties.ToDictionary(p => p.Key, p => p.Value);
                string? existingKey = string.IsNullOrEmpty(request.Key) ? null : request.Key;
                bool merge = request.Mode != Grpc.UpdateMode.Replace;

                // Explicit update by key
                if (existingKey != null)
                {
                    if (!_server.Contains(existingKey))
                    {
                        Log("SET", $"NOT_FOUND key={existingKey}", context);
                        return Task.FromResult(new SetElementResponse
                        {
                            Success = false, Key = existingKey,
                            Action = Grpc.SetAction.NotFound,
                            Message = $"Element '{existingKey}' not found"
                        });
                    }

                    _server.Update(existingKey, props, merge);
                    Log("SET", $"UPDATED  key={existingKey} (explicit, {(merge ? "merge" : "replace")})", context);
                    return Task.FromResult(new SetElementResponse
                    {
                        Success = true, Key = existingKey,
                        Action = Grpc.SetAction.Updated,
                        Message = "Updated successfully"
                    });
                }

                // No key → Server decides via identity matching
                var result = _server.Set(request.Type, props, null, merge, GetClientId(context));

                var grpcAction = result.Action switch
                {
                    Core.SetAction.Created => Grpc.SetAction.Created,
                    Core.SetAction.Updated => Grpc.SetAction.Updated,
                    _ => Grpc.SetAction.Created
                };

                string message = result.Action switch
                {
                    Core.SetAction.Created => "Created successfully",
                    Core.SetAction.Updated => $"Identity match found, updated {result.Key}",
                    _ => "OK"
                };

                Log("SET", $"{result.Action,-8} key={result.Key} type={request.Type} owner={GetClientId(context)} (count={_server.Count})", context);

                return Task.FromResult(new SetElementResponse
                {
                    Success = true, Key = result.Key,
                    Action = grpcAction, Message = message,
                    Owner = GetClientId(context)
                });
            }
            catch (Exception ex)
            {
                Log("SET", $"ERR {ex.Message}", context);
                return Task.FromResult(new SetElementResponse { Success = false, Message = ex.Message });
            }
        }

        public override Task<SearchResponse> Search(SearchRequest request, ServerCallContext context)
        {
            var filters = request.Filters.ToDictionary(f => f.Key, f => f.Value);
            string? type = string.IsNullOrEmpty(request.Type) ? null : request.Type;
            string? owner = string.IsNullOrEmpty(request.Owner) ? null : request.Owner;

            var results = _server.Search(type, filters, owner);
            var response = new SearchResponse();

            foreach (var element in results)
            {
                var msg = new DataElementMessage
                {
                    Key = element.Key, Type = element.Type,
                    Display = element.ToDisplayString(),
                    Owner = (element is GenericDataElement g) ? g.Owner : ""
                };
                if (element is GenericDataElement generic)
                {
                    foreach (var prop in generic.Properties)
                        msg.Properties.Add(new KeyValuePair { Key = prop.Key, Value = prop.Value });
                }
                response.Results.Add(msg);
            }
            response.TotalFound = response.Results.Count;

            string filterStr = filters.Count > 0
                ? string.Join(",", filters.Select(f => $"{f.Key}={f.Value}"))
                : "(none)";
            Log("SEARCH", $"type={type ?? "*"} filters={filterStr} → {response.TotalFound} found", context);

            return Task.FromResult(response);
        }

        public override Task<PrintResponse> Print(PrintRequest request, ServerCallContext context)
        {
            var element = _server.Get(request.Key);
            Log("PRINT", element != null ? $"key={request.Key} → found" : $"key={request.Key} → not found", context);

            if (element != null)
            {
                return Task.FromResult(new PrintResponse { Found = true, Display = element.ToDisplayString() });
            }
            return Task.FromResult(new PrintResponse { Found = false, Display = "" });
        }

        public override async Task PrintAll(PrintAllRequest request, IServerStreamWriter<DataElementMessage> responseStream, ServerCallContext context)
        {
            var elements = _server.GetAll();
            Log("PRINT_ALL", $"streaming {elements.Count()} element(s)", context);

            foreach (var element in elements)
            {
                if (context.CancellationToken.IsCancellationRequested) break;

                var msg = new DataElementMessage
                {
                    Key = element.Key, Type = element.Type,
                    Display = element.ToDisplayString(),
                    Owner = (element is GenericDataElement ge) ? ge.Owner : ""
                };
                if (element is GenericDataElement generic)
                {
                    foreach (var prop in generic.Properties)
                        msg.Properties.Add(new KeyValuePair { Key = prop.Key, Value = prop.Value });
                }
                await responseStream.WriteAsync(msg);
            }
        }

        public override async Task<BatchSetResponse> BatchSet(IAsyncStreamReader<SetElementRequest> requestStream, ServerCallContext context)
        {
            int totalReceived = 0, totalCreated = 0, totalUpdated = 0;
            var keys = new List<string>();
            string clientId = GetClientId(context);

            Log("BATCH_SET", $"stream started (client={clientId})", context);

            await foreach (var request in requestStream.ReadAllAsync(context.CancellationToken))
            {
                totalReceived++;
                var props = request.Properties.ToDictionary(p => p.Key, p => p.Value);
                bool merge = request.Mode != Grpc.UpdateMode.Replace;

                try
                {
                    var result = _server.Set(request.Type, props, null, merge, clientId);
                    if (result.Action == Core.SetAction.Created) totalCreated++;
                    else totalUpdated++;
                    keys.Add(result.Key);
                }
                catch { /* skip invalid entries */ }
            }

            Log("BATCH_SET", $"done: received={totalReceived} created={totalCreated} updated={totalUpdated}", context);

            var response = new BatchSetResponse
            {
                TotalReceived = totalReceived,
                TotalCreated = totalCreated,
                TotalUpdated = totalUpdated
            };
            response.Keys.AddRange(keys);
            return response;
        }

        public override Task<CountResponse> GetCount(Empty request, ServerCallContext context)
        {
            int count = _server.Count;
            Log("COUNT", $"{count}", context);
            return Task.FromResult(new CountResponse { Count = count });
        }

        public override Task<ContainsResponse> Contains(ContainsRequest request, ServerCallContext context)
        {
            bool exists = _server.Contains(request.Key);
            Log("CONTAINS", $"key={request.Key} → {exists}", context);
            return Task.FromResult(new ContainsResponse { Exists = exists });
        }
    }
}
