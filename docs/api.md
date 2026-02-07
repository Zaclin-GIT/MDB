---
layout: default
title: API Reference
---

# API Reference

Complete reference documentation for all MDB Framework APIs.

---

## Core APIs

### Mod System

- **[ModBase]({{ '/api/modbase' | relative_url }})** - Base class for all mods with lifecycle callbacks
- **[ModAttribute]({{ '/api/modattribute' | relative_url }})** - Metadata attribute for declaring mods
- **[ModLogger]({{ '/api/logger' | relative_url }})** - Logging system with color-coded console output

### Patching System

- **[Patch Attributes]({{ '/api/patch-attributes' | relative_url }})** - Declarative method hooking (Harmony-style)
  - `[Patch]` - Target a class for patching
  - `[PatchMethod]` - Target a specific method
  - `[PatchRva]` - Target by RVA (for obfuscated methods)
  - `[Prefix]` - Run before original method
  - `[Postfix]` - Run after original method
  - `[Finalizer]` - Run even if original throws
- **[HookManager]({{ '/api/hookmanager' | relative_url }})** - Manual hooking API for runtime method hooks

### IL2CPP Bridge

- **[Il2CppBridge]({{ '/api/il2cpp-bridge' | relative_url }})** - P/Invoke declarations for IL2CPP runtime access
  - Class resolution
  - Method invocation
  - Field access
  - Type system queries
  - String marshaling
  - Array helpers
  - Unity-specific helpers

### ImGui Integration

- **[ImGuiManager]({{ '/api/imgui-manager' | relative_url }})** - Callback registration and window management
- **[ImGui]({{ '/api/imgui' | relative_url }})** - Dear ImGui API bindings
  - Windows and layouts
  - Widgets (buttons, inputs, sliders, etc.)
  - Trees and lists
  - Menus and popups
  - Drawing primitives
  - Styling and theming

---

## API Categories

### By Difficulty

#### 🟢 Beginner-Friendly
- [ModBase]({{ '/api/modbase' | relative_url }}) - Simple lifecycle and logging
- [ModLogger]({{ '/api/logger' | relative_url }}) - Easy logging
- [ImGuiManager]({{ '/api/imgui-manager' | relative_url }}) - Basic UI registration

#### 🟡 Intermediate
- [Patch Attributes]({{ '/api/patch-attributes' | relative_url }}) - Declarative hooks
- [ImGui]({{ '/api/imgui' | relative_url }}) - UI construction

#### 🔴 Advanced
- [HookManager]({{ '/api/hookmanager' | relative_url }}) - Manual hooking
- [Il2CppBridge]({{ '/api/il2cpp-bridge' | relative_url }}) - Direct IL2CPP access

---

## Quick Reference

### Mod Lifecycle

```csharp
[Mod("Author.ModName", "Display Name", "1.0.0")]
public class MyMod : ModBase
{
    public override void OnLoad() { }           // Called once on load
    public override void OnUpdate() { }         // Every frame
    public override void OnFixedUpdate() { }    // Physics tick
    public override void OnLateUpdate() { }     // After all updates
    public override void OnGUI() { }            // ImGui rendering
}
```

### Logging

```csharp
Logger.Info("Info message");
Logger.Warning("Warning message");
Logger.Error("Error message");
Logger.Debug("Debug message");
```

### Patching

```csharp
[Patch("Namespace", "ClassName")]
[PatchMethod("MethodName", 2)]  // 2 parameters
public static class MyPatch
{
    [Prefix]
    public static bool Prefix(IntPtr __instance, int __0, float __1)
    {
        // Return false to skip original
        return true;
    }
    
    [Postfix]
    public static void Postfix(ref int __result)
    {
        // Modify return value
    }
}
```

### ImGui UI

```csharp
public override void OnLoad()
{
    ImGuiManager.RegisterCallback(DrawUI, "My Window");
}

private void DrawUI()
{
    if (ImGui.Begin("My Window"))
    {
        ImGui.Text("Hello!");
        if (ImGui.Button("Click"))
            Logger.Info("Clicked!");
    }
    ImGui.End();
}
```

### IL2CPP Bridge

```csharp
// Find a class
IntPtr klass = Il2CppBridge.mdb_find_class("Assembly-CSharp", "Game", "Player");

// Get a method
IntPtr method = Il2CppBridge.mdb_get_method(klass, "TakeDamage", 1);

// Get a field value
IntPtr field = Il2CppBridge.mdb_get_field(klass, "health");
float health = Il2CppBridge.mdb_field_get_value<float>(instance, field);
```

---

## Navigation

- [ModBase - Mod Lifecycle]({{ '/api/modbase' | relative_url }})
- [ModAttribute - Mod Metadata]({{ '/api/modattribute' | relative_url }})
- [ModLogger - Logging System]({{ '/api/logger' | relative_url }})
- [Patch Attributes - Declarative Hooks]({{ '/api/patch-attributes' | relative_url }})
- [HookManager - Manual Hooks]({{ '/api/hookmanager' | relative_url }})
- [Il2CppBridge - IL2CPP Runtime Access]({{ '/api/il2cpp-bridge' | relative_url }})
- [ImGuiManager - UI Management]({{ '/api/imgui-manager' | relative_url }})
- [ImGui - UI Construction]({{ '/api/imgui' | relative_url }})

---

[← Back to Home]({{ '/' | relative_url }}) | [Getting Started →]({{ '/getting-started' | relative_url }})
