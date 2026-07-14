using System.Diagnostics;
using DMC.Core;
using DMC.Common;
using DMC.Common.DataElements;

namespace DMC.Tests
{
    /// <summary>
    /// Unit tests for DMCServer V2 core logic.
    /// Tests Schema (DefineType) + Data (Set/Search/Print) without gRPC.
    /// </summary>
    public class DMCServerTests
    {
        private int _passed;
        private int _failed;

        // =====================================================================
        // Schema Tests
        // =====================================================================

        public void S01_DefineType()
        {
            Console.WriteLine("=== S-01: DefineType ===");
            DMCServer.ResetInstance();
            var server = DMCServer.Instance;

            bool result = server.DefineType("Car", new List<string> { "VIN" });
            Assert(result, "DefineType Car[VIN] should succeed");

            result = server.DefineType("Sensor", new List<string> { "DeviceId" });
            Assert(result, "DefineType Sensor[DeviceId] should succeed");

            result = server.DefineType("Person", new List<string> { "EmployeeId" });
            Assert(result, "DefineType Person[EmployeeId] should succeed");
            Console.WriteLine();
        }

        public void S02_DefineTypeDuplicate()
        {
            Console.WriteLine("=== S-02: DefineType Duplicate ===");
            var server = DMCServer.Instance;

            bool result = server.DefineType("Car", new List<string> { "Make" });
            Assert(!result, "Duplicate DefineType should return false");
            Console.WriteLine();
        }

        public void S03_DefineTypeValidation()
        {
            Console.WriteLine("=== S-03: DefineType Validation ===");
            var server = DMCServer.Instance;

            bool threw = false;
            try { server.DefineType("", new List<string> { "Id" }); }
            catch (ArgumentException) { threw = true; }
            Assert(threw, "Empty type should throw ArgumentException");

            threw = false;
            try { server.DefineType("Test", new List<string>()); }
            catch (ArgumentException) { threw = true; }
            Assert(threw, "Empty IdentityKeys should throw ArgumentException");
            Console.WriteLine();
        }

        public void S04_GetTypeSchema()
        {
            Console.WriteLine("=== S-04: GetTypeSchema ===");
            var server = DMCServer.Instance;

            var schema = server.GetTypeSchema("Car");
            Assert(schema != null, "Car schema should exist");
            Assert(schema!.Contains("VIN"), "Car schema should contain VIN");

            var noSchema = server.GetTypeSchema("Unknown");
            Assert(noSchema == null, "Unknown schema should be null");
            Console.WriteLine();
        }

        // =====================================================================
        // Set Tests — Identity Matching with VIN
        // =====================================================================

        public void S05_SetNewElement()
        {
            Console.WriteLine("=== S-05: Set New Element ===");
            var server = DMCServer.Instance;

            var result = server.Set("Car", new Dictionary<string, string>
            {
                ["VIN"] = "1HGBH41JXMN109186",
                ["Make"] = "Toyota",
                ["Model"] = "Camry",
                ["Year"] = "2024",
                ["Color"] = "White",
                ["Mileage"] = "0"
            });

            Assert(result.Action == SetAction.Created, $"Should be Created, got {result.Action}");
            Assert(result.Key == "Car:1", $"Key should be Car:1, got {result.Key}");
            Assert(server.Count == 1, $"Count should be 1, got {server.Count}");
            Console.WriteLine();
        }

