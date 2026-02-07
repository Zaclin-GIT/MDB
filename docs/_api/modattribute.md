---
layout: default
title: ModAttribute API
---

# ModAttribute API

The `[Mod]` attribute marks a class as a mod and provides metadata about it. Every mod must have exactly one class decorated with this attribute.

**Namespace:** `GameSDK.ModHost`

---

## Attribute Definition

```csharp
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class ModAttribute : Attribute
{
    public string Id { get; }
    public string Name { get; }
    public string Version { get; }
    public string Author { get; set; }
    public string Description { get; set; }
}
```

---

## Constructor

```csharp
[Mod(string id, string name, string version)]
```

### Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `id` | `string` | **Required.** Unique identifier for your mod. Use reverse-domain notation (e.g., "Author.ModName") |
| `name` | `string` | **Required.** Display name shown in logs and UI |
| `version` | `string` | **Required.** Version string (e.g., "1.0.0"). Recommend semantic versioning |

### Optional Properties

| Property | Type | Description | Default |
|----------|------|-------------|---------|
| `Author` | `string` | Mod author name | `"Unknown"` |
| `Description` | `string` | Brief description of what the mod does | `""` |

---

## Usage

### Basic Example

```csharp
using GameSDK.ModHost;

[Mod("JohnDoe.HelloWorld", "Hello World", "1.0.0")]
public class HelloWorldMod : ModBase
{
    public override void OnLoad()
    {
        Logger.Info("Hello World!");
    }
}
```

### Full Example with All Properties

```csharp
[Mod("JohnDoe.AdvancedMod", "Advanced Mod", "2.1.0",
     Author = "John Doe",
     Description = "An advanced mod that does cool things")]
public class AdvancedMod : ModBase
{
    public override void OnLoad()
    {
        Logger.Info($"{Info.Name} v{Info.Version} by {Info.Author}");
        Logger.Info($"Description: {Info.Description}");
    }
}
```

---

## ID Naming Guidelines

The `id` parameter should uniquely identify your mod. Follow these conventions:

### ✅ Good IDs

- `"Author.ModName"` - Simple and clear
- `"Author.GameName.Feature"` - Game-specific mod
- `"com.author.modname"` - Reverse domain notation
- `"AuthorName.MyAwesomeMod"` - Descriptive

### ❌ Bad IDs

- `"mod1"` - Too generic, likely to conflict
- `"My Mod"` - Contains spaces (prefer dots or underscores)
- `"test"` - Not descriptive
- `"ModName"` - Missing author identifier

---

## Version String Format

Recommend using [Semantic Versioning](https://semver.org/):

```
MAJOR.MINOR.PATCH
```

- **MAJOR** - Breaking changes
- **MINOR** - New features (backward-compatible)
- **PATCH** - Bug fixes (backward-compatible)

### Examples

```csharp
[Mod("Author.Mod", "My Mod", "1.0.0")]    // Initial release
[Mod("Author.Mod", "My Mod", "1.1.0")]    // Added features
[Mod("Author.Mod", "My Mod", "1.1.1")]    // Bug fix
[Mod("Author.Mod", "My Mod", "2.0.0")]    // Breaking changes
```

---

## Runtime Access

The attribute values are accessible at runtime through `ModBase.Info`:

```csharp
public override void OnLoad()
{
    Logger.Info($"ID: {Info.Id}");
    Logger.Info($"Name: {Info.Name}");
    Logger.Info($"Version: {Info.Version}");
    Logger.Info($"Author: {Info.Author}");
    Logger.Info($"Description: {Info.Description}");
    Logger.Info($"File: {Info.FilePath}");
}
```

---

## Multiple Mods in One DLL

**Not supported.** Each DLL should contain exactly one class with the `[Mod]` attribute. If you need multiple mods, create separate DLL projects.

### ❌ Don't Do This

```csharp
// Mod1.cs
[Mod("Author.Mod1", "Mod 1", "1.0.0")]
public class Mod1 : ModBase { }

// Mod2.cs - Same DLL
[Mod("Author.Mod2", "Mod 2", "1.0.0")]
public class Mod2 : ModBase { }
```

The framework will only load one of them (undefined behavior).

### ✅ Do This Instead

Create two separate projects, each with one mod class.

---

## Examples from Framework

### HelloWorld

```csharp
[Mod("MDB.Examples.HelloWorld", "Hello World", "1.0.0",
     Author = "MDB Framework",
     Description = "A simple example mod demonstrating the basics")]
public class HelloWorldMod : ModBase
{
    // ...
}
```

### UnityDebugInterceptor

```csharp
[Mod("MDB.Examples.UnityDebugInterceptor", "Unity Debug Interceptor", "1.0.0",
     Author = "MDB Framework",
     Description = "Intercepts Unity Debug.Log calls")]
public class UnityDebugInterceptor : ModBase
{
    // ...
}
```

### GameStats

```csharp
[Mod("MDB.Examples.GameStats", "Game Stats Dashboard", "1.0.0",
     Author = "MDB Framework",
     Description = "Demonstrates patching, IL2CPP bridge, and ImGui")]
public class GameStatsMod : ModBase
{
    // ...
}
```

---

## Common Mistakes

### Missing Required Parameters

```csharp
// ❌ Error: Missing required parameters
[Mod("Author.Mod")]
public class MyMod : ModBase { }
```

```csharp
// ✅ Correct: All required parameters
[Mod("Author.Mod", "My Mod", "1.0.0")]
public class MyMod : ModBase { }
```

### Wrong Target

```csharp
// ❌ Error: Applied to method instead of class
public class MyMod : ModBase
{
    [Mod("Author.Mod", "My Mod", "1.0.0")]
    public override void OnLoad() { }
}
```

```csharp
// ✅ Correct: Applied to class
[Mod("Author.Mod", "My Mod", "1.0.0")]
public class MyMod : ModBase
{
    public override void OnLoad() { }
}
```

### Missing Inheritance

```csharp
// ❌ Error: Doesn't inherit ModBase
[Mod("Author.Mod", "My Mod", "1.0.0")]
public class MyMod
{
    // Won't be loaded!
}
```

```csharp
// ✅ Correct: Inherits ModBase
[Mod("Author.Mod", "My Mod", "1.0.0")]
public class MyMod : ModBase
{
    // Will be loaded
}
```

---

## See Also

- [ModBase]({{ '/api/modbase' | relative_url }}) - Base class with lifecycle methods
- [ModInfo]({{ '/api/modbase' | relative_url }}#info) - Runtime metadata access
- [Getting Started]({{ '/getting-started' | relative_url }}) - Creating your first mod

---

[← Back to API Index]({{ '/api' | relative_url }}) | [ModLogger →]({{ '/api/logger' | relative_url }})
