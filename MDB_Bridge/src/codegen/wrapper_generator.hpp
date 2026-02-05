#pragma once
#include <string>
#include <vector>
#include <map>

// ============================================================================
// C# Wrapper Generator for MDB Bridge
// ============================================================================
// This module generates C# wrapper classes from IL2CPP dump.cs files

namespace MDB {
namespace WrapperGen {

struct GeneratorResult {
    bool success;
    std::string error_message;
    std::vector<std::string> generated_files;
    size_t total_classes_generated;
};

// Generate C# wrapper classes from dump.cs
GeneratorResult GenerateWrappers(
    const std::string& dump_file_path,
    const std::string& output_directory,
    const std::string& namespace_prefix = "GameSDK"
);

// Check if wrappers already exist and are fresh
bool AreWrappersFresh(const std::string& output_directory);

} // namespace WrapperGen
} // namespace MDB
