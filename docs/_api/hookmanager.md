---
layout: default
title: HookManager API
---

# HookManager API

The `HookManager` provides low-level manual control over native method hooks using MinHook via the MDB bridge. This is an **advanced fallback** for scenarios where the declarative `[Patch]` attribute system is insufficient.

> **For most use cases, use [Patch Attributes]({{ '/api/patch-attributes' | relative_url }}) instead.** The attribute system provides type-safe parameters, automatic marshaling, and much less boilerplate. Only reach for HookManager when you need dynamic runtime control over hooks.

**Namespace:** `GameSDK.ModHost.Patching`

---

## When to Use HookManager

HookManager exists for specific advanced scenarios:

- **Dynamic hooking at runtime** — creating or removing hooks based on game state
- **Toggling hooks on/off** — temporarily disabling a hook without removing it
- **Raw function pointer hooking** — hooking dynamically resolved addresses
- **Hook lifecycle management** — fine-grained control over when hooks are active

If your hook is static (applied on load, always active), use `[Patch]` attributes — it's simpler, safer, and more maintainable.

---

## Classes and Types

### HookManager

```csharp
public static class HookManager
```

Static class for managing native method hooks. All methods are thread-safe.

**Methods:**

| Method | Description |
|--------|-------------|
| `CreateHook` | Create a hook on an IL2CPP method pointer |
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

| Parameter | Type | Description |
|-----------|------|-------------|
| `instance` | `IntPtr` | The object instance (`IntPtr.Zero` for static methods) |
| `args` | `IntPtr` | Pointer to argument array |
| `original` | `IntPtr` | Pointer to original function trampoline |

**Returns:** `IntPtr` — Return value pointer

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

| Parameter | Type | Description |
|-----------|------|-------------|
| `methodPtr` | `IntPtr` | Pointer to the IL2CPP MethodInfo |
| `detour` | `HookCallback` | Delegate called instead of the original |
| `original` | `out IntPtr` | Output: Trampoline pointer for calling the original |
| `description` | `string` | Optional description for logging |

**Returns:** `HookInfo` on success, `null` on failure.

**Example:**
```csharp
IntPtr klass = Il2CppBridge.mdb_find_class("Assembly-CSharp", "GameNamespace", "Player");
IntPtr method = Il2CppBridge.mdb_get_method(klass, "TakeDamage", 1);
IntPtr methodPtr = Il2CppBridge.mdb_get_method_pointer(method);

HookInfo hook = HookManager.CreateHook(methodPtr, MyDetour, out IntPtr original, "Player.TakeDamage");

if (hook != null)
    Logger.Info($"Hook created: {hook.Description}");
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

Creates a hook on a raw function pointer. Use this for functions obtained from native libraries or dynamic resolution.

| Parameter | Type | Description |
|-----------|------|-------------|
| `target` | `IntPtr` | The target function pointer |
| `detour` | `HookCallback` | Delegate called instead of the original |
| `original` | `out IntPtr` | Output: Trampoline pointer for calling the original |
| `description` | `string` | Optional description for logging |

**Returns:** `HookInfo` on success, `null` on failure.

---

### SetHookEnabled

```csharp
public static bool SetHookEnabled(long hookHandle, bool enabled)
```

Enables or disables a hook without removing it. This is faster than removing and recreating a hook.

**Returns:** `true` on success, `false` on failure.

```csharp
HookManager.SetHookEnabled(hook.Handle, false); // Disable
HookManager.SetHookEnabled(hook.Handle, true);  // Re-enable
```

---

### RemoveHook

```csharp
public static bool RemoveHook(long hookHandle)
public static bool RemoveHook(HookInfo hook)
```

Removes a hook permanently.

```csharp
HookManager.RemoveHook(hook.Handle);
// or
HookManager.RemoveHook(hook);
```

---

### RemoveAllHooks

```csharp
public static void RemoveAllHooks()
```

Removes all hooks managed by HookManager.

---

### GetAllHooks

```csharp
public static IEnumerable<HookInfo> GetAllHooks()
```

Returns information about all currently active hooks.

```csharp
foreach (var hook in HookManager.GetAllHooks())
    Logger.Info($"{hook.Description} (enabled={hook.Enabled})");
