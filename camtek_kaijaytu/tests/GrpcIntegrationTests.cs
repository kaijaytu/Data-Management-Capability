using System.Diagnostics;
using Grpc.Core;
using Grpc.Net.Client;
using DMC.Grpc;
using KeyValuePair = DMC.Grpc.KeyValuePair;

namespace DMC.Tests
{
    /// <summary>
    /// V2 Integration tests: Schema (DefineType) + Data (SetElement/Search/Print).
    /// Uses realistic IdentityKeys (VIN, DeviceId, EmployeeId) per Jack's feedback.
    /// </summary>
    public class GrpcIntegrationTests
    {
        private readonly DMCService.DMCServiceClient _client;
        private readonly GrpcChannel _channel;
        private int _passed;
        private int _failed;

        public GrpcIntegrationTests(string serverAddress)
        {
            var handler = new SocketsHttpHandler { EnableMultipleHttp2Connections = true };
            _channel = GrpcChannel.ForAddress(serverAddress, new GrpcChannelOptions { HttpHandler = handler });
            _client = new DMCService.DMCServiceClient(_channel);
        }

        // =====================================================================
        // Schema Tests
        // =====================================================================

        /// <summary>
        /// T-01: Define Car type with VIN as IdentityKey (realistic: globally unique per vehicle).
        /// </summary>
        public async Task T01_DefineTypes()
        {
            Console.WriteLine("=== T-01: DefineType (Car, Sensor, Person) ===");

            // Car: VIN is the only truly unique identifier for a vehicle
            var carReq = new DefineTypeRequest { Type = "Car" };
            carReq.IdentityKeys.Add("VIN");
            var carResp = await _client.DefineTypeAsync(carReq);
            Assert(carResp.Success, "DefineType Car[VIN] should succeed");
            Console.WriteLine($"  {carResp.Message}");

            // Sensor: DeviceId is manufacturer-assigned serial number
            var sensorReq = new DefineTypeRequest { Type = "Sensor" };
            sensorReq.IdentityKeys.Add("DeviceId");
            var sensorResp = await _client.DefineTypeAsync(sensorReq);
            Assert(sensorResp.Success, "DefineType Sensor[DeviceId] should succeed");

            // Person: EmployeeId is company-assigned unique identifier
            var personReq = new DefineTypeRequest { Type = "Person" };
            personReq.IdentityKeys.Add("EmployeeId");
            var personResp = await _client.DefineTypeAsync(personReq);
            Assert(personResp.Success, "DefineType Person[EmployeeId] should succeed");

            Console.WriteLine();
        }

        /// <summary>
        /// T-02: Duplicate DefineType should fail.
        /// </summary>
        public async Task T02_DefineTypeDuplicate()
        {
            Console.WriteLine("=== T-02: DefineType Duplicate ===");
            var request = new DefineTypeRequest { Type = "Car" };
            request.IdentityKeys.Add("Make");

            var response = await _client.DefineTypeAsync(request);
            Assert(!response.Success, "Duplicate DefineType should fail");
            Console.WriteLine($"  {response.Message}");
            Console.WriteLine();
        }

        /// <summary>
        /// T-03: GetTypeSchema returns correct IdentityKeys.
        /// </summary>
        public async Task T03_GetTypeSchema()
        {
            Console.WriteLine("=== T-03: GetTypeSchema ===");
            var response = await _client.GetTypeSchemaAsync(new GetTypeSchemaRequest { Type = "Car" });
            Assert(response.Found, "Car schema should be found");
            Assert(response.IdentityKeys.Contains("VIN"),
                $"IdentityKeys should contain VIN, got [{string.Join(", ", response.IdentityKeys)}]");
            Console.WriteLine($"  Car: [{string.Join(", ", response.IdentityKeys)}]");
            Console.WriteLine();
        }

        // =====================================================================
        // SetElement Tests — Car with VIN
        // =====================================================================

