# MDB - IL2CPP Modding Framework

> **⚠️ EXTREMELY EARLY DEVELOPMENT WARNING ⚠️**
> 
> This framework is in very early stages of development. It has only been tested on a handful of games I'm actively working with. Expect bugs, breaking changes, and incomplete features. **If it works for your game, consider yourself lucky. If it doesn't, well... that's expected at this point.**

---

<a href="https://buymeacoffee.com/winnforge">
  <img src="https://img.shields.io/badge/Buy%20Me%20A%20Coffee-%23FFDD00?style=for-the-badge&logo=buymeacoffee&logoColor=black" alt="Buy Me A Coffee">
</a>

## Why Does This Exist?

**One word: Encrypted metadata.**

If you've ever tried to mod an IL2CPP Unity game, you've probably used tools like Il2CppDumper or Cpp2IL to extract type information from `global-metadata.dat`. These tools work great... until they don't.

Many modern games now **encrypt their metadata**. The encryption schemes vary wildly:
- Some XOR with rolling keys
- Some use custom compression + encryption
- Some split metadata across multiple files
- Some use virtualized protection

Figuring out each game's encryption is a nightmare. You need to reverse engineer the game's binary, find the decryption routines, understand the custom metadata format, and pray the developers didn't add anti-tamper on top.

**MDB Framework takes a different approach: dump everything at runtime.**

Instead of trying to decrypt static files, we inject into the running game *after* IL2CPP has already decrypted and loaded everything. At that point, all type information is sitting in memory, ready to be enumerated through IL2CPP's own reflection APIs. No decryption needed.

The dumped metadata gets parsed by a Python script into clean C# wrapper classes, giving you a strongly-typed SDK for the game.

**TL;DR:** Encrypted metadata? Don't care. We bypass it entirely.

