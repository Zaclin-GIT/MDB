# Plan: Unity Explorer Mod for MDB Framework

**TL;DR:** Create a real-time scene explorer UI using Unity's IMGUI system. The mod will enumerate all GameObjects, display hierarchies, and show component/property details. Performance is achieved through lazy loading, caching, and throttled updates. Several framework gaps need to be addressed first (OnGUI callback, IMGUI wrappers, Transform traversal APIs).

## Steps

1. **Add `OnGUI()` callback to ModBase.cs** — The current lifecycle lacks GUI rendering support; add `public virtual void OnGUI() { }` and hook it properly in `ModManager`.

2. **Hook OnGUI in native bridge** — Unlike `OnUpdate`, IMGUI must run on Unity's main thread during rendering. Add a Unity hook (via `GameObject` with `MonoBehaviour` script injection or find/create a `GUICallback` bridge) in bridge_exports.cpp.

3. **Ensure IMGUI wrapper generation** — Verify wrapper_generator.py generates `GUI`, `GUILayout`, `GUIStyle`, `GUISkin`, `Rect`, `Event`, `Input`, `KeyCode` classes from the target game's `dump.cs`.

4. **Create `UnityExplorerMod.cs`** — Implement the main mod class with:
   - Scene tree view (collapsible hierarchy via `GUILayout.Toggle`)
   - Object inspector panel (position, rotation, scale, name, tag, layer, active state)
   - Component list with expandable property viewers
   - Caching + throttled refresh (e.g., hierarchy refresh every 0.5s, selected object updates every frame)

5. **Implement performance optimizations** — Use lazy loading (only enumerate children when expanded), cache `Il2CppClass*` and method pointers, batch P/Invoke calls, and limit reflection depth for component properties.

## Further Considerations

1. **OnGUI threading** — IMGUI requires Unity's main thread. Option A: Inject a hidden `MonoBehaviour` to forward `OnGUI()` to managed code. Option B: Hook the game's existing GUI rendering. **Recommend Option A for reliability.**

2. **Input handling** — Need `Input.GetKeyDown()` and `KeyCode` wrappers for toggling the UI (e.g., F2). Verify these exist in generated wrappers or add to skip/include lists.

3. **Real-time position updates** — For the selected object, update transform values every frame (in `OnGUI` or `OnUpdate`). For the full hierarchy list, throttle to every 500ms to avoid performance issues in scenes with thousands of objects.

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                        UnityExplorerMod                             │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  ┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐  │
│  │  SceneTreeView  │    │ ObjectInspector │    │ComponentInspector│ │
│  │                 │    │                 │    │                 │  │
│  │ - Root objects  │───▶│ - Transform     │───▶│ - Component[]   │  │
│  │ - Expandable    │    │ - Name/Tag/Layer│    │ - Properties    │  │
│  │ - Search filter │    │ - Active state  │    │ - Methods       │  │
│  └─────────────────┘    └─────────────────┘    └─────────────────┘  │
│           │                      │                      │           │
│           ▼                      ▼                      ▼           │
│  ┌─────────────────────────────────────────────────────────────────┐│
│  │                      Caching Layer                              ││
│  │  - GameObjectCache (hierarchy, throttled refresh)               ││
│  │  - ComponentCache (per-object, lazy loaded)                     ││
│  │  - PropertyCache (per-component, on-demand)                     ││
│  └─────────────────────────────────────────────────────────────────┘│
│           │                                                         │
│           ▼                                                         │
│  ┌─────────────────────────────────────────────────────────────────┐│
│  │                    IL2CPP Bridge Calls                          ││
│  │  - FindObjectsOfType<GameObject>                                ││
│  │  - Transform.GetChild(i), childCount, parent                    ││
│  │  - GetComponents<Component>()                                   ││
│  └─────────────────────────────────────────────────────────────────┘│
│                                                                     │
└─────────────────────────────────────────────────────────────────────┘
```

## Required IMGUI Wrappers

| Class | Key Methods/Properties |
|-------|------------------------|
| `GUI` | `Button`, `Label`, `TextField`, `Toggle`, `Box`, `Window`, `DragWindow`, `skin` |
| `GUILayout` | `Button`, `Label`, `TextField`, `Toggle`, `BeginHorizontal/Vertical`, `BeginScrollView`, `EndScrollView`, `ExpandWidth/Height`, `Width`, `Height`, `FlexibleSpace` |
| `GUIStyle` | `normal`, `hover`, `fontSize`, `fontStyle`, `alignment` |
| `GUISkin` | `button`, `label`, `textField`, `toggle`, `box`, `window`, `scrollView` |
| `Rect` | Constructor, `x`, `y`, `width`, `height`, `Contains` |
| `Event` | `current`, `type`, `keyCode`, `Use` |
| `Input` | `GetKeyDown`, `GetKey`, `GetKeyUp`, `mousePosition` |
| `KeyCode` | `F2`, `Escape`, `Return`, etc. (enum) |
| `Screen` | `width`, `height` |

## Required Transform Wrappers

| Property/Method | Purpose |
|-----------------|---------|
| `parent` | Navigate up hierarchy |
| `childCount` | Count children |
| `GetChild(int)` | Access child by index |
| `position` | World position |
| `localPosition` | Local position |
| `rotation` | World rotation |
| `localRotation` | Local rotation |
| `localScale` | Scale |
| `SetParent(Transform, bool)` | Reparent (optional, for editing) |

## Required GameObject Wrappers

| Property/Method | Purpose |
|-----------------|---------|
| `name` | Display name |
| `tag` | Tag string |
| `layer` | Layer int |
| `activeSelf` | Is active |
| `activeInHierarchy` | Is active considering parents |
| `SetActive(bool)` | Toggle active (optional, for editing) |
| `GetComponents<Component>()` | List all components |
| `transform` | Get transform |

## Performance Strategy

### 1. Throttled Hierarchy Refresh
```csharp
private float _lastHierarchyRefresh = 0f;
private const float HIERARCHY_REFRESH_INTERVAL = 0.5f; // 500ms