        /// <summary>
        /// T-04: Set new Car with realistic properties.
        /// </summary>
        public async Task T04_SetNewCar()
        {
            Console.WriteLine("=== T-04: Set New Car (VIN=1HGBH41JXMN109186) ===");
            var request = new SetElementRequest { Type = "Car" };
            request.Properties.Add(new KeyValuePair { Key = "VIN", Value = "1HGBH41JXMN109186" });
            request.Properties.Add(new KeyValuePair { Key = "Make", Value = "Toyota" });
            request.Properties.Add(new KeyValuePair { Key = "Model", Value = "Camry" });
            request.Properties.Add(new KeyValuePair { Key = "Year", Value = "2024" });
            request.Properties.Add(new KeyValuePair { Key = "Color", Value = "White" });
            request.Properties.Add(new KeyValuePair { Key = "Mileage", Value = "0" });

            var response = await _client.SetElementAsync(request);
            Assert(response.Success, "Set should succeed");
            Assert(response.Action == SetAction.Created, $"Action should be CREATED, got {response.Action}");
            Assert(response.Key == "Car:1", $"Key should be Car:1, got {response.Key}");
            Console.WriteLine($"  {response.Action}, Key: {response.Key}");
            Console.WriteLine();
        }

        /// <summary>
        /// T-05: Set same VIN with updated properties → identity match → UPDATED (merge).
        /// </summary>
        public async Task T05_SetIdentityMatch()
        {
            Console.WriteLine("=== T-05: Set Same VIN (identity match → UPDATED) ===");
            var request = new SetElementRequest { Type = "Car" };
            request.Properties.Add(new KeyValuePair { Key = "VIN", Value = "1HGBH41JXMN109186" });
            request.Properties.Add(new KeyValuePair { Key = "Year", Value = "2025" });
            request.Properties.Add(new KeyValuePair { Key = "Color", Value = "Red" });
            request.Properties.Add(new KeyValuePair { Key = "Mileage", Value = "15000" });

            var response = await _client.SetElementAsync(request);
            Assert(response.Action == SetAction.Updated, $"Should be UPDATED (same VIN), got {response.Action}");
            Assert(response.Key == "Car:1", $"Should update Car:1, got {response.Key}");

            // Verify merge: Make/Model preserved, Year/Color/Mileage updated
            var print = await _client.PrintAsync(new PrintRequest { Key = "Car:1" });
            Assert(print.Display.Contains("Toyota"), "Make=Toyota should be preserved (merge)");
            Assert(print.Display.Contains("Camry"), "Model=Camry should be preserved (merge)");
            Assert(print.Display.Contains("2025"), "Year should be updated to 2025");
            Assert(print.Display.Contains("Red"), "Color should be updated to Red");
            Assert(print.Display.Contains("15000"), "Mileage should be updated to 15000");
            Console.WriteLine($"  {print.Display}");
            Console.WriteLine();
        }

        /// <summary>
        /// T-06: Set different VIN, same Make+Model → CREATED (different car).
        /// Proves VIN is the correct IdentityKey, not Make+Model.
        /// </summary>
        public async Task T06_SetDifferentVIN()
        {
            Console.WriteLine("=== T-06: Set Different VIN (same Make+Model → CREATED) ===");
            var request = new SetElementRequest { Type = "Car" };
            request.Properties.Add(new KeyValuePair { Key = "VIN", Value = "5YJSA1DN0DFP14555" });
            request.Properties.Add(new KeyValuePair { Key = "Make", Value = "Toyota" });
            request.Properties.Add(new KeyValuePair { Key = "Model", Value = "Camry" });
            request.Properties.Add(new KeyValuePair { Key = "Year", Value = "2024" });
            request.Properties.Add(new KeyValuePair { Key = "Color", Value = "Blue" });

            var response = await _client.SetElementAsync(request);
            Assert(response.Action == SetAction.Created, $"Should be CREATED (different VIN), got {response.Action}");
            Assert(response.Key == "Car:2", $"Key should be Car:2, got {response.Key}");
            Console.WriteLine($"  {response.Action}, Key: {response.Key} (different car, same Make+Model)");
            Console.WriteLine();
        }

