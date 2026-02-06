// ==============================
// ExplorerMod - Main ImGui-based Unity Explorer
// ==============================

using System;
using System.Numerics;
using GameSDK;
using GameSDK.ModHost;

namespace MDB.Explorer.ImGui
{
    /// <summary>
    /// Main Unity Explorer mod using Dear ImGui for rendering.
    /// </summary>
    [Mod("MDB.Explorer.ImGui", "MDB Explorer", "0.5.0", Author = "MDB Team")]
    public class ExplorerMod : ModBase
    {
        private const string LOG_TAG = "ExplorerMod";

        // ImGui controller
        private ImGuiController _imguiController;
        private bool _imguiInitialized;

        // Explorer state
        private SceneHierarchy _hierarchy;
        private GameObjectInspector _inspector;
        private DeobfuscationPanel _deobfuscationPanel;
        private HierarchyNode _selectedNode;

        // UI state
        private bool _showExplorer = true;
        private bool _showHierarchy = false;  // Start collapsed
        private bool _showInspector = false;  // Start collapsed
        private bool _showDeobfuscation = false;  // Start collapsed
        private string _searchFilter = "";
        private float _hierarchyWidth = 350f;
        private float _inspectorWidth = 400f;

        // Timing
        private int _frameCount;
        private float _initDelayFrames = 60; // Wait for game to stabilize

        public override void OnLoad()
        {
            Logger.Info("MDB Explorer (ImGui) loading...");

            try
            {
                // Initialize deobfuscation helper for friendly name display
                DeobfuscationHelper.Initialize();
                if (DeobfuscationHelper.IsInitialized && DeobfuscationHelper.MappingCount > 0)
                {
                    Logger.Info($"Deobfuscation enabled with {DeobfuscationHelper.MappingCount} mappings");
                }
                
                // Create components
                _hierarchy = new SceneHierarchy();
                _inspector = new GameObjectInspector();
                _deobfuscationPanel = new DeobfuscationPanel();
                _imguiController = new ImGuiController();

                // Set up draw callback
                _imguiController.OnDraw = DrawUI;

                Logger.Info("MDB Explorer (ImGui) loaded. Waiting for game to initialize...");
            }
            catch (Exception ex)
            {
                Logger.Error($"OnLoad failed: {ex.Message}");
            }
        }

        public override void OnUpdate()
        {
            _frameCount++;

            // Wait for game to stabilize before initializing ImGui
            if (!_imguiInitialized && _frameCount > _initDelayFrames)
            {
                TryInitialize();
            }
        }

        private void TryInitialize()
        {
            try
            {
                Logger.Debug("Attempting ImGui initialization...");

                // Initialize hierarchy
                if (!_hierarchy.Initialize())
                {
                    Logger.Warning("Hierarchy initialization failed, will retry...");
                    _initDelayFrames += 30;
                    return;
                }

                // Initialize inspector
                if (!_inspector.Initialize())
                {
                    Logger.Warning("Inspector initialization failed, will retry...");
                    _initDelayFrames += 30;
                    return;
                }

                // Initialize ImGui
                if (!_imguiController.Initialize())
                {
                    Logger.Warning("ImGui initialization failed, will retry...");
                    _initDelayFrames += 30;
                    return;
                }

                // Initial hierarchy refresh
                _hierarchy.Refresh();

                // Initialize deobfuscation panel with dump path
                InitializeDeobfuscationPanel();

                _imguiInitialized = true;
                Logger.Info($"ImGui initialized! DirectX version: {_imguiController.DirectXVersion}");
                Logger.Info("Press F2 to toggle input capture");
            }
            catch (Exception ex)
            {
                Logger.Error($"Initialization failed: {ex.Message}");
                _initDelayFrames += 60;
            }
        }

