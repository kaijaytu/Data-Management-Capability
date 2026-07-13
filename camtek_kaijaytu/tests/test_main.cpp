#include <cstdio>
#include <cstring>
#include <cstdlib>
#include <thread>
#include <vector>
#include <atomic>
#include <string>
#include "hardware_logic.h"

// ============================================================================
// Fault Injection Tests for libHardwareLogic.so
// Directly calls native API without C# layer.
//
// Build:
//   g++ -o test_main test_main.cpp -I../src/HardwareLogic/include \
//       -L../../build/output/lib -lHardwareLogic -Wl,-rpath,../../build/output/lib
//
// Run:
//   ./test_main
// ============================================================================

static int g_passed = 0;
static int g_failed = 0;

#define ASSERT_EQ(actual, expected, msg) \
    do { \
        if ((actual) == (expected)) { \
            printf("  [PASS] %s (got %d)\n", msg, (int)(actual)); \
            g_passed++; \
        } else { \
            printf("  [FAIL] %s (expected %d, got %d)\n", msg, (int)(expected), (int)(actual)); \
            g_failed++; \
        } \
    } while(0)

#define ASSERT_NEQ(actual, not_expected, msg) \
    do { \
        if ((actual) != (not_expected)) { \
            printf("  [PASS] %s (got %d)\n", msg, (int)(actual)); \
            g_passed++; \
        } else { \
            printf("  [FAIL] %s (should not be %d)\n", msg, (int)(not_expected)); \
            g_failed++; \
        } \
    } while(0)

// ----------------------------------------------------------------------------
// Test 1: Write before Initialize
// Expectation: returns -1 ("Hardware not ready"), no crash
// ----------------------------------------------------------------------------
void test_write_before_init()
{
    printf("\n=== Test 1: HW_WriteData before HW_Initialize ===\n");

    int result = HW_WriteData("sensor_01", "hello", 5);
    ASSERT_EQ(result, -1, "WriteData should fail with -1");

    const char* err = HW_GetLastError();
    printf("  LastError: \"%s\"\n", err ? err : "(null)");

    // Also test ReadData before init
    char buf[64];
    result = HW_ReadData("sensor_01", buf, sizeof(buf));
    ASSERT_EQ(result, -1, "ReadData should fail with -1");
}

// ----------------------------------------------------------------------------
// Test 2: Write with oversized dataSize (exceeds actual string length)
// Expectation: no segfault. The function will read dataSize bytes from pointer,
//              which may read garbage but should not crash in this controlled test.
// ----------------------------------------------------------------------------
void test_write_oversized_data()
{
    printf("\n=== Test 2: HW_WriteData with oversized dataSize ===\n");

    // Initialize first
    int result = HW_Initialize();
    ASSERT_EQ(result, 0, "Initialize should succeed");

    // Write a small string but claim it's much larger
    // We allocate a large buffer to avoid actual out-of-bounds read
    const int oversized = 8192;
    char* large_buf = (char*)calloc(oversized, 1);
    if (!large_buf) {
        printf("  [SKIP] malloc failed\n");
        return;
    }
    memcpy(large_buf, "short", 5);  // Only 5 meaningful bytes, rest is zeros

    // Pass dataSize = oversized; the function will do std::string(data, dataSize)
    result = HW_WriteData("overflow_key", large_buf, oversized);
    ASSERT_EQ(result, 0, "WriteData with large buffer should succeed (no crash)");

    // Now try to read it back with a small buffer → should return -2 (buffer too small)
    char small_buf[16];
    result = HW_ReadData("overflow_key", small_buf, sizeof(small_buf));
    ASSERT_EQ(result, -2, "ReadData into small buffer should return -2");

    const char* err = HW_GetLastError();
    printf("  LastError: \"%s\"\n", err ? err : "(null)");

    // Read with adequate buffer → should return oversized bytes
    char* read_buf = (char*)calloc(oversized + 1, 1);
    if (read_buf) {
        result = HW_ReadData("overflow_key", read_buf, oversized + 1);
        ASSERT_EQ(result, oversized, "ReadData should return full oversized length");
        free(read_buf);
    }

    free(large_buf);

    // Shutdown to reset state for next test
    HW_Shutdown();
}

// ----------------------------------------------------------------------------
// Test 2b: Read with invalid bufferSize (0, negative)
// Expectation: returns -4 ("Invalid buffer size"), no crash
// ----------------------------------------------------------------------------
void test_invalid_buffer_size()
{
    printf("\n=== Test 2b: HW_ReadData with invalid bufferSize ===\n");

    int result = HW_Initialize();
    ASSERT_EQ(result, 0, "Initialize should succeed");

    result = HW_WriteData("buf_key", "some_data", 9);
    ASSERT_EQ(result, 0, "WriteData should succeed");

    char buf[64];

    // bufferSize = 0
    result = HW_ReadData("buf_key", buf, 0);
    ASSERT_EQ(result, -4, "ReadData with bufferSize=0 should return -4");
    const char* err = HW_GetLastError();
    printf("  LastError (size=0): \"%s\"\n", err ? err : "(null)");

    // bufferSize = -1
    result = HW_ReadData("buf_key", buf, -1);
    ASSERT_EQ(result, -4, "ReadData with bufferSize=-1 should return -4");
    err = HW_GetLastError();
    printf("  LastError (size=-1): \"%s\"\n", err ? err : "(null)");

    // bufferSize = 1 (too small for data but valid size)
    result = HW_ReadData("buf_key", buf, 1);
    ASSERT_EQ(result, -2, "ReadData with bufferSize=1 should return -2 (buffer too small)");

    // Negative dataSize on write
    result = HW_WriteData("buf_key", "abc", -5);
    ASSERT_EQ(result, -4, "WriteData with dataSize=-5 should return -4");

    HW_Shutdown();
}

