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

        // Cached IL2CPP pointers
        private IntPtr _gameObjectClass;
        private IntPtr _transformClass;
        private IntPtr _componentClass;
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
        
        public class InspectionContext
        {
            public IntPtr Instance { get; set; }
            public string TypeName { get; set; }
            public ComponentReflectionData ReflectionData { get; set; }
            
            /// <summary>
            /// Display type name (deobfuscated if available).
            /// </summary>
            public string DisplayTypeName => DeobfuscationHelper.GetTypeName(TypeName);
        }

        public HierarchyNode Target => _target;

        public class ComponentInfo
        {
            public IntPtr Pointer { get; set; }
            public string TypeName { get; set; }
            public bool IsExpanded { get; set; }
            public ComponentReflectionData ReflectionData { get; set; }
            
            /// <summary>
            /// Display type name (deobfuscated if available).
            /// </summary>
            public string DisplayTypeName => DeobfuscationHelper.GetTypeName(TypeName);
        }

        /// <summary>
        /// Initialize IL2CPP class pointers.
        /// </summary>
        public bool Initialize()
        {
            if (_classesResolved) return true;

            try
            {
                _gameObjectClass = Il2CppBridge.mdb_find_class("UnityEngine.CoreModule", "UnityEngine", "GameObject");
                _transformClass = Il2CppBridge.mdb_find_class("UnityEngine.CoreModule", "UnityEngine", "Transform");
                _componentClass = Il2CppBridge.mdb_find_class("UnityEngine.CoreModule", "UnityEngine", "Component");

                _classesResolved = _gameObjectClass != IntPtr.Zero && 
                                   _transformClass != IntPtr.Zero && 
                                   _componentClass != IntPtr.Zero;

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

                    string typeName = GetComponentTypeName(compPtr);

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
                _transformPtr = GetTransform(_target.Pointer);
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
                SetGameObjectActive(_target.Pointer, active);
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
                ImGui.SameLine(100);
                ImGui.SetNextItemWidth(-1);
                if (ImGui.DragFloat3("##pos", ref _position, 0.1f))
                {
                    if (_transformPtr != IntPtr.Zero)
                        Il2CppBridge.mdb_transform_set_local_position(_transformPtr, _position.X, _position.Y, _position.Z);
                }

                // Rotation
                ImGui.Text("Rotation:");
                ImGui.SameLine(100);
                ImGui.SetNextItemWidth(-1);
                if (ImGui.DragFloat3("##rot", ref _rotation, 1.0f))
                {
                    if (_transformPtr != IntPtr.Zero)
                        Il2CppBridge.mdb_transform_set_local_euler_angles(_transformPtr, _rotation.X, _rotation.Y, _rotation.Z);
                }

                // Scale
                ImGui.Text("Scale:");
                ImGui.SameLine(100);
                ImGui.SetNextItemWidth(-1);
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
            ImGui.TextColored(new Vector4(1.0f, 0.8f, 0.2f, 1.0f), current.DisplayTypeName ?? "Object");
            
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
                        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.6f, 0.6f, 0.8f, 1.0f));
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
                        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.6f, 0.6f, 0.8f, 1.0f));
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
                        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.6f, 0.6f, 0.8f, 1.0f));
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
                        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.6f, 0.6f, 0.8f, 1.0f));
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
                        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.6f, 0.6f, 0.8f, 1.0f));
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
                        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.6f, 0.6f, 0.8f, 1.0f));
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

        // Column width constants for field/property display
        private const float VALUE_COLUMN_WIDTH = 220f;  // Width reserved for value column on right (includes padding)
        private const float EDIT_WIDGET_WIDTH = 120f;
        private const int MAX_NAME_CHARS = 32;

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
            
            // Static indicator (use actual IL2CPP flag)
            if (field.IsStatic)
            {
                ImGui.TextDisabled("[S]");
                ImGui.SameLine();
            }
            
            // Type name in color (truncate if needed)
            string displayType = TruncateText(typeName, 12);
            ImGui.TextColored(new Vector4(0.4f, 0.7f, 1.0f, 1.0f), displayType);
            ImGui.SameLine();
            
            // Calculate remaining space for field name - use deobfuscated name
            int nameMaxChars = MAX_NAME_CHARS - displayType.Length - (field.IsStatic ? 4 : 0);
            string fieldName = field.DisplayName ?? "(unnamed)";
            string displayName = TruncateText(fieldName, Math.Max(10, nameMaxChars));
            
            // For arrays, use a TreeNode so it can be expanded
            string arrayKey = $"{compIndex}_{fieldIndex}";
            if (isArrayType && !field.IsStatic && instance != IntPtr.Zero)
            {
                // Get array info
                IntPtr arrPtr = GetArrayPointer(instance, field);
                int arrLength = arrPtr != IntPtr.Zero ? Il2CppBridge.mdb_array_length(arrPtr) : 0;
                
                bool isExpanded = ImGui.TreeNode($"{displayName} [{arrLength}]##arr_{fieldIndex}");
                
                if (isExpanded)
                {
                    DrawArrayElements(arrPtr, arrLength, field);
                    ImGui.TreePop();
                }
            }
            else
            {
                ImGui.Text(displayName);
                
                // For object types, add drill-down button
                if (isObjectType)
                {
                    ImGui.SameLine();
                    if (ImGui.SmallButton($">##drill_{fieldIndex}"))
                    {
                        DrillIntoField(instance, field, field.IsStatic);
                    }
                }
                
                // Value column - right-justified
                float valueStart = ImGui.GetWindowWidth() - VALUE_COLUMN_WIDTH;
                ImGui.SameLine(valueStart > 150 ? valueStart : 150);
                
                // Read and display/edit the field value
                if (!field.IsStatic && instance != IntPtr.Zero)
                {
                    DrawFieldValue(instance, field, fieldIndex);
                }
                else if (field.IsStatic)
                {
                    ImGui.TextDisabled("(static)");
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
                    if (!field.IsStatic && instance != IntPtr.Zero)
                    {
                        try
                        {
                            object val = ComponentReflector.ReadFieldValue(instance, field);
                            string valStr = FormatValue(val);
                            if (ImGui.MenuItem($"Copy Value: {TruncateText(valStr, 20)}"))
                                ImGui.SetClipboardText(valStr);
                        }
                        catch { }
                    }
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
                ImGui.SetNextItemWidth(EDIT_WIDGET_WIDTH);
                
                switch (field.TypeEnum)
                {
                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_BOOLEAN:
                        {
                            object val = ComponentReflector.ReadFieldValue(instance, field);
                            bool boolVal = val is bool b ? b : false;
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
                            
                            ImGui.SetNextItemWidth(EDIT_WIDGET_WIDTH + 30);
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
                                ImGui.TextColored(new Vector4(1.0f, 0.8f, 0.0f, 1.0f), "*");
                            }
                        }
                        break;
                        
                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_ENUM:
                        {
                            object val = ComponentReflector.ReadFieldValue(instance, field);
                            int enumVal = val is int i ? i : 0;
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
                            ImGui.TextColored(new Vector4(0.8f, 0.9f, 0.6f, 1.0f), valueStr);
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
                ImGui.TextColored(new Vector4(0.4f, 0.7f, 1.0f, 1.0f), elementTypeName);
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
            
            // Read/Write indicator
            string rwIndicator = prop.CanWrite ? "[RW]" : "[R]";
            ImGui.TextDisabled(rwIndicator);
            ImGui.SameLine();
            
            // Property name - use deobfuscated name (truncate if needed)
            string propName = prop.DisplayName ?? "(unnamed)";
            string displayName = TruncateText(propName, MAX_NAME_CHARS - 5); // Account for [RW] prefix
            ImGui.Text(displayName);
            
            // Value column - right-justified
            float valueStart = ImGui.GetWindowWidth() - VALUE_COLUMN_WIDTH;
            ImGui.SameLine(valueStart > 150 ? valueStart : 150);
            
            // Read and display the property value
            if (instance != IntPtr.Zero && prop.GetterMethod != IntPtr.Zero)
            {
                try
                {
                    object value = ComponentReflector.InvokePropertyGetter(instance, prop);
                    string valueStr = FormatValue(value);
                    ImGui.TextColored(new Vector4(0.8f, 0.9f, 0.6f, 1.0f), valueStr);
                }
                catch
                {
                    ImGui.TextDisabled("(error)");
                }
            }
            else
            {
                ImGui.TextDisabled("(N/A)");
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
                if (s.Length > 50)
                    return $"\"{s.Substring(0, 47)}...\"";
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
        /// Draw a method entry.
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
            
            // Static indicator
            if (method.IsStatic)
            {
                ImGui.TextDisabled("[S]");
                ImGui.SameLine();
            }
            
            // Show method with param count
            ImGui.Text($"{name}({method.ParameterCount})");
            
            // NOTE: Method invocation disabled - causes crashes
            // TODO: Investigate proper IL2CPP method invocation
            /*
            // Add invoke button for parameterless methods
            if (method.ParameterCount == 0)
            {
                ImGui.SameLine();
                if (ImGui.SmallButton($"Invoke##invoke_{methodIndex}"))
                {
                    InvokeMethod(method);
                }
            }
            */
            
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
        
        /// <summary>
        /// Check if a field is likely static (event delegates, constants, etc.)
        /// </summary>
        private bool IsLikelyStaticField(FieldInfo field)
        {
            if (field.IsStatic) return true;
            
            string name = field.Name;
            if (string.IsNullOrEmpty(name)) return true;
            
            // Common static field patterns
            if (name.StartsWith("s_")) return true;  // Static prefix
            if (name.StartsWith("k") && name.Length > 1 && char.IsUpper(name[1])) return true;  // kConstant
            if (name.Contains("WillRender")) return true;  // Event delegates
            if (name.Contains("Callback")) return true;  // Event callbacks
            if (name.Contains("Event") && !name.StartsWith("m_")) return true;  // Events
            
            // Check type name for delegate types
            if (field.TypeName != null)
            {
                if (field.TypeName.Contains("Action") || 
                    field.TypeName.Contains("Func") ||
                    field.TypeName.Contains("Delegate") ||
                    field.TypeName.Contains("Event"))
                    return true;
            }
            
            return false;
        }

        // ========== Private Helpers ==========

        private IntPtr GetTransform(IntPtr goPtr)
        {
            try
            {
                IntPtr method = Il2CppBridge.mdb_get_method(_gameObjectClass, "get_transform", 0);
                if (method == IntPtr.Zero) return IntPtr.Zero;

                IntPtr exception;
                return Il2CppBridge.mdb_invoke_method(method, goPtr, Array.Empty<IntPtr>(), out exception);
            }
            catch { return IntPtr.Zero; }
        }

        private Vector3 GetVector3Property(IntPtr obj, string methodName)
        {
            try
            {
                IntPtr method = Il2CppBridge.mdb_get_method(_transformClass, methodName, 0);
                if (method == IntPtr.Zero)
                {
                    ModLogger.LogInternal(LOG_TAG, $"[WARN] Method not found: {methodName}");
                    return Vector3.Zero;
                }

                IntPtr exception;
                IntPtr result = Il2CppBridge.mdb_invoke_method(method, obj, Array.Empty<IntPtr>(), out exception);

                if (exception != IntPtr.Zero)
                {
                    ModLogger.LogInternal(LOG_TAG, $"[WARN] Exception calling {methodName}");
                    return Vector3.Zero;
                }

                // For value types, IL2CPP returns a boxed object
                // The actual data starts at offset 0x10 (16 bytes) after the object header on 64-bit
                // Or we need to use il2cpp_object_unbox
                if (result == IntPtr.Zero) return Vector3.Zero;

                // Try to read the Vector3 data
                // The boxed object has header + data, data starts at object + 2*sizeof(void*) typically
                unsafe
                {
                    // For 64-bit IL2CPP, object header is typically 16 bytes (2 pointers)
                    byte* basePtr = (byte*)result;
                    float* dataPtr = (float*)(basePtr + 16); // Skip object header
                    return new Vector3(dataPtr[0], dataPtr[1], dataPtr[2]);
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogInternal(LOG_TAG, $"[ERROR] GetVector3Property failed: {ex.Message}");
                return Vector3.Zero;
            }
        }

        private string GetComponentTypeName(IntPtr compPtr)
        {
            try
            {
                // Get the object's class
                IntPtr klass = Il2CppBridge.mdb_object_get_class(compPtr);
                if (klass == IntPtr.Zero) return null;

                return Il2CppBridge.GetClassName(klass);
            }
            catch { return null; }
        }

        private void SetGameObjectActive(IntPtr goPtr, bool active)
        {
            if (goPtr == IntPtr.Zero)
            {
                ModLogger.LogInternal(LOG_TAG, "[WARN] SetActive called with null pointer");
                return;
            }
            
            try
            {
                // Use the native helper that properly handles bool boxing for IL2CPP
                bool success = Il2CppBridge.mdb_gameobject_set_active(goPtr, active);
                if (!success)
                {
                    ModLogger.LogInternal(LOG_TAG, "[WARN] mdb_gameobject_set_active returned false");
                }
                else
                {
                    ModLogger.LogInternal(LOG_TAG, $"[INFO] SetActive({active}) succeeded");
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogInternal(LOG_TAG, $"[ERROR] SetActive failed: {ex.Message}");
            }
        }
    }
}
