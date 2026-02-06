---
layout: default
title: Patch Attributes API
---

# Patch Attributes API

The MDB Framework provides a HarmonyX-compatible declarative patching system using attributes. Patches are automatically discovered and applied when mods load, allowing you to hook into game methods without manual hook management.

**Namespace:** `GameSDK.ModHost.Patching`

---

## Overview

The patching system consists of:

1. **[Patch]** - Declares the target type to patch
2. **[PatchMethod]** - Specifies the method name and parameter count
3. **[PatchRva]** - Targets methods by RVA offset (for obfuscated code)
4. **[Prefix]** - Runs before the original method
5. **[Postfix]** - Runs after the original method
6. **[Finalizer]** - Runs even if the original throws an exception

All patches use special parameters like `__instance`, `__0`/`__1`, `ref __result`, `__state`, and `__exception` to interact with the original method's execution.

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
        // __0 is the damage amount parameter
        return true;
    }
}
```

**2. Patch by Namespace and Name**

```csharp
[Patch(string namespace, string typeName)]
```

Directly specifies the IL2CPP namespace and type name. Use this for types without generated wrappers or for obfuscated types.

**Example:**
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
        __result = 1920; // Override screen width
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

Specifies the target method name and optionally the parameter count for overload resolution.

#### Constructors

```csharp
[PatchMethod(string methodName)]
[PatchMethod(string methodName, int parameterCount)]
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `methodName` | `string` | Name of the method to patch |
| `parameterCount` | `int` | Number of parameters (use -1 for any) |

**Example - Simple Method:**
```csharp
[Patch("MyNamespace", "MyClass")]
[PatchMethod("DoSomething")]  // No parameter count = any overload
public static class MyPatch { }
```

**Example - Overload Resolution:**
```csharp
[Patch("UnityEngine", "Debug")]
[PatchMethod("Log", 1)]  // Specifically targets Debug.Log(object)
public static class DebugLogPatch { }
```

---

