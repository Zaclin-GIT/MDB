// ==============================
// ImGui Integration Implementation for MDB_Bridge
// ==============================
// Auto-detects DirectX version and provides ImGui rendering hooks.

#include "imgui_integration.h"
#include "core/mdb_log.h"
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

// ========== HRESULT Helper ==========

namespace {

static const char* HResultToStr(HRESULT hr) {
    switch (hr) {
    case S_OK:                              return "S_OK";
    case E_OUTOFMEMORY:                     return "E_OUTOFMEMORY";
    case E_INVALIDARG:                      return "E_INVALIDARG";
    case E_FAIL:                            return "E_FAIL";
    case E_NOINTERFACE:                     return "E_NOINTERFACE";
    case DXGI_ERROR_DEVICE_REMOVED:         return "DXGI_ERROR_DEVICE_REMOVED";
    case DXGI_ERROR_DEVICE_HUNG:            return "DXGI_ERROR_DEVICE_HUNG";
    case DXGI_ERROR_DEVICE_RESET:           return "DXGI_ERROR_DEVICE_RESET";
    case DXGI_ERROR_INVALID_CALL:           return "DXGI_ERROR_INVALID_CALL";
    case DXGI_ERROR_ACCESS_DENIED:          return "DXGI_ERROR_ACCESS_DENIED";
    case DXGI_ERROR_UNSUPPORTED:            return "DXGI_ERROR_UNSUPPORTED";
    case DXGI_ERROR_SDK_COMPONENT_MISSING:  return "DXGI_ERROR_SDK_COMPONENT_MISSING";
    default:                                return "UNKNOWN";
    }
}

} // anonymous namespace

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

// Attempt to create a dummy D3D11 device+swapchain targeting the given HWND.
// On success, extracts the Present vTable pointer and cleans up.
static bool TryCreateDummySwapChain(HWND hWnd, const char* label, void*& outPresent) {
    outPresent = nullptr;

    // Try with explicit FL 11_0 first, then let the runtime pick if that fails
    D3D_FEATURE_LEVEL requestedLevel = D3D_FEATURE_LEVEL_11_0;
    D3D_FEATURE_LEVEL* pLevels = &requestedLevel;
    UINT numLevels = 1;

    for (int attempt = 0; attempt < 2; ++attempt) {
        DXGI_SWAP_CHAIN_DESC desc = {};
        desc.BufferCount = 1;
        desc.BufferDesc.Format = DXGI_FORMAT_R8G8B8A8_UNORM;
        desc.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
        desc.OutputWindow = hWnd;
        desc.SampleDesc.Count = 1;
        desc.Windowed = TRUE;
        desc.SwapEffect = DXGI_SWAP_EFFECT_DISCARD;

        IDXGISwapChain* pSwap = nullptr;
        ID3D11Device* pDev = nullptr;
        ID3D11DeviceContext* pCtx = nullptr;
        D3D_FEATURE_LEVEL achievedLevel{};

        HRESULT hr = D3D11CreateDeviceAndSwapChain(
            nullptr, D3D_DRIVER_TYPE_HARDWARE, nullptr, 0,
            pLevels, numLevels, D3D11_SDK_VERSION,
            &desc, &pSwap, &pDev,
            &achievedLevel, &pCtx
        );

        if (SUCCEEDED(hr)) {
            LOG_INFO("[ImGui] [%s] Dummy swapchain created (attempt %d, feature level 0x%x)",
                      label, attempt + 1, (unsigned)achievedLevel);

            void** vTable = *reinterpret_cast<void***>(pSwap);
            outPresent = vTable[8]; // IDXGISwapChain::Present

            pSwap->Release();
            pCtx->Release();
            pDev->Release();
            return true;
        }

        LOG_WARN("[ImGui] [%s] D3D11CreateDeviceAndSwapChain failed (attempt %d): "
                  "HRESULT=0x%08X (%s), featureLevels=%s",
                  label, attempt + 1, (unsigned)hr, HResultToStr(hr),
                  (attempt == 0) ? "11_0 explicit" : "nullptr (runtime default)");

        // Second attempt: let the runtime choose the best feature level
        pLevels = nullptr;
        numLevels = 0;
    }

    return false;
}

