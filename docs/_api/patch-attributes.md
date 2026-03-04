---
layout: default
title: Patch Attributes API
---

# Patch Attributes API

The MDB Framework provides a declarative patching system using attributes. This is the **primary and recommended** way to hook game methods. Patches are automatically discovered and applied when mods load, giving you type-safe parameter handling without any manual hook management.

**Namespace:** `GameSDK.ModHost.Patching`

---

## Overview

The patching system consists of:

1. **[Patch]** - Declares the target type to patch
2. **[PatchMethod]** - Specifies the method name and parameter count
3. **[Prefix]** - Runs before the original method
4. **[Postfix]** - Runs after the original method
5. **[Finalizer]** - Runs even if the original throws an exception

Patch methods receive parameters by name. You can use special names like `__instance`, `__result`, `__state`, and `__exception`, or use the **actual IL2CPP parameter names** from the generated SDK to receive method arguments with full type safety.

---

## Attributes

### [Patch]

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public class PatchAttribute : Attribute
```

Marks a class as containing patches for a specific type. Can be used in three ways:

#### Constructor Overloads

**1. Patch by Type (Generated Wrapper)**

```csharp
[Patch(Type targetType)]
```

Uses a generated wrapper type reference. The framework extracts the original namespace and type name from the wrapper.

**Example:**
```csharp
[Patch(typeof(Player))]
[PatchMethod("TakeDamage", 1)]
public static class PlayerPatch
{
    [Prefix]
    public static bool Prefix(IntPtr __instance, int __0)
    {
        return true;
    }
}
```

**2. Patch by Namespace and Name**

```csharp
[Patch(string namespace, string typeName)]
```

Directly specifies the IL2CPP namespace and type name. Use this for types without generated wrappers, for obfuscated types, or for types in the global namespace.

**Example — Named namespace:**
```csharp
[Patch("UnityEngine", "Debug")]
[PatchMethod("Log", 1)]
public static class DebugLogPatch
{
    [Prefix]
    public static bool Prefix(string __0)
    {
        Logger.Info($"Unity logged: {__0}");
        return true;
    }
}
```

**Example — Global namespace (empty string):**
```csharp
// For types in the global namespace, use an empty string ""
[Patch("", "ClassName")]
[PatchMethod("MethodName", 1)]
public static class CustomPatch
{
    [Prefix]
    public static bool Prefix(IntPtr __instance)
    {
        Logger.Info("Method called");
        return true;
    }
}
```

**3. Specify Method Name Only**

```csharp
[Patch(string methodName)]
```

Use as a second `[Patch]` attribute to specify the method name without using `[PatchMethod]`.

**Example:**
```csharp
[Patch("UnityEngine", "Screen")]
[Patch("get_width")]  // Second [Patch] specifies method
public static class ScreenWidthPatch
{
    [Postfix]
    public static void Postfix(ref int __result)
    {
        __result = 1920;
    }
}
```

#### Properties

| Property | Type | Description | Default |
|----------|------|-------------|---------|
| `TargetType` | `Type` | The wrapper type to patch (read-only) | `null` |
| `Namespace` | `string` | IL2CPP namespace of target type (read-only) | `""` |
| `TypeName` | `string` | IL2CPP type name (read-only) | `""` |
| `MethodName` | `string` | Target method name (settable) | `null` |
| `Assembly` | `string` | Assembly containing the target type | `"Assembly-CSharp"` |

---

### [PatchMethod]

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class PatchMethodAttribute : Attribute
```

Specifies the target method name and the parameter count. The parameter count is important for overload resolution — IL2CPP games frequently have multiple methods with the same name but different parameter counts.

#### Constructors

