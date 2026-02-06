---
layout: default
title: ModLogger API
---

# ModLogger API

`ModLogger` provides color-coded logging for your mod. Each mod gets its own logger instance that prefixes all messages with the mod name.

**Namespace:** `GameSDK.ModHost`

---

## Class Definition

```csharp
public class ModLogger
{
    public void Info(string message);
    public void Warning(string message);
    public void Error(string message);
    public void Debug(string message);
}
```

---

## Accessing the Logger

Every mod automatically gets a logger instance via `ModBase.Logger`:

```csharp
[Mod("Author.MyMod", "My Mod", "1.0.0")]
public class MyMod : ModBase
{
    public override void OnLoad()
    {
        Logger.Info("Mod loaded!");  // Access via Logger property
    }
}
```

---

## Methods

### Info()

```csharp
public void Info(string message)
```

Logs an informational message.

**Console Color:** Blue  
**Log Level:** `[INFO]`

**Example:**
```csharp
Logger.Info("Player health initialized to 100");
```

**Output:**
```
[INFO] [MyMod] Player health initialized to 100
```

---

### Warning()

```csharp
public void Warning(string message)
```

Logs a warning message. Use for non-critical issues.

**Console Color:** Yellow  
**Log Level:** `[WARN]`

**Example:**
```csharp
Logger.Warning("Configuration file not found, using defaults");
```

**Output:**
```
[WARN] [MyMod] Configuration file not found, using defaults
```

---

### Error()

```csharp
public void Error(string message)
```

Logs an error message. Use for errors and exceptions.

**Console Color:** Red  
**Log Level:** `[ERROR]`

**Example:**
```csharp
try
{
    // Some operation
}
catch (Exception ex)
{
    Logger.Error($"Failed to load config: {ex.Message}");
}
```

**Output:**
```
[ERROR] [MyMod] Failed to load config: File not found
```

---

### Debug()

```csharp
public void Debug(string message)
```

Logs a debug message. Use for verbose development output.

**Console Color:** Gray  
**Log Level:** `[DEBUG]`

**Example:**
```csharp
Logger.Debug($"Method called with parameter: {value}");
```

**Output:**
```
[DEBUG] [MyMod] Method called with parameter: 42
```

---

## Output Destinations

All log messages are written to:

1. **File:** `MDB/Logs/Mods.log`
   - Persistent log file
   - Appended to on each game launch
   - Includes timestamps

2. **Console:** MDB console window (if available)
   - Color-coded by level
   - Real-time output

---

## Examples

### Basic Logging

```csharp
public override void OnLoad()
{
    Logger.Info("Starting initialization...");
    Logger.Debug("Debug mode enabled");
    
    if (SomeCondition())
        Logger.Warning("Non-optimal configuration detected");
    
    try
    {
        DoSomething();
        Logger.Info("Operation completed successfully");
    }
    catch (Exception ex)
    {
        Logger.Error($"Operation failed: {ex.Message}");
    }
}
```

### Structured Logging

```csharp
public override void OnUpdate()
{
    if (updateCount % 300 == 0)  // Every 5 seconds @ 60fps
    {
        Logger.Info($"Stats - Frames: {updateCount}, FPS: {GetFPS():F1}");
    }
}

private void OnPlayerDamage(int damage, string source)
{
    Logger.Debug($"Player took {damage} damage from {source}");
}

private void OnConfigLoad(string path)
{
    Logger.Info($"Loading config from: {path}");
    
    if (!File.Exists(path))
    {
        Logger.Warning($"Config not found: {path}");
        Logger.Info("Creating default configuration");
        CreateDefaultConfig(path);
    }
}
```

### Exception Logging

```csharp
public override void OnLoad()
{
    try
    {
        LoadConfiguration();
        InitializeComponents();
        RegisterHooks();
        Logger.Info("Mod initialized successfully");
    }
    catch (FileNotFoundException ex)
    {
        Logger.Error($"Required file missing: {ex.FileName}");
    }
    catch (Exception ex)
    {
        Logger.Error($"Initialization failed: {ex.GetType().Name}");
        Logger.Error($"Message: {ex.Message}");
        Logger.Error($"Stack: {ex.StackTrace}");
    }
}
```

---

## Best Practices

### ✅ Do

- Use `Info()` for important events (initialization, major state changes)
- Use `Warning()` for non-critical issues (fallback behavior, deprecated features)
- Use `Error()` for actual errors (exceptions, failed operations)
- Use `Debug()` for verbose development output
- Include context in messages (values, states, paths)
- Log exceptions with full details

### ❌ Don't

- Don't log every frame in `OnUpdate()` (causes performance issues and log spam)
- Don't log sensitive information (passwords, tokens, personal data)
- Don't use `Error()` for warnings or info
- Don't use complex string formatting in tight loops
- Don't forget to log important errors

---

## Performance Considerations

### Throttle High-Frequency Logging

```csharp
private int frameCount = 0;

public override void OnUpdate()
{
    frameCount++;
    
    // ✅ Log every 60 frames instead of every frame
    if (frameCount % 60 == 0)
    {
        Logger.Debug($"Frame {frameCount}");
    }
}
```

### Use String Interpolation Wisely

```csharp
// ❌ Formats even if Debug isn't called
string message = $"Complex calculation: {ExpensiveOperation()}";
Logger.Debug(message);

// ✅ Only formats if you actually call the log method
Logger.Debug($"Complex calculation: {ExpensiveOperation()}");
```

---

## Log File Location

Logs are written to:

```
<GameFolder>/MDB/Logs/Mods.log
```

The log file is created automatically on first mod load and appended to on subsequent launches.

### Log Format

```
[TIMESTAMP] [LEVEL] [ModName] Message
```

Example:
```
[2024-01-15 14:23:45] [INFO] [MyMod] Mod loaded successfully
[2024-01-15 14:23:45] [WARN] [MyMod] Config not found, using defaults
[2024-01-15 14:23:50] [ERROR] [OtherMod] Failed to hook method
```

---

## Advanced: Custom Logging

If you need custom logging behavior, you can access the internal log method:

```csharp
ModLogger.LogInternal(string level, string modName, string message, ConsoleColor color)
```

**Note:** This is advanced usage and rarely needed. The standard methods cover most cases.

---

## Troubleshooting

### Logs not appearing?

1. Check the file exists: `MDB/Logs/Mods.log`
2. Ensure your mod is loading (check `MDB/Logs/MDB.log`)
3. Verify you're calling the logger methods
4. Make sure you're accessing `Logger` property (not creating your own instance)

### Console not showing colors?

- Colors only work in MDB's console window
- Standard terminals may not support colors
- The log file doesn't include color codes

---

## See Also

- [ModBase](modbase) - Base class that provides Logger property
- [Getting Started](../getting-started#testing) - Testing and viewing logs
- [Troubleshooting Guide](../guides/troubleshooting) - Common logging issues

---

[← Back to API Index](../api) | [Patch Attributes →](patch-attributes)
