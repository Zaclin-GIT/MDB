---
layout: default
title: HookManager API
---

# HookManager API

The `HookManager` provides low-level manual control over native method hooks using MinHook (a Windows API hooking library) via the MDB bridge. Use this when you need dynamic hooking, runtime hook toggling, or when declarative `[Patch]` attributes are insufficient.

**Namespace:** `GameSDK.ModHost.Patching`

---

## Overview

The MDB Framework offers two approaches to method hooking:

1. **Declarative Patching** - Using `[Patch]` attributes (recommended for most cases)
2. **Manual Hooking** - Using `HookManager` API (for advanced scenarios)

**When to use HookManager:**
- Dynamic hook creation/removal at runtime
- Conditional hooking based on game state
- Temporarily enabling/disabling hooks
- Hooking methods by RVA or raw function pointers
- Direct control over hook lifecycle

**When to use [Patch] attributes:**
- Static patches that apply on mod load
- Simple method interception
- Declarative, maintainable code
- Standard modding workflows

---

## Classes and Types

### HookManager

```csharp
public static class HookManager
```

Static class for managing native method hooks. All methods are thread-safe.

**Static Methods:**

| Method | Description |
|--------|-------------|
| `CreateHook` | Create a hook on an IL2CPP method |
| `CreateHookByRva` | Create a hook on a method by RVA offset |
| `CreateHookByPtr` | Create a hook on a raw function pointer |
| `SetHookEnabled` | Enable or disable a hook |
| `RemoveHook` | Remove a specific hook |
| `RemoveAllHooks` | Remove all hooks created by this manager |
| `GetAllHooks` | Get information about all active hooks |

---

### HookInfo

```csharp
public class HookInfo
{
    public long Handle { get; internal set; }
    public IntPtr Target { get; internal set; }
    public IntPtr Original { get; internal set; }
    public bool Enabled { get; internal set; }
    public string Description { get; internal set; }
}
```

Information about an installed hook.

**Properties:**

| Property | Type | Description |
|----------|------|-------------|
| `Handle` | `long` | Unique identifier for this hook |
| `Target` | `IntPtr` | Pointer to the target method being hooked |
| `Original` | `IntPtr` | Pointer to the trampoline for calling the original method |
| `Enabled` | `bool` | Whether the hook is currently active |
| `Description` | `string` | User-provided description for logging and debugging |

---

### HookCallback

```csharp
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate IntPtr HookCallback(IntPtr instance, IntPtr args, IntPtr original);
```

Delegate signature for hook detour functions.

**Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `instance` | `IntPtr` | The object instance (`IntPtr.Zero` for static methods) |
| `args` | `IntPtr` | Pointer to argument array |
| `original` | `IntPtr` | Pointer to original function trampoline |

**Returns:** `IntPtr` - Return value pointer

**Example:**
```csharp
private static IntPtr MyDetour(IntPtr instance, IntPtr args, IntPtr original)
{
    // Your hook logic here
    // Call original if needed: return original(instance, args);
    return IntPtr.Zero;
}
```

---

## Methods

### CreateHook

```csharp
public static HookInfo CreateHook(
    IntPtr methodPtr,
    HookCallback detour,
    out IntPtr original,
    string description = null)
```

Creates a hook on an IL2CPP method.

**Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `methodPtr` | `IntPtr` | Pointer to the IL2CPP MethodInfo |
| `detour` | `HookCallback` | Delegate that will be called instead of the original |
| `original` | `out IntPtr` | Output: Pointer to trampoline for calling the original method |
| `description` | `string` | Optional description for logging (default: `"Hook_{handle}"`) |

**Returns:** `HookInfo` on success, `null` on failure.