```csharp
[PatchMethod(string methodName)]
[PatchMethod(string methodName, int parameterCount)]
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `methodName` | `string` | Name of the method to patch |
| `parameterCount` | `int` | Number of IL2CPP parameters (use -1 for any) |

**Always specify the parameter count** when you know it — this prevents accidentally hooking the wrong overload.

**Example:**
```csharp
[Patch("UnityEngine", "Debug")]
[PatchMethod("Log", 1)]  // Specifically targets Debug.Log(object)
public static class DebugLogPatch { }
```

---

### [Prefix]

```csharp
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public class PrefixAttribute : Attribute
```

Marks a method as a prefix patch. Prefix methods run **BEFORE** the original method.

#### Behavior

- Return `false` to skip executing the original method
- Return `true` (or `void`) to continue to the original method
- Can modify parameters passed to the original via `ref` parameters
- Can set the return value via `ref __result` when skipping the original

**Example — Log and continue:**
```csharp
[Patch("GameNamespace", "Player")]
[PatchMethod("TakeDamage", 1)]
public static class LogDamagePatch
{
    [Prefix]
    public static bool Prefix(IntPtr __instance, ref int __0)
    {
        Logger.Info($"Received {__0} damage");
        return true; // Continue to original = damage taken
    }
}
```

**Example — Skip original:**
```csharp
[Patch("GameNamespace", "Player")]
[PatchMethod("TakeDamage", 1)]
public static class InvincibilityPatch
{
    [Prefix]
    public static bool Prefix(IntPtr __instance, ref int __0)
    {
        Logger.Info($"Blocked {__0} damage");
        return false; // Skip original = no damage taken
    }
}
```

---

### [Postfix]

```csharp
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public class PostfixAttribute : Attribute
```

Marks a method as a postfix patch. Postfix methods run **AFTER** the original method completes successfully.

#### Behavior

- Always runs if the original method completes without throwing
- Can inspect or modify the return value via `ref __result`
- Can access the original parameters and instance
- Cannot skip the original (it already ran)

**Example — Modify return value:**
```csharp
[Patch("UnityEngine", "Screen")]
[PatchMethod("get_width", 0)]
public static class ForceResolutionPatch
{
    [Postfix]
    public static void Postfix(ref int __result)
    {
        __result = 1920; // Override screen width
    }
}
```

**Example — Log method results:**
```csharp
[Patch("GameNamespace", "SaveManager")]
[PatchMethod("SaveGame", 1)]
public static class SaveGameLogger
{
    [Postfix]
    public static void Postfix(string __0, bool __result)
    {
        if (__result)
            Logger.Info($"Game saved to slot: {__0}");
        else
            Logger.Warning($"Failed to save game to slot: {__0}");
    }
}
```

---

### [Finalizer]

```csharp
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public class FinalizerAttribute : Attribute
```

Marks a method as a finalizer patch. Finalizer methods run **EVEN IF** the original method throws an exception.

#### Behavior

- Always runs after the original method, regardless of success or failure
- Receives the exception via `__exception` parameter (null if no exception)
- Return `null` to swallow the exception
- Return an `Exception` instance to throw a different exception
- Return the original `__exception` to re-throw it

**Example — Catch and log exceptions:**
```csharp
[Patch("GameNamespace", "FileManager")]
[PatchMethod("LoadFile", 1)]
public static class LoadFileSafety
{
    [Finalizer]
    public static Exception Finalizer(string __0, Exception __exception)
    {
        if (__exception != null)
        {
            Logger.Error($"Failed to load file {__0}: {__exception.Message}");
            return null; // Swallow exception - game continues
        }
        return null;
    }
}
```

---

## Parameter Naming

Patch methods support two approaches for receiving method parameters:

### Approach 1: Named Parameters (Recommended)

Use the **actual IL2CPP parameter names** from the generated SDK or dump files. The framework maps them by position automatically. This is the most readable approach for obfuscated games where the parameter names come directly from the game's metadata.

```csharp
[Patch("", "HBEAKBIHANL")]
[PatchMethod("KOBMINBDOBD", 12)]
public static class BulletSpawnPatch
{
    [Prefix]
    public static bool Prefix(
        IntPtr __instance,
        ref ObjectProperties ODEMIJKAJMH,    // Positional arg 0
        ref ProjectileProperties GIEJOHKLGJO, // Positional arg 1
        ref int HHCCBONIIOM,                  // Positional arg 2
        ref uint KLHOFENGJNM,                // Positional arg 3
        ref float FFFFKPDHEFP,               // Positional arg 4
        ref int GHEBEMMJLDJ,                 // Positional arg 5
        ref string CFJBHEKKLNF,              // Positional arg 6
        ref string JCADLABDPIO,              // Positional arg 7
        ref float AFCNMCJIKFD,               // Positional arg 8
        ref float KDAJOMOFMJB,               // Positional arg 9
        ref bool KCHJBMCNIIA,                // Positional arg 10
        ref bool PBGHBKMHACI)                // Positional arg 11
    {
        Logger.Info($"Damage={HHCCBONIIOM}, Count={KLHOFENGJNM}");
        return true;
    }
}
```

Non-special parameter names (anything that doesn't start with `__`) are mapped **positionally** — the first non-special parameter maps to arg 0, the second to arg 1, etc.

### Approach 2: Indexed Parameters

Use `__0`, `__1`, `__2`, etc. to reference parameters by zero-based index:

```csharp
[Patch("GameNamespace", "Calculator")]
[PatchMethod("Add", 2)]
public static class CalculatorPatch
{
    [Prefix]
    public static bool Prefix(int __0, int __1, ref int __result)
    {
        Logger.Info($"Adding {__0} + {__1}");
        __result = __0 + __1 + 10; // Add bonus
        return false; // Skip original
    }
}
```

### Special Parameters

These parameter names have special meaning and are not mapped positionally:

| Name | Type | Description |
|------|------|-------------|
| `__instance` | `IntPtr` | The `this` pointer (IL2CPP object). `IntPtr.Zero` for static methods. |
| `__result` | `ref T` | The method return value. Must use `ref`. |
| `__state` | `ref object` | Shared state between Prefix and Postfix. |
| `__exception` | `Exception` | The thrown exception (Finalizer only). |
| `__0`, `__1`, ... | `T` | Parameter by zero-based index. |

---

## Supported Parameter Types

The patching system automatically marshals between IL2CPP native pointers and managed types:

| C# Type | IL2CPP Representation | Notes |
|----------|-----------------------|-------|
| `int` | Passed directly in pointer | |
| `uint` | Passed directly in pointer | |
| `long` | Passed directly in pointer | |
| `ulong` | Passed directly in pointer | |
| `short` | Passed directly in pointer | |
| `ushort` | Passed directly in pointer | |
| `byte` | Passed directly in pointer | |
| `sbyte` | Passed directly in pointer | |
| `bool` | Non-zero = true | |
| `float` | Bit-cast from int32 | Handled automatically |
| `double` | Bit-cast from int64 | Handled automatically |
| `string` | IL2CPP String pointer | Auto-converted to/from managed string |
| `IntPtr` | Raw pointer passthrough | Use for object references |
| IL2CPP wrapper types | Pointer wrapped via `Activator.CreateInstance` | e.g., `ObjectProperties`, `PlayerClass` |

### Using `ref` Parameters

All non-special parameters can be declared with `ref` to allow modification. When you modify a `ref` parameter, the change is written back to the native args array, affecting the original method call:

```csharp
[Prefix]
public static bool Prefix(ref int HHCCBONIIOM, ref float FFFFKPDHEFP)
{
    HHCCBONIIOM = 0;     // Zero out damage
    FFFFKPDHEFP *= 0.5f; // Halve the angle
    return true;         // Continue with modified args
}
```

---

## Complete Examples

### Example 1: Intercepting Game Methods with Named Parameters

A real-world example hooking an obfuscated method in a Unity IL2CPP game. The method has 12 parameters including structs, primitives, strings, and booleans.

```csharp
using System;
using GameSDK.ModHost;
using GameSDK.ModHost.Patching;

