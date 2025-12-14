using System;
using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;
using System.Text;
using GameSDK.ModHost;

namespace GameSDK
{
    /// <summary>
    /// Helper class for IL2CPP string operations.
    /// Provides multiple fallback methods for extracting strings from IL2CPP objects.
    /// </summary>
    public static class Il2CppStringHelper
    {
        private static bool _debugLogging = false;  // Disabled by default - enable for debugging
        
        private static void DebugLog(string message)
        {
            if (_debugLogging)
            {
                ModLogger.LogInternal("StringHelper", message);
            }
        }
        
        /// <summary>
        /// Enable or disable debug logging for string operations.
        /// </summary>
        public static bool DebugLogging
        {
            get => _debugLogging;
            set => _debugLogging = value;
        }
        
        /// <summary>
        /// Convert an IL2CPP string pointer to a managed string.
        /// Uses multiple fallback methods to handle edge cases.
        /// </summary>
        /// <param name="il2cppStringPtr">Pointer to an IL2CPP String object</param>
        /// <returns>Managed string, or null if conversion fails</returns>
        public static string ToString(IntPtr il2cppStringPtr)
        {
            if (il2cppStringPtr == IntPtr.Zero)
            {
                DebugLog("ToString: Received null pointer");
                return null;
            }

            DebugLog($"ToString: Processing ptr 0x{il2cppStringPtr.ToInt64():X}");

            // Method 1: Use the bridge's string conversion
            try
            {
                DebugLog("ToString: Trying bridge conversion...");
                string result = Il2CppBridge.Il2CppStringToManaged(il2cppStringPtr);
                if (!string.IsNullOrEmpty(result))
                {
                    DebugLog($"ToString: Bridge succeeded, length={result.Length}");
                    return result;
                }
                DebugLog("ToString: Bridge returned null/empty");
            }
            catch (Exception ex)
            {
                DebugLog($"ToString: Bridge threw {ex.GetType().Name}: {ex.Message}");
            }

            // Method 2: Direct memory read
            try
            {
                DebugLog("ToString: Trying direct memory read...");
                string result = ReadStringDirect(il2cppStringPtr);
                if (!string.IsNullOrEmpty(result))
                {
                    DebugLog($"ToString: Direct read succeeded, length={result.Length}");
                    return result;
                }
                DebugLog("ToString: Direct read returned null/empty");
            }
            catch (Exception ex)
            {
                DebugLog($"ToString: Direct read threw {ex.GetType().Name}: {ex.Message}");
            }

            DebugLog("ToString: All methods failed");
            return null;
        }

        /// <summary>
        /// Convert any IL2CPP object to a string representation.
        /// For String objects, extracts the string content.
        /// For other objects, calls ToString() or returns type info.
        /// This method is designed to NEVER crash, even with invalid pointers.
        /// </summary>
        /// <param name="objectPtr">Pointer to any IL2CPP object</param>
        /// <returns>String representation of the object</returns>
        [HandleProcessCorruptedStateExceptions]
        public static string ObjectToString(IntPtr objectPtr)
        {
            DebugLog($"ObjectToString: Called with ptr 0x{(objectPtr == IntPtr.Zero ? 0 : objectPtr.ToInt64()):X}");
            
            if (objectPtr == IntPtr.Zero)
            {
                DebugLog("ObjectToString: Null pointer, returning <null>");
                return "<null>";
            }

            // First, try direct string extraction using memory read (safest approach)
            try
            {
                DebugLog("ObjectToString: Trying ReadStringDirect...");
                string directString = ReadStringDirect(objectPtr);
                if (!string.IsNullOrEmpty(directString))
                {
                    DebugLog($"ObjectToString: ReadStringDirect succeeded: \"{directString.Substring(0, Math.Min(50, directString.Length))}...\"");
                    return directString;
                }
                DebugLog("ObjectToString: ReadStringDirect returned null/empty");
            }
            catch (Exception ex)
            {
                DebugLog($"ObjectToString: ReadStringDirect threw {ex.GetType().Name}: {ex.Message}");
            }

            // Try the bridge's string conversion
            try
            {
                DebugLog("ObjectToString: Trying bridge Il2CppStringToManaged...");
                string bridgeString = Il2CppBridge.Il2CppStringToManaged(objectPtr);
                if (!string.IsNullOrEmpty(bridgeString))
                {
                    DebugLog($"ObjectToString: Bridge succeeded: \"{bridgeString.Substring(0, Math.Min(50, bridgeString.Length))}...\"");
                    return bridgeString;
                }
                DebugLog("ObjectToString: Bridge returned null/empty");
            }
            catch (Exception ex)
            {
                DebugLog($"ObjectToString: Bridge threw {ex.GetType().Name}: {ex.Message}");
            }

            // If we couldn't read it as a string, just return the pointer
            // Don't try to get class info as mdb_object_get_class can crash
            DebugLog($"ObjectToString: All methods failed, returning pointer address");
            return $"<object @ 0x{objectPtr.ToInt64():X}>";
        }

