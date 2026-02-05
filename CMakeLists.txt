# ==============================================================================
# MDB Framework - Root CMake Configuration
# ==============================================================================
# IL2CPP Modding Framework - Unified Bridge Architecture
#
# Build:
#   cmake --preset release
#   cmake --build --preset release
#
# Or manually:
#   cmake -B build -G "Visual Studio 17 2022" -A x64
#   cmake --build build --config Release
# ==============================================================================

cmake_minimum_required(VERSION 3.21)

project(MDB_Framework
    VERSION 2.0.0
    DESCRIPTION "IL2CPP Modding Framework"
    LANGUAGES C CXX
)

# ------------------------------------------------------------------------------
# Global Settings
# ------------------------------------------------------------------------------
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

# Windows-only project
if(NOT WIN32)
    message(FATAL_ERROR "MDB Framework only supports Windows (x64).")
endif()

# Force x64 — 32-bit is not supported
if(CMAKE_SIZEOF_VOID_P EQUAL 4)
    message(FATAL_ERROR "MDB Framework requires 64-bit (x64) builds. 32-bit is not supported.")
endif()

# Default to Release if not specified
if(NOT CMAKE_BUILD_TYPE AND NOT CMAKE_CONFIGURATION_TYPES)
    set(CMAKE_BUILD_TYPE Release CACHE STRING "Build type" FORCE)
    set_property(CACHE CMAKE_BUILD_TYPE PROPERTY STRINGS Debug Release RelWithDebInfo MinSizeRel)
endif()

# Output directories
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin)
set(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin)
set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib)

# Unicode character set (matches vcxproj)
add_compile_definitions(UNICODE _UNICODE)

# Suppress CRT security warnings
add_compile_definitions(_CRT_SECURE_NO_WARNINGS)

# ------------------------------------------------------------------------------
# Subdirectories
# ------------------------------------------------------------------------------
add_subdirectory(MDB_Common)
add_subdirectory(MDB_Bridge)