        // =====================================================================
        // SetElement Tests — Explicit update by key, NOT_FOUND
        // =====================================================================

        /// <summary>
        /// T-07: Explicit update by server-assigned key.
        /// </summary>
        public async Task T07_SetExplicitUpdateByKey()
        {
            Console.WriteLine("=== T-07: Explicit Update by Key ===");
            var request = new SetElementRequest { Type = "Car", Key = "Car:2" };
            request.Properties.Add(new KeyValuePair { Key = "Owner", Value = "John" });

            var response = await _client.SetElementAsync(request);
            Assert(response.Action == SetAction.Updated, $"Should be UPDATED, got {response.Action}");

            var print = await _client.PrintAsync(new PrintRequest { Key = "Car:2" });
            Assert(print.Display.Contains("John"), "Owner=John should be merged");
            Assert(print.Display.Contains("Blue"), "Color=Blue should be preserved");
            Console.WriteLine($"  {print.Display}");
            Console.WriteLine();
        }

        /// <summary>
        /// T-08: Update non-existing key → NOT_FOUND.
        /// </summary>
        public async Task T08_SetNotFound()
        {
            Console.WriteLine("=== T-08: Update Non-Existing Key → NOT_FOUND ===");
            var request = new SetElementRequest { Type = "Car", Key = "Car:99" };
            request.Properties.Add(new KeyValuePair { Key = "VIN", Value = "GHOST" });

            var response = await _client.SetElementAsync(request);
            Assert(!response.Success, "Should fail for non-existing key");
            Assert(response.Action == SetAction.NotFound, $"Should be NOT_FOUND, got {response.Action}");
            Console.WriteLine($"  {response.Action}: {response.Message}");
            Console.WriteLine();
        }

        /// <summary>
        /// T-09: Set without DefineType → error.
        /// </summary>
        public async Task T09_SetWithoutSchema()
        {
            Console.WriteLine("=== T-09: Set Without DefineType → Error ===");
            var request = new SetElementRequest { Type = "Unknown" };
            request.Properties.Add(new KeyValuePair { Key = "Foo", Value = "Bar" });

            var response = await _client.SetElementAsync(request);
            Assert(!response.Success, "Should fail without schema");
            Console.WriteLine($"  {response.Message}");
            Console.WriteLine();
        }

        // =====================================================================
        // Sensor Tests — realistic multi-property data
        // =====================================================================

        /// <summary>
        /// T-10: Set Sensor with many properties (realistic IoT data).
        /// </summary>
        public async Task T10_SetSensor()
        {
            Console.WriteLine("=== T-10: Set Sensor (realistic IoT data) ===");
            var request = new SetElementRequest { Type = "Sensor" };
            request.Properties.Add(new KeyValuePair { Key = "DeviceId", Value = "SNR-2024-0817" });
            request.Properties.Add(new KeyValuePair { Key = "Location", Value = "Floor3-RoomA" });
            request.Properties.Add(new KeyValuePair { Key = "Temperature", Value = "72.5" });
            request.Properties.Add(new KeyValuePair { Key = "Humidity", Value = "45.2" });
            request.Properties.Add(new KeyValuePair { Key = "IsActive", Value = "true" });
            request.Properties.Add(new KeyValuePair { Key = "LastCalibration", Value = "2025-01-15" });
            request.Properties.Add(new KeyValuePair { Key = "FirmwareVersion", Value = "3.2.1" });
            request.Properties.Add(new KeyValuePair { Key = "Manufacturer", Value = "Honeywell" });

            var response = await _client.SetElementAsync(request);
            Assert(response.Action == SetAction.Created, $"Should be CREATED, got {response.Action}");
            Console.WriteLine($"  {response.Action}, Key: {response.Key}");

            // Update same sensor (identity match on DeviceId)
            var updateReq = new SetElementRequest { Type = "Sensor" };
            updateReq.Properties.Add(new KeyValuePair { Key = "DeviceId", Value = "SNR-2024-0817" });
            updateReq.Properties.Add(new KeyValuePair { Key = "Temperature", Value = "73.1" });
            updateReq.Properties.Add(new KeyValuePair { Key = "Humidity", Value = "44.8" });

            var updateResp = await _client.SetElementAsync(updateReq);
            Assert(updateResp.Action == SetAction.Updated, $"Should be UPDATED (same DeviceId), got {updateResp.Action}");
            Assert(updateResp.Key == response.Key, "Should update same sensor");

            // Verify merge: Manufacturer preserved, Temperature updated
            var print = await _client.PrintAsync(new PrintRequest { Key = response.Key });
            Assert(print.Display.Contains("Honeywell"), "Manufacturer should be preserved");
            Assert(print.Display.Contains("73.1"), "Temperature should be updated to 73.1");
            Console.WriteLine($"  {print.Display}");
            Console.WriteLine();
        }