### [PatchRva]

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class PatchRvaAttribute : Attribute
```

Targets a method by its RVA (Relative Virtual Address) offset. Use this for heavily obfuscated methods that can't be found by name.

#### Constructor

```csharp
[PatchRva(ulong rva)]
```

| Parameter | Type | Description |
|-----------|------|-------------|
| `rva` | `ulong` | The RVA offset from the IL2CPP dump |

**Example:**
```csharp
[Patch("ObfuscatedNamespace", "ObfuscatedClass")]
[PatchRva(0x1A3B5C0)]  // RVA from IL2CPP dump
public static class ObfuscatedMethodPatch
{
    [Prefix]
    public static bool Prefix(IntPtr __instance)
    {
        Logger.Info("Hooked obfuscated method by RVA");
        return true;
    }
}
```

**Finding RVAs:**
RVA offsets are found in the IL2CPP metadata dump that MDB generates on first run. Look in `MDB_Core/Generated/` for dump files.

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

**Example - Skip Original:**
```csharp
[Patch("GameNamespace", "Player")]
[PatchMethod("TakeDamage", 1)]
public static class InvincibilityPatch
{
    [Prefix]
    public static bool Prefix(IntPtr __instance, ref int __0)
    {
        // __0 is the damage parameter
        Logger.Info($"Player would take {__0} damage - blocking!");
        return false; // Skip original = no damage taken
    }
}
```

**Example - Modify Return Value:**
```csharp
[Patch("GameNamespace", "Inventory")]
[PatchMethod("HasItem", 1)]
public static class UnlockAllItemsPatch
{
    [Prefix]
    public static bool Prefix(string __0, ref bool __result)
    {
        // Always return true for HasItem checks
        __result = true;
        return false; // Skip original
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

**Example - Modify Return Value:**
```csharp
[Patch("UnityEngine", "Screen")]
[PatchMethod("get_width", 0)]
public static class ForceResolutionPatch
{
    [Postfix]
    public static void Postfix(ref int __result)
    {
        Logger.Debug($"Original width: {__result}");
        __result = 1920; // Override screen width
    }
}
```

**Example - Log Method Calls:**
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

**Example - Catch and Log Exceptions:**
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

**Example - Transform Exceptions:**
```csharp
[Patch("GameNamespace", "NetworkManager")]
[PatchMethod("Connect", 1)]
public static class NetworkErrorHandler
{
    [Finalizer]
    public static Exception Finalizer(Exception __exception)
    {
        if (__exception is System.Net.Sockets.SocketException)
        {
            Logger.Error("Network connection failed");
            return new Exception("Custom network error", __exception);
        }
        return __exception; // Re-throw other exceptions
    }
}
```

---

## Special Parameters

Patch methods can use special parameter names to access information about the original method's execution. Parameters are injected based on their name.

### __instance

```csharp
IntPtr __instance
```

The object instance that the method is called on (the `this` pointer).

- Type is always `IntPtr` (IL2CPP object pointer)
- `IntPtr.Zero` for static methods
- Can be passed to IL2CPP bridge functions
- Can be wrapped using generated wrapper types

**Example:**
```csharp
[Patch("GameNamespace", "Player")]
[PatchMethod("GetHealth", 0)]
public static class PlayerHealthPatch
{
    [Postfix]
    public static void Postfix(IntPtr __instance, ref float __result)
    {
        if (__instance != IntPtr.Zero)
        {
            Logger.Info($"Player instance 0x{__instance:X} has {__result} health");
        }
    }
}
```

---

### __0, __1, __2, ... (Method Parameters)

```csharp
Type __0  // First parameter
Type __1  // Second parameter
Type __2  // Third parameter
// etc.
```

Original method parameters by zero-based index. The type must match the parameter's IL2CPP type.

**Type Mapping:**
- Value types: Use the C# equivalent (`int`, `float`, `bool`, etc.)
- Strings: Use `string` or `IntPtr`
- Objects: Use `IntPtr` (IL2CPP object pointer)
- Enums: Use the underlying integer type

**Example:**
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

**Example with ref:**
```csharp
[Patch("GameNamespace", "Player")]
[PatchMethod("ModifyStats", 2)]
public static class StatModifierPatch
{
    [Prefix]
    public static void Prefix(ref int __0, ref float __1)
    {
        // Modify parameters before they reach the original
        __0 *= 2;  // Double the first parameter
        __1 += 5f; // Add to the second parameter
    }
}
```

---

### ref __result

```csharp
ref ReturnType __result
```

The return value of the original method. Must be passed by reference (`ref`).

**In [Prefix]:**
- Set this value when returning `false` to skip the original
- Provides the return value to the caller

**In [Postfix]:**
- Read or modify the value returned by the original method
- Changes are visible to the caller

**Type:** Must match the original method's return type. Use `IntPtr` for objects.

**Example - Prefix:**
```csharp
[Patch("GameNamespace", "Inventory")]
[PatchMethod("GetItemCount", 1)]
public static class UnlimitedItemsPatch
{
    [Prefix]
    public static bool Prefix(string __0, ref int __result)
    {
        __result = 999; // Always have 999 of every item
        return false; // Skip original
    }
}
```

**Example - Postfix:**
```csharp
[Patch("GameNamespace", "DamageCalculator")]
[PatchMethod("CalculateDamage", 2)]
public static class DoubleDamagePatch
{
    [Postfix]
    public static void Postfix(ref float __result)
    {
        __result *= 2f; // Double all damage
    }
}
```

---

### __state

```csharp
ref object __state
```

Shared state between Prefix and Postfix. Use this to pass data from your Prefix to your Postfix.

- Always type `object` - cast to your desired type
- Set in Prefix, read in Postfix
- Scoped to a single method call

**Example:**
```csharp
[Patch("GameNamespace", "Timer")]
[PatchMethod("MeasurePerformance", 0)]
public static class PerformanceTrackerPatch
{
    [Prefix]
    public static void Prefix(ref object __state)
    {
        // Store start time in __state
        __state = System.Diagnostics.Stopwatch.StartNew();
    }

    [Postfix]
    public static void Postfix(ref object __state)
    {
        // Retrieve and stop the stopwatch
        if (__state is System.Diagnostics.Stopwatch sw)
        {
            sw.Stop();
            Logger.Info($"Method took {sw.ElapsedMilliseconds}ms");
        }
    }
}
```

---

### __exception

```csharp
Exception __exception
```

The exception thrown by the original method. Only available in **[Finalizer]** patches.

- `null` if the original method completed successfully
- Contains the actual exception if one was thrown
- Read-only (use return value to modify exception handling)

**Example:**
```csharp
[Patch("GameNamespace", "RiskyOperation")]
[PatchMethod("DoSomethingDangerous", 0)]
public static class SafetyWrapper
{
    [Finalizer]
    public static Exception Finalizer(Exception __exception)
    {
        if (__exception != null)
        {
            Logger.Error($"RiskyOperation failed: {__exception.Message}");
            Logger.Error($"Stack trace: {__exception.StackTrace}");
            return null; // Swallow exception
        }
        return null;
    }
}
```

---

## Complete Examples

### Example 1: Simple Method Hook

```csharp
using GameSDK.ModHost;
using GameSDK.ModHost.Patching;

[Mod("Author.SimpleHook", "Simple Hook Example", "1.0.0")]
public class SimpleHookMod : ModBase
{
    public override void OnLoad()
    {
        Logger.Info("Simple hook installed");
    }
}

[Patch("UnityEngine", "Debug")]
[PatchMethod("Log", 1)]
public static class DebugLogPatch
{
    [Prefix]
    public static bool Prefix(string __0)
    {
        ModLogger.LogInternal("Unity", __0 ?? "<null>", System.ConsoleColor.Gray);
        return true; // Allow original to run
    }
}
```

### Example 2: Skip Original with Custom Logic

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
            __result = false; // Indicate no damage taken
            return false; // Skip original method
        }
        return true; // Normal damage
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
        Logger.Debug($"Original max health: {__result}");
        __result *= 1.5f; // 50% health boost
        Logger.Debug($"Modified max health: {__result}");
    }
}
```

### Example 4: Track Method Call Duration

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

### Example 5: Exception Handling

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
            
            // Decide whether to swallow or re-throw
            if (__exception is System.Net.Sockets.SocketException)
            {
                Logger.Warning("Network error - swallowing exception");
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
// Patch class 1
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

// Patch class 2 - same type, different method
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

// Patch class 3 - same type, different method
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

### Example 7: RVA-Based Patching (Obfuscated Code)

```csharp
[Patch("ObfuscatedGame.Core", "㐀㐀㐀")]  // Obfuscated class name
[PatchRva(0x1A3B5C0)]  // RVA from IL2CPP dump
public static class ObfuscatedPatch
{
    [Prefix]
    public static bool Prefix(IntPtr __instance, int __0, float __1)
    {
        Logger.Info($"Hooked obfuscated method: param0={__0}, param1={__1}");
        return true;
    }
}
```

---

## Best Practices

### ✅ Do

- **Use static classes** for patch containers - instance classes won't work
- **Use static methods** for Prefix/Postfix/Finalizer - instance methods won't work
- **Match parameter types** exactly to the IL2CPP method signature
- **Return `false` carefully** - only skip the original when you've handled all side effects
- **Use `ref __result`** when skipping the original in a Prefix
- **Log important events** for debugging patch behavior
- **Test with and without patches** to ensure your changes work correctly
- **Use [PatchMethod]** with parameter count for overload resolution
- **Check for IntPtr.Zero** before dereferencing object pointers

### ❌ Don't

- **Don't use instance classes or methods** - patches must be static
- **Don't modify game memory directly** - use the IL2CPP bridge or wrappers
- **Don't skip the original without setting __result** - caller expects a return value
- **Don't throw exceptions in patches** - wrap in try/catch or use Finalizer
- **Don't assume parameter types** - check the IL2CPP dump for exact types
- **Don't forget Prefix return values** - `void` means "continue", `false` means "skip"
- **Don't rely on method names for obfuscated code** - use RVA patching
- **Don't patch the same method twice in the same mod** - undefined behavior

---

## Common Mistakes

### Mistake 1: Non-Static Patch Class

```csharp
// ❌ WRONG - Instance class
[Patch("Namespace", "Type")]
public class MyPatch  // Missing 'static'
{
    [Prefix]
    public static bool Prefix() { return true; }
}
```

```csharp
// ✅ CORRECT - Static class
[Patch("Namespace", "Type")]
public static class MyPatch
{
    [Prefix]
    public static bool Prefix() { return true; }
}
```

### Mistake 2: Non-Static Patch Method

```csharp
// ❌ WRONG - Instance method
[Patch("Namespace", "Type")]
[PatchMethod("Method", 0)]
public static class MyPatch
{
    [Prefix]
    public bool Prefix() { return true; }  // Missing 'static'
}
```

```csharp
// ✅ CORRECT - Static method
[Patch("Namespace", "Type")]
[PatchMethod("Method", 0)]
public static class MyPatch
{
    [Prefix]
    public static bool Prefix() { return true; }
}
```

### Mistake 3: Skipping Original Without Setting __result

```csharp
// ❌ WRONG - Returns false but doesn't set __result
[Patch("Namespace", "Type")]
[PatchMethod("GetValue", 0)]
public static class MyPatch
{
    [Prefix]
    public static bool Prefix()
    {
        return false; // Caller gets undefined value!
    }
}
```

```csharp
// ✅ CORRECT - Sets __result before skipping
[Patch("Namespace", "Type")]
[PatchMethod("GetValue", 0)]
public static class MyPatch
{
    [Prefix]
    public static bool Prefix(ref int __result)
    {
        __result = 42; // Provide return value
        return false; // Now safe to skip
    }
}
```

### Mistake 4: Wrong Parameter Type

```csharp
// ❌ WRONG - Parameter type mismatch
[Patch("UnityEngine", "Debug")]
[PatchMethod("Log", 1)]
public static class MyPatch
{
    [Prefix]
    public static bool Prefix(int __0)  // Debug.Log takes 'object', not 'int'
    {
        return true;
    }
}
```

```csharp
// ✅ CORRECT - Use string or IntPtr for object parameters
[Patch("UnityEngine", "Debug")]
[PatchMethod("Log", 1)]
public static class MyPatch
{
    [Prefix]
    public static bool Prefix(string __0)  // Correct type
    {
        Logger.Info(__0);
        return true;
    }
}
```

### Mistake 5: Forgetting 'ref' on __result

```csharp
// ❌ WRONG - __result without ref
[Patch("Namespace", "Type")]
[PatchMethod("Calculate", 0)]
public static class MyPatch
{
    [Postfix]
    public static void Postfix(int __result)  // Missing 'ref'
    {
        __result *= 2; // Changes local copy, not the actual return value
    }
}
```

```csharp
// ✅ CORRECT - Use ref to modify return value
[Patch("Namespace", "Type")]
[PatchMethod("Calculate", 0)]
public static class MyPatch
{
    [Postfix]
    public static void Postfix(ref int __result)
    {
        __result *= 2; // Actually modifies the return value
    }
}
```

---

## Advanced Topics

### Combining Prefix, Postfix, and Finalizer

You can have multiple patch types in the same class. They execute in this order:

1. **Prefix** - Before original
2. **Original Method** - If Prefix returns true
3. **Postfix** - After original (if no exception)
4. **Finalizer** - Always runs, even on exception

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
        return __exception; // Re-throw
    }
}
```