bool HookDX11Present() {
    LOG_INFO("[ImGui] HookDX11Present: starting DX11 Present hook sequence");

    void* pPresent = nullptr;

    // ---- Attempt 1: Desktop Window (fast, works on most systems) ----
    {
        HWND hDesktop = GetDesktopWindow();
        LOG_INFO("[ImGui] Attempt 1: Using GetDesktopWindow() -> HWND 0x%p", (void*)hDesktop);

        if (TryCreateDummySwapChain(hDesktop, "DesktopWnd", pPresent)) {
            LOG_INFO("[ImGui] Attempt 1 succeeded, Present @ 0x%p", pPresent);
        }
    }

    // ---- Attempt 2: Hidden temporary window (fallback) ----
    if (!pPresent) {
        LOG_WARN("[ImGui] Attempt 1 failed, trying hidden window fallback...");

        const wchar_t* className = L"MDB_DummyDX11Wnd";
        HINSTANCE hInst = GetModuleHandleW(nullptr);

        WNDCLASSEXW wc = {};
        wc.cbSize = sizeof(wc);
        wc.lpfnWndProc = DefWindowProcW;
        wc.hInstance = hInst;
        wc.lpszClassName = className;

        if (!RegisterClassExW(&wc)) {
            DWORD err = GetLastError();
            // ERROR_CLASS_ALREADY_EXISTS (1410) is fine
            if (err != ERROR_CLASS_ALREADY_EXISTS) {
                LOG_ERROR("[ImGui] Attempt 2: RegisterClassExW failed, GetLastError=%lu", err);
            }
        }

        HWND hHidden = CreateWindowExW(
            0, className, L"", WS_OVERLAPPEDWINDOW,
            0, 0, 100, 100, nullptr, nullptr, hInst, nullptr);

        if (!hHidden) {
            LOG_ERROR("[ImGui] Attempt 2: CreateWindowExW failed, GetLastError=%lu", GetLastError());
        } else {
            LOG_INFO("[ImGui] Attempt 2: Using hidden window -> HWND 0x%p", (void*)hHidden);

            if (TryCreateDummySwapChain(hHidden, "HiddenWnd", pPresent)) {
                LOG_INFO("[ImGui] Attempt 2 succeeded, Present @ 0x%p", pPresent);
            } else {
                LOG_ERROR("[ImGui] Attempt 2 also failed");
            }

            DestroyWindow(hHidden);
        }

        UnregisterClassW(className, hInst);
    }

    // ---- Evaluate results ----
    if (!pPresent) {
        LOG_ERROR("[ImGui] HookDX11Present: All attempts to obtain Present vTable pointer failed");

        // Log extra diagnostics to help users report the issue
        LOG_ERROR("[ImGui]   Diagnostics:");

        // Check adapter info
        IDXGIFactory* pFactory = nullptr;
        if (SUCCEEDED(CreateDXGIFactory(IID_PPV_ARGS(&pFactory)))) {
            IDXGIAdapter* pAdapter = nullptr;
            for (UINT i = 0; pFactory->EnumAdapters(i, &pAdapter) != DXGI_ERROR_NOT_FOUND; ++i) {
                DXGI_ADAPTER_DESC adapterDesc;
                if (SUCCEEDED(pAdapter->GetDesc(&adapterDesc))) {
                    char adapterName[128];
                    WideCharToMultiByte(CP_UTF8, 0, adapterDesc.Description, -1, adapterName, sizeof(adapterName), nullptr, nullptr);
                    LOG_ERROR("[ImGui]     Adapter %u: %s (VRAM: %llu MB, Vendor: 0x%04X, Device: 0x%04X)",
                              i, adapterName,
                              (unsigned long long)(adapterDesc.DedicatedVideoMemory / (1024 * 1024)),
                              adapterDesc.VendorId, adapterDesc.DeviceId);
                }
                pAdapter->Release();
            }
            pFactory->Release();
        } else {
            LOG_ERROR("[ImGui]     CreateDXGIFactory failed - no DXGI available");
        }

        // Check for RDP / remote session
        if (GetSystemMetrics(SM_REMOTESESSION)) {
            LOG_ERROR("[ImGui]     ** Remote Desktop session detected - hardware GPU may not be available **");
        }

        return false;
    }

    // ---- Install MinHook ----
    LOG_INFO("[ImGui] Installing MinHook on Present @ 0x%p", pPresent);

    MH_STATUS mhStatus = MH_CreateHook(pPresent, &HookedPresent11, reinterpret_cast<void**>(&g_originalPresent));
    if (mhStatus != MH_OK) {
        LOG_ERROR("[ImGui] MH_CreateHook failed: %s (code %d). "
                  "Another overlay (Steam/Discord/RivaTuner/MSI Afterburner) may have already hooked Present.",
                  MH_StatusToString(mhStatus), (int)mhStatus);
        return false;
    }

    mhStatus = MH_EnableHook(pPresent);
    if (mhStatus != MH_OK) {
        LOG_ERROR("[ImGui] MH_EnableHook failed: %s (code %d)",
                  MH_StatusToString(mhStatus), (int)mhStatus);
        MH_RemoveHook(pPresent);
        return false;
    }

    LOG_INFO("[ImGui] HookDX11Present: Present hook installed successfully");
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
    LOG_INFO("[ImGui] mdb_imgui_init called");

    if (g_initialized.load()) {
        LOG_INFO("[ImGui] Already initialized, returning true");
        return true;
    }

    // Ensure MinHook is initialized (needed for DX Present hooking)
    static bool s_mhInitialized = false;
    if (!s_mhInitialized) {
        MH_STATUS status = MH_Initialize();
        if (status != MH_OK && status != MH_ERROR_ALREADY_INITIALIZED) {
            LOG_ERROR("[ImGui] MH_Initialize failed: %s (code %d)",
                      MH_StatusToString(status), (int)status);
            return false;
        }
        LOG_INFO("[ImGui] MinHook initialized (status: %s)", MH_StatusToString(status));
        s_mhInitialized = true;
    }

    // Log loaded graphics modules for diagnostics
    HMODULE hD3D11 = GetModuleHandleW(L"d3d11.dll");
    HMODULE hD3D12 = GetModuleHandleW(L"d3d12.dll");
    HMODULE hDXGI  = GetModuleHandleW(L"dxgi.dll");
    LOG_INFO("[ImGui] Module check: d3d11.dll=0x%p, d3d12.dll=0x%p, dxgi.dll=0x%p",
              (void*)hD3D11, (void*)hD3D12, (void*)hDXGI);

    // Detect DirectX version
    g_dxVersion.store(DetectDxVersion());
    LOG_INFO("[ImGui] Initial DX detection: %d", (int)g_dxVersion.load());

    if (g_dxVersion.load() == MDB_DX_UNKNOWN) {
        LOG_WARN("[ImGui] DX version unknown, polling up to 10 times (100ms each)...");
        for (int i = 0; i < 10; i++) {
            Sleep(100);
            g_dxVersion.store(DetectDxVersion());
            if (g_dxVersion.load() != MDB_DX_UNKNOWN) {
                LOG_INFO("[ImGui] DX detected after %d polls: %d", i + 1, (int)g_dxVersion.load());
                break;
            }
        }
    }

    switch (g_dxVersion.load()) {
    case MDB_DX_11: {
        LOG_INFO("[ImGui] Proceeding with DX11 Present hook");
        bool result = HookDX11Present();
        if (!result) {
            LOG_ERROR("[ImGui] DX11 Present hook FAILED - ImGui will not be available");
        }
        return result;
    }
    case MDB_DX_12:
        LOG_ERROR("[ImGui] DX12 detected but not yet supported");
        return false;
    default:
        LOG_ERROR("[ImGui] No DirectX version detected after all retries. "
                  "d3d11.dll loaded: %s, d3d12.dll loaded: %s",
                  hD3D11 ? "YES" : "NO", hD3D12 ? "YES" : "NO");
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