**Example:**
```csharp
// Find the method
IntPtr klass = Il2CppBridge.mdb_find_class("Assembly-CSharp", "GameNamespace", "Player");
IntPtr method = Il2CppBridge.mdb_get_method(klass, "TakeDamage", 1);
IntPtr methodPtr = Il2CppBridge.mdb_get_method_pointer(method);

// Create the hook
HookInfo hook = HookManager.CreateHook(methodPtr, MyDetour, out IntPtr original, "Player.TakeDamage");

if (hook != null)
{
    Logger.Info($"Hook created: {hook.Description}");
    // Store 'original' to call the original method later
}
```

---

### CreateHookByRva

```csharp
public static HookInfo CreateHookByRva(
    ulong rva,
    HookCallback detour,
    out IntPtr original,
    string description = null)
```

Creates a hook on a method by its RVA (Relative Virtual Address) offset.

**Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `rva` | `ulong` | The RVA offset from the IL2CPP dump |
| `detour` | `HookCallback` | Delegate that will be called instead of the original |
| `original` | `out IntPtr` | Output: Pointer to trampoline for calling the original method |
| `description` | `string` | Optional description (default: `"RvaHook_0x{rva:X}"`) |

**Returns:** `HookInfo` on success, `null` on failure.

**Use case:** Hooking obfuscated methods that can't be found by name.

**Example:**
```csharp
// Hook a method at a specific RVA (from IL2CPP dump)
HookInfo hook = HookManager.CreateHookByRva(0x1A3B5C0, ObfuscatedDetour, out IntPtr original, "ObfuscatedMethod");

if (hook != null)
{
    Logger.Info("Hooked obfuscated method by RVA");
}
```

---

### CreateHookByPtr

```csharp
public static HookInfo CreateHookByPtr(
    IntPtr target,
    HookCallback detour,
    out IntPtr original,
    string description = null)
```

Creates a hook on a raw function pointer.

**Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `target` | `IntPtr` | The target function pointer |
| `detour` | `HookCallback` | Delegate that will be called instead of the original |
| `original` | `out IntPtr` | Output: Pointer to trampoline for calling the original method |
| `description` | `string` | Optional description (default: `"PtrHook_0x{target:X}"`) |

**Returns:** `HookInfo` on success, `null` on failure.

**Use case:** Hooking functions obtained from native libraries or dynamic resolution.

**Example:**
```csharp
// Hook a function pointer obtained dynamically
IntPtr functionPtr = GetFunctionPointerSomehow();

HookInfo hook = HookManager.CreateHookByPtr(functionPtr, CustomDetour, out IntPtr original, "DynamicFunction");

if (hook != null)
{
    Logger.Info($"Hooked function at 0x{functionPtr:X}");
}
```

---

### SetHookEnabled

```csharp
public static bool SetHookEnabled(long hookHandle, bool enabled)
```

Enables or disables a hook without removing it.

**Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `hookHandle` | `long` | The hook handle from `HookInfo.Handle` |
| `enabled` | `bool` | `true` to enable, `false` to disable |

**Returns:** `true` on success, `false` on failure.

**Example:**
```csharp
// Temporarily disable a hook
if (HookManager.SetHookEnabled(hook.Handle, false))
{
    Logger.Info("Hook disabled");
}

// Re-enable later
if (HookManager.SetHookEnabled(hook.Handle, true))
{
    Logger.Info("Hook re-enabled");
}
```

---

### RemoveHook

```csharp
public static bool RemoveHook(long hookHandle)
public static bool RemoveHook(HookInfo hook)
```

Removes a hook permanently.

**Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `hookHandle` | `long` | The hook handle from `HookInfo.Handle` |
| `hook` | `HookInfo` | The HookInfo object |

**Returns:** `true` on success, `false` on failure.

**Example:**
```csharp
// Remove by handle
if (HookManager.RemoveHook(hook.Handle))
{
    Logger.Info("Hook removed");
}

// Remove by HookInfo object
if (HookManager.RemoveHook(hook))
{
    Logger.Info("Hook removed");
}
```

---

### RemoveAllHooks

```csharp
public static void RemoveAllHooks()
```

