// ==============================
// ImGui Integration Implementation for MDB_Bridge
// ==============================
// Auto-detects DirectX version and provides ImGui rendering hooks.

#include "imgui_integration.h"
#include <MinHook.h>

// ImGui headers
#include "imgui.h"
#include "imgui_impl_win32.h"
#include "imgui_impl_dx11.h"
// #include "imgui_impl_dx12.h"  // Add DX12 support later

#include <mutex>
#include <atomic>
#include <vector>
#include <string>
#include <algorithm>

// ========== Forward declarations ==========

extern IMGUI_IMPL_API LRESULT ImGui_ImplWin32_WndProcHandler(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam);

// ========== Global State ==========

namespace {

// DirectX version
std::atomic<MdbDxVersion> g_dxVersion{ MDB_DX_UNKNOWN };

// Initialization state
std::atomic<bool> g_initialized{ false };
std::atomic<bool> g_inputEnabled{ true };

// Toggle key
std::atomic<int> g_toggleKey{ VK_F2 };
bool g_toggleKeyWasDown = false;

// ========== Multi-Callback System ==========

struct ImGuiCallbackInfo {
    int id;
    std::string name;
    MdbImGuiDrawCallback callback;
    int priority;
    bool enabled;
};

// Vector of registered callbacks sorted by priority
std::vector<ImGuiCallbackInfo> g_callbacks;
std::mutex g_callbackMutex;
std::atomic<int> g_nextCallbackId{ 1 };

// Legacy single callback for backwards compatibility
MdbImGuiDrawCallback g_legacyCallback = nullptr;

// DX11 state
ID3D11Device* g_pd3dDevice11 = nullptr;
ID3D11DeviceContext* g_pd3dDeviceContext = nullptr;
IDXGISwapChain* g_pSwapChain = nullptr;
ID3D11RenderTargetView* g_mainRenderTargetView = nullptr;

// DX12 state
ID3D12Device* g_pd3dDevice12 = nullptr;
ID3D12CommandQueue* g_pCommandQueue = nullptr;
ID3D12DescriptorHeap* g_pd3dSrvDescHeap = nullptr;
// DX12 requires more complex setup - defer for now

// Window handle
HWND g_hWnd = nullptr;
WNDPROC g_originalWndProc = nullptr;

// Hook pointers
typedef HRESULT(WINAPI* PFN_Present)(IDXGISwapChain*, UINT, UINT);
PFN_Present g_originalPresent = nullptr;

typedef void(WINAPI* PFN_ExecuteCommandLists)(ID3D12CommandQueue*, UINT, ID3D12CommandList* const*);
PFN_ExecuteCommandLists g_originalExecuteCommandLists = nullptr;

// Helper: Invoke all registered callbacks
void InvokeAllCallbacks() {
    std::lock_guard<std::mutex> lock(g_callbackMutex);
    
    // Invoke legacy callback first (if any)
    if (g_legacyCallback) {
        try {
            g_legacyCallback();
        } catch (...) {
            // Silently ignore callback errors
        }
    }
    
    // Invoke all registered callbacks in priority order
    for (const auto& info : g_callbacks) {
        if (info.enabled && info.callback) {
            try {
                info.callback();
            } catch (...) {
                // Silently ignore callback errors
            }
        }
    }
}

// Helper: Sort callbacks by priority (higher priority first)
void SortCallbacks() {
    std::sort(g_callbacks.begin(), g_callbacks.end(),
        [](const ImGuiCallbackInfo& a, const ImGuiCallbackInfo& b) {
            return a.priority > b.priority;
        });
}

// ========== DX11 Helpers ==========

bool CreateRenderTarget11() {
    ID3D11Texture2D* pBackBuffer = nullptr;
    if (FAILED(g_pSwapChain->GetBuffer(0, IID_PPV_ARGS(&pBackBuffer)))) {
        return false;
    }
    HRESULT hr = g_pd3dDevice11->CreateRenderTargetView(pBackBuffer, nullptr, &g_mainRenderTargetView);
    pBackBuffer->Release();
    return SUCCEEDED(hr);
}

void CleanupRenderTarget11() {
    if (g_mainRenderTargetView) {
        g_mainRenderTargetView->Release();
        g_mainRenderTargetView = nullptr;
    }
}

// ========== WndProc Hook ==========

LRESULT WINAPI HookedWndProc(HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam) {
    // Handle toggle key
    if (msg == WM_KEYDOWN && wParam == static_cast<WPARAM>(g_toggleKey.load())) {
        if (!g_toggleKeyWasDown) {
            g_toggleKeyWasDown = true;
            g_inputEnabled.store(!g_inputEnabled.load());
        }
    }
    else if (msg == WM_KEYUP && wParam == static_cast<WPARAM>(g_toggleKey.load())) {
        g_toggleKeyWasDown = false;
    }

    // Forward to ImGui if input enabled
    if (g_inputEnabled.load() && g_initialized.load()) {
        if (ImGui_ImplWin32_WndProcHandler(hWnd, msg, wParam, lParam)) {
            return true;
        }

        // Block input from reaching game when ImGui wants it
        ImGuiIO& io = ImGui::GetIO();
        if (io.WantCaptureMouse && (msg >= WM_MOUSEFIRST && msg <= WM_MOUSELAST)) {
            return true;
        }
        if (io.WantCaptureKeyboard && (msg >= WM_KEYFIRST && msg <= WM_KEYLAST)) {
            // Still allow toggle key through
            if (wParam != static_cast<WPARAM>(g_toggleKey.load())) {
                return true;
            }
        }
    }

    return CallWindowProcW(g_originalWndProc, hWnd, msg, wParam, lParam);
}

// ========== Present Hook (DX11) ==========

HRESULT WINAPI HookedPresent11(IDXGISwapChain* pSwapChain, UINT SyncInterval, UINT Flags) {
    static bool firstCall = true;

    if (firstCall) {
        firstCall = false;

        // Get device and context
        if (SUCCEEDED(pSwapChain->GetDevice(IID_PPV_ARGS(&g_pd3dDevice11)))) {
            g_pd3dDevice11->GetImmediateContext(&g_pd3dDeviceContext);
            g_pSwapChain = pSwapChain;

            // Get window handle
            DXGI_SWAP_CHAIN_DESC desc;
            pSwapChain->GetDesc(&desc);
            g_hWnd = desc.OutputWindow;

            // Hook WndProc
            g_originalWndProc = (WNDPROC)SetWindowLongPtrW(g_hWnd, GWLP_WNDPROC, (LONG_PTR)HookedWndProc);

            // Initialize ImGui
            IMGUI_CHECKVERSION();
            ImGui::CreateContext();
            ImGuiIO& io = ImGui::GetIO();
            io.ConfigFlags |= ImGuiConfigFlags_NavEnableKeyboard;
            io.IniFilename = nullptr; // Don't save settings

            // Setup style (UnityExplorer-like dark theme)
            ImGui::StyleColorsDark();
            ImGuiStyle& style = ImGui::GetStyle();
            style.WindowRounding = 0.0f;
            style.FrameRounding = 0.0f;
            style.ScrollbarRounding = 0.0f;
            style.Colors[ImGuiCol_WindowBg] = ImVec4(0.065f, 0.065f, 0.065f, 1.0f);
            style.Colors[ImGuiCol_TitleBg] = ImVec4(0.1f, 0.1f, 0.1f, 1.0f);
            style.Colors[ImGuiCol_TitleBgActive] = ImVec4(0.15f, 0.15f, 0.15f, 1.0f);
            style.Colors[ImGuiCol_FrameBg] = ImVec4(0.1f, 0.1f, 0.1f, 1.0f);
            style.Colors[ImGuiCol_Button] = ImVec4(0.2f, 0.2f, 0.2f, 1.0f);
            style.Colors[ImGuiCol_ButtonHovered] = ImVec4(0.3f, 0.3f, 0.3f, 1.0f);
            style.Colors[ImGuiCol_Header] = ImVec4(0.1f, 0.3f, 0.3f, 1.0f);
            style.Colors[ImGuiCol_HeaderHovered] = ImVec4(0.15f, 0.4f, 0.4f, 1.0f);

            // Setup platform/renderer backends
            ImGui_ImplWin32_Init(g_hWnd);
            ImGui_ImplDX11_Init(g_pd3dDevice11, g_pd3dDeviceContext);

            CreateRenderTarget11();

            g_initialized.store(true);
        }
    }

    // Render ImGui
    if (g_initialized.load()) {
        ImGui_ImplDX11_NewFrame();
        ImGui_ImplWin32_NewFrame();
        ImGui::NewFrame();

        // Invoke all registered callbacks (including legacy)
        bool hasCallbacks = false;
        {
            std::lock_guard<std::mutex> lock(g_callbackMutex);
            hasCallbacks = g_legacyCallback != nullptr || !g_callbacks.empty();
        }
        
        if (hasCallbacks) {
            InvokeAllCallbacks();
        }
        else {
            // Default: show a simple overlay if no callback registered
            if (g_inputEnabled.load()) {
                ImGui::SetNextWindowPos(ImVec2(10, 10), ImGuiCond_FirstUseEver);
                ImGui::Begin("MDB Explorer", nullptr, ImGuiWindowFlags_AlwaysAutoResize);
                ImGui::Text("ImGui initialized successfully!");
                ImGui::Text("Press F2 to toggle input capture");
                ImGui::Text("Waiting for C# callback...");
                ImGui::End();
            }
        }

        ImGui::Render();

        g_pd3dDeviceContext->OMSetRenderTargets(1, &g_mainRenderTargetView, nullptr);
        ImGui_ImplDX11_RenderDrawData(ImGui::GetDrawData());
    }

    return g_originalPresent(pSwapChain, SyncInterval, Flags);
}

// ========== DirectX Detection ==========

MdbDxVersion DetectDxVersion() {
    // Check if d3d12.dll is loaded (check first as games may load both)
    HMODULE hD3D12 = GetModuleHandleW(L"d3d12.dll");
    if (hD3D12) {
        return MDB_DX_12;
    }

    // Check if d3d11.dll is loaded
    HMODULE hD3D11 = GetModuleHandleW(L"d3d11.dll");
    if (hD3D11) {
        return MDB_DX_11;
    }

    return MDB_DX_UNKNOWN;
}

// ========== SwapChain vTable Hook ==========

bool HookDX11Present() {
    // Create dummy device to get vTable
    D3D_FEATURE_LEVEL featureLevel = D3D_FEATURE_LEVEL_11_0;
    DXGI_SWAP_CHAIN_DESC swapChainDesc = {};
    swapChainDesc.BufferCount = 1;
    swapChainDesc.BufferDesc.Format = DXGI_FORMAT_R8G8B8A8_UNORM;
    swapChainDesc.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
    swapChainDesc.OutputWindow = GetDesktopWindow();
    swapChainDesc.SampleDesc.Count = 1;
    swapChainDesc.Windowed = TRUE;
    swapChainDesc.SwapEffect = DXGI_SWAP_EFFECT_DISCARD;

    IDXGISwapChain* pDummySwapChain = nullptr;
    ID3D11Device* pDummyDevice = nullptr;
    ID3D11DeviceContext* pDummyContext = nullptr;

    HRESULT hr = D3D11CreateDeviceAndSwapChain(
        nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, 0,
        &featureLevel, 1, D3D11_SDK_VERSION,
        &swapChainDesc, &pDummySwapChain, &pDummyDevice,
        nullptr, &pDummyContext
    );

    if (FAILED(hr)) {
        return false;
    }

    // Get vTable
    void** vTable = *reinterpret_cast<void***>(pDummySwapChain);

    // Present is at index 8
    void* pPresent = vTable[8];

    // Cleanup dummy objects
    pDummySwapChain->Release();
    pDummyContext->Release();
    pDummyDevice->Release();

    // Hook Present
    if (MH_CreateHook(pPresent, &HookedPresent11, reinterpret_cast<void**>(&g_originalPresent)) != MH_OK) {
        return false;
    }

    if (MH_EnableHook(pPresent) != MH_OK) {
        return false;
    }

    return true;
}

bool HookDX12() {
    // DX12 hook implementation - more complex, defer for now
    // Would need to hook ID3D12CommandQueue::ExecuteCommandLists
    // and manage descriptor heaps for ImGui
    return false;
}

} // anonymous namespace

