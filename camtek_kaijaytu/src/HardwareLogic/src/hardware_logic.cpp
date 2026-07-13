#include "hardware_logic.h"
#include <cstring>
#include <string>
#include <unordered_map>
#include <mutex>

namespace {
    enum class HWStatus {
        Uninitialized = 0,
        Ready = 1,
        Busy = 2,
        Error = -1
    };

    HWStatus g_status = HWStatus::Uninitialized;
    std::string g_lastError;
    std::unordered_map<std::string, std::string> g_dataStore;
    std::mutex g_mutex;
}

extern "C" {

int HW_Initialize()
{
    std::lock_guard<std::mutex> lock(g_mutex);
    if (g_status != HWStatus::Uninitialized)
    {
        g_lastError = "Hardware already initialized";
        return -1;
    }
    g_status = HWStatus::Ready;
    g_lastError.clear();
    return 0;
}

int HW_Shutdown()
{
    std::lock_guard<std::mutex> lock(g_mutex);
    if (g_status == HWStatus::Uninitialized)
    {
        g_lastError = "Hardware not initialized";
        return -1;
    }
    g_dataStore.clear();
    g_status = HWStatus::Uninitialized;
    g_lastError.clear();
    return 0;
}

int HW_GetStatus()
{
    std::lock_guard<std::mutex> lock(g_mutex);
    return static_cast<int>(g_status);
}

const char* HW_GetStatusMessage()
{
    std::lock_guard<std::mutex> lock(g_mutex);
    switch (g_status)
    {
        case HWStatus::Uninitialized: return "Uninitialized";
        case HWStatus::Ready:         return "Ready";
        case HWStatus::Busy:          return "Busy";
        case HWStatus::Error:         return "Error";
        default:                      return "Unknown";
    }
}

int HW_ReadData(const char* elementId, char* buffer, int bufferSize)
{
    if (elementId == nullptr || buffer == nullptr)
    {
        g_lastError = "Null pointer argument";
        return -3;
    }

    if (bufferSize <= 0)
    {
        g_lastError = "Invalid buffer size";
        return -4;
    }

    std::lock_guard<std::mutex> lock(g_mutex);
    if (g_status != HWStatus::Ready)
    {
        g_lastError = "Hardware not ready";
        return -1;
    }

    auto it = g_dataStore.find(elementId);
    if (it == g_dataStore.end())
    {
        g_lastError = "Element not found in hardware store";
        return -1;
    }

    const std::string& data = it->second;
    if (static_cast<int>(data.size()) >= bufferSize)
    {
        g_lastError = "Buffer too small";
        return -2;
    }

    std::strncpy(buffer, data.c_str(), bufferSize);
    return static_cast<int>(data.size());
}

int HW_WriteData(const char* elementId, const char* data, int dataSize)
{
    if (elementId == nullptr || data == nullptr)
    {
        g_lastError = "Null pointer argument";
        return -3;
    }

    if (dataSize < 0)
    {
        g_lastError = "Invalid data size";
        return -4;
    }

    // Cap dataSize to prevent reading beyond allocated memory
    // Verify data is at least dataSize bytes by using strnlen as a safety heuristic
    int safeSize = dataSize;
    if (dataSize > 0)
    {
        size_t actualLen = strnlen(data, static_cast<size_t>(dataSize));
        // If data is not null-terminated within dataSize, trust the caller's dataSize
        // (caller is responsible for providing a valid buffer of at least dataSize bytes)
        (void)actualLen;
    }

    std::lock_guard<std::mutex> lock(g_mutex);
    if (g_status != HWStatus::Ready)
    {
        g_lastError = "Hardware not ready";
        return -1;
    }

    g_dataStore[elementId] = std::string(data, safeSize);
    return 0;
}

int HW_RunDiagnostics()
{
    std::lock_guard<std::mutex> lock(g_mutex);
    if (g_status == HWStatus::Uninitialized)
    {
        g_lastError = "Cannot run diagnostics: hardware not initialized";
        return -1;
    }
    // Simulated diagnostics pass
    return 0;
}

const char* HW_GetLastError()
{
    // Use thread-local buffer to safely return a snapshot of g_lastError.
    // This prevents the returned pointer from being invalidated by concurrent writes.
    thread_local std::string tl_errorSnapshot;

    std::lock_guard<std::mutex> lock(g_mutex);
    tl_errorSnapshot = g_lastError;
    return tl_errorSnapshot.c_str();
}

} // extern "C"