Removes all hooks managed by this HookManager instance.

**Example:**
```csharp
// Clean up all hooks (e.g., on mod unload)
HookManager.RemoveAllHooks();
Logger.Info("All hooks removed");
```

---

### GetAllHooks

```csharp
public static IEnumerable<HookInfo> GetAllHooks()
```

Returns information about all currently active hooks.

**Returns:** Collection of `HookInfo` objects.

**Example:**
```csharp
foreach (var hook in HookManager.GetAllHooks())
{
    Logger.Info($"Hook: {hook.Description} (enabled={hook.Enabled}, handle={hook.Handle})");
}
```

---

## Complete Examples

### Example 1: Basic Manual Hook

```csharp
using System;
using GameSDK.ModHost;
using GameSDK.ModHost.Patching;

[Mod("Author.ManualHook", "Manual Hook Example", "1.0.0")]
public class ManualHookMod : ModBase
{
    private HookInfo _playerDamageHook;
    private IntPtr _originalDamage;

    public override void OnLoad()
    {
        // Find the target method
        IntPtr klass = Il2CppBridge.mdb_find_class("Assembly-CSharp", "GameNamespace", "Player");
        if (klass == IntPtr.Zero)
        {
            Logger.Error("Failed to find Player class");
            return;
        }

        IntPtr method = Il2CppBridge.mdb_get_method(klass, "TakeDamage", 1);
        if (method == IntPtr.Zero)
        {
            Logger.Error("Failed to find TakeDamage method");
            return;
        }

        IntPtr methodPtr = Il2CppBridge.mdb_get_method_pointer(method);

        // Create the hook
        _playerDamageHook = HookManager.CreateHook(methodPtr, TakeDamageDetour, out _originalDamage, "Player.TakeDamage");

        if (_playerDamageHook != null)
        {
            Logger.Info("Player damage hook installed successfully");
        }
        else
        {
            Logger.Error("Failed to install player damage hook");
        }
    }

    private IntPtr TakeDamageDetour(IntPtr instance, IntPtr args, IntPtr original)
    {
        try
        {
            // Log the damage call
            Logger.Info("Player is taking damage");

            // Call the original method
            return Il2CppBridge.InvokeOriginal(original, instance, args);
        }
        catch (Exception ex)
        {
            Logger.Error($"Error in TakeDamageDetour: {ex.Message}");
            return IntPtr.Zero;
        }
    }
}
```

---

### Example 2: Conditional Hooking

```csharp
[Mod("Author.ConditionalHook", "Conditional Hook Example", "1.0.0")]
public class ConditionalHookMod : ModBase
{
    private HookInfo _networkHook;
    private IntPtr _originalNetworkSend;
    private bool _monitorNetwork = false;

    public override void OnLoad()
    {
        // Setup hook (but don't enable yet)
        IntPtr klass = Il2CppBridge.mdb_find_class("Assembly-CSharp", "Network", "NetworkManager");
        IntPtr method = Il2CppBridge.mdb_get_method(klass, "SendPacket", 1);
        IntPtr methodPtr = Il2CppBridge.mdb_get_method_pointer(method);

        _networkHook = HookManager.CreateHook(methodPtr, NetworkSendDetour, out _originalNetworkSend, "NetworkManager.SendPacket");

        if (_networkHook != null)
        {
            // Disable by default
            HookManager.SetHookEnabled(_networkHook.Handle, false);
            Logger.Info("Network hook created (disabled)");
        }

        // Register UI to toggle monitoring
        ImGuiManager.RegisterCallback(DrawUI, "Network Monitor");
    }

    private void DrawUI()
    {
        if (ImGui.Begin("Network Monitor"))
        {
            if (ImGui.Checkbox("Monitor Network Traffic", ref _monitorNetwork))
            {
                // Toggle hook based on checkbox
                if (_networkHook != null)
                {
                    HookManager.SetHookEnabled(_networkHook.Handle, _monitorNetwork);
                    Logger.Info($"Network monitoring {(_monitorNetwork ? "enabled" : "disabled")}");
                }
            }
        }
        ImGui.End();
    }

    private IntPtr NetworkSendDetour(IntPtr instance, IntPtr args, IntPtr original)
    {
        Logger.Info("Network packet sent");
        return Il2CppBridge.InvokeOriginal(original, instance, args);
    }
}
```

