#ifndef HARDWARE_LOGIC_H
#define HARDWARE_LOGIC_H

#ifdef __cplusplus
extern "C" {
#endif

// Hardware initialization and shutdown
int HW_Initialize();
int HW_Shutdown();

// Hardware status query
int HW_GetStatus();
const char* HW_GetStatusMessage();

// Hardware data operations
int HW_ReadData(const char* elementId, char* buffer, int bufferSize);
int HW_WriteData(const char* elementId, const char* data, int dataSize);

// Hardware diagnostics
int HW_RunDiagnostics();
const char* HW_GetLastError();

#ifdef __cplusplus
}
#endif

#endif // HARDWARE_LOGIC_H
