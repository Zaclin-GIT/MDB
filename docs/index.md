---
layout: default
title: MDB Framework
---

# MDB Framework

**Runtime IL2CPP modding framework for Unity games.** Dumps metadata, generates C# wrappers, builds an SDK, and loads mods — all automatically from a single DLL injection.

Encrypted `global-metadata.dat`? Don't care.

---

## Quick Links

- [Getting Started](getting-started) - Install and create your first mod
- [API Reference](api) - Complete API documentation
- [Guides](guides) - Tutorials and how-to guides
- [Examples](examples) - Working mod examples with explanations

---

## What is MDB Framework?

MDB Framework is a powerful runtime modding solution for Unity IL2CPP games. It eliminates the complexity of traditional IL2CPP modding by automating the entire workflow:

1. **Automatic Metadata Dumping** - Extracts all classes, methods, and fields from the IL2CPP runtime
2. **C# Wrapper Generation** - Creates type-safe C# wrappers for all game types
3. **SDK Auto-Building** - Compiles a complete modding SDK using MSBuild
4. **CLR Hosting** - Hosts .NET Framework 4.0 for managed mod execution
5. **Mod Loading** - Auto-discovers and loads mods from the `Mods/` folder
6. **Harmony-Style Patching** - Declarative method hooking with `[Patch]` attributes
7. **ImGui Integration** - Built-in Dear ImGui overlay with input capture

All of this happens automatically on first injection. Subsequent launches are instant if nothing has changed.

---

## Key Features

### 🚀 Zero-Configuration Setup
Inject `MDB_Bridge.dll` into your game and you're done. No manual dumping, no external tools, no complex setup.

### 🎯 Harmony-Style Patching
```csharp
[Patch("GameNamespace", "Player")]
[PatchMethod("TakeDamage", 1)]
public static class PlayerPatch
{
    [Prefix]
    public static bool Prefix(IntPtr __instance, int __0)
    {
        // Return false to skip original method
        return true;
    }
}
```

### 🎨 Built-in ImGui UI
```csharp
public override void OnLoad()
{
    ImGuiManager.RegisterCallback(DrawUI, "My Window");
}

private void DrawUI()
{
    if (ImGui.Begin("My Window"))
    {
        ImGui.Text("Hello from MDB!");
    }
    ImGui.End();
}
```

### 🔧 Full IL2CPP Bridge Access
```csharp
IntPtr playerClass = Il2CppBridge.mdb_find_class("Assembly-CSharp", "", "Player");
IntPtr healthField = Il2CppBridge.mdb_get_field(playerClass, "health");
float health = Il2CppBridge.mdb_field_get_value<float>(playerInstance, healthField);
```

### 🧬 Generic Type Resolution
Unlike traditional dumpers that erase generics to `object`, MDB resolves actual type arguments:
- `List<string>` stays `List<string>`
- `Dictionary<string, int>` stays `Dictionary<string, int>`
- System types are fully resolved at runtime

---

## How It Works

```
Inject MDB_Bridge.dll
  → Wait for GameAssembly.dll
  → Resolve IL2CPP API exports
  → Dump all classes/methods/fields with generic type resolution
  → Generate C# wrapper source files
  → Invoke MSBuild to compile GameSDK.ModHost.dll
  → Host .NET CLR (v4.0.30319)
  → Auto-discover and apply [Patch] hooks
  → Load mods from MDB/Mods/
  → Start update loop (~60Hz)
```

See the [Architecture](guides/architecture) guide for detailed explanation of each step.

---

## Supported Platforms

- **Operating System:** Windows (x64 only)
- **Unity Runtime:** IL2CPP
- **Graphics API:** DirectX 11 or DirectX 12 (for ImGui overlay)
- **.NET Target:** Framework 4.7.2 or higher

---

## Example Mods

The framework includes four example mods demonstrating every major API:

| Example | Difficulty | Description |
|---------|-----------|-------------|
| [HelloWorld](examples/helloworld) | 🟢 Simple | Lifecycle, Logger, basic ImGui |
| [UnityDebugInterceptor](examples/unity-debug-interceptor) | 🟢 Simple | Declarative patching, hooking Debug.Log |
| [GameStats](examples/gamestats) | 🟡 Medium | Advanced patching, IL2CPP Bridge, HookManager |
| [MDB_Explorer_ImGui](examples/mdb-explorer) | 🔴 Complex | Full IL2CPP reflection, scene traversal |

All examples target universal Unity types and work across any Unity IL2CPP game.

---

## Get Started

Ready to start modding? Head to the [Getting Started](getting-started) guide to:

1. Prepare your environment
2. Inject MDB into a Unity game
3. Create and load your first mod
4. Learn the mod lifecycle and APIs

---

## Disclaimer

**This framework is provided "as-is" for educational and research purposes only.**

- Many games prohibit modding in their Terms of Service
- Using this framework may trigger anti-cheat systems
- Never use in online/multiplayer games
- You are solely responsible for your actions

See the full [Disclaimer](#disclaimer-section) for important legal information.

---

## Acknowledgments

MDB Framework builds upon the excellent work of:

- [MelonLoader](https://github.com/LavaGang/MelonLoader) - Unity mod loader
- [BepInEx](https://github.com/BepInEx/BepInEx) - Unity/Mono mod framework
- [Il2CppDumper](https://github.com/Perfare/Il2CppDumper) - IL2CPP metadata dumper
- [Dear ImGui](https://github.com/ocornut/imgui) - Immediate mode GUI library
- [MinHook](https://github.com/TsudaKageyu/minhook) - x86/x64 API hooking library

---

<a name="disclaimer-section"></a>
## Full Disclaimer

The author(s) of MDB Framework are **not responsible** for any consequences resulting from the use or misuse of this software. This includes but is not limited to:

- **Game bans or account suspensions** - Many games prohibit modding
- **Anti-cheat detections** - May trigger anti-cheat systems
- **Data loss or corruption** - Memory modifications can cause crashes
- **Legal consequences** - Violating EULAs may have legal implications

**Before using MDB Framework:**

1. **Read the game's Terms of Service** - Respect the rules
2. **Never use in online/multiplayer** - This ruins experiences for others
3. **Use only for single-player** - Or games that explicitly allow modding
4. **Understand the risks** - You are solely responsible

By using this framework, you acknowledge that you understand these risks and agree to use it responsibly and ethically.