        public void S06_SetIdentityMatch_Merge()
        {
            Console.WriteLine("=== S-06: Set Same VIN → Updated (Merge) ===");
            var server = DMCServer.Instance;

            var result = server.Set("Car", new Dictionary<string, string>
            {
                ["VIN"] = "1HGBH41JXMN109186",
                ["Year"] = "2025",
                ["Color"] = "Red",
                ["Mileage"] = "15000"
            });

            Assert(result.Action == SetAction.Updated, $"Should be Updated, got {result.Action}");
            Assert(result.Key == "Car:1", $"Should update Car:1, got {result.Key}");
            Assert(server.Count == 1, "Count should still be 1 (no duplicate)");

            // Verify merge: Make/Model preserved, Year/Color/Mileage updated
            var element = server.Get("Car:1") as GenericDataElement;
            Assert(element != null, "Car:1 should exist");
            Assert(element!.Properties["Make"] == "Toyota", "Make should be preserved");
            Assert(element.Properties["Model"] == "Camry", "Model should be preserved");
            Assert(element.Properties["Year"] == "2025", "Year should be updated to 2025");
            Assert(element.Properties["Color"] == "Red", "Color should be updated to Red");
            Assert(element.Properties["Mileage"] == "15000", "Mileage should be updated to 15000");
            Console.WriteLine();
        }

        public void S07_SetDifferentIdentity()
        {
            Console.WriteLine("=== S-07: Set Different VIN (same Make+Model) → Created ===");
            var server = DMCServer.Instance;

            var result = server.Set("Car", new Dictionary<string, string>
            {
                ["VIN"] = "5YJSA1DN0DFP14555",
                ["Make"] = "Toyota",
                ["Model"] = "Camry",
                ["Year"] = "2024",
                ["Color"] = "Blue"
            });

            Assert(result.Action == SetAction.Created, $"Should be Created (different VIN), got {result.Action}");
            Assert(result.Key == "Car:2", $"Key should be Car:2, got {result.Key}");
            Assert(server.Count == 2, "Count should be 2");
            Console.WriteLine();
        }

        // =====================================================================
        // Explicit Update by Key
        // =====================================================================

        public void S08_UpdateByKey_Merge()
        {
            Console.WriteLine("=== S-08: Update by Key (Merge) ===");
            var server = DMCServer.Instance;

            bool result = server.Update("Car:2", new Dictionary<string, string>
            {
                ["Owner"] = "John",
                ["Mileage"] = "5000"
            });

            Assert(result, "Update should succeed");

            var element = server.Get("Car:2") as GenericDataElement;
            Assert(element!.Properties["Color"] == "Blue", "Color should be preserved");
            Assert(element.Properties["Owner"] == "John", "Owner should be added");
            Assert(element.Properties["Mileage"] == "5000", "Mileage should be added");
            Console.WriteLine();
        }

        public void S09_UpdateByKey_Replace()
        {
            Console.WriteLine("=== S-09: Update by Key (Replace) ===");
            var server = DMCServer.Instance;

            bool result = server.Update("Car:2", new Dictionary<string, string>
            {
                ["VIN"] = "5YJSA1DN0DFP14555",
                ["Make"] = "Honda",
                ["Model"] = "Civic"
            }, merge: false);

            Assert(result, "Replace should succeed");

            var element = server.Get("Car:2") as GenericDataElement;
            Assert(element!.Properties.Count == 3, $"Should have 3 props after replace, got {element.Properties.Count}");
            Assert(element.Properties["Make"] == "Honda", "Make should be Honda");
            Assert(!element.Properties.ContainsKey("Owner"), "Owner should be gone (replaced)");
            Assert(!element.Properties.ContainsKey("Color"), "Color should be gone (replaced)");
            Console.WriteLine();
        }

        public void S10_UpdateNotFound()
        {
            Console.WriteLine("=== S-10: Update Non-Existing Key ===");
            var server = DMCServer.Instance;

            bool result = server.Update("Car:99", new Dictionary<string, string> { ["Foo"] = "Bar" });
            Assert(!result, "Update non-existing should return false");
            Console.WriteLine();
        }

        // =====================================================================
        // Set Without Schema
        // =====================================================================

        public void S11_SetWithoutSchema()
        {
            Console.WriteLine("=== S-11: Set Without DefineType ===");
            var server = DMCServer.Instance;

            bool threw = false;
            try
            {
                server.Set("Unknown", new Dictionary<string, string> { ["Foo"] = "Bar" });
            }
            catch (InvalidOperationException) { threw = true; }
            Assert(threw, "Set without schema should throw InvalidOperationException");
            Console.WriteLine();
        }

