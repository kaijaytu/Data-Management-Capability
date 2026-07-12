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
    std::lock_guard<std::mutex> lock(g_mutex);
    if (g_status != HWStatus::Ready)
    {
        g_lastError = "Hardware not ready";
        return -1;
    }

    g_dataStore[elementId] = std::string(data, dataSize);
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
    // Note: not thread-safe for the returned pointer, but acceptable for diagnostics
    return g_lastError.c_str();
}

} // extern "C"
