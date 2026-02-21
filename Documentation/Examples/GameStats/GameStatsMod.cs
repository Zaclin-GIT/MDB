// ==============================
// GameStats - Medium-Complexity MDB Example Mod
// ==============================
// Demonstrates: Patching ([Patch]/[Prefix]/[Postfix]/[Finalizer]),
//               IL2CPP Bridge (class/method/field access),
//               HookManager (manual hooks), ImGui dashboard,
//               OnUpdate/OnLateUpdate lifecycle, advanced widgets.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using GameSDK;
using GameSDK.ModHost;
using GameSDK.ModHost.Patching;
using UnityEngine;

// Alias System.Numerics vector types for ImGui (avoids ambiguity with UnityEngine structs)
using Vector2 = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;

namespace MDB.Examples.GameStats
{
    /// <summary>
    /// A medium-complexity mod that tracks game statistics and method calls.
    /// 
    /// Features demonstrated:
    ///   - Declarative patching with [Patch], [Prefix], [Postfix], [Finalizer]
    ///   - Patching by wrapper type and by RVA
    ///   - IL2CPP Bridge: find classes, invoke methods, read fields
    ///   - HookManager: manual native hooks with enable/disable
    ///   - ImGui: tabbed window, combo box, tree nodes, tooltips, popups,
    ///            menus, colored text, draw list overlay, drag widgets
    ///   - OnUpdate + OnLateUpdate lifecycle
    /// </summary>
    [Mod("Examples.GameStats", "Game Stats Dashboard", "1.0.0",
        Author = "MDB Framework",
        Description = "Tracks game statistics, demonstrates patching & IL2CPP bridge.")]
    public class GameStatsMod : ModBase
    {
        // ── ImGui State ──
        private int _callbackId;
        private bool _windowOpen = true;
        private int _selectedTab;
        private readonly string[] _tabs = { "Statistics", "Hooks", "IL2CPP Browser", "Settings" };
        private bool _showOverlay = true;
        private float _refreshRate = 1.0f;
        private float _refreshTimer;

        // ── Statistics ──
        private int _frameCount;
        private int _updateCallCount;
        private int _lateUpdateCallCount;
        private static int _patchedMethodCalls;
        private static int _prefixSkipCount;
        private static int _screenWidthPostfixCount;
        private static int _manualHookCallCount;
        private static readonly List<string> _eventLog = new List<string>();
        private static readonly object _logLock = new object();

        // ── IL2CPP Browser State ──
        private string _assemblyName = "UnityEngine.CoreModule";
        private string _namespaceName = "UnityEngine";
        private string _className = "Time";
        private string _methodName = "";
        private string _fieldName = "";
        private string _browserResult = "";
        private IntPtr _foundClass = IntPtr.Zero;

        // ── Hook State ──
        private HookInfo _manualHook;
        private static IntPtr _originalTrampoline;
        private bool _hookEnabled = true;

        // ── Settings ──
        private int _maxLogEntries = 100;
        private bool _logPatchCalls = true;
        private float _overlayScale = 1.0f;

        // ── Constants ──
        private const int MAX_EVENT_LOG = 500;

        // ═══════════════════════════════════════
        //  Lifecycle
        // ═══════════════════════════════════════

        public override void OnLoad()
        {
            Logger.Info("GameStats mod loading...");

            // Register ImGui callback at High priority (renders before Normal mods)
            _callbackId = ImGuiManager.RegisterCallback(
                "GameStatsDashboard",
                DrawDashboard,
                ImGuiPriority.High
            );

            if (_callbackId > 0)
                Logger.Info("Dashboard UI registered");
            else
                Logger.Error("Failed to register dashboard UI");

            // Try to set up a manual hook (see Hooks section)
            SetupManualHook();

            Logger.Info("GameStats mod loaded successfully");
        }

        public override void OnUpdate()
        {
            _frameCount++;
            _updateCallCount++;

            // Use the generated Time wrapper to track elapsed time
            _refreshTimer += Time.deltaTime;
        }

        public override void OnLateUpdate()
        {
            _lateUpdateCallCount++;
        }

        // ═══════════════════════════════════════
        //  Declarative Patching
        // ═══════════════════════════════════════
        //
        // The [Patch] system automatically discovers these inner classes at load time.
        // It finds the IL2CPP method and installs hooks via MinHook.
        //
        // You can target methods by:
        //   1. Wrapper type:  [Patch(typeof(SomeGeneratedWrapper))]
        //   2. String lookup: [Patch("Namespace", "TypeName")]
        //   3. RVA offset:    [PatchRva(0x123ABC)]
        //

        // ── Example 1: Prefix that conditionally skips the original ──

        /// <summary>
        /// Patches Camera.get_fieldOfView — a universal Unity method present in every game.
        /// The [Prefix] runs BEFORE the original. Returning false skips the original method.
        /// </summary>
        [Patch("UnityEngine", "Camera")]
        [PatchMethod("get_fieldOfView", 0)]
        private static class CameraFieldOfViewPatch
        {
            /// <summary>
            /// Prefix: Runs before Camera.get_fieldOfView().
            /// 
            /// Special parameters:
            ///   __instance  — the 'this' pointer (IntPtr)
            ///   __0, __1    — positional method arguments
            ///   ref __result — the method's return value (modifiable)
            /// 
            /// Returns: true  = continue to original method
            ///          false = skip original method
            /// </summary>
            [Prefix]
            public static bool Prefix(IntPtr __instance)
            {
                _patchedMethodCalls++;

                // Example: Skip the original every 500th call
                if (_patchedMethodCalls % 500 == 0)
                {
                    _prefixSkipCount++;
                    AddEvent($"[Prefix] Camera.get_fieldOfView intercepted (call #{_patchedMethodCalls})");
                    return false; // Skip original
                }

                return true; // Continue to original
            }

            /// <summary>
            /// Postfix: Runs AFTER Camera.get_fieldOfView() completes.
            /// Can inspect or modify the result.
            /// </summary>
            [Postfix]
            public static void Postfix(IntPtr __instance)
            {
                // Postfix has access to the same special parameters.
                // Use ref __result to change what the method returns.
            }
        }

        // ── Example 2: Postfix that modifies a return value ──

        /// <summary>
        /// Patches Screen.get_width — a universal static Unity property.
        /// This demonstrates modifying the return value of a property getter.
        /// </summary>
        [Patch("UnityEngine", "Screen")]
        [PatchMethod("get_width", 0)]
        private static class ScreenWidthPatch
        {
            [Postfix]
            public static void Postfix(IntPtr __instance, ref IntPtr __result)
            {
                _screenWidthPostfixCount++;

                // Read the actual return value from the boxed IL2CPP result
                int width = 0;
                if (__result != IntPtr.Zero)
                {
                    try { width = Marshal.ReadInt32(__result + 16); } // skip 16-byte IL2CPP object header
                    catch { /* ignore read errors */ }
                }

                // Only log periodically to avoid flooding the event log
                if (_screenWidthPostfixCount % 500 == 1)
                {
                    AddEvent($"[Postfix] Screen.get_width = {width} (call #{_screenWidthPostfixCount})");
                }
            }
        }

        // ── Example 3: Finalizer (runs even on exception) ──

        /// <summary>
        /// Patches by RVA offset — required for heavily obfuscated methods
        /// that can't be found by name. Get RVAs from the IL2CPP dump.
        /// 
        /// The [Finalizer] always runs, even if the original method throws.
        /// Return null to swallow exceptions, or return a new Exception to rethrow.
        /// 
        /// Note: Replace 0xDEAD with a real RVA from your game's dump output.
        /// </summary>
        [Patch("UnityEngine", "Application")]
        [PatchRva(0xDEAD)]  // Placeholder RVA — replace with real value from dump
        private static class ApplicationFinalizer
        {
            [Finalizer]
            public static Exception Finalizer(Exception __exception)
            {
                if (__exception != null)
                {
                    AddEvent($"[Finalizer] Caught exception: {__exception.Message}");
                    return null; // Swallow the exception
                }
                return null;
            }
        }

        // ═══════════════════════════════════════
        //  Manual Hooking (HookManager)
        // ═══════════════════════════════════════

        /// <summary>
        /// Demonstrates manual hook installation using HookManager.
        /// Use this when you need more control than declarative [Patch] provides.
        /// 
        /// This example hooks Camera.get_main — a static property present in every
        /// Unity game. In practice you'd hook game-specific methods the same way.
        /// </summary>
        private void SetupManualHook()
        {
            try
            {
                // Step 1: Find the target class via IL2CPP bridge
                IntPtr klass = Il2CppBridge.mdb_find_class(
                    "UnityEngine.CoreModule",  // Assembly name
                    "UnityEngine",             // Namespace
                    "Camera"                   // Type name
                );

                if (klass == IntPtr.Zero)
                {
                    Logger.Warning("Camera class not found - manual hook skipped");
                    return;
                }

                // Step 2: Find the method
                IntPtr method = Il2CppBridge.mdb_get_method(klass, "get_main", 0);
                if (method == IntPtr.Zero)
                {
                    Logger.Warning("Camera.get_main not found");
                    return;
                }

                // Step 3: Create the hook
                _manualHook = HookManager.CreateHook(
                    method,
                    ManualHookDetour,
                    out _originalTrampoline,
                    "Camera.get_main (manual)"
                );

                if (_manualHook != null)
                    Logger.Info($"Manual hook installed: {_manualHook.Description}");
            }
            catch (Exception ex)
            {
                Logger.Error("Manual hook setup failed", ex);
            }
        }

        /// <summary>
        /// The detour function for our manual hook.
        /// Signature must match HookCallback: (IntPtr instance, IntPtr args, IntPtr original) → IntPtr
        /// </summary>
        private static IntPtr ManualHookDetour(IntPtr instance, IntPtr args, IntPtr original)
        {
            _manualHookCallCount++;

            // Only log periodically to avoid flooding the event log
            if (_manualHookCallCount % 500 == 1)
                AddEvent($"[ManualHook] Camera.get_main intercepted (call #{_manualHookCallCount})");

            // Call the original method through the trampoline
            if (_originalTrampoline != IntPtr.Zero)
            {
                // In a real scenario you'd call through the function pointer:
                // var originalFn = Marshal.GetDelegateForFunctionPointer<OriginalDelegate>(_originalTrampoline);
                // return originalFn(instance, args);
            }

            return IntPtr.Zero;
        }

        // ═══════════════════════════════════════
        //  IL2CPP Bridge Helpers
        // ═══════════════════════════════════════

        /// <summary>
        /// Demonstrates looking up a class, reading fields, and invoking methods
        /// through the IL2CPP bridge.
        /// </summary>
        private void BrowseIl2CppClass()
        {
            _browserResult = "";

            try
            {
                // Find the class
                _foundClass = Il2CppBridge.mdb_find_class(_assemblyName, _namespaceName, _className);
                if (_foundClass == IntPtr.Zero)
                {
                    _browserResult = $"Class not found: {_namespaceName}.{_className} in {_assemblyName}";
                    return;
                }

                var result = new System.Text.StringBuilder();
                result.AppendLine($"Found class: {_namespaceName}.{_className}");
                result.AppendLine($"  Pointer: 0x{_foundClass.ToInt64():X}");

                // Get field count
                int fieldCount = Il2CppBridge.mdb_class_get_field_count(_foundClass);
                result.AppendLine($"  Fields: {fieldCount}");

                // Enumerate fields
                for (int i = 0; i < Math.Min(fieldCount, 20); i++)
                {
                    IntPtr field = Il2CppBridge.mdb_class_get_field_by_index(_foundClass, i);
                    if (field != IntPtr.Zero)
                    {
                        string name = Il2CppBridge.GetFieldName(field) ?? "<unknown>";
                        bool isStatic = Il2CppBridge.mdb_field_is_static(field);
                        result.AppendLine($"    [{i}] {(isStatic ? "static " : "")}{name}");
                    }
                }

                // Get method count
                int methodCount = Il2CppBridge.mdb_class_get_method_count(_foundClass);
                result.AppendLine($"  Methods: {methodCount}");

                // Enumerate methods
                for (int i = 0; i < Math.Min(methodCount, 20); i++)
                {
                    IntPtr method = Il2CppBridge.mdb_class_get_method_by_index(_foundClass, i);
                    if (method != IntPtr.Zero)
                    {
                        string name = Il2CppBridge.GetMethodNameStr(method) ?? "<unknown>";
                        int paramCount = Il2CppBridge.mdb_method_get_param_count(method);
                        result.AppendLine($"    [{i}] {name}({paramCount} params)");
                    }
                }

                // Get property count
                int propCount = Il2CppBridge.mdb_class_get_property_count(_foundClass);
                result.AppendLine($"  Properties: {propCount}");

                // Get parent class
                IntPtr parent = Il2CppBridge.mdb_class_get_parent(_foundClass);
                if (parent != IntPtr.Zero)
                {
                    string parentName = ReadNativeString(Il2CppBridge.mdb_class_get_name(parent));
                    result.AppendLine($"  Parent: {parentName}");
                }

                _browserResult = result.ToString();
            }
            catch (Exception ex)
            {
                _browserResult = $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Demonstrates invoking an IL2CPP method at runtime.
        /// </summary>
        private string InvokeMethod()
        {
            if (_foundClass == IntPtr.Zero)
                return "No class loaded - search first";

            try
            {
                IntPtr method = Il2CppBridge.mdb_get_method(_foundClass, _methodName, 0);
                if (method == IntPtr.Zero)
                    return $"Method '{_methodName}' not found";

                // Get method info
                Il2CppBridge.mdb_get_method_info(method, out int paramCount, out bool isStatic, out bool hasReturn);

                string info = $"Method: {_methodName}\n" +
                              $"  Params: {paramCount}, Static: {isStatic}, Returns: {hasReturn}\n" +
                              $"  Pointer: 0x{Il2CppBridge.mdb_get_method_pointer(method).ToInt64():X}";

                // For static methods with 0 params, we could invoke:
                // IntPtr result = Il2CppBridge.mdb_invoke_method(method, IntPtr.Zero, null, out IntPtr ex);

                return info;
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Demonstrates reading a field value via the IL2CPP bridge.
        /// </summary>
        private string ReadFieldValue()
        {
            if (_foundClass == IntPtr.Zero)
                return "No class loaded - search first";

            try
            {
                IntPtr field = Il2CppBridge.mdb_get_field(_foundClass, _fieldName);
                if (field == IntPtr.Zero)
                    return $"Field '{_fieldName}' not found";

                int offset = Il2CppBridge.mdb_get_field_offset(field);
                bool isStatic = Il2CppBridge.mdb_field_is_static(field);

                return $"Field: {_fieldName}\n" +
                       $"  Offset: 0x{offset:X}\n" +
                       $"  Static: {isStatic}";
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        // ═══════════════════════════════════════
        //  ImGui Dashboard
        // ═══════════════════════════════════════

        private void DrawDashboard()
        {
            // Draw overlay first (behind main window)
            if (_showOverlay)
                DrawStatsOverlay();

            if (!_windowOpen) return;

            ImGui.SetNextWindowSize(new Vector2(550, 500), ImGuiCond.FirstUseEver);

            ImGuiWindowFlags windowFlags = ImGuiWindowFlags.MenuBar;
            if (ImGui.Begin("Game Stats Dashboard", ref _windowOpen, windowFlags))
            {
                // ── Menu Bar ──
                DrawMenuBar();

                // ── Tab Selector (Combo) ──
                if (ImGui.BeginCombo("##tabs", _tabs[_selectedTab]))
                {
                    for (int i = 0; i < _tabs.Length; i++)
                    {
                        if (ImGui.Selectable(_tabs[i], i == _selectedTab))
                            _selectedTab = i;
                    }
                    ImGui.EndCombo();
                }
                ImGui.Separator();
                ImGui.Spacing();

                // ── Tab Content ──
                switch (_selectedTab)
                {
                    case 0: DrawStatsTab(); break;
                    case 1: DrawHooksTab(); break;
                    case 2: DrawBrowserTab(); break;
                    case 3: DrawSettingsTab(); break;
                }
            }
            ImGui.End();
        }

        /// <summary>
        /// Demonstrates ImGui menu bar with menus and menu items.
        /// </summary>
        private void DrawMenuBar()
        {
            if (ImGui.BeginMenuBar())
            {
                if (ImGui.BeginMenu("File"))
                {
                    if (ImGui.MenuItem("Reset Stats"))
                    {
                        _frameCount = 0;
                        _updateCallCount = 0;
                        _lateUpdateCallCount = 0;
                        _patchedMethodCalls = 0;
                        Logger.Info("Stats reset");
                    }
                    if (ImGui.MenuItem("Clear Event Log"))
                    {
                        lock (_logLock) { _eventLog.Clear(); }
                    }
                    ImGui.Separator();
                    if (ImGui.MenuItem("Close"))
                        _windowOpen = false;
                    ImGui.EndMenu();
                }

                if (ImGui.BeginMenu("View"))
                {
                    ImGui.MenuItem("Show Overlay", null, _showOverlay);
                    if (ImGui.IsItemClicked())
                        _showOverlay = !_showOverlay;
                    ImGui.EndMenu();
                }

                ImGui.EndMenuBar();
            }
        }

        /// <summary>
        /// Statistics tab: shows counters, runtime info from generated wrappers, event log.
        /// </summary>
        private void DrawStatsTab()
        {
            // ── Counters ──
            ImGui.TextColored(new Vector4(0.4f, 0.8f, 1.0f, 1.0f), "Counters");
            ImGui.Separator();

            ImGui.BulletText($"Frame Count: {_frameCount}");
            ImGui.BulletText($"Update Calls: {_updateCallCount}");
            ImGui.BulletText($"LateUpdate Calls: {_lateUpdateCallCount}");
            ImGui.BulletText($"Patched Method Calls: {_patchedMethodCalls}");
            ImGui.BulletText($"Prefix Skips: {_prefixSkipCount}");

            ImGui.Spacing();

            // ── Unity Runtime Info (from generated wrappers) ──
            ImGui.TextColored(new Vector4(0.3f, 1.0f, 0.8f, 1.0f), "Unity Runtime");
            ImGui.Separator();

            try
            {
                ImGui.BulletText($"Time.time: {Time.time:F2}s");
                ImGui.BulletText($"Time.deltaTime: {Time.deltaTime:F4}s");
                ImGui.BulletText($"Time.frameCount: {Time.frameCount}");
                ImGui.BulletText($"Time.timeScale: {Time.timeScale:F2}");
                ImGui.BulletText($"Screen: {Screen.width}x{Screen.height}");
                ImGui.BulletText($"Application.productName: {Application.productName ?? "?"}");
                ImGui.BulletText($"Application.unityVersion: {Application.unityVersion ?? "?"}");
                ImGui.BulletText($"Application.platform: {Application.platform}");
            }
            catch
            {
                ImGui.TextDisabled("(Some Unity properties not available yet)");
            }

            ImGui.Spacing();

            // ── Event Log (Tree Node) ──
            ImGui.TextColored(new Vector4(1.0f, 0.8f, 0.3f, 1.0f), "Event Log");
            ImGui.Separator();

            List<string> events;
            lock (_logLock) { events = new List<string>(_eventLog); }

            int displayCount = Math.Min(events.Count, _maxLogEntries);
            ImGui.TextDisabled($"Showing {displayCount} of {events.Count} events");

            ImGui.BeginChild("##eventlog", new Vector2(0, 200), 1);
            for (int i = events.Count - 1; i >= Math.Max(0, events.Count - displayCount); i--)
            {
                ImGui.PushID(i);

                // Color-code by event type
                string evt = events[i];
                if (evt.Contains("[Prefix]"))
                    ImGui.TextColored(new Vector4(0.3f, 1.0f, 0.3f, 1.0f), evt);
                else if (evt.Contains("[Postfix]"))
                    ImGui.TextColored(new Vector4(0.3f, 0.8f, 1.0f, 1.0f), evt);
                else if (evt.Contains("[Finalizer]"))
                    ImGui.TextColored(new Vector4(1.0f, 0.5f, 0.3f, 1.0f), evt);
                else if (evt.Contains("[ManualHook]"))
                    ImGui.TextColored(new Vector4(1.0f, 1.0f, 0.3f, 1.0f), evt);
                else
                    ImGui.Text(evt);

                // Right-click context menu on each event
                if (ImGui.BeginPopupContextItem("event_ctx"))
                {
                    if (ImGui.MenuItem("Copy"))
                        ImGui.SetClipboardText(evt);
                    ImGui.EndPopup();
                }

                ImGui.PopID();
            }
            ImGui.EndChild();
        }

        /// <summary>
        /// Hooks tab: shows manual hook state with enable/disable controls.
        /// </summary>
        private void DrawHooksTab()
        {
            ImGui.TextColored(new Vector4(0.4f, 0.8f, 1.0f, 1.0f), "Manual Hooks");
            ImGui.Separator();
            ImGui.Spacing();

            if (_manualHook != null)
            {
                // Hook info tree node
                if (ImGui.TreeNodeEx("Camera.get_main", ImGuiTreeNodeFlags.DefaultOpen))
                {
                    ImGui.BulletText($"Handle: {_manualHook.Handle}");
                    ImGui.BulletText($"Target: 0x{_manualHook.Target.ToInt64():X}");
                    ImGui.BulletText($"Original: 0x{_manualHook.Original.ToInt64():X}");
                    ImGui.BulletText($"Enabled: {_manualHook.Enabled}");

                    ImGui.Spacing();

                    // Toggle hook on/off
                    if (ImGui.Checkbox("Hook Enabled", ref _hookEnabled))
                    {
                        HookManager.SetHookEnabled(_manualHook.Handle, _hookEnabled);
                        Logger.Info($"Manual hook {(_hookEnabled ? "enabled" : "disabled")}");
                    }

                    // Tooltip on hover
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Toggle the hook without removing it.\nDisabled hooks pass through to the original.");

                    ImGui.Spacing();

                    // Remove hook button (destructive)
                    ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.8f, 0.2f, 0.2f, 1.0f));
                    if (ImGui.Button("Remove Hook"))
                    {
                        HookManager.RemoveHook(_manualHook);
                        _manualHook = null;
                        Logger.Info("Manual hook removed");
                    }
                    ImGui.PopStyleColor();

                    ImGui.TreePop();
                }
            }
            else
            {
                ImGui.TextDisabled("No manual hooks installed.");
                ImGui.TextWrapped(
                    "Manual hooks are installed in OnLoad() via HookManager.CreateHook(). " +
                    "They require the target class to exist in the game's IL2CPP runtime.");

                ImGui.Spacing();
                if (ImGui.Button("Retry Hook Setup"))
                    SetupManualHook();
            }

            ImGui.Spacing();
            ImGui.Separator();

            // ── All Hooks Summary ──
            ImGui.TextColored(new Vector4(1.0f, 0.8f, 0.3f, 1.0f), "All Active Hooks");
            ImGui.Spacing();

            foreach (var hook in HookManager.GetAllHooks())
            {
                ImGui.BulletText($"{hook.Description} (handle={hook.Handle}, enabled={hook.Enabled})");
            }
        }

        /// <summary>
        /// IL2CPP Browser tab: interactive class/method/field browser.
        /// Pre-populated with universal Unity types for demonstration.
        /// </summary>
        private void DrawBrowserTab()
        {
            ImGui.TextColored(new Vector4(0.4f, 0.8f, 1.0f, 1.0f), "IL2CPP Browser");
            ImGui.Separator();
            ImGui.Spacing();

            // ── Class Lookup ──
            ImGui.SetNextItemWidth(200);
            ImGui.InputText("Assembly", ref _assemblyName);
            ImGui.SetNextItemWidth(200);
            ImGui.InputText("Namespace", ref _namespaceName);
            ImGui.SetNextItemWidth(200);
            ImGui.InputText("Class Name", ref _className);

            ImGui.Spacing();
            if (ImGui.Button("Find Class"))
            {
                BrowseIl2CppClass();
                Logger.Info($"Browsed class: {_namespaceName}.{_className}");
            }

            // ── Method Info ──
            ImGui.Spacing();
            ImGui.SetNextItemWidth(200);
            ImGui.InputText("Method Name", ref _methodName);
            ImGui.SameLine();
            if (ImGui.SmallButton("Inspect Method"))
            {
                _browserResult = InvokeMethod();
            }

            // ── Field Info ──
            ImGui.SetNextItemWidth(200);
            ImGui.InputText("Field Name", ref _fieldName);
            ImGui.SameLine();
            if (ImGui.SmallButton("Inspect Field"))
            {
                _browserResult = ReadFieldValue();
            }

            // ── Results ──
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.TextColored(new Vector4(0.3f, 1.0f, 0.3f, 1.0f), "Results:");
            ImGui.BeginChild("##results", new Vector2(0, 200), 1);
            if (!string.IsNullOrEmpty(_browserResult))
            {
                ImGui.TextWrapped(_browserResult);

                // Copy button
                ImGui.Spacing();
                if (ImGui.SmallButton("Copy to Clipboard"))
                    ImGui.SetClipboardText(_browserResult);
            }
            else
            {
                ImGui.TextDisabled("Enter a class name and click 'Find Class' to browse IL2CPP metadata.");
                ImGui.TextDisabled("Try: UnityEngine.CoreModule / UnityEngine / Camera");
            }
            ImGui.EndChild();
        }

        /// <summary>
        /// Settings tab: demonstrates drag widgets, input fields, checkboxes.
        /// </summary>
        private void DrawSettingsTab()
        {
            ImGui.TextColored(new Vector4(0.4f, 0.8f, 1.0f, 1.0f), "Settings");
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.Checkbox("Log Patch Calls", ref _logPatchCalls);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Log every patched method call to the event log.");

            ImGui.Checkbox("Show Stats Overlay", ref _showOverlay);

            ImGui.Spacing();

            ImGui.SliderFloat("Refresh Rate (sec)", ref _refreshRate, 0.1f, 5.0f);
            ImGui.SliderFloat("Overlay Scale", ref _overlayScale, 0.5f, 2.0f);
            ImGui.InputInt("Max Log Entries", ref _maxLogEntries, 10, 50);

            ImGui.Spacing();
            ImGui.Separator();

            // ── ImGui Info ──
            if (ImGui.CollapsingHeader("ImGui Info"))
            {
                ImGui.BulletText($"DirectX: {ImGuiManager.DirectXVersion}");
                ImGui.BulletText($"Initialized: {ImGuiManager.IsInitialized}");
                ImGui.BulletText($"Input Enabled: {ImGuiManager.IsInputEnabled}");
                ImGui.BulletText($"Active Callbacks: {ImGuiManager.CallbackCount}");
                ImGui.BulletText($"Active Hooks: {Il2CppBridge.mdb_hook_get_count()}");
            }

            ImGui.Spacing();

            // ── Danger Zone (popup) ──
            ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.6f, 0.1f, 0.1f, 1.0f));
            if (ImGui.CollapsingHeader("Danger Zone"))
            {
                if (ImGui.Button("Remove All Hooks"))
                {
                    ImGui.OpenPopup("confirm_remove_hooks");
                }

                if (ImGui.BeginPopup("confirm_remove_hooks"))
                {
                    ImGui.TextError("Are you sure?");
                    ImGui.Text("This will remove ALL active hooks.");
                    ImGui.Spacing();
                    if (ImGui.Button("Yes, Remove All"))
                    {
                        HookManager.RemoveAllHooks();
                        _manualHook = null;
                        Logger.Warning("All hooks removed!");
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("Cancel"))
                    {
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.EndPopup();
                }
            }
            ImGui.PopStyleColor();
        }

        /// <summary>
        /// Draws a compact overlay using the DrawList API for screen-space rendering.
        /// Shows live data from the generated Unity wrappers.
        /// </summary>
        private void DrawStatsOverlay()
        {
            // Use DrawList API for direct screen-space drawing
            uint white = ImGui.ColorToU32(1, 1, 1);
            uint green = ImGui.ColorToU32(0.3f, 1.0f, 0.3f);
            uint bgColor = ImGui.ColorToU32(0, 0, 0, 0.6f);

            float x = 10, y = 60;
            float w = 200 * _overlayScale, h = 100 * _overlayScale;

            // Background box
            ImGui.DrawRectFilled(new Vector2(x, y), new Vector2(x + w, y + h), bgColor, 4f);
            ImGui.DrawRect(new Vector2(x, y), new Vector2(x + w, y + h), green, 1f, 4f);

            // Text — using generated wrappers for live data
            float fps = 1.0f / Math.Max(Time.deltaTime, 0.001f);
            ImGui.DrawText(new Vector2(x + 5, y + 5), green, "GameStats");
            ImGui.DrawText(new Vector2(x + 5, y + 22), white, $"FPS: {fps:F0}");
            ImGui.DrawText(new Vector2(x + 5, y + 39), white, $"Frames: {Time.frameCount}");
            ImGui.DrawText(new Vector2(x + 5, y + 56), white, $"Patches: {_patchedMethodCalls}");
            ImGui.DrawText(new Vector2(x + 5, y + 73), white, $"Hooks: {Il2CppBridge.mdb_hook_get_count()}");
        }

        // ═══════════════════════════════════════
        //  Utilities
        // ═══════════════════════════════════════

        private static void AddEvent(string message)
        {
            lock (_logLock)
            {
                _eventLog.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
                while (_eventLog.Count > MAX_EVENT_LOG)
                    _eventLog.RemoveAt(0);
            }
        }

        private static string ReadNativeString(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero) return "<null>";
            return Marshal.PtrToStringAnsi(ptr) ?? "<null>";
        }
    }
}