### Working with IL2CPP Object Pointers

When a parameter or `__instance` is `IntPtr`, you're working with IL2CPP object pointers:

```csharp
[Patch("GameNamespace", "GameObject")]
[PatchMethod("GetComponent", 1)]
public static class ComponentPatch
{
    [Postfix]
    public static void Postfix(IntPtr __instance, string __0, IntPtr __result)
    {
        if (__instance != IntPtr.Zero)
        {
            // Use IL2CPP bridge to inspect the object
            IntPtr klass = Il2CppBridge.mdb_object_get_class(__instance);
            string className = Il2CppBridge.mdb_class_get_name(klass);
            Logger.Info($"GameObject type: {className}, GetComponent<{__0}> returned: 0x{__result:X}");
        }
    }
}
```

### Handling Properties

Unity properties are compiled to `get_PropertyName` and `set_PropertyName` methods:

```csharp
// Hooking a property getter
[Patch("UnityEngine", "Screen")]
[PatchMethod("get_width", 0)]  // Property getter
public static class ScreenWidthPatch
{
    [Postfix]
    public static void Postfix(ref int __result)
    {
        __result = 1920; // Force screen width
    }
}

// Hooking a property setter
[Patch("GameNamespace", "Settings")]
[PatchMethod("set_Volume", 1)]  // Property setter
public static class VolumeSetterPatch
{
    [Prefix]
    public static void Prefix(ref float __0)
    {
        // Clamp volume to safe range
        __0 = Math.Max(0f, Math.Min(__0, 1f));
    }
}
```

