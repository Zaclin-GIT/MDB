// ==============================
// MDB Bridge - DLL Entry Point & CLR Hosting
// ==============================
// This file handles DLL initialization and hosts the .NET Framework CLR
// to load and execute the managed mod assemblies.

#include "bridge_exports.h"
#include "il2cpp_resolver.hpp"
#include "il2cpp_dumper.hpp"
#include "wrapper_generator.hpp"
#include "build_trigger.hpp"

#include <windows.h>
#include <metahost.h>
#include <mscoree.h>
#include <string>
#include <filesystem>

#pragma comment(lib, "mscoree.lib")

// ==============================
// Debug/Release Logging Configuration
// ==============================
// Define MDB_DEBUG in your project settings for debug builds
// or uncomment the following line for debug mode:
// #define MDB_DEBUG

#ifdef MDB_DEBUG
    #define LOG_DEBUG(fmt, ...) log_message("[DEBUG] " fmt, ##__VA_ARGS__)
    #define LOG_TRACE(fmt, ...) log_message("[TRACE] " fmt, ##__VA_ARGS__)
#else
    #define LOG_DEBUG(fmt, ...) ((void)0)
    #define LOG_TRACE(fmt, ...) ((void)0)
#endif

// Always-on logging for errors, warnings, and important info
#define LOG_ERROR(fmt, ...) log_message("[ERROR] " fmt, ##__VA_ARGS__)
#define LOG_WARN(fmt, ...)  log_message("[WARN] " fmt, ##__VA_ARGS__)
#define LOG_INFO(fmt, ...)  log_message("[INFO] " fmt, ##__VA_ARGS__)

// CLR interfaces
static ICLRMetaHost* g_pMetaHost = nullptr;
static ICLRRuntimeInfo* g_pRuntimeInfo = nullptr;
static ICLRRuntimeHost* g_pRuntimeHost = nullptr;
static bool g_clr_initialized = false;
static bool g_mods_loaded = false;

// Logging
static FILE* g_log_file = nullptr;
static bool g_console_allocated = false;

// Allocate a console window for logging output
static void allocate_console() {
    if (g_console_allocated) return;
    
    if (AllocConsole()) {
        FILE* fp;
        freopen_s(&fp, "CONOUT$", "w", stdout);
        freopen_s(&fp, "CONOUT$", "w", stderr);
        freopen_s(&fp, "CONIN$", "r", stdin);
        
        SetConsoleTitleA("MDB Framework Console");
        
        // Set console colors for better readability
        HANDLE hConsole = GetStdHandle(STD_OUTPUT_HANDLE);
        
        // Print header in purple/magenta
        SetConsoleTextAttribute(hConsole, FOREGROUND_RED | FOREGROUND_BLUE | FOREGROUND_INTENSITY);
        printf("=== MDB Framework Console ===\n\n");
        
        // Reset to default gray for subsequent output
        SetConsoleTextAttribute(hConsole, FOREGROUND_RED | FOREGROUND_GREEN | FOREGROUND_BLUE);
        
        g_console_allocated = true;
    }
}