        public void S12_SetMissingIdentityKey()
        {
            Console.WriteLine("=== S-12: Set Missing IdentityKey in Properties ===");
            var server = DMCServer.Instance;

            bool threw = false;
            try
            {
                // Car requires VIN, but we don't provide it
                server.Set("Car", new Dictionary<string, string> { ["Make"] = "Ford" });
            }
            catch (ArgumentException) { threw = true; }
            Assert(threw, "Set without required IdentityKey should throw ArgumentException");
            Console.WriteLine();
        }

        // =====================================================================
        // Search Tests
        // =====================================================================

        public void S13_SearchByType()
        {
            Console.WriteLine("=== S-13: Search by Type ===");
            var server = DMCServer.Instance;

            var cars = server.Search("Car", new Dictionary<string, string>()).ToList();
            Assert(cars.Count == 2, $"Should find 2 Cars, got {cars.Count}");

            var sensors = server.Search("Sensor", new Dictionary<string, string>()).ToList();
            Assert(sensors.Count == 0, $"Should find 0 Sensors (none added), got {sensors.Count}");
            Console.WriteLine();
        }

        public void S14_SearchByFilter()
        {
            Console.WriteLine("=== S-14: Search by Property Filter ===");
            var server = DMCServer.Instance;

            // Both cars are now different makes (Car:1=Toyota, Car:2=Honda after replace)
            var toyotas = server.Search("Car", new Dictionary<string, string> { ["Make"] = "Toyota" }).ToList();
            Assert(toyotas.Count == 1, $"Should find 1 Toyota, got {toyotas.Count}");
            Assert(toyotas[0].Key == "Car:1", $"Toyota should be Car:1, got {toyotas[0].Key}");

            var hondas = server.Search("Car", new Dictionary<string, string> { ["Make"] = "Honda" }).ToList();
            Assert(hondas.Count == 1, $"Should find 1 Honda, got {hondas.Count}");
            Console.WriteLine();
        }

        public void S15_SearchCrossType()
        {
            Console.WriteLine("=== S-15: Search Cross-Type (no type filter) ===");
            var server = DMCServer.Instance;

            // Add a sensor
            server.Set("Sensor", new Dictionary<string, string>
            {
                ["DeviceId"] = "SNR-001",
                ["Location"] = "Floor3",
                ["Temperature"] = "72.5"
            });

            // Search all elements without type filter
            var all = server.Search(null, new Dictionary<string, string>()).ToList();
            Assert(all.Count == 3, $"Should find 3 total (2 Cars + 1 Sensor), got {all.Count}");
            Console.WriteLine();
        }

        // =====================================================================
        // Sensor Merge Test (realistic IoT scenario)
        // =====================================================================

        public void S16_SensorMergeRealistic()
        {
            Console.WriteLine("=== S-16: Sensor Merge (IoT readings update) ===");
            var server = DMCServer.Instance;

            // Update sensor readings (same DeviceId → merge)
            var result = server.Set("Sensor", new Dictionary<string, string>
            {
                ["DeviceId"] = "SNR-001",
                ["Temperature"] = "73.1",
                ["Humidity"] = "44.8"
            });

            Assert(result.Action == SetAction.Updated, $"Should be Updated (same DeviceId), got {result.Action}");

            var sensor = server.Get(result.Key) as GenericDataElement;
            Assert(sensor!.Properties["Location"] == "Floor3", "Location should be preserved");
            Assert(sensor.Properties["Temperature"] == "73.1", "Temperature should be updated");
            Assert(sensor.Properties["Humidity"] == "44.8", "Humidity should be added");
            Console.WriteLine();
        }

        // =====================================================================
        // Print Tests
        // =====================================================================

