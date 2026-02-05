// ==============================
// Il2CppFieldAccessor - Field Access Methods
// ==============================
// Handles reading and writing IL2CPP object fields

using System;
using System.Runtime.InteropServices;

namespace GameSDK
{
    /// <summary>
    /// Field access functionality for IL2CPP runtime.
    /// Supports reading and writing fields with automatic type marshaling.
    /// </summary>
    public static partial class Il2CppRuntime
    {
        // ==============================
        // Field Access Methods
        // ==============================

        /// <summary>
        /// Get an instance field value by field name.
        /// </summary>
        /// <typeparam name="T">The type of the field value</typeparam>
        /// <param name="instance">The object instance</param>
        /// <param name="fieldName">The IL2CPP field name</param>
        /// <returns>The field value, or default(T) on failure</returns>
        public static T GetField<T>(object instance, string fieldName)
        {
            EnsureInitialized();

            try
            {
                IntPtr nativeInstance = GetNativePointer(instance);
                if (nativeInstance == IntPtr.Zero)
                {
                    LogError($"Instance is null for field {fieldName}");
                    return default(T);
                }

                IntPtr klass = Il2CppBridge.mdb_object_get_class(nativeInstance);
                if (klass == IntPtr.Zero)
                {
                    LogError($"Could not get class for instance when accessing field {fieldName}");
                    return default(T);
                }

                IntPtr field = Il2CppBridge.mdb_get_field(klass, fieldName);
                if (field == IntPtr.Zero)
                {
                    LogError($"Field not found: {fieldName}");
                    return default(T);
                }

                int offset = Il2CppBridge.mdb_get_field_offset(field);
                if (offset < 0)
                {
                    LogError($"Invalid field offset for {fieldName}");
                    return default(T);
                }

                // Read the field value at instance + offset
                return ReadFieldValue<T>(nativeInstance, offset);
            }
            catch (Exception ex)
            {
                LogError($"GetField<{typeof(T).Name}>({fieldName}): {ex.Message}");
                return default(T);
            }
        }

        /// <summary>
        /// Set an instance field value by field name.
        /// </summary>
        /// <typeparam name="T">The type of the field value</typeparam>
        /// <param name="instance">The object instance</param>
        /// <param name="fieldName">The IL2CPP field name</param>
        /// <param name="value">The value to set</param>
        public static void SetField<T>(object instance, string fieldName, T value)
        {
            EnsureInitialized();

            try
            {
                IntPtr nativeInstance = GetNativePointer(instance);
                if (nativeInstance == IntPtr.Zero)
                {
                    LogError($"Instance is null for field {fieldName}");
                    return;
                }

                IntPtr klass = Il2CppBridge.mdb_object_get_class(nativeInstance);
                if (klass == IntPtr.Zero)
                {
                    LogError($"Could not get class for instance when accessing field {fieldName}");
                    return;
                }

                IntPtr field = Il2CppBridge.mdb_get_field(klass, fieldName);
                if (field == IntPtr.Zero)
                {
                    LogError($"Field not found: {fieldName}");
                    return;
                }

                int offset = Il2CppBridge.mdb_get_field_offset(field);
                if (offset < 0)
                {
                    LogError($"Invalid field offset for {fieldName}");
                    return;
                }

                // Write the field value at instance + offset
                WriteFieldValue<T>(nativeInstance, offset, value);
            }
            catch (Exception ex)
            {
                LogError($"SetField<{typeof(T).Name}>({fieldName}): {ex.Message}");
            }
        }

