---
layout: default
title: ImGuiManager API
---

# ImGuiManager API

`ImGuiManager` is the centralized manager for Dear ImGui integration in MDB. It handles initialization, multi-mod support, and callback management automatically, allowing multiple mods to safely register their own UI callbacks with priority-based rendering.

**Namespace:** `GameSDK`

---

## Class Definition

```csharp
public static class ImGuiManager
{
    // Properties
    public static bool IsInitialized { get; }
    public static DxVersion DirectXVersion { get; }
    public static bool IsInputEnabled { get; set; }
    public static int CallbackCount { get; }
    
    // Methods
    public static bool Initialize();
    public static int RegisterCallback(string name, ImGuiDrawCallback callback, int priority = ImGuiPriority.Normal);
    public static bool UnregisterCallback(int callbackId);
    public static bool SetCallbackEnabled(int callbackId, bool enabled);
    public static void SetToggleKey(int virtualKeyCode);
    public static void Shutdown();
}
```

---

## Properties

### IsInitialized

```csharp
public static bool IsInitialized { get; }
```

Whether ImGui has been initialized.

**Returns:** `true` if ImGui is ready to use, `false` otherwise.

**Example:**
```csharp
if (ImGuiManager.IsInitialized)
{
    Logger.Info("ImGui is ready!");
}
```

---

### DirectXVersion

```csharp
public static DxVersion DirectXVersion { get; }
```

The detected DirectX version of the game.

**Returns:** `DxVersion.DX11`, `DxVersion.DX12`, or `DxVersion.Unknown`

**Example:**
```csharp
public override void OnLoad()
{
    var dxVersion = ImGuiManager.DirectXVersion;
    Logger.Info($"Game is using DirectX {(int)dxVersion}");
}
```

---

### IsInputEnabled

```csharp
public static bool IsInputEnabled { get; set; }
```

Whether ImGui input capture is currently enabled. When enabled, ImGui captures mouse and keyboard input.

**Default toggle key:** F2

**Example:**
```csharp
// Check if input is captured
if (ImGuiManager.IsInputEnabled)
{
    ImGui.Text("Input captured - Press F2 to release");
}

// Programmatically enable/disable
ImGuiManager.IsInputEnabled = false;  // Disable input capture
```

---

### CallbackCount

```csharp
public static int CallbackCount { get; }
```

Number of currently registered callbacks across all mods.

**Example:**
```csharp
Logger.Info($"Total UI callbacks: {ImGuiManager.CallbackCount}");
```

---

## Methods

### Initialize()

```csharp
public static bool Initialize()
```

Initialize ImGui if not already initialized. This is called automatically when registering a callback, but can be called manually if you need to ensure ImGui is ready.

**Returns:** `true` if initialization succeeded or was already done.

**Example:**
```csharp
public override void OnLoad()
{
    if (ImGuiManager.Initialize())
    {
        Logger.Info("ImGui initialized successfully!");
    }
    else
    {
        Logger.Error("Failed to initialize ImGui");
    }
}
```

**Note:** You typically don't need to call this manually - `RegisterCallback()` handles initialization automatically.

---

### RegisterCallback()

```csharp
public static int RegisterCallback(
    string name, 
    ImGuiDrawCallback callback, 
    int priority = ImGuiPriority.Normal
)
```

Register a draw callback. ImGui will be initialized automatically if needed. The callback will be invoked every frame to render UI.

**Parameters:**
- `name` - Name for this callback (shown in debug/logs). If null, auto-generated.
- `callback` - The draw callback delegate to invoke each frame.
- `priority` - Priority level (higher = called first). Use `ImGuiPriority` constants.

**Returns:** Callback ID (positive integer) that can be used to unregister, or `0` on failure.

**Priority Levels:**
```csharp
public static class ImGuiPriority
{
    public const int Background = -100;  // Background layers
    public const int Low = 0;            // Low priority
    public const int Normal = 100;       // Default priority
    public const int High = 200;         // High priority
    public const int Overlay = 300;      // Top-most overlays
}
```