        public void S17_PrintAndPrintAll()
        {
            Console.WriteLine("=== S-17: Print and PrintAll ===");
            var server = DMCServer.Instance;

            Assert(server.Contains("Car:1"), "Car:1 should exist");
            Assert(!server.Contains("Car:99"), "Car:99 should not exist");
            Assert(server.Count == 3, $"Total count should be 3, got {server.Count}");

            var element = server.Get("Car:1");
            Assert(element != null, "Get Car:1 should return element");
            Assert(element!.ToDisplayString().Contains("Toyota"), "Display should contain Toyota");

            Console.WriteLine("  PrintAll:");
            server.PrintAll();
            Console.WriteLine();
        }

        // =====================================================================
        // Thread Safety Tests
        // =====================================================================

        public void S18_ConcurrentSet()
        {
            Console.WriteLine("=== S-18: Concurrent Set (100 threads, same Type) ===");
            DMCServer.ResetInstance();
            var server = DMCServer.Instance;
            server.DefineType("Item", new List<string> { "Id" });

            int threadCount = 100;
            int successCount = 0;
            var exceptions = new List<Exception>();

            Parallel.For(0, threadCount, new ParallelOptions { MaxDegreeOfParallelism = threadCount }, i =>
            {
                try
                {
                    server.Set("Item", new Dictionary<string, string>
                    {
                        ["Id"] = $"ITEM-{i:D4}",
                        ["Value"] = $"data-{i}"
                    });
                    Interlocked.Increment(ref successCount);
                }
                catch (Exception ex)
                {
                    lock (exceptions) { exceptions.Add(ex); }
                }
            });

            Assert(successCount == threadCount, $"All {threadCount} should succeed, got {successCount}");
            Assert(exceptions.Count == 0, $"No exceptions, got {exceptions.Count}");
            Assert(server.Count == threadCount, $"Count should be {threadCount}, got {server.Count}");
            Console.WriteLine();
        }

        public void S19_ConcurrentSetSameIdentity()
        {
            Console.WriteLine("=== S-19: Concurrent Set Same Identity (race condition test) ===");
            DMCServer.ResetInstance();
            var server = DMCServer.Instance;
            server.DefineType("Car", new List<string> { "VIN" });

            int threadCount = 50;
            int createdCount = 0;
            int updatedCount = 0;

            Parallel.For(0, threadCount, new ParallelOptions { MaxDegreeOfParallelism = threadCount }, i =>
            {
                var result = server.Set("Car", new Dictionary<string, string>
                {
                    ["VIN"] = "SAME-VIN-001",
                    ["Iteration"] = $"{i}"
                });

                if (result.Action == SetAction.Created)
                    Interlocked.Increment(ref createdCount);
                else
                    Interlocked.Increment(ref updatedCount);
            });

            Assert(createdCount == 1, $"Exactly 1 should be Created, got {createdCount}");
            Assert(updatedCount == threadCount - 1, $"{threadCount - 1} should be Updated, got {updatedCount}");
            Assert(server.Count == 1, $"Count should be 1 (no duplicates), got {server.Count}");
            Console.WriteLine($"  Created: {createdCount}, Updated: {updatedCount}");
            Console.WriteLine();
        }

        public void S20_ConcurrentReadWrite()
        {
            Console.WriteLine("=== S-20: Concurrent Read/Write Mix ===");
            DMCServer.ResetInstance();
            var server = DMCServer.Instance;
            server.DefineType("Sensor", new List<string> { "DeviceId" });

            // Pre-populate
            for (int i = 0; i < 10; i++)
            {
                server.Set("Sensor", new Dictionary<string, string>
                {
                    ["DeviceId"] = $"SNR-{i:D3}",
                    ["Value"] = "0"
                });
            }

            int readErrors = 0;
            int writeErrors = 0;
            int totalOps = 0;

            Parallel.For(0, 200, new ParallelOptions { MaxDegreeOfParallelism = 50 }, i =>
            {
                try
                {
                    if (i % 2 == 0)
                    {
                        // Write: update existing sensor
                        server.Set("Sensor", new Dictionary<string, string>
                        {
                            ["DeviceId"] = $"SNR-{i % 10:D3}",
                            ["Value"] = $"{i * 1.5}"
                        });
                    }
                    else
                    {
                        // Read: search
                        var results = server.Search("Sensor", new Dictionary<string, string>());
                        if (results == null)
                            Interlocked.Increment(ref readErrors);
                    }
                    Interlocked.Increment(ref totalOps);
                }
                catch
                {
                    if (i % 2 == 0) Interlocked.Increment(ref writeErrors);
                    else Interlocked.Increment(ref readErrors);
                }
            });

            Assert(readErrors == 0, $"No read errors, got {readErrors}");
            Assert(writeErrors == 0, $"No write errors, got {writeErrors}");
            Assert(totalOps == 200, $"All 200 ops should complete, got {totalOps}");
            Assert(server.Count == 10, $"Count should still be 10, got {server.Count}");
            Console.WriteLine($"  Total ops: {totalOps}, Read errors: {readErrors}, Write errors: {writeErrors}");
            Console.WriteLine();
        }