// ----------------------------------------------------------------------------
// Test 3: Read after Shutdown
// Expectation: returns -1 ("Hardware not ready"), no crash
// ----------------------------------------------------------------------------
void test_read_after_shutdown()
{
    printf("\n=== Test 3: HW_ReadData after HW_Shutdown ===\n");

    // Initialize, write some data, then shutdown
    int result = HW_Initialize();
    ASSERT_EQ(result, 0, "Initialize should succeed");

    result = HW_WriteData("temp_key", "temp_data", 9);
    ASSERT_EQ(result, 0, "WriteData should succeed");

    result = HW_Shutdown();
    ASSERT_EQ(result, 0, "Shutdown should succeed");

    // Now try to read — hardware is no longer ready
    char buf[64];
    result = HW_ReadData("temp_key", buf, sizeof(buf));
    ASSERT_EQ(result, -1, "ReadData after shutdown should fail with -1");

    const char* err = HW_GetLastError();
    printf("  LastError: \"%s\"\n", err ? err : "(null)");

    // Also try write after shutdown
    result = HW_WriteData("temp_key", "new_data", 8);
    ASSERT_EQ(result, -1, "WriteData after shutdown should fail with -1");
}

// ----------------------------------------------------------------------------
// Bonus Test 4: Null pointer handling
// Expectation: ideally returns error, but may segfault (documents the risk)
// ----------------------------------------------------------------------------
void test_null_pointer()
{
    printf("\n=== Test 4 (Bonus): Null pointer arguments ===\n");

    int result = HW_Initialize();
    ASSERT_EQ(result, 0, "Initialize should succeed");

    // Null elementId on write — this may crash if not guarded
    printf("  Testing HW_WriteData(NULL, ...)...\n");
    fflush(stdout);
    result = HW_WriteData(NULL, "data", 4);
    // If we reach here, it didn't crash
    ASSERT_NEQ(result, 0, "WriteData with NULL elementId should not succeed");

    // Null buffer on read
    printf("  Testing HW_ReadData(\"key\", NULL, 64)...\n");
    fflush(stdout);
    result = HW_ReadData("nonexistent", NULL, 64);
    ASSERT_NEQ(result, 0, "ReadData with NULL buffer should not succeed");

    HW_Shutdown();
}

// ----------------------------------------------------------------------------
// Test 5: HW_GetLastError thread safety
// Multiple threads trigger errors and read GetLastError concurrently.
// Before fix: returned pointer could become dangling (use-after-free).
// After fix: thread_local snapshot ensures stability.
// ----------------------------------------------------------------------------
void test_getlasterror_thread_safety()
{
    printf("\n=== Test 5: HW_GetLastError Thread Safety ===\n");

    int result = HW_Initialize();
    ASSERT_EQ(result, 0, "Initialize should succeed");

    const int NUM_THREADS = 50;
    const int ITERATIONS = 10000;
    std::atomic<int> corrupted{0};
    std::atomic<int> completed{0};

    std::vector<std::thread> threads;
    for (int t = 0; t < NUM_THREADS; t++)
    {
        threads.emplace_back([&, t]() {
            for (int i = 0; i < ITERATIONS; i++)
            {
                // Trigger an error (read non-existent key)
                char buf[16];
                char key[32];
                snprintf(key, sizeof(key), "no_such_key_%d_%d", t, i);
                HW_ReadData(key, buf, sizeof(buf));

                // Immediately read the error
                const char* err = HW_GetLastError();
                if (err == nullptr)
                {
                    corrupted.fetch_add(1);
                    continue;
                }

                // Validate the string is readable and plausible
                size_t len = strlen(err);  // Would segfault if pointer is dangling
                if (len == 0 || len > 256)
                {
                    corrupted.fetch_add(1);
                }
            }
            completed.fetch_add(1);
        });
    }

    for (auto& th : threads)
        th.join();

    HW_Shutdown();

    printf("  Threads: %d, Iterations/thread: %d\n", NUM_THREADS, ITERATIONS);
    printf("  Completed: %d, Corrupted reads: %d\n", completed.load(), corrupted.load());
    ASSERT_EQ(corrupted.load(), 0, "No corrupted GetLastError reads");
}

// ============================================================================
int main()
{
    printf("==============================================================\n");
    printf("  libHardwareLogic Fault Injection Tests\n");
    printf("==============================================================\n");

    test_write_before_init();
    test_write_oversized_data();
    test_invalid_buffer_size();
    test_read_after_shutdown();

    // Test 4 may segfault — run last among single-threaded tests
    test_null_pointer();

    // Test 5: multi-threaded (run after null pointer test passes)
    test_getlasterror_thread_safety();

    printf("\n==============================================================\n");
    printf("  Results: %d passed, %d failed\n", g_passed, g_failed);
    printf("  (If you see this line, no Segmentation Fault occurred)\n");
    printf("==============================================================\n");

    return g_failed > 0 ? 1 : 0;
}
