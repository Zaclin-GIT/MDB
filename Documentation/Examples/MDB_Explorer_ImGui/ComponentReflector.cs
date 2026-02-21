// ==============================
// ComponentReflector - IL2CPP Reflection for Component Inspection
// ==============================

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using GameSDK;
using GameSDK.ModHost;

namespace MDB.Explorer.ImGui
{
    /// <summary>
    /// Provides IL2CPP reflection capabilities for inspecting components.
    /// </summary>
    public static class ComponentReflector
    {
        private const string LOG_TAG = "ComponentReflector";

        // Cache of reflection data per class pointer
        private static Dictionary<IntPtr, ComponentReflectionData> _cache = new Dictionary<IntPtr, ComponentReflectionData>();

        // Types to skip when enumerating (internal Unity types)
        private static HashSet<string> _skipTypes = new HashSet<string>
        {
            "Object", "Component", "Behaviour", "MonoBehaviour", "ScriptableObject"
        };

        /// <summary>
        /// Get reflection data for a component instance.
        /// </summary>
        public static ComponentReflectionData GetReflectionData(IntPtr componentPtr)
        {
            if (componentPtr == IntPtr.Zero) return null;

            try
            {
                // Get the object's class
                IntPtr klass = Il2CppBridge.mdb_object_get_class(componentPtr);
                if (klass == IntPtr.Zero) return null;

                // Check cache
                if (_cache.TryGetValue(klass, out var cached))
                {
                    return cached;
                }

                // Build reflection data
                string className = Il2CppBridge.GetClassName(klass);
                
                var data = new ComponentReflectionData
                {
                    ClassPointer = klass,
                    ClassName = className
                };

                // Enumerate fields, properties, methods (including inherited)
                EnumerateMembers(klass, data, includeBase: true);

                // Cache and return
                _cache[klass] = data;
                return data;
            }
            catch (Exception ex)
            {
                ModLogger.LogInternal(LOG_TAG, $"[ERROR] GetReflectionData failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Clear the reflection cache.
        /// </summary>
        public static void ClearCache()
        {
            _cache.Clear();
        }

        private static void EnumerateMembers(IntPtr klass, ComponentReflectionData data, bool includeBase)
        {
            if (klass == IntPtr.Zero) return;

            string className = Il2CppBridge.GetClassName(klass);
            
            // Stop at Unity base types
            if (_skipTypes.Contains(className)) 
            {
                return;
            }
            
            // Add this class to the hierarchy (ordered from most derived to base)
            if (!data.ClassHierarchy.Contains(className))
            {
                data.ClassHierarchy.Add(className);
            }

            // Enumerate fields
            int fieldCount = Il2CppBridge.mdb_class_get_field_count(klass);
            
            for (int i = 0; i < fieldCount; i++)
            {
                try
                {
                    IntPtr field = Il2CppBridge.mdb_class_get_field_by_index(klass, i);
                    if (field == IntPtr.Zero) continue;

                    string name = Il2CppBridge.GetFieldName(field);
                    if (string.IsNullOrEmpty(name)) continue;

                    // Skip compiler-generated fields
                    if (name.StartsWith("<") || name.Contains("k__BackingField")) continue;

                    IntPtr type = Il2CppBridge.mdb_field_get_type(field);
                    
                    int typeEnum = -1;
                    string typeName = "unknown";
                    bool isValueType = false;
                    
                    if (type != IntPtr.Zero)
                    {
                        try
                        {
                            typeEnum = Il2CppBridge.mdb_type_get_type_enum(type);
                            typeName = Il2CppBridge.GetTypeName(type) ?? "unknown";
                            isValueType = Il2CppBridge.mdb_type_is_valuetype(type);
                        }
                        catch { /* Ignore type info errors */ }
                    }

                    data.Fields.Add(new FieldInfo
                    {
                        Pointer = field,
                        Name = name,
                        Type = type,
                        TypeName = typeName,
                        TypeEnum = typeEnum,
                        IsStatic = Il2CppBridge.mdb_field_is_static(field),
                        IsValueType = isValueType,
                        ParentClassName = className
                    });
                }
                catch { /* Skip problematic fields */ }
            }

            // Enumerate properties
            int propCount = Il2CppBridge.mdb_class_get_property_count(klass);
            
            for (int i = 0; i < propCount; i++)
            {
                try
                {
                    IntPtr prop = Il2CppBridge.mdb_class_get_property_by_index(klass, i);
                    if (prop == IntPtr.Zero) continue;

                    string name = Il2CppBridge.GetPropertyName(prop);
                    if (string.IsNullOrEmpty(name)) continue;

                    IntPtr getter = Il2CppBridge.mdb_property_get_get_method(prop);
                    IntPtr setter = Il2CppBridge.mdb_property_get_set_method(prop);

                    // Get type info from getter's return type
                    IntPtr returnType = IntPtr.Zero;
                    string typeName = "unknown";
                    int typeEnum = -1;
                    
                    if (getter != IntPtr.Zero)
                    {
                        try
                        {
                            returnType = Il2CppBridge.mdb_method_get_return_type(getter);
                            if (returnType != IntPtr.Zero)
                            {
                                typeEnum = Il2CppBridge.mdb_type_get_type_enum(returnType);
                                typeName = Il2CppBridge.GetTypeName(returnType) ?? "unknown";
                            }
                        }
                        catch { /* Fall back to unknown */ }
                    }

                    data.Properties.Add(new PropertyInfo
                    {
                        Pointer = prop,
                        Name = name,
                        GetterMethod = getter,
                        SetterMethod = setter,
                        ReturnType = returnType,
                        TypeName = typeName,
                        TypeEnum = typeEnum,
                        ParentClassName = className
                    });
                }
                catch { /* Skip problematic properties */ }
            }

            // Enumerate methods (only non-special public methods)
            int methodCount = Il2CppBridge.mdb_class_get_method_count(klass);
            
            for (int i = 0; i < methodCount; i++)
            {
                try
                {
                    IntPtr method = Il2CppBridge.mdb_class_get_method_by_index(klass, i);
                    if (method == IntPtr.Zero) continue;

                    // Use the proper IL2CPP API method
                    string name = Il2CppBridge.GetMethodName(method);
                    if (string.IsNullOrEmpty(name)) continue;

                    int flags = Il2CppBridge.mdb_method_get_flags(method);
                    int paramCount = Il2CppBridge.mdb_method_get_param_count(method);

                    // Get actual return type
                    string returnTypeName = "void";
                    IntPtr returnType = IntPtr.Zero;
                    try
                    {
                        returnType = Il2CppBridge.mdb_method_get_return_type(method);
                        if (returnType != IntPtr.Zero)
                        {
                            string rtName = Il2CppBridge.GetTypeName(returnType);
                            if (!string.IsNullOrEmpty(rtName))
                                returnTypeName = rtName;
                        }
                    }
                    catch { /* Keep "void" default */ }
                    
                    // Build parameter signature
                    string paramSig = null;
                    if (paramCount > 0)
                    {
                        try
                        {
                            var paramParts = new List<string>();
                            for (int p = 0; p < paramCount; p++)
                            {
                                IntPtr paramType = Il2CppBridge.mdb_method_get_param_type(method, p);
                                if (paramType != IntPtr.Zero)
                                {
                                    string ptName = Il2CppBridge.GetTypeName(paramType);
                                    if (!string.IsNullOrEmpty(ptName))
                                    {
                                        // Simplify: remove namespace
                                        int dot = ptName.LastIndexOf('.');
                                        paramParts.Add(dot >= 0 ? ptName.Substring(dot + 1) : ptName);
                                        continue;
                                    }
                                }
                                paramParts.Add("?");
                            }
                            paramSig = string.Join(", ", paramParts);
                        }
                        catch { paramSig = $"{paramCount} params"; }
                    }
                    else
                    {
                        paramSig = "";
                    }

                    var methodInfo = new MethodInfo
                    {
                        Pointer = method,
                        Name = name,
                        ParameterCount = paramCount,
                        ReturnType = returnType,
                        ReturnTypeName = returnTypeName,
                        Flags = flags,
                        ParentClassName = className,
                        ParameterSignature = paramSig
                    };

                    // Skip property accessors and event handlers for the method list
                    // They're already shown as properties
                    if (!methodInfo.IsSpecialName)
                    {
                        data.Methods.Add(methodInfo);
                    }
                }
                catch { /* Skip problematic methods */ }
            }

            // Recurse to parent class
            if (includeBase)
            {
                IntPtr parent = Il2CppBridge.mdb_class_get_parent(klass);
                if (parent != IntPtr.Zero)
                {
                    EnumerateMembers(parent, data, includeBase);
                }
            }
        }

        /// <summary>
        /// Read a static field value.
        /// </summary>
        public static object ReadStaticFieldValue(FieldInfo field)
        {
            if (field == null || field.Pointer == IntPtr.Zero)
                return null;

            try
            {
                switch (field.TypeEnum)
                {
                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_BOOLEAN:
                        return ReadStaticPrimitive<bool>(field.Pointer);

                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_I1:
                        return (int)ReadStaticPrimitive<sbyte>(field.Pointer);

                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_U1:
                        return (int)ReadStaticPrimitive<byte>(field.Pointer);

                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_I2:
                        return (int)ReadStaticPrimitive<short>(field.Pointer);

                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_U2:
                        return (int)ReadStaticPrimitive<ushort>(field.Pointer);

                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_I4:
                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_ENUM:
                        return ReadStaticPrimitive<int>(field.Pointer);

                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_U4:
                        return ReadStaticPrimitive<uint>(field.Pointer);

                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_I8:
                        return ReadStaticPrimitive<long>(field.Pointer);

                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_U8:
                        return ReadStaticPrimitive<ulong>(field.Pointer);

                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_R4:
                        return ReadStaticPrimitive<float>(field.Pointer);

                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_R8:
                        return ReadStaticPrimitive<double>(field.Pointer);

                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_STRING:
                        return ReadStaticString(field.Pointer);

                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_CLASS:
                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_OBJECT:
                        return ReadStaticObjectReference(field.Pointer);

                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_SZARRAY:
                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_ARRAY:
                        return ReadStaticArrayInfo(field.Pointer);

                    default:
                        return $"[{DeobfuscationHelper.GetTypeName(field.TypeName)}]";
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogInternal(LOG_TAG, $"[ERROR] ReadStaticFieldValue failed for {field.Name}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Read a field value from a component instance.
        /// </summary>
        public static object ReadFieldValue(IntPtr instance, FieldInfo field)
        {
            if (instance == IntPtr.Zero || field == null || field.Pointer == IntPtr.Zero)
                return null;

            try
            {
                switch (field.TypeEnum)
                {
                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_BOOLEAN:
                        return ReadPrimitive<bool>(instance, field.Pointer);

                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_I1:
                        return ReadPrimitive<sbyte>(instance, field.Pointer);

                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_U1:
                        return ReadPrimitive<byte>(instance, field.Pointer);

                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_I2:
                        return ReadPrimitive<short>(instance, field.Pointer);

                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_U2:
                        return ReadPrimitive<ushort>(instance, field.Pointer);

                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_I4:
                        return ReadPrimitive<int>(instance, field.Pointer);

                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_U4:
                        return ReadPrimitive<uint>(instance, field.Pointer);

                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_I8:
                        return ReadPrimitive<long>(instance, field.Pointer);

                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_U8:
                        return ReadPrimitive<ulong>(instance, field.Pointer);

                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_R4:
                        return ReadPrimitive<float>(instance, field.Pointer);

                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_R8:
                        return ReadPrimitive<double>(instance, field.Pointer);

                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_CHAR:
                        return ReadPrimitive<char>(instance, field.Pointer);

                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_STRING:
                        return ReadString(instance, field.Pointer);

                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_VALUETYPE:
                        // Check if it's a Vector3 or similar (including Unity.Mathematics float2/float3/float4)
                        if (field.TypeName?.Contains("Vector3") == true || field.TypeName?.Contains("float3") == true)
                            return ReadVector3(instance, field.Pointer);
                        if (field.TypeName?.Contains("Vector2") == true || field.TypeName?.Contains("float2") == true)
                            return ReadVector2(instance, field.Pointer);
                        if (field.TypeName?.Contains("Color") == true || field.TypeName?.Contains("float4") == true)
                            return ReadColor(instance, field.Pointer);
                        if (field.TypeName?.Contains("Quaternion") == true)
                            return ReadColor(instance, field.Pointer); // Quaternion is also 4 floats like Color
                        // Use deobfuscated name if available
                        return $"[{DeobfuscationHelper.GetTypeName(field.TypeName)}]";

                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_CLASS:
                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_OBJECT:
                        return ReadObjectReference(instance, field.Pointer);

                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_SZARRAY:
                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_ARRAY:
                        return ReadArrayInfo(instance, field.Pointer);

                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_ENUM:
                        return ReadPrimitive<int>(instance, field.Pointer);

                    default:
                        // Use deobfuscated name if available
                        return $"[{DeobfuscationHelper.GetTypeName(field.TypeName)}]";
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogInternal(LOG_TAG, $"[ERROR] ReadFieldValue failed for {field.Name}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Write a field value to a component instance.
        /// </summary>
        public static bool WriteFieldValue(IntPtr instance, FieldInfo field, object value)
        {
            if (instance == IntPtr.Zero || field == null || field.Pointer == IntPtr.Zero || value == null)
                return false;

            try
            {
                switch (field.TypeEnum)
                {
                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_BOOLEAN:
                        return WritePrimitive(instance, field.Pointer, (bool)value);

                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_I4:
                        return WritePrimitive(instance, field.Pointer, (int)value);

                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_U4:
                        return WritePrimitive(instance, field.Pointer, (uint)value);

                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_R4:
                        return WritePrimitive(instance, field.Pointer, (float)value);

                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_R8:
                        return WritePrimitive(instance, field.Pointer, (double)value);

                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_VALUETYPE:
                        if (field.TypeName?.Contains("Vector3") == true && value is System.Numerics.Vector3 v3)
                            return WriteVector3(instance, field.Pointer, v3);
                        return false;

                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                ModLogger.LogInternal(LOG_TAG, $"[ERROR] WriteFieldValue failed for {field.Name}: {ex.Message}");
                return false;
            }
        }

        // Helper methods for reading primitives
        private static T ReadPrimitive<T>(IntPtr instance, IntPtr field) where T : unmanaged
        {
            unsafe
            {
                T value = default;
                IntPtr buffer = new IntPtr(&value);
                if (Il2CppBridge.mdb_field_get_value_direct(instance, field, buffer, sizeof(T)))
                    return value;
                return default;
            }
        }

        private static bool WritePrimitive<T>(IntPtr instance, IntPtr field, T value) where T : unmanaged
        {
            unsafe
            {
                IntPtr buffer = new IntPtr(&value);
                return Il2CppBridge.mdb_field_set_value_direct(instance, field, buffer, sizeof(T));
            }
        }

        private static string ReadString(IntPtr instance, IntPtr field)
        {
            unsafe
            {
                IntPtr strPtr = IntPtr.Zero;
                IntPtr buffer = new IntPtr(&strPtr);
                if (Il2CppBridge.mdb_field_get_value_direct(instance, field, buffer, IntPtr.Size))
                {
                    if (strPtr == IntPtr.Zero) return "(null)";

                    // Read IL2CPP string using StringBuilder
                    var sb = new System.Text.StringBuilder(256);
                    int len = Il2CppBridge.mdb_string_to_utf8(strPtr, sb, sb.Capacity);
                    if (len > 0)
                        return sb.ToString(0, len);
                    return "(error)";
                }
                return "(null)";
            }
        }

        private static System.Numerics.Vector3 ReadVector3(IntPtr instance, IntPtr field)
        {
            unsafe
            {
                float* values = stackalloc float[3];
                if (Il2CppBridge.mdb_field_get_value_direct(instance, field, new IntPtr(values), 12))
                    return new System.Numerics.Vector3(values[0], values[1], values[2]);
                return System.Numerics.Vector3.Zero;
            }
        }

        private static bool WriteVector3(IntPtr instance, IntPtr field, System.Numerics.Vector3 value)
        {
            unsafe
            {
                float* values = stackalloc float[3];
                values[0] = value.X;
                values[1] = value.Y;
                values[2] = value.Z;
                return Il2CppBridge.mdb_field_set_value_direct(instance, field, new IntPtr(values), 12);
            }
        }

        private static System.Numerics.Vector2 ReadVector2(IntPtr instance, IntPtr field)
        {
            unsafe
            {
                float* values = stackalloc float[2];
                if (Il2CppBridge.mdb_field_get_value_direct(instance, field, new IntPtr(values), 8))
                    return new System.Numerics.Vector2(values[0], values[1]);
                return System.Numerics.Vector2.Zero;
            }
        }

        private static System.Numerics.Vector4 ReadColor(IntPtr instance, IntPtr field)
        {
            unsafe
            {
                float* values = stackalloc float[4];
                if (Il2CppBridge.mdb_field_get_value_direct(instance, field, new IntPtr(values), 16))
                    return new System.Numerics.Vector4(values[0], values[1], values[2], values[3]);
                return System.Numerics.Vector4.Zero;
            }
        }

        private static string ReadObjectReference(IntPtr instance, IntPtr field)
        {
            unsafe
            {
                IntPtr objPtr = IntPtr.Zero;
                IntPtr buffer = new IntPtr(&objPtr);
                if (Il2CppBridge.mdb_field_get_value_direct(instance, field, buffer, IntPtr.Size))
                {
                    if (objPtr == IntPtr.Zero) return "(null)";

                    // Get class name
                    IntPtr klass = Il2CppBridge.mdb_object_get_class(objPtr);
                    if (klass != IntPtr.Zero)
                    {
                        string className = Il2CppBridge.GetClassName(klass);
                        // Use deobfuscated name if available
                        string displayName = DeobfuscationHelper.GetTypeName(className);
                        return $"[{displayName}]";
                    }
                    return "[Object]";
                }
                return "(null)";
            }
        }

        private static string ReadArrayInfo(IntPtr instance, IntPtr field)
        {
            unsafe
            {
                IntPtr arrPtr = IntPtr.Zero;
                IntPtr buffer = new IntPtr(&arrPtr);
                if (Il2CppBridge.mdb_field_get_value_direct(instance, field, buffer, IntPtr.Size))
                {
                    if (arrPtr == IntPtr.Zero) return "(null)";

                    int length = Il2CppBridge.mdb_array_length(arrPtr);
                    return $"[Array: {length} elements]";
                }
                return "(null)";
            }
        }

        /// <summary>
        /// Invoke a property getter and return the properly typed value.
        /// </summary>
        public static object InvokePropertyGetter(IntPtr instance, PropertyInfo property)
        {
            if (instance == IntPtr.Zero || property == null || property.GetterMethod == IntPtr.Zero)
                return null;

            try
            {
                IntPtr exception;
                IntPtr result = Il2CppBridge.mdb_invoke_method(property.GetterMethod, instance, Array.Empty<IntPtr>(), out exception);

                if (exception != IntPtr.Zero)
                    return "(exception)";

                if (result == IntPtr.Zero)
                    return "(null)";

                // Handle based on return type
                switch (property.TypeEnum)
                {
                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_BOOLEAN:
                        unsafe { return *(bool*)((byte*)result + 16); }

                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_I1:
                        unsafe { return (int)*(sbyte*)((byte*)result + 16); }

                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_U1:
                        unsafe { return (int)*(byte*)((byte*)result + 16); }

                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_I2:
                        unsafe { return (int)*(short*)((byte*)result + 16); }

                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_U2:
                        unsafe { return (int)*(ushort*)((byte*)result + 16); }

                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_I4:
                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_ENUM:
                        unsafe { return *(int*)((byte*)result + 16); }

                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_U4:
                        unsafe { return *(uint*)((byte*)result + 16); }

                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_I8:
                        unsafe { return *(long*)((byte*)result + 16); }

                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_U8:
                        unsafe { return *(ulong*)((byte*)result + 16); }

                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_R4:
                        unsafe { return *(float*)((byte*)result + 16); }

                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_R8:
                        unsafe { return *(double*)((byte*)result + 16); }

                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_STRING:
                        {
                            var sb = new System.Text.StringBuilder(256);
                            int len = Il2CppBridge.mdb_string_to_utf8(result, sb, sb.Capacity);
                            if (len > 0)
                                return sb.ToString(0, len);
                            return "(null)";
                        }

                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_VALUETYPE:
                        {
                            // Try to identify the value type and unbox
                            string tn = property.TypeName;
                            if (tn != null)
                            {
                                if (tn.Contains("Vector3") || tn.Contains("float3"))
                                {
                                    unsafe
                                    {
                                        float* p = (float*)((byte*)result + 16);
                                        return new System.Numerics.Vector3(p[0], p[1], p[2]);
                                    }
                                }
                                if (tn.Contains("Vector2") || tn.Contains("float2"))
                                {
                                    unsafe
                                    {
                                        float* p = (float*)((byte*)result + 16);
                                        return new System.Numerics.Vector2(p[0], p[1]);
                                    }
                                }
                                if (tn.Contains("Color") || tn.Contains("Quaternion") || tn.Contains("float4"))
                                {
                                    unsafe
                                    {
                                        float* p = (float*)((byte*)result + 16);
                                        return new System.Numerics.Vector4(p[0], p[1], p[2], p[3]);
                                    }
                                }
                            }
                            // For other value types, try to show as int (common for enums disguised as ValueType)
                            unsafe { return *(int*)((byte*)result + 16); }
                        }

                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_CLASS:
                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_OBJECT:
                        {
                            // For reference types, get the class name of the returned object
                            IntPtr klass = Il2CppBridge.mdb_object_get_class(result);
                            if (klass != IntPtr.Zero)
                            {
                                string cn = Il2CppBridge.GetClassName(klass);
                                string dn = DeobfuscationHelper.GetTypeName(cn);
                                return $"[{dn}]";
                            }
                            return "[Object]";
                        }

                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_SZARRAY:
                    case Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_ARRAY:
                        {
                            int length = Il2CppBridge.mdb_array_length(result);
                            return $"[Array: {length}]";
                        }

                    default:
                        // For unknown types, try to show the object's class name
                        try
                        {
                            IntPtr klass = Il2CppBridge.mdb_object_get_class(result);
                            if (klass != IntPtr.Zero)
                            {
                                string cn = Il2CppBridge.GetClassName(klass);
                                return $"[{DeobfuscationHelper.GetTypeName(cn)}]";
                            }
                        }
                        catch { }
                        return $"0x{result.ToInt64():X}";
                }
            }
            catch (Exception ex)
            {
                return $"(error: {ex.Message})";
            }
        }

        // ===== Static field reading helpers =====

        private static T ReadStaticPrimitive<T>(IntPtr field) where T : unmanaged
        {
            unsafe
            {
                T value = default;
                IntPtr buffer = new IntPtr(&value);
                Il2CppBridge.mdb_field_static_get_value(field, buffer);
                return value;
            }
        }

        private static string ReadStaticString(IntPtr field)
        {
            unsafe
            {
                IntPtr strPtr = IntPtr.Zero;
                IntPtr buffer = new IntPtr(&strPtr);
                Il2CppBridge.mdb_field_static_get_value(field, buffer);
                if (strPtr == IntPtr.Zero) return "(null)";

                var sb = new System.Text.StringBuilder(256);
                int len = Il2CppBridge.mdb_string_to_utf8(strPtr, sb, sb.Capacity);
                if (len > 0) return sb.ToString(0, len);
                return "(error)";
            }
        }

        private static string ReadStaticObjectReference(IntPtr field)
        {
            unsafe
            {
                IntPtr objPtr = IntPtr.Zero;
                IntPtr buffer = new IntPtr(&objPtr);
                Il2CppBridge.mdb_field_static_get_value(field, buffer);
                if (objPtr == IntPtr.Zero) return "(null)";

                IntPtr klass = Il2CppBridge.mdb_object_get_class(objPtr);
                if (klass != IntPtr.Zero)
                {
                    string className = Il2CppBridge.GetClassName(klass);
                    return $"[{DeobfuscationHelper.GetTypeName(className)}]";
                }
                return "[Object]";
            }
        }

        private static string ReadStaticArrayInfo(IntPtr field)
        {
            unsafe
            {
                IntPtr arrPtr = IntPtr.Zero;
                IntPtr buffer = new IntPtr(&arrPtr);
                Il2CppBridge.mdb_field_static_get_value(field, buffer);
                if (arrPtr == IntPtr.Zero) return "(null)";

                int length = Il2CppBridge.mdb_array_length(arrPtr);
                return $"[Array: {length}]";
            }
        }
    }
}