[Mod("Author.BulletMonitor", "Bullet Monitor", "1.0.0")]
public class BulletMonitorMod : ModBase
{
    public static ModLogger Log;

    public override void OnLoad()
    {
        Log = Logger;
        Logger.Info("Bullet monitor loaded");
    }
}

### Example 2: Skip Original with Return Value

```csharp
[Patch("GameNamespace", "Player")]
[PatchMethod("TakeDamage", 2)]
public static class GodModePatch
{
    private static bool _godMode = true;

    [Prefix]
    public static bool Prefix(IntPtr __instance, float __0, string __1, ref bool __result)
    {
        if (_godMode)
        {
            Logger.Info($"God mode: blocked {__0} damage from {__1}");
            __result = false;
            return false; // Skip original
        }
        return true;
    }
}
```

### Example 3: Modify Return Values

```csharp
[Patch("GameNamespace", "PlayerStats")]
[PatchMethod("GetMaxHealth", 0)]
public static class HealthBoostPatch
{
    [Postfix]
    public static void Postfix(IntPtr __instance, ref float __result)
    {
        __result *= 1.5f; // 50% health boost
    }
}
```

### Example 4: Track Method Duration with __state

```csharp
[Patch("GameNamespace", "AssetLoader")]
[PatchMethod("LoadAsset", 1)]
public static class LoadAssetTimerPatch
{
    [Prefix]
    public static void Prefix(string __0, ref object __state)
    {
        Logger.Info($"Loading asset: {__0}");
        __state = System.Diagnostics.Stopwatch.StartNew();
    }

    [Postfix]
    public static void Postfix(string __0, ref object __state, IntPtr __result)
    {
        if (__state is System.Diagnostics.Stopwatch sw)
        {
            sw.Stop();
            bool success = __result != IntPtr.Zero;
            Logger.Info($"Loaded {__0} in {sw.ElapsedMilliseconds}ms (success: {success})");
        }
    }
}
```