void RefreshHierarchyIfNeeded()
{
    float time = Time.time; // or use stopwatch
    if (time - _lastHierarchyRefresh > HIERARCHY_REFRESH_INTERVAL)
    {
        RefreshHierarchy();
        _lastHierarchyRefresh = time;
    }
}
```

### 2. Lazy Child Enumeration
Only enumerate children when a node is expanded:
```csharp
Dictionary<IntPtr, List<GameObjectNode>> _childrenCache;
HashSet<IntPtr> _expandedNodes;

void DrawNode(GameObjectNode node)
{
    bool isExpanded = _expandedNodes.Contains(node.Ptr);
    bool newExpanded = GUILayout.Toggle(isExpanded, node.Name);
    
    if (newExpanded != isExpanded)
    {
        if (newExpanded)
        {
            _expandedNodes.Add(node.Ptr);
            // Lazy load children now
            LoadChildren(node);
        }
        else
        {
            _expandedNodes.Remove(node.Ptr);
        }
    }
    
    if (isExpanded && _childrenCache.TryGetValue(node.Ptr, out var children))
    {
        foreach (var child in children)
            DrawNode(child);
    }
}
```

### 3. Selected Object Real-time Updates
Only the selected object updates every frame:
```csharp
void DrawInspector()
{
    if (_selectedObject == null) return;
    
    // These update every frame (in OnGUI)
    var transform = _selectedObject.transform;
    Vector3 pos = transform.position;
    Vector3 rot = transform.rotation.eulerAngles;
    Vector3 scale = transform.localScale;
    
    GUILayout.Label($"Position: {pos}");
    GUILayout.Label($"Rotation: {rot}");
    GUILayout.Label($"Scale: {scale}");
}
```

### 4. Component Caching
Cache component lists per object, refresh on demand:
```csharp
Dictionary<IntPtr, Component[]> _componentCache;

Component[] GetComponents(GameObject obj)
{
    if (!_componentCache.TryGetValue(obj.NativePtr, out var components))
    {
        components = obj.GetComponents<Component>();
        _componentCache[obj.NativePtr] = components;
    }
    return components;
}
```

## OnGUI Hook Implementation Options

### Option A: MonoBehaviour Injection (Recommended)
1. Create a hidden GameObject at runtime
2. Attach a custom MonoBehaviour component (need to generate IL2CPP class or find existing)
3. Hook its `OnGUI` method to call back into managed code
4. Challenge: Creating new MonoBehaviour types at runtime in IL2CPP

### Option B: Function Pointer Hook
1. Find Unity's GUI rendering function in GameAssembly.dll
2. Hook it with a detour
3. Call managed `OnGUI` before/after original
4. More invasive but doesn't require MonoBehaviour

### Option C: Coroutine-based GUI
1. Use existing GameObject's coroutine system
2. `WaitForEndOfFrame` then draw GUI
3. May not have proper IMGUI timing

**Recommended: Option A with fallback to Option B**

## File Structure

```
Mod Development/
├── UnityExplorer/
│   ├── UnityExplorerMod.cs      # Main mod class
│   ├── UnityExplorerMod.csproj  # Project file
│   ├── UI/
│   │   ├── ExplorerWindow.cs    # Main window layout
│   │   ├── SceneTreeView.cs     # Hierarchy panel
│   │   ├── ObjectInspector.cs   # Selected object details
│   │   └── ComponentViewer.cs   # Component property display
│   ├── Core/
│   │   ├── SceneCache.cs        # Hierarchy caching
│   │   ├── GameObjectNode.cs    # Tree node data structure
│   │   └── ReflectionHelper.cs  # IL2CPP reflection utilities
│   └── Utils/
│       ├── GUIHelper.cs         # IMGUI convenience methods
│       └── ColorScheme.cs       # UI colors/styles
```

## Open Questions

1. **MonoBehaviour injection feasibility** — Can we create/instantiate IL2CPP MonoBehaviour types at runtime without AOT compilation?

2. **GUI availability** — Do all IL2CPP games include UnityEngine.IMGUIModule? Need fallback for games that stripped it.

3. **Property editing** — Should the inspector allow editing values (position, rotation, etc.) or be read-only initially?

4. **Search functionality** — Add GameObject search by name? By component type?

5. **Persistence** — Save expanded nodes, window position between sessions?
