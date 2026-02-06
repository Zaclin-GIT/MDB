# MDB Framework — Example Mods

Four example mods of increasing complexity that together demonstrate **every** major framework API.

| Example | Difficulty | Files | Key Topics |
|---------|-----------|-------|------------|
| [HelloWorld](HelloWorld/) | 🟢 Simple | 1 source file | Lifecycle, Logger, basic ImGui |
| [UnityDebugInterceptor](UnityDebugInterceptor/) | 🟢 Simple | 1 source file | Declarative patching, `[Prefix]`, Debug.Log hooking |
| [GameStats](GameStats/) | 🟡 Medium | 1 source file | Patching, IL2CPP Bridge, HookManager, advanced ImGui |
| [MDB_Explorer_ImGui](MDB_Explorer_ImGui/) | 🔴 Complex | 7 source files | Full IL2CPP reflection, scene traversal, deobfuscation, custom ImGui bindings |

### Shared Files

- **[UnityStubs.cs](UnityStubs.cs)** — Stripped-down universal Unity Engine wrappers copied from the generated output. Contains `Time`, `Screen`, `Application`, `Camera`, `GameObject`, `Transform`, `Debug`, `Object`, `Component`, `SceneManager`, and all supporting structs/enums. Both HelloWorld and GameStats link this file so they compile standalone without needing the full generated wrapper set. All examples target **only universal Unity types** (no game-specific classes) so they work across any Unity IL2CPP game.

---

## API Coverage Matrix

The table below shows which API each example covers. Start with HelloWorld and work your way up.

### Core Framework

| API | HelloWorld | DebugInterceptor | GameStats | Explorer |
|-----|:----------:|:----------------:|:---------:|:--------:|
| `[Mod]` attribute | ✅ | ✅ | ✅ | ✅ |
| `ModBase.OnLoad()` | ✅ | ✅ | ✅ | ✅ |
| `ModBase.OnUpdate()` | ✅ | | ✅ | |
| `ModBase.OnLateUpdate()` | | | ✅ | |
| `Logger.Info/Warning/Error/Debug` | ✅ | ✅ | ✅ | ✅ |
| `ModLogger.LogInternal` | | ✅ | | |

### Patching System

| API | HelloWorld | DebugInterceptor | GameStats | Explorer |
|-----|:----------:|:----------------:|:---------:|:--------:|
| `[Patch("ns", "type")]` | | ✅ | ✅ | |
| `[PatchMethod("name", count)]` | | ✅ | ✅ | |
| `[PatchRva(0x...)]` | | | ✅ | |
| `[Prefix]` (skip original) | | ✅ | ✅ | |
| `[Postfix]` (modify result) | | | ✅ | |
| `[Finalizer]` (catch errors) | | | ✅ | |
| Special params (`__instance`, `__result`, `__exception`) | | ✅ | ✅ | |

### Manual Hooking (HookManager)

| API | HelloWorld | DebugInterceptor | GameStats | Explorer |
|-----|:----------:|:----------------:|:---------:|:--------:|
| `HookManager.CreateHook()` | | | ✅ | |
| `HookManager.SetHookEnabled()` | | | ✅ | |
| `HookManager.RemoveHook()` | | | ✅ | |
| `HookManager.RemoveAllHooks()` | | | ✅ | |
| `HookManager.GetAllHooks()` | | | ✅ | |
| `HookCallback` delegate | | | ✅ | |

### IL2CPP Bridge

| API | HelloWorld | DebugInterceptor | GameStats | Explorer |
|-----|:----------:|:----------------:|:---------:|:--------:|
| `mdb_find_class` | | | ✅ | ✅ |
| `mdb_get_method` / `mdb_get_method_pointer` | | | ✅ | ✅ |
| `mdb_invoke_method` | | | ✅ | ✅ |
| `mdb_get_method_info` | | | ✅ | ✅ |
| `mdb_get_field` / `mdb_get_field_offset` | | | ✅ | ✅ |
| `mdb_field_get_value` / `mdb_field_static_get_value` | | | ✅ | ✅ |
| `mdb_field_set_value_direct` | | | | ✅ |
| `mdb_class_get_field_count/by_index` | | | ✅ | ✅ |
| `mdb_class_get_method_count/by_index` | | | ✅ | ✅ |
| `mdb_class_get_property_count/by_index` | | | | ✅ |
| `mdb_class_get_parent` | | | ✅ | ✅ |
| `mdb_object_get_class` / `mdb_class_get_name` | | | ✅ | ✅ |
| `mdb_string_new` / `mdb_string_to_utf8` | | | | ✅ |
| `mdb_gameobject_get_components` | | | | ✅ |
| `mdb_transform_*` helpers | | | | ✅ |
| `mdb_scenemanager_*` helpers | | | | ✅ |
| `mdb_array_*` helpers | | | | ✅ |
| `mdb_hook_get_count` | | | ✅ | |

