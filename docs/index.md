---
layout: default
title: MDB Framework
---

# MDB Framework

**Runtime IL2CPP modding framework for Unity games.** Dumps metadata, generates C# wrappers, builds an SDK, and loads mods — all automatically from a single DLL injection.

Encrypted `global-metadata.dat`? Don't care.

---

## Quick Links

- [Getting Started]({{ '/getting-started' | relative_url }}) - Install and create your first mod
- [API Reference]({{ '/api' | relative_url }}) - Complete API documentation
- [Guides]({{ '/guides' | relative_url }}) - Architecture deep-dives and technical internals
- [Examples]({{ '/examples' | relative_url }}) - Working mod examples with explanations

---

## What is MDB Framework?

MDB Framework is a powerful runtime modding solution for Unity IL2CPP games. It eliminates the complexity of traditional IL2CPP modding by automating the entire workflow:

1. **Proxy DLL Injection** - Ships as a `version.dll` proxy — just drop it in the game folder, no external injector needed
2. **Automatic Metadata Dumping** - Extracts all classes, methods, and fields from the IL2CPP runtime
3. **C# Wrapper Generation** - Creates type-safe C# wrappers for all game types
4. **SDK Auto-Building** - Compiles a complete modding SDK using MSBuild
5. **CLR Hosting** - Hosts .NET Framework 4.7.2 via COM-based CLR hosting
6. **MonoBehaviour Injection** - Fabricates an IL2CPP MonoBehaviour subclass in memory for main-thread callbacks
7. **Mod Loading** - Auto-discovers and loads mods from the `Mods/` folder
8. **Harmony-Style Patching** - Declarative method hooking with `[Patch]` attributes
9. **ImGui Integration** - Built-in Dear ImGui overlay with input capture (DX11/DX12)

All of this happens automatically on first injection. Subsequent launches are instant if nothing has changed.

---

## Key Features

### 🚀 Zero-Configuration Setup
Rename `MDB_Bridge.dll` to `version.dll`, drop it in the game folder, and launch the game — no injector required. MDB also supports direct DLL injection for development. No manual dumping, no external tools, no complex setup.

### 🎯 Harmony-Style Patching
```csharp
[Patch("GameNamespace", "Player")]
[PatchMethod("TakeDamage", 1)]
public static class PlayerPatch
{
    [Prefix]
    public static bool Prefix(IntPtr __instance, int damage)
    {
        // Named parameters map positionally to native args
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

MDB supports two injection modes: **proxy mode** (recommended) and **direct injection** (for development).

In proxy mode, `MDB_Bridge.dll` is renamed to `version.dll` and placed in the game folder. Windows loads it automatically at startup via DLL search order — no external injector needed.

```
Game.exe launches
  → Windows loads version.dll (our proxy) from game directory
  → DllMain spawns background initialization thread
  → Proxy forwards all 17 version API calls to the real System32 version.dll
  → Background thread polls for GameAssembly.dll (up to 30s)
  → Resolves 50+ IL2CPP function exports (with obfuscation fallback)
  → Dumps all IL2CPP metadata (classes, methods, fields, properties)
  → Generates C# wrapper source files with full generic type resolution
  → Invokes MSBuild to compile GameSDK.ModHost.dll
  → Hosts .NET Framework 4.7.2 CLR via COM (ICLRRuntimeHost)
  → Calls ModManager.Initialize() → discovers and loads mods
  → Fabricates MDBRunner MonoBehaviour in IL2CPP memory
  → Attaches to Unity player loop for Update/FixedUpdate/LateUpdate callbacks
  → Mods run on Unity's main thread via MDBRunner dispatch
```

See the [Architecture Guide]({{ '/guides/architecture' | relative_url }}) for a detailed explanation of each step, the [Proxy DLL Injection Guide]({{ '/guides/proxy-injection' | relative_url }}) for the version.dll proxy system, and the [Class Injection Guide]({{ '/guides/class-injection' | relative_url }}) for the MonoBehaviour fabrication system.

---

## Supported Platforms

- **Operating System:** Windows 10/11 (x64 only)
- **Unity Runtime:** IL2CPP (Unity 2021+, metadata v29+)
- **Graphics API:** DirectX 11 or DirectX 12 (for ImGui overlay)
- **.NET Target:** Framework 4.7.2 or higher (hosted via COM CLR)

---

## Example Mods

The framework includes four example mods demonstrating every major API:

| Example | Difficulty | Description |
|---------|-----------|-------------|
| [HelloWorld]({{ '/examples' | relative_url }}#helloworld) | 🟢 Simple | Lifecycle, Logger, basic ImGui |
| [UnityDebugInterceptor]({{ '/examples' | relative_url }}#unity-debug-interceptor) | 🟢 Simple | Declarative patching, hooking Debug.Log |
| [GameStats]({{ '/examples' | relative_url }}#gamestats) | 🟡 Medium | Advanced patching, IL2CPP Bridge |
| [MDB_Explorer_ImGui]({{ '/examples' | relative_url }}#mdb-explorer) | 🔴 Complex | Full IL2CPP reflection, scene traversal |

All examples target universal Unity types and work across any Unity IL2CPP game.

---

## Guides

In-depth technical guides covering MDB's internal architecture and design:

| Guide | Description |
|-------|-------------|
| [Architecture]({{ '/guides/architecture' | relative_url }}) | Full injection chain, initialization sequence, and component overview |
| [Proxy DLL Injection]({{ '/guides/proxy-injection' | relative_url }}) | How the version.dll proxy works — DLL search order, forwarding, loader lock safety |
| [Class Injection]({{ '/guides/class-injection' | relative_url }}) | MonoBehaviour fabrication — IL2CPP memory layouts, hooks, negative tokens |

---

## Get Started

Ready to start modding? Head to the [Getting Started]({{ '/getting-started' | relative_url }}) guide to:

1. Prepare your environment
2. Deploy MDB to a Unity game (proxy or direct injection)
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
