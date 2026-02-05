// ==============================
// Il2CppObject - Base IL2CPP Object Types
// ==============================
// Base class and exception type for IL2CPP objects

using System;

namespace GameSDK
{
    /// <summary>
    /// Base class for all IL2CPP objects. Contains a pointer to the native object.
    /// All generated wrapper classes inherit from this class.
    /// </summary>
    public class Il2CppObject
    {
        /// <summary>
        /// Pointer to the native IL2CPP object.
        /// </summary>
        public IntPtr NativePtr { get; protected set; }

        /// <summary>
        /// Creates an empty IL2CPP object wrapper.
        /// </summary>
        public Il2CppObject() { }

        /// <summary>
        /// Creates an IL2CPP object wrapper for a native pointer.
        /// </summary>
        /// <param name="nativePtr">The native IL2CPP object pointer</param>
        public Il2CppObject(IntPtr nativePtr)
        {
            NativePtr = nativePtr;
        }

        /// <summary>
        /// Returns true if this object has a valid native pointer.
        /// Check this before accessing any properties or methods to avoid IL2CPP errors.
        /// </summary>
        public bool IsValid => NativePtr != IntPtr.Zero;

        /// <summary>
        /// Safe equality check that handles null and invalid pointers.
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj == null) return NativePtr == IntPtr.Zero;
            if (obj is Il2CppObject other) return NativePtr == other.NativePtr;
            if (obj is IntPtr ptr) return NativePtr == ptr;
            return false;
        }

        /// <summary>
        /// Gets the hash code based on the native pointer.
        /// </summary>
        /// <returns>The hash code of the native pointer</returns>
        public override int GetHashCode() => NativePtr.GetHashCode();

        /// <summary>
        /// Implicit conversion to IntPtr for P/Invoke calls.
        /// </summary>
        public static implicit operator IntPtr(Il2CppObject obj) => obj?.NativePtr ?? IntPtr.Zero;

        /// <summary>
        /// Null-check operator for cleaner null-coalescing patterns.
        /// Returns true if the object is valid (non-null pointer).
        /// </summary>
        public static implicit operator bool(Il2CppObject obj) => obj != null && obj.NativePtr != IntPtr.Zero;

        /// <summary>
        /// Override equality operators to handle null pointers correctly.
        /// </summary>
        public static bool operator ==(Il2CppObject left, Il2CppObject right)
        {
            if (ReferenceEquals(left, null) && ReferenceEquals(right, null)) return true;
            if (ReferenceEquals(left, null)) return right.NativePtr == IntPtr.Zero;
            if (ReferenceEquals(right, null)) return left.NativePtr == IntPtr.Zero;
            return left.NativePtr == right.NativePtr;
        }

        /// <summary>
        /// Override inequality operator.
        /// </summary>
        public static bool operator !=(Il2CppObject left, Il2CppObject right)
        {
            return !(left == right);
        }

        /// <summary>
        /// Returns a string representation for debugging.
        /// </summary>
        public override string ToString()
        {
            return NativePtr == IntPtr.Zero 
                ? $"{GetType().Name}(null)" 
                : $"{GetType().Name}(0x{NativePtr.ToInt64():X})";
        }
    }

    /// <summary>
    /// Exception thrown when an IL2CPP operation fails.
    /// Contains information about the error from the native bridge.
    /// </summary>
    public class Il2CppException : Exception
    {
        /// <summary>
        /// The native IL2CPP exception pointer (if available).
        /// </summary>
        public IntPtr NativeException { get; }
        
        /// <summary>
        /// The error code from the bridge (if available).
        /// </summary>
        public MdbErrorCode ErrorCode { get; }
        
        /// <summary>
        /// The native error message from the bridge.
        /// </summary>
        public string NativeMessage { get; }

        /// <summary>
        /// Creates an IL2CPP exception with a message.
        /// </summary>
        public Il2CppException(string message) : base(message)
        {
            ErrorCode = Il2CppBridge.GetLastErrorCode();
            NativeMessage = Il2CppBridge.GetLastError();
        }

        /// <summary>
        /// Creates an IL2CPP exception from a native exception pointer.
        /// </summary>
        public Il2CppException(IntPtr nativeException) 
            : base($"IL2CPP exception at 0x{nativeException.ToInt64():X}")
        {
            NativeException = nativeException;
            ErrorCode = MdbErrorCode.ExceptionThrown;
            NativeMessage = Il2CppBridge.GetLastError();
        }
        
        /// <summary>
        /// Creates an IL2CPP exception with an error code and message.
        /// </summary>
        public Il2CppException(MdbErrorCode errorCode, string message) 
            : base(message)
        {
            ErrorCode = errorCode;
            NativeMessage = Il2CppBridge.GetLastError();
        }
        
        /// <summary>
        /// Returns a detailed string representation of the exception.
        /// </summary>
        public override string ToString()
        {
            return $"{base.ToString()}\nError Code: {ErrorCode}\nNative Message: {NativeMessage}";
        }
    }
}