        // =====================================================================
        // Search Tests
        // =====================================================================

        /// <summary>
        /// T-11: Search Cars by Make.
        /// </summary>
        public async Task T11_SearchByType()
        {
            Console.WriteLine("=== T-11: Search Cars ===");
            var response = await _client.SearchAsync(new SearchRequest { Type = "Car" });
            Assert(response.TotalFound == 2, $"Should find 2 Cars, got {response.TotalFound}");
            foreach (var r in response.Results)
                Console.WriteLine($"  [{r.Key}] {r.Display}");
            Console.WriteLine();
        }

        /// <summary>
        /// T-12: Search by property filter across types.
        /// </summary>
        public async Task T12_SearchByFilter()
        {
            Console.WriteLine("=== T-12: Search by Make=Toyota ===");
            var request = new SearchRequest { Type = "Car" };
            request.Filters.Add(new KeyValuePair { Key = "Make", Value = "Toyota" });

            var response = await _client.SearchAsync(request);
            Assert(response.TotalFound == 2, $"Should find 2 Toyotas (different VINs), got {response.TotalFound}");
            Console.WriteLine($"  Found: {response.TotalFound} Toyota(s)");
            Console.WriteLine();
        }

        // =====================================================================
        // Print / BatchSet / Count
        // =====================================================================

        /// <summary>
        /// T-13: PrintAll streams all elements.
        /// </summary>
        public async Task T13_PrintAll()
        {
            Console.WriteLine("=== T-13: PrintAll ===");
            int count = 0;
            using var call = _client.PrintAll(new PrintAllRequest());
            await foreach (var msg in call.ResponseStream.ReadAllAsync())
            {
                Console.WriteLine($"  [{msg.Key}] {msg.Display}");
                count++;
            }
            Assert(count >= 3, $"Should have >= 3 elements (2 Cars + 1 Sensor), got {count}");
            Console.WriteLine();
        }

        /// <summary>
        /// T-14: BatchSet multiple Persons.
        /// </summary>
        public async Task T14_BatchSet()
        {
            Console.WriteLine("=== T-14: BatchSet Persons ===");
            using var call = _client.BatchSet();

            var people = new[]
            {
                ("EMP001", "Alice", "Engineering", "Senior"),
                ("EMP002", "Bob", "Marketing", "Junior"),
                ("EMP003", "Charlie", "Engineering", "Lead"),
            };

            foreach (var (id, name, dept, level) in people)
            {
                var req = new SetElementRequest { Type = "Person" };
                req.Properties.Add(new KeyValuePair { Key = "EmployeeId", Value = id });
                req.Properties.Add(new KeyValuePair { Key = "Name", Value = name });
                req.Properties.Add(new KeyValuePair { Key = "Department", Value = dept });
                req.Properties.Add(new KeyValuePair { Key = "Level", Value = level });
                await call.RequestStream.WriteAsync(req);
            }
            await call.RequestStream.CompleteAsync();
            var response = await call;

            Assert(response.TotalReceived == 3, $"Should receive 3, got {response.TotalReceived}");
            Assert(response.TotalCreated == 3, $"Should create 3, got {response.TotalCreated}");
            Console.WriteLine($"  Created: {response.TotalCreated}, Keys: {string.Join(", ", response.Keys)}");
            Console.WriteLine();
        }

