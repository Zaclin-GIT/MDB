// ==============================
// Il2CppBridge - Constants and Enums
// ==============================
// This file contains enums, constants, and type definitions for the IL2CPP bridge.

namespace GameSDK
{
    /// <summary>
    /// Error codes returned by bridge functions.
    /// These must match the MdbErrorCode enum in bridge_exports.h.
    /// </summary>
    public enum MdbErrorCode : int
    {
        Success = 0,
        
        // Initialization errors (1-99)
        NotInitialized = 1,
        InitFailed = 2,
        GameAssemblyNotFound = 3,
        ExportNotFound = 4,
        
        // Argument errors (100-199)
        InvalidArgument = 100,
        NullPointer = 101,
        InvalidClass = 102,
        InvalidMethod = 103,
        InvalidField = 104,
        
        // Resolution errors (200-299)
        ClassNotFound = 200,
        MethodNotFound = 201,
        FieldNotFound = 202,
        AssemblyNotFound = 203,
        
        // Invocation errors (300-399)
        InvocationFailed = 300,
        ExceptionThrown = 301,
        ThreadNotAttached = 302,
        
        // Memory errors (400-499)
        AllocationFailed = 400,
        BufferTooSmall = 401,
        
        // Unknown error
        Unknown = -1
    }

    /// <summary>
    /// P/Invoke declarations for the native IL2CPP bridge.
    /// These functions communicate with the MDB_Bridge.dll which wraps IL2CPP APIs.
    /// </summary>
    public static partial class Il2CppBridge
    {
        /// <summary>
        /// Name of the native bridge DLL.
        /// </summary>
        private const string DllName = "MDB_Bridge.dll";

        // ==============================
        // IL2CPP Type Constants
        // ==============================
        
        /// <summary>IL2CPP void type.</summary>
        public const int IL2CPP_TYPE_VOID = 0x01;
        
        /// <summary>IL2CPP boolean type.</summary>
        public const int IL2CPP_TYPE_BOOLEAN = 0x02;
        
        /// <summary>IL2CPP int type.</summary>
        public const int IL2CPP_TYPE_I4 = 0x08;
        
        /// <summary>IL2CPP uint type.</summary>
        public const int IL2CPP_TYPE_U4 = 0x09;
        
        /// <summary>IL2CPP long type.</summary>
        public const int IL2CPP_TYPE_I8 = 0x0a;
        
        /// <summary>IL2CPP ulong type.</summary>
        public const int IL2CPP_TYPE_U8 = 0x0b;
        
        /// <summary>IL2CPP float type.</summary>
        public const int IL2CPP_TYPE_R4 = 0x0c;
        
        /// <summary>IL2CPP double type.</summary>
        public const int IL2CPP_TYPE_R8 = 0x0d;
        
        /// <summary>IL2CPP string type.</summary>
        public const int IL2CPP_TYPE_STRING = 0x0e;
        
        /// <summary>IL2CPP pointer type.</summary>
        public const int IL2CPP_TYPE_PTR = 0x0f;
        
        /// <summary>IL2CPP class type.</summary>
        public const int IL2CPP_TYPE_CLASS = 0x12;
        
        /// <summary>IL2CPP object type.</summary>
        public const int IL2CPP_TYPE_OBJECT = 0x1c;

        // ==============================
        // IL2CPP Type Enum Constants
        // ==============================
        
        /// <summary>
        /// Complete enumeration of IL2CPP type constants.
        /// These correspond to the Il2CppTypeEnum in il2cpp metadata.
        /// </summary>
        public static class Il2CppTypeEnum
        {
            /// <summary>End of type list marker.</summary>
            public const int IL2CPP_TYPE_END = 0x00;
            
            /// <summary>Void type.</summary>
            public const int IL2CPP_TYPE_VOID = 0x01;
            
            /// <summary>Boolean type.</summary>
            public const int IL2CPP_TYPE_BOOLEAN = 0x02;
            
            /// <summary>Character type.</summary>
            public const int IL2CPP_TYPE_CHAR = 0x03;
            
            /// <summary>Signed byte type.</summary>
            public const int IL2CPP_TYPE_I1 = 0x04;
            
            /// <summary>Unsigned byte type.</summary>
            public const int IL2CPP_TYPE_U1 = 0x05;
            
            /// <summary>Short type.</summary>
            public const int IL2CPP_TYPE_I2 = 0x06;
            
            /// <summary>Unsigned short type.</summary>
            public const int IL2CPP_TYPE_U2 = 0x07;
            
            /// <summary>Int type.</summary>
            public const int IL2CPP_TYPE_I4 = 0x08;
            
            /// <summary>Unsigned int type.</summary>
            public const int IL2CPP_TYPE_U4 = 0x09;
            
            /// <summary>Long type.</summary>
            public const int IL2CPP_TYPE_I8 = 0x0a;
            
            /// <summary>Unsigned long type.</summary>
            public const int IL2CPP_TYPE_U8 = 0x0b;
            
            /// <summary>Float type.</summary>
            public const int IL2CPP_TYPE_R4 = 0x0c;
            
            /// <summary>Double type.</summary>
            public const int IL2CPP_TYPE_R8 = 0x0d;
            
            /// <summary>String type.</summary>
            public const int IL2CPP_TYPE_STRING = 0x0e;
            
            /// <summary>Pointer type.</summary>
            public const int IL2CPP_TYPE_PTR = 0x0f;
            
            /// <summary>By-reference type.</summary>
            public const int IL2CPP_TYPE_BYREF = 0x10;
            
            /// <summary>Value type (struct).</summary>
            public const int IL2CPP_TYPE_VALUETYPE = 0x11;
            
            /// <summary>Class type.</summary>
            public const int IL2CPP_TYPE_CLASS = 0x12;
            
            /// <summary>Generic type variable.</summary>
            public const int IL2CPP_TYPE_VAR = 0x13;
            
            /// <summary>Array type.</summary>
            public const int IL2CPP_TYPE_ARRAY = 0x14;
            
            /// <summary>Generic instance type.</summary>
            public const int IL2CPP_TYPE_GENERICINST = 0x15;
            
            /// <summary>Typed by-reference.</summary>
            public const int IL2CPP_TYPE_TYPEDBYREF = 0x16;
            
            /// <summary>Native int pointer (IntPtr).</summary>
            public const int IL2CPP_TYPE_I = 0x18;
            
            /// <summary>Native unsigned int pointer (UIntPtr).</summary>
            public const int IL2CPP_TYPE_U = 0x19;
            
            /// <summary>Function pointer type.</summary>
            public const int IL2CPP_TYPE_FNPTR = 0x1b;
            
            /// <summary>System.Object type.</summary>
            public const int IL2CPP_TYPE_OBJECT = 0x1c;
            
            /// <summary>Single-dimension zero-based array.</summary>
            public const int IL2CPP_TYPE_SZARRAY = 0x1d;
            
            /// <summary>Generic method type variable.</summary>
            public const int IL2CPP_TYPE_MVAR = 0x1e;
            
            /// <summary>Enumeration type.</summary>
            public const int IL2CPP_TYPE_ENUM = 0x55;
        }
    }
}