**Example - Basic Registration:**
```csharp
private int _uiCallbackId;

public override void OnLoad()
{
    _uiCallbackId = ImGuiManager.RegisterCallback("My UI", DrawUI);
    
    if (_uiCallbackId > 0)
    {
        Logger.Info("UI callback registered successfully!");
    }
}

private void DrawUI()
{
    if (ImGui.Begin("My Window"))
    {
        ImGui.Text("Hello from my mod!");
    }
    ImGui.End();
}
```

**Example - With Priority:**
```csharp
public override void OnLoad()
{
    // Background window (drawn first, appears behind other UI)
    ImGuiManager.RegisterCallback("Background", DrawBackground, ImGuiPriority.Background);
    
    // Normal priority window
    ImGuiManager.RegisterCallback("Main UI", DrawMainUI, ImGuiPriority.Normal);
    
    // Overlay (drawn last, appears on top)
    ImGuiManager.RegisterCallback("Overlay", DrawOverlay, ImGuiPriority.Overlay);
}
```

**Example - Multiple Windows:**
```csharp
public override void OnLoad()
{
    ImGuiManager.RegisterCallback("Main Menu", DrawMainMenu);
    ImGuiManager.RegisterCallback("Debug Panel", DrawDebugPanel);
    ImGuiManager.RegisterCallback("Stats Overlay", DrawStatsOverlay);
}
```

**Thread Safety:** The callback system is thread-safe. Multiple mods can register callbacks simultaneously.

---

### UnregisterCallback()

```csharp
public static bool UnregisterCallback(int callbackId)
```

Unregister a previously registered callback. The callback will no longer be invoked.

**Parameters:**
- `callbackId` - The callback ID returned by `RegisterCallback()`

**Returns:** `true` if successfully unregistered, `false` if callback ID was not found.

**Example:**
```csharp
private int _callbackId;

public override void OnLoad()
{
    _callbackId = ImGuiManager.RegisterCallback("My UI", DrawUI);
}

private void Cleanup()
{
    if (_callbackId > 0)
    {
        if (ImGuiManager.UnregisterCallback(_callbackId))
        {
            Logger.Info("UI callback unregistered");
            _callbackId = 0;
        }
    }
}
```

**Important:** Always unregister callbacks when your mod is disabled or unloaded to prevent memory leaks and crashes.

---

### SetCallbackEnabled()

```csharp
public static bool SetCallbackEnabled(int callbackId, bool enabled)
```

Enable or disable a callback without unregistering it. Disabled callbacks are not invoked but remain registered.

**Parameters:**
- `callbackId` - The callback ID returned by `RegisterCallback()`
- `enabled` - `true` to enable, `false` to disable

**Returns:** `true` if successful, `false` if callback ID was not found.

**Example:**
```csharp
private int _callbackId;
private bool _uiVisible = true;

public override void OnLoad()
{
    _callbackId = ImGuiManager.RegisterCallback("My UI", DrawUI);
}

private void ToggleUI()
{
    _uiVisible = !_uiVisible;
    ImGuiManager.SetCallbackEnabled(_callbackId, _uiVisible);
    Logger.Info($"UI {(_uiVisible ? "enabled" : "disabled")}");
}
```

**Use Case:** Temporarily hiding UI without the overhead of unregistering and re-registering.

---

### SetToggleKey()

```csharp
public static void SetToggleKey(int virtualKeyCode)
```

Set the virtual key code used to toggle ImGui input capture.

**Parameters:**
- `virtualKeyCode` - Windows virtual key code (e.g., `0x71` for F2)

**Default:** F2 (VK_F2 = 0x71 = 113)

**Common Virtual Key Codes:**
```csharp
// Function keys
F1  = 0x70  // 112
F2  = 0x71  // 113 (default)
F3  = 0x72  // 114
F4  = 0x73  // 115

// Other keys
Insert    = 0x2D  // 45
Home      = 0x24  // 36
End       = 0x23  // 35
PageUp    = 0x21  // 33
PageDown  = 0x22  // 34
```

