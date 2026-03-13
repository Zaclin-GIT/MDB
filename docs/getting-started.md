---
layout: default
title: Getting Started
---

# Getting Started with MDB Framework

This guide will walk you through installing MDB Framework and creating your first mod for a Unity IL2CPP game.

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Building MDB Framework](#building-mdb)
3. [Deploying to a Game](#deploying)
   - [Proxy Mode (Recommended)](#proxy-mode)
   - [Direct Injection Mode](#direct-injection-mode)
4. [Creating Your First Mod](#first-mod)
5. [Testing Your Mod](#testing)
6. [Next Steps](#next-steps)

---

<a name="prerequisites"></a>
## Prerequisites

Before you begin, ensure you have:

- **Windows 10/11 (x64)** - MDB is Windows-only
- **Visual Studio 2022** with:
  - C++ desktop development workload (for MDB_Bridge)
  - .NET desktop development workload (for MDB_Core and mods)
- **MSBuild** - Installed with Visual Studio
- **.NET Framework 4.7.2 SDK or higher**
- **A Unity IL2CPP game (x64)**
- **DLL injector** (optional) — Only needed for [direct injection mode](#direct-injection-mode). Proxy mode requires no external tools.

### Supported Games

MDB works with any Unity game using IL2CPP runtime (x64 only). To check if a game uses IL2CPP:

1. Navigate to `<GameFolder>/GameName_Data/`
2. Look for `GameAssembly.dll` - This indicates IL2CPP
3. Look for `global-metadata.dat` - Additional confirmation

If you see `mono.dll` instead, the game uses Mono runtime and MDB won't work.

---

<a name="building-mdb"></a>
## Building MDB Framework

### 1. Clone the Repository

```bash
git clone https://github.com/Zaclin-GIT/MDB.git
cd MDB
```

### 2. Build MDB_Bridge (C++ Component)

The bridge is the native DLL that gets injected into the game.

```powershell
cd MDB_Bridge
msbuild MDB_Bridge.vcxproj /p:Configuration=Release /p:Platform=x64
```

Output: `MDB_Bridge/x64/Release/MDB_Bridge.dll`

**Note:** MDB_Bridge depends on vcpkg packages (MinHook, Dear ImGui). The build process will automatically restore these dependencies.

### 3. Build MDB_Core (C# Component)

The core contains the modding SDK and runtime.

```powershell
cd ../MDB_Core
dotnet build MDB_Core.csproj -c Release
```

Output: `MDB_Core/bin/Release/net48/MDB_Core.dll`

**Note:** MDB_Core is normally built automatically by the bridge at runtime. Manual building is only needed for development.

---

<a name="deploying"></a>
## Deploying to a Game

MDB supports two deployment modes. **Proxy mode** is recommended for most users — it requires no external tools. Direct injection is available for development and debugging.

<a name="proxy-mode"></a>
### Proxy Mode (Recommended)

Proxy mode exploits Windows' DLL search order. By renaming `MDB_Bridge.dll` to `version.dll` and placing it in the game folder, Windows loads our DLL automatically when the game starts. Our proxy transparently forwards all 17 version API calls to the real system DLL while bootstrapping the modding framework in the background.

**No external injector required.** Just drop files and launch.

#### Setup Steps

1. **Copy MDB_Core folder** from the repository to the game folder:
   ```powershell
   xcopy /E /I MDB_Core <GameFolder>\MDB_Core
   ```

2. **Rename and deploy MDB_Bridge.dll:**
   ```powershell
   copy MDB_Bridge\bin\Release\MDB_Bridge.dll <GameFolder>\version.dll
   ```

3. **Launch the game normally** — no injector needed.

#### Proxy Mode Directory Layout

```
<GameFolder>/
├── Game.exe                  ← Target application
├── GameAssembly.dll          ← IL2CPP runtime (Unity ships this)
├── version.dll               ← MDB_Bridge.dll renamed
├── MDB_Core/                 ← C# project sources (you deploy this)
│   ├── MDB_Core.csproj
│   ├── Core/
│   ├── ModHost/
│   └── Generated/            ← Auto-populated on first run
├── MDB/                      ← Auto-created by framework
│   ├── Logs/
│   │   └── MDB.log           ← Runtime log output
│   ├── Managed/
│   │   └── GameSDK.ModHost.dll  ← Auto-compiled SDK
│   ├── Mods/                 ← Place your mod DLLs here
│   └── MDB_Bridge.dll        ← Auto-copied for P/Invoke resolution
```

> **How it works:** The proxy DLL loads very early in process startup. It waits for `GameAssembly.dll` to load, then resolves IL2CPP exports, dumps metadata, generates C# wrappers, compiles the SDK via MSBuild, hosts the .NET CLR, and loads your mods. The `MDB_Bridge.dll` copy in `MDB/` is created automatically so that C# P/Invoke (`[DllImport("MDB_Bridge.dll")]`) can resolve the bridge — a named event guard prevents this second load from re-initializing.
>
> See the [Proxy DLL Injection Guide]({{ '/guides/proxy-injection' | relative_url }}) for the full technical deep-dive.

---

<a name="direct-injection-mode"></a>
### Direct Injection Mode

For development and debugging, you can inject `MDB_Bridge.dll` directly using an external injector.

#### Directory Structure

```
<GameFolder>/
├── Game.exe
├── MDB_Bridge.dll          ← Place the built bridge DLL here
├── MDB/
│   ├── Logs/               ← Auto-created for logs
│   ├── Managed/            ← Auto-created for SDK
│   └── Mods/
└── MDB_Core/               ← Place the core project here
```

#### Setup Steps

1. **Copy MDB_Bridge.dll** to the game's root folder (where the .exe is)

2. **Copy MDB_Core folder** from the repository to the game folder:
   ```powershell
   xcopy /E /I MDB_Core <GameFolder>\MDB_Core
   ```

3. **Launch the game**

4. **Inject MDB_Bridge.dll** using your DLL injector of choice:
   - Target process: `Game.exe`
   - DLL to inject: `<GameFolder>/MDB_Bridge.dll`

> **Note:** The same `MDB_Bridge.dll` binary works in both modes without recompilation. In direct injection mode, the P/Invoke bridge name resolves automatically since the DLL is already loaded under its own name.

### Comparing Injection Modes

| Aspect | Proxy Mode (`version.dll`) | Direct Injection (`MDB_Bridge.dll`) |
|--------|---------------------------|-------------------------------------|
| **Setup** | Rename DLL, copy to game folder | Copy to game folder, use external injector |
| **User experience** | Launch game normally | Launch game, then inject separately |
| **External tools** | None required | DLL injector required |
| **Timing** | Automatic — loaded at process start | Manual — user controls injection timing |
| **Anti-cheat risk** | Lower (OS-level DLL loading) | Higher (uses `CreateRemoteThread` or similar) |
| **Best for** | End users, distribution | Development, debugging |

### First Launch

Regardless of injection mode, the first launch takes 30–60 seconds:

1. **Wait for initialization:**
   - Dumps IL2CPP metadata
   - Generates C# wrappers (~10,000+ files)
   - Compiles `GameSDK.ModHost.dll` via MSBuild
   - Fabricates MDBRunner MonoBehaviour for main-thread callbacks
   - Loads any mods from `MDB/Mods/`

2. **Check the logs:**
   - `MDB/Logs/MDB.log` — Framework initialization (bridge, CLR hosting, shutdown)
   - `MDB/Logs/Mods.log` — Managed operations (injection, mod loading, hooks)

### Subsequent Launches

After the first successful launch, subsequent launches are instant. The framework detects that nothing has changed and skips the dump/build phase.

To force a full rebuild, delete `MDB/Managed/GameSDK.ModHost.dll` and all of the generated files in `MDB_Core/Generated/`.

---

<a name="first-mod"></a>
## Creating Your First Mod

Now that MDB is installed, let's create a simple mod.

### 1. Clone the Mod Skeleton

MDB provides a skeleton project you can clone to get started quickly:

```bash
git clone -b mdbmod https://github.com/Zaclin-GIT/MDB.Templates.git MyFirstMod
```

This gives you a ready-to-build project with:
- A `.csproj` targeting .NET Framework 4.8.1
- A `Mod.cs` entry point with lifecycle methods
- An `ImGuiWindow.cs` with a starter UI window

### 2. Add the SDK

The SDK (`GameSDK.MDBUnityStandard.dll`) is generated on first injection. You must inject MDB into the game at least once before you can build mods.

Once the SDK has been generated, copy it into the mod project's folder

### 3. Build Your Mod

```bash
cd MyFirstMod
dotnet build -c Release
```

Output: `bin\Release\net481\MyFirstMod.dll`

### 4. Deploy Your Mod

Copy the built DLL to the game's mod folder

---

<a name="next-steps"></a>
## Next Steps

Congratulations! You've created and loaded your first MDB mod. Here's what to explore next:

### Learn the APIs

- **[Mod Lifecycle]({{ '/api/modbase' | relative_url }})** - Understanding OnLoad, OnUpdate, and other callbacks
- **[Logger System]({{ '/api/logger' | relative_url }})** - Advanced logging features
- **[ImGui UI]({{ '/api/imgui' | relative_url }})** - Creating complex user interfaces

### Explore Examples

- **[HelloWorld]({{ '/examples' | relative_url }}#helloworld)** - Simple mod with ImGui
- **[UnityDebugInterceptor]({{ '/examples' | relative_url }}#unity-debug-interceptor)** - Method patching basics
- **[GameStats]({{ '/examples' | relative_url }}#gamestats)** - Advanced patching and IL2CPP Bridge
- **[MDB_Explorer_ImGui]({{ '/examples' | relative_url }}#mdb-explorer)** - Full-featured runtime inspector

### Advanced Topics

- **[Patch Attributes]({{ '/api/patch-attributes' | relative_url }})** - Declarative method hooking (recommended approach)
- **[HookManager]({{ '/api/hookmanager' | relative_url }})** - Advanced fallback for dynamic runtime hook management
- **[IL2CPP Bridge]({{ '/api/il2cpp-bridge' | relative_url }})** - Direct IL2CPP runtime access

---

## Troubleshooting

### MDB_Bridge.dll fails to inject

- Ensure the game is x64 (not 32-bit)
- Try running the game and injector as administrator
- Check if anti-cheat is blocking injection

### Build fails: SDK not found

- You must inject MDB once to generate the SDK
- Copy `GameSDK.MDBUnityStandard.dll` from `<GameFolder>/MDB/Managed/` into your project's `deps/` folder
- Verify the `HintPath` in your `.csproj` points to `deps\GameSDK.MDBUnityStandard.dll`

### Mod doesn't load

- Check `MDB/Logs/Mods.log` for errors
- Ensure the DLL is in `MDB/Mods/` folder
- Verify your mod class has the `[Mod]` attribute
- Ensure your mod inherits from `ModBase`

### ImGui window doesn't appear

- Press F2 to toggle input capture (enabled by default)
- Verify `RegisterCallback` was called in `OnLoad`

### Game crashes on injection

- Check `MDB/Logs/MDB.log` for errors
- Ensure MDB_Core folder is present and complete
- Try deleting `MDB/Managed/` and regenerating
- Some games may not be compatible

---

## Getting Help

If you're still stuck:

1. Look at the [Example Mods]({{ '/examples' | relative_url }})
2. Browse the [API Reference]({{ '/api' | relative_url }})
4. Open an issue on [GitHub](https://github.com/Zaclin-GIT/MDB/issues)

---

[← Back to Home]({{ '/' | relative_url }}) | [API Reference →]({{ '/api' | relative_url }})