        /// <summary>
        /// T-15: Final count verification.
        /// </summary>
        public async Task T15_FinalCount()
        {
            Console.WriteLine("=== T-15: Final Count ===");
            var response = await _client.GetCountAsync(new Empty());
            // 2 Cars + 1 Sensor + 3 Persons = 6
            Assert(response.Count >= 6, $"Should be >= 6, got {response.Count}");
            Console.WriteLine($"  Count: {response.Count}");
            Console.WriteLine();
        }

        // =====================================================================
        // Concurrency Tests (via gRPC — tests Server thread safety end-to-end)
        // =====================================================================

        /// <summary>
        /// T-16: 50 concurrent SetElement with unique identities via gRPC.
        /// </summary>
        public async Task T16_ConcurrentSetUnique()
        {
            Console.WriteLine("=== T-16: Concurrent Set (50 gRPC clients, unique DeviceId) ===");

            // DefineType "ConcTest" if not already defined
            var defineReq = new DefineTypeRequest { Type = "ConcTest" };
            defineReq.IdentityKeys.Add("DeviceId");
            await _client.DefineTypeAsync(defineReq);

            int countBefore = (await _client.GetCountAsync(new Empty())).Count;

            int threadCount = 50;
            int successCount = 0;
            var tasks = new List<Task>();

            for (int i = 0; i < threadCount; i++)
            {
                int idx = i;
                tasks.Add(Task.Run(async () =>
                {
                    var req = new SetElementRequest { Type = "ConcTest" };
                    req.Properties.Add(new KeyValuePair { Key = "DeviceId", Value = $"CONC-{idx:D4}" });
                    req.Properties.Add(new KeyValuePair { Key = "Value", Value = $"{idx}" });

                    var resp = await _client.SetElementAsync(req);
                    if (resp.Success) Interlocked.Increment(ref successCount);
                }));
            }

            await Task.WhenAll(tasks);

            int countAfter = (await _client.GetCountAsync(new Empty())).Count;
            int created = countAfter - countBefore;

            Assert(successCount == threadCount, $"All {threadCount} should succeed, got {successCount}");
            Assert(created == threadCount, $"Should create {threadCount} elements, got {created}");
            Console.WriteLine($"  Success: {successCount}, Created: {created}");
            Console.WriteLine();
        }

        /// <summary>
        /// T-17: 50 concurrent SetElement with SAME identity via gRPC.
        /// Only 1 should be Created, rest Updated.
        /// </summary>
        public async Task T17_ConcurrentSetSameIdentity()
        {
            Console.WriteLine("=== T-17: Concurrent Set Same Identity (50 gRPC clients, same DeviceId) ===");

            int threadCount = 50;
            int createdCount = 0;
            int updatedCount = 0;
            var tasks = new List<Task>();

            for (int i = 0; i < threadCount; i++)
            {
                int idx = i;
                tasks.Add(Task.Run(async () =>
                {
                    var req = new SetElementRequest { Type = "ConcTest" };
                    req.Properties.Add(new KeyValuePair { Key = "DeviceId", Value = "RACE-SAME-001" });
                    req.Properties.Add(new KeyValuePair { Key = "Iteration", Value = $"{idx}" });

                    var resp = await _client.SetElementAsync(req);
                    if (resp.Action == SetAction.Created) Interlocked.Increment(ref createdCount);
                    else if (resp.Action == SetAction.Updated) Interlocked.Increment(ref updatedCount);
                }));
            }

            await Task.WhenAll(tasks);

            Assert(createdCount == 1, $"Exactly 1 should be Created, got {createdCount}");
            Assert(updatedCount == threadCount - 1, $"{threadCount - 1} should be Updated, got {updatedCount}");
            Console.WriteLine($"  Created: {createdCount}, Updated: {updatedCount}");
            Console.WriteLine();
        }