        public void S21_ConcurrentDefineType()
        {
            Console.WriteLine("=== S-21: Concurrent DefineType (same Type) ===");
            DMCServer.ResetInstance();
            var server = DMCServer.Instance;

            int successCount = 0;

            Parallel.For(0, 50, new ParallelOptions { MaxDegreeOfParallelism = 50 }, i =>
            {
                bool result = server.DefineType("Race", new List<string> { $"Key{i}" });
                if (result) Interlocked.Increment(ref successCount);
            });

            Assert(successCount == 1, $"Exactly 1 DefineType should succeed, got {successCount}");
            var schema = server.GetTypeSchema("Race");
            Assert(schema != null, "Race schema should exist");
            Console.WriteLine($"  Winner's IdentityKeys: [{string.Join(", ", schema!)}]");
            Console.WriteLine();
        }

        // =====================================================================
        // Multi-User / Ownership Tests
        // =====================================================================

        public void S22_OwnerTracking()
        {
            Console.WriteLine("=== S-22: Owner Tracking ===");
            DMCServer.ResetInstance();
            var server = DMCServer.Instance;
            server.DefineType("Car", new List<string> { "VIN" });

            // Alice creates a car
            var r1 = server.Set("Car", new Dictionary<string, string>
            {
                ["VIN"] = "VIN-ALICE-001",
                ["Make"] = "Toyota"
            }, owner: "alice");

            Assert(r1.Action == SetAction.Created, "Alice's car should be Created");

            var element1 = server.Get(r1.Key) as GenericDataElement;
            Assert(element1!.Owner == "alice", $"Owner should be alice, got {element1.Owner}");

            // Bob creates a car
            var r2 = server.Set("Car", new Dictionary<string, string>
            {
                ["VIN"] = "VIN-BOB-001",
                ["Make"] = "Honda"
            }, owner: "bob");

            var element2 = server.Get(r2.Key) as GenericDataElement;
            Assert(element2!.Owner == "bob", $"Owner should be bob, got {element2.Owner}");

            // Bob updates Alice's car (collaborative — allowed)
            server.Update(r1.Key, new Dictionary<string, string> { ["Color"] = "Red" });
            var updated = server.Get(r1.Key) as GenericDataElement;
            Assert(updated!.Owner == "alice", "Owner should remain alice after update by bob");
            Assert(updated.Properties["Color"] == "Red", "Color should be updated");

            Console.WriteLine($"  Alice's car: {element1.ToDisplayString()}");
            Console.WriteLine($"  Bob's car: {element2.ToDisplayString()}");
            Console.WriteLine();
        }