        /// <summary>
        /// Get a field value with runtime type checking.
        /// Returns null if the field value is not of the expected type.
        /// </summary>
        /// <typeparam name="T">The expected wrapper type (must inherit from Il2CppObject)</typeparam>
        /// <param name="instance">The object instance</param>
        /// <param name="fieldName">The IL2CPP field name</param>
        /// <returns>The field value if it's of type T, otherwise null</returns>
        public static T GetFieldTyped<T>(object instance, string fieldName) where T : Il2CppObject
        {
            EnsureInitialized();

            try
            {
                IntPtr nativeInstance = GetNativePointer(instance);
                if (nativeInstance == IntPtr.Zero)
                    return null;

                IntPtr klass = Il2CppBridge.mdb_object_get_class(nativeInstance);
                if (klass == IntPtr.Zero)
                    return null;

                IntPtr field = Il2CppBridge.mdb_get_field(klass, fieldName);
                if (field == IntPtr.Zero)
                    return null;

                int offset = Il2CppBridge.mdb_get_field_offset(field);
                if (offset < 0)
                    return null;

                // Read the pointer
                IntPtr objPtr = Marshal.ReadIntPtr(nativeInstance, offset);
                if (objPtr == IntPtr.Zero)
                    return null;

                // Validate the runtime type
                IntPtr objClass = Il2CppBridge.mdb_object_get_class(objPtr);
                string actualClassName = Il2CppBridge.GetClassName(objClass);

                // Get expected class name from wrapper type
                Type wrapperType = typeof(T);
                var classNameField = wrapperType.GetField("_il2cppClassName",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Static);

                if (classNameField != null)
                {
                    string expectedClassName = (string)classNameField.GetValue(null);
                    if (actualClassName != expectedClassName)
                    {
                        // Type mismatch - not the expected type
                        LogDebug($"GetFieldTyped: Expected {expectedClassName}, got {actualClassName}");
                        return null;
                    }
                }

                // Create wrapper instance
                return (T)Activator.CreateInstance(typeof(T), objPtr);
            }
            catch (Exception ex)
            {
                LogError($"GetFieldTyped<{typeof(T).Name}>({fieldName}): {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Try to get a field value with runtime type checking.
        /// </summary>
        /// <typeparam name="T">The expected wrapper type (must inherit from Il2CppObject)</typeparam>
        /// <param name="instance">The object instance</param>
        /// <param name="fieldName">The IL2CPP field name</param>
        /// <param name="value">The output value if successful</param>
        /// <returns>True if the field exists and is of type T</returns>
        public static bool TryGetFieldTyped<T>(object instance, string fieldName, out T value) where T : Il2CppObject
        {
            value = GetFieldTyped<T>(instance, fieldName);
            return value != null;
        }

        // ==============================
        // Field Marshaling Helpers
        // ==============================

        /// <summary>
        /// Read a field value at a given offset from an instance pointer.
        /// Handles primitive types, strings, reference types, and blittable structs.
        /// </summary>
        /// <typeparam name="T">The field type</typeparam>
        /// <param name="instance">The instance pointer</param>
        /// <param name="offset">The field offset in bytes</param>
        /// <returns>The field value</returns>
        private static T ReadFieldValue<T>(IntPtr instance, int offset)
        {
            Type t = typeof(T);

            // Handle string specially - it's an IL2CPP string pointer
            if (t == typeof(string))
            {
                IntPtr strPtr = Marshal.ReadIntPtr(instance, offset);
                if (strPtr == IntPtr.Zero) return default(T);
                return (T)(object)Il2CppBridge.Il2CppStringToManaged(strPtr);
            }

            // Handle other reference types (IL2CPP objects)
            if (!t.IsValueType)
            {
                IntPtr objPtr = Marshal.ReadIntPtr(instance, offset);
                if (objPtr == IntPtr.Zero) return default(T);
                
                // Create wrapper instance
                if (typeof(Il2CppObject).IsAssignableFrom(t))
                {
                    return (T)Activator.CreateInstance(t, objPtr);
                }
                
                return default(T);
            }

            // Handle primitive value types
            if (t == typeof(int)) return (T)(object)Marshal.ReadInt32(instance, offset);
            if (t == typeof(uint)) return (T)(object)(uint)Marshal.ReadInt32(instance, offset);
            if (t == typeof(long)) return (T)(object)Marshal.ReadInt64(instance, offset);
            if (t == typeof(ulong)) return (T)(object)(ulong)Marshal.ReadInt64(instance, offset);
            if (t == typeof(short)) return (T)(object)Marshal.ReadInt16(instance, offset);
            if (t == typeof(ushort)) return (T)(object)(ushort)Marshal.ReadInt16(instance, offset);
            if (t == typeof(byte)) return (T)(object)Marshal.ReadByte(instance, offset);
            if (t == typeof(sbyte)) return (T)(object)(sbyte)Marshal.ReadByte(instance, offset);
            if (t == typeof(bool)) return (T)(object)(Marshal.ReadByte(instance, offset) != 0);
            if (t == typeof(float))
            {
                int bits = Marshal.ReadInt32(instance, offset);
                return (T)(object)BitConverter.ToSingle(BitConverter.GetBytes(bits), 0);
            }
            if (t == typeof(double))
            {
                long bits = Marshal.ReadInt64(instance, offset);
                return (T)(object)BitConverter.ToDouble(BitConverter.GetBytes(bits), 0);
            }
            if (t == typeof(IntPtr)) return (T)(object)Marshal.ReadIntPtr(instance, offset);

            // Handle blittable structs (like Vector2, Vector3, Color, etc.)
            if (t.IsValueType && !t.IsEnum && !t.IsPrimitive)
            {
                IntPtr fieldPtr = IntPtr.Add(instance, offset);
                return (T)Marshal.PtrToStructure(fieldPtr, t);
            }

            LogError($"Unsupported field type: {t.Name}");
            return default(T);
        }

        /// <summary>
        /// Write a field value at a given offset on an instance pointer.
        /// Handles primitive types, strings, reference types, and blittable structs.
        /// </summary>
        /// <typeparam name="T">The field type</typeparam>
        /// <param name="instance">The instance pointer</param>
        /// <param name="offset">The field offset in bytes</param>
        /// <param name="value">The value to write</param>
        private static void WriteFieldValue<T>(IntPtr instance, int offset, T value)
        {
            Type t = typeof(T);

            // Handle string specially - need to create IL2CPP string
            if (t == typeof(string))
            {
                string str = value as string;
                IntPtr strPtr = str != null ? Il2CppBridge.ManagedStringToIl2Cpp(str) : IntPtr.Zero;
                Marshal.WriteIntPtr(instance, offset, strPtr);
                return;
            }

            // Handle other reference types (IL2CPP objects)
            if (!t.IsValueType)
            {
                IntPtr objPtr = GetNativePointer(value);
                Marshal.WriteIntPtr(instance, offset, objPtr);
                return;
            }

            // Handle primitive value types
            if (t == typeof(int)) { Marshal.WriteInt32(instance, offset, (int)(object)value); return; }
            if (t == typeof(uint)) { Marshal.WriteInt32(instance, offset, (int)(uint)(object)value); return; }
            if (t == typeof(long)) { Marshal.WriteInt64(instance, offset, (long)(object)value); return; }
            if (t == typeof(ulong)) { Marshal.WriteInt64(instance, offset, (long)(ulong)(object)value); return; }
            if (t == typeof(short)) { Marshal.WriteInt16(instance, offset, (short)(object)value); return; }
            if (t == typeof(ushort)) { Marshal.WriteInt16(instance, offset, (short)(ushort)(object)value); return; }
            if (t == typeof(byte)) { Marshal.WriteByte(instance, offset, (byte)(object)value); return; }
            if (t == typeof(sbyte)) { Marshal.WriteByte(instance, offset, (byte)(sbyte)(object)value); return; }
            if (t == typeof(bool)) { Marshal.WriteByte(instance, offset, (bool)(object)value ? (byte)1 : (byte)0); return; }
            if (t == typeof(float))
            {
                byte[] bytes = BitConverter.GetBytes((float)(object)value);
                Marshal.WriteInt32(instance, offset, BitConverter.ToInt32(bytes, 0));
                return;
            }
            if (t == typeof(double))
            {
                byte[] bytes = BitConverter.GetBytes((double)(object)value);
                Marshal.WriteInt64(instance, offset, BitConverter.ToInt64(bytes, 0));
                return;
            }
            if (t == typeof(IntPtr)) { Marshal.WriteIntPtr(instance, offset, (IntPtr)(object)value); return; }

            // Handle blittable structs (like Vector2, Vector3, Color, etc.)
            if (t.IsValueType && !t.IsEnum && !t.IsPrimitive)
            {
                IntPtr fieldPtr = IntPtr.Add(instance, offset);
                Marshal.StructureToPtr(value, fieldPtr, false);
                return;
            }

            LogError($"Unsupported field type for writing: {t.Name}");
        }
    }
}
