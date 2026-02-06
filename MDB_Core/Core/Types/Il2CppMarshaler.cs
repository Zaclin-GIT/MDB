// ==============================
// Il2CppMarshaler - Type Marshaling Utilities
// ==============================
// Handles conversion between managed C# types and IL2CPP native types

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace GameSDK
{
    /// <summary>
    /// Provides marshaling utilities for converting between managed and IL2CPP types.
    /// </summary>
    public static class Il2CppMarshaler
    {
        /// <summary>
        /// Marshal an array of managed arguments to native IL2CPP pointers.
        /// </summary>
        /// <param name="args">Managed arguments</param>
        /// <returns>Array of native pointers</returns>
        public static IntPtr[] MarshalArguments(object[] args)
        {
            if (args == null || args.Length == 0)
            {
                return Array.Empty<IntPtr>();
            }

            IntPtr[] nativeArgs = new IntPtr[args.Length];

            for (int i = 0; i < args.Length; i++)
            {
                nativeArgs[i] = MarshalToNative(args[i]);
            }

            return nativeArgs;
        }

        /// <summary>
        /// Marshal a single managed object to a native IL2CPP pointer.
        /// </summary>
        public static IntPtr MarshalToNative(object value)
        {
            if (value == null)
            {
                return IntPtr.Zero;
            }

            Type type = value.GetType();

            // IL2CPP objects - return their native pointer
            if (value is Il2CppObject il2cppObj)
            {
                return il2cppObj.NativePtr;
            }

            // Already a pointer
            if (value is IntPtr ptr)
            {
                return ptr;
            }

            // Strings - create IL2CPP string
            if (value is string str)
            {
                return Il2CppBridge.mdb_string_new(str);
            }

            // System.Type - convert to IL2CPP Type object
            if (value is Type managedType)
            {
                return MarshalType(managedType);
            }

            // Primitive types - need to box them for IL2CPP
            // For now, we allocate unmanaged memory and copy the value
            if (type.IsPrimitive)
            {
                return MarshalPrimitive(value, type);
            }

            // Enums - convert to underlying integer value
            if (type.IsEnum)
            {
                // Get the underlying value and marshal it as a primitive
                object underlyingValue = Convert.ChangeType(value, Enum.GetUnderlyingType(type));
                return MarshalPrimitive(underlyingValue, Enum.GetUnderlyingType(type));
            }

            // Value types (structs like Vector3, etc.)
            if (type.IsValueType)
            {
                return MarshalValueType(value);
            }

            // Unsupported type - return zero
            System.Diagnostics.Debug.WriteLine($"[Il2CppMarshaler] WARNING: Unsupported type for marshaling: {type.Name}");
            return IntPtr.Zero;
        }

        /// <summary>
        /// Marshal a primitive value to unmanaged memory.
        /// </summary>
        private static IntPtr MarshalPrimitive(object value, Type type)
        {
            int size = Marshal.SizeOf(type);
            IntPtr ptr = Marshal.AllocHGlobal(size);

            if (type == typeof(bool))
                Marshal.WriteByte(ptr, (byte)((bool)value ? 1 : 0));
            else if (type == typeof(byte))
                Marshal.WriteByte(ptr, (byte)value);
            else if (type == typeof(sbyte))
                Marshal.WriteByte(ptr, (byte)(sbyte)value);
            else if (type == typeof(short))
                Marshal.WriteInt16(ptr, (short)value);
            else if (type == typeof(ushort))
                Marshal.WriteInt16(ptr, (short)(ushort)value);
            else if (type == typeof(int))
                Marshal.WriteInt32(ptr, (int)value);
            else if (type == typeof(uint))
                Marshal.WriteInt32(ptr, (int)(uint)value);
            else if (type == typeof(long))
                Marshal.WriteInt64(ptr, (long)value);
            else if (type == typeof(ulong))
                Marshal.WriteInt64(ptr, (long)(ulong)value);
            else if (type == typeof(float))
            {
                float f = (float)value;
                Marshal.WriteInt32(ptr, BitConverter.ToInt32(BitConverter.GetBytes(f), 0));
            }
            else if (type == typeof(double))
            {
                double d = (double)value;
                Marshal.WriteInt64(ptr, BitConverter.ToInt64(BitConverter.GetBytes(d), 0));
            }
            else if (type == typeof(char))
                Marshal.WriteInt16(ptr, (short)(char)value);
            else
            {
                Marshal.FreeHGlobal(ptr);
                return IntPtr.Zero;
            }

            return ptr;
        }

        /// <summary>
        /// Marshal a value type (struct) to unmanaged memory.
        /// </summary>
        private static IntPtr MarshalValueType(object value)
        {
            int size = Marshal.SizeOf(value);
            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(value, ptr, false);
            return ptr;
        }

        /// <summary>
        /// Marshal a System.Type to an IL2CPP Type object.
        /// Maps managed type info to IL2CPP's type system.
        /// </summary>
        public static IntPtr MarshalType(Type managedType)
        {
            // Get the IL2CPP class for this type
            string ns = managedType.Namespace ?? "";
            string name = managedType.Name;
            
            // Find the class in IL2CPP - signature is (assembly, ns, name)
            IntPtr klass = Il2CppBridge.mdb_find_class("", ns, name);
            if (klass == IntPtr.Zero)
            {
                // Try common assemblies
                klass = Il2CppBridge.mdb_find_class("UnityEngine.CoreModule", ns, name);
                if (klass == IntPtr.Zero)
                {
                    klass = Il2CppBridge.mdb_find_class("Assembly-CSharp", ns, name);
                }
            }
            
            if (klass == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }
            
            // Get the Il2CppType* from the class
            IntPtr il2cppType = Il2CppBridge.mdb_class_get_type(klass);
            if (il2cppType == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }
            
            // Get the System.Type object from the Il2CppType*
            IntPtr typeObject = Il2CppBridge.mdb_type_get_object(il2cppType);
            return typeObject;
        }

        // IL2CPP object header size (klass pointer + monitor = 16 bytes on 64-bit)
        private const int IL2CPP_OBJECT_HEADER_SIZE = 16;
        
        /// <summary>
        /// Marshal a native IL2CPP return value to a managed type.
        /// 
        /// IMPORTANT: il2cpp_runtime_invoke returns BOXED values for primitives and value types.
        /// A boxed value is an Il2CppObject with:
        /// - 8 bytes: klass pointer
        /// - 8 bytes: monitor (GC sync block)
        /// - N bytes: actual data
        /// 
        /// So we need to skip the 16-byte header to read the actual value.
        /// </summary>
        public static T MarshalReturn<T>(IntPtr nativeValue)
        {
            Type type = typeof(T);

            // Null/void
            if (nativeValue == IntPtr.Zero)
            {
                return default(T);
            }

            // String - already an Il2CppString object, handle directly
            if (type == typeof(string))
            {
                string result = Il2CppBridge.Il2CppStringToManaged(nativeValue);
                return (T)(object)result;
            }

            // Arrays - need special handling for IL2CPP arrays
            if (type.IsArray)
            {
                return MarshalArrayReturn<T>(nativeValue, type);
            }

            // IL2CPP object types - wrap in managed object
            if (typeof(Il2CppObject).IsAssignableFrom(type))
            {
                // Create instance using constructor that takes IntPtr
                try
                {
                    var instance = (T)Activator.CreateInstance(type, nativeValue);
                    return instance;
                }
                catch
                {
                    return default(T);
                }
            }

            // IntPtr - return directly
            if (type == typeof(IntPtr))
            {
                return (T)(object)nativeValue;
            }

            // Enums - boxed like primitives, read as underlying integer type
            if (type.IsEnum)
            {
                IntPtr dataPtr = nativeValue + IL2CPP_OBJECT_HEADER_SIZE;
                Type underlyingType = Enum.GetUnderlyingType(type);
                int size = Marshal.SizeOf(underlyingType);

                long rawValue;
                if (size == 1) rawValue = Marshal.ReadByte(dataPtr);
                else if (size == 2) rawValue = Marshal.ReadInt16(dataPtr);
                else if (size == 4) rawValue = Marshal.ReadInt32(dataPtr);
                else rawValue = Marshal.ReadInt64(dataPtr);

                return (T)Enum.ToObject(type, rawValue);
            }

            // Primitives - the return value is a BOXED primitive, skip object header
            if (type.IsPrimitive)
            {
                // Skip the 16-byte IL2CPP object header to get to the actual data
                IntPtr dataPtr = nativeValue + IL2CPP_OBJECT_HEADER_SIZE;
                return MarshalPrimitiveReturn<T>(dataPtr, type);
            }

            // Value types (structs) - also boxed, skip object header
            if (type.IsValueType)
            {
                IntPtr dataPtr = nativeValue + IL2CPP_OBJECT_HEADER_SIZE;
                return (T)Marshal.PtrToStructure(dataPtr, type);
            }

            return default(T);
        }

        /// <summary>
        /// Marshal a primitive return value from a pointer to the actual data.
        /// Note: The caller is responsible for skipping any object header if needed.
        /// </summary>
        private static T MarshalPrimitiveReturn<T>(IntPtr ptr, Type type)
        {
            object result;

            if (type == typeof(bool))
            {
                byte b = Marshal.ReadByte(ptr);
                result = b != 0;
            }
            else if (type == typeof(byte))
            {
                result = Marshal.ReadByte(ptr);
            }
            else if (type == typeof(sbyte))
            {
                result = (sbyte)Marshal.ReadByte(ptr);
            }
            else if (type == typeof(short))
            {
                result = Marshal.ReadInt16(ptr);
            }
            else if (type == typeof(ushort))
            {
                result = (ushort)Marshal.ReadInt16(ptr);
            }
            else if (type == typeof(int))
            {
                result = Marshal.ReadInt32(ptr);
            }
            else if (type == typeof(uint))
            {
                result = (uint)Marshal.ReadInt32(ptr);
            }
            else if (type == typeof(long))
            {
                result = Marshal.ReadInt64(ptr);
            }
            else if (type == typeof(ulong))
            {
                result = (ulong)Marshal.ReadInt64(ptr);
            }
            else if (type == typeof(float))
            {
                int bits = Marshal.ReadInt32(ptr);
                result = BitConverter.ToSingle(BitConverter.GetBytes(bits), 0);
            }
            else if (type == typeof(double))
            {
                long bits = Marshal.ReadInt64(ptr);
                result = BitConverter.ToDouble(BitConverter.GetBytes(bits), 0);
            }
            else if (type == typeof(char))
            {
                result = (char)Marshal.ReadInt16(ptr);
            }
            else
            {
                result = default(T);
            }

            return (T)result;
        }

        /// <summary>
        /// Marshal an IL2CPP array to a managed array.
        /// </summary>
        private static T MarshalArrayReturn<T>(IntPtr il2cppArray, Type arrayType)
        {
            Type elementType = arrayType.GetElementType();
            
            // Get array length from IL2CPP array
            // IL2CPP arrays have their length at offset 24 (after object header + bounds pointer)
            // Structure: Il2CppObject (16 bytes) + Il2CppArrayBounds* (8 bytes) + max_length (8 bytes)
            int length = 0;
            try
            {
                // Try to read length - it's typically at offset 24 for 64-bit
                length = Marshal.ReadInt32(il2cppArray + 24);
            }
            catch
            {
                return default(T);
            }
            
            if (length < 0 || length > 100000)
            {
                return (T)(object)Array.CreateInstance(elementType, 0);
            }
            
            // Create managed array
            Array result = Array.CreateInstance(elementType, length);
            
            // IL2CPP array data starts at offset 32 (after the header)
            IntPtr dataStart = il2cppArray + 32;
            
            // For object arrays (like Object[]), each element is an 8-byte pointer
            if (!elementType.IsValueType || typeof(Il2CppObject).IsAssignableFrom(elementType))
            {
                for (int i = 0; i < length; i++)
                {
                    IntPtr elementPtr = Marshal.ReadIntPtr(dataStart + i * IntPtr.Size);
                    if (elementPtr != IntPtr.Zero)
                    {
                        // Try to create wrapper object
                        try
                        {
                            object element = Activator.CreateInstance(elementType, elementPtr);
                            result.SetValue(element, i);
                        }
                        catch
                        {
                            // Could not create wrapper, leave as null
                        }
                    }
                }
            }
            else
            {
                // Value type array - copy data directly
                int elementSize = Marshal.SizeOf(elementType);
                for (int i = 0; i < length; i++)
                {
                    IntPtr elementPtr = dataStart + i * elementSize;
                    object element = Marshal.PtrToStructure(elementPtr, elementType);
                    result.SetValue(element, i);
                }
            }
            
            return (T)(object)result;
        }

        /// <summary>
        /// Free native memory allocated by marshaling operations.
        /// Note: This should be called to free memory from MarshalPrimitive/MarshalValueType.
        /// IL2CPP strings and objects are managed by IL2CPP's GC.
        /// </summary>
        public static void FreeNativeMemory(IntPtr ptr)
        {
            if (ptr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
    }
}