---

### Example 3: Dynamic Hook Management

```csharp
[Mod("Author.DynamicHooks", "Dynamic Hook Manager", "1.0.0")]
public class DynamicHookMod : ModBase
{
    private List<HookInfo> _activeHooks = new List<HookInfo>();
    private Dictionary<string, IntPtr> _originalMethods = new Dictionary<string, IntPtr>();

    public override void OnLoad()
    {
        Logger.Info("Dynamic hook manager initialized");
        ImGuiManager.RegisterCallback(DrawUI, "Hook Manager");
    }

    private void DrawUI()
    {
        if (ImGui.Begin("Hook Manager"))
        {
            ImGui.Text($"Active Hooks: {_activeHooks.Count}");
            ImGui.Separator();

            if (ImGui.Button("Hook Player.TakeDamage"))
            {
                HookPlayerDamage();
            }

            if (ImGui.Button("Hook Enemy.Attack"))
            {
                HookEnemyAttack();
            }

            if (ImGui.Button("Remove All Hooks"))
            {
                RemoveAllDynamicHooks();
            }

            ImGui.Separator();
            ImGui.Text("Current Hooks:");

            foreach (var hook in _activeHooks)
            {
                ImGui.PushID(hook.Handle.GetHashCode());
                ImGui.Text($"• {hook.Description}");
                ImGui.SameLine();

                bool enabled = hook.Enabled;
                if (ImGui.Checkbox("Enabled", ref enabled))
                {
                    HookManager.SetHookEnabled(hook.Handle, enabled);
                }

                ImGui.SameLine();
                if (ImGui.Button("Remove"))
                {
                    HookManager.RemoveHook(hook);
                    _activeHooks.Remove(hook);
                    Logger.Info($"Removed hook: {hook.Description}");
                }

                ImGui.PopID();
            }
        }
        ImGui.End();
    }

    private void HookPlayerDamage()
    {
        IntPtr klass = Il2CppBridge.mdb_find_class("Assembly-CSharp", "GameNamespace", "Player");
        IntPtr method = Il2CppBridge.mdb_get_method(klass, "TakeDamage", 1);
        IntPtr methodPtr = Il2CppBridge.mdb_get_method_pointer(method);

        HookInfo hook = HookManager.CreateHook(methodPtr, GenericDetour, out IntPtr original, "Player.TakeDamage");
        if (hook != null)
        {
            _activeHooks.Add(hook);
            _originalMethods[hook.Description] = original;
            Logger.Info("Hooked Player.TakeDamage");
        }
    }

    private void HookEnemyAttack()
    {
        IntPtr klass = Il2CppBridge.mdb_find_class("Assembly-CSharp", "GameNamespace", "Enemy");
        IntPtr method = Il2CppBridge.mdb_get_method(klass, "Attack", 0);
        IntPtr methodPtr = Il2CppBridge.mdb_get_method_pointer(method);

        HookInfo hook = HookManager.CreateHook(methodPtr, GenericDetour, out IntPtr original, "Enemy.Attack");
        if (hook != null)
        {
            _activeHooks.Add(hook);
            _originalMethods[hook.Description] = original;
            Logger.Info("Hooked Enemy.Attack");
        }
    }

    private void RemoveAllDynamicHooks()
    {
        foreach (var hook in _activeHooks)
        {
            HookManager.RemoveHook(hook);
        }
        _activeHooks.Clear();
        _originalMethods.Clear();
        Logger.Info("Removed all dynamic hooks");
    }

    private IntPtr GenericDetour(IntPtr instance, IntPtr args, IntPtr original)
    {
        Logger.Debug("Hook triggered");
        return Il2CppBridge.InvokeOriginal(original, instance, args);
    }
}
```

