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
- **DLL injector** - Such as [Extreme Injector](https://github.com/master131/ExtremeInjector)

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

### Directory Structure

MDB requires a specific folder layout in your game directory:

```
<GameFolder>/
├── GameName.exe
├── GameName_Data/
├── MDB_Bridge.dll          ← Place the built bridge DLL here
├── MDB/
│   ├── Logs/               ← Auto-created for logs
│   ├── Managed/            ← Auto-created for SDK
│   └── Mods/               ← Create this, place mod DLLs here
└── MDB_Core/
    ├── MDB_Core.csproj     ← Copy entire MDB_Core folder here
    ├── Core/
    ├── Deobfuscation/
    └── ModHost/
```

### Setup Steps

1. **Copy MDB_Bridge.dll** to the game's root folder (where the .exe is)

2. **Create MDB folder structure:**
   ```powershell
   mkdir MDB
   mkdir MDB\Mods
   ```

3. **Copy MDB_Core folder** from the repository to the game folder:
   ```powershell
   xcopy /E /I MDB_Core <GameFolder>\MDB_Core
   ```

### First Launch

1. **Inject MDB_Bridge.dll** using your DLL injector of choice
   - Target process: `GameName.exe`
   - DLL to inject: `<GameFolder>/MDB_Bridge.dll`

2. **Wait for initialization** - First launch takes 30-60 seconds:
   - Dumps IL2CPP metadata
   - Generates C# wrappers (~10,000+ files)
   - Compiles `GameSDK.ModHost.dll` via MSBuild
   - Loads any mods from `MDB/Mods/`

3. **Check the logs:**
   - `MDB/Logs/MDB.log` - Framework initialization and errors
   - `MDB/Logs/Mods.log` - Mod loading and output

### Subsequent Launches

After the first successful injection, subsequent launches are instant. The framework detects that nothing has changed and skips the dump/build phase.

To force a rebuild, delete `MDB/Managed/GameSDK.ModHost.dll`.

---

<a name="first-mod"></a>
## Creating Your First Mod

Now that MDB is installed, let's create a simple "Hello World" mod.

### 1. Create a New Project

Create a new .NET Framework class library:

```bash
mkdir MyFirstMod
cd MyFirstMod
dotnet new classlib -f net48
```

### 2. Add MDB Reference

Edit `MyFirstMod.csproj` and add a reference to the generated SDK:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net48</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>
  
  <ItemGroup>
    <!-- Reference the auto-generated SDK -->
    <Reference Include="GameSDK.ModHost">
      <HintPath>PATH_TO_GAME\MDB\Managed\GameSDK.ModHost.dll</HintPath>
    </Reference>
  </ItemGroup>
</Project>
```

Replace `PATH_TO_GAME` with your game's folder path.

**Important:** The SDK (`GameSDK.ModHost.dll`) is generated on first injection. You must inject MDB once before you can build mods.

### 3. Write Your Mod

Replace the contents of `Class1.cs` with:

```csharp
using System;
using GameSDK.ModHost;
using GameSDK.ModHost.ImGui;

namespace MyFirstMod
{
    [Mod("YourName.MyFirstMod", "My First Mod", "1.0.0", 
         Author = "Your Name", 
         Description = "My first MDB mod!")]
    public class MyFirstMod : ModBase
    {
        public override void OnLoad()
        {
            Logger.Info("My First Mod has loaded!");
            Logger.Warning("This is a warning message");
            Logger.Error("This is an error message");
            
            // Register an ImGui window
            ImGuiManager.RegisterCallback(DrawUI, "My First Window", 
                                         ImGuiCallbackPriority.Normal);
        }

        public override void OnUpdate()
        {
            // Called every frame
            // You can check input, update state, etc.
        }

        private void DrawUI()
        {
            if (ImGui.Begin("My First Window"))
            {
                ImGui.Text("Hello from my first mod!");
                ImGui.Separator();
                
                if (ImGui.Button("Click Me!"))
                {
                    Logger.Info("Button was clicked!");
                }
            }
            ImGui.End();
        }
    }
}
```

### 4. Build Your Mod

```bash
dotnet build -c Release
```

Output: `bin/Release/net48/MyFirstMod.dll`

### 5. Deploy Your Mod

Copy the built DLL to the game's mod folder:

```powershell
copy bin\Release\net48\MyFirstMod.dll <GameFolder>\MDB\Mods\
```

---

<a name="testing"></a>
## Testing Your Mod

1. **Launch the game** with MDB_Bridge.dll injected (if not already running)

2. **Press F2** to toggle ImGui input capture

3. **Look for your window** - "My First Window" should appear

4. **Check the logs:**
   ```
   MDB/Logs/Mods.log
   ```
   You should see:
   ```
   [INFO] [MyFirstMod] My First Mod has loaded!
   [WARN] [MyFirstMod] This is a warning message
   [ERROR] [MyFirstMod] This is an error message
   ```

5. **Interact with the UI:**
   - Click the "Click Me!" button
   - Check the logs for "Button was clicked!"

---

<a name="next-steps"></a>
## Next Steps

Congratulations! You've created and loaded your first MDB mod. Here's what to explore next:

### Learn the APIs

- **[Mod Lifecycle](../api/modbase)** - Understanding OnLoad, OnUpdate, and other callbacks
- **[Logger System](../api/logger)** - Advanced logging features
- **[ImGui UI](../api/imgui)** - Creating complex user interfaces

### Explore Examples

- **[HelloWorld](../examples/helloworld)** - Simple mod with ImGui
- **[UnityDebugInterceptor](../examples/unity-debug-interceptor)** - Method patching basics
- **[GameStats](../examples/gamestats)** - Advanced patching and IL2CPP Bridge
- **[MDB_Explorer_ImGui](../examples/mdb-explorer)** - Full-featured runtime inspector

### Advanced Topics

- **[Method Patching](../guides/patching)** - Hook and modify game methods
- **[IL2CPP Bridge](../guides/il2cpp-bridge)** - Direct IL2CPP runtime access
- **[Generic Type Resolution](../guides/generic-types)** - Working with generic types
- **[Deobfuscation](../guides/deobfuscation)** - Handling obfuscated games

---

## Troubleshooting

### MDB_Bridge.dll fails to inject

- Ensure the game is x64 (not 32-bit)
- Try running the game and injector as administrator
- Check if anti-cheat is blocking injection

### Build fails: "GameSDK.ModHost.dll not found"

- You must inject MDB once to generate the SDK
- Check `MDB/Managed/GameSDK.ModHost.dll` exists
- Update the `HintPath` in your .csproj to the correct path

### Mod doesn't load

- Check `MDB/Logs/Mods.log` for errors
- Ensure the DLL is in `MDB/Mods/` folder
- Verify your mod class has the `[Mod]` attribute
- Ensure your mod inherits from `ModBase`

### ImGui window doesn't appear

- Press F2 to toggle input capture
- Check if the window is off-screen (drag from edges)
- Verify `RegisterCallback` was called in `OnLoad`

### Game crashes on injection

- Check `MDB/Logs/MDB.log` for errors
- Ensure MDB_Core folder is present and complete
- Try deleting `MDB/Managed/` and regenerating
- Some games may not be compatible

---

## Getting Help

If you're still stuck:

1. Check the [Troubleshooting Guide](../guides/troubleshooting)
2. Review the [FAQ](../guides/faq)
3. Look at the [Example Mods](../examples)
4. Open an issue on [GitHub](https://github.com/Zaclin-GIT/MDB/issues)

---

[← Back to Home](../index) | [API Reference →](../api)
