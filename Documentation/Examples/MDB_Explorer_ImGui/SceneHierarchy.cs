// ==============================
// SceneHierarchy - Lightweight scene hierarchy cache
// ==============================
// Loosely adapted from MDB_Explorer's SceneCache

using System;
using System.Collections.Generic;
using System.Numerics;
using GameSDK;
using GameSDK.ModHost;

namespace MDB.Explorer.ImGui
{
    /// <summary>
    /// Lightweight scene hierarchy cache.
    /// </summary>
    public class SceneHierarchy
    {
        private const string LOG_TAG = "SceneHierarchy";

        private readonly List<HierarchyNode> _rootNodes = new List<HierarchyNode>();
        private readonly Dictionary<IntPtr, HierarchyNode> _nodeMap = new Dictionary<IntPtr, HierarchyNode>();
        private readonly HashSet<IntPtr> _expandedPtrs = new HashSet<IntPtr>();
        private readonly List<SceneInfo> _scenes = new List<SceneInfo>();
        private readonly HashSet<int> _loadedSceneHandles = new HashSet<int>(); // Track loaded scene handles to identify DDOL

        private string _currentFilter = "";
        private int _selectedSceneIndex = -1; // -1 = All Scenes
        private float _lastRefreshTime;
        private const float REFRESH_INTERVAL = 2.0f;

        // Cached IL2CPP pointers (scene-specific; core classes come from Il2CppHelpers)
        private IntPtr _sceneManagerClass;
        private IntPtr _sceneClass;
        private bool _classesResolved;

        // Rendering state
        private HierarchyNode _selectedNode;
        private string _searchFilter = "";

        /// <summary>
        /// Callback invoked when a node is selected in the hierarchy.
        /// </summary>
        public Action<HierarchyNode> OnNodeSelected { get; set; }

        public IReadOnlyList<HierarchyNode> RootNodes => _rootNodes;
        public IReadOnlyList<SceneInfo> Scenes => _scenes;
        public int TotalNodeCount => _nodeMap.Count;
        public string CurrentFilter => _currentFilter;
        public int SelectedSceneIndex
        {
            get => _selectedSceneIndex;
            set => _selectedSceneIndex = value;
        }

        /// <summary>
        /// Get the selected scene info, or null if "All Scenes" is selected.
        /// </summary>
        public SceneInfo SelectedScene => _selectedSceneIndex >= 0 && _selectedSceneIndex < _scenes.Count 
            ? _scenes[_selectedSceneIndex] 
            : null;

        /// <summary>
        /// Check if a node belongs to the currently selected scene.
        /// </summary>
        public bool IsInSelectedScene(HierarchyNode node)
        {
            if (_selectedSceneIndex < 0) return true; // "All Scenes" selected
            if (node == null) return false;
            
            var selectedScene = SelectedScene;
            if (selectedScene == null) return true;

            // For DDOL scene, check if the object's scene handle is NOT in loaded scenes
            if (selectedScene.Handle == -12345)
            {
                // DDOL objects have a scene handle that doesn't match any loaded scene
                return !_loadedSceneHandles.Contains(node.SceneHandle);
            }

            // For regular scenes, match the handle
            return node.SceneHandle == selectedScene.Handle;
        }