---

### Example 4: RVA-Based Hooking (Obfuscated Code)

```csharp
[Mod("Author.RvaHook", "RVA Hook Example", "1.0.0")]
public class RvaHookMod : ModBase
{
    private HookInfo _obfuscatedHook;
    private IntPtr _originalObfuscated;

    public override void OnLoad()
    {
        // Hook a method by RVA (from IL2CPP dump)
        // RVA 0x1A3B5C0 corresponds to an obfuscated method
        _obfuscatedHook = HookManager.CreateHookByRva(
            0x1A3B5C0,
            ObfuscatedMethodDetour,
            out _originalObfuscated,
            "ObfuscatedCriticalFunction"
        );

        if (_obfuscatedHook != null)
        {
            Logger.Info($"Hooked obfuscated method at RVA 0x1A3B5C0");
        }
        else
        {
            Logger.Error("Failed to hook obfuscated method");
        }
    }

    private IntPtr ObfuscatedMethodDetour(IntPtr instance, IntPtr args, IntPtr original)
    {
        Logger.Info("Obfuscated method called");

        // Inspect arguments if needed
        // (requires knowledge of the method signature)

        // Call original
        return Il2CppBridge.InvokeOriginal(original, instance, args);
    }
}
```

---

## Declarative vs. Manual Hooking

### Comparison Table

| Feature | [Patch] Attributes | HookManager |
|---------|-------------------|-------------|
| **Setup Complexity** | Low - declarative | High - manual code |
| **Runtime Control** | Static - applied at load | Dynamic - create/remove anytime |
| **Enable/Disable** | Not supported | `SetHookEnabled()` |
| **Type Safety** | High - strongly typed parameters | Low - raw pointers |
| **Performance** | Same - both use MinHook | Same - both use MinHook |
| **Code Readability** | High - self-documenting | Medium - requires setup code |
| **Special Parameters** | Yes - `__instance`, `__result`, etc. | No - manual marshalling |
| **Multiple Patches** | Easy - multiple classes | Manual - manage collection |
| **RVA Support** | Yes - `[PatchRva]` | Yes - `CreateHookByRva()` |
| **Conditional Hooking** | No - always active | Yes - runtime decisions |

---

### When to Use [Patch] Attributes

Use declarative `[Patch]` attributes when:

✅ Patches are **static** and don't need to change at runtime  
✅ You want **type-safe** parameter handling  
✅ You need **special parameters** like `__instance`, `__result`, `__state`  
✅ **Readability** and maintainability are priorities  
✅ You're implementing **standard modding patterns**  

**Example:**
```csharp
[Patch("GameNamespace", "Player")]
[PatchMethod("TakeDamage", 1)]
public static class PlayerDamagePatch
{
    [Prefix]
    public static bool Prefix(IntPtr __instance, ref int __0)
    {
        Logger.Info($"Player taking {__0} damage");
        return true;
    }
}
```

---

### When to Use HookManager

Use manual `HookManager` when:

✅ Hooks need to be **created/removed dynamically**  
✅ You need to **enable/disable** hooks at runtime  
✅ Hooking is **conditional** based on game state  
✅ You're hooking by **raw pointers** or dynamically resolved addresses  
✅ You need **fine-grained control** over hook lifecycle  

**Example:**
```csharp
// Create hook conditionally
if (shouldMonitorNetwork)
{
    hook = HookManager.CreateHook(methodPtr, Detour, out original, "NetworkSend");
}

// Disable temporarily
HookManager.SetHookEnabled(hook.Handle, false);

// Re-enable later
HookManager.SetHookEnabled(hook.Handle, true);

// Remove when done
HookManager.RemoveHook(hook);
```