**Example:**
```csharp
public override void OnLoad()
{
    // Change toggle key to F3
    ImGuiManager.SetToggleKey(0x72);
    Logger.Info("ImGui toggle key set to F3");
}
```

**Note:** This affects all mods globally. Use sparingly and document in your mod's README.

---

### Shutdown()

```csharp
public static void Shutdown()
```

Shutdown ImGui and cleanup all callbacks. This is called automatically by the framework on game exit.

**Warning:** Calling this manually will disable ImGui for all mods. Only use in special circumstances.

---

## Delegate Types

### ImGuiDrawCallback

```csharp
public delegate void ImGuiDrawCallback()
```

Delegate for ImGui draw callbacks. Called once per frame when ImGui is rendering.

**Example:**
```csharp
private void MyDrawCallback()
{
    // Your ImGui rendering code here
    ImGui.Begin("My Window");
    ImGui.Text("Hello!");
    ImGui.End();
}
```

---

## Enums

### DxVersion

```csharp
public enum DxVersion : int
{
    Unknown = 0,
    DX11 = 11,
    DX12 = 12
}
```

DirectX version detected by the bridge.

---

## Complete Examples

### Example 1: Basic UI Window

```csharp
using GameSDK;
using GameSDK.ModHost;

[Mod("Author.BasicUI", "Basic UI", "1.0.0")]
public class BasicUIMod : ModBase
{
    private int _callbackId;
    private bool _windowOpen = true;

    public override void OnLoad()
    {
        // Register UI callback
        _callbackId = ImGuiManager.RegisterCallback("Basic UI", DrawUI);
        
        if (_callbackId > 0)
        {
            Logger.Info("UI registered successfully!");
        }
        else
        {
            Logger.Error("Failed to register UI!");
        }
    }

    private void DrawUI()
    {
        // Window with close button
        if (ImGui.Begin("Basic UI", ref _windowOpen))
        {
            ImGui.Text("Welcome to my mod!");
            
            if (ImGui.Button("Click Me"))
            {
                Logger.Info("Button clicked!");
            }
        }
        ImGui.End();
        
        // Hide UI when window is closed
        if (!_windowOpen)
        {
            ImGuiManager.SetCallbackEnabled(_callbackId, false);
        }
    }
}
```

---

### Example 2: Multiple Windows with Priorities