        /// <summary>
        /// Initialize IL2CPP class pointers.
        /// </summary>
        public bool Initialize()
        {
            if (_classesResolved) return true;

            try
            {
                // Resolve core Unity classes via shared helper
                if (!Il2CppHelpers.ResolveClasses())
                {
                    ModLogger.LogInternal(LOG_TAG, "[ERROR] Failed to resolve core Unity classes");
                    return false;
                }

                // Resolve scene-specific classes locally
                _sceneManagerClass = Il2CppBridge.mdb_find_class("UnityEngine.CoreModule", "UnityEngine.SceneManagement", "SceneManager");
                _sceneClass = Il2CppBridge.mdb_find_class("UnityEngine.CoreModule", "UnityEngine.SceneManagement", "Scene");

                _classesResolved = true;

                ModLogger.LogInternal(LOG_TAG, $"[INFO] Classes resolved. SceneManager: {_sceneManagerClass != IntPtr.Zero}");

                return _classesResolved;
            }
            catch (Exception ex)
            {
                ModLogger.LogInternal(LOG_TAG, $"[ERROR] Initialize failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Refresh the list of loaded scenes.
        /// </summary>
        public void RefreshScenes()
        {
            _scenes.Clear();
            _loadedSceneHandles.Clear();

            try
            {
                // Use native helper that properly unboxes the int result
                int sceneCount = Il2CppBridge.mdb_scenemanager_get_scene_count();
                
                ModLogger.LogInternal(LOG_TAG, $"[INFO] Scene count from native: {sceneCount}");
                
                // Add each loaded scene and track their handles
                for (int i = 0; i < sceneCount; i++)
                {
                    string name = Il2CppBridge.GetSceneName(i);
                    int handle = Il2CppBridge.mdb_scenemanager_get_scene_handle(i);
                    int rootCount = Il2CppBridge.mdb_scenemanager_get_scene_root_count(i);
                    
                    if (!string.IsNullOrEmpty(name))
                    {
                        _scenes.Add(new SceneInfo
                        {
                            Handle = handle,
                            Name = name,
                            Path = "",
                            IsLoaded = true,
                            RootCount = rootCount
                        });
                        _loadedSceneHandles.Add(handle); // Track this handle as a loaded scene
                        ModLogger.LogInternal(LOG_TAG, $"[INFO] Scene {i}: '{name}' (handle={handle}, roots={rootCount})");
                    }
                }

                // Always add DontDestroyOnLoad as a synthetic scene
                // Root count will be updated after we scan all objects
                _scenes.Add(new SceneInfo
                {
                    Handle = -12345, // Special marker for DDOL
                    Name = "DontDestroyOnLoad",
                    Path = "",
                    IsLoaded = true,
                    RootCount = 0 // Will count from actual objects
                });
                
                ModLogger.LogInternal(LOG_TAG, $"[INFO] Added {_scenes.Count} scenes (including DontDestroyOnLoad)");
            }
            catch (Exception ex)
            {
                ModLogger.LogInternal(LOG_TAG, $"[ERROR] RefreshScenes failed: {ex.Message}");
            }
        }

        private string GetSceneName(IntPtr scenePtr)
        {
            try
            {
                if (_sceneClass == IntPtr.Zero) return null;

                IntPtr method = Il2CppBridge.mdb_get_method(_sceneClass, "get_name", 0);
                if (method == IntPtr.Zero) return null;

                IntPtr exception;
                IntPtr result = Il2CppBridge.mdb_invoke_method(method, scenePtr, Array.Empty<IntPtr>(), out exception);
                if (result == IntPtr.Zero) return null;

                return Il2CppBridge.Il2CppStringToManaged(result);
            }
            catch { return null; }
        }

        private int GetSceneRootCount(IntPtr scenePtr)
        {
            try
            {
                if (_sceneClass == IntPtr.Zero) return 0;

                IntPtr method = Il2CppBridge.mdb_get_method(_sceneClass, "get_rootCount", 0);
                if (method == IntPtr.Zero) return 0;

                IntPtr exception;
                IntPtr result = Il2CppBridge.mdb_invoke_method(method, scenePtr, Array.Empty<IntPtr>(), out exception);
                
                // Validate result
                long rawCount = result.ToInt64();
                if (rawCount < 0 || rawCount > 10000) return 0;
                
                return (int)rawCount;
            }
            catch { return 0; }
        }

        /// <summary>
        /// Refresh the hierarchy from the game.
        /// </summary>
        public void Refresh()
        {
            if (!_classesResolved && !Initialize()) return;

            try
            {
                _rootNodes.Clear();
                _nodeMap.Clear();

                // Refresh scenes first
                RefreshScenes();

                // Find all GameObjects using FindObjectsOfType
                var goClass = Il2CppHelpers.GameObjectClass;
                IntPtr findMethod = Il2CppBridge.mdb_get_method(goClass, "FindObjectsOfType", 1);
                if (findMethod == IntPtr.Zero)
                {
                    // Try alternate signature
                    findMethod = Il2CppBridge.mdb_get_method(goClass, "FindObjectsOfType", 2);
                }

                if (findMethod == IntPtr.Zero)
                {
                    ModLogger.LogInternal(LOG_TAG, "[ERROR] Could not find FindObjectsOfType method");
                    return;
                }

                // Get GameObject type object
                IntPtr il2cppType = Il2CppBridge.mdb_class_get_type(goClass);
                IntPtr typeObj = Il2CppBridge.mdb_type_get_object(il2cppType);
                if (typeObj == IntPtr.Zero)
                {
                    ModLogger.LogInternal(LOG_TAG, "[ERROR] Could not get GameObject type object");
                    return;
                }

                // Call FindObjectsOfType(typeof(GameObject))
                IntPtr exception;
                IntPtr[] args = new IntPtr[] { typeObj };
                IntPtr result = Il2CppBridge.mdb_invoke_method(findMethod, IntPtr.Zero, args, out exception);

                if (exception != IntPtr.Zero || result == IntPtr.Zero)
                {
                    ModLogger.LogInternal(LOG_TAG, "[ERROR] FindObjectsOfType failed");
                    return;
                }

                // Parse array result
                int length = Il2CppBridge.mdb_array_length(result);
                ModLogger.LogInternal(LOG_TAG, $"[INFO] Found {length} GameObjects");

                for (int i = 0; i < length; i++)
                {
                    IntPtr goPtr = Il2CppBridge.mdb_array_get_element(result, i);
                    if (goPtr == IntPtr.Zero) continue;

                    // Check if this is a root object (no parent transform)
                    if (IsRootObject(goPtr))
                    {
                        var node = CreateNode(goPtr, 0);
                        if (node != null)
                        {
                            _rootNodes.Add(node);

                            // Restore expanded state
                            if (_expandedPtrs.Contains(node.Pointer))
                            {
                                node.IsExpanded = true;
                                LoadChildren(node);
                            }
                        }
                    }
                }

                // Sort by name
                _rootNodes.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

                // Update DDOL root count (count roots that are not in any loaded scene)
                int ddolCount = 0;
                foreach (var node in _rootNodes)
                {
                    if (!_loadedSceneHandles.Contains(node.SceneHandle))
                    {
                        ddolCount++;
                    }
                }
                // Update the DDOL scene info (it's the last one in the list)
                if (_scenes.Count > 0 && _scenes[_scenes.Count - 1].Handle == -12345)
                {
                    _scenes[_scenes.Count - 1].RootCount = ddolCount;
                }

                ModLogger.LogInternal(LOG_TAG, $"[INFO] Hierarchy refreshed: {_rootNodes.Count} roots, {_nodeMap.Count} total, {ddolCount} DDOL");
            }
            catch (Exception ex)
            {
                ModLogger.LogInternal(LOG_TAG, $"[ERROR] Refresh failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Refresh if enough time has passed.
        /// </summary>
        public void RefreshIfNeeded(float currentTime)
        {
            if (currentTime - _lastRefreshTime >= REFRESH_INTERVAL)
            {
                _lastRefreshTime = currentTime;
                Refresh();
            }
        }

        /// <summary>
        /// Set the search filter.
        /// </summary>
        public void SetFilter(string filter)
        {
            _currentFilter = filter ?? "";
        }

        /// <summary>
        /// Toggle node expansion.
        /// </summary>
        public void ToggleExpanded(HierarchyNode node)
        {
            if (node == null) return;

            node.IsExpanded = !node.IsExpanded;

            if (node.IsExpanded)
            {
                _expandedPtrs.Add(node.Pointer);
                if (!node.ChildrenLoaded)
                {
                    LoadChildren(node);
                }
            }
            else
            {
                _expandedPtrs.Remove(node.Pointer);
            }
        }

        /// <summary>
        /// Load children for a node.
        /// </summary>
        public void LoadChildren(HierarchyNode node)
        {
            if (node == null || node.ChildrenLoaded) return;

            try
            {
                node.Children.Clear();

                IntPtr transform = Il2CppHelpers.GetTransform(node.Pointer);
                if (transform == IntPtr.Zero) return;

                int childCount = Il2CppHelpers.GetChildCount(transform);

                for (int i = 0; i < childCount; i++)
                {
                    IntPtr childTransform = Il2CppHelpers.GetChildTransform(transform, i);
                    if (childTransform == IntPtr.Zero) continue;

                    IntPtr childGO = Il2CppHelpers.GetGameObject(childTransform);
                    if (childGO == IntPtr.Zero) continue;

                    var childNode = CreateNode(childGO, node.Depth + 1);
                    if (childNode != null)
                    {
                        childNode.Parent = node;
                        node.Children.Add(childNode);

                        // Restore expanded state
                        if (_expandedPtrs.Contains(childNode.Pointer))
                        {
                            childNode.IsExpanded = true;
                            LoadChildren(childNode);
                        }
                    }
                }

                node.Children.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
                node.ChildrenLoaded = true;
            }
            catch (Exception ex)
            {
                ModLogger.LogInternal(LOG_TAG, $"[ERROR] LoadChildren failed: {ex.Message}");
                node.ChildrenLoaded = true;
            }
        }

        /// <summary>
        /// Get a node by pointer.
        /// </summary>
        public HierarchyNode GetNode(IntPtr ptr)
        {
            _nodeMap.TryGetValue(ptr, out var node);
            return node;
        }

        // ========== Private Helpers ==========

        private HierarchyNode CreateNode(IntPtr goPtr, int depth)
        {
            if (goPtr == IntPtr.Zero) return null;
            if (_nodeMap.ContainsKey(goPtr)) return _nodeMap[goPtr];

            IntPtr transform = Il2CppHelpers.GetTransform(goPtr);
            int childCount = transform != IntPtr.Zero ? Il2CppHelpers.GetChildCount(transform) : 0;
            int sceneHandle = Il2CppBridge.mdb_gameobject_get_scene_handle(goPtr);

            var node = new HierarchyNode
            {
                Pointer = goPtr,
                Name = Il2CppHelpers.GetGameObjectName(goPtr) ?? "<unnamed>",
                IsActive = Il2CppHelpers.GetGameObjectActive(goPtr),
                Depth = depth,
                HasChildren = childCount > 0,
                ChildCount = childCount,
                SceneHandle = sceneHandle
            };

            _nodeMap[goPtr] = node;
            return node;
        }

        private bool IsRootObject(IntPtr goPtr)
        {
            IntPtr transform = Il2CppHelpers.GetTransform(goPtr);
            if (transform == IntPtr.Zero) return true;

            IntPtr parent = Il2CppHelpers.GetParentTransform(transform);
            return parent == IntPtr.Zero;
        }

        // ========== Rendering ==========

        /// <summary>
        /// Render the hierarchy panel contents (scene selector, search, tree view).
        /// Call this inside an ImGui.Begin/End block.
        /// </summary>
        public void Render()
        {
            // Scene selector dropdown
            string currentScene = _selectedSceneIndex < 0 ? "All Scenes" :
                (_selectedSceneIndex < _scenes.Count ? _scenes[_selectedSceneIndex].Name : "All Scenes");

            if (ImGui.BeginCombo("##SceneSelector", currentScene))
            {
                if (ImGui.Selectable("All Scenes", _selectedSceneIndex < 0))
                {
                    _selectedSceneIndex = -1;
                }

                for (int i = 0; i < _scenes.Count; i++)
                {
                    var scene = _scenes[i];
                    string label = $"{scene.Name} ({scene.RootCount} roots)";
                    if (ImGui.Selectable(label, _selectedSceneIndex == i))
                    {
                        _selectedSceneIndex = i;
                    }
                }
                ImGui.EndCombo();
            }

            // Search bar
            ImGui.SetNextItemWidth(-1);
            if (ImGui.InputTextWithHint("##search", "Search...", ref _searchFilter, 256))
            {
                SetFilter(_searchFilter);
            }

            ImGui.Separator();

            // Toolbar
            if (ImGui.Button("Refresh"))
            {
                Refresh();
            }
            ImGui.SameLine();
            ImGui.Text($"({_rootNodes.Count} roots, {_nodeMap.Count} total)");

            ImGui.Separator();

            // Tree view
            if (ImGui.BeginChild("TreeView"))
            {
                foreach (var node in _rootNodes)
                {
                    if (MatchesFilter(node))
                    {
                        DrawTreeNode(node);
                    }
                }
            }
            ImGui.EndChild();
        }

        private void DrawTreeNode(HierarchyNode node)
        {
            if (node == null) return;

            ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth;

            if (!node.HasChildren)
                flags |= ImGuiTreeNodeFlags.Leaf;

            if (_selectedNode?.Pointer == node.Pointer)
                flags |= ImGuiTreeNodeFlags.Selected;

            if (node.IsExpanded)
                flags |= ImGuiTreeNodeFlags.DefaultOpen;

            // Dim inactive objects
            if (!node.IsActive)
                ImGui.PushStyleColor(ImGuiCol.Text, Theme.Disabled);

            string label = node.HasChildren
                ? $"{node.Name} ({node.ChildCount})##{node.Pointer}"
                : $"{node.Name}##{node.Pointer}";

            bool opened = ImGui.TreeNodeEx(label, flags);

            if (!node.IsActive)
                ImGui.PopStyleColor();

            // Selection
            if (ImGui.IsItemClicked() && !ImGui.IsItemToggledOpen())
            {
                _selectedNode = node;
                OnNodeSelected?.Invoke(node);
            }

            // Context menu
            if (ImGui.BeginPopupContextItem("node_ctx"))
            {
                if (ImGui.MenuItem("Copy Name"))
                    ImGui.SetClipboardText(node.Name);
                if (ImGui.MenuItem($"Copy Pointer: 0x{node.Pointer.ToInt64():X}"))
                    ImGui.SetClipboardText($"0x{node.Pointer.ToInt64():X}");
                ImGui.EndPopup();
            }

            // Children
            if (opened)
            {
                if (!node.ChildrenLoaded && node.HasChildren)
                    LoadChildren(node);

                node.IsExpanded = true;

                foreach (var child in node.Children)
                {
                    if (MatchesFilter(child))
                        DrawTreeNode(child);
                }

                ImGui.TreePop();
            }
            else
            {
                node.IsExpanded = false;
            }
        }

        private bool MatchesFilter(HierarchyNode node)
        {
            // Scene filter
            if (!IsInSelectedScene(node))
                return false;

            // Text filter
            if (string.IsNullOrEmpty(_searchFilter)) return true;

            if (node.Name.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            // Check children for parent visibility
            if (node.HasChildren)
            {
                if (!node.ChildrenLoaded)
                    LoadChildren(node);

                foreach (var child in node.Children)
                {
                    if (MatchesFilter(child))
                        return true;
                }
            }

            return false;
        }
    }
}