### ImGui (via `GameSDK.ImGui` or direct bindings)

| API | HelloWorld | DebugInterceptor | GameStats | Explorer |
|-----|:----------:|:----------------:|:---------:|:--------:|
| `ImGuiManager.RegisterCallback` | ✅ | | ✅ | |
| `ImGuiManager.SetCallbackEnabled` | | | ✅ | |
| `ImGuiPriority` levels | ✅ | | ✅ | |
| Direct bridge integration | | | | ✅ |
| `Begin/End` (window + close button) | ✅ | | ✅ | ✅ |
| `BeginChild/EndChild` | | | ✅ | ✅ |
| `SetNextWindowSize/Pos` | ✅ | | ✅ | ✅ |
| `Text`, `TextColored`, `TextDisabled`, `TextWrapped` | ✅ | | ✅ | ✅ |
| `BulletText` | ✅ | | ✅ | ✅ |
| `Button`, `SmallButton` | ✅ | | ✅ | ✅ |
| `Checkbox` | ✅ | | ✅ | ✅ |
| `InputText` | ✅ | | ✅ | ✅ |
| `InputInt`, `InputFloat` | | | ✅ | ✅ |
| `SliderFloat`, `SliderInt` | ✅ | | ✅ | |
| `CollapsingHeader` | ✅ | | ✅ | ✅ |
| `TreeNode/TreeNodeEx/TreePop` | | | ✅ | ✅ |
| `BeginCombo/EndCombo`, `Selectable` | | | ✅ | ✅ |
| `BeginMenuBar/EndMenuBar` | | | ✅ | ✅ |
| `BeginMenu/EndMenu`, `MenuItem` | | | ✅ | ✅ |
| `BeginMainMenuBar/EndMainMenuBar` | | | | ✅ |
| `Separator`, `SameLine`, `Spacing` | ✅ | | ✅ | ✅ |
| `Indent/Unindent` | | | | ✅ |
| `SetNextItemWidth` | | | ✅ | ✅ |
| `PushStyleColor/PopStyleColor` | ✅ | | ✅ | ✅ |
| `PushID/PopID` | | | ✅ | ✅ |
| `SetTooltip`, `BeginTooltip/EndTooltip` | | | ✅ | ✅ |
| `BeginPopup/EndPopup`, `OpenPopup`, `CloseCurrentPopup` | | | ✅ | ✅ |
| `BeginPopupContextItem` | | | ✅ | ✅ |
| `IsItemClicked`, `IsItemHovered` | | | ✅ | ✅ |
| `SetClipboardText` | | | ✅ | ✅ |
| `DrawList` overlay (`DrawRect`, `DrawText`, etc.) | | | ✅ | |
| `ColorToU32` | | | ✅ | |
| `ImGuiWindowFlags` | ✅ | | ✅ | ✅ |
| `ImGuiTreeNodeFlags` | | | ✅ | ✅ |

---

## Quick Start

### Building any example

```bash
cd Documentation/Examples/<ExampleName>
dotnet restore
dotnet build -c Release
```

### Installing

Copy the output DLL from `bin/Release/` into `<GameDir>/MDB/Mods/`.

### Project template

Every example uses this `.csproj` pattern:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net481</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <OutputType>Library</OutputType>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="System.Numerics.Vectors" Version="4.5.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\..\MDB_Core\MDB_Core.csproj" />
  </ItemGroup>
  <!-- Link the shared generated Unity wrappers -->
  <ItemGroup>
    <Compile Include="..\UnityStubs.cs" Link="UnityStubs.cs" />
  </ItemGroup>
</Project>
```

### Minimal mod skeleton

```csharp
using GameSDK;
using GameSDK.ModHost;

[Mod("Author.MyMod", "My Mod", "1.0.0")]
public class MyMod : ModBase
{
    public override void OnLoad()
    {
        Logger.Info("Hello from MyMod!");
    }
}
```
