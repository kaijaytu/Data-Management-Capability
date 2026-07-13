using Grpc.Core;
using DMC.Common;
using DMC.Common.DataElements;
using DMC.Grpc;
using KeyValuePair = DMC.Grpc.KeyValuePair;

namespace DMC.Core
{
    /// <summary>
    /// gRPC service implementation wrapping DMCServer.
    /// Handles Register, Update, Print, PrintAll (server streaming), and BatchRegister (client streaming).
    /// </summary>
    public class DMCGrpcService : DMCService.DMCServiceBase
    {
        private readonly DMCServer _server;

        public DMCGrpcService()
        {
            _server = DMCServer.Instance;
        }

        public override Task<RegisterResponse> Register(RegisterRequest request, ServerCallContext context)
        {
            var props = request.Properties.ToDictionary(p => p.Key, p => p.Value);
            var element = new GenericDataElement(request.Type, props, request.KeyProperty);

            bool result = _server.Register(element);

            return Task.FromResult(new RegisterResponse
            {
                Success = result,
                GeneratedKey = element.GetKey(),
                Message = result ? "Registered successfully" : "Routed to update (key already existed)"
            });
        }

        public override Task<UpdateResponse> Update(UpdateRequest request, ServerCallContext context)
        {
            var props = request.Properties.ToDictionary(p => p.Key, p => p.Value);
            var element = new GenericDataElement(request.Type, props, request.KeyProperty);

            bool result = _server.Update(element);

            return Task.FromResult(new UpdateResponse
            {
                Success = result,
                Key = element.GetKey(),
                Message = result ? "Updated successfully" : "Element not found"
            });
        }

        public override Task<PrintResponse> Print(PrintRequest request, ServerCallContext context)
        {
            // We need to get the display string without printing to console
            // Check if element exists and get its display string
            if (_server.Contains(request.Key))
            {
                // Use reflection-free approach: temporarily capture
                var element = GetElement(request.Key);
                if (element != null)
                {
                    return Task.FromResult(new PrintResponse
                    {
                        Found = true,
                        Display = element.ToDisplayString()
                    });
                }
            }

            return Task.FromResult(new PrintResponse
            {
                Found = false,
                Display = ""
            });
        }

        public override async Task PrintAll(PrintAllRequest request, IServerStreamWriter<DataElementMessage> responseStream, ServerCallContext context)
        {
            var elements = GetAllElements();

            foreach (var element in elements)
            {
                if (context.CancellationToken.IsCancellationRequested)
                    break;

                var msg = new DataElementMessage
                {
                    Key = element.GetKey(),
                    Type = element.Type,
                    Display = element.ToDisplayString()
                };

                // Add properties if it's a GenericDataElement
                if (element is GenericDataElement generic)
                {
                    foreach (var prop in generic.Properties)
                    {
                        msg.Properties.Add(new KeyValuePair { Key = prop.Key, Value = prop.Value });
                    }
                }

                await responseStream.WriteAsync(msg);
            }
        }

        public override async Task<BatchRegisterResponse> BatchRegister(IAsyncStreamReader<RegisterRequest> requestStream, ServerCallContext context)
        {
            int totalReceived = 0;
            int totalRegistered = 0;
            int totalUpdated = 0;
            var keys = new List<string>();

            await foreach (var request in requestStream.ReadAllAsync(context.CancellationToken))
            {
                totalReceived++;

                var props = request.Properties.ToDictionary(p => p.Key, p => p.Value);
                var element = new GenericDataElement(request.Type, props, request.KeyProperty);
                string key = element.GetKey();

                if (_server.Contains(key))
                {
                    _server.Update(element);
                    totalUpdated++;
                }
                else
                {
                    _server.Register(element);
                    totalRegistered++;
                }

                keys.Add(key);
            }

            var response = new BatchRegisterResponse
            {
                TotalReceived = totalReceived,
                TotalRegistered = totalRegistered,
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

        // Helper: Get a single element by key (internal access to registry)
        private IDataElement? GetElement(string key)
        {
            // Access via reflection or add a method to DMCServer
            return _server.Get(key);
        }

        // Helper: Get all elements
        private IEnumerable<IDataElement> GetAllElements()
        {
            return _server.GetAll();
        }
    }
}
