---
layout: default
title: Proxy DLL Injection
---

# Proxy DLL Injection — Technical Deep Dive

This guide explains how MDB Framework uses a **version.dll proxy** to inject into Unity IL2CPP games without requiring an external injector. It covers the Windows DLL loading exploit, the forwarding implementation, loader lock safety, the double-load problem, and P/Invoke name resolution.

For the high-level architecture, see the [Architecture Overview]({{ '/guides/architecture' | relative_url }}). For the MonoBehaviour fabrication system, see the [Class Injection Guide]({{ '/guides/class-injection' | relative_url }}).

---

## Table of Contents

1. [Overview](#overview)
2. [Why Proxy DLL Injection?](#why-proxy-dll-injection)
3. [Windows DLL Search Order](#windows-dll-search-order)
4. [Why version.dll?](#why-versiondll)
5. [The Module Definition File](#the-module-definition-file)
6. [Proxy Forwarding Implementation](#proxy-forwarding-implementation)
7. [DllMain and Loader Lock Safety](#dllmain-and-loader-lock-safety)
8. [The Double-Load Problem](#the-double-load-problem)
9. [P/Invoke Bridge Name Resolution](#pinvoke-bridge-name-resolution)
10. [Build Configuration](#build-configuration)
11. [Proxy Mode vs Direct Injection Mode](#proxy-mode-vs-direct-injection-mode)
12. [Engineering Decisions](#engineering-decisions)

---

## Overview

MDB Framework's compiled `MDB_Bridge.dll` is renamed to `version.dll` and placed in the game's root directory. When the game launches, Windows loads our fake `version.dll` instead of the real one from `System32`. Our DLL transparently forwards all 17 version API calls to the real system DLL while simultaneously bootstrapping the entire MDB modding framework in the background.

MDB supports **both modes** — proxy injection (preferred) and direct injection (for development/debugging). The same `MDB_Bridge.dll` binary works in either mode without recompilation.

---

## Why Proxy DLL Injection?

Traditional DLL injection methods require an **external injector** — a separate program that uses Windows APIs like `CreateRemoteThread` + `LoadLibrary` to force a target process to load your DLL. This approach has several drawbacks:

| Problem | Description |
|---------|-------------|
| **User friction** | Users must download, configure, and run a separate injector tool |
| **Timing** | The injector must run after the game starts but before critical initialization — getting this window right is fragile |
| **Anti-cheat detection** | External injection is a well-known pattern that anti-cheat systems actively monitor |
| **Permissions** | Injectors typically require administrator privileges |

Proxy DLL injection eliminates all of these. The user drops a file into the game folder and launches normally. The operating system itself loads our DLL — no external tool, no timing issues, no elevated permissions.

---

## Windows DLL Search Order

The technique exploits how Windows resolves DLL dependencies at process startup.

When an application calls `LoadLibrary("version.dll")` — or has it listed in its import table — Windows searches for the DLL in this order:

1. **The directory containing the application executable** ← We exploit this
2. The system directory (`C:\Windows\System32`)
3. The 16-bit system directory (`C:\Windows\System`)
4. The Windows directory (`C:\Windows`)
5. The current working directory
6. Directories listed in the `PATH` environment variable

Because the **application directory is searched first**, placing a DLL with the same name as a system DLL in the game folder causes Windows to load our proxy instead of the real one.

This is documented, intentional behavior — it's how Windows DLL loading works by design. We use it constructively to bootstrap the modding framework.

### Important Caveat: Known DLLs

Windows maintains a registry key at `HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\KnownDLLs` that lists DLLs which are **always** loaded from `System32`, bypassing the search order entirely. Critical system DLLs like `kernel32.dll`, `ntdll.dll`, and `user32.dll` are on this list and **cannot** be proxied.

`version.dll` is **not** on the KnownDLLs list, which is why this technique works.

---

## Why version.dll?

| Reason | Explanation |
|--------|-------------|
| **Universal loading** | Nearly every Windows application loads `version.dll` — Unity games included. It's used by the PE loader for version resource queries. |
| **Small API surface** | Only 17 exported functions, all with simple, well-documented signatures. |
| **Not in KnownDLLs** | Windows does not force-load it from System32. |
| **Non-critical behavior** | Even if forwarding fails temporarily, the game won't crash — version queries return benign defaults. |
| **No state** | The version API is purely query-based with no persistent state, sessions, or callbacks. |

Other commonly proxied DLLs include `winmm.dll`, `dinput8.dll`, and `d3d9.dll`. We chose `version.dll` for the smallest API surface and the most universal loading guarantee.

---

## The Module Definition File

**File:** `MDB_Bridge/version.def`

The `.def` file is a linker input that controls the DLL's export table. It maps the 17 public export names (matching the real `version.dll`) to internal `_impl` forwarding functions:

```def
EXPORTS
    GetFileVersionInfoA        = GetFileVersionInfoA_impl
    GetFileVersionInfoByHandle = GetFileVersionInfoByHandle_impl
    GetFileVersionInfoExA      = GetFileVersionInfoExA_impl
    GetFileVersionInfoExW      = GetFileVersionInfoExW_impl
    GetFileVersionInfoSizeA    = GetFileVersionInfoSizeA_impl
    GetFileVersionInfoSizeExA  = GetFileVersionInfoSizeExA_impl
    GetFileVersionInfoSizeExW  = GetFileVersionInfoSizeExW_impl
    GetFileVersionInfoSizeW    = GetFileVersionInfoSizeW_impl
    GetFileVersionInfoW        = GetFileVersionInfoW_impl
    VerFindFileA               = VerFindFileA_impl
    VerFindFileW               = VerFindFileW_impl
    VerInstallFileA            = VerInstallFileA_impl
    VerInstallFileW            = VerInstallFileW_impl
    VerLanguageNameA           = VerLanguageNameA_impl
    VerLanguageNameW           = VerLanguageNameW_impl
    VerQueryValueA             = VerQueryValueA_impl
    VerQueryValueW             = VerQueryValueW_impl
```

When the MSVC linker processes this file, it produces a DLL whose PE export directory contains entries matching exactly what the game's import table expects from `version.dll`. Each export points to our `_impl` function instead of the real Windows implementation.

### The 17 Exports

The version API groups into five families:

| Family | Functions | Purpose |
|--------|-----------|---------|
| **GetFileVersionInfo** | `A/W`, `ExA/ExW` | Read version resource data from a file |
| **GetFileVersionInfoSize** | `A/W`, `ExA/ExW` | Query the size of version resource data |
| **GetFileVersionInfoByHandle** | (single) | Read version info by file handle (legacy) |
| **VerFindFile / VerInstallFile** | `A/W` each | File installation helpers (legacy) |
| **VerLanguageName / VerQueryValue** | `A/W` each | Query specific version fields |

The `A`/`W` suffixes indicate ANSI/Wide character variants — standard Windows convention.

---

## Proxy Forwarding Implementation

**Files:** `MDB_Bridge/src/proxy/version_proxy.h`, `MDB_Bridge/src/proxy/version_proxy.cpp`

The forwarding layer has three components:

### 1. Loading the Real version.dll

```cpp
static HMODULE g_hRealVersion = nullptr;

static void EnsureRealVersionLoaded()
{
    if (g_hRealVersion) return;

    wchar_t sysDir[MAX_PATH];
    GetSystemDirectoryW(sysDir, MAX_PATH);           // → "C:\Windows\System32"
    wcscat_s(sysDir, L"\\version.dll");               // → "C:\Windows\System32\version.dll"
    g_hRealVersion = LoadLibraryW(sysDir);            // Load the REAL version.dll
}
```

We use `GetSystemDirectoryW` to build the **full path** to the real `version.dll` in System32. This bypasses the DLL search order entirely — we always load the genuine system DLL, never ourselves recursively.

### 2. The RESOLVE() Macro

```cpp
#define RESOLVE(name) \
    do { \
        if (!p_##name) { \
            EnsureRealVersionLoaded(); \
            if (g_hRealVersion) \
                p_##name = reinterpret_cast<fn_##name>( \
                    GetProcAddress(g_hRealVersion, #name)); \
        } \
    } while(0)
```

Each forwarding function uses this macro on its first call to:
1. Ensure the real DLL is loaded
2. Resolve the real function pointer via `GetProcAddress`
3. Cache it in a file-scope static variable

Subsequent calls skip resolution entirely and call the cached pointer — essentially zero overhead after the first invocation.

### 3. Forwarding Functions

Each of the 17 `_impl` functions follows the same pattern:

```cpp
extern "C" BOOL WINAPI GetFileVersionInfoA_impl(LPCSTR f, DWORD h, DWORD len, LPVOID d) {
    RESOLVE(GetFileVersionInfoA);
    return p_GetFileVersionInfoA ? p_GetFileVersionInfoA(f, h, len, d) : FALSE;
}
```

- Resolve the real function pointer (no-op after first call)
- If resolved successfully, forward the call with identical arguments
- If the real DLL failed to load, return a safe default (`FALSE` for BOOL, `0` for DWORD)

### Error Handling

The forwarding layer is intentionally **fault-tolerant**. If the real `version.dll` cannot be loaded (extremely unusual), the proxy functions return benign defaults rather than crashing. Most games don't check version query return values during startup.

### Cleanup

```cpp
void VersionProxy_Cleanup()
{
    if (g_hRealVersion) {
        FreeLibrary(g_hRealVersion);
        g_hRealVersion = nullptr;
    }
}
```

Called during `DLL_PROCESS_DETACH` to release the real DLL handle.

---

## DllMain and Loader Lock Safety

**File:** `MDB_Bridge/src/core/dllmain.cpp`

`DllMain` runs **under the loader lock** — a critical section that serializes all DLL loading in the process. This is one of the most constrained execution environments in Windows.

### What You Cannot Do Under the Loader Lock

- Call `LoadLibrary` or `LoadLibraryEx` (deadlock risk)
- Call `GetProcAddress` on another DLL
- Create threads that synchronize with the loader
- Acquire synchronization objects held by threads waiting on the loader lock
- Call into managed code (CLR, COM)

Violating these rules causes **deadlocks**, **crashes**, or **undefined behavior**.

### Our DllMain Implementation

```cpp
BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved) {
    switch (ul_reason_for_call) {
        case DLL_PROCESS_ATTACH:
            DisableThreadLibraryCalls(hModule);

            g_init_event = CreateEventW(nullptr, TRUE, FALSE,
                                        L"Local\\MDB_Bridge_InitGuard");
            if (GetLastError() == ERROR_ALREADY_EXISTS) {
                if (g_init_event) { CloseHandle(g_init_event); g_init_event = nullptr; }
                break;
            }

            {
                HANDLE hThread = CreateThread(nullptr, 0,
                                             initialization_thread, nullptr, 0, nullptr);
                if (hThread) CloseHandle(hThread);
            }
            break;

        case DLL_PROCESS_DETACH:
            // ... cleanup
            break;
    }
    return TRUE;
}
```

**Key design decisions:**

1. **`DisableThreadLibraryCalls(hModule)`** — stops Windows from notifying us about thread creation/destruction. Performance optimization — the game creates hundreds of threads.

2. **No `LoadLibrary` calls** — the real `version.dll` is NOT loaded here. Proxy functions handle it lazily on first call, after the loader lock is released.

3. **No heavy initialization** — creating a named event and spawning a thread are safe under the loader lock. Everything else happens in the background thread.

4. **Named event init guard** — prevents double-initialization (see next section).

### DLL_PROCESS_DETACH Cleanup

The detach handler distinguishes between two exit scenarios:

```cpp
case DLL_PROCESS_DETACH:
    mdb_log_detail::console_suppressed() = true;

    if (lpReserved == nullptr) {
        // Dynamic FreeLibrary — safe to do full cleanup
        shutdown_clr();              // Stop CLR, release COM interfaces
        il2cpp::cleanup();           // Release IL2CPP state
        VersionProxy_Cleanup();      // FreeLibrary on real version.dll
    } else {
        // Process termination — minimal cleanup only
        mdb_imgui_shutdown();        // Release DirectX resources (idempotent)
        if (g_pRuntimeHost) {
            g_pRuntimeHost->Stop();  // Stop CLR but don't Release (unsafe)
        }
    }
    if (g_init_event) { CloseHandle(g_init_event); g_init_event = nullptr; }
    break;
```

- **`lpReserved == nullptr`**: explicit `FreeLibrary` unload — full cleanup is safe
- **`lpReserved != nullptr`**: process termination — many DLLs may already be unloaded, only minimal self-contained cleanup is safe

---

## The Double-Load Problem

One of the most subtle engineering challenges in the proxy injection system.

### The Problem

In proxy mode, the same DLL binary gets loaded **twice**:

1. **First load:** Windows loads `version.dll` (our renamed `MDB_Bridge.dll`) from the game directory when resolving the game's import table.
2. **Second load:** Later, when C# code calls `[DllImport("MDB_Bridge.dll")]`, the P/Invoke runtime calls `LoadLibrary("MDB_Bridge.dll")` — which loads a separate copy from the `MDB/` subdirectory.

Each load creates a **distinct module image** in memory with its own copy of all static variables. Without protection, both instances would try to initialize — spawning two background threads, hosting two CLR instances, and creating chaos.

### The Solution: Named Events

```cpp
static HANDLE g_init_event = nullptr;

// In DLL_PROCESS_ATTACH:
g_init_event = CreateEventW(nullptr, TRUE, FALSE, L"Local\\MDB_Bridge_InitGuard");
if (GetLastError() == ERROR_ALREADY_EXISTS) {
    if (g_init_event) { CloseHandle(g_init_event); g_init_event = nullptr; }
    break;
}
```

We use a **process-local named event** as a cross-module initialization guard:

- The first DLL instance creates the event and proceeds with initialization.
- The second instance receives a valid handle but `GetLastError()` returns `ERROR_ALREADY_EXISTS`. It closes the handle and skips all init.

Named events work across module boundaries because they're **kernel objects** identified by name within the process, not static variables scoped to a module image. This is why `static bool g_initialized = false` wouldn't work — each module gets its own copy.

---

<a name="pinvoke-bridge-name-resolution"></a>
## P/Invoke Bridge Name Resolution

**The Problem:** In proxy mode, our DLL is loaded as `version.dll`, but C# code uses `[DllImport("MDB_Bridge.dll")]`. The CLR can't find a module with that name.

**The Solution:** `ensure_bridge_searchable()` in `dllmain.cpp`:

```cpp
static void ensure_bridge_searchable() {
    // In direct injection mode, MDB_Bridge.dll is already loaded — nothing to do
    if (GetModuleHandleW(L"MDB_Bridge.dll")) return;

    // Find our own DLL path
    HMODULE hSelf = nullptr;
    GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS |
                       GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                       reinterpret_cast<LPCWSTR>(&ensure_bridge_searchable),
                       &hSelf);
    wchar_t selfPath[MAX_PATH];
    GetModuleFileNameW(hSelf, selfPath, MAX_PATH);

    // Copy ourselves to MDB/MDB_Bridge.dll
    std::filesystem::path bridgePath = mdbDir / L"MDB_Bridge.dll";
    if (!std::filesystem::exists(bridgePath)) {
        std::filesystem::copy_file(selfPath, bridgePath, ec);
    }

    // Pre-load by full path → module name "MDB_Bridge.dll" now exists in process
    LoadLibraryW(bridgePath.wstring().c_str());
}
```

The strategy:
1. In **direct injection mode**, `MDB_Bridge.dll` is already loaded under its own name — nothing to do.
2. In **proxy mode**, copy ourselves to `MDB/MDB_Bridge.dll` and pre-load by full path. Windows now has a module with base name `MDB_Bridge.dll` loaded, so P/Invoke resolves it.

This second load triggers `DllMain` again, but the **named event guard** ensures the second instance skips initialization entirely.

**Why not rename the DllImport?** Using `[DllImport("version.dll")]` was rejected because the same C# code must work in both proxy and direct injection modes without recompilation.

---

## Build Configuration

**File:** `MDB_Bridge/MDB_Bridge.vcxproj`

### Key Linker Setting

```xml
<ModuleDefinitionFile>version.def</ModuleDefinitionFile>
```

This single setting transforms `MDB_Bridge.dll` into a version.dll proxy. The linker reads `version.def`, processes the `EXPORTS` section, and generates the corresponding export directory in the output PE.

### Project Configuration

| Setting | Value |
|---------|-------|
| Output type | Dynamic Library (`.dll`) |
| Toolset | v143 (Visual Studio 2022) |
| C++ Standard | C++17 |
| Platform | x64 only |
| Output name | `MDB_Bridge.dll` |
| Module definition | `version.def` |

### Dependencies

| Library | Purpose |
|---------|---------|
| `mscoree.lib` | .NET CLR hosting APIs |
| `d3d11.lib` / `d3d12.lib` | DirectX (ImGui backend) |
| `dxgi.lib` | DXGI swap chain hooking |
| `d3dcompiler.lib` | HLSL shader compilation |

### Third-Party Code

| Library | Path | Purpose |
|---------|------|---------|
| MinHook | `thirdparty/minhook/` | API hooking (Application.Quit, DX vtables) |
| Dear ImGui | `thirdparty/imgui/` | In-game overlay UI (DX11/DX12 backends) |

---

## Proxy Mode vs Direct Injection Mode

| Aspect | Proxy Mode (`version.dll`) | Direct Injection (`MDB_Bridge.dll`) |
|--------|---------------------------|-------------------------------------|
| **Setup** | Rename DLL, copy to game folder | Copy to game folder, use external injector |
| **User experience** | Launch game normally | Launch game, then inject separately |
| **External tools** | None required | DLL injector required |
| **Timing** | Automatic — loaded at process start | Manual — user controls injection timing |
| **Module name** | `version.dll` | `MDB_Bridge.dll` |
| **P/Invoke resolution** | Requires `ensure_bridge_searchable()` workaround | Works automatically |
| **Double-load** | Yes — requires named event guard | No — single load |
| **Anti-cheat risk** | Lower (OS-level DLL loading) | Higher (uses `CreateRemoteThread` or similar) |
| **Best for** | End users, distribution | Development, debugging |

---

## Engineering Decisions

### 1. Loader Lock Avoidance

**Challenge:** `DllMain` runs under the loader lock, where calling `LoadLibrary`, `GetProcAddress` (on other DLLs), or CLR APIs will deadlock.

**Solution:** All heavy work is deferred to a background thread via `CreateThread`. Proxy functions lazy-load the real `version.dll` on first call, after `DllMain` returns.

### 2. Named Events over Named Mutexes

**Challenge:** Need a cross-module initialization guard that works under the loader lock.

**Solution:** `CreateEventW` is safe under the loader lock and doesn't require ownership management. The `ERROR_ALREADY_EXISTS` pattern is simpler and more robust than mutex acquisition.

### 3. Lazy vs Eager Loading

**Challenge:** The real `version.dll` must be available before the first version API call, but we can't call `LoadLibrary` during `DllMain`.

**Solution:** Each `_impl` function calls `RESOLVE()` which lazy-loads on first invocation. The check is a single null-pointer comparison per call after first resolution — effectively free.

### 4. P/Invoke Compatibility

**Challenge:** C# code expects `[DllImport("MDB_Bridge.dll")]`, but in proxy mode the loaded module is `version.dll`.

**Solution:** Copy self to `MDB/MDB_Bridge.dll` and pre-load. Renaming the DllImport was rejected because the same C# code must work in both modes without recompilation.

### 5. Obfuscated IL2CPP Exports

**Challenge:** Some games rename IL2CPP exports (e.g., `il2cpp_domain_get` → `il2cpp_domain_get_wasting_your_life`).

**Solution:** 3-tier resolution: canonical names → cached mappings → suffix-based PE scanning. Core functions are required; introspection functions degrade gracefully.

### 6. Runtime Compilation Trade-off

**Challenge:** Every game has unique types. The SDK must be generated and compiled per-game.

**Solution:** The native bridge runs MSBuild as a child process, located via `vswhere.exe`. This requires Visual Studio to be installed — acceptable because the target audience is mod developers.

---

[← Architecture Overview]({{ '/guides/architecture' | relative_url }}) | [Class Injection →]({{ '/guides/class-injection' | relative_url }})
