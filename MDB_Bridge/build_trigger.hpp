#pragma once
#include <string>

// ============================================================================
// MSBuild Trigger for MDB Bridge
// ============================================================================
// This module invokes MSBuild to compile the generated wrapper classes

namespace MDB {
namespace Build {

struct BuildResult {
    bool success;
    std::string error_message;
    std::string build_output;
    int exit_code;
};

// Trigger MSBuild to compile the MDB_Core project
BuildResult TriggerBuild(const std::string& project_path);

// Find MSBuild.exe in standard locations
std::string FindMSBuild();

} // namespace Build
} // namespace MDB
