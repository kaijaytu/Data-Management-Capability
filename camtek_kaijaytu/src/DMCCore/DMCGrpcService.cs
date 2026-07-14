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

        // =================================================================
        // Schema RPCs
        // =================================================================

        public override Task<DefineTypeResponse> DefineType(DefineTypeRequest request, ServerCallContext context)
        {
            try
            {
                bool result = _server.DefineType(request.Type, request.IdentityKeys.ToList());
                return Task.FromResult(new DefineTypeResponse
                {
                    Success = result,
                    Message = result
                        ? $"Type '{request.Type}' defined with IdentityKeys=[{string.Join(", ", request.IdentityKeys)}]"
                        : $"Type '{request.Type}' already defined"
                });
            }
            catch (ArgumentException ex)
            {
                return Task.FromResult(new DefineTypeResponse { Success = false, Message = ex.Message });
            }
        }

        public override Task<GetTypeSchemaResponse> GetTypeSchema(GetTypeSchemaRequest request, ServerCallContext context)
        {
            var schema = _server.GetTypeSchema(request.Type);
            var response = new GetTypeSchemaResponse
            {
                Found = schema != null,
                Type = request.Type
            };
            if (schema != null)
                response.IdentityKeys.AddRange(schema);
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
                        return Task.FromResult(new SetElementResponse
                        {
                            Success = false,
                            Key = existingKey,
                            Action = Grpc.SetAction.NotFound,
                            Message = $"Element '{existingKey}' not found"
                        });
                    }

                    _server.Update(existingKey, props, merge);
                    return Task.FromResult(new SetElementResponse
                    {
                        Success = true,
                        Key = existingKey,
                        Action = Grpc.SetAction.Updated,
                        Message = "Updated successfully"
                    });
                }

                // No key → Server decides via identity matching
                var result = _server.Set(request.Type, props, null, merge);

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

                return Task.FromResult(new SetElementResponse
                {
                    Success = true,
                    Key = result.Key,
                    Action = grpcAction,
                    Message = message
                });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new SetElementResponse
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        public override Task<SearchResponse> Search(SearchRequest request, ServerCallContext context)
        {
            var filters = request.Filters.ToDictionary(f => f.Key, f => f.Value);
            string? type = string.IsNullOrEmpty(request.Type) ? null : request.Type;

            var results = _server.Search(type, filters);
            var response = new SearchResponse();

            foreach (var element in results)
            {
                var msg = new DataElementMessage
                {
                    Key = element.Key,
                    Type = element.Type,
                    Display = element.ToDisplayString()
                };
                if (element is GenericDataElement generic)
                {
                    foreach (var prop in generic.Properties)
                        msg.Properties.Add(new KeyValuePair { Key = prop.Key, Value = prop.Value });
                }
                response.Results.Add(msg);
            }
            response.TotalFound = response.Results.Count;
            return Task.FromResult(response);
        }

        public override Task<PrintResponse> Print(PrintRequest request, ServerCallContext context)
        {
            var element = _server.Get(request.Key);
            if (element != null)
            {
                return Task.FromResult(new PrintResponse
                {
                    Found = true,
                    Display = element.ToDisplayString()
                });
            }
            return Task.FromResult(new PrintResponse { Found = false, Display = "" });
        }

        public override async Task PrintAll(PrintAllRequest request, IServerStreamWriter<DataElementMessage> responseStream, ServerCallContext context)
        {
            foreach (var element in _server.GetAll())
            {
                if (context.CancellationToken.IsCancellationRequested) break;

                var msg = new DataElementMessage
                {
                    Key = element.Key,
                    Type = element.Type,
                    Display = element.ToDisplayString()
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

            await foreach (var request in requestStream.ReadAllAsync(context.CancellationToken))
            {
                totalReceived++;
                var props = request.Properties.ToDictionary(p => p.Key, p => p.Value);
                bool merge = request.Mode != Grpc.UpdateMode.Replace;

                try
                {
                    var result = _server.Set(request.Type, props, null, merge);
                    if (result.Action == Core.SetAction.Created) totalCreated++;
                    else totalUpdated++;
                    keys.Add(result.Key);
                }
                catch { /* skip invalid entries */ }
            }

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
            return Task.FromResult(new CountResponse { Count = _server.Count });
        }

        public override Task<ContainsResponse> Contains(ContainsRequest request, ServerCallContext context)
        {
            return Task.FromResult(new ContainsResponse { Exists = _server.Contains(request.Key) });
        }
    }
}