        private void InitializeDeobfuscationPanel()
        {
            try
            {
                // Get the MDB folder structure
                // First try assembly location (won't work for IL2CPP byte-loaded assemblies)
                var assemblyLocation = typeof(ExplorerMod).Assembly.Location;
                string mdbFolder = null;
                
                if (!string.IsNullOrEmpty(assemblyLocation))
                {
                    var modsFolder = System.IO.Path.GetDirectoryName(assemblyLocation);
                    mdbFolder = !string.IsNullOrEmpty(modsFolder) 
                        ? System.IO.Path.GetDirectoryName(modsFolder) 
                        : null;
                    Logger.Info($"[DeobfuscationPanel] Found MDB folder via assembly: {mdbFolder}");
                }
                
                // Fallback: Use AppDomain.CurrentDomain.BaseDirectory (works for IL2CPP)
                if (string.IsNullOrEmpty(mdbFolder))
                {
                    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    var potentialMdbFolder = System.IO.Path.Combine(baseDir, "MDB");
                    
                    if (System.IO.Directory.Exists(potentialMdbFolder))
                    {
                        mdbFolder = potentialMdbFolder;
                        Logger.Info($"[DeobfuscationPanel] Found MDB folder via AppDomain: {mdbFolder}");
                    }
                    else
                    {
                        Logger.Warning($"[DeobfuscationPanel] MDB folder not found at: {potentialMdbFolder}");
                    }
                }

                if (!string.IsNullOrEmpty(mdbFolder))
                {
                    var dumpPath = System.IO.Path.Combine(mdbFolder, "Dump", "dump.cs");
                    var mappingsPath = System.IO.Path.Combine(mdbFolder, "Dump", "mappings.json");
                    Logger.Info($"[DeobfuscationPanel] Looking for dump at: {dumpPath}");
                    
                    if (System.IO.File.Exists(dumpPath))
                    {
                        Logger.Info($"[DeobfuscationPanel] Found dump file, initializing...");
                        _deobfuscationPanel.Initialize(dumpPath, mappingsPath);
                        Logger.Info($"[DeobfuscationPanel] Panel initialized with {_deobfuscationPanel.TypeCount} types indexed");
                    }
                    else
                    {
                        Logger.Warning($"[DeobfuscationPanel] Dump file not found at: {dumpPath}");
                    }
                }
                else
                {
                    Logger.Warning("[DeobfuscationPanel] Could not determine MDB folder path");
                }
            }
            catch (Exception ex)
            {
                Logger.Warning($"[DeobfuscationPanel] Failed to initialize: {ex.Message}");
                Logger.Warning($"[DeobfuscationPanel] Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Main ImGui drawing function.
        /// Called each frame from the native Present hook.
        /// </summary>
        private void DrawUI()
        {
            if (!_imguiInitialized) return;

            try
            {
                // Main menu bar
                if (ImGui.BeginMainMenuBar())
                {
                    if (ImGui.BeginMenu("MDB Explorer"))
                    {
                        if (ImGui.MenuItem("Show Explorer", null, _showExplorer))
                        {
                            _showExplorer = !_showExplorer;
                        }
                        ImGui.Separator();
                        if (ImGui.MenuItem("Hierarchy", null, _showHierarchy))
                        {
                            _showHierarchy = !_showHierarchy;
                        }
                        if (ImGui.MenuItem("Inspector", null, _showInspector))
                        {
                            _showInspector = !_showInspector;
                        }
                        if (ImGui.MenuItem("Deobfuscation", null, _showDeobfuscation))
                        {
                            _showDeobfuscation = !_showDeobfuscation;
                        }
                        ImGui.Separator();
                        if (ImGui.MenuItem("Refresh Hierarchy"))
                        {
                            _hierarchy.Refresh();
                        }
                        ImGui.Separator();
                        bool inputEnabled = _imguiController.IsInputEnabled;
                        if (ImGui.MenuItem("Capture Input (F2)", null, inputEnabled))
                        {
                            _imguiController.IsInputEnabled = !inputEnabled;
                        }
                        ImGui.EndMenu();
                    }

                    // Status
                    ImGui.SameLine(ImGui.GetWindowWidth() - 250);
                    if (!_imguiController.IsInputEnabled)
                    {
                        ImGui.TextColored(new Vector4(1.0f, 0.5f, 0.0f, 1.0f), "[Input Off - F2]");
                    }
                    else
                    {
                        ImGui.Text($"Objects: {_hierarchy.TotalNodeCount}");
                    }
                    ImGui.SameLine();
                    ImGui.Text($"| DX{(int)_imguiController.DirectXVersion}");

                    ImGui.EndMainMenuBar();
                }

                if (!_showExplorer) return;

                // Scene Hierarchy Panel
                if (_showHierarchy)
                {
                    DrawHierarchyPanel();
                }

                // Inspector Panel
                if (_showInspector)
                {
                    DrawInspectorPanel();
                }

                // Deobfuscation Panel
                if (_showDeobfuscation)
                {
                    DrawDeobfuscationPanel();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"DrawUI error: {ex.Message}");
            }
        }

        private void DrawDeobfuscationPanel()
        {
            ImGui.SetNextWindowPos(new Vector2(780, 30), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new Vector2(500, 600), ImGuiCond.FirstUseEver);

            if (ImGui.Begin("Deobfuscation", ref _showDeobfuscation))
            {
                _deobfuscationPanel.Render();
            }
            ImGui.End();
        }

        private void DrawHierarchyPanel()
        {
            ImGui.SetNextWindowPos(new Vector2(10, 30), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new Vector2(_hierarchyWidth, 500), ImGuiCond.FirstUseEver);

            if (ImGui.Begin("Scene Hierarchy", ref _showHierarchy))
            {
                // Scene selector dropdown
                string currentScene = _hierarchy.SelectedSceneIndex < 0 ? "All Scenes" :
                    (_hierarchy.SelectedSceneIndex < _hierarchy.Scenes.Count ? 
                     _hierarchy.Scenes[_hierarchy.SelectedSceneIndex].Name : "All Scenes");

                if (ImGui.BeginCombo("##SceneSelector", currentScene))
                {
                    // "All Scenes" option
                    if (ImGui.Selectable("All Scenes", _hierarchy.SelectedSceneIndex < 0))
                    {
                        _hierarchy.SelectedSceneIndex = -1;
                    }

                    // Individual scenes
                    for (int i = 0; i < _hierarchy.Scenes.Count; i++)
                    {
                        var scene = _hierarchy.Scenes[i];
                        string label = $"{scene.Name} ({scene.RootCount} roots)";
                        if (ImGui.Selectable(label, _hierarchy.SelectedSceneIndex == i))
                        {
                            _hierarchy.SelectedSceneIndex = i;
                        }
                    }
                    ImGui.EndCombo();
                }

                // Search bar
                ImGui.SetNextItemWidth(-1);
                if (ImGui.InputTextWithHint("##search", "Search...", ref _searchFilter, 256))
                {
                    _hierarchy.SetFilter(_searchFilter);
                }

                ImGui.Separator();

                // Toolbar
                if (ImGui.Button("Refresh"))
                {
                    _hierarchy.Refresh();
                }
                ImGui.SameLine();
                ImGui.Text($"({_hierarchy.RootNodes.Count} roots, {_hierarchy.TotalNodeCount} total)");

                ImGui.Separator();

                // Tree view
                if (ImGui.BeginChild("TreeView"))
                {
                    foreach (var node in _hierarchy.RootNodes)
                    {
                        if (MatchesFilter(node))
                        {
                            DrawTreeNode(node);
                        }
                    }
                }
                ImGui.EndChild();
            }
            ImGui.End();
        }

        private void DrawTreeNode(HierarchyNode node)
        {
            if (node == null) return;

            // Build flags
            ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth;

            if (!node.HasChildren)
            {
                flags |= ImGuiTreeNodeFlags.Leaf;
            }

            if (_selectedNode?.Pointer == node.Pointer)
            {
                flags |= ImGuiTreeNodeFlags.Selected;
            }

            if (node.IsExpanded)
            {
                flags |= ImGuiTreeNodeFlags.DefaultOpen;
            }

            // Color based on active state
            if (!node.IsActive)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.5f, 0.5f, 1.0f));
            }

            // Build label with child count
            string label = node.HasChildren 
                ? $"{node.Name} ({node.ChildCount})##{node.Pointer}" 
                : $"{node.Name}##{node.Pointer}";

            // Draw tree node
            bool opened = ImGui.TreeNodeEx(label, flags);

            if (!node.IsActive)
            {
                ImGui.PopStyleColor();
            }

            // Handle selection
            if (ImGui.IsItemClicked() && !ImGui.IsItemToggledOpen())
            {
                _selectedNode = node;
                _inspector.SetTarget(node);
            }
            
            // Right-click context menu
            if (ImGui.BeginPopupContextItem("node_ctx"))
            {
                if (ImGui.MenuItem("Copy Name"))
                    ImGui.SetClipboardText(node.Name);
                if (ImGui.MenuItem($"Copy Pointer: 0x{node.Pointer.ToInt64():X}"))
                    ImGui.SetClipboardText($"0x{node.Pointer.ToInt64():X}");
                ImGui.EndPopup();
            }

            // Handle expansion
            if (opened)
            {
                if (!node.ChildrenLoaded && node.HasChildren)
                {
                    _hierarchy.LoadChildren(node);
                }
                node.IsExpanded = true;

                foreach (var child in node.Children)
                {
                    if (MatchesFilter(child))
                    {
                        DrawTreeNode(child);
                    }
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
            // First check scene filter
            if (!_hierarchy.IsInSelectedScene(node))
            {
                return false;
            }

            // Then check text filter
            if (string.IsNullOrEmpty(_searchFilter)) return true;

            // Check this node
            if (node.Name.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            // Check children (for parent visibility)
            if (node.HasChildren)
            {
                if (!node.ChildrenLoaded)
                {
                    _hierarchy.LoadChildren(node);
                }

                foreach (var child in node.Children)
                {
                    if (MatchesFilter(child))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void DrawInspectorPanel()
        {
            ImGui.SetNextWindowPos(new Vector2(370, 30), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new Vector2(_inspectorWidth, 500), ImGuiCond.FirstUseEver);

            if (ImGui.Begin("Inspector", ref _showInspector))
            {
                _inspector.Draw();
            }
            ImGui.End();
        }

        /// <summary>
        /// Cleanup method - can be called manually if needed
        /// </summary>
        public void Shutdown()
        {
            Logger.Info("MDB Explorer (ImGui) shutting down...");

            try
            {
                _imguiController?.Shutdown();
                _imguiController?.Dispose();
            }
            catch (Exception ex)
            {
                Logger.Error($"Shutdown error: {ex.Message}");
            }
        }
    }
}
