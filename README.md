# MDB - IL2CPP Modding Framework

> **⚠️ EXTREMELY EARLY DEVELOPMENT WARNING ⚠️**
> 
> This framework is in very early stages of development. Expect bugs, breaking changes, and incomplete features. **If it works for your game, consider yourself lucky.**

---

<a href="https://buymeacoffee.com/winnforge">
  <img src="https://img.shields.io/badge/Buy%20Me%20A%20Coffee-%23FFDD00?style=for-the-badge&logo=buymeacoffee&logoColor=black" alt="Buy Me A Coffee">
</a>

## What Is This?

MDB Framework enables modding of Unity IL2CPP games by **dumping metadata at runtime** - bypassing encrypted `global-metadata.dat` files entirely. It then generates C# wrapper classes that let you interact with game objects using familiar syntax.

**TL;DR:** Encrypted metadata? Don't care.

---

## Table of Contents

- [Quick Start](#quick-start)
- [Architecture Overview](#architecture-overview)
- [Creating a Mod](#creating-a-mod)
- [Method Hooking](#method-hooking)
- [ImGui Integration](#imgui-integration)
- [API Reference](#api-reference)
- [Building](#building)
- [Troubleshooting](#troubleshooting)
- [Limitations](#limitations)

---

## Quick Start

### Deployment Structure

```
<GameFolder>/
├── MDB_Bridge.dll
└── MDB/
    ├── Dump/
    │   ├── dump.cs                 # IL2CPP metadata dump
    │   └── wrapper_generator.py    # Parser script
    ├── Managed/
    │   └── GameSDK.Core.dll        # Compiled SDK + wrappers
    ├── Mods/
    │   └── YourMod.dll             # Your mods here
    └── Logs/
        └── Mods.log                # Mod logs
```

### Workflow

1. **Inject the dumper** → Generates `dump.cs` with all game types
2. **Create your mod** → Reference `GameSDK.Core.dll`, write C# code
3. **Deploy** → Copy mod DLL to `MDB/Mods/`
4. **Launch game and Inject MDB_Bridge.dll** → Mods load automatically

---

## Architecture Overview

```
┌───────────────────────────────────────────────────────────────┐
│                        Game Process                           │
├───────────────────────────────────────────────────────────────┤
│  IL2CPP Runtime          │         .NET CLR (v4.0)            │
│  ┌─────────────────┐     │    ┌─────────────────────────────┐ │
│  │ GameAssembly.dll│◄────┼────│ GameSDK.Core.dll            │ │
│  │ (native code)   │  P/Invoke│ ├─ Generated Wrappers       │ │
│  └────────┬────────┘     │    │ ├─ ModManager               │ │
│           │              │    │ └─ Your Mods                │ │
│  ┌────────▼────────┐     │    └─────────────────────────────┘ │
│  │   MinHook       │     │                                    │
│  │   (Hooking)     │     │    ┌─────────────────────────────┐ │
│  └─────────────────┘     │    │ Dear ImGui Overlay          │ │
│                          │    │ (DX11/DX12 auto-detect)     │ │
│  MDB_Bridge.dll ─────────┼────└─────────────────────────────┘ │
│  (CLR Host + IL2CPP)     │                                    │
└───────────────────────────────────────────────────────────────┘
```

---

## Creating a Mod

### Step 1: Create Project

Create a .NET Framework 4.8.1 class library referencing `GameSDK.Core.dll`.

### Step 2: Write Your Mod

```csharp
using System;
using GameSDK;
using GameSDK.ModHost;
using UnityEngine;

namespace MyMod
{
    [Mod("My Awesome Mod", "1.0.0", "YourName")]
    public class MyMod : ModBase
    {
        public override void OnLoad()
        {
            Log.Info("Mod loaded!");
            
            // Find game objects
            var player = GameObject.Find("Player");
            if (player != null && player.IsValid)
            {
                Log.Info($"Found player at {player.transform.position}");
            }
        }
        
        public override void OnUpdate()
        {
            // Called every frame
        }
    }
}
```

### Step 3: Deploy

1. Build your mod DLL
2. Copy to `<GameFolder>/MDB/Mods/`
3. Launch game

---

## Method Hooking

Intercept game methods using Harmony-style attributes:

```csharp
[Mod("Hook Example", "1.0.0", "YourName")]
public class HookMod : ModBase
{
    public override void OnLoad()
    {
        Log.Info("Patches applied automatically!");
    }
}

[Patch("", "Player")]              // Target class
[PatchMethod("TakeDamage", 1)]     // Target method
public static class PlayerPatch
{
    [Prefix]
    public static bool Prefix(IntPtr __instance, int __0)
    {
        // __0 = first parameter (damage amount)
        // Return false to skip original method
        return true;
    }

    [Postfix]
    public static void Postfix(IntPtr __instance)
    {
        // Runs after original method
    }
}
```

### Patch Attributes

```csharp
[Patch("Namespace", "ClassName")]    // Target class
[Patch(typeof(GeneratedWrapper))]    // Or by type

[PatchMethod("MethodName", 2)]       // Target method (name, param count)
[PatchRva(0x1A3B5C0)]                // Target by RVA offset

[Prefix]     // Return false to skip original
[Postfix]    // Runs after original
[Finalizer]  // Runs even on exception
```

### Hook by RVA (for obfuscated methods)

```csharp
[PatchRva(0x1A3B5C0)]  // Target by memory offset
public static class ObfuscatedPatch { ... }
```

---

## ImGui Integration

Create in-game overlay UIs with Dear ImGui:

```csharp
using MDB.Explorer.ImGui;

[Mod("ImGui Demo", "1.0.0", "YourName")]
public class ImGuiMod : ModBase
{
    private ImGuiController _imgui;
    private bool _showWindow = true;
    private float _speed = 1.0f;

    public override void OnLoad()
    {
        _imgui = new ImGuiController();
        _imgui.OnDraw = DrawUI;
        _imgui.Initialize();
        Log.Info("Press F2 to toggle input capture");
    }

    private void DrawUI()
    {
        if (ImGui.Begin("My Menu", ref _showWindow))
        {
            ImGui.Text("Hello from ImGui!");
            ImGui.SliderFloat("Speed", ref _speed, 0.1f, 10.0f);
            
            if (ImGui.Button("Apply"))
                Log.Info($"Speed set to {_speed}");
        }
        ImGui.End();
    }
}
```

---

## API Reference

### ModBase

```csharp
[Mod("Name", "Version", "Author")]
public class MyMod : ModBase
{
    protected ModLogger Log { get; }           // Logger instance
    
    public virtual void OnLoad() { }           // Called once when mod loads
    public virtual void OnUpdate() { }         // Called every frame
    public virtual void OnFixedUpdate() { }    // Called on physics tick
    public virtual void OnLateUpdate() { }     // Called after all Updates
}
```

### ModLogger

```csharp
Log.Info("Message");                           // Blue - normal info
Log.Warning("Message");                        // Yellow - warnings
Log.Error("Message");                          // Red - errors
Log.Error("Message", exception);               // Red - with exception details
```

---

## Troubleshooting

| Problem | Solution |
|---------|----------|
| **SDK Build Failed** | Check `MDB/Dump/@Logs/` for details. Parser may need game-specific fixes. |
| **"Method not found"** | Check `dump.cs` for exact method name/signature. May be obfuscated. |
| **"Class not found"** | Use exact namespace from `dump.cs`. Try empty namespace `""`. |
| **Mod doesn't load** | Check `MDB/Logs/Mods.log`. Verify `[Mod]` attribute and `ModBase` inheritance. |
| **Game crashes** | Check Event Viewer. May be architecture mismatch or anti-cheat. |

### Unicode/Obfuscated Names

Games with Unicode obfuscation (e.g., Malayalam script) are handled automatically:
- Names become `unicode_method_1`, `unicode_class_2`, etc.
- Methods use RVA-based calling instead of name lookup
- Original names stored for IL2CPP class resolution

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