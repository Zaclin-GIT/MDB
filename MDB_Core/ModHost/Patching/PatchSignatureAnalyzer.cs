// ==============================
// PatchSignatureAnalyzer - Method Signature Analysis
// ==============================
// Analyzes method signatures and parameters for IL2CPP type information

using System;
using System.Linq;

namespace GameSDK.ModHost.Patching
{
    /// <summary>
    /// Handles method signature analysis and parameter type detection.
    /// </summary>
    public static partial class PatchProcessor
    {
        /// <summary>
        /// Build the parameter signature string for a method by querying IL2CPP type info.
        /// Signature uses: P = pointer/int, F = float, D = double
        /// For instance methods, first character is always 'P' for 'this' pointer.
        /// </summary>
        private static void BuildParameterSignature(PatchInfo patchInfo, IntPtr method)
        {
            var sig = new System.Text.StringBuilder();
            
            // Check if there's an __instance parameter to determine if it's an instance method
            bool hasInstance = false;
            if (patchInfo.PrefixMethod != null)
                hasInstance = patchInfo.PrefixMethod.GetParameters().Any(p => p.Name == "__instance");
            else if (patchInfo.PostfixMethod != null)
                hasInstance = patchInfo.PostfixMethod.GetParameters().Any(p => p.Name == "__instance");
            
            // Instance methods have 'this' pointer as first arg
            if (hasInstance)
            {
                sig.Append('P');
            }
            
            // Query each parameter's type
            int paramCount = patchInfo.ParameterCount >= 0 ? patchInfo.ParameterCount : 0;
            
            for (int i = 0; i < paramCount; i++)
            {
                IntPtr paramType = Il2CppBridge.mdb_method_get_param_type(method, i);
                if (paramType == IntPtr.Zero)
                {
                    sig.Append('P'); // Default to pointer if we can't get type
                    continue;
                }
                
                int typeEnum = Il2CppBridge.mdb_type_get_type_enum(paramType);
                
                switch (typeEnum)
                {
                    case Il2CppBridge.IL2CPP_TYPE_R4: // float
                        sig.Append('F');
                        break;
                    case Il2CppBridge.IL2CPP_TYPE_R8: // double
                        sig.Append('D');
                        break;
                    default:
                        sig.Append('P'); // int, pointer, object, etc.
                        break;
                }
            }
            
            patchInfo.ParameterSignature = sig.ToString();
            
            // Check return type and store the type enum
            IntPtr returnType = Il2CppBridge.mdb_method_get_return_type(method);
            if (returnType != IntPtr.Zero)
            {
                int returnTypeEnum = Il2CppBridge.mdb_type_get_type_enum(returnType);
                patchInfo.ReturnTypeEnum = returnTypeEnum;
                patchInfo.ReturnsFloat = (returnTypeEnum == Il2CppBridge.IL2CPP_TYPE_R4 || 
                                          returnTypeEnum == Il2CppBridge.IL2CPP_TYPE_R8);
            }
            else
            {
                // Default to void if we can't determine the return type
                patchInfo.ReturnTypeEnum = Il2CppBridge.IL2CPP_TYPE_VOID;
            }
        }

        /// <summary>
        /// Convert an IL2CPP pointer to the expected managed type.
        /// </summary>
        private static object ConvertToType(IntPtr ptr, Type targetType)
        {
            if (targetType == typeof(IntPtr))
                return ptr;
            
            if (ptr == IntPtr.Zero)
                return GetDefault(targetType);

            // Primitive types - IL2CPP passes small values directly in the pointer
            if (targetType == typeof(int))
                return ptr.ToInt32();
            if (targetType == typeof(long))
                return ptr.ToInt64();
            if (targetType == typeof(bool))
                return ptr != IntPtr.Zero && ptr.ToInt32() != 0;
            if (targetType == typeof(float))
            {
                // Float is passed as IntPtr containing the bits
                int bits = ptr.ToInt32();
                return BitConverter.ToSingle(BitConverter.GetBytes(bits), 0);
            }
            if (targetType == typeof(double))
            {
                long bits = ptr.ToInt64();
                return BitConverter.ToDouble(BitConverter.GetBytes(bits), 0);
            }

            // String - convert from IL2CPP string
            if (targetType == typeof(string))
            {
                return Il2CppStringHelper.ObjectToString(ptr);
            }

            // Il2CppObject or derived type - wrap the pointer
            if (typeof(Il2CppObject).IsAssignableFrom(targetType))
            {
                return Activator.CreateInstance(targetType, ptr);
            }

            // Fallback - return as IntPtr
            return ptr;
        }

        /// <summary>
        /// Convert a managed value back to IntPtr for IL2CPP.
        /// Used when writing back ref parameter values.
        /// </summary>
        private static IntPtr ConvertToIntPtr(object value)
        {
            if (value == null)
                return IntPtr.Zero;
            
            if (value is IntPtr ptr)
                return ptr;
            
            if (value is int i)
                return new IntPtr(i);
            
            if (value is long l)
                return new IntPtr(l);
            
            if (value is bool b)
                return new IntPtr(b ? 1 : 0);
            
            if (value is float f)
            {
                byte[] bytes = BitConverter.GetBytes(f);
                int bits = BitConverter.ToInt32(bytes, 0);
                return new IntPtr(bits);
            }
            
            if (value is double d)
            {
                byte[] bytes = BitConverter.GetBytes(d);
                long bits = BitConverter.ToInt64(bytes, 0);
                return new IntPtr(bits);
            }
            
            if (value is string s)
            {
                return Il2CppBridge.ManagedStringToIl2Cpp(s);
            }
            
            if (value is Il2CppObject obj)
            {
                return obj.NativePtr;
            }
            
            // Fallback - try to get IntPtr from the value
            return IntPtr.Zero;
        }

        /// <summary>
        /// Get the default value for a type.
        /// </summary>
        private static object GetDefault(Type type)
        {
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }
    }
}
