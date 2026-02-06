# Game Stats Dashboard — Medium-Complexity MDB Example

A mod that tracks game statistics and demonstrates the full MDB patching + IL2CPP bridge + ImGui toolkit.

## What This Demonstrates

### Patching System

| Feature | API Used |
|---------|----------|
| Target by string | `[Patch("Namespace", "TypeName")]` |
| Target method | `[PatchMethod("MethodName", paramCount)]` |
| Target by RVA | `[PatchRva(0xDEAD)]` |
| Prefix (skip original) | `[Prefix]` returning `false` |
| Postfix (modify result) | `[Postfix]` with `ref IntPtr __result` |
| Finalizer (catch errors) | `[Finalizer]` swallowing exceptions |
| Special parameters | `__instance`, `__0`/`__1`, `ref __result`, `__exception` |

### Manual Hooking (HookManager)

| Feature | API Used |
|---------|----------|
| Create hook | `HookManager.CreateHook(methodPtr, detour, out original)` |
| Enable / disable | `HookManager.SetHookEnabled(handle, bool)` |
| Remove hook | `HookManager.RemoveHook(hookInfo)` |
| Remove all | `HookManager.RemoveAllHooks()` |
| List hooks | `HookManager.GetAllHooks()` |
| Hook delegate | `HookCallback(IntPtr instance, IntPtr args, IntPtr original)` |

### IL2CPP Bridge

| Feature | API Used |
|---------|----------|
| Find class | `mdb_find_class(assembly, namespace, name)` |
| Get method | `mdb_get_method(klass, name, paramCount)` |
| Method info | `mdb_get_method_info(method, out params, out static, out returns)` |
| Method pointer | `mdb_get_method_pointer(method)` |
| Invoke method | `mdb_invoke_method(method, instance, args, out exception)` |
| Get field | `mdb_get_field(klass, name)`, `mdb_get_field_offset(field)` |
| Field enumeration | `mdb_class_get_field_count`, `mdb_class_get_field_by_index` |
| Method enumeration | `mdb_class_get_method_count`, `mdb_class_get_method_by_index` |
| Property enumeration | `mdb_class_get_property_count` |
| Class hierarchy | `mdb_class_get_parent(klass)` |
| Class / field names | `mdb_class_get_name`, `mdb_field_get_name`, `mdb_field_is_static` |
| Hook count | `mdb_hook_get_count()` |

### ImGui Widgets & Features

| Feature | API Used |
|---------|----------|
| Menu bar | `BeginMenuBar/EndMenuBar`, `BeginMenu/EndMenu`, `MenuItem` |
| Combo box (tabs) | `BeginCombo/EndCombo`, `Selectable` |
| Tree nodes | `TreeNodeEx`, `TreePop` |
| Child regions | `BeginChild/EndChild` |
| Tooltips | `SetTooltip`, hover detection |
| Popups | `OpenPopup`, `BeginPopup/EndPopup`, `CloseCurrentPopup` |
| Context menus | `BeginPopupContextItem` |
| Collapsing headers | `CollapsingHeader` |
| Text variants | `Text`, `TextColored`, `TextDisabled`, `TextWrapped`, `BulletText`, `TextError` |
| Input widgets | `InputText`, `InputInt`, `SliderFloat` |
| Buttons | `Button`, `SmallButton` |
| Checkbox | `Checkbox` |
| Style colors | `PushStyleColor/PopStyleColor` |
| ID stack | `PushID/PopID` |
| Clipboard | `SetClipboardText` |
| DrawList overlay | `DrawRectFilled`, `DrawRect`, `DrawText`, `ColorToU32` |

## Build

```bash
dotnet build -c Release
```

## Install

Copy `bin/Release/GameStats.dll` into `<GameDir>/MDB/Mods/`.

## File Structure

```
GameStats/
├── GameStats.csproj       # Project file
├── GameStatsMod.cs        # All code in one file for clarity
└── README.md              # This file
```

## Notes

- Patch targets in this example (`GameManager`, `Player`, `NetworkManager`) are **placeholders**. Replace them with actual class/method names from your game's IL2CPP dump.
- The `[PatchRva(0xDEAD)]` is a placeholder RVA. Use the IL2CPP dumper output to find real RVAs.
- Manual hooks require the target class to exist at runtime — if the class isn't found, the hook setup is gracefully skipped.