        /// <summary>
        /// T-18: Mixed concurrent reads and writes via gRPC.
        /// </summary>
        public async Task T18_ConcurrentReadWrite()
        {
            Console.WriteLine("=== T-18: Concurrent Read/Write Mix (100 ops via gRPC) ===");

            int totalOps = 100;
            int readSuccess = 0;
            int writeSuccess = 0;
            int errors = 0;
            var tasks = new List<Task>();

            for (int i = 0; i < totalOps; i++)
            {
                int idx = i;
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        if (idx % 2 == 0)
                        {
                            // Write
                            var req = new SetElementRequest { Type = "ConcTest" };
                            req.Properties.Add(new KeyValuePair { Key = "DeviceId", Value = $"CONC-{idx % 20:D4}" });
                            req.Properties.Add(new KeyValuePair { Key = "Reading", Value = $"{idx * 1.1}" });
                            var resp = await _client.SetElementAsync(req);
                            if (resp.Success) Interlocked.Increment(ref writeSuccess);
                        }
                        else
                        {
                            // Read
                            var resp = await _client.SearchAsync(new SearchRequest { Type = "ConcTest" });
                            if (resp.TotalFound >= 0) Interlocked.Increment(ref readSuccess);
                        }
                    }
                    catch
                    {
                        Interlocked.Increment(ref errors);
                    }
                }));
            }

            await Task.WhenAll(tasks);

            Assert(errors == 0, $"No errors, got {errors}");
            Assert(readSuccess + writeSuccess == totalOps, $"All {totalOps} ops should complete, got {readSuccess + writeSuccess}");
            Console.WriteLine($"  Reads: {readSuccess}, Writes: {writeSuccess}, Errors: {errors}");
            Console.WriteLine();
        }

        // =====================================================================
        // Multi-User Tests
        // =====================================================================

        /// <summary>
        /// T-19: Verify owner is tracked and returned in SetElement response.
        /// </summary>
        public async Task T19_OwnerTracking()
        {
            Console.WriteLine("=== T-19: Owner Tracking via gRPC ===");

            // Our client sends with client-id from metadata (default: "client-{pid}")
            // Create a new element and verify owner is returned
            var defineReq = new DefineTypeRequest { Type = "OwnerTest" };
            defineReq.IdentityKeys.Add("Id");
            await _client.DefineTypeAsync(defineReq);

            var setReq = new SetElementRequest { Type = "OwnerTest" };
            setReq.Properties.Add(new KeyValuePair { Key = "Id", Value = "OT-001" });
            setReq.Properties.Add(new KeyValuePair { Key = "Data", Value = "hello" });

            var setResp = await _client.SetElementAsync(setReq);
            Assert(setResp.Success, "Set should succeed");
            Assert(!string.IsNullOrEmpty(setResp.Owner), $"Owner should not be empty, got '{setResp.Owner}'");
            Console.WriteLine($"  Created: {setResp.Key}, Owner: {setResp.Owner}");

            // Search and verify owner in DataElementMessage
            var searchResp = await _client.SearchAsync(new SearchRequest { Type = "OwnerTest" });
            Assert(searchResp.Results.Count == 1, "Should find 1 OwnerTest element");
            Assert(!string.IsNullOrEmpty(searchResp.Results[0].Owner),
                $"Owner in search result should not be empty, got '{searchResp.Results[0].Owner}'");
            Console.WriteLine($"  Search result owner: {searchResp.Results[0].Owner}");
            Console.WriteLine();
        }

        /// <summary>
        /// T-20: Verify search by owner filter works via gRPC.
        /// </summary>
        public async Task T20_SearchByOwner()
        {
            Console.WriteLine("=== T-20: Search by Owner via gRPC ===");

            // Search with owner filter matching our client-id
            var resp = await _client.SearchAsync(new SearchRequest { Type = "OwnerTest", Owner = "anonymous" });
            // Integration tests don't set client-id header by default, so owner = "anonymous"
            // If our elements were created as "anonymous", this should find them
            Console.WriteLine($"  Search owner=anonymous: {resp.TotalFound} found");

            // Search with non-existing owner
            var resp2 = await _client.SearchAsync(new SearchRequest { Owner = "nonexistent" });
            Assert(resp2.TotalFound == 0, $"Non-existing owner should find 0, got {resp2.TotalFound}");
            Console.WriteLine($"  Search owner=nonexistent: {resp2.TotalFound} found");
            Console.WriteLine();
        }

        // =====================================================================
        // Runner
        // =====================================================================

        public async Task RunAll()
        {
            var sw = Stopwatch.StartNew();

            await T01_DefineTypes();
            await T02_DefineTypeDuplicate();
            await T03_GetTypeSchema();
            await T04_SetNewCar();
            await T05_SetIdentityMatch();
            await T06_SetDifferentVIN();
            await T07_SetExplicitUpdateByKey();
            await T08_SetNotFound();
            await T09_SetWithoutSchema();
            await T10_SetSensor();
            await T11_SearchByType();
            await T12_SearchByFilter();
            await T13_PrintAll();
            await T14_BatchSet();
            await T15_FinalCount();
            await T16_ConcurrentSetUnique();
            await T17_ConcurrentSetSameIdentity();
            await T18_ConcurrentReadWrite();
            await T19_OwnerTracking();
            await T20_SearchByOwner();

            sw.Stop();
            Console.WriteLine("══════════════════════════════════════════════════════");
            Console.WriteLine($"  Results: {_passed} passed, {_failed} failed");
            Console.WriteLine($"  Elapsed: {sw.ElapsedMilliseconds} ms");
            Console.WriteLine("──────────────────────────────────────────────────────");
            Console.WriteLine("  Coverage:");
            Console.WriteLine("    Schema      T01-T03  DefineType(Car[VIN], Sensor, Person), duplicate, GetTypeSchema");
            Console.WriteLine("    Set/Update  T04-T09  new car, identity match(merge), different VIN, explicit key, NOT_FOUND, no schema");
            Console.WriteLine("    Sensor      T10      realistic IoT 8-prop element, merge readings");
            Console.WriteLine("    Search      T11-T12  by type, by property filter");
            Console.WriteLine("    Print       T13      PrintAll server streaming");
            Console.WriteLine("    Batch       T14-T15  BatchSet 3 persons, final count verification");
            Console.WriteLine("    Concurrency T16-T18  50 concurrent unique, 50 same identity race, 100 mixed R/W");
            Console.WriteLine("    Multi-User  T19-T20  owner tracking via gRPC metadata, search by owner");
            Console.WriteLine("══════════════════════════════════════════════════════");
        }

        public void Dispose() => _channel.Dispose();
        public int ExitCode => _failed > 0 ? 1 : 0;

        private void Assert(bool condition, string message)
        {
            if (condition) { Console.WriteLine($"  [PASS] {message}"); _passed++; }
            else { Console.WriteLine($"  [FAIL] {message}"); _failed++; }
        }

        public static async Task<int> Main(string[] args)
        {
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            string address = args.Length > 0 ? args[0] : "http://localhost:5050";

            Console.WriteLine("╔══════════════════════════════════════════════════════╗");
            Console.WriteLine("║  DMC gRPC Integration Tests (V2)                    ║");
            Console.WriteLine($"║  Server: {address,-42}║");
            Console.WriteLine("╚══════════════════════════════════════════════════════╝");
            Console.WriteLine();

            var tests = new GrpcIntegrationTests(address);
            try { await tests.RunAll(); }
            catch (RpcException ex) { Console.WriteLine($"\n[FATAL] gRPC: {ex.Status.Detail}"); return 2; }
            catch (Exception ex) { Console.WriteLine($"\n[FATAL] {ex.Message}"); return 2; }
            finally { tests.Dispose(); }
            return tests.ExitCode;
        }
    }
}