---

## Best Practices

### ✅ Do

- **Store HookInfo and original pointers** - You need them to call the original or remove the hook
- **Check for null** after `CreateHook()` - Hook creation can fail
- **Use descriptive names** in the `description` parameter for debugging
- **Wrap detours in try-catch** - Exceptions in detours can crash the game
- **Call the original method** unless you intentionally want to skip it
- **Clean up hooks** - Remove or disable hooks when they're no longer needed
- **Use HookManager for dynamic scenarios** - When hooks need runtime control
- **Test hook creation failures** - Handle the case where the target method doesn't exist

### ❌ Don't

- **Don't lose the HookInfo** - You can't control the hook without it
- **Don't forget the original pointer** - You'll need it to call the original method
- **Don't hook the same method twice** - Will cause undefined behavior
- **Don't throw unhandled exceptions** in detours - Will crash the game
- **Don't hook critical engine methods** without careful testing
- **Don't leak hooks** - Always remove hooks when the mod unloads (if applicable)
- **Don't use HookManager for simple static patches** - Use `[Patch]` attributes instead
- **Don't call HookManager from multiple threads** without additional synchronization

---

## Common Mistakes

### Mistake 1: Losing the HookInfo

```csharp
// ❌ WRONG - Can't control the hook later
HookManager.CreateHook(methodPtr, Detour, out IntPtr original, "MyHook");
```

```csharp
// ✅ CORRECT - Store the HookInfo
private HookInfo _myHook;

_myHook = HookManager.CreateHook(methodPtr, Detour, out IntPtr original, "MyHook");
```

---

### Mistake 2: Not Checking for Null

```csharp
// ❌ WRONG - Assumes hook creation succeeds
var hook = HookManager.CreateHook(methodPtr, Detour, out IntPtr original, "MyHook");
HookManager.SetHookEnabled(hook.Handle, false); // NullReferenceException if hook is null
```

```csharp
// ✅ CORRECT - Check for null
var hook = HookManager.CreateHook(methodPtr, Detour, out IntPtr original, "MyHook");
if (hook != null)
{
    HookManager.SetHookEnabled(hook.Handle, false);
}
else
{
    Logger.Error("Failed to create hook");
}
```

---

### Mistake 3: Forgetting to Store the Original Pointer

```csharp
// ❌ WRONG - Lost the original pointer
HookManager.CreateHook(methodPtr, Detour, out IntPtr original, "MyHook");
// 'original' goes out of scope

private IntPtr Detour(IntPtr instance, IntPtr args, IntPtr original)
{
    // Can't call the original method!
    return IntPtr.Zero;
}
```

```csharp
// ✅ CORRECT - Store the original pointer
private IntPtr _originalMethod;

HookManager.CreateHook(methodPtr, Detour, out _originalMethod, "MyHook");

private IntPtr Detour(IntPtr instance, IntPtr args, IntPtr original)
{
    // Call the original method
    return Il2CppBridge.InvokeOriginal(_originalMethod, instance, args);
}
```

---

### Mistake 4: Unhandled Exceptions in Detours

```csharp
// ❌ WRONG - Unhandled exception crashes the game
private IntPtr Detour(IntPtr instance, IntPtr args, IntPtr original)
{
    string value = GetSomeValue(); // Might throw
    ProcessValue(value); // Might throw
    return Il2CppBridge.InvokeOriginal(original, instance, args);
}
```

```csharp
// ✅ CORRECT - Wrap in try-catch
private IntPtr Detour(IntPtr instance, IntPtr args, IntPtr original)
{
    try
    {
        string value = GetSomeValue();
        ProcessValue(value);
    }
    catch (Exception ex)
    {
        Logger.Error($"Error in detour: {ex.Message}");
    }

    return Il2CppBridge.InvokeOriginal(original, instance, args);
}
```

---

### Mistake 5: Using HookManager for Static Patches