static void log_message(const char* format, ...) {
    // Allocate console on first log (debug builds only)
    allocate_console();
    
    if (!g_log_file) {
        // Try to open log file
        char path[MAX_PATH];
        GetModuleFileNameA(nullptr, path, MAX_PATH);
        std::filesystem::path exe_path(path);
        auto log_path = exe_path.parent_path() / "MDB" / "Logs" / "MDB.log";
        
        // Create directories if needed
        std::filesystem::create_directories(log_path.parent_path());
        
        g_log_file = fopen(log_path.string().c_str(), "a");
    }
    
    va_list args;
    va_start(args, format);
    
    // Timestamp
    SYSTEMTIME st;
    GetLocalTime(&st);
    char timestamp[32];
    snprintf(timestamp, sizeof(timestamp), "[%02d:%02d:%02d.%03d] ", 
             st.wHour, st.wMinute, st.wSecond, st.wMilliseconds);
    
    // Log to file
    if (g_log_file) {
        fprintf(g_log_file, "%s", timestamp);
        vfprintf(g_log_file, format, args);
        fprintf(g_log_file, "\n");
        fflush(g_log_file);
    }
    
    // Also print to console if available
    if (g_console_allocated) {
        HANDLE hConsole = GetStdHandle(STD_OUTPUT_HANDLE);
        
        // Set color based on log level in format string
        // Blue for INFO, Yellow for WARN, Red for ERROR
        if (strstr(format, "[ERROR]")) {
            SetConsoleTextAttribute(hConsole, FOREGROUND_RED | FOREGROUND_INTENSITY);
        } else if (strstr(format, "[WARN]")) {
            SetConsoleTextAttribute(hConsole, FOREGROUND_RED | FOREGROUND_GREEN | FOREGROUND_INTENSITY);
        } else {
            // Default to blue for INFO and other messages
            SetConsoleTextAttribute(hConsole, FOREGROUND_BLUE | FOREGROUND_INTENSITY);
        }
        
        va_list args_copy;
        va_copy(args_copy, args);
        printf("%s", timestamp);
        vprintf(format, args_copy);
        printf("\n");
        va_end(args_copy);
        
        // Reset to default gray
        SetConsoleTextAttribute(hConsole, FOREGROUND_RED | FOREGROUND_GREEN | FOREGROUND_BLUE);
    }
    
    va_end(args);
}

// Get the MDB directory path (next to the game executable)
static std::wstring get_mdb_directory() {
    wchar_t path[MAX_PATH];
    GetModuleFileNameW(nullptr, path, MAX_PATH);
    std::filesystem::path exe_path(path);
    return (exe_path.parent_path() / L"MDB").wstring();
}

// Initialize the .NET Framework CLR
static bool initialize_clr() {
    if (g_clr_initialized) {
        return true;
    }
    
    LOG_INFO("Initializing .NET Framework CLR...");
    
    HRESULT hr;
    
    // Get the CLR meta host
    hr = CLRCreateInstance(CLSID_CLRMetaHost, IID_ICLRMetaHost, (void**)&g_pMetaHost);
    if (FAILED(hr)) {
        LOG_ERROR("CLRCreateInstance failed: 0x%08X", hr);
        return false;
    }
    
    // Get runtime info for .NET Framework 4.0 (v4.0.30319 covers 4.0-4.8)
    hr = g_pMetaHost->GetRuntime(L"v4.0.30319", IID_ICLRRuntimeInfo, (void**)&g_pRuntimeInfo);
    if (FAILED(hr)) {
        LOG_ERROR("GetRuntime failed: 0x%08X", hr);
        return false;
    }
    LOG_DEBUG("Got CLR runtime v4.0.30319");
    
    // Check if the runtime is loadable
    BOOL loadable = FALSE;
    hr = g_pRuntimeInfo->IsLoadable(&loadable);
    if (FAILED(hr) || !loadable) {
        LOG_ERROR(".NET Framework 4.x runtime is not loadable");
        return false;
    }
    
    // Get the runtime host interface
    hr = g_pRuntimeInfo->GetInterface(CLSID_CLRRuntimeHost, IID_ICLRRuntimeHost, (void**)&g_pRuntimeHost);
    if (FAILED(hr)) {
        LOG_ERROR("GetInterface for CLRRuntimeHost failed: 0x%08X", hr);
        return false;
    }
    
    // Start the CLR
    hr = g_pRuntimeHost->Start();
    if (FAILED(hr)) {
        LOG_ERROR("CLR Start failed: 0x%08X", hr);
        return false;
    }
    
    LOG_INFO("CLR initialized successfully");
    g_clr_initialized = true;
    return true;
}

