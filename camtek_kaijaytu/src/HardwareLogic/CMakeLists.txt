cmake_minimum_required(VERSION 3.20)
project(HardwareLogic VERSION 1.0.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

# Build shared library (.so on Linux)
add_library(HardwareLogic SHARED
    src/hardware_logic.cpp
)

target_include_directories(HardwareLogic PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}/include
)

# Install target
install(TARGETS HardwareLogic
    LIBRARY DESTINATION lib
    ARCHIVE DESTINATION lib
)

install(DIRECTORY include/
    DESTINATION include
)