---

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [How IL2CPP Works](#how-il2cpp-works)
- [Components](#components)
  - [MDB_Dumper](#mdb_dumper)
  - [MDB_Parser](#mdb_parser)
  - [MDB_Bridge](#mdb_bridge)
  - [MDB_Core](#mdb_core)
- [Deep Dive: The Runtime System](#deep-dive-the-runtime-system)
- [Type Marshaling](#type-marshaling)
- [Creating a Mod](#creating-a-mod)
- [Method Hooking (Harmony-Style)](#method-hooking-harmony-style)
- [ImGui Integration](#imgui-integration)
- [API Reference](#api-reference)
- [Building](#building)
- [Troubleshooting](#troubleshooting)
- [Unicode/Obfuscation Deobfuscation](#unicodeobfuscation-deobfuscation)

---

## Overview

MDB Framework enables modding of Unity games that have been compiled with IL2CPP (Intermediate Language to C++). Unlike traditional .NET Unity games where you can simply inject C# code, IL2CPP games compile all C# code to native machine code, making modding significantly more challenging.

### The Problem Everyone Faces

When Unity compiles a game with IL2CPP:
1. All C# code is converted to C++ source code
2. The C++ code is compiled to native machine code
3. Type metadata is preserved in `global-metadata.dat` for reflection
4. The original .NET assemblies no longer exist at runtime

Traditional modding tools try to parse `global-metadata.dat` statically. This works until developers encrypt it—and increasingly, they do.

### Our Solution: Runtime Dumping

MDB Framework doesn't care about encryption:

1. **Inject at runtime** → Game has already decrypted everything
2. **Dump metadata** → Use IL2CPP's own APIs to enumerate all types
3. **Parse with Python** → Transform the dump into C# wrapper classes  
4. **Bridge two worlds** → Host a .NET CLR alongside IL2CPP
5. **Call anything** → Wrappers marshal data between managed and native

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────────────┐
│                              Game Process                               │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  ┌──────────────────────┐         ┌──────────────────────────────────┐  │
│  │   IL2CPP Runtime     │         │      .NET CLR (v4.0)             │  │
│  │                      │         │                                  │  │
│  │  ┌────────────────┐  │         │  ┌─────────────────────────────┐ │  │
│  │  │ GameAssembly   │  │  P/Invoke  │ GameSDK.ModHost.dll         │ │  │
│  │  │ .dll (native)  │◄─┼─────────┼──┼─►                           │ │  │
│  │  └────────────────┘  │         │  │  ┌───────────────────────┐  │ │  │
│  │         ▲            │         │  │  │ Generated Wrappers    │  │ │  │
│  │  ┌──────┴─────────┐  │         │  │  │ (GameObject, etc.)    │  │ │  │
│  │  │   MinHook      │  │         │  │  └───────────────────────┘  │ │  │
│  │  │  (Hooking)     │  │         │  │  ┌───────────────────────┐  │ │  │
│  │  └────────────────┘  │         │  │  │ PatchProcessor        │  │ │  │
│  │                      │         │  │  │ (Harmony-style)       │  │ │  │
│  │  ┌────────────────┐  │         │  │  └───────────────────────┘  │ │  │
│  │  │ global-        │  │         │  │  ┌───────────────────────┐  │ │  │
│  │  │ metadata.dat   │  │         │  │  │ Your Mod DLLs         │  │ │  │
│  │  └────────────────┘  │         │  │  │ (ExampleMod.dll)      │  │ │  │
│  │                      │         │  │  └───────────────────────┘  │ │  │
│  └──────────────────────┘         │  └─────────────────────────────┘ │  │
│                                   │                                  │  │
│  ┌──────────────────────┐         │                                  │  │
│  │   MDB_Bridge.dll     │◄────────┤  Il2CppBridge P/Invoke calls     │  │
│  │   (Native C++)       │─────────┤                                  │  │
│  │                      │         └──────────────────────────────────┘  │
│  │  - CLR Host          │                                               │
│  │  - IL2CPP Interop    │         ┌──────────────────────────────────┐  │
│  │  - MinHook Manager   │         │        DirectX Overlay           │  │
│  │  - ImGui Integration │────────►│  ┌────────────────────────────┐  │  │
│  │                      │         │  │  Dear ImGui Rendering      │  │  │
│  └──────────────────────┘         │  │  (DX11/DX12 auto-detect)   │  │  │
│                                   │  └────────────────────────────┘  │  │
│                                   └──────────────────────────────────┘  │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### Data Flow

1. **Game starts** → IL2CPP runtime initializes
2. **MDB_Bridge.dll injected** → Hooks into game process  
3. **CLR initialized** → .NET Framework 4.0 runtime started inside game
4. **ModHost loaded** → `GameSDK.ModHost.dll` loaded into CLR
5. **Mods discovered** → `ModManager` scans `MDB/Mods/` folder
6. **Patches applied** → `PatchProcessor` discovers and installs method hooks
7. **ImGui initialized** → DirectX Present hook installed for overlay rendering
8. **Mods initialized** → Each mod's `OnLoad()` called
9. **Game loop** → `OnUpdate()`, `OnFixedUpdate()`, `OnLateUpdate()`, `OnGUI()` called

---

## How IL2CPP Works

Understanding IL2CPP is crucial for understanding why MDB Framework exists.

### Traditional .NET Unity Games

```
C# Source Code → C# Compiler → IL Bytecode (.dll) → Mono Runtime → Execution
```

In traditional Unity games, the Mono runtime interprets IL bytecode at runtime. You can inject new .NET assemblies and they "just work" because Mono can load and execute them.

### IL2CPP Unity Games

```
C# Source Code → C# Compiler → IL Bytecode → IL2CPP → C++ Source → Native Compiler → Machine Code
```

IL2CPP converts all IL bytecode to C++ source code, which is then compiled to native machine code. This means:

1. **No IL interpreter** - There's no runtime that can execute new IL bytecode
2. **Metadata preserved** - Type information is stored in `global-metadata.dat`
3. **Runtime reflection** - IL2CPP provides APIs to query type information
4. **Native pointers** - All objects are native pointers, not managed references

### IL2CPP Memory Layout

Every IL2CPP object in memory has this structure:

```
┌─────────────────────────────────────────┐
│ Il2CppObject (16 bytes on x64)          │
├─────────────────────────────────────────┤
│ Il2CppClass* klass     (8 bytes)        │ ← Pointer to class metadata
│ MonitorData* monitor   (8 bytes)        │ ← Thread synchronization
├─────────────────────────────────────────┤
│ Object Fields...                        │ ← Instance fields start here
│ field1                                  │
│ field2                                  │
│ ...                                     │
└─────────────────────────────────────────┘
```

### IL2CPP Arrays

Arrays have a more complex structure:

```
┌─────────────────────────────────────────┐
│ Il2CppArray Header (32 bytes on x64)    │
├─────────────────────────────────────────┤
│ Il2CppClass* klass     (8 bytes)        │
│ MonitorData* monitor   (8 bytes)        │
│ Il2CppArrayBounds* bounds (8 bytes)     │ ← Bounds info (multi-dim arrays)
│ il2cpp_array_size_t max_length (8 bytes)│ ← Array length
├─────────────────────────────────────────┤
│ Element[0]                              │ ← Elements start at offset 32
│ Element[1]                              │
│ Element[2]                              │
│ ...                                     │
└─────────────────────────────────────────┘
```

---

## Components

### MDB_Dumper

A native C++ DLL that extracts IL2CPP metadata at runtime using the IL2CPP scripting API.

**Purpose:** Generate `dump.cs` containing all type definitions from the game.

**How it works:**

1. Loads into the game process
2. Waits for IL2CPP to initialize
3. Uses `il2cpp_domain_get_assemblies()` to enumerate all assemblies
4. For each assembly, uses `il2cpp_image_get_class_count()` and `il2cpp_image_get_class()` to enumerate types
5. For each type, extracts:
   - Class name, namespace, base type
   - Fields with types and offsets
   - Methods with signatures and RVAs
   - Properties with getters/setters
6. Outputs everything to `dump.cs`

**Key IL2CPP APIs used:**
```cpp
il2cpp_domain_get_assemblies()      // Get all loaded assemblies
il2cpp_assembly_get_image()         // Get image from assembly
il2cpp_image_get_class_count()      // Count classes in image
il2cpp_image_get_class()            // Get class by index
il2cpp_class_get_name()             // Get class name
il2cpp_class_get_namespace()        // Get namespace
il2cpp_class_get_methods()          // Iterate methods
il2cpp_class_get_fields()           // Iterate fields
il2cpp_method_get_name()            // Get method name
il2cpp_method_get_return_type()     // Get return type
```

**Output format (`dump.cs`):**
```csharp
// Dll : UnityEngine.CoreModule.dll
// Namespace: UnityEngine
public class GameObject : Object
{
    // Fields
    
    // Properties
    public Transform transform { get; }
    public string name { get; set; }
    public bool activeSelf { get; }
    
    // Methods
    // RVA: 0x1234567 Offset: 0x1234567
    public static GameObject Find(string name) { }
    // RVA: 0x1234568 Offset: 0x1234568
    public T GetComponent<T>() { }
}
```

---

### MDB_Parser

Python script (`wrapper_generator.py`) that transforms `dump.cs` into usable C# wrapper classes.

**Purpose:** Generate strongly-typed C# code that can call IL2CPP methods at runtime.

**Processing Pipeline:**

```
dump.cs → Parse Types → Filter Invalid → Resolve Dependencies → Generate Code → GameSDK.*.cs
```

**Key transformations:**

1. **Base class resolution:**
   ```python
   # Input: class without visible base
   public class Object { }
   
   # Output: inherit from Il2CppObject for pointer storage
   public class Object : Il2CppObject { }
   ```

2. **Constructor generation:**
   ```python
   # Every wrapper needs a constructor to store the native pointer
   public GameObject(IntPtr nativePtr) : base(nativePtr) { }
   ```

3. **Method wrapping:**
   ```python
   # Input (from dump):
   public string get_name() { }
   
   # Output (wrapper):
   public string get_name()
   {
       return Il2CppRuntime.Call<string>(this, "get_name", Type.EmptyTypes);
   }
   ```

4. **Static method wrapping:**
   ```python
   # Input:
   public static GameObject Find(string name) { }
   
   # Output:
   public static GameObject Find(string name)
   {
       return Il2CppRuntime.CallStatic<GameObject>(
           "UnityEngine", "GameObject", "Find",
           new Type[] { typeof(string) }, name);
   }
   ```

5. **Type mapping:**
   ```python
   TYPE_MAP = {
       "Void": "void",
       "Boolean": "bool",
       "Int32": "int",
       "Single": "float",
       "String": "string",
       # etc.
   }
   ```

6. **Generic handling:**
   ```python
   # IL2CPP erases generics, so List`1 becomes List<object>
   "List`1" → "List<object>"
   "Dictionary`2" → "Dictionary<object, object>"
   ```

**Output structure:**
```
<GameFolder>/MDB/Dump/MDB_Core/Generated/
├── GameSDK.UnityEngine.cs           # UnityEngine namespace
├── GameSDK.UnityEngine_UI.cs        # UnityEngine.UI namespace
├── GameSDK.Global.cs                # Global namespace (no namespace)
├── GameSDK.TMPro.cs                 # TextMeshPro
├── GameSDK.Photon_Pun.cs            # Game-specific (Photon networking)
├── GameSDK.Steamworks.cs            # Game-specific (Steam integration)
└── ... (varies by game, 100-300+ files typical)
```

---

### MDB_Bridge

Native C++ DLL that acts as the bridge between IL2CPP and the .NET CLR.

**Purpose:** 
1. Host a .NET CLR inside the game process
2. Provide P/Invoke exports for IL2CPP operations
3. Load and initialize the mod system
4. Manage native method hooks via MinHook
5. Provide Dear ImGui overlay rendering

**Key Files:**

- `dllmain.cpp` - DLL entry point, CLR initialization
- `bridge_exports.cpp` - P/Invoke function implementations
- `bridge_exports.h` - Function declarations
- `il2cpp_resolver.hpp` - IL2CPP API function resolution
- `imgui_integration.cpp` - Dear ImGui DirectX hooks
- `imgui_integration.h` - ImGui P/Invoke exports
- `minhook/` - MinHook library for native hooking

**CLR Hosting:**

The bridge hosts the .NET Framework 4.0 CLR using the CLR Hosting APIs:

```cpp
// 1. Load CLR runtime
ICLRMetaHost* metaHost;
CLRCreateInstance(CLSID_CLRMetaHost, IID_ICLRMetaHost, &metaHost);

// 2. Get v4.0 runtime
ICLRRuntimeInfo* runtimeInfo;
metaHost->GetRuntime(L"v4.0.30319", IID_ICLRRuntimeInfo, &runtimeInfo);

// 3. Get runtime host
ICLRRuntimeHost* runtimeHost;
runtimeInfo->GetInterface(CLSID_CLRRuntimeHost, IID_ICLRRuntimeHost, &runtimeHost);

// 4. Start CLR
runtimeHost->Start();

// 5. Execute managed code
runtimeHost->ExecuteInDefaultAppDomain(
    L"GameSDK.ModHost.dll",
    L"GameSDK.ModHost.ModManager",
    L"Initialize",
    L"argument",
    &returnValue
);
```

**P/Invoke Exports:**

These functions are called from C# via `[DllImport("MDB_Bridge.dll")]`:

```cpp
// Find an IL2CPP class by namespace and name
extern "C" __declspec(dllexport) 
void* mdb_find_class(const char* assemblyName, const char* ns, const char* name)
{
    // Try to find in specific assembly first, then search all
    auto domain = il2cpp_domain_get();
    size_t count;
    auto assemblies = il2cpp_domain_get_assemblies(domain, &count);
    
    for (size_t i = 0; i < count; i++) {
        auto image = il2cpp_assembly_get_image(assemblies[i]);
        auto klass = il2cpp_class_from_name(image, ns, name);
        if (klass) return klass;
    }
    return nullptr;
}

// Get a method from a class
extern "C" __declspec(dllexport)
void* mdb_get_method(void* klass, const char* name, int paramCount)
{
    return il2cpp_class_get_method_from_name(
        (Il2CppClass*)klass, name, paramCount);
}

// Invoke a method using il2cpp_runtime_invoke
extern "C" __declspec(dllexport)
void* mdb_invoke_method(void* method, void* instance, void** args)
{
    Il2CppException* exception = nullptr;
    auto result = il2cpp_runtime_invoke(
        (MethodInfo*)method, instance, args, &exception);
    
    if (exception) {
        // Handle exception
    }
    return result;
}

// Create a new IL2CPP object
extern "C" __declspec(dllexport)
void* mdb_new_object(void* klass)
{
    return il2cpp_object_new((Il2CppClass*)klass);
}

// Create an IL2CPP string from C string
extern "C" __declspec(dllexport)
void* mdb_new_string(const char* str)
{
    return il2cpp_string_new(str);
}

// Get System.Type object from IL2CPP class
extern "C" __declspec(dllexport)
void* mdb_class_get_type(void* klass)
{
    return il2cpp_class_get_type((Il2CppClass*)klass);
}

// Get reflection Type object
extern "C" __declspec(dllexport)
void* mdb_type_get_object(void* type)
{
    return il2cpp_type_get_object((Il2CppType*)type);
}
```

**IL2CPP Function Resolution:**

IL2CPP functions are loaded dynamically from `GameAssembly.dll`:

```cpp
// il2cpp_resolver.hpp
namespace il2cpp {
    inline HMODULE game_assembly = nullptr;
    
    template<typename T>
    T resolve(const char* name) {
        if (!game_assembly) {
            game_assembly = GetModuleHandleA("GameAssembly.dll");
        }
        return (T)GetProcAddress(game_assembly, name);
    }
}

// Usage:
auto il2cpp_domain_get = il2cpp::resolve<Il2CppDomain*(*)()>("il2cpp_domain_get");
auto il2cpp_runtime_invoke = il2cpp::resolve<Il2CppObject*(*)(
    const MethodInfo*, void*, void**, Il2CppException**)>("il2cpp_runtime_invoke");
```

**Hook Exports (MinHook):**

Native method hooking is handled through MinHook:

```cpp
// Create a hook on a method pointer
extern "C" __declspec(dllexport)
long mdb_create_hook(void* target, void* detour, void** original);

// Create a hook by RVA offset
extern "C" __declspec(dllexport)
long mdb_create_hook_rva(uint64_t rva, void* detour, void** original);

// Create a hook on a raw pointer
extern "C" __declspec(dllexport)
long mdb_create_hook_ptr(void* target, void* detour, void** original);

// Enable/disable a hook
extern "C" __declspec(dllexport)
int mdb_set_hook_enabled(long handle, bool enabled);

// Remove a hook
extern "C" __declspec(dllexport)
int mdb_remove_hook(long handle);
```

**ImGui Exports:**

DirectX overlay rendering via Dear ImGui:

```cpp
// Get detected DirectX version (11 or 12)
extern "C" __declspec(dllexport)
MdbDxVersion mdb_imgui_get_dx_version();

// Initialize ImGui with auto-detected DirectX
extern "C" __declspec(dllexport)
bool mdb_imgui_init();

// Shutdown ImGui
extern "C" __declspec(dllexport)
void mdb_imgui_shutdown();

// Register a draw callback (called each frame)
extern "C" __declspec(dllexport)
void mdb_imgui_register_draw_callback(MdbImGuiDrawCallback callback);

// Toggle input capture
extern "C" __declspec(dllexport)
void mdb_imgui_set_input_enabled(bool enabled);

// Set toggle key (default: F2)
extern "C" __declspec(dllexport)
void mdb_imgui_set_toggle_key(int vkCode);
```

---

### MDB_Core

The C# SDK containing everything needed for mod development.

**Project Structure:**

```
MDB_Core/
├── Core/
│   ├── Il2CppBase.cs          # Base classes for IL2CPP objects
│   ├── Il2CppBridge.cs        # P/Invoke declarations
│   ├── Il2CppMarshaler.cs     # Type conversion/marshaling
│   ├── UnityEngineBase.cs     # Unity-specific base classes
│   └── UnityValueTypes.cs     # Vector3, Quaternion, Color, etc.
├── Generated/
│   └── GameSDK.*.cs           # Generated wrapper files
├── ModHost/
│   ├── ModAttribute.cs        # [Mod] attribute
│   ├── ModBase.cs             # Base class for mods
│   ├── ModLogger.cs           # Logging system
│   ├── ModManager.cs          # Mod discovery and lifecycle
│   └── Patching/              # Hooking system
│       ├── HookManager.cs     # Low-level hook API
│       ├── PatchAttribute.cs  # [Patch], [Prefix], [Postfix] attributes
│       └── PatchProcessor.cs  # Auto-discovery and application
├── MDB_Core.csproj            # .NET 8.0 (development)
└── GameSDK.ModHost.csproj     # .NET Framework 4.7.2 (runtime)
```

#### Il2CppBase.cs

Defines the base class for all IL2CPP object wrappers:

```csharp
namespace GameSDK
{
    /// <summary>
    /// Base class for all IL2CPP objects. Stores the native pointer.
    /// </summary>
    public class Il2CppObject
    {
        /// <summary>
        /// Pointer to the native IL2CPP object in memory.
        /// </summary>
        public IntPtr NativePtr { get; protected set; }

        public Il2CppObject() { }

        public Il2CppObject(IntPtr nativePtr)
        {
            NativePtr = nativePtr;
        }

        /// <summary>
        /// Returns true if this object has a valid native pointer.
        /// </summary>
        public bool IsValid => NativePtr != IntPtr.Zero;

        /// <summary>
        /// Implicit conversion to IntPtr for P/Invoke calls.
        /// </summary>
        public static implicit operator IntPtr(Il2CppObject obj) 
            => obj?.NativePtr ?? IntPtr.Zero;
    }
}
```

#### Il2CppBridge.cs

P/Invoke declarations for the native bridge:

```csharp
namespace GameSDK
{
    internal static class Il2CppBridge
    {
        private const string DllName = "MDB_Bridge.dll";

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr mdb_find_class(
            string assemblyName, string ns, string name);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr mdb_get_method(
            IntPtr klass, string name, int paramCount);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr mdb_invoke_method(
            IntPtr method, IntPtr instance, IntPtr[] args);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr mdb_new_object(IntPtr klass);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr mdb_new_string(string str);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr mdb_class_get_type(IntPtr klass);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr mdb_type_get_object(IntPtr type);
        
        // ... more exports
    }
}
```

#### Il2CppRuntime (in Il2CppBase.cs)

High-level API for calling IL2CPP methods:

```csharp
public static class Il2CppRuntime
{
    // Cache for class lookups (expensive operation)
    private static Dictionary<string, IntPtr> _classCache = new();

    /// <summary>
    /// Call an instance method and return the result.
    /// </summary>
    public static T Call<T>(Il2CppObject instance, string methodName, 
        Type[] paramTypes, params object[] args)
    {
        // 1. Validate instance
        if (instance == null || !instance.IsValid)
            throw new InvalidOperationException("Instance is null or invalid");

        // 2. Get class from instance
        IntPtr klass = GetClassFromInstance(instance.NativePtr);

        // 3. Find the method
        IntPtr method = Il2CppBridge.mdb_get_method(klass, methodName, args.Length);
        if (method == IntPtr.Zero)
            throw new MissingMethodException($"Method {methodName} not found");

        // 4. Marshal arguments
        IntPtr[] marshaledArgs = MarshalArguments(args, paramTypes);

        // 5. Invoke
        IntPtr result = Il2CppBridge.mdb_invoke_method(
            method, instance.NativePtr, marshaledArgs);

        // 6. Marshal return value
        return Il2CppMarshaler.MarshalReturn<T>(result);
    }

    /// <summary>
    /// Call a static method and return the result.
    /// </summary>
    public static T CallStatic<T>(string ns, string className, string methodName,
        Type[] paramTypes, params object[] args)
    {
        // 1. Find the class
        IntPtr klass = GetOrCacheClass(ns, className);

        // 2. Find the method
        IntPtr method = Il2CppBridge.mdb_get_method(klass, methodName, args.Length);

        // 3. Marshal arguments
        IntPtr[] marshaledArgs = MarshalArguments(args, paramTypes);

        // 4. Invoke (null instance for static)
        IntPtr result = Il2CppBridge.mdb_invoke_method(method, IntPtr.Zero, marshaledArgs);

        // 5. Marshal return value
        return Il2CppMarshaler.MarshalReturn<T>(result);
    }
}
```

---

## Deep Dive: The Runtime System

### Method Invocation Flow

When you access a wrapper property like `player.transform`:

```
┌─────────────────────────────────────────────────────────────────────┐
│ Your Mod Code                                                       │
│                                                                     │
│ var player = GameObject.Find("Player");                             │
│ Transform t = player.transform;   // ← This triggers the flow       │
└───────────────────────────────┬─────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────────┐
│ Generated Wrapper Property (GameSDK.UnityEngine.cs)                 │
│                                                                     │
│ public Transform transform                                          │
│ {                                                                   │
│     get => Il2CppRuntime.Call<Transform>(this, "get_transform",     │
│                                          Type.EmptyTypes);          │
│ }                                                                   │
└───────────────────────────────┬─────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────────┐
│ Il2CppRuntime.Call<T>                                               │
│ 1. Validate instance.NativePtr != IntPtr.Zero                       │
│ 2. Get Il2CppClass* from instance header                            │
│ 3. Call mdb_get_method(klass, "get_transform", 0)                   │
│ 4. Marshal arguments (none for getter)                              │
│ 5. Call mdb_invoke_method(method, instance, args)                   │
└───────────────────────────────┬─────────────────────────────────────┘
                                │ P/Invoke
                                ▼
┌─────────────────────────────────────────────────────────────────────┐
│ MDB_Bridge.dll (Native C++)                                         │
│                                                                     │
│ mdb_get_method:                                                     │
│   return il2cpp_class_get_method_from_name(klass, name, paramCount) │
│                                                                     │
│ mdb_invoke_method:                                                  │
│   return il2cpp_runtime_invoke(method, instance, args, &exception)  │
└───────────────────────────────┬─────────────────────────────────────┘
                                │ Function pointer call
                                ▼
┌─────────────────────────────────────────────────────────────────────┐
│ GameAssembly.dll (IL2CPP Runtime)                                   │
│                                                                     │
│ il2cpp_runtime_invoke:                                              │
│   1. Validate method signature                                      │
│   2. Set up stack frame                                             │
│   3. Call native compiled method code                               │
│   4. Return result                                                  │
└───────────────────────────────┬─────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────────┐
│ Native Compiled Method (from original Unity C#)                     │
│                                                                     │
│ // Original: public Transform transform { get; }                    │
│ // Compiled to native x64 assembly                                  │
│ mov rax, [rcx+0x10]  ; Load m_CachedTransform field                 │
│ ret                                                                 │
└───────────────────────────────┬─────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────────┐
│ Return Path                                                         │
│                                                                     │
│ IL2CPP returns Transform* → MDB_Bridge returns void*                │
│ → Il2CppRuntime receives IntPtr → Il2CppMarshaler.MarshalReturn<T>  │
│ → Detects wrapper type → Creates new Transform(nativePtr)           │
│ → Returns managed Transform wrapper object                          │
└─────────────────────────────────────────────────────────────────────┘
```

### Example: Calling a Method with Arguments

```csharp
// Your mod code
player.SetActive(false);
```

```
┌─────────────────────────────────────────────────────────────────────┐
│ Generated Wrapper Method                                            │
│                                                                     │
│ public void SetActive(bool value)                                   │
│ {                                                                   │
│     Il2CppRuntime.InvokeVoid(this, "SetActive",                     │
│                              new[] { typeof(bool) }, value);        │
│ }                                                                   │
└───────────────────────────────┬─────────────────────────────────────┘
                                │
                                ▼
┌─────────────────────────────────────────────────────────────────────┐
│ Argument Marshaling                                                 │
│                                                                     │
│ bool value = false → boxed to IL2CPP bool (1 byte, value 0)         │
│ Stored in IntPtr[] args array                                       │
└───────────────────────────────┬─────────────────────────────────────┘
                                │
                                ▼
                     (continues through bridge to native...)
```

---

## Type Marshaling

Converting data between .NET and IL2CPP is one of the most complex parts of the framework.

### Primitive Types

Primitives marshal directly (same memory representation):
- `int`, `float`, `double`, `bool`, `byte`, etc.

```csharp
// Primitives: just copy the bytes
if (typeof(T) == typeof(int))
    return (T)(object)result.ToInt32();
if (typeof(T) == typeof(float))
    return (T)(object)BitConverter.ToSingle(BitConverter.GetBytes(result.ToInt32()), 0);
```

### Strings

IL2CPP strings have a specific memory layout:

```
┌─────────────────────────────────────────┐
│ Il2CppString                            │
├─────────────────────────────────────────┤
│ Il2CppObject header (16 bytes)          │
│ int32_t length      (4 bytes)           │ ← String length
│ char16_t chars[0]   (variable)          │ ← UTF-16 characters
└─────────────────────────────────────────┘
```

```csharp
public static string MarshalString(IntPtr il2cppString)
{
    if (il2cppString == IntPtr.Zero) return null;
    
    // Length is at offset 16 (after Il2CppObject header)
    int length = Marshal.ReadInt32(il2cppString, 16);
    
    // Characters start at offset 20
    char[] chars = new char[length];
    for (int i = 0; i < length; i++)
    {
        chars[i] = (char)Marshal.ReadInt16(il2cppString, 20 + i * 2);
    }
    
    return new string(chars);
}
```

### System.Type Parameters

Some Unity APIs require `System.Type` parameters (e.g., `FindObjectsByType<T>`).
IL2CPP has its own type system, so we must convert:

```csharp
public static IntPtr MarshalType(Type managedType)
{
    // 1. Get the IL2CPP class for this type
    string ns = managedType.Namespace ?? "";
    string name = managedType.Name;
    
    IntPtr klass = Il2CppBridge.mdb_find_class("", ns, name);
    if (klass == IntPtr.Zero)
    {
        // Try common assemblies
        klass = Il2CppBridge.mdb_find_class("UnityEngine.CoreModule", ns, name);
    }
    
    // 2. Get Il2CppType* from Il2CppClass*
    IntPtr il2cppType = Il2CppBridge.mdb_class_get_type(klass);
    
    // 3. Get System.Type (Il2CppReflectionType*) from Il2CppType*
    IntPtr typeObject = Il2CppBridge.mdb_type_get_object(il2cppType);
    
    return typeObject;
}
```

### Arrays

IL2CPP arrays are marshaled by reading the header and elements:

```csharp
public static T[] MarshalArrayReturn<T>(IntPtr arrayPtr)
{
    if (arrayPtr == IntPtr.Zero) return null;
    
    // Read array length from offset 24 (after 24-byte header)
    long length = Marshal.ReadInt64(arrayPtr, 24);
    
    // Elements start at offset 32
    T[] result = new T[length];
    int elementSize = Marshal.SizeOf<IntPtr>(); // For reference types
    
    for (int i = 0; i < length; i++)
    {
        IntPtr elementPtr = Marshal.ReadIntPtr(arrayPtr, 32 + i * elementSize);
        
        if (typeof(T).IsSubclassOf(typeof(Il2CppObject)))
        {
            // Create wrapper instance with native pointer
            result[i] = (T)Activator.CreateInstance(typeof(T), elementPtr);
        }
        else
        {
            result[i] = MarshalReturn<T>(elementPtr);
        }
    }
    
    return result;
}
```

### Enums

Enums are converted to their underlying integer type:

```csharp
public static IntPtr MarshalArgument(object arg, Type paramType)
{
    if (paramType.IsEnum)
    {
        // Convert enum to its underlying type (usually int)
        int enumValue = Convert.ToInt32(arg);
        return new IntPtr(enumValue);
    }
    // ...
}
```

### IL2CPP Objects (Wrapper Classes)

Wrapper classes simply pass their `NativePtr`:

```csharp
if (arg is Il2CppObject il2cppObj)
{
    return il2cppObj.NativePtr;
}
```

---

## Creating a Mod

### Step 1: Create Project

Create a .NET Framework 4.7.2 class library:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net472</TargetFramework>
    <AssemblyName>MyAwesomeMod</AssemblyName>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
  
  <ItemGroup>
    <Reference Include="GameSDK.ModHost">
      <HintPath>..\path\to\GameSDK.ModHost.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>
</Project>
```

### Step 2: Create Mod Class

```csharp
using System;
using GameSDK;
using GameSDK.ModHost;
using UnityEngine;

namespace MyAwesomeMod
{
    [Mod("My Awesome Mod", "1.0.0", "YourName")]
    public class MyMod : ModBase
    {
        private int frameCount = 0;
        
        public override void OnLoad()
        {
            Log.Info("My Awesome Mod loaded!");
            
            // Example: Find all GameObjects in the scene
            var allObjects = UnityEngine.Object.FindObjectsByType<GameObject>(
                FindObjectsSortMode.None);
            
            Log.Info($"Found {allObjects.Length} GameObjects!");
            
            // List first 10 objects
            for (int i = 0; i < Math.Min(10, allObjects.Length); i++)
            {
                string name = allObjects[i].get_name();
                Log.Info($"  - {name}");
            }
        }
        
        public override void OnUpdate()
        {
            frameCount++;
            
            // Do something every 300 frames (~5 seconds at 60fps)
            if (frameCount % 300 == 0)
            {
                Log.Info($"Frame {frameCount} - still running!");
            }
        }
        
        public override void OnFixedUpdate()
        {
            // Physics-rate updates (50fps by default)
        }
        
        public override void OnLateUpdate()
        {
            // After all Update() methods have run
        }
    }
}
```

### Step 3: Using Game Wrappers

The generated wrappers let you interact with game objects:

```csharp
//Not all unity functions are available in all games, I tested on one and GameObject.Find() didnt exist

// Find objects
var player = GameObject.Find("Player");
var enemies = UnityEngine.FindObjectsByType(typeof(Enemy), FindObjectsSortMode.None);

// Access properties (generated as real C# properties)
Vector3 position = player.transform.position;
string playerName = player.name;

// Call methods
player.SetActive(false);  // Hide player
player.SetActive(true);   // Show player

// Work with components
var renderer = player.GetComponent(typeof(Renderer); // I actually havent tested this, idk if it works
var material = renderer.material;

// Modify values
transform.position = new Vector3(0, 10, 0);
```

### Step 4: Deploy

1. Build your mod project
2. Copy the output DLL to `<GameFolder>/MDB/Mods/`
3. Launch the game
4. Check `<GameFolder>/MDB/Logs/Mods.log` for output

---

## Method Hooking (Harmony-Style)

MDB Framework includes a powerful method hooking system inspired by Harmony/HarmonyX. You can intercept game methods, modify their behavior, or completely replace them.

### Quick Start

```csharp
using System;
using GameSDK;
using GameSDK.ModHost;
using GameSDK.ModHost.Patching;

namespace MyHookMod
{
    [Mod("My Hook Mod", "1.0.0", "YourName")]
    public class MyHookMod : ModBase
    {
        public override void OnLoad()
        {
            Logger.Info("Hook mod loaded! Patches are applied automatically.");
        }
    }

    // Patches are discovered and applied automatically when your mod loads
    [Patch("", "Player")]              // Target: global namespace, class "Player"
    [PatchMethod("Update", 0)]         // Target method "Update" with 0 parameters
    public static class PlayerUpdatePatch
    {
        [Prefix]
        public static bool Prefix(IntPtr __instance)
        {
            // Runs BEFORE Player.Update()
            // Return true to continue to original method
            // Return false to skip original method
            return true;
        }

        [Postfix]
        public static void Postfix(IntPtr __instance)
        {
            // Runs AFTER Player.Update()
        }
    }
}
```

### Patch Attributes

#### `[Patch]` - Target Class

```csharp
// Option 1: By namespace and class name (for IL2CPP lookup)
[Patch("UnityEngine", "GameObject")]

// Option 2: Using generated wrapper type (if available)
[Patch(typeof(Player))]

// Option 3: Empty namespace for global types
[Patch("", "GameManager")]
```

#### `[PatchMethod]` - Target Method

```csharp
// Specify method name and parameter count
[PatchMethod("TakeDamage", 1)]      // TakeDamage with 1 parameter

// Or just the name (parameter count inferred from patch method)
[PatchMethod("Update")]
```

#### `[PatchRva]` - Target by RVA (for obfuscated methods)

```csharp
[PatchRva(0x1A3B5C0)]  // Target method by its RVA offset
```

### Patch Types

#### `[Prefix]` - Runs Before Original

```csharp
[Prefix]
public static bool Prefix(IntPtr __instance, int __0, ref float __1)
{
    // __instance = the object instance (IntPtr.Zero for static methods)
    // __0 = first parameter (by index)
    // __1 = second parameter (ref to modify before calling original)
    
    // Return true = continue to original method
    // Return false = skip original method entirely
    return true;
}
```

#### `[Postfix]` - Runs After Original

```csharp
[Postfix]
public static void Postfix(IntPtr __instance, int __0, ref IntPtr __result)
{
    // __result = the return value (ref to modify)
    
    // Modify return value example:
    // __result = Il2CppBridge.mdb_new_string("Modified!");
}
```

#### `[Finalizer]` - Runs Even on Exception

```csharp
[Finalizer]
public static Exception Finalizer(Exception __exception)
{
    if (__exception != null)
    {
        Log.Error($"Method threw: {__exception.Message}");
        return null;  // Swallow exception
    }
    return __exception;  // Re-throw original
}
```

### Low-Level Hook API

For more control, use `HookManager` directly:

```csharp
using GameSDK.ModHost.Patching;

public override void OnLoad()
{
    // Find the method to hook
    IntPtr klass = Il2CppBridge.mdb_find_class("", "", "Player");
    IntPtr method = Il2CppBridge.mdb_get_method(klass, "Update", 0);
    
    // Create hook with callback
    HookInfo hook = HookManager.CreateHook(method, MyDetour, out IntPtr original, "Player.Update");
    
    // Store original for calling
    _originalUpdate = original;
}

private static IntPtr _originalUpdate;

private static IntPtr MyDetour(IntPtr instance, IntPtr args, IntPtr original)
{
    // Custom logic before
    Log.Info("Before Update");
    
    // Call original method
    IntPtr result = CallOriginal(_originalUpdate, instance);
    
    // Custom logic after
    Log.Info("After Update");
    
    return result;
}
```

### Hook by RVA

For obfuscated methods with Unicode names:

```csharp
// Hook by RVA offset (found in dump.cs)
HookInfo hook = HookManager.CreateHookByRva(0x1A3B5C0UL, MyDetour, out IntPtr original, "ObfuscatedMethod");
```

---

## ImGui Integration

MDB Framework includes built-in Dear ImGui support for creating in-game overlay UIs. ImGui is rendered on top of the game using DirectX hooks. I mostly implemented this for the Walmart UnityExplorer I created.

### Quick Start

```csharp
using System;
using System.Numerics;
using MDB.Explorer.ImGui;
using GameSDK.ModHost;

namespace MyImGuiMod
{
    [Mod("ImGui Demo", "1.0.0", "YourName")]
    public class ImGuiDemoMod : ModBase
    {
        private ImGuiController _imgui;
        private bool _showWindow = true;
        private string _playerName = "Player1";
        private float _speed = 1.0f;
        private bool _godMode = false;

        public override void OnLoad()
        {
            Logger.Info("ImGui Demo loading...");
            
            // Initialize ImGui controller
            _imgui = new ImGuiController();
            _imgui.OnDraw = DrawUI;
            
            if (_imgui.Initialize())
            {
                Logger.Info($"ImGui initialized! DirectX: {_imgui.DirectXVersion}");
                Logger.Info("Press F2 to toggle input capture");
            }
            else
            {
                Logger.Error("Failed to initialize ImGui");
            }
        }

        private void DrawUI()
        {
            // Create a window
            if (ImGui.Begin("My Mod Menu", ref _showWindow, ImGuiWindowFlags.None))
            {
                ImGui.Text("Welcome to my mod!");
                ImGui.Separator();
                
                // Text input
                ImGui.InputText("Player Name", ref _playerName, 64);
                
                // Slider
                ImGui.SliderFloat("Speed", ref _speed, 0.1f, 10.0f);
                
                // Checkbox
                ImGui.Checkbox("God Mode", ref _godMode);
                
                // Button
                if (ImGui.Button("Apply Settings"))
                {
                    Logger.Info($"Settings applied: {_playerName}, Speed={_speed}, GodMode={_godMode}");
                }
                
                ImGui.Separator();
                ImGui.TextDisabled("Press F2 to toggle mouse capture");
            }
            ImGui.End();
        }
    }
}
```

### ImGui API

The framework exposes a comprehensive ImGui API through P/Invoke bindings:

#### Windows

```csharp
// Basic window
if (ImGui.Begin("Window Title"))
{
    // Window content
}
ImGui.End();

// Window with close button
bool open = true;
if (ImGui.Begin("Closeable Window", ref open))
{
    // Content
}
ImGui.End();

// Child regions (scrollable areas)
if (ImGui.BeginChild("ScrollArea", new Vector2(0, 200)))
{
    // Scrollable content
}
ImGui.EndChild();
```

#### Text

```csharp
ImGui.Text("Normal text");
ImGui.TextDisabled("Grayed out text");
ImGui.TextWrapped("This text will wrap to multiple lines if needed.");
ImGui.TextColored(new Vector4(1, 0, 0, 1), "Red text");
```

#### Buttons & Inputs

```csharp
// Buttons
if (ImGui.Button("Click Me")) { /* clicked */ }
if (ImGui.SmallButton("Small")) { /* clicked */ }

// Checkbox
bool enabled = false;
ImGui.Checkbox("Enable Feature", ref enabled);

// Text input
string text = "";
ImGui.InputText("Label", ref text, 256);
ImGui.InputTextWithHint("Search", "Type here...", ref text, 256);

// Sliders
float value = 0.5f;
ImGui.SliderFloat("Volume", ref value, 0.0f, 1.0f);

int intValue = 50;
ImGui.SliderInt("Level", ref intValue, 1, 100);
```

#### Trees & Collapsing Headers

```csharp
// Collapsing header
if (ImGui.CollapsingHeader("Advanced Options"))
{
    ImGui.Text("Hidden content");
}

// Tree nodes
if (ImGui.TreeNode("Parent"))
{
    ImGui.Text("Child content");
    
    if (ImGui.TreeNode("Nested"))
    {
        ImGui.Text("Deeply nested");
        ImGui.TreePop();
    }
    
    ImGui.TreePop();
}

// Tree with flags
if (ImGui.TreeNodeEx("Selectable", ImGuiTreeNodeFlags.Selected | ImGuiTreeNodeFlags.OpenOnArrow))
{
    ImGui.Text("Selected node");
    ImGui.TreePop();
}
```

#### Layout

```csharp
ImGui.Separator();              // Horizontal line
ImGui.SameLine();               // Next widget on same line
ImGui.Indent();                 // Increase indentation
ImGui.Unindent();               // Decrease indentation
ImGui.SetNextItemWidth(200);    // Set next widget width
```

#### Menus

```csharp
if (ImGui.BeginMainMenuBar())
{
    if (ImGui.BeginMenu("File"))
    {
        if (ImGui.MenuItem("New")) { /* new */ }
        if (ImGui.MenuItem("Open", "Ctrl+O")) { /* open */ }
        ImGui.Separator();
        if (ImGui.MenuItem("Exit")) { /* exit */ }
        ImGui.EndMenu();
    }
    
    if (ImGui.BeginMenu("Edit"))
    {
        if (ImGui.MenuItem("Undo", "Ctrl+Z")) { }
        if (ImGui.MenuItem("Redo", "Ctrl+Y")) { }
        ImGui.EndMenu();
    }
    
    ImGui.EndMainMenuBar();
}
```

#### Styling

```csharp
// Push/pop colors
ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1, 0, 0, 1));
ImGui.Button("Red Button");
ImGui.PopStyleColor();

// Multiple style changes
ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1, 1, 0, 1));
ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.1f, 0.1f, 0.1f, 0.9f));
// ... draw content ...
ImGui.PopStyleColor(2);  // Pop 2 colors
```

### Input Control

ImGui input capture can be toggled with F2 (default):

```csharp
// Check if ImGui is capturing input
if (_imgui.IsInputEnabled)
{
    // Mouse/keyboard goes to ImGui
}

// Manually enable/disable
_imgui.IsInputEnabled = true;

// Change toggle key (uses Windows VK codes)
_imgui.SetToggleKey(0x71);  // F2 = 0x71
```

### DirectX Version Detection

The framework auto-detects DirectX 11 or 12:

```csharp
var version = _imgui.DirectXVersion;  // DX11 or DX12
Logger.Info($"Game uses DirectX {version}");
```

### Complete Example: Scene Explorer

```csharp
[Mod("Scene Explorer", "1.0.0", "MDB")]
public class SceneExplorer : ModBase
{
    private ImGuiController _imgui;
    private List<string> _gameObjects = new List<string>();
    private string _searchFilter = "";
    private int _selectedIndex = -1;

    public override void OnLoad()
    {
        _imgui = new ImGuiController();
        _imgui.OnDraw = DrawExplorer;
        _imgui.Initialize();
        
        RefreshGameObjects();
    }

    private void RefreshGameObjects()
    {
        _gameObjects.Clear();
        // Use wrapper to get all GameObjects
        var allObjects = UnityEngine.Object.FindObjectsByType(
            typeof(UnityEngine.GameObject), 
            FindObjectsSortMode.None);
        
        foreach (var obj in allObjects)
        {
            _gameObjects.Add(obj.get_name());
        }
    }

    private void DrawExplorer()
    {
        ImGui.SetNextWindowSize(new Vector2(400, 600), ImGuiCond.FirstUseEver);
        
        if (ImGui.Begin("Scene Explorer"))
        {
            // Search bar
            ImGui.InputTextWithHint("##search", "Filter...", ref _searchFilter, 256);
            ImGui.SameLine();
            if (ImGui.Button("Refresh"))
            {
                RefreshGameObjects();
            }
            
            ImGui.Separator();
            
            // Object list
            if (ImGui.BeginChild("ObjectList"))
            {
                for (int i = 0; i < _gameObjects.Count; i++)
                {
                    string name = _gameObjects[i];
                    
                    // Filter
                    if (!string.IsNullOrEmpty(_searchFilter) && 
                        !name.ToLower().Contains(_searchFilter.ToLower()))
                        continue;
                    
                    bool selected = (i == _selectedIndex);
                    if (ImGui.Selectable(name, selected))
                    {
                        _selectedIndex = i;
                        Logger.Info($"Selected: {name}");
                    }
                }
            }
            ImGui.EndChild();
        }
        ImGui.End();
    }
}
```

### Cleanup

Always dispose the controller when your mod unloads:

```csharp
public class MyMod : ModBase, IDisposable
{
    private ImGuiController _imgui;
    
    public void Dispose()
    {
        _imgui?.Dispose();
    }
}
```

---

## API Reference

### ModBase

```csharp
public abstract class ModBase
{
    // Metadata from [Mod] attribute
    public string Name { get; }
    public string Version { get; }
    public string Author { get; }
    
    // Logger for this mod
    protected ModLogger Log { get; }
    
    // Lifecycle callbacks
    public virtual void OnLoad() { }        // Called once when mod loads
    public virtual void OnUpdate() { }      // Called every frame
    public virtual void OnFixedUpdate() { } // Called on physics tick
    public virtual void OnLateUpdate() { }  // Called after all Updates
    public virtual void OnGUI() { }         // Called during Unity's GUI phase (for IMGUI)
}
```

### ModLogger

```csharp
Log.Info("Information message");
Log.Warning("Warning message");  
Log.Error("Error message");
Log.Debug("Debug message");

// Output format:
// [HH:MM:SS.mmm] [LEVEL] [ModName] Message
```

### Il2CppRuntime

```csharp
// Instance method calls
T result = Il2CppRuntime.Call<T>(instance, "MethodName", paramTypes, args);
Il2CppRuntime.InvokeVoid(instance, "MethodName", paramTypes, args);

// Static method calls
T result = Il2CppRuntime.CallStatic<T>("Namespace", "ClassName", "MethodName", paramTypes, args);
Il2CppRuntime.InvokeStaticVoid("Namespace", "ClassName", "MethodName", paramTypes, args);

// Object creation
IntPtr klass = Il2CppRuntime.FindClass("UnityEngine", "GameObject");
IntPtr obj = Il2CppBridge.mdb_new_object(klass);
IntPtr str = Il2CppBridge.mdb_new_string("Hello World");
```

### Common Unity Types

```csharp
// Vector3
Vector3 pos = new Vector3(1.0f, 2.0f, 3.0f);
float magnitude = pos.magnitude;
Vector3 normalized = pos.normalized;

// Quaternion
Quaternion rot = Quaternion.identity;
Quaternion euler = Quaternion.Euler(0, 90, 0);

// Color
Color red = new Color(1, 0, 0, 1);
Color32 blue = new Color32(0, 0, 255, 255);

// These are defined in UnityValueTypes.cs as structs
// They marshal directly to IL2CPP (same memory layout)
```

### HookManager

```csharp
using GameSDK.ModHost.Patching;

// Create a hook on an IL2CPP method
HookInfo hook = HookManager.CreateHook(methodPtr, detourDelegate, out IntPtr original, "Description");

// Create a hook by RVA (for obfuscated methods)
HookInfo hook = HookManager.CreateHookByRva(0x1A3B5C0UL, detourDelegate, out IntPtr original, "Description");

// Create a hook on a raw pointer
HookInfo hook = HookManager.CreateHookByPtr(targetPtr, detourDelegate, out IntPtr original, "Description");

// Enable/disable a hook
HookManager.SetHookEnabled(hook.Handle, false);  // Disable
HookManager.SetHookEnabled(hook.Handle, true);   // Re-enable

// Remove a hook
HookManager.RemoveHook(hook);
HookManager.RemoveAllHooks();

// Get all active hooks
IEnumerable<HookInfo> allHooks = HookManager.GetAllHooks();
```

### Patch Attributes

```csharp
// Target a class for patching
[Patch("Namespace", "ClassName")]           // By namespace and name
[Patch(typeof(GeneratedWrapper))]           // By wrapper type

// Specify target method
[PatchMethod("MethodName")]                 // By name
[PatchMethod("MethodName", 2)]              // By name and parameter count

// Target obfuscated method by RVA
[PatchRva(0x1A3B5C0)]

// Patch method types
[Prefix]        // Runs before original
[Postfix]       // Runs after original  
[Finalizer]     // Runs even on exception
```

### ImGuiController

```csharp
using MDB.Explorer.ImGui;

// Create and initialize
var imgui = new ImGuiController();
imgui.OnDraw = () => { /* draw ImGui content */ };
bool success = imgui.Initialize();

// Properties
bool ready = imgui.IsInitialized;
bool capturing = imgui.IsInputEnabled;
DxVersion dx = imgui.DirectXVersion;  // DX11 or DX12

// Control input capture
imgui.IsInputEnabled = true;
imgui.SetToggleKey(0x71);  // VK_F2

// Cleanup
imgui.Shutdown();
imgui.Dispose();
```

---

## Building

### Prerequisites

- **Visual Studio 2022** with:
  - "Desktop development with C++" workload
  - Windows SDK
- **.NET SDK 8.0+** (for development builds) OR just **.NET Framework 4.7.2** (parser has csc.exe fallback)
- **Python 3.x** (for wrapper generation)

### Automatic Build (Recommended)

The framework includes an automatic build pipeline. When using the dumper:

1. **Dumper runs** → Writes `dump.cs` to `GameDir/MDB/Dump/`
2. **Dumper triggers** → `build.bat` in the Dump folder
3. **Parser executes** → Generates wrappers and builds `GameSDK.ModHost.dll`
4. **Output ready** → DLL placed in `GameDir/MDB/Managed/`

### Manual Build Steps

```powershell
# Navigate to game's Dump folder
cd "C:\Path\To\Game\MDB\Dump"

# Run the parser (generates wrappers and builds automatically)
python wrapper_generator.py dump.cs

# Or run without argument if dump.cs is in same folder
python wrapper_generator.py
```

The parser will:
1. Parse `dump.cs` and generate wrapper files to `MDB_Core/Generated/`
2. Build `GameSDK.ModHost.dll` using dotnet CLI (preferred) or csc.exe (fallback)
3. Output to `../Managed/GameSDK.ModHost.dll`

### Build Native Components (One-time)

```powershell
# Build the dumper (Visual Studio)
msbuild "MDB_Dumper\Il2CppRuntimeDumper.vcxproj" /p:Configuration=Release /p:Platform=x64

# Build the bridge (Visual Studio)
msbuild "MDB_Bridge\MDB_Bridge.vcxproj" /p:Configuration=Release /p:Platform=x64
```

### Deployment Structure

```
<GameFolder>/
├── GameAssembly.dll                # Original game
├── version.dll                     # MDB_Bridge.dll renamed (proxy injection)
└── MDB/
    ├── MDB_Bridge.dll              # Native bridge (copy, not renamed)
    ├── Development/                # Mod development template
    │   ├── ExampleMod.cs           # Example mod source
    │   └── ExampleMod.csproj       # Project file referencing GameSDK.ModHost.dll
    ├── Dump/
    │   ├── dump.cs                 # IL2CPP metadata dump
    │   ├── wrapper_generator.py    # Parser script
    │   ├── build.bat               # Build trigger (called automatically)
    │   ├── Il2CppRuntimeDumper.dll # Dumper DLL
    │   ├── @Logs/                  # Parser logs
    │   └── MDB_Core/               # C# SDK sources
    │       ├── Core/               # Runtime core files
    │       ├── Generated/          # Generated wrapper classes (GameSDK.*.cs)
    │       ├── ModHost/            # Mod system (ModBase, Patching, etc.)
    │       └── GameSDK.ModHost.csproj
    ├── Managed/
    │   ├── GameSDK.ModHost.dll     # Compiled mod host + wrappers
    │   └── GameSDK.ModHost.pdb     # Debug symbols
    ├── Mods/
    │   ├── YourMod.dll             # Your mods here
    │   └── MDB_Explorer_ImGui.dll  # Built-in scene explorer (optional)
    └── Logs/
        ├── MDB.log                 # Bridge/runtime logs
        └── Mods.log                # Mod-specific logs
```

---

## Troubleshooting

### "SDK Build Failed"
The parser failed to build `GameSDK.ModHost.dll`. Causes:
- This is absolutely the most likely cause of issues. It's nearly impossible for the parser to generate perfect code for every game. If the build fails, check the log files in `MDB/Dump/@Logs/` for details and report issues on GitHub. If I own the game, I may be able to fix the parser for it.
- Missing .NET SDK installation
- Incorrect .NET Framework version
- Ambiguous references in Generated Wrapper files
- Missing types in Generated Wrappers

### "Instance is null or invalid for method X"

The wrapper object's `NativePtr` is `IntPtr.Zero`. Causes:
- The object was garbage collected by IL2CPP
- The object was never properly initialized
- Array marshaling returned invalid pointers

**Fix:** Verify the object is valid before calling methods:
```csharp
if (obj != null && obj.IsValid)
{
    obj.SomeMethod();
}
```

### "Method X not found"

The method doesn't exist with that exact name/parameter count. Causes:
- Method name is obfuscated in this game version
- Wrong parameter count
- Method is generic (generics are erased)

**Fix:** Check `dump.cs` for the actual method signature.

### Unicode/Obfuscated Methods

If a game uses Unicode obfuscation (e.g., Malayalam script characters), the parser automatically:
1. Renames methods to `unicode_method_N`
2. Uses RVA-based calling instead of name lookup
3. Stores original Unicode names for class resolution

You can find these methods in the generated wrappers:
```csharp
// Look for methods like this in generated code
public void unicode_method_42() { ... }  // RVA-based call
```

The original Unicode names are preserved as comments in the generated code.

### "Class not found: Namespace.ClassName"

The IL2CPP class lookup failed. Causes:
- Wrong namespace (check dump.cs)
- Class is in a different assembly
- Class is internal/private

**Fix:** Use the exact namespace from `dump.cs`. Try empty assembly name `""`.

### Mod doesn't load

Check `MDB/Logs/Mods.log` for errors. Common issues:
- Missing `[Mod]` attribute
- Not inheriting from `ModBase`
- DLL not in correct folder
- Wrong .NET Framework version

### Game crashes on startup

Usually a native bridge issue:
- Wrong architecture (must be x64)
- IL2CPP API mismatch (game update changed exports)
- CLR hosting failed
- Game has anti-cheat protections that block injection

**Fix:** Check Windows Event Viewer for crash details.

---

## How It All Comes Together

1. **Game launches** → Loads `GameAssembly.dll` (IL2CPP runtime)
2. **Injection** → `MDB_Bridge.dll` loads into process (via proxy DLL, injector, etc.)
3. **MinHook initialized** → Native hooking library ready
4. **CLR Hosting** → Bridge starts .NET Framework 4.0 CLR
5. **ImGui initialized** → DirectX Present hook installed for overlay
6. **ModHost loads** → `GameSDK.ModHost.dll` loaded into CLR
7. **ModManager starts** → Scans `MDB/Mods/` for mod DLLs
8. **Mods instantiated** → Creates instances of classes with `[Mod]` attribute
9. **Patches applied** → `PatchProcessor` discovers `[Patch]` classes and installs hooks
10. **OnLoad called** → Each mod's `OnLoad()` executes
11. **Game loop hooks** → Unity callbacks trigger `OnUpdate()`, `OnGUI()`, etc.
12. **Hooks execute** → Prefix/Postfix patches intercept game methods
13. **ImGui renders** → Draw callbacks execute during Present hook
14. **Wrapper calls** → Mods call wrapper methods (e.g., `GameObject.Find()`)
15. **P/Invoke** → Wrapper calls `Il2CppBridge` P/Invoke functions
16. **Native bridge** → `MDB_Bridge.dll` calls IL2CPP runtime functions
17. **IL2CPP execution** → Native compiled game code executes
18. **Return marshaling** → Results converted back to managed types
19. **Mod receives result** → Wrapper returns managed object to mod

---

## License

This project is for educational purposes. Use responsibly and respect game developers' terms of service. Modifying games may violate their EULA.

---

## Unicode/Obfuscation Deobfuscation

Many games use obfuscation techniques that replace readable method, class, and property names with Unicode characters (e.g., Malayalam script: `ക`, `ഖ`, `ഗ`). Since C# identifiers cannot contain non-ASCII Unicode characters, MDB Framework automatically deobfuscates these names while preserving full functionality.

### How It Works

When the parser encounters Unicode names:

1. **Detection** - Names containing non-ASCII characters (codepoint > 127) are detected
2. **Sanitization** - Unicode names are replaced with sequential identifiers:
   - `unicode_class_1`, `unicode_class_2`, etc.
   - `unicode_method_1`, `unicode_method_2`, etc.
   - `unicode_property_1`, `unicode_property_2`, etc.
   - `unicode_ns_1`, `unicode_ns_2`, etc. (for namespaces)
3. **RVA-Based Calling** - Methods with Unicode names use RVA (Relative Virtual Address) instead of name-based lookups
4. **Original Name Storage** - The original Unicode name is stored as a string constant for IL2CPP class lookups

### RVA-Based Method Calls

For methods with Unicode names, the parser generates RVA-based calls:

```csharp
// Generated wrapper for a method with Unicode name "ക"
public void unicode_method_42()
{
    Il2CppRuntime.InvokeVoidByRva(this, 0x1A3B5C0UL); // RVA from dump
}

public int unicode_method_43()
{
    return Il2CppRuntime.CallByRva<int>(this, 0x1A3B5D0UL); // RVA from dump
}
```

This bypasses IL2CPP's name-based method lookup entirely by:
1. Getting the `GameAssembly.dll` base address
2. Adding the method's RVA offset
3. Calling the method directly via function pointer

### Original Name Preservation

Classes with Unicode names store their original IL2CPP names:

```csharp
public class unicode_class_1 : ParentClass
{
    // Store original names for IL2CPP lookups
    internal static readonly string _il2cppClassName = "ക";  // Original Unicode name
    internal static readonly string _il2cppNamespace = "";
    
    // Static methods use original names for class resolution
    public static void unicode_method_1()
    {
        Il2CppRuntime.InvokeStaticVoidByRva(_il2cppNamespace, _il2cppClassName, 0x1A3B5E0UL);
    }
}
```

### Bridge Functions

The C++ bridge provides two new exports for RVA-based calling:

```cpp
// Get GameAssembly.dll base address
extern "C" __declspec(dllexport)
uint64_t mdb_get_gameassembly_base()
{
    return reinterpret_cast<uint64_t>(p_game_assembly);
}

// Calculate function pointer from RVA
extern "C" __declspec(dllexport)
uint64_t mdb_get_method_pointer_from_rva(uint64_t rva)
{
    return reinterpret_cast<uint64_t>(p_game_assembly) + rva;
}
```

### Why RVA Instead of Name Lookup?

- **Reliability** - RVA offsets are always valid, regardless of name encoding
- **Performance** - Direct pointer calls skip IL2CPP's method resolution
- **Compatibility** - Works with any Unicode script (Malayalam, Chinese, Japanese, etc.)
- **Future-proof** - New obfuscation schemes won't break method calls as long as RVAs are dumped

---

## Current Limitations

- **Universal but not perfect** - The parser handles most games automatically, but some edge cases may require adding types to skip lists
- **No automatic injection** - You need to bring your own DLL injector or use the version.dll proxy method - Doesnt work for every game. Actually, most games that go through the effort of encrypting the metadata also have anti-cheat the prevents side-loading.
- **No hot reload** - Restart the game to reload mods
- **Generic methods are tricky** - IL2CPP erases generics, so `List<Player>` becomes `List<object>` - Working on improving this.
- **Some games may detect injection** - Anti-cheat protected games will likely block this
- **ImGui DirectX only** - OpenGL/Vulkan games not currently supported
- **x64 only** - 32-bit games not supported

## Contributing

Found a bug? Have an improvement? PRs welcome! If the parser fails on a new game, please include the error log - it helps improve universal compatibility.

## Acknowledgments

Inspired by the excellent work of:
- [MelonLoader](https://github.com/LavaGang/MelonLoader)
- [BepInEx](https://github.com/BepInEx/BepInEx)
- [Il2CppDumper](https://github.com/Perfare/Il2CppDumper)
- [Il2CppAssemblyUnhollower](https://github.com/knah/Il2CppAssemblyUnhollower)
- [Il2CppRuntimeDumper](https://github.com/kagasu/Il2CppRuntimeDumper)
- [Dear ImGui](https://github.com/ocornut/imgui) - Immediate mode GUI library
- [MinHook](https://github.com/TsudaKageyu/minhook) - Minimalistic x86/x64 API hooking

These projects paved the way. MDB Framework just takes a different route to the same destination.

---

## Disclaimer

**This framework is provided "as-is" for educational and research purposes only.**

The author(s) of MDB Framework are **not responsible** for any consequences resulting from the use or misuse of this software. This includes but is not limited to:

- **Game bans or account suspensions** - Many games prohibit modding in their Terms of Service
- **Anti-cheat detections** - Using this framework may trigger anti-cheat systems
- **Data loss or corruption** - Modifying game memory can cause crashes or save corruption
- **Legal consequences** - Violating a game's EULA may have legal implications

**Before using MDB Framework:**

1. **Read the game's Terms of Service** - Respect the rules set by game developers
2. **Never use in online/multiplayer games** - This can ruin the experience for others and result in permanent bans
3. **Use only for single-player experimentation** - Or games that explicitly allow modding
4. **Understand the risks** - You are solely responsible for your actions

By using this framework, you acknowledge that you understand these risks and agree to use the software responsibly and ethically.