// Load and initialize the managed mod system
static bool load_managed_assemblies() {
    if (g_mods_loaded) {
        return true;
    }
    
    if (!g_clr_initialized) {
        LOG_ERROR("CLR not initialized");
        return false;
    }
    
    LOG_INFO("Loading managed assemblies...");
    
    std::wstring mdb_dir = get_mdb_directory();
    std::wstring managed_dir = mdb_dir + L"\\Managed";
    std::wstring modhost_dll = managed_dir + L"\\GameSDK.ModHost.dll";
    
    // Check if the ModHost DLL exists
    if (!std::filesystem::exists(modhost_dll)) {
        LOG_ERROR("GameSDK.ModHost.dll not found at: %ls", modhost_dll.c_str());
        return false;
    }
    
    LOG_DEBUG("Loading ModHost from: %ls", modhost_dll.c_str());
    
    // Execute the ModManager.Initialize method
    // This will load all mods from the Mods folder
    DWORD retVal = 0;
    HRESULT hr = g_pRuntimeHost->ExecuteInDefaultAppDomain(
        modhost_dll.c_str(),
        L"GameSDK.ModHost.ModManager",
        L"Initialize",
        L"",  // No arguments
        &retVal
    );
    
    if (FAILED(hr)) {
        LOG_ERROR("ExecuteInDefaultAppDomain failed: 0x%08X", hr);
        return false;
    }
    
    if (retVal != 0) {
        LOG_WARN("ModManager.Initialize returned: %d", retVal);
    }
    
    g_mods_loaded = true;
    return true;
}

// Cleanup CLR resources
static void shutdown_clr() {
    LOG_DEBUG("Shutting down CLR...");
    
    if (g_pRuntimeHost) {
        g_pRuntimeHost->Stop();
        g_pRuntimeHost->Release();
        g_pRuntimeHost = nullptr;
    }
    
    if (g_pRuntimeInfo) {
        g_pRuntimeInfo->Release();
        g_pRuntimeInfo = nullptr;
    }
    
    if (g_pMetaHost) {
        g_pMetaHost->Release();
        g_pMetaHost = nullptr;
    }
    
    g_clr_initialized = false;
    g_mods_loaded = false;
    
    if (g_log_file) {
        fclose(g_log_file);
        g_log_file = nullptr;
    }
    
    if (g_console_allocated) {
        FreeConsole();
        g_console_allocated = false;
    }
}

// Prepare game SDK by dumping and generating wrappers if needed
static bool prepare_game_sdk() {
    std::wstring mdb_dir = get_mdb_directory();
    std::filesystem::path mdb_path(mdb_dir);
    
    // Define paths
    auto dump_dir = mdb_path / L"Dump";
    auto dump_file = dump_dir / L"dump.cs";
    auto generated_dir = mdb_path.parent_path() / L"MDB_Core" / L"Generated";
    auto core_project = mdb_path.parent_path() / L"MDB_Core" / L"MDB_Core.csproj";
    
    // Validate that MDB_Core directory exists
    auto core_dir = mdb_path.parent_path() / L"MDB_Core";
    if (!std::filesystem::exists(core_dir)) {
        LOG_ERROR("MDB_Core directory not found at: %ls", core_dir.c_str());
        LOG_ERROR("Expected structure: <GameFolder>/MDB/ and <GameFolder>/MDB_Core/");
        return false;
    }
    
    // Validate that MDB_Core.csproj exists
    if (!std::filesystem::exists(core_project)) {
        LOG_ERROR("MDB_Core.csproj not found at: %ls", core_project.c_str());
        return false;
    }
    
    // Convert paths to narrow strings for logging
    std::string dump_dir_str = dump_dir.string();
    std::string dump_file_str = dump_file.string();
    std::string generated_dir_str = generated_dir.string();
    std::string core_project_str = core_project.string();
    
    // Check if wrappers already exist and are fresh
    if (MDB::WrapperGen::AreWrappersFresh(generated_dir_str)) {
        LOG_INFO("Game SDK wrappers are up to date, skipping generation");
        return true;
    }
    
    LOG_INFO("=== Game SDK Preparation ===");
    LOG_INFO("Step 1/3: Dumping IL2CPP metadata...");
    
    // Step 1: Dump IL2CPP metadata
    auto dump_result = MDB::Dumper::DumpIL2CppRuntime(dump_dir_str);
    if (!dump_result.success) {
        LOG_ERROR("Failed to dump IL2CPP metadata: %s", dump_result.error_message.c_str());
        return false;
    }
    
    LOG_INFO("  Dumped %zu classes from %zu assemblies", 
             dump_result.total_classes, dump_result.total_assemblies);
    LOG_INFO("  Dump saved to: %s", dump_result.dump_path.c_str());
    
    LOG_INFO("Step 2/3: Generating C# wrapper classes...");
    
    // Step 2: Generate C# wrappers
    auto gen_result = MDB::WrapperGen::GenerateWrappers(
        dump_file_str,
        generated_dir_str,
        "GameSDK"
    );
    
    if (!gen_result.success) {
        LOG_ERROR("Failed to generate wrappers: %s", gen_result.error_message.c_str());
        return false;
    }
    
    LOG_INFO("  Generated %zu wrapper files", gen_result.generated_files.size());
    LOG_INFO("  Total classes: %zu", gen_result.total_classes_generated);
    
    LOG_INFO("Step 3/3: Building MDB_Core project...");
    
    // Step 3: Build MDB_Core project with MSBuild
    auto build_result = MDB::Build::TriggerBuild(core_project_str);
    
    if (!build_result.success) {
        LOG_ERROR("Failed to build MDB_Core: %s", build_result.error_message.c_str());
        if (!build_result.build_output.empty()) {
            LOG_ERROR("Build output:\n%s", build_result.build_output.c_str());
        }
        return false;
    }
    
    LOG_INFO("  Build succeeded!");
    if (!build_result.build_output.empty()) {
        LOG_DEBUG("Build output:\n%s", build_result.build_output.c_str());
    }
    
    LOG_INFO("=== Game SDK Ready ===");
    return true;
}