```csharp
// ❌ WRONG - Unnecessarily complex for static patches
[Mod("Author.Mod", "My Mod", "1.0.0")]
public class MyMod : ModBase
{
    public override void OnLoad()
    {
        IntPtr klass = Il2CppBridge.mdb_find_class("Assembly-CSharp", "Game", "Player");
        IntPtr method = Il2CppBridge.mdb_get_method(klass, "Update", 0);
        IntPtr methodPtr = Il2CppBridge.mdb_get_method_pointer(method);
        HookManager.CreateHook(methodPtr, PlayerUpdateDetour, out _original, "Player.Update");
    }
}
```

```csharp
// ✅ CORRECT - Use [Patch] attributes for static patches
[Patch("Game", "Player")]
[PatchMethod("Update", 0)]
public static class PlayerUpdatePatch
{
    [Prefix]
    public static void Prefix(IntPtr __instance)
    {
        // Much simpler!
    }
}
```

---

## Performance Considerations

### Hook Overhead

Manual hooks have the **same performance** as declarative `[Patch]` attributes - both use MinHook trampolines:

- **Hook creation:** ~1-5ms per hook (one-time cost)
- **Hook call overhead:** ~10-50ns per call
- **Disable/enable:** ~10-100μs (microseconds)

### Best Practices for Performance

✅ **Create hooks once** - Don't repeatedly create/remove hooks in hot paths  
✅ **Disable instead of remove** - Disabling is faster if you'll re-enable later  
✅ **Keep detours lightweight** - They run every time the method is called  
✅ **Avoid hooking hot path methods** - Methods called thousands of times per frame  
✅ **Use conditional logic** - Check conditions before doing expensive work in detours  

---

## Thread Safety

`HookManager` is **thread-safe**. All methods use internal locking to prevent race conditions.

However, your **detour functions** may be called from multiple threads if the hooked method is called from multiple threads. Design your detours accordingly.

---

## Cleanup and Resource Management

### Automatic Cleanup

Hooks are automatically cleaned up when:
- You call `RemoveHook()` or `RemoveAllHooks()`
- The mod is unloaded (framework handles cleanup)

### Manual Cleanup

For long-running mods with dynamic hooking, explicitly remove hooks you no longer need:

```csharp
// Remove specific hook
if (_tempHook != null)
{
    HookManager.RemoveHook(_tempHook);
    _tempHook = null;
}

// Remove all hooks
HookManager.RemoveAllHooks();
```

---

## Troubleshooting

### Hook Creation Fails

**Problem:** `CreateHook()` returns `null`.

**Solutions:**
1. Verify the method pointer is valid (not `IntPtr.Zero`)
2. Check that the target method exists at runtime
3. Look at `MDB/Logs/MDB.log` for error messages
4. Ensure the method signature is correct
5. Try using RVA if the method is obfuscated

### Detour Not Called

**Problem:** Hook created successfully but detour never executes.

**Solutions:**
1. Verify the hook is enabled: `hook.Enabled == true`
2. Check if the target method is actually being called
3. Ensure you're not removing the hook prematurely
4. Look for error messages in the logs

### Game Crashes After Hook

**Problem:** Game crashes when the hooked method is called.

**Solutions:**
1. Wrap detour in try-catch to isolate exceptions
2. Check calling convention matches (should be Cdecl)
3. Ensure you're not corrupting memory in the detour
4. Verify parameter handling is correct
5. Test by simply calling the original without any logic

---

## See Also

- [Patch Attributes](patch-attributes) - Declarative patching system
- [IL2CPP Bridge](il2cpp-bridge) - Direct IL2CPP runtime access
- [ModBase](modbase) - Mod lifecycle and base class
- [Examples](../examples) - Working mod examples
- [Getting Started](../getting-started) - Creating your first mod

---

[← Back to API Index](../api) | [Patch Attributes →](patch-attributes)