// ========== Exported C API ==========

extern "C" {

MDB_IMGUI_API MdbDxVersion mdb_imgui_get_dx_version() {
    return g_dxVersion.load();
}

MDB_IMGUI_API bool mdb_imgui_init() {
    if (g_initialized.load()) {
        return true;
    }

    // Detect DirectX version
    g_dxVersion.store(DetectDxVersion());

    if (g_dxVersion.load() == MDB_DX_UNKNOWN) {
        // Try to wait a bit for DirectX to load
        for (int i = 0; i < 10; i++) {
            Sleep(100);
            g_dxVersion.store(DetectDxVersion());
            if (g_dxVersion.load() != MDB_DX_UNKNOWN) break;
        }
    }

    switch (g_dxVersion.load()) {
    case MDB_DX_11:
        return HookDX11Present();
    case MDB_DX_12:
        // DX12 support deferred
        return false;
    default:
        return false;
    }
}

MDB_IMGUI_API void mdb_imgui_shutdown() {
    if (!g_initialized.load()) {
        return;
    }

    g_initialized.store(false);

    // Clear all callbacks
    {
        std::lock_guard<std::mutex> lock(g_callbackMutex);
        g_callbacks.clear();
        g_legacyCallback = nullptr;
    }

    // Restore WndProc
    if (g_hWnd && g_originalWndProc) {
        SetWindowLongPtrW(g_hWnd, GWLP_WNDPROC, (LONG_PTR)g_originalWndProc);
        g_originalWndProc = nullptr;
    }

    // Cleanup ImGui
    if (g_dxVersion.load() == MDB_DX_11) {
        ImGui_ImplDX11_Shutdown();
        ImGui_ImplWin32_Shutdown();
        ImGui::DestroyContext();
        CleanupRenderTarget11();
    }

    // Disable hooks
    MH_DisableHook(MH_ALL_HOOKS);
}

MDB_IMGUI_API bool mdb_imgui_is_initialized() {
    return g_initialized.load();
}

// Legacy single callback (backwards compatibility)
MDB_IMGUI_API void mdb_imgui_register_draw_callback(MdbImGuiDrawCallback callback) {
    std::lock_guard<std::mutex> lock(g_callbackMutex);
    g_legacyCallback = callback;
}

// ========== Multi-Callback API ==========

MDB_IMGUI_API int mdb_imgui_add_callback(const char* name, MdbImGuiDrawCallback callback, int priority) {
    if (!callback) {
        return 0;
    }
    
    std::lock_guard<std::mutex> lock(g_callbackMutex);
    
    int id = g_nextCallbackId.fetch_add(1);
    
    ImGuiCallbackInfo info;
    info.id = id;
    info.name = name ? name : "";
    info.callback = callback;
    info.priority = priority;
    info.enabled = true;
    
    g_callbacks.push_back(info);
    SortCallbacks();
    
    return id;
}

MDB_IMGUI_API bool mdb_imgui_remove_callback(int callbackId) {
    std::lock_guard<std::mutex> lock(g_callbackMutex);
    
    auto it = std::find_if(g_callbacks.begin(), g_callbacks.end(),
        [callbackId](const ImGuiCallbackInfo& info) { return info.id == callbackId; });
    
    if (it != g_callbacks.end()) {
        g_callbacks.erase(it);
        return true;
    }
    
    return false;
}

MDB_IMGUI_API bool mdb_imgui_set_callback_enabled(int callbackId, bool enabled) {
    std::lock_guard<std::mutex> lock(g_callbackMutex);
    
    auto it = std::find_if(g_callbacks.begin(), g_callbacks.end(),
        [callbackId](const ImGuiCallbackInfo& info) { return info.id == callbackId; });
    
    if (it != g_callbacks.end()) {
        it->enabled = enabled;
        return true;
    }
    
    return false;
}

MDB_IMGUI_API int mdb_imgui_get_callback_count() {
    std::lock_guard<std::mutex> lock(g_callbackMutex);
    return static_cast<int>(g_callbacks.size()) + (g_legacyCallback ? 1 : 0);
}

MDB_IMGUI_API void mdb_imgui_set_input_enabled(bool enabled) {
    g_inputEnabled.store(enabled);
}

MDB_IMGUI_API bool mdb_imgui_is_input_enabled() {
    return g_inputEnabled.load();
}

MDB_IMGUI_API void mdb_imgui_set_toggle_key(int vkCode) {
    g_toggleKey.store(vkCode);
}

} // extern "C"

// ========== Internal C++ API ==========

namespace MdbImGui {

bool InitializeHooks() {
    // MinHook should already be initialized by bridge_exports
    return mdb_imgui_init();
}

} // namespace MdbImGui
