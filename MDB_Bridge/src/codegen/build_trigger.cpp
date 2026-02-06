#include "build_trigger.hpp"
#include <Windows.h>
#include <filesystem>
#include <sstream>
#include <vector>

namespace MDB {
namespace Build {

static std::string ReadPipeToString(HANDLE hReadPipe) {
    std::string result;
    char buffer[4096];
    DWORD bytesRead;
    
    while (ReadFile(hReadPipe, buffer, sizeof(buffer) - 1, &bytesRead, NULL) && bytesRead > 0) {
        buffer[bytesRead] = '\0';
        result += buffer;
    }
    
    return result;
}

std::string FindMSBuild() {
    // Try vswhere first — most reliable method, works for any VS version/edition
    std::string vswhere_path = "C:\\Program Files (x86)\\Microsoft Visual Studio\\Installer\\vswhere.exe";
    if (std::filesystem::exists(vswhere_path)) {
        // Note: vswhere_path is hardcoded and trusted, command is properly quoted
        std::string command = "\"" + vswhere_path + "\" -latest -requires Microsoft.Component.MSBuild -find MSBuild\\**\\Bin\\MSBuild.exe";
        
        SECURITY_ATTRIBUTES sa = { sizeof(SECURITY_ATTRIBUTES), NULL, TRUE };
        HANDLE hReadPipe, hWritePipe;
        if (CreatePipe(&hReadPipe, &hWritePipe, &sa, 0)) {
            STARTUPINFOA si = { sizeof(STARTUPINFOA) };
            si.dwFlags = STARTF_USESTDHANDLES | STARTF_USESHOWWINDOW;
            si.hStdOutput = hWritePipe;
            si.hStdError = hWritePipe;
            si.wShowWindow = SW_HIDE;
            
            PROCESS_INFORMATION pi = { 0 };
            
            if (CreateProcessA(NULL, const_cast<char*>(command.c_str()), NULL, NULL, TRUE, 0, NULL, NULL, &si, &pi)) {
                CloseHandle(hWritePipe);
                
                std::string output = ReadPipeToString(hReadPipe);
                CloseHandle(hReadPipe);
                
                WaitForSingleObject(pi.hProcess, INFINITE);
                CloseHandle(pi.hProcess);
                CloseHandle(pi.hThread);
                
                // Trim output and check if file exists
                output.erase(0, output.find_first_not_of(" \t\n\r"));
                output.erase(output.find_last_not_of(" \t\n\r") + 1);
                
                if (!output.empty() && std::filesystem::exists(output)) {
                    return output;
                }
            } else {
                CloseHandle(hReadPipe);
                CloseHandle(hWritePipe);
            }
        }
    }
    
    // Fallback: check hardcoded paths for modern VS installations
    // NOTE: Do NOT include old .NET Framework MSBuild (v4.0.30319) — it cannot
    // build SDK-style projects and will fail with MSB4041.
    std::vector<std::string> search_paths = {
        // Visual Studio 2022
        "C:\\Program Files\\Microsoft Visual Studio\\2022\\Enterprise\\MSBuild\\Current\\Bin\\MSBuild.exe",
        "C:\\Program Files\\Microsoft Visual Studio\\2022\\Professional\\MSBuild\\Current\\Bin\\MSBuild.exe",
        "C:\\Program Files\\Microsoft Visual Studio\\2022\\Community\\MSBuild\\Current\\Bin\\MSBuild.exe",
        "C:\\Program Files (x86)\\Microsoft Visual Studio\\2022\\BuildTools\\MSBuild\\Current\\Bin\\MSBuild.exe",
        
        // Visual Studio 2019
        "C:\\Program Files (x86)\\Microsoft Visual Studio\\2019\\Enterprise\\MSBuild\\Current\\Bin\\MSBuild.exe",
        "C:\\Program Files (x86)\\Microsoft Visual Studio\\2019\\Professional\\MSBuild\\Current\\Bin\\MSBuild.exe",
        "C:\\Program Files (x86)\\Microsoft Visual Studio\\2019\\Community\\MSBuild\\Current\\Bin\\MSBuild.exe",
        "C:\\Program Files (x86)\\Microsoft Visual Studio\\2019\\BuildTools\\MSBuild\\Current\\Bin\\MSBuild.exe",
    };
    
    for (const auto& path : search_paths) {
        if (std::filesystem::exists(path)) {
            return path;
        }
    }
    
    return "";
}

BuildResult TriggerBuild(const std::string& project_path) {
    BuildResult result = { false, "", "", -1 };
    
    // Check if project file exists
    if (!std::filesystem::exists(project_path)) {
        result.error_message = "Project file not found: " + project_path;
        return result;
    }
    
    // Find MSBuild
    std::string msbuild_path = FindMSBuild();
    if (msbuild_path.empty()) {
        result.error_message = "MSBuild.exe not found. Please install Visual Studio or Build Tools.";
        return result;
    }
    
    // Prepare command line
    // Note: Both msbuild_path and project_path are properly quoted to handle spaces
    // and special characters. These paths come from trusted sources (filesystem/registry),
    // not user input, so command injection is not a concern.
    std::stringstream cmd;
    cmd << "\"" << msbuild_path << "\" \"" << project_path << "\" "
        << "/restore "
        << "/p:Configuration=Release "
        << "/p:Platform=AnyCPU "
        << "/v:minimal "
        << "/nologo";
    
    std::string command = cmd.str();
    
    // Create pipe for reading output
    SECURITY_ATTRIBUTES sa = { sizeof(SECURITY_ATTRIBUTES), NULL, TRUE };
    HANDLE hReadPipe, hWritePipe;
    
    if (!CreatePipe(&hReadPipe, &hWritePipe, &sa, 0)) {
        result.error_message = "Failed to create pipe for MSBuild output";
        return result;
    }
    
    // Set up process startup info
    STARTUPINFOA si = { sizeof(STARTUPINFOA) };
    si.dwFlags = STARTF_USESTDHANDLES | STARTF_USESHOWWINDOW;
    si.hStdOutput = hWritePipe;
    si.hStdError = hWritePipe;
    si.wShowWindow = SW_HIDE;
    
    PROCESS_INFORMATION pi = { 0 };
    
    // Start MSBuild process
    if (!CreateProcessA(NULL, const_cast<char*>(command.c_str()), NULL, NULL, TRUE, 0, NULL, NULL, &si, &pi)) {
        result.error_message = "Failed to start MSBuild process";
        CloseHandle(hReadPipe);
        CloseHandle(hWritePipe);
        return result;
    }
    
    // Close write end of pipe (we're only reading)
    CloseHandle(hWritePipe);
    
    // Read build output
    result.build_output = ReadPipeToString(hReadPipe);
    CloseHandle(hReadPipe);
    
    // Wait for process to complete
    WaitForSingleObject(pi.hProcess, INFINITE);
    
    // Get exit code
    DWORD exitCode;
    if (GetExitCodeProcess(pi.hProcess, &exitCode)) {
        result.exit_code = static_cast<int>(exitCode);
        result.success = (exitCode == 0);
        
        if (!result.success) {
            result.error_message = "MSBuild failed with exit code " + std::to_string(exitCode);
        }
    } else {
        result.error_message = "Failed to get MSBuild exit code";
    }
    
    CloseHandle(pi.hProcess);
    CloseHandle(pi.hThread);
    
    return result;
}

} // namespace Build
} // namespace MDB