```csharp
using GameSDK;
using GameSDK.ModHost;
using System.Numerics;

[Mod("Author.MultiUI", "Multi Window UI", "1.0.0")]
public class MultiWindowMod : ModBase
{
    private int _backgroundId;
    private int _mainMenuId;
    private int _overlayId;

    public override void OnLoad()
    {
        // Register multiple windows with different priorities
        _backgroundId = ImGuiManager.RegisterCallback(
            "Background", 
            DrawBackground, 
            ImGuiPriority.Background
        );
        
        _mainMenuId = ImGuiManager.RegisterCallback(
            "Main Menu", 
            DrawMainMenu, 
            ImGuiPriority.Normal
        );
        
        _overlayId = ImGuiManager.RegisterCallback(
            "Stats Overlay", 
            DrawOverlay, 
            ImGuiPriority.Overlay
        );

        Logger.Info($"Registered {ImGuiManager.CallbackCount} UI callbacks");
    }

    private void DrawBackground()
    {
        // Background window (no titlebar, no input)
        ImGui.SetNextWindowPos(new Vector2(0, 0));
        ImGui.SetNextWindowSize(new Vector2(300, 200));
        
        if (ImGui.Begin("Background", ImGuiWindowFlags.NoTitleBar | 
                                       ImGuiWindowFlags.NoResize | 
                                       ImGuiWindowFlags.NoMove))
        {
            ImGui.Text("Background Layer");
            ImGui.TextDisabled("Appears behind other windows");
        }
        ImGui.End();
    }

    private void DrawMainMenu()
    {
        if (ImGui.Begin("Main Menu"))
        {
            ImGui.Text("Main Menu Layer");
            ImGui.Separator();
            
            if (ImGui.Button("Toggle Background"))
            {
                // Toggle background visibility
                ToggleCallback(_backgroundId);
            }
            
            if (ImGui.Button("Toggle Overlay"))
            {
                // Toggle overlay visibility
                ToggleCallback(_overlayId);
            }
        }
        ImGui.End();
    }

    private void DrawOverlay()
    {
        // Overlay window (top-right corner, transparent)
        ImGui.SetNextWindowPos(new Vector2(
            ImGui.GetIO().DisplaySize.X - 250, 
            10
        ), ImGuiCond.FirstUseEver);
        
        ImGui.SetNextWindowSize(new Vector2(240, 100), ImGuiCond.FirstUseEver);
        
        if (ImGui.Begin("Stats Overlay", ImGuiWindowFlags.NoCollapse))
        {
            ImGui.Text("Overlay Layer");
            ImGui.TextColored(new Vector4(0, 1, 0, 1), "Appears on top");
            ImGui.Text($"UI Callbacks: {ImGuiManager.CallbackCount}");
            ImGui.Text($"Input: {(ImGuiManager.IsInputEnabled ? "Enabled" : "Disabled")}");
        }
        ImGui.End();
    }

    private void ToggleCallback(int callbackId)
    {
        // Toggle by unregistering and re-registering
        // (In practice, you'd track enabled state separately)
        ImGuiManager.SetCallbackEnabled(callbackId, !IsCallbackEnabled(callbackId));
    }

    private bool IsCallbackEnabled(int callbackId)
    {
        // Track state externally (simplified example)
        return true;
    }
}
```

---

### Example 3: Conditional UI with Input Management

```csharp
using GameSDK;
using GameSDK.ModHost;
using System.Numerics;

[Mod("Author.ConditionalUI", "Conditional UI", "1.0.0")]
public class ConditionalUIMod : ModBase
{
    private int _callbackId;
    private bool _showUI = false;
    private bool _initialized = false;

    public override void OnLoad()
    {
        // Register callback
        _callbackId = ImGuiManager.RegisterCallback("Conditional UI", DrawUI);
        
        // Initially disable it
        ImGuiManager.SetCallbackEnabled(_callbackId, false);
        
        // Change toggle key to F4
        ImGuiManager.SetToggleKey(0x73);
        
        Logger.Info("Press F4 to toggle input, then F5 to show UI");
        
        _initialized = true;
    }

    public override void OnUpdate()
    {
        if (!_initialized) return;
        
        // Check for F5 key to toggle UI visibility
        if (IsKeyPressed(0x74)) // F5
        {
            _showUI = !_showUI;
            ImGuiManager.SetCallbackEnabled(_callbackId, _showUI);
            
            if (_showUI)
            {
                Logger.Info("UI shown");
            }
            else
            {
                Logger.Info("UI hidden");
            }
        }
    }

    private void DrawUI()
    {
        ImGui.SetNextWindowSize(new Vector2(400, 300), ImGuiCond.FirstUseEver);
        
        if (ImGui.Begin("Conditional UI"))
        {
            ImGui.TextColored(new Vector4(0, 1, 1, 1), "UI is visible!");
            ImGui.Text("Press F5 to hide");
            ImGui.Separator();
            
            ImGui.Text($"DirectX Version: {ImGuiManager.DirectXVersion}");
            ImGui.Text($"ImGui Initialized: {ImGuiManager.IsInitialized}");
            ImGui.Text($"Input Enabled: {ImGuiManager.IsInputEnabled}");
            ImGui.Text($"Total Callbacks: {ImGuiManager.CallbackCount}");
            
            ImGui.Spacing();
            
            if (ImGui.Button("Disable Input Capture"))
            {
                ImGuiManager.IsInputEnabled = false;
            }
            
            ImGui.SameLine();
            
            if (ImGui.Button("Enable Input Capture"))
            {
                ImGuiManager.IsInputEnabled = true;
            }
        }
        ImGui.End();
    }

    private bool IsKeyPressed(int virtualKeyCode)
    {
        // Simplified key check (implement properly using GetAsyncKeyState or similar)
        return false;
    }
}
```