```

---

## Examples

### Example 1: Conditional Hook Toggle

The primary advantage of HookManager — toggling hooks at runtime based on user input.

```csharp
[Mod("Author.ConditionalHook", "Conditional Hook", "1.0.0")]
public class ConditionalHookMod : ModBase
{
    private HookInfo _networkHook;
    private IntPtr _originalNetworkSend;
    private bool _monitorNetwork = false;

    public override void OnLoad()
    {
        IntPtr klass = Il2CppBridge.mdb_find_class("Assembly-CSharp", "Network", "NetworkManager");
        IntPtr method = Il2CppBridge.mdb_get_method(klass, "SendPacket", 1);
        IntPtr methodPtr = Il2CppBridge.mdb_get_method_pointer(method);

        _networkHook = HookManager.CreateHook(
            methodPtr, NetworkSendDetour, out _originalNetworkSend,
            "NetworkManager.SendPacket");

        if (_networkHook != null)
        {
            // Start disabled — user toggles via UI
            HookManager.SetHookEnabled(_networkHook.Handle, false);
            Logger.Info("Network hook created (disabled)");
        }

        ImGuiManager.RegisterCallback(DrawUI, "Network Monitor");
    }

    private void DrawUI()
    {
        if (ImGui.Begin("Network Monitor"))
        {
            if (ImGui.Checkbox("Monitor Traffic", ref _monitorNetwork))
            {
                if (_networkHook != null)
                    HookManager.SetHookEnabled(_networkHook.Handle, _monitorNetwork);
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

### Example 2: Dynamic Hook Collection

Managing multiple hooks that can be added and removed at runtime.

```csharp
[Mod("Author.DynamicHooks", "Dynamic Hooks", "1.0.0")]
public class DynamicHookMod : ModBase
{
    private List<HookInfo> _activeHooks = new List<HookInfo>();

    public override void OnLoad()
    {
        ImGuiManager.RegisterCallback(DrawUI, "Hook Manager");
    }

    private void DrawUI()
    {
        if (ImGui.Begin("Hook Manager"))
        {
            ImGui.Text($"Active Hooks: {_activeHooks.Count}");
            ImGui.Separator();

            if (ImGui.Button("Hook Player.TakeDamage"))
                AddHook("GameNamespace", "Player", "TakeDamage", 1);

            if (ImGui.Button("Remove All Hooks"))
            {
                foreach (var hook in _activeHooks)
                    HookManager.RemoveHook(hook);
                _activeHooks.Clear();
            }

            foreach (var hook in _activeHooks.ToArray())
            {
                bool enabled = hook.Enabled;
                if (ImGui.Checkbox(hook.Description, ref enabled))
                    HookManager.SetHookEnabled(hook.Handle, enabled);
            }
        }
        ImGui.End();
    }

    private void AddHook(string ns, string type, string method, int paramCount)
    {
        IntPtr klass = Il2CppBridge.mdb_find_class("Assembly-CSharp", ns, type);
        IntPtr m = Il2CppBridge.mdb_get_method(klass, method, paramCount);
        IntPtr ptr = Il2CppBridge.mdb_get_method_pointer(m);

        var hook = HookManager.CreateHook(ptr, GenericDetour, out _, $"{type}.{method}");
        if (hook != null)
            _activeHooks.Add(hook);
    }

    private IntPtr GenericDetour(IntPtr instance, IntPtr args, IntPtr original)
    {
        Logger.Debug("Hook triggered");
        return Il2CppBridge.InvokeOriginal(original, instance, args);
    }
}
```

---

## Attribute Patching vs. HookManager

| Feature | [Patch] Attributes | HookManager |
|---------|-------------------|-------------|
| **Recommended for** | Most use cases | Advanced/dynamic scenarios |
| **Setup** | Declarative — just add attributes | Manual — write hook setup code |
| **Runtime control** | Static — applied at load | Dynamic — create/remove anytime |
| **Enable/Disable** | Not supported | `SetHookEnabled()` |
| **Type safety** | High — strongly typed parameters | Low — raw pointers |
| **Parameter marshaling** | Automatic | Manual |
| **Performance** | Same (both use MinHook) | Same (both use MinHook) |
| **Code readability** | High — self-documenting | Medium — requires boilerplate |

**Rule of thumb:** If you don't need runtime toggle/removal, use `[Patch]` attributes.

---

## Best Practices

### Do

- **Store `HookInfo` and original pointers** — you need them to control the hook or call the original
- **Check for null** after `CreateHook()` — hook creation can fail
- **Wrap detours in try-catch** — unhandled exceptions in detours crash the game
- **Call the original method** unless you intentionally want to skip it
- **Use descriptive names** in the `description` parameter
- **Clean up hooks** when they're no longer needed
- **Prefer `[Patch]` attributes** for static hooks — simpler and safer

### Don't

- **Don't lose the HookInfo** — you can't control the hook without it
- **Don't hook the same method twice** — will cause undefined behavior
- **Don't throw unhandled exceptions** in detours
- **Don't use HookManager for simple static patches** — use `[Patch]` attributes instead

---

## Common Mistakes

### Losing the HookInfo

```csharp
// WRONG — Can't control the hook later
HookManager.CreateHook(methodPtr, Detour, out IntPtr original, "MyHook");
```

```csharp
// CORRECT — Store the HookInfo
private HookInfo _myHook;
_myHook = HookManager.CreateHook(methodPtr, Detour, out IntPtr original, "MyHook");
```

### Not Checking for Null

```csharp
// WRONG — NullReferenceException if creation fails
var hook = HookManager.CreateHook(methodPtr, Detour, out IntPtr original, "MyHook");
HookManager.SetHookEnabled(hook.Handle, false);
```

```csharp
// CORRECT
var hook = HookManager.CreateHook(methodPtr, Detour, out IntPtr original, "MyHook");
if (hook != null)
    HookManager.SetHookEnabled(hook.Handle, false);
else
    Logger.Error("Failed to create hook");
```

### Unhandled Exceptions in Detours

```csharp
// WRONG — Unhandled exception crashes the game
private IntPtr Detour(IntPtr instance, IntPtr args, IntPtr original)
{
    string value = GetSomeValue(); // Might throw
    return Il2CppBridge.InvokeOriginal(original, instance, args);
}
```

```csharp
// CORRECT — Wrap in try-catch
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

## Performance

Manual hooks have the **same performance** as `[Patch]` attributes — both use MinHook trampolines:

- **Hook creation:** ~1-5ms per hook (one-time cost)
- **Hook call overhead:** ~10-50ns per call
- **Disable/enable:** ~10-100μs

**Tip:** Disable instead of remove if you'll re-enable later — disabling is faster.

---

## Thread Safety

`HookManager` methods are **thread-safe** with internal locking.

Your detour functions may be called from multiple threads if the hooked method is multi-threaded. Design detours accordingly.

---

## Cleanup

Hooks are automatically cleaned up when:
- You call `RemoveHook()` or `RemoveAllHooks()`
- The mod is unloaded (framework handles cleanup)

For long-running mods with dynamic hooking, explicitly remove hooks you no longer need:

```csharp
if (_tempHook != null)
{
    HookManager.RemoveHook(_tempHook);
    _tempHook = null;
}
```

---

## Troubleshooting

### Hook Creation Fails

**`CreateHook()` returns `null`.**

1. Verify the method pointer is valid (not `IntPtr.Zero`)
2. Check that the target method exists at runtime
3. Look at `MDB/Logs/MDB.log` for error messages

### Detour Not Called

1. Verify the hook is enabled: `hook.Enabled == true`
2. Check if the target method is actually being called
3. Ensure you're not removing the hook prematurely

### Game Crashes After Hook

1. Wrap detour in try-catch to isolate exceptions
2. Check calling convention matches (should be Cdecl)
3. Test by simply calling the original without any logic

---

## See Also

- [Patch Attributes]({{ '/api/patch-attributes' | relative_url }}) — Declarative patching (recommended)
- [IL2CPP Bridge]({{ '/api/il2cpp-bridge' | relative_url }}) — Direct IL2CPP runtime access
- [ModBase]({{ '/api/modbase' | relative_url }}) — Mod lifecycle and base class
- [Examples]({{ '/examples' | relative_url }}) — Working mod examples

---

[← Back to API Index]({{ '/api' | relative_url }}) | [Patch Attributes →]({{ '/api/patch-attributes' | relative_url }})