### Example 5: Exception Handling with Finalizer

```csharp
[Patch("GameNamespace", "NetworkManager")]
[PatchMethod("SendPacket", 1)]
public static class NetworkSafetyPatch
{
    [Finalizer]
    public static Exception Finalizer(IntPtr __0, Exception __exception)
    {
        if (__exception != null)
        {
            Logger.Error($"Failed to send packet: {__exception.Message}");

            if (__exception is System.Net.Sockets.SocketException)
            {
                Logger.Warning("Network error — swallowing exception");
                return null; // Swallow
            }

            return __exception; // Re-throw other exceptions
        }
        return null;
    }
}
```

### Example 6: Multiple Patches on Same Type

```csharp
[Patch("UnityEngine", "Debug")]
[PatchMethod("Log", 1)]
public static class DebugLogPatch
{
    [Prefix]
    public static bool Prefix(string __0)
    {
        Logger.Info($"[Log] {__0}");
        return true;
    }
}

[Patch("UnityEngine", "Debug")]
[PatchMethod("LogWarning", 1)]
public static class DebugLogWarningPatch
{
    [Prefix]
    public static bool Prefix(string __0)
    {
        Logger.Warning($"[Warning] {__0}");
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
        Logger.Error($"[Error] {__0}");
        return true;
    }
}
```

### Example 7: Combining Prefix, Postfix, and Finalizer

Execution order: Prefix → Original → Postfix → Finalizer (always)

```csharp
[Patch("GameNamespace", "CriticalOperation")]
[PatchMethod("Execute", 1)]
public static class CompletePatch
{
    [Prefix]
    public static void Prefix(string __0, ref object __state)
    {
        Logger.Info($"Starting operation: {__0}");
        __state = System.Diagnostics.Stopwatch.StartNew();
    }

    [Postfix]
    public static void Postfix(ref object __state, bool __result)
    {
        if (__state is System.Diagnostics.Stopwatch sw)
        {
            sw.Stop();
            Logger.Info($"Operation completed: {__result} ({sw.ElapsedMilliseconds}ms)");
        }
    }

    [Finalizer]
    public static Exception Finalizer(ref object __state, Exception __exception)
    {
        if (__exception != null && __state is System.Diagnostics.Stopwatch sw)
        {
            sw.Stop();
            Logger.Error($"Operation failed after {sw.ElapsedMilliseconds}ms: {__exception.Message}");
        }
        return __exception;
    }
}
```

### Example 8: Hooking Properties

Unity properties compile to `get_PropertyName` and `set_PropertyName` methods:

```csharp
[Patch("UnityEngine", "Screen")]
[PatchMethod("get_width", 0)]
public static class ScreenWidthPatch
{
    [Postfix]
    public static void Postfix(ref int __result)
    {
        __result = 1920; // Force screen width
    }
}

[Patch("GameNamespace", "Settings")]
[PatchMethod("set_Volume", 1)]
public static class VolumeSetterPatch
{
    [Prefix]
    public static void Prefix(ref float __0)
    {
        __0 = Math.Max(0f, Math.Min(__0, 1f)); // Clamp to safe range
    }
}
```

---

## Best Practices

### Do

- **Use static classes and static methods** — patches must be fully static
- **Always specify the parameter count** in `[PatchMethod]` for reliable overload resolution
- **Use named parameters** from the generated SDK for readability
- **Use `ref` on `__result`** — without it, modifications are lost
- **Set `__result` before returning `false`** — the caller expects a return value
- **Wrap expensive logic in try/catch** — exceptions in patches can crash the game
- **Throttle logging** in frequently-called methods to avoid log spam
- **Use `[Patch("", "TypeName")]`** for global namespace types (empty string, not omitted)

### Don't

- **Don't use instance classes or methods** — the framework can't instantiate patch classes
- **Don't skip the original without setting `__result`** — caller gets undefined values
- **Don't assume parameter types** — check the generated SDK or dump for exact types
- **Don't patch the same method twice in the same mod** — undefined behavior
- **Don't log in hot-path methods** without throttling — you'll flood the log