---

## Best Practices

### ✅ Do

- **Always store callback IDs** for later management
- **Use appropriate priorities** for layered UI
- **Disable callbacks** instead of unregistering for temporary hiding
- **Handle exceptions** in your draw callbacks to prevent crashing other mods
- **Use descriptive names** for callbacks (shows in logs and debugging)
- **Unregister on cleanup** to prevent memory leaks

### ❌ Don't

- **Don't call `Initialize()` manually** unless necessary (RegisterCallback does it automatically)
- **Don't call `Shutdown()`** - let the framework handle it
- **Don't change toggle keys** without documenting it
- **Don't render complex UI every frame** - use throttling for heavy operations
- **Don't forget to check return values** from RegisterCallback

---

## Callback Execution Order

Callbacks are executed in **descending priority order** (highest first):

```
Priority 300 (Overlay)      → Drawn last, appears on top
Priority 200 (High)         ↓
Priority 100 (Normal)       ↓
Priority 0 (Low)            ↓
Priority -100 (Background)  → Drawn first, appears behind
```

Within the same priority, callbacks are executed in registration order.

---

## Thread Safety

`ImGuiManager` is thread-safe:
- Multiple mods can call `RegisterCallback()` simultaneously
- Callbacks are invoked on the main render thread
- Internal callback list is protected with locks

---

## Troubleshooting

### UI not appearing?

1. **Check initialization:**
   ```csharp
   Logger.Info($"ImGui initialized: {ImGuiManager.IsInitialized}");
   ```

2. **Check callback registration:**
   ```csharp
   if (callbackId == 0)
       Logger.Error("Failed to register callback!");
   ```

3. **Check DirectX version:**
   ```csharp
   if (ImGuiManager.DirectXVersion == DxVersion.Unknown)
       Logger.Error("DirectX version not detected!");
   ```

### Callback not firing?

- Verify callback is enabled: Check with internal tracking
- Ensure window is not collapsed or minimized
- Check for exceptions in callback (logs to console)

### Input not working?

- Press F2 (or custom toggle key) to enable input capture
- Check: `ImGuiManager.IsInputEnabled`
- Ensure game window has focus

---

## Performance Considerations

### Throttle Heavy Operations

```csharp
private int _frameCounter = 0;
private string _cachedData = "";

private void DrawUI()
{
    // Update expensive data every 60 frames
    if (_frameCounter++ % 60 == 0)
    {
        _cachedData = GetExpensiveData();
    }
    
    if (ImGui.Begin("My Window"))
    {
        ImGui.Text(_cachedData);
    }
    ImGui.End();
}
```

### Conditional Rendering

```csharp
private bool _isWindowVisible = false;

private void DrawUI()
{
    // Don't render if window is hidden
    if (!_isWindowVisible)
        return;
    
    if (ImGui.Begin("My Window", ref _isWindowVisible))
    {
        // ... UI code ...
    }
    ImGui.End();
}
```

---

## See Also

- [ImGui API](imgui) - Dear ImGui bindings documentation
- [ModBase](modbase) - Base class with lifecycle callbacks
- [Logger](logger) - Logging API for debugging UI issues
- [Getting Started]({{ '/getting-started' | relative_url }}) - Basic mod setup
- [Hello World Example](../../Documentation/Examples/HelloWorld/) - Complete ImGui example

---

[← Back to API Index]({{ '/api' | relative_url }}) | [Dear ImGui Bindings →](imgui)