### Float Parameter Handling

The patching system automatically handles float parameters correctly for x64 calling conventions:

```csharp
[Patch("UnityEngine", "Time")]
[PatchMethod("get_deltaTime", 0)]
public static class DeltaTimePatch
{
    [Postfix]
    public static void Postfix(ref float __result)
    {
        // Framework handles float return values automatically
        __result *= 0.5f; // Slow down game time
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
5. Check the logs - patch discovery errors are logged to `MDB/Logs/MDB.log`
6. Use `[PatchRva]` if the method name is obfuscated

### Wrong Parameter Types

**Problem:** Patch applies but crashes or doesn't receive correct values.

**Solutions:**
1. Check the IL2CPP dump for exact parameter types
2. Use `IntPtr` for object references
3. Use `string` for string parameters (not `IntPtr`)
4. Match primitive types exactly (`int`, `float`, `bool`, etc.)
5. For enums, use the underlying integer type

### Prefix Not Skipping Original

**Problem:** Returning `false` from Prefix but original still runs.

**Solutions:**
1. Ensure you're returning `bool`, not `void`
2. Make sure the method signature is correct
3. Check logs for patch application errors
4. Verify special parameters are spelled correctly

### Can't Modify __result

**Problem:** Changes to `__result` don't affect the return value.

**Solutions:**
1. Add `ref` keyword: `ref int __result` not `int __result`
2. Ensure parameter type matches return type
3. Use `IntPtr` for object return values
4. In Prefix, you must return `false` after setting `ref __result`

---

## Performance Considerations

### Overhead

Each patch adds minimal overhead:
- **Prefix:** ~10-50 nanoseconds per call
- **Postfix:** ~10-50 nanoseconds per call
- **Finalizer:** ~20-100 nanoseconds per call (exception handling overhead)

Patches are implemented using MinHook with direct trampolines, so overhead is negligible for most use cases.

### Hot Path Methods

Avoid patching methods called thousands of times per frame (e.g., Vector3 operations, math functions) unless absolutely necessary. The overhead is small but can add up.

### Logging in Patches

Be careful with logging in frequently-called methods:

```csharp
// ❌ BAD - Logs every frame
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
// ✅ GOOD - Throttled logging
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

## See Also

- [ModBase](modbase) - Mod lifecycle and base class
- [ModAttribute](modattribute) - Mod metadata declaration
- [HookManager](hookmanager) - Manual native hooking API
- [IL2CPP Bridge](il2cpp-bridge) - Direct IL2CPP runtime access
- [Examples](../examples) - Working mod examples
- [Getting Started](../getting-started) - Creating your first mod

---

[← Back to API Index](../api) | [HookManager →](hookmanager)
