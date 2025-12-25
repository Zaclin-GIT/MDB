// ==============================
// ImGui Integration Header for MDB_Bridge
// ==============================
// Auto-detects DirectX version and provides ImGui rendering hooks.
// Requires: Dear ImGui source files in imgui/ subfolder

#pragma once

#include <Windows.h>
#include <d3d11.h>
#include <d3d12.h>
#include <dxgi.h>
#include <dxgi1_4.h>

// Export macro for ImGui functions
#ifdef MDB_BRIDGE_EXPORTS
#define MDB_IMGUI_API __declspec(dllexport)
#else
#define MDB_IMGUI_API __declspec(dllimport)
#endif

#ifdef __cplusplus
extern "C" {
#endif

// ========== DirectX Version Detection ==========

enum MdbDxVersion {
    MDB_DX_UNKNOWN = 0,
    MDB_DX_11 = 11,
    MDB_DX_12 = 12
};

// Get detected DirectX version
MDB_IMGUI_API MdbDxVersion mdb_imgui_get_dx_version();

// ========== ImGui Lifecycle ==========

// Initialize ImGui with auto-detected DirectX version
// Returns true on success
MDB_IMGUI_API bool mdb_imgui_init();

// Shutdown ImGui and unhook everything
MDB_IMGUI_API void mdb_imgui_shutdown();

// Check if ImGui is initialized
MDB_IMGUI_API bool mdb_imgui_is_initialized();

// ========== Draw Callback ==========

// Callback type for C# draw functions
// Called each frame during Present hook, after ImGui::NewFrame()
typedef void (*MdbImGuiDrawCallback)();

// Register a draw callback (called from C#) - DEPRECATED: use multi-callback API
MDB_IMGUI_API void mdb_imgui_register_draw_callback(MdbImGuiDrawCallback callback);

// ========== Multi-Callback API ==========

// Register a named draw callback (supports multiple mods)
// Returns callback ID on success, 0 on failure
MDB_IMGUI_API int mdb_imgui_add_callback(const char* name, MdbImGuiDrawCallback callback, int priority);

// Remove a callback by ID
MDB_IMGUI_API bool mdb_imgui_remove_callback(int callbackId);

// Enable or disable a callback by ID
MDB_IMGUI_API bool mdb_imgui_set_callback_enabled(int callbackId, bool enabled);

// Get the number of registered callbacks
MDB_IMGUI_API int mdb_imgui_get_callback_count();

// ========== Input Control ==========

// Toggle whether ImGui captures input (F2 by default)
MDB_IMGUI_API void mdb_imgui_set_input_enabled(bool enabled);

// Check if ImGui input is enabled
MDB_IMGUI_API bool mdb_imgui_is_input_enabled();

// Set the toggle key (default: VK_F2)
MDB_IMGUI_API void mdb_imgui_set_toggle_key(int vkCode);

#ifdef __cplusplus
}
#endif

// ========== Internal C++ API ==========

#ifdef __cplusplus

namespace MdbImGui {

// Initialize the hook system (called from DllMain)
bool InitializeHooks();

// Process window messages (called from hooked WndProc)
LRESULT ProcessWndProc(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam);

// Render frame (called from hooked Present)
void RenderFrame();

} // namespace MdbImGui

#endif
