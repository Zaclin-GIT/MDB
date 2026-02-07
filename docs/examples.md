---
layout: default
title: Examples
---

# Example Mods

Working example mods covering every major MDB API. Each targets universal Unity types and works across any IL2CPP game.

---

| Example | Difficulty | Key APIs |
|---------|-----------|----------|
| [HelloWorld](#helloworld) | 🟢 Simple | ModBase, Logger, ImGui widgets |
| [UnityDebugInterceptor](#unity-debug-interceptor) | 🟢 Simple | Declarative patching |
| [GameStats](#gamestats) | 🟡 Medium | Patching, IL2CPP Bridge, HookManager |
| [MDB Explorer](#mdb-explorer) | 🔴 Complex | Full IL2CPP reflection, scene traversal |

All examples build with `dotnet build -c Release` and deploy to `<GameDir>/MDB/Mods/`.

---

<a name="helloworld"></a>
## 🟢 HelloWorld

A minimal mod demonstrating the core building blocks.

**What you'll learn:**
- `[Mod]` attribute and `ModBase` lifecycle (`OnLoad`, `OnUpdate`)
- `Logger` output (Info, Warning, Error, Debug)
- ImGui window creation and basic widgets

```csharp
[Mod("Examples.HelloWorld", "Hello World", "1.0.0",
    Author = "MDB Framework",
    Description = "A simple example mod showing basic framework usage.")]
public class HelloWorldMod : ModBase
{
    private bool _windowOpen = true;
    private int _clickCount;
    private string _userName = "Modder";

    public override void OnLoad()
    {
        Logger.Info("Hello World mod loaded!");
        ImGuiManager.RegisterCallback("HelloWorld", DrawImGui,
            ImGuiCallbackPriority.Normal);
    }

    private void DrawImGui()
    {
        if (ImGui.Begin("Hello World Mod", ref _windowOpen))
        {
            ImGui.Text("Hello from MDB!");
            ImGui.InputText("Name", ref _userName, 64);
            if (ImGui.Button("Click Me!"))
                Logger.Info($"Clicked {++_clickCount} times!");
        }
        ImGui.End();
    }
}
```

📁 [View source on GitHub](https://github.com/Zaclin-GIT/MDB/tree/main/Documentation/Examples/HelloWorld)

---

<a name="unity-debug-interceptor"></a>
## 🟢 Unity Debug Interceptor

Intercepts Unity's `Debug.Log`, `Debug.LogWarning`, and `Debug.LogError` and redirects them to the MDB console with color coding.

**What you'll learn:**
- Declarative patching with `[Patch]`, `[PatchMethod]`, `[Prefix]`
- Targeting Unity Engine classes by namespace + name
- Prefix hooks that allow the original method to continue

```csharp
[Patch("UnityEngine", "Debug")]
[PatchMethod("Log", 1)]
public static class DebugLogPatch
{
    [Prefix]
    public static bool Prefix(string __0)
    {
        ModLogger.LogInternal("Unity", __0 ?? "<null>", ConsoleColor.Gray);
        return true; // Continue to original
    }
}

[Patch("UnityEngine", "Debug")]
[PatchMethod("LogWarning", 1)]
public static class DebugLogWarningPatch
{
    [Prefix]
    public static bool Prefix(string __0)
    {
        ModLogger.LogInternal("Unity.Warn", __0 ?? "<null>", ConsoleColor.Yellow);
        return true;
    }
}

[Patch("UnityEngine", "Debug")]
[PatchMethod("LogError", 1)]
public static class DebugLogErrorPatch
{
    [Prefix]
    public static bool Prefix(string __0)
    {
        ModLogger.LogInternal("Unity.Error", __0 ?? "<null>", ConsoleColor.Red);
        return true;
    }
}
```

📁 [View source on GitHub](https://github.com/Zaclin-GIT/MDB/tree/main/Documentation/Examples/UnityDebugInterceptor)

---

<a name="gamestats"></a>
## 🟡 GameStats Dashboard

A medium-complexity mod that tracks game statistics and demonstrates the full patching + IL2CPP Bridge + ImGui toolkit.

**What you'll learn:**
- All patch types: `[Prefix]`, `[Postfix]`, `[Finalizer]`
- Targeting by string, by wrapper type, and by RVA (`[PatchRva]`)
- Special parameters: `__instance`, `__0`/`__1`, `ref __result`, `__exception`
- `HookManager` for manual native hooks with enable/disable
- IL2CPP Bridge: find classes, invoke methods, read fields
- Advanced ImGui: tabs, trees, tooltips, popups, menus, draw list overlay

### Patching APIs Covered

```csharp
// Target by class name
[Patch("UnityEngine", "Time")]
[PatchMethod("get_deltaTime", 0)]
public static class TimePatch
{
    [Postfix]
    public static void Postfix(ref IntPtr __result) { /* modify return */ }
}

// Target by RVA (for obfuscated methods)
[Patch("UnityEngine", "Application")]
[PatchRva(0xDEAD)]
public static class ApplicationPatch
{
    [Finalizer]
    public static void Finalizer(Exception __exception) { /* catch errors */ }
}
```

### Manual Hooking

```csharp
IntPtr methodPtr = Il2CppBridge.mdb_get_method_pointer(method);
HookManager.CreateHook(methodPtr, detour, out original);
HookManager.SetHookEnabled(handle, true);
```

### IL2CPP Bridge

```csharp
IntPtr klass = Il2CppBridge.mdb_find_class("Assembly-CSharp", "", "Player");
IntPtr method = Il2CppBridge.mdb_get_method(klass, "GetHealth", 0);
IntPtr result = Il2CppBridge.mdb_invoke_method(method, instance, null, out ex);
```

📁 [View source on GitHub](https://github.com/Zaclin-GIT/MDB/tree/main/Documentation/Examples/GameStats)

---

<a name="mdb-explorer"></a>
## 🔴 MDB Explorer (ImGui)

A full-featured runtime Unity scene explorer. Browse GameObjects, inspect Components, and map obfuscated names — all through a Dear ImGui interface.

**What you'll learn:**
- Runtime IL2CPP reflection (enumerate classes, fields, methods, properties)
- Scene traversal via IL2CPP Bridge
- Complex ImGui application architecture (multiple panels, drag-to-resize)
- Deobfuscation system integration

### Features

- **Scene Hierarchy** — Tree view of all loaded scenes and GameObjects
- **Inspector Panel** — View/edit fields, read properties, browse methods
- **Drill-through Navigation** — Click object references to navigate deeper
- **Deobfuscation Tool** — Map obfuscated names to friendly names in real-time

### Architecture

| File | Purpose |
|------|---------|
| `ExplorerMod.cs` | Main mod entry, panel layout, menu bar |
| `SceneHierarchy.cs` | Scene tree traversal via IL2CPP |
| `GameObjectInspector.cs` | Component/field/property reflection |
| `ComponentReflector.cs` | IL2CPP type introspection helpers |
| `DeobfuscationPanel.cs` | Name mapping UI |
| `DeobfuscationHelper.cs` | Mapping database integration |
| `ImGuiBindings.cs` | ImGui helper extensions |

📁 [View source on GitHub](https://github.com/Zaclin-GIT/MDB/tree/main/Documentation/Examples/MDB_Explorer_ImGui)

---

## Building Any Example

```bash
cd Documentation/Examples/<ExampleName>
dotnet build -c Release
```

Copy the output DLL from `bin/Release/` to `<GameDir>/MDB/Mods/`.

---

[← Back to Home]({{ '/' | relative_url }}) | [API Reference →]({{ '/api' | relative_url }})