        public void S23_SearchByOwner()
        {
            Console.WriteLine("=== S-23: Search by Owner ===");
            var server = DMCServer.Instance; // continues from S22

            // Search all cars
            var allCars = server.Search("Car", new Dictionary<string, string>()).ToList();
            Assert(allCars.Count == 2, $"Should find 2 cars total, got {allCars.Count}");

            // Search only Alice's cars
            var aliceCars = server.Search("Car", new Dictionary<string, string>(), owner: "alice").ToList();
            Assert(aliceCars.Count == 1, $"Alice should have 1 car, got {aliceCars.Count}");
            Assert((aliceCars[0] as GenericDataElement)!.Properties["Make"] == "Toyota",
                "Alice's car should be Toyota");

            // Search only Bob's cars
            var bobCars = server.Search("Car", new Dictionary<string, string>(), owner: "bob").ToList();
            Assert(bobCars.Count == 1, $"Bob should have 1 car, got {bobCars.Count}");

            // Search non-existing owner
            var nobody = server.Search("Car", new Dictionary<string, string>(), owner: "nobody").ToList();
            Assert(nobody.Count == 0, $"Nobody should have 0 cars, got {nobody.Count}");

            Console.WriteLine();
        }

        // =====================================================================
        // Schema Migration Tests
        // =====================================================================

        public void S24_UpdateSchemaSuccess()
        {
            Console.WriteLine("=== S-24: UpdateTypeSchema (no collision) ===");
            DMCServer.ResetInstance();
            var server = DMCServer.Instance;
            server.DefineType("Car", new List<string> { "VIN" });

            server.Set("Car", new Dictionary<string, string>
                { ["VIN"] = "VIN-001", ["Make"] = "Toyota", ["Model"] = "Camry" });
            server.Set("Car", new Dictionary<string, string>
                { ["VIN"] = "VIN-002", ["Make"] = "Honda", ["Model"] = "Civic" });

            // Tighten: [VIN] → [VIN, Make] — no collision
            var (success, conflicts) = server.UpdateTypeSchema("Car", new List<string> { "VIN", "Make" });
            Assert(success, "Schema update should succeed (no collision)");
            Assert(conflicts.Count == 0, "Should have 0 conflicts");

            var schema = server.GetTypeSchema("Car");
            Assert(schema!.Contains("VIN") && schema.Contains("Make"),
                $"Schema should be [VIN, Make], got [{string.Join(", ", schema)}]");

            // Verify new schema works: same VIN+Make → Updated
            var r = server.Set("Car", new Dictionary<string, string>
                { ["VIN"] = "VIN-001", ["Make"] = "Toyota", ["Year"] = "2025" });
            Assert(r.Action == SetAction.Updated, "Same VIN+Make should still match after schema update");
            Console.WriteLine();
        }

        public void S25_UpdateSchemaCollision()
        {
            Console.WriteLine("=== S-25: UpdateTypeSchema (collision → rejected) ===");
            DMCServer.ResetInstance();
            var server = DMCServer.Instance;
            server.DefineType("Car", new List<string> { "VIN" });

            server.Set("Car", new Dictionary<string, string>
                { ["VIN"] = "VIN-A", ["Make"] = "Toyota", ["Model"] = "Camry" });
            server.Set("Car", new Dictionary<string, string>
                { ["VIN"] = "VIN-B", ["Make"] = "Toyota", ["Model"] = "RAV4" });

            // Loosen: [VIN] → [Make] — collision! Both are Toyota
            var (success, conflicts) = server.UpdateTypeSchema("Car", new List<string> { "Make" });
            Assert(!success, "Schema update should fail (collision)");
            Assert(conflicts.Count == 1, $"Should have 1 conflict, got {conflicts.Count}");
            Console.WriteLine($"  Conflict: {conflicts[0].KeyA} ↔ {conflicts[0].KeyB}");

            // Schema should remain unchanged
            var schema = server.GetTypeSchema("Car");
            Assert(schema!.Count == 1 && schema[0] == "VIN",
                $"Schema should remain [VIN], got [{string.Join(", ", schema)}]");
            Console.WriteLine();
        }

