---
layout: default
title: ModBase API
---

# ModBase API

`ModBase` is the abstract base class that all MDB mods must inherit from. It provides lifecycle callbacks and access to core framework services like logging.

**Namespace:** `GameSDK.ModHost`

---

## Class Definition

```csharp
public abstract class ModBase
{
    public ModInfo Info { get; internal set; }
    public ModLogger Logger { get; internal set; }
    
    public virtual void OnLoad() { }
    public virtual void OnUpdate() { }
    public virtual void OnFixedUpdate() { }
    public virtual void OnLateUpdate() { }
    public virtual void OnGUI() { }
}
```

---

## Properties

### Info

```csharp
public ModInfo Info { get; internal set; }
```

Metadata about your mod, populated from the `[Mod]` attribute. Contains:
- `Id` - Unique identifier
- `Name` - Display name
- `Version` - Version string
- `Author` - Author name
- `Description` - Brief description
- `FilePath` - Absolute path to the mod DLL

**Example:**
```csharp
public override void OnLoad()
{
    Logger.Info($"Loading {Info.Name} v{Info.Version} by {Info.Author}");
}
```

### Logger

```csharp
public ModLogger Logger { get; internal set; }
```

Logger instance for this mod. All messages are prefixed with the mod name and written to `MDB/Logs/Mods.log`.

See [ModLogger API](logger) for details.

---

## Lifecycle Methods

All lifecycle methods are `virtual` - override only the ones you need.

### OnLoad()

```csharp
public virtual void OnLoad()
```

Called once when the mod is loaded. Use this for initialization:
- Register ImGui callbacks
- Set up patching (if not using attributes)
- Initialize data structures
- Log startup messages

**Timing:** Called immediately after the mod DLL is loaded, before the game's main loop starts.

**Example:**
```csharp
public override void OnLoad()
{
    Logger.Info("Mod initialized!");
    ImGuiManager.RegisterCallback(DrawUI, "My Window");
}
```

### OnUpdate()

```csharp
public virtual void OnUpdate()
```

Called every frame during Unity's Update loop.

**Frequency:** ~60 Hz (varies with game framerate)

**Use for:**
- Frame-by-frame game logic
- Input polling
- Non-physics updates
- State management

**Example:**
```csharp
private int frameCount = 0;

public override void OnUpdate()
{
    frameCount++;
    if (frameCount % 60 == 0)
        Logger.Info($"60 frames passed, total: {frameCount}");
}
```

**Performance Note:** This runs every frame. Keep logic lightweight or use timers to run expensive operations less frequently.

### OnFixedUpdate()

```csharp
public virtual void OnFixedUpdate()
```

Called at fixed time intervals during Unity's FixedUpdate loop.

**Frequency:** Fixed timestep (usually 50 Hz / 0.02s, but game-dependent)

**Use for:**
- Physics-related updates
- Anything requiring consistent timing
- Movement and forces

**Example:**
```csharp
public override void OnFixedUpdate()
{
    // Apply constant velocity to player
    // Runs at fixed intervals regardless of framerate
}
```

### OnLateUpdate()

```csharp
public virtual void OnLateUpdate()
```

Called after all Update callbacks during Unity's LateUpdate loop.

**Frequency:** ~60 Hz (varies with game framerate)

**Use for:**
- Camera follow logic
- Updates that depend on other updates completing first
- Finalization logic

**Example:**
```csharp
public override void OnLateUpdate()
{
    // Update camera position after player has moved
}
```

### OnGUI()

```csharp
public virtual void OnGUI()
```

Called during Unity's GUI rendering phase for ImGui rendering.

**Frequency:** ~60 Hz (varies with game framerate)

**Use for:**
- Drawing ImGui windows (if not using `ImGuiManager.RegisterCallback`)
- Custom ImGui rendering

**Note:** Most mods should use `ImGuiManager.RegisterCallback` in `OnLoad()` instead of overriding `OnGUI()` directly.

**Example:**
```csharp
public override void OnGUI()
{
    if (ImGui.Begin("Direct GUI"))
    {
        ImGui.Text("Rendered in OnGUI");
    }
    ImGui.End();
}
```

---

## Complete Example

```csharp
using System;
using GameSDK.ModHost;
using GameSDK.ModHost.ImGui;

namespace ExampleMod
{
    [Mod("Author.Example", "Example Mod", "1.0.0",
         Author = "Your Name",
         Description = "Demonstrates ModBase lifecycle")]
    public class ExampleMod : ModBase
    {
        private int updateCount = 0;
        private int fixedUpdateCount = 0;
        private bool showWindow = true;

        public override void OnLoad()
        {
            // Initialization
            Logger.Info($"{Info.Name} v{Info.Version} loading...");
            Logger.Info($"Mod file: {Info.FilePath}");
            
            // Register UI
            ImGuiManager.RegisterCallback(DrawUI, "Example Window");
            
            Logger.Info("Mod loaded successfully!");
        }

        public override void OnUpdate()
        {
            // Every frame
            updateCount++;
            
            // Example: Log every 300 frames (5 seconds @ 60fps)
            if (updateCount % 300 == 0)
            {
                Logger.Debug($"Updates: {updateCount}, Fixed: {fixedUpdateCount}");
            }
        }

        public override void OnFixedUpdate()
        {
            // Fixed timestep
            fixedUpdateCount++;
        }

        public override void OnLateUpdate()
        {
            // After all updates
            // Could do camera work here
        }

        private void DrawUI()
        {
            if (!showWindow) return;
            
            if (ImGui.Begin("Example Window", ref showWindow))
            {
                ImGui.Text($"Update calls: {updateCount}");
                ImGui.Text($"FixedUpdate calls: {fixedUpdateCount}");
                ImGui.Separator();
                
                if (ImGui.Button("Reset Counters"))
                {
                    updateCount = 0;
                    fixedUpdateCount = 0;
                    Logger.Info("Counters reset!");
                }
            }
            ImGui.End();
        }
    }
}
```

---

## Best Practices

### ✅ Do

- Override only the lifecycle methods you actually need
- Use `OnLoad()` for all initialization
- Keep `OnUpdate()` logic lightweight
- Use timers or frame counters to throttle expensive operations
- Log important events for debugging

### ❌ Don't

- Don't do heavy initialization in constructors - use `OnLoad()`
- Don't block in lifecycle methods - they run on the game's main thread
- Don't leak resources - clean up in your mod if possible
- Don't assume specific framerate in `OnUpdate()` - use `Time.deltaTime` for time-based logic

---

## See Also

- [ModAttribute](modattribute) - Declaring mod metadata
- [ModLogger](logger) - Logging API
- [ImGuiManager](imgui-manager) - UI registration
- [Examples]({{ '/examples' | relative_url }}) - Working mod examples

---

[← Back to API Index]({{ '/api' | relative_url }}) | [ModAttribute →](modattribute)
