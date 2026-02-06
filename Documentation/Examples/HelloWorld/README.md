# Hello World — Simple MDB Example

A minimal mod that demonstrates the core MDB framework building blocks.

## What This Demonstrates

| Feature | API Used |
|---------|----------|
| Mod metadata | `[Mod]` attribute with Id, Name, Version, Author, Description |
| Lifecycle | `ModBase.OnLoad()`, `ModBase.OnUpdate()` |
| Logging | `Logger.Info()`, `Logger.Warning()`, `Logger.Error()`, `Logger.Debug()` |
| ImGui init | `ImGuiManager.RegisterCallback()`, `ImGuiPriority` |
| ImGui windows | `ImGui.Begin()` / `End()` with close button |
| ImGui widgets | `Text`, `TextColored`, `TextDisabled`, `BulletText`, `Button`, `Checkbox`, `SliderFloat`, `InputText` |
| ImGui layout | `Separator`, `SameLine`, `Spacing`, `CollapsingHeader`, `SetNextWindowSize/Pos` |
| ImGui style | `PushStyleColor` / `PopStyleColor` |
| ImGui overlay | No-titlebar window with `ImGuiWindowFlags` |
| IL2CPP bridge | Static field read via `mdb_find_class` / `mdb_get_field` / `mdb_field_static_get_value` |

## Build

```bash
dotnet build -c Release
```

## Install

Copy `bin/Release/HelloWorld.dll` into `<GameDir>/MDB/Mods/`.

## Usage

1. Launch the game with MDB injected.
2. Press **F2** to toggle ImGui input capture.
3. The "Hello World Mod" window and overlay appear automatically.

## File Structure

```
HelloWorld/
├── HelloWorld.csproj      # Project file (references MDB_Core)
├── HelloWorldMod.cs       # Single source file — the entire mod
└── README.md              # This file
```