        public void S26_UpdateSchemaMissingProperty()
        {
            Console.WriteLine("=== S-26: UpdateTypeSchema (missing property → error) ===");
            DMCServer.ResetInstance();
            var server = DMCServer.Instance;
            server.DefineType("Car", new List<string> { "VIN" });

            server.Set("Car", new Dictionary<string, string>
                { ["VIN"] = "VIN-X", ["Make"] = "Toyota" });

            bool threw = false;
            try { server.UpdateTypeSchema("Car", new List<string> { "Color" }); }
            catch (ArgumentException) { threw = true; }
            Assert(threw, "Should throw if element missing new IdentityKey property");
            Console.WriteLine();
        }

        public void S27_UpdateSchemaUndefinedType()
        {
            Console.WriteLine("=== S-27: UpdateTypeSchema (undefined type → error) ===");
            var server = DMCServer.Instance;

            bool threw = false;
            try { server.UpdateTypeSchema("Ghost", new List<string> { "Id" }); }
            catch (InvalidOperationException) { threw = true; }
            Assert(threw, "Should throw for undefined type");
            Console.WriteLine();
        }

        // =====================================================================
        // Runner
        // =====================================================================

        public void RunAll()
        {
            var sw = Stopwatch.StartNew();

            S01_DefineType();
            S02_DefineTypeDuplicate();
            S03_DefineTypeValidation();
            S04_GetTypeSchema();
            S05_SetNewElement();
            S06_SetIdentityMatch_Merge();
            S07_SetDifferentIdentity();
            S08_UpdateByKey_Merge();
            S09_UpdateByKey_Replace();
            S10_UpdateNotFound();
            S11_SetWithoutSchema();
            S12_SetMissingIdentityKey();
            S13_SearchByType();
            S14_SearchByFilter();
            S15_SearchCrossType();
            S16_SensorMergeRealistic();
            S17_PrintAndPrintAll();
            S18_ConcurrentSet();
            S19_ConcurrentSetSameIdentity();
            S20_ConcurrentReadWrite();
            S21_ConcurrentDefineType();
            S22_OwnerTracking();
            S23_SearchByOwner();
            S24_UpdateSchemaSuccess();
            S25_UpdateSchemaCollision();
            S26_UpdateSchemaMissingProperty();
            S27_UpdateSchemaUndefinedType();

            sw.Stop();

            Console.WriteLine("══════════════════════════════════════════════════════");
            Console.WriteLine($"  DMCServer Unit Tests: {_passed} passed, {_failed} failed");
            Console.WriteLine($"  Elapsed: {sw.ElapsedMilliseconds} ms");
            Console.WriteLine("──────────────────────────────────────────────────────");
            Console.WriteLine("  Coverage:");
            Console.WriteLine("    Schema     S01-S04  DefineType, duplicate, validation, GetTypeSchema");
            Console.WriteLine("    Set/Update S05-S12  identity match, merge, replace, NOT_FOUND, validation");
            Console.WriteLine("    Search     S13-S16  by type, by filter, cross-type, sensor merge");
            Console.WriteLine("    Print      S17      Print, PrintAll, Contains, Count, ToDisplayString");
            Console.WriteLine("    Concurrency S18-S21 100-thread Set, same-identity race, R/W mix, DefineType race");
            Console.WriteLine("    Multi-User S22-S23  owner tracking, search by owner");
            Console.WriteLine("    Migration  S24-S27  schema tighten, collision reject, missing prop, undefined type");
            Console.WriteLine("══════════════════════════════════════════════════════");
        }

        private void Assert(bool condition, string message)
        {
            if (condition) { Console.WriteLine($"  [PASS] {message}"); _passed++; }
            else { Console.WriteLine($"  [FAIL] {message}"); _failed++; }
        }

        public int ExitCode => _failed > 0 ? 1 : 0;

        public static int Main(string[] args)
        {
            Console.WriteLine("╔══════════════════════════════════════════════════════╗");
            Console.WriteLine("║  DMC Server Unit Tests (V2)                         ║");
            Console.WriteLine("║  No gRPC required — tests core logic directly       ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════╝");
            Console.WriteLine();

            var tests = new DMCServerTests();
            tests.RunAll();
            return tests.ExitCode;
        }
    }
}