        /// <summary>
        /// Invoke ToString() on an IL2CPP object.
        /// </summary>
        private static string InvokeToString(IntPtr objectPtr, IntPtr classPtr)
        {
            IntPtr toStringMethod = Il2CppBridge.mdb_get_method(classPtr, "ToString", 0);
            if (toStringMethod == IntPtr.Zero)
                return null;

            IntPtr exception;
            IntPtr resultPtr = Il2CppBridge.mdb_invoke_method(toStringMethod, objectPtr, null, out exception);
            
            if (exception != IntPtr.Zero || resultPtr == IntPtr.Zero)
                return null;

            return ToString(resultPtr);
        }

        /// <summary>
        /// Directly read an IL2CPP string from memory.
        /// 
        /// IL2CPP String memory layout (64-bit):
        /// Offset 0x00: Il2CppClass* klass (8 bytes)
        /// Offset 0x08: MonitorData* monitor (8 bytes)  
        /// Offset 0x10: int32 length (4 bytes)
        /// Offset 0x14: char16_t chars[length] (2 bytes per char)
        /// </summary>
        private static string ReadStringDirect(IntPtr stringPtr)
        {
            if (stringPtr == IntPtr.Zero)
            {
                DebugLog("ReadStringDirect: Null pointer");
                return null;
            }

            DebugLog($"ReadStringDirect: Reading from 0x{stringPtr.ToInt64():X}");

            // Read length at offset 0x10
            int length;
            try
            {
                length = Marshal.ReadInt32(stringPtr, 0x10);
                DebugLog($"ReadStringDirect: Length at offset 0x10 = {length}");
            }
            catch (Exception ex)
            {
                DebugLog($"ReadStringDirect: Failed to read length: {ex.GetType().Name}");
                return null;
            }
            
            // Sanity checks
            if (length <= 0 || length > 1000000)
            {
                DebugLog($"ReadStringDirect: Invalid length {length}, aborting");
                return null;
            }

            // Read chars at offset 0x14
            DebugLog($"ReadStringDirect: Reading {length} chars from offset 0x14...");
            char[] chars = new char[length];
            IntPtr charPtr = IntPtr.Add(stringPtr, 0x14);
            
            try
            {
                for (int i = 0; i < length; i++)
                {
                    chars[i] = (char)Marshal.ReadInt16(charPtr, i * 2);
                    
                    // Basic validation - check for obviously invalid chars
                    if (chars[i] == 0 && i < length - 1)
                    {
                        // Embedded null - truncate here
                        DebugLog($"ReadStringDirect: Found null at index {i}, truncating");
                        return new string(chars, 0, i);
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLog($"ReadStringDirect: Exception reading chars: {ex.GetType().Name}: {ex.Message}");
                return null;
            }
            
            string result = new string(chars);
            DebugLog($"ReadStringDirect: Success, read {result.Length} chars");
            return result;
        }

        /// <summary>
        /// Check if a pointer points to a valid IL2CPP String object.
        /// </summary>
        public static bool IsString(IntPtr objectPtr)
        {
            if (objectPtr == IntPtr.Zero)
                return false;

            try
            {
                IntPtr classPtr = Il2CppBridge.mdb_object_get_class(objectPtr);
                if (classPtr == IntPtr.Zero)
                    return false;

                string className = Il2CppBridge.GetClassName(classPtr);
                return className == "String";
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Try to get the class name of an IL2CPP object.
        /// </summary>
        public static string GetTypeName(IntPtr objectPtr)
        {
            if (objectPtr == IntPtr.Zero)
                return null;

            try
            {
                IntPtr classPtr = Il2CppBridge.mdb_object_get_class(objectPtr);
                if (classPtr == IntPtr.Zero)
                    return null;

                return Il2CppBridge.GetClassFullName(classPtr);
            }
            catch
            {
                return null;
            }
        }
    }
}
