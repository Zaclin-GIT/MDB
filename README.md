# MDB Framework

**Runtime IL2CPP modding framework for Unity games.** Dumps metadata, generates C# wrappers, builds an SDK, and loads mods — all automatically from a single DLL injection.

Encrypted `global-metadata.dat`? Don't care. 

> [!WARNING]
> Early development. Expect breaking changes. x64 only. See [Disclaimer](#disclaimer).

<a href="https://buymeacoffee.com/winnforge">
  <img src="https://img.shields.io/badge/Buy%20Me%20A%20Coffee-%23FFDD00?style=for-the-badge&logo=buymeacoffee&logoColor=black" alt="Buy Me A Coffee">
</a>

---

## How It Works

Inject `MDB_Bridge.dll` into a running Unity IL2CPP game. The framework handles everything else:

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

Subsequent launches skip the dump & build if nothing has changed.

---

## Deployment

```
<GameFolder>/
├── MDB_Bridge.dll                    # Inject this
├── MDB/
│   ├── Logs/                         # MDB.log, Mods.log (auto-created)
│   ├── Managed/
│   │   └── GameSDK.ModHost.dll       # Auto-built SDK + wrappers
│   └── Mods/
│       └── YourMod.dll               # Drop mods here
└── MDB_Core/
    ├── MDB_Core.csproj               # Required for auto-build
    ├── Core/                          # Runtime, Bridge, Types, ImGui
    ├── Deobfuscation/                 # Name mapping support
    ├── ModHost/                       # Mod loading + patching
    └── Generated/                     # Auto-generated wrappers
```

---

## Creating a Mod

Create a .NET Framework 4.7.2+ class library and add a reference to `GameSDK.ModHost.dll` — this is the SDK that the framework auto-generates and compiles on first launch. You'll find it at `<GameFolder>/MDB/Managed/GameSDK.ModHost.dll` after the first successful injection. It contains all the generated wrappers, the mod host API, patching system, and ImGui bindings.

```csharp
using GameSDK.ModHost;

namespace MyMod
{
    [Mod("AuthorName.MyMod", "My Mod", "1.0.0", Author = "You")]
    public class MyMod : ModBase
    {
        public override void OnLoad()
        {
            Logger.Info("Mod loaded successfully!");
            Logger.Warning("This is a warning");
            Logger.Error("This is an error");
        }

        public override void OnUpdate() { }       // Every frame
        public override void OnFixedUpdate() { }   // Physics tick
        public override void OnLateUpdate() { }    // After all updates
        public override void OnGUI() { }           // ImGui rendering
    }
}
```

Each mod gets its own `Logger` instance (inherited from `ModBase`) that writes to `MDB/Logs/Mods.log` with color-coded console output — blue for info, yellow for warnings, red for errors.

**Deploy:** Build your mod DLL and copy it to `<GameFolder>/MDB/Mods/`. The framework scans this folder on startup and loads every `.dll` it finds.

---

## Method Hooking

HarmonyX-style attribute patching. Hooks are auto-discovered and applied on mod load.

```csharp
[Patch("SomeNamespace", "Player")]
[PatchMethod("TakeDamage", 1)]
public static class PlayerPatch
{
    [Prefix]
    public static bool Prefix(IntPtr __instance, int __0)
    {
        // Return false to skip original
        return true;
    }

    [Postfix]
    public static void Postfix(IntPtr __instance) { }
}
```

### Targeting

```csharp
[Patch("Namespace", "ClassName")]     // By name
[Patch(typeof(GeneratedWrapper))]     // By type
[PatchMethod("MethodName", 2)]        // By name + param count
[PatchRva(0x1A3B5C0)]                 // By RVA (obfuscated methods)
```

### Hook Types

| Attribute | Behavior |
|-----------|----------|
| `[Prefix]` | Runs before original. Return `false` to skip it. |
| `[Postfix]` | Runs after original. |
| `[Finalizer]` | Runs even if original throws. |

### Special Parameters

| Parameter | Description |
|-----------|-------------|
| `IntPtr __instance` | Object instance (`IntPtr.Zero` for static) |
| `__0`, `__1`, ... | Method parameters by index |
| `ref __result` | Return value (modifiable) |
| `__state` | Shared state between prefix and postfix |
| `Exception __exception` | Exception (finalizer only) |

The patching system is now float-aware — it analyzes IL2CPP parameter types and selects specialized detour delegates matching the exact x64 calling convention.

---

## ImGui Overlay

Built-in Dear ImGui integration with DX11/DX12 auto-detection. Toggle input capture with F2.

```csharp
using GameSDK.ModHost.ImGui;

[Mod("MyMod.UI", "UI Demo", "1.0.0")]
public class UIMod : ModBase
{
    public override void OnLoad()
    {
        ImGuiManager.RegisterCallback(Draw, "My Window", ImGuiCallbackPriority.Normal);
    }

    private void Draw()
    {
        if (ImGui.Begin("My Window"))
        {
            ImGui.Text("Hello!");
            if (ImGui.Button("Click Me"))
                Logger.Info("Clicked!");
        }
        ImGui.End();
    }
}
```

---

## Generic Type Resolution

The dumper walks `Il2CppGenericInst` structs at runtime to resolve actual generic type arguments instead of erasing everything to `object`:

- **System types** resolve fully: `List<string>`, `Dictionary<string, int>`, `Action<bool, float>`
- **Game types** fall back to their plain class name (wrappers don't emit generic parameters)
- **Unavailable types** (`Span<T>`, `ReadOnlySpan<T>`, `Memory<T>`, `CallSite<T>`) are safely erased to `object`

---

## Obfuscated Names

Unicode and obfuscated class/method names are handled automatically:

- Sanitized to `unicode_class_1`, `unicode_method_2`, etc.
- Methods use RVA-based calling — name doesn't matter
- Original IL2CPP names preserved for runtime resolution

---

## Known Limitations

- **x64 only** — no 32-bit game support
- **DirectX only** — ImGui overlay requires DX11 or DX12
- **No hot reload** — restart the game to reload mods
- **Anti-cheat** — protected games will likely block injection
- **Game-type generics** — game classes aren't emitted with generic parameters, so `GameClass<T>` resolves to `GameClass`

---

## Building

**MDB_Bridge** (C++17, MSVC v143):
```powershell
msbuild MDB_Bridge.vcxproj /p:Configuration=Release /p:Platform=x64
```

**MDB_Core** (.NET Framework 4.7.2/4.8.1):
```powershell
dotnet build MDB_Core.csproj -c Release
```
Normally built automatically by the bridge at runtime via MSBuild.

---

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