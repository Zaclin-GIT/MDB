// ==============================
// GameObjectInspector - Displays selected GameObject properties
// ==============================

using System;
using System.Collections.Generic;
using System.Numerics;
using GameSDK;
using GameSDK.ModHost;

namespace MDB.Explorer.ImGui
{
    /// <summary>
    /// Displays inspector panel for a selected GameObject.
    /// </summary>
    public class GameObjectInspector
    {
        private const string LOG_TAG = "GameObjectInspector";

        // IL2CPP class resolution
        private bool _classesResolved;

        // Current target
        private HierarchyNode _target;
        private List<ComponentInfo> _components = new List<ComponentInfo>();

        // Transform values
        private Vector3 _position;
        private Vector3 _rotation;
        private Vector3 _scale;
        private IntPtr _transformPtr;
        
        // Drill-down inspection stack
        private Stack<InspectionContext> _inspectionStack = new Stack<InspectionContext>();
        
        // Track expanded arrays (key = "compIndex_fieldIndex")
        private HashSet<string> _expandedArrays = new HashSet<string>();
        
        // Cache for string field editing (key = field pointer, value = current edit string)
        private Dictionary<IntPtr, string> _stringEditCache = new Dictionary<IntPtr, string>();

        // Auto-size: set when target changes so the parent can resize the window
        private bool _needsAutoSize;
        private float _desiredWidth;

        public HierarchyNode Target => _target;

        /// <summary>
        /// If true, the inspector wants the window to auto-resize. Consume with ConsumeAutoSizeRequest.
        /// </summary>
        public bool NeedsAutoSize => _needsAutoSize;

        /// <summary>
        /// The computed ideal width. Valid when NeedsAutoSize is true.
        /// </summary>
        public float DesiredWidth => _desiredWidth;

        /// <summary>
        /// Consume the auto-size request (resets the flag). Returns the desired width.
        /// </summary>
        public float ConsumeAutoSizeRequest()
        {
            _needsAutoSize = false;
            return _desiredWidth;
        }

        /// <summary>
        /// Initialize IL2CPP class pointers.
        /// </summary>
        public bool Initialize()
        {
            if (_classesResolved) return true;

            try
            {
                _classesResolved = Il2CppHelpers.ResolveClasses();
                return _classesResolved;
            }
            catch (Exception ex)
            {
                ModLogger.LogInternal(LOG_TAG, $"[ERROR] Initialize failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Set the target GameObject to inspect.
        /// </summary>
        public void SetTarget(HierarchyNode node)
        {
            if (_target?.Pointer == node?.Pointer) return;

            _target = node;
            _components.Clear();
            _inspectionStack.Clear();  // Clear drill-down when changing target
            _stringEditCache.Clear();  // Clear string edit cache when changing target

            if (node == null || !node.IsValid) return;

            RefreshComponents();
            RefreshTransform();
            ComputeDesiredWidth();
        }

        /// <summary>
        /// Compute the ideal window width based on the widest content in the current components.
        /// </summary>
        private void ComputeDesiredWidth()
        {
            // Base: padding + indent for tree nodes
            float maxContentWidth = 200f; // minimum baseline
            const float charW = 7.5f;
            const float padding = 80f; // tree indent + margins + scrollbar
            const float spacing = 30f; // gap between name and value columns

            foreach (var comp in _components)
            {
                if (comp.ReflectionData == null) continue;
                var data = comp.ReflectionData;

                foreach (var field in data.Fields)
                {
                    string typeName = GetSimpleTypeName(field.DisplayTypeName);
                    string fieldName = field.DisplayName ?? "(unnamed)";
                    // Estimate value width from the field's type name (shown as value for object/class types)
                    string valueTypeName = GetSimpleTypeName(field.DisplayTypeName);
                    float valueWidth = Math.Max(80f, valueTypeName.Length * charW + 40f);
                    float rowWidth = padding + (typeName.Length + fieldName.Length + 2) * charW + spacing + valueWidth;
                    if (rowWidth > maxContentWidth) maxContentWidth = rowWidth;
                }

                foreach (var prop in data.Properties)
                {
                    string typeName = GetSimpleTypeName(prop.DisplayTypeName);
                    string propName = prop.DisplayName ?? "(unnamed)";
                    string valueTypeName = GetSimpleTypeName(prop.DisplayTypeName);
                    float valueWidth = Math.Max(80f, valueTypeName.Length * charW + 40f);
                    float rowWidth = padding + (typeName.Length + propName.Length + 2) * charW + spacing + valueWidth;
                    if (rowWidth > maxContentWidth) maxContentWidth = rowWidth;
                }
            }

            // Clamp to reasonable bounds
            _desiredWidth = Math.Max(350f, Math.Min(maxContentWidth, 1200f));
            _needsAutoSize = true;
        }

        /// <summary>
        /// Refresh the component list.
        /// </summary>
        public void RefreshComponents()
        {
            _components.Clear();

            if (_target == null || !_target.IsValid) return;
            if (!_classesResolved && !Initialize()) return;

            try
            {
                // Use the native helper to get components
                IntPtr result = Il2CppBridge.mdb_gameobject_get_components(_target.Pointer);

                if (result == IntPtr.Zero)
                {
                    ModLogger.LogInternal(LOG_TAG, "[WARN] mdb_gameobject_get_components returned null");
                    return;
                }

                int length = Il2CppBridge.mdb_array_length(result);
                ModLogger.LogInternal(LOG_TAG, $"[INFO] Found {length} components");

                for (int i = 0; i < length; i++)
                {
                    IntPtr compPtr = Il2CppBridge.mdb_array_get_element(result, i);
                    if (compPtr == IntPtr.Zero) continue;

                    string typeName = Il2CppHelpers.GetComponentTypeName(compPtr);

                    // Get reflection data for this component
                    ComponentReflectionData reflectionData = ComponentReflector.GetReflectionData(compPtr);

                    _components.Add(new ComponentInfo
                    {
                        Pointer = compPtr,
                        TypeName = typeName ?? "<unknown>",
                        ReflectionData = reflectionData
                    });
                }

                ModLogger.LogInternal(LOG_TAG, $"[INFO] Loaded {_components.Count} components");
            }
            catch (Exception ex)
            {
                ModLogger.LogInternal(LOG_TAG, $"[ERROR] RefreshComponents failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Refresh transform values.
        /// </summary>
        public void RefreshTransform()
        {
            if (_target == null || !_target.IsValid) return;
            if (!_classesResolved && !Initialize()) return;

            try
            {
                _transformPtr = Il2CppHelpers.GetTransform(_target.Pointer);
                if (_transformPtr == IntPtr.Zero) return;

                // Use native helpers that properly handle IL2CPP value type unboxing
                float x, y, z;
                
                if (Il2CppBridge.mdb_transform_get_local_position(_transformPtr, out x, out y, out z))
                    _position = new Vector3(x, y, z);
                else
                    _position = Vector3.Zero;

                if (Il2CppBridge.mdb_transform_get_local_euler_angles(_transformPtr, out x, out y, out z))
                    _rotation = new Vector3(x, y, z);
                else
                    _rotation = Vector3.Zero;

                if (Il2CppBridge.mdb_transform_get_local_scale(_transformPtr, out x, out y, out z))
                    _scale = new Vector3(x, y, z);
                else
                    _scale = Vector3.One;
            }
            catch (Exception ex)
            {
                ModLogger.LogInternal(LOG_TAG, $"[ERROR] RefreshTransform failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Draw the inspector panel using ImGui.
        /// </summary>
        public void Draw()
        {
            if (_target == null || !_target.IsValid)
            {
                ImGui.TextDisabled("No GameObject selected");
                return;
            }

            // Header with name and active toggle
            bool active = _target.IsActive;
            if (ImGui.Checkbox("##active", ref active))
            {
                Il2CppHelpers.SetGameObjectActive(_target.Pointer, active);
                _target.IsActive = active;
            }
            ImGui.SameLine();
            ImGui.Text(_target.Name);

            ImGui.Separator();

            // Transform section - use unique ID to avoid conflicts with "Transform" component
            if (ImGui.CollapsingHeader("Transform##inspector_transform", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Indent();

                // Position
                ImGui.Text("Position:");
                ImGui.SameLine(LayoutConstants.TransformLabelIndent);
                ImGui.SetNextItemWidth(-RIGHT_PADDING);
                if (ImGui.DragFloat3("##pos", ref _position, 0.1f))
                {
                    if (_transformPtr != IntPtr.Zero)
                        Il2CppBridge.mdb_transform_set_local_position(_transformPtr, _position.X, _position.Y, _position.Z);
                }

                // Rotation
                ImGui.Text("Rotation:");
                ImGui.SameLine(LayoutConstants.TransformLabelIndent);
                ImGui.SetNextItemWidth(-RIGHT_PADDING);
                if (ImGui.DragFloat3("##rot", ref _rotation, 1.0f))
                {
                    if (_transformPtr != IntPtr.Zero)
                        Il2CppBridge.mdb_transform_set_local_euler_angles(_transformPtr, _rotation.X, _rotation.Y, _rotation.Z);
                }

                // Scale
                ImGui.Text("Scale:");
                ImGui.SameLine(LayoutConstants.TransformLabelIndent);
                ImGui.SetNextItemWidth(-RIGHT_PADDING);
                if (ImGui.DragFloat3("##scale", ref _scale, 0.01f))
                {
                    if (_transformPtr != IntPtr.Zero)
                        Il2CppBridge.mdb_transform_set_local_scale(_transformPtr, _scale.X, _scale.Y, _scale.Z);
                }

                ImGui.Unindent();
            }

            // Components section
            if (ImGui.CollapsingHeader($"Components ({_components.Count})##inspector_components", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Indent();
                
                // If we have a drill-down stack, show that instead
                if (_inspectionStack.Count > 0)
                {
                    DrawInspectionStack();
                }
                else
                {
                    // Normal component display
                    for (int i = 0; i < _components.Count; i++)
                    {
                        var comp = _components[i];
                        
                        // Skip Transform as we already show it above
                        if (comp.TypeName == "Transform") continue;
                        
                        // Use display name (deobfuscated if available) for header
                        string displayName = comp.DisplayTypeName;
                        // Show original obfuscated name in parentheses if different
                        if (displayName != comp.TypeName && DeobfuscationHelper.IsObfuscatedName(comp.TypeName))
                        {
                            displayName = $"{displayName} [{comp.TypeName}]";
                        }
                        
                        // Use index suffix to ensure unique IDs for each component
                        ImGui.PushID(i);
                        if (ImGui.CollapsingHeader($"{displayName}##comp_{i}"))
                        {
                            ImGui.Indent();
                            DrawComponentMembers(comp, i);
                            ImGui.Unindent();
                        }
                        
                        // Right-click context menu for component
                        if (ImGui.BeginPopupContextItem("comp_ctx"))
                        {
                            if (ImGui.MenuItem("Copy Type Name"))
                                ImGui.SetClipboardText(comp.TypeName);
                            if (comp.DisplayTypeName != comp.TypeName && ImGui.MenuItem("Copy Display Name"))
                                ImGui.SetClipboardText(comp.DisplayTypeName);
                            if (ImGui.MenuItem($"Copy Pointer: 0x{comp.Pointer.ToInt64():X}"))
                                ImGui.SetClipboardText($"0x{comp.Pointer.ToInt64():X}");
                            ImGui.EndPopup();
                        }
                        ImGui.PopID();
                    }
                }

                ImGui.Unindent();
            }
        }

        /// <summary>
        /// Draw the inspection stack for drill-down navigation.
        /// Groups members by their declaring class in the inheritance hierarchy.
        /// </summary>
        private void DrawInspectionStack()
        {
            var current = _inspectionStack.Peek();
            
            // Back button and breadcrumb - use display name
            if (ImGui.Button("<< Back"))
            {
                _inspectionStack.Pop();
            }
            ImGui.SameLine();
            ImGui.TextColored(Theme.Highlight, current.DisplayTypeName ?? "Object");
            
            ImGui.Separator();
            
            if (current.ReflectionData == null)
            {
                ImGui.TextDisabled("No reflection data");
                return;
            }
            
            var data = current.ReflectionData;
            const int drilldownCompIndex = 999;
            
            // Fields - grouped by declaring class
            if (data.Fields.Count > 0 && ImGui.TreeNode($"Fields ({data.Fields.Count})##drilldown_fields"))
            {
                int fieldIndex = 0;
                
                foreach (var (className, displayClassName, fields) in data.GetFieldsByClass())
                {
                    bool isInherited = className != data.ClassName;
                    
                    if (isInherited)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, Theme.InheritedClass);
                        bool classExpanded = ImGui.TreeNode($"[{displayClassName}]##drilldown_inherited_fields_{className}");
                        ImGui.PopStyleColor();
                        
                        // Right-click context menu for inherited class
                        if (ImGui.BeginPopupContextItem($"inherited_class_ctx_fields_{className}"))
                        {
                            if (ImGui.MenuItem("Copy Class Name (Obfuscated)"))
                                ImGui.SetClipboardText(className);
                            if (displayClassName != className && ImGui.MenuItem("Copy Class Name (Display)"))
                                ImGui.SetClipboardText(displayClassName);
                            ImGui.EndPopup();
                        }
                        
                        if (classExpanded)
                        {
                            foreach (var field in fields)
                            {
                                DrawField(current.Instance, field, drilldownCompIndex, fieldIndex++);
                            }
                            ImGui.TreePop();
                        }
                        else
                        {
                            fieldIndex += fields.Count;
                        }
                    }
                    else
                    {
                        foreach (var field in fields)
                        {
                            DrawField(current.Instance, field, drilldownCompIndex, fieldIndex++);
                        }
                    }
                }
                ImGui.TreePop();
            }
            
            // Properties - grouped by declaring class
            if (data.Properties.Count > 0 && ImGui.TreeNode($"Properties ({data.Properties.Count})##drilldown_props"))
            {
                int propIndex = 0;
                
                foreach (var (className, displayClassName, props) in data.GetPropertiesByClass())
                {
                    bool isInherited = className != data.ClassName;
                    
                    if (isInherited)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, Theme.InheritedClass);
                        bool classExpanded = ImGui.TreeNode($"[{displayClassName}]##drilldown_inherited_props_{className}");
                        ImGui.PopStyleColor();
                        
                        // Right-click context menu for inherited class
                        if (ImGui.BeginPopupContextItem($"inherited_class_ctx_props_{className}"))
                        {
                            if (ImGui.MenuItem("Copy Class Name (Obfuscated)"))
                                ImGui.SetClipboardText(className);
                            if (displayClassName != className && ImGui.MenuItem("Copy Class Name (Display)"))
                                ImGui.SetClipboardText(displayClassName);
                            ImGui.EndPopup();
                        }
                        
                        if (classExpanded)
                        {
                            foreach (var prop in props)
                            {
                                DrawProperty(current.Instance, prop, drilldownCompIndex, propIndex++);
                            }
                            ImGui.TreePop();
                        }
                        else
                        {
                            propIndex += props.Count;
                        }
                    }
                    else
                    {
                        foreach (var prop in props)
                        {
                            DrawProperty(current.Instance, prop, drilldownCompIndex, propIndex++);
                        }
                    }
                }
                ImGui.TreePop();
            }
            
            // Methods - grouped by declaring class
            if (data.Methods.Count > 0 && ImGui.TreeNode($"Methods ({data.Methods.Count})##drilldown_methods"))
            {
                int methodIndex = 0;
                
                foreach (var (className, displayClassName, methods) in data.GetMethodsByClass())
                {
                    bool isInherited = className != data.ClassName;
                    
                    if (isInherited)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, Theme.InheritedClass);
                        bool classExpanded = ImGui.TreeNode($"[{displayClassName}]##drilldown_inherited_methods_{className}");
                        ImGui.PopStyleColor();
                        
                        // Right-click context menu for inherited class
                        if (ImGui.BeginPopupContextItem($"inherited_class_ctx_methods_{className}"))
                        {
                            if (ImGui.MenuItem("Copy Class Name (Obfuscated)"))
                                ImGui.SetClipboardText(className);
                            if (displayClassName != className && ImGui.MenuItem("Copy Class Name (Display)"))
                                ImGui.SetClipboardText(displayClassName);
                            ImGui.EndPopup();
                        }
                        
                        if (classExpanded)
                        {
                            foreach (var method in methods)
                            {
                                DrawMethod(method, drilldownCompIndex, methodIndex++);
                            }
                            ImGui.TreePop();
                        }
                        else
                        {
                            methodIndex += methods.Count;
                        }
                    }
                    else
                    {
                        foreach (var method in methods)
                        {
                            DrawMethod(method, drilldownCompIndex, methodIndex++);
                        }
                    }
                }
                ImGui.TreePop();
            }
        }

        /// <summary>
        /// Draw component fields, properties, and methods.
        /// Groups members by their declaring class in the inheritance hierarchy.
        /// </summary>
        private void DrawComponentMembers(ComponentInfo comp, int compIndex)
        {
            if (comp.ReflectionData == null)
            {
                ImGui.TextDisabled("No reflection data available");
                return;
            }

            var data = comp.ReflectionData;

            // Fields section - grouped by declaring class
            if (data.Fields.Count > 0 && ImGui.TreeNode($"Fields ({data.Fields.Count})##fields_{compIndex}"))
            {
                int fieldIndex = 0;
                
                foreach (var (className, displayClassName, fields) in data.GetFieldsByClass())
                {
                    // Show class header for inherited classes
                    bool isInherited = className != data.ClassName;
                    
                    if (isInherited)
                    {
                        // Inherited class header with distinct styling
                        ImGui.PushStyleColor(ImGuiCol.Text, Theme.InheritedClass);
                        bool classExpanded = ImGui.TreeNode($"[{displayClassName}]##inherited_fields_{className}_{compIndex}");
                        ImGui.PopStyleColor();
                        
                        // Right-click context menu for inherited class
                        if (ImGui.BeginPopupContextItem($"inherited_class_ctx_fields_{className}_{compIndex}"))
                        {
                            if (ImGui.MenuItem("Copy Class Name (Obfuscated)"))
                                ImGui.SetClipboardText(className);
                            if (displayClassName != className && ImGui.MenuItem("Copy Class Name (Display)"))
                                ImGui.SetClipboardText(displayClassName);
                            ImGui.EndPopup();
                        }
                        
                        if (classExpanded)
                        {
                            foreach (var field in fields)
                            {
                                DrawField(comp.Pointer, field, compIndex, fieldIndex++);
                            }
                            ImGui.TreePop();
                        }
                        else
                        {
                            fieldIndex += fields.Count; // Skip indices for collapsed fields
                        }
                    }
                    else
                    {
                        // Direct class members (no extra header)
                        foreach (var field in fields)
                        {
                            DrawField(comp.Pointer, field, compIndex, fieldIndex++);
                        }
                    }
                }
                ImGui.TreePop();
            }

            // Properties section - grouped by declaring class
            if (data.Properties.Count > 0 && ImGui.TreeNode($"Properties ({data.Properties.Count})##props_{compIndex}"))
            {
                int propIndex = 0;
                
                foreach (var (className, displayClassName, props) in data.GetPropertiesByClass())
                {
                    bool isInherited = className != data.ClassName;
                    
                    if (isInherited)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, Theme.InheritedClass);
                        bool classExpanded = ImGui.TreeNode($"[{displayClassName}]##inherited_props_{className}_{compIndex}");
                        ImGui.PopStyleColor();
                        
                        // Right-click context menu for inherited class
                        if (ImGui.BeginPopupContextItem($"inherited_class_ctx_props_{className}_{compIndex}"))
                        {
                            if (ImGui.MenuItem("Copy Class Name (Obfuscated)"))
                                ImGui.SetClipboardText(className);
                            if (displayClassName != className && ImGui.MenuItem("Copy Class Name (Display)"))
                                ImGui.SetClipboardText(displayClassName);
                            ImGui.EndPopup();
                        }
                        
                        if (classExpanded)
                        {
                            foreach (var prop in props)
                            {
                                DrawProperty(comp.Pointer, prop, compIndex, propIndex++);
                            }
                            ImGui.TreePop();
                        }
                        else
                        {
                            propIndex += props.Count;
                        }
                    }
                    else
                    {
                        foreach (var prop in props)
                        {
                            DrawProperty(comp.Pointer, prop, compIndex, propIndex++);
                        }
                    }
                }
                ImGui.TreePop();
            }

            // Methods section - grouped by declaring class
            if (data.Methods.Count > 0 && ImGui.TreeNode($"Methods ({data.Methods.Count})##methods_{compIndex}"))
            {
                int methodIndex = 0;
                
                foreach (var (className, displayClassName, methods) in data.GetMethodsByClass())
                {
                    bool isInherited = className != data.ClassName;
                    
                    if (isInherited)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, Theme.InheritedClass);
                        bool classExpanded = ImGui.TreeNode($"[{displayClassName}]##inherited_methods_{className}_{compIndex}");
                        ImGui.PopStyleColor();
                        
                        // Right-click context menu for inherited class
                        if (ImGui.BeginPopupContextItem($"inherited_class_ctx_methods_{className}_{compIndex}"))
                        {
                            if (ImGui.MenuItem("Copy Class Name (Obfuscated)"))
                                ImGui.SetClipboardText(className);
                            if (displayClassName != className && ImGui.MenuItem("Copy Class Name (Display)"))
                                ImGui.SetClipboardText(displayClassName);
                            ImGui.EndPopup();
                        }
                        
                        if (classExpanded)
                        {
                            foreach (var method in methods)
                            {
                                DrawMethod(method, compIndex, methodIndex++);
                            }
                            ImGui.TreePop();
                        }
                        else
                        {
                            methodIndex += methods.Count;
                        }
                    }
                    else
                    {
                        foreach (var method in methods)
                        {
                            DrawMethod(method, compIndex, methodIndex++);
                        }
                    }
                }
                ImGui.TreePop();
            }
        }

        // Layout constants for inspector field rendering
        private const float APPROX_CHAR_WIDTH = 7f;
        private const float RIGHT_PADDING = 16f;
        private const float CONTENT_MARGIN = 4f;
        private const float EDIT_WIDGET_MAX = 140f;
        private const float STRING_WIDGET_MAX = 160f;

        /// <summary>
        /// Compute dynamic character limits for type and name columns based on available width.
        /// Allocates roughly 30% to type and 45% to name, leaving the rest for the value.
        /// </summary>
        private void ComputeColumnLimits(out int typeMaxChars, out int nameMaxChars)
        {
            float avail = ImGui.GetContentRegionAvailX();
            // Type gets ~30%, name gets ~45%, value gets the remaining ~25%
            float typeWidth = avail * 0.28f;
            float nameWidth = avail * 0.40f;
            typeMaxChars = Math.Max(6, (int)(typeWidth / APPROX_CHAR_WIDTH));
            nameMaxChars = Math.Max(8, (int)(nameWidth / APPROX_CHAR_WIDTH));
        }

        /// <summary>
        /// Position the cursor for right-aligned text. Never moves cursor backward (prevents overlap).
        /// Call this after SameLine(), before drawing the text.
        /// </summary>
        private void RightAlignValue(string text)
        {
            float textWidth = ImGui.CalcTextSize(text).X;
            // Use content region to respect indent/scrollbar, not raw window width
            float rightEdge = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvailX();
            float cursorX = ImGui.GetCursorPosX();
            float rightAligned = rightEdge - textWidth - RIGHT_PADDING;
            // Only right-align if there's room; otherwise just flow naturally
            if (rightAligned > cursorX)
                ImGui.SetCursorPosX(rightAligned);
        }

        /// <summary>
        /// Right-align a small widget (checkbox, small button) by reserving a fixed width from the right edge.
        /// </summary>
        private void RightAlignWidget(float widgetWidth)
        {
            float rightEdge = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvailX();
            float cursorX = ImGui.GetCursorPosX();
            float rightAligned = rightEdge - widgetWidth - RIGHT_PADDING;
            if (rightAligned > cursorX)
                ImGui.SetCursorPosX(rightAligned);
        }

        /// <summary>
        /// Draw a field with type display, value, and drill-down support.
        /// Supports editing for simple types (int, float, bool, string).
        /// Supports expanding arrays/lists.
        /// </summary>
        private void DrawField(IntPtr instance, FieldInfo field, int compIndex, int fieldIndex)
        {
            ImGui.PushID(fieldIndex);
            
            // Use deobfuscated type name if available
            string typeName = GetSimpleTypeName(field.DisplayTypeName);
            bool isObjectType = field.TypeEnum == Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_CLASS ||
                               field.TypeEnum == Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_OBJECT;
            bool isArrayType = field.TypeEnum == Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_SZARRAY ||
                               field.TypeEnum == Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_ARRAY;
            
            // Dynamic column sizing based on available width
            ComputeColumnLimits(out int typeMaxChars, out int nameMaxChars);
            
            // Type name in color
            string displayType = TruncateText(typeName, typeMaxChars);
            ImGui.TextColored(Theme.TypeName, displayType);
            if (displayType != typeName && ImGui.IsItemHovered())
                ImGui.SetTooltip(typeName);
            ImGui.SameLine();
            
            // Field name - use deobfuscated name
            string fieldName = field.DisplayName ?? "(unnamed)";
            string displayName = TruncateText(fieldName, nameMaxChars);
            
            // For arrays, use a TreeNode so it can be expanded
            if (isArrayType && instance != IntPtr.Zero)
            {
                // Get array info — works for both static and instance
                IntPtr arrPtr = field.IsStatic
                    ? ReadStaticObjectPtr(field.Pointer)
                    : GetArrayPointer(instance, field);
                int arrLength = arrPtr != IntPtr.Zero ? Il2CppBridge.mdb_array_length(arrPtr) : 0;
                
                bool isExpanded = ImGui.TreeNode($"{displayName} [{arrLength}]##arr_{fieldIndex}");
                if (displayName != fieldName && ImGui.IsItemHovered())
                    ImGui.SetTooltip(fieldName);
                
                if (isExpanded)
                {
                    DrawArrayElements(arrPtr, arrLength, field);
                    ImGui.TreePop();
                }
            }
            else
            {
                ImGui.Text(displayName);
                if (displayName != fieldName && ImGui.IsItemHovered())
                    ImGui.SetTooltip(fieldName);
                
                // For object types, add drill-down button
                if (isObjectType)
                {
                    ImGui.SameLine();
                    if (ImGui.SmallButton($">##drill_{fieldIndex}"))
                    {
                        DrillIntoField(instance, field, field.IsStatic);
                    }
                }
                
                // Value column - auto-flow after name
                ImGui.SameLine();
                
                // Pre-read value for read-only displays
                string preReadValue = null;
                bool isEditable = !field.IsStatic && instance != IntPtr.Zero && IsEditableType(field.TypeEnum);
                if (!isEditable)
                {
                    try
                    {
                        object val = field.IsStatic
                            ? ComponentReflector.ReadStaticFieldValue(field)
                            : (instance != IntPtr.Zero ? ComponentReflector.ReadFieldValue(instance, field) : null);
                        preReadValue = FormatValue(val);
                    }
                    catch { preReadValue = "(error)"; }
                }
                
                // Read and display/edit the field value (static or instance)
                if (field.IsStatic)
                {
                    DrawReadOnlyValue(preReadValue);
                }
                else if (instance != IntPtr.Zero)
                {
                    if (isEditable)
                    {
                        DrawFieldValue(instance, field, fieldIndex);
                    }
                    else
                    {
                        DrawReadOnlyValue(preReadValue);
                    }
                }
                else
                {
                    ImGui.TextDisabled("(N/A)");
                }
                
                // Right-click context menu for field
                if (ImGui.BeginPopupContextItem("field_ctx"))
                {
                    if (ImGui.MenuItem("Copy Field Name"))
                        ImGui.SetClipboardText(field.Name);
                    if (field.DisplayName != field.Name && ImGui.MenuItem("Copy Display Name"))
                        ImGui.SetClipboardText(field.DisplayName);
                    if (ImGui.MenuItem("Copy Type"))
                        ImGui.SetClipboardText(field.TypeName);
                    if (field.DisplayTypeName != field.TypeName && ImGui.MenuItem("Copy Display Type"))
                        ImGui.SetClipboardText(field.DisplayTypeName);
                    
                    // Try to get and copy value
                    try
                    {
                        object val = field.IsStatic
                            ? ComponentReflector.ReadStaticFieldValue(field)
                            : (instance != IntPtr.Zero ? ComponentReflector.ReadFieldValue(instance, field) : null);
                        if (val != null)
                        {
                            string valStr = FormatValue(val);
                            if (ImGui.MenuItem($"Copy Value: {TruncateText(valStr, 30)}"))
                                ImGui.SetClipboardText(valStr);
                        }
                    }
                    catch { }
                    ImGui.EndPopup();
                }
            }
            
            ImGui.PopID();
        }
        
        /// <summary>
        /// Draw field value with editing support for simple types.
        /// </summary>
        private void DrawFieldValue(IntPtr instance, FieldInfo field, int fieldIndex)
        {
            try
            {
                switch (field.TypeEnum)
                {
                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_BOOLEAN:
                        {
                            object val = ComponentReflector.ReadFieldValue(instance, field);
                            bool boolVal = val is bool b ? b : false;
                            // Right-align the checkbox (approx 20px wide)
                            RightAlignWidget(20f);
                            if (ImGui.Checkbox($"##val_{fieldIndex}", ref boolVal))
                            {
                                ComponentReflector.WriteFieldValue(instance, field, boolVal);
                            }
                        }
                        break;
                        
                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_I4:
                        {
                            object val = ComponentReflector.ReadFieldValue(instance, field);
                            int intVal = val is int i ? i : 0;
                            // Cap widget width, right-aligned
                            float avail = ImGui.GetContentRegionAvailX();
                            float w = Math.Min(EDIT_WIDGET_MAX, avail - RIGHT_PADDING);
                            RightAlignWidget(w);
                            ImGui.SetNextItemWidth(w);
                            if (ImGui.DragInt($"##val_{fieldIndex}", ref intVal, 1.0f))
                            {
                                ComponentReflector.WriteFieldValue(instance, field, intVal);
                            }
                        }
                        break;
                        
                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_R4:
                        {
                            object val = ComponentReflector.ReadFieldValue(instance, field);
                            float floatVal = val is float f ? f : 0f;
                            float avail2 = ImGui.GetContentRegionAvailX();
                            float w2 = Math.Min(EDIT_WIDGET_MAX, avail2 - RIGHT_PADDING);
                            RightAlignWidget(w2);
                            ImGui.SetNextItemWidth(w2);
                            if (ImGui.DragFloat($"##val_{fieldIndex}", ref floatVal, 0.1f))
                            {
                                ComponentReflector.WriteFieldValue(instance, field, floatVal);
                            }
                        }
                        break;
                        
                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_STRING:
                        {
                            // Always read current value from field
                            object currentVal = ComponentReflector.ReadFieldValue(instance, field);
                            string currentStr = currentVal as string ?? "";
                            
                            // Use cache for editing, initialize from current value if not in cache
                            if (!_stringEditCache.TryGetValue(field.Pointer, out string editStr))
                            {
                                editStr = currentStr;
                                _stringEditCache[field.Pointer] = editStr;
                            }
                            
                            // Cap string input width, leave room for Set button
                            float availStr = ImGui.GetContentRegionAvailX();
                            float strW = Math.Min(STRING_WIDGET_MAX, availStr - 50f);
                            RightAlignWidget(strW + 40f); // account for Set button
                            ImGui.SetNextItemWidth(strW);
                            if (ImGui.InputText($"##val_{fieldIndex}", ref editStr, 256))
                            {
                                _stringEditCache[field.Pointer] = editStr;
                            }
                            
                            // Apply button to commit the string change
                            ImGui.SameLine();
                            if (ImGui.SmallButton("Set"))
                            {
                                // Get the current cached value to write
                                string valueToWrite = _stringEditCache.TryGetValue(field.Pointer, out string cached) ? cached : editStr;
                                
                                // Create new IL2CPP string and set the field
                                IntPtr newStr = Il2CppBridge.mdb_string_new(valueToWrite);
                                
                                if (newStr != IntPtr.Zero)
                                {
                                    bool result = WriteStringField(instance, field.Pointer, newStr);
                                    
                                    // Clear cache so next frame reads the new value
                                    _stringEditCache.Remove(field.Pointer);
                                }
                            }
                            
                            // Show if value differs from field value
                            if (_stringEditCache.TryGetValue(field.Pointer, out string cachedVal) && cachedVal != currentStr)
                            {
                                ImGui.SameLine();
                                ImGui.TextColored(Theme.Highlight, "*");
                            }
                        }
                        break;
                        
                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_ENUM:
                        {
                            object val = ComponentReflector.ReadFieldValue(instance, field);
                            int enumVal = val is int i ? i : 0;
                            float avail3 = ImGui.GetContentRegionAvailX();
                            float w3 = Math.Min(EDIT_WIDGET_MAX, avail3 - RIGHT_PADDING);
                            RightAlignWidget(w3);
                            ImGui.SetNextItemWidth(w3);
                            if (ImGui.DragInt($"##val_{fieldIndex}", ref enumVal, 1.0f))
                            {
                                ComponentReflector.WriteFieldValue(instance, field, enumVal);
                            }
                        }
                        break;
                        
                    default:
                        {
                            object value = ComponentReflector.ReadFieldValue(instance, field);
                            string valueStr = FormatValue(value);
                            float availDef = ImGui.GetContentRegionAvailX();
                            float wDef = Math.Min(EDIT_WIDGET_MAX, availDef - RIGHT_PADDING);
                            RightAlignWidget(wDef);
                            ImGui.SetNextItemWidth(wDef);
                            ImGui.BeginDisabled();
                            ImGui.InputText($"##val_{fieldIndex}", ref valueStr, (uint)valueStr.Length + 1, ImGuiInputTextFlags.ReadOnly);
                            ImGui.EndDisabled();
                        }
                        break;
                }
            }
            catch
            {
                ImGui.TextDisabled("(error)");
            }
        }
        
        /// <summary>
        /// Display a read-only value in a disabled input field for visual consistency.
        /// </summary>
        private void DrawReadOnlyValue(string valueStr)
        {
            if (valueStr == null) valueStr = "(N/A)";
            float avail = ImGui.GetContentRegionAvailX();
            float w = Math.Min(EDIT_WIDGET_MAX, avail - RIGHT_PADDING);
            RightAlignWidget(w);
            ImGui.SetNextItemWidth(w);
            ImGui.BeginDisabled();
            ImGui.InputText("##ro", ref valueStr, (uint)valueStr.Length + 1, ImGuiInputTextFlags.ReadOnly);
            ImGui.EndDisabled();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(valueStr);
        }

        /// <summary>
        /// Check if a field type is editable (has a dedicated edit widget).
        /// </summary>
        private bool IsEditableType(int typeEnum)
        {
            return typeEnum == Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_BOOLEAN ||
                   typeEnum == Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_I4 ||
                   typeEnum == Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_R4 ||
                   typeEnum == Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_STRING ||
                   typeEnum == Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_ENUM;
        }

        /// <summary>
        /// Read a static field's object pointer.
        /// </summary>
        private IntPtr ReadStaticObjectPtr(IntPtr fieldPtr)
        {
            unsafe
            {
                IntPtr objPtr = IntPtr.Zero;
                IntPtr buffer = new IntPtr(&objPtr);
                Il2CppBridge.mdb_field_static_get_value(fieldPtr, buffer);
                return objPtr;
            }
        }

        /// <summary>
        /// Write an IL2CPP string pointer to a field.
        /// </summary>
        private bool WriteStringField(IntPtr instance, IntPtr fieldPtr, IntPtr strPtr)
        {
            unsafe
            {
                IntPtr buffer = new IntPtr(&strPtr);
                return Il2CppBridge.mdb_field_set_value_direct(instance, fieldPtr, buffer, IntPtr.Size);
            }
        }
        
        /// <summary>
        /// Get pointer to array field value.
        /// </summary>
        private IntPtr GetArrayPointer(IntPtr instance, FieldInfo field)
        {
            unsafe
            {
                IntPtr arrPtr = IntPtr.Zero;
                IntPtr buffer = new IntPtr(&arrPtr);
                if (Il2CppBridge.mdb_field_get_value_direct(instance, field.Pointer, buffer, IntPtr.Size))
                {
                    return arrPtr;
                }
                return IntPtr.Zero;
            }
        }
        
        /// <summary>
        /// Draw array elements when expanded.
        /// </summary>
        private void DrawArrayElements(IntPtr arrPtr, int length, FieldInfo field)
        {
            if (arrPtr == IntPtr.Zero || length == 0)
            {
                ImGui.TextDisabled("  (empty)");
                return;
            }
            
            // Limit displayed elements to avoid performance issues
            int displayCount = Math.Min(length, 50);
            
            // Try to get element type from field type name (e.g., "UnityEngine.Renderer[]" -> "Renderer")
            // Use deobfuscated type name if available
            string elementTypeName = field.DisplayTypeName ?? "Object";
            if (elementTypeName.EndsWith("[]"))
                elementTypeName = elementTypeName.Substring(0, elementTypeName.Length - 2);
            elementTypeName = GetSimpleTypeName(elementTypeName);
            
            for (int i = 0; i < displayCount; i++)
            {
                ImGui.PushID(i);
                
                IntPtr elementPtr = Il2CppBridge.mdb_array_get_element(arrPtr, i);
                
                ImGui.TextDisabled($"[{i}]");
                ImGui.SameLine();
                ImGui.TextColored(Theme.TypeName, elementTypeName);
                ImGui.SameLine();
                
                if (elementPtr == IntPtr.Zero)
                {
                    ImGui.TextDisabled("(null)");
                }
                else
                {
                    // Try to get the actual object name or type - use deobfuscated name
                    string elementName = GetObjectDisplayName(elementPtr);
                    ImGui.Text(elementName);
                    
                    // Add drill-down button for object elements
                    ImGui.SameLine();
                    if (ImGui.SmallButton($">##elem_{i}"))
                    {
                        DrillIntoObject(elementPtr, elementTypeName);
                    }
                }
                
                ImGui.PopID();
            }
            
            if (length > displayCount)
            {
                ImGui.TextDisabled($"  ... and {length - displayCount} more elements");
            }
        }
        
        /// <summary>
        /// Get a display name for an IL2CPP object.
        /// </summary>
        private string GetObjectDisplayName(IntPtr objPtr)
        {
            if (objPtr == IntPtr.Zero)
                return "(null)";
            
            try
            {
                IntPtr klass = Il2CppBridge.mdb_object_get_class(objPtr);
                if (klass != IntPtr.Zero)
                {
                    string className = Il2CppBridge.GetClassName(klass);
                    // Use deobfuscated name if available
                    string displayName = DeobfuscationHelper.GetTypeName(className);
                    return $"[{displayName}]";
                }
            }
            catch { }
            
            return $"0x{objPtr.ToInt64():X}";
        }
        
        /// <summary>
        /// Drill into an object by pointer.
        /// </summary>
        private void DrillIntoObject(IntPtr objPtr, string typeName)
        {
            if (objPtr == IntPtr.Zero)
                return;
            
            try
            {
                IntPtr klass = Il2CppBridge.mdb_object_get_class(objPtr);
                if (klass == IntPtr.Zero)
                    return;
                
                string className = Il2CppBridge.GetClassName(klass);
                var reflectionData = ComponentReflector.GetReflectionData(objPtr);
                
                if (reflectionData != null)
                {
                    _inspectionStack.Push(new InspectionContext
                    {
                        Instance = objPtr,
                        TypeName = className,  // Keep original for IL2CPP calls, DisplayTypeName handles display
                        ReflectionData = reflectionData
                    });
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogInternal(LOG_TAG, $"[ERROR] DrillIntoObject failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Draw a property with type display and value.
        /// </summary>
        private void DrawProperty(IntPtr instance, PropertyInfo prop, int compIndex, int propIndex)
        {
            if (!prop.CanRead) return;
            
            ImGui.PushID(propIndex);
            
            // Dynamic column sizing based on available width
            ComputeColumnLimits(out int typeMaxChars, out int nameMaxChars);
            
            // Type name in color
            string typeName = GetSimpleTypeName(prop.DisplayTypeName);
            string displayType = TruncateText(typeName, typeMaxChars);
            ImGui.TextColored(Theme.TypeName, displayType);
            if (displayType != typeName && ImGui.IsItemHovered())
                ImGui.SetTooltip($"{typeName} ({(prop.CanWrite ? "read/write" : "read-only")})");
            ImGui.SameLine();
            
            // Property name
            string propName = prop.DisplayName ?? "(unnamed)";
            string displayName = TruncateText(propName, nameMaxChars);
            ImGui.Text(displayName);
            if (displayName != propName && ImGui.IsItemHovered())
                ImGui.SetTooltip(propName);
            
            // Value column - right-aligned
            ImGui.SameLine();
            
            // Pre-read value text for right-alignment
            string valueStr = null;
            if (instance != IntPtr.Zero && prop.GetterMethod != IntPtr.Zero)
            {
                try
                {
                    object value = ComponentReflector.InvokePropertyGetter(instance, prop);
                    valueStr = FormatValue(value);
                }
                catch { valueStr = "(error)"; }
            }
            
            string displayStr = valueStr ?? "(N/A)";
            
            // Show value in a disabled input field for visual consistency
            float propAvail = ImGui.GetContentRegionAvailX();
            float propW = Math.Min(EDIT_WIDGET_MAX, propAvail - RIGHT_PADDING);
            RightAlignWidget(propW);
            ImGui.SetNextItemWidth(propW);
            ImGui.BeginDisabled();
            ImGui.InputText("##pval", ref displayStr, (uint)displayStr.Length + 1, ImGuiInputTextFlags.ReadOnly);
            ImGui.EndDisabled();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(displayStr);
            
            // Right-click context menu
            if (ImGui.BeginPopupContextItem("prop_ctx"))
            {
                string accessStr = prop.CanWrite ? "Read/Write" : "Read-only";
                ImGui.TextDisabled(accessStr);
                ImGui.Separator();
                if (ImGui.MenuItem("Copy Property Name"))
                    ImGui.SetClipboardText(prop.Name);
                if (prop.DisplayName != prop.Name && ImGui.MenuItem("Copy Display Name"))
                    ImGui.SetClipboardText(prop.DisplayName);
                if (ImGui.MenuItem("Copy Type"))
                    ImGui.SetClipboardText(prop.TypeName);
                ImGui.EndPopup();
            }
            
            ImGui.PopID();
        }
        
        /// <summary>
        /// Truncate text to fit within a maximum character count.
        /// </summary>
        private string TruncateText(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxChars)
                return text;
            
            if (maxChars <= 3)
                return text.Substring(0, maxChars);
            
            return text.Substring(0, maxChars - 2) + "..";
        }
        
        /// <summary>
        /// Format a value for display.
        /// </summary>
        private string FormatValue(object value)
        {
            if (value == null)
                return "(null)";
            
            if (value is bool b)
                return b ? "true" : "false";
            
            if (value is float f)
                return f.ToString("F3");
            
            if (value is double d)
                return d.ToString("F3");
            
            if (value is string s)
            {
                if (s.Length > 100)
                    return $"\"{s.Substring(0, 97)}...\"";
                return $"\"{s}\"";
            }
            
            if (value is IntPtr ptr)
            {
                if (ptr == IntPtr.Zero)
                    return "(null)";
                return $"0x{ptr.ToInt64():X}";
            }
            
            // Handle System.Numerics vector types returned from ReadVector2/3
            if (value is Vector2 v2)
                return $"({v2.X:F2}, {v2.Y:F2})";
            
            if (value is Vector3 v3)
                return $"({v3.X:F2}, {v3.Y:F2}, {v3.Z:F2})";
            
            if (value is Vector4 v4)
                return $"({v4.X:F2}, {v4.Y:F2}, {v4.Z:F2}, {v4.W:F2})";
            
            // Check for tuple types (Vector3, Color, etc. returned as tuples)
            var valueType = value.GetType();
            if (valueType.IsGenericType && valueType.Name.StartsWith("ValueTuple"))
            {
                var fields = valueType.GetFields();
                if (fields.Length == 3)
                {
                    // Likely Vector3 or Color RGB
                    var v1 = fields[0].GetValue(value);
                    var v2_ = fields[1].GetValue(value);
                    var v3_ = fields[2].GetValue(value);
                    if (v1 is float f1 && v2_ is float f2 && v3_ is float f3)
                        return $"({f1:F2}, {f2:F2}, {f3:F2})";
                }
                else if (fields.Length == 4)
                {
                    // Likely Color RGBA or Quaternion
                    var v1 = fields[0].GetValue(value);
                    var v2_ = fields[1].GetValue(value);
                    var v3_ = fields[2].GetValue(value);
                    var v4_ = fields[3].GetValue(value);
                    if (v1 is float f1 && v2_ is float f2 && v3_ is float f3 && v4_ is float f4)
                        return $"({f1:F2}, {f2:F2}, {f3:F2}, {f4:F2})";
                }
                else if (fields.Length == 2)
                {
                    // Likely Vector2
                    var v1 = fields[0].GetValue(value);
                    var v2_ = fields[1].GetValue(value);
                    if (v1 is float f1 && v2_ is float f2)
                        return $"({f1:F2}, {f2:F2})";
                }
            }
            
            return value.ToString();
        }

        /// <summary>
        /// Drill into an object field to inspect its members.
        /// </summary>
        private void DrillIntoField(IntPtr instance, FieldInfo field, bool isStatic)
        {
            try
            {
                IntPtr objPtr = IntPtr.Zero;
                
                if (isStatic)
                {
                    // For static fields, use the static field API
                    unsafe
                    {
                        IntPtr buffer = new IntPtr(&objPtr);
                        Il2CppBridge.mdb_field_static_get_value(field.Pointer, buffer);
                    }
                }
                else
                {
                    // For instance fields, use the instance field API
                    unsafe
                    {
                        IntPtr buffer = new IntPtr(&objPtr);
                        if (!Il2CppBridge.mdb_field_get_value_direct(instance, field.Pointer, buffer, IntPtr.Size))
                        {
                            ModLogger.LogInternal(LOG_TAG, "[ERROR] Failed to read field value");
                            return;
                        }
                    }
                }
                
                if (objPtr == IntPtr.Zero)
                {
                    ModLogger.LogInternal(LOG_TAG, "[INFO] Field value is null, cannot drill in");
                    return;
                }
                
                // Get the class of this object
                IntPtr klass = Il2CppBridge.mdb_object_get_class(objPtr);
                if (klass == IntPtr.Zero)
                {
                    ModLogger.LogInternal(LOG_TAG, "[ERROR] Could not get class for field object");
                    return;
                }
                
                string className = Il2CppBridge.GetClassName(klass);
                ModLogger.LogInternal(LOG_TAG, $"[INFO] Drilling into {field.Name} of type {className}");
                
                // Get reflection data for this object
                var reflectionData = ComponentReflector.GetReflectionData(objPtr);
                
                if (reflectionData != null)
                {
                    _inspectionStack.Push(new InspectionContext
                    {
                        Instance = objPtr,
                        TypeName = className,
                        ReflectionData = reflectionData
                    });
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogInternal(LOG_TAG, $"[ERROR] DrillIntoField failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Get a simplified type name (remove namespace).
        /// </summary>
        private string GetSimpleTypeName(string fullTypeName)
        {
            if (string.IsNullOrEmpty(fullTypeName)) return "?";
            
            int lastDot = fullTypeName.LastIndexOf('.');
            if (lastDot >= 0 && lastDot < fullTypeName.Length - 1)
                return fullTypeName.Substring(lastDot + 1);
            
            return fullTypeName;
        }

        /// <summary>
        /// Draw a method entry with return type and parameter info.
        /// </summary>
        private void DrawMethod(MethodInfo method, int compIndex, int methodIndex)
        {
            ImGui.PushID(methodIndex);
            
            // Use deobfuscated name if available
            string name = method.DisplayName;
            
            // Just check for null/empty - show whatever we get
            if (string.IsNullOrEmpty(name))
            {
                ImGui.PopID();
                return;
            }
            
            // Dynamic layout
            ComputeColumnLimits(out int typeMaxChars, out int _nameMax);
            
            // Return type in color
            string retType = GetSimpleTypeName(method.DisplayReturnTypeName ?? "void");
            string displayRetType = TruncateText(retType, typeMaxChars);
            ImGui.TextColored(Theme.TypeName, displayRetType);
            if (displayRetType != retType && ImGui.IsItemHovered())
                ImGui.SetTooltip(retType);
            ImGui.SameLine();
            
            // Method name with parameter signature
            string paramSig = method.ParameterSignature ?? $"{method.ParameterCount} params";
            string methodLabel = $"{name}({paramSig})";
            ImGui.TextColored(Theme.MethodName, methodLabel);
            if (ImGui.IsItemHovered())
            {
                string fullSig = $"{retType} {name}({paramSig})";
                if (method.IsStatic) fullSig = "static " + fullSig;
                ImGui.SetTooltip(fullSig);
            }
            
            // Invoke button for parameterless methods
            if (method.ParameterCount == 0 && !method.IsStatic)
            {
                ImGui.SameLine();
                if (ImGui.SmallButton($"Invoke##invoke_{methodIndex}"))
                {
                    InvokeMethod(method);
                }
            }
            
            // Right-click context menu
            if (ImGui.BeginPopupContextItem("method_ctx"))
            {
                string fullSig = $"{retType} {name}({paramSig})";
                if (method.IsStatic) fullSig = "static " + fullSig;
                if (ImGui.MenuItem("Copy Signature"))
                    ImGui.SetClipboardText(fullSig);
                if (ImGui.MenuItem("Copy Method Name"))
                    ImGui.SetClipboardText(method.Name);
                ImGui.EndPopup();
            }
            
            ImGui.PopID();
        }
        
        /// <summary>
        /// Invoke a parameterless method.
        /// </summary>
        private void InvokeMethod(MethodInfo method)
        {
            try
            {
                // Get the current instance from stack or component
                IntPtr instance = IntPtr.Zero;
                if (_inspectionStack.Count > 0)
                {
                    instance = _inspectionStack.Peek().Instance;
                }
                
                ModLogger.LogInternal(LOG_TAG, $"[INFO] Invoking method: {method.Name}");
                
                IntPtr exception;
                IntPtr result = Il2CppBridge.mdb_invoke_method(method.Pointer, instance, Array.Empty<IntPtr>(), out exception);
                
                if (exception != IntPtr.Zero)
                {
                    ModLogger.LogInternal(LOG_TAG, $"[ERROR] Method threw exception");
                }
                else
                {
                    ModLogger.LogInternal(LOG_TAG, $"[INFO] Method invoked successfully, result ptr: 0x{result.ToInt64():X}");
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogInternal(LOG_TAG, $"[ERROR] InvokeMethod failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Check if a method name looks valid (no garbage characters).
        /// </summary>
        private bool IsValidMethodName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length < 1 || name.Length > 200) return false;
            
            // First character must be a letter or underscore
            char first = name[0];
            if (!char.IsLetter(first) && first != '_' && first != '.' && first != '<') 
                return false;
            
            // Check for control characters or very unusual chars
            foreach (char c in name)
            {
                // Allow printable ASCII and common identifier chars
                if (c < 32 || c > 126)
                    return false;
            }
            return true;
        }
    }
}
