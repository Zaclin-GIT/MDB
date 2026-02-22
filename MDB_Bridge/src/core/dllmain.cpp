// ==============================
// MDB Bridge - DLL Entry Point & CLR Hosting
// ==============================
// This file handles DLL initialization and hosts the .NET Framework CLR
// to load and execute the managed mod assemblies.

#include "bridge_exports.h"
#include "core/mdb_log.h"
#include "il2cpp/il2cpp_resolver.hpp"
#include "il2cpp/il2cpp_dumper.hpp"
// wrapper_generator.hpp removed — dumper now generates wrappers directly
#include "codegen/build_trigger.hpp"

#include <windows.h>
#include <metahost.h>
#include <mscoree.h>
#include <string>
#include <filesystem>

#pragma comment(lib, "mscoree.lib")

// CLR interfaces
static ICLRMetaHost* g_pMetaHost = nullptr;
static ICLRRuntimeInfo* g_pRuntimeInfo = nullptr;
static ICLRRuntimeHost* g_pRuntimeHost = nullptr;
static bool g_clr_initialized = false;
static bool g_mods_loaded = false;

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
    
    if (mdb_log_detail::log_file()) {
        fclose(mdb_log_detail::log_file());
        mdb_log_detail::log_file() = nullptr;
    }
    
    if (mdb_log_detail::console_allocated()) {
        FreeConsole();
        mdb_log_detail::console_allocated() = false;
    }
}

// Create the expected directory structure next to the game executable.
// Returns true if all directories exist (or were created), false on failure.
static bool ensure_directory_structure() {
    std::wstring mdb_dir = get_mdb_directory();
    std::filesystem::path mdb(mdb_dir);
    std::filesystem::path game_dir = mdb.parent_path();
    
    // Directories that must exist at runtime
    const std::filesystem::path dirs[] = {
        mdb,                                    // MDB/
        mdb / L"Logs",                          // MDB/Logs/
        mdb / L"Managed",                       // MDB/Managed/  (build output)
        mdb / L"Mods",                          // MDB/Mods/     (user mods)
        game_dir / L"MDB_Core",                 // MDB_Core/     (project sources)
        game_dir / L"MDB_Core" / L"Generated",  // MDB_Core/Generated/  (generated wrappers)
    };
    
    for (const auto& dir : dirs) {
        try {
            if (!std::filesystem::exists(dir)) {
                std::filesystem::create_directories(dir);
                LOG_INFO("Created directory: %ls", dir.c_str());
            }
        } catch (const std::exception& e) {
            LOG_ERROR("Failed to create directory %ls: %s", dir.c_str(), e.what());
            return false;
        }
    }
    
    return true;
}

// Prepare game SDK by dumping and generating wrappers if needed
static bool prepare_game_sdk() {
    std::wstring mdb_dir = get_mdb_directory();
    std::filesystem::path mdb_path(mdb_dir);
    
    // Define paths
    auto generated_dir = mdb_path.parent_path() / L"MDB_Core" / L"Generated";
    auto core_project = mdb_path.parent_path() / L"MDB_Core" / L"MDB_Core.csproj";
    
    auto managed_dll = mdb_path / L"Managed" / L"GameSDK.ModHost.dll";
    
    // Validate that MDB_Core.csproj exists
    if (!std::filesystem::exists(core_project)) {
        LOG_ERROR("MDB_Core.csproj not found at: %ls", core_project.c_str());
        LOG_ERROR("Please deploy the MDB_Core project to: %ls", (mdb_path.parent_path() / L"MDB_Core").c_str());
        return false;
    }
    
    // Convert paths to narrow strings for logging
    std::string generated_dir_str = generated_dir.string();
    std::string core_project_str = core_project.string();
    
    bool dll_exists = std::filesystem::exists(managed_dll);
    
    // Check if wrappers already exist and are fresh AND the built DLL exists
    if (MDB::Dumper::AreWrappersFresh(generated_dir_str) && dll_exists) {
        LOG_INFO("Game SDK wrappers and managed DLL are up to date, skipping");
        return true;
    }
    
    bool need_dump = !MDB::Dumper::AreWrappersFresh(generated_dir_str);
    bool need_build = need_dump || !dll_exists;
    
    LOG_INFO("=== Game SDK Preparation ===");
    
    // Step 1: Dump IL2CPP metadata and generate buildable C# wrappers (if needed)
    if (need_dump) {
        LOG_INFO("Step 1/2: Dumping IL2CPP metadata & generating C# wrappers...");
        
        auto dump_result = MDB::Dumper::DumpIL2CppRuntime(generated_dir_str);
        if (!dump_result.success) {
            LOG_ERROR("Failed to dump/generate: %s", dump_result.error_message.c_str());
            return false;
        }
        
        LOG_INFO("  Dumped %zu classes from %zu assemblies", 
                 dump_result.total_classes, dump_result.total_assemblies);
        LOG_INFO("  Generated %zu wrapper files (%zu classes)",
                 dump_result.generated_files.size(), dump_result.total_wrappers_generated);
        if (dump_result.fake_methods_detected > 0 || dump_result.fake_classes_detected > 0) {
            LOG_INFO("  Obfuscation: filtered %zu fake methods, %zu fake classes",
                     dump_result.fake_methods_detected, dump_result.fake_classes_detected);
            LOG_INFO("  Obfuscation report: %s", dump_result.fake_report_path.c_str());
        }
        if (dump_result.mappings_loaded > 0) {
            LOG_INFO("  Deobfuscation: applied %zu friendly name mappings to SDK",
                     dump_result.mappings_loaded);
        }
    } else {
        LOG_INFO("Step 1/2: Wrappers up to date, skipping dump");
    }
    
    // Step 2: Build MDB_Core project with MSBuild
    if (need_build) {
        LOG_INFO("Step 2/2: Building MDB_Core project...");
        
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
    
    // Ensure the expected directory structure exists
    if (!ensure_directory_structure()) {
        LOG_ERROR("Failed to create required directory structure");
        return 1;
    }
    
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