---

## Common Mistakes

### Mistake 1: Non-Static Patch Class

```csharp
// WRONG — Instance class
[Patch("Namespace", "Type")]
public class MyPatch
{
    [Prefix]
    public static bool Prefix() { return true; }
}
```

```csharp
// CORRECT — Static class
[Patch("Namespace", "Type")]
public static class MyPatch
{
    [Prefix]
    public static bool Prefix() { return true; }
}
```

### Mistake 2: Skipping Original Without Setting __result

```csharp
// WRONG — Caller gets garbage return value
[Prefix]
public static bool Prefix()
{
    return false;
}
```

```csharp
// CORRECT — Provide the return value
[Prefix]
public static bool Prefix(ref int __result)
{
    __result = 42;
    return false;
}
```

### Mistake 3: Forgetting `ref` on __result

```csharp
// WRONG — Modifies local copy only
[Postfix]
public static void Postfix(int __result)
{
    __result *= 2; // Does nothing
}
```

```csharp
// CORRECT
[Postfix]
public static void Postfix(ref int __result)
{
    __result *= 2; // Actually modifies the return value
}
```

### Mistake 4: Logging in Hot Paths

```csharp
// WRONG — Logs every frame
[Patch("UnityEngine", "Time")]
[PatchMethod("get_deltaTime", 0)]
public static class BadPatch
{
    [Postfix]
    public static void Postfix(float __result)
    {
        Logger.Info($"deltaTime: {__result}"); // Spams logs!
    }
}
```

```csharp
// CORRECT — Throttled logging
[Patch("UnityEngine", "Time")]
[PatchMethod("get_deltaTime", 0)]
public static class GoodPatch
{
    private static int _callCount = 0;

    [Postfix]
    public static void Postfix(float __result)
    {
        if (++_callCount % 60 == 0) // Log once per second at 60fps
            Logger.Info($"deltaTime: {__result}");
    }
}
```

---

## Troubleshooting

### Patch Not Applied

**Problem:** Your patch class is defined but the method isn't being hooked.

**Solutions:**
1. Ensure the patch class is **static**
2. Ensure patch methods are **static**
3. Check that namespace and type name exactly match the IL2CPP names
4. Verify method name and parameter count are correct
5. Check the logs — patch discovery errors are logged to `MDB/Logs/MDB.log`
6. For global namespace types, use `[Patch("", "TypeName")]` (empty string, not null)

### Parameters Show as Zero/Default

**Problem:** Patch applies but parameters are always 0/null/false.

**Solutions:**
1. Make sure your parameter names don't accidentally match a special name (`__instance`, `__result`, etc.)
2. Verify the parameter count in `[PatchMethod]` matches exactly
3. Check that parameter types match the IL2CPP types (see type mapping table)
4. For unsigned types, use `uint`/`ulong`/`ushort`/`byte` — not `int`

### Strings Show as Empty or `<object>`

**Problem:** String parameters appear empty or as pointer addresses.

**Solutions:**
1. Declare string parameters as `ref string` — IL2CPP strings are pointers
2. Verify the method actually passes non-empty strings (some parameters are genuinely empty)
3. Check the logs for string conversion errors

### Prefix Not Skipping Original

**Problem:** Returning `false` from Prefix but original still runs.

**Solutions:**
1. Ensure the return type is `bool`, not `void`
2. Make sure the method signature is correct
3. Check logs for patch application errors

### Game Crashes on Hook

**Problem:** Game crashes when the hooked method is called.

**Solutions:**
1. Check that parameter types match exactly
2. Wrap your patch logic in try/catch
3. Verify the parameter count matches the actual IL2CPP method
4. Try a minimal Prefix that just returns `true` to isolate the issue

---

## See Also

- [HookManager]({{ '/api/hookmanager' | relative_url }}) — Manual hook API (advanced fallback)
- [ModBase]({{ '/api/modbase' | relative_url }}) — Mod lifecycle and base class
- [IL2CPP Bridge]({{ '/api/il2cpp-bridge' | relative_url }}) — Direct IL2CPP runtime access
- [Examples]({{ '/examples' | relative_url }}) — Working mod examples
- [Getting Started]({{ '/getting-started' | relative_url }}) — Creating your first mod

---

[← Back to API Index]({{ '/api' | relative_url }}) | [HookManager →]({{ '/api/hookmanager' | relative_url }})