// Background thread for initialization
// We delay initialization to ensure the game has loaded IL2CPP
static DWORD WINAPI initialization_thread(LPVOID lpParam) {
    // Wait for GameAssembly.dll to be loaded
    LOG_DEBUG("Waiting for GameAssembly.dll...");
    HMODULE hGameAssembly = nullptr;
    for (int i = 0; i < 300 && !hGameAssembly; ++i) {  // Wait up to 30 seconds
        hGameAssembly = GetModuleHandleA("GameAssembly.dll");
        if (!hGameAssembly) {
            Sleep(100);
        }
    }
    
    if (!hGameAssembly) {
        LOG_ERROR("GameAssembly.dll not found after 30 seconds");
        return 1;
    }
    
    LOG_DEBUG("GameAssembly.dll found at: 0x%p", hGameAssembly);
    
    // Initialize IL2CPP bridge
    LOG_INFO("Initializing IL2CPP bridge...");
    int result = mdb_init();
    if (result != 0) {
        LOG_ERROR("mdb_init failed with code: %d (%s)", result, mdb_get_last_error());
        return 1;
    }
    LOG_DEBUG("IL2CPP bridge initialized");
    
    // Attach this thread to IL2CPP
    void* domain = mdb_domain_get();
    if (domain) {
        mdb_thread_attach(domain);
        LOG_DEBUG("Thread attached to IL2CPP domain");
    }
    
    // Prepare Game SDK (dump + generate + build)
    LOG_INFO("Preparing Game SDK...");
    if (!prepare_game_sdk()) {
        LOG_ERROR("Failed to prepare Game SDK");
        return 1;
    }
    
    // Initialize CLR and load mods
    if (!initialize_clr()) {
        LOG_ERROR("Failed to initialize CLR");
        return 1;
    }
    
    // Small delay to let the game initialize more
    Sleep(1000);
    
    if (!load_managed_assemblies()) {
        LOG_ERROR("Failed to load managed assemblies");
        return 1;
    }
    
    return 0;
}

// DLL entry point
BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved) {
    switch (ul_reason_for_call) {
        case DLL_PROCESS_ATTACH:
            DisableThreadLibraryCalls(hModule);
            
            // Create initialization thread
            CreateThread(nullptr, 0, initialization_thread, nullptr, 0, nullptr);
            break;
            
        case DLL_PROCESS_DETACH:
            shutdown_clr();
            il2cpp::cleanup();
            break;
            
        case DLL_THREAD_ATTACH:
        case DLL_THREAD_DETACH:
            break;
    }
    return TRUE;
}
