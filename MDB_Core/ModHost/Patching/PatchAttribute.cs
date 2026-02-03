// ==============================
// PatchAttribute - Harmony-like Patching Attributes
// ==============================
// Attributes for declaring method patches similar to MelonLoader's HarmonyX

using System;

namespace GameSDK.ModHost.Patching
{
    /// <summary>
    /// Marks a class as containing patches for a specific type.
    /// Usage: [Patch(typeof(TargetClass))] or [Patch("Namespace", "ClassName")]
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public class PatchAttribute : Attribute
    {
        /// <summary>
        /// The target type to patch (if using generated wrapper type).
        /// </summary>
        public Type TargetType { get; }

        /// <summary>
        /// The namespace of the target type (for IL2CPP lookup).
        /// </summary>
        public string Namespace { get; }

        /// <summary>
        /// The name of the target type (for IL2CPP lookup).
        /// </summary>
        public string TypeName { get; }

        /// <summary>
        /// The target method name to patch. Can also be specified via a second [Patch] attribute.
        /// </summary>
        public string MethodName { get; set; }

        /// <summary>
        /// The assembly containing the target type. Defaults to "Assembly-CSharp".
        /// </summary>
        public string Assembly { get; set; } = "Assembly-CSharp";

        /// <summary>
        /// Patch a target type using a generated wrapper type reference.
        /// </summary>
        /// <param name="targetType">The wrapper type (e.g., typeof(Player))</param>
        public PatchAttribute(Type targetType)
        {
            TargetType = targetType;
            
            // Extract namespace and type name from the wrapper type
            // Wrapper types store original IL2CPP namespace/name in constants
            Namespace = targetType.Namespace ?? "";
            TypeName = targetType.Name;
        }

        /// <summary>
        /// Patch a target type by namespace and name (for obfuscated or non-wrapped types).
        /// </summary>
        /// <param name="ns">The IL2CPP namespace</param>
        /// <param name="typeName">The IL2CPP type name</param>
        public PatchAttribute(string ns, string typeName)
        {
            Namespace = ns ?? "";
            TypeName = typeName;
        }

        /// <summary>
        /// Patch a specific method on the current target type.
        /// Use as a second [Patch] attribute to specify the method name.
        /// </summary>
        /// <param name="methodName">The method name to patch</param>
        public PatchAttribute(string methodName)
        {
            MethodName = methodName;
        }
    }

    /// <summary>
    /// Marks a method as a prefix patch.
    /// Prefix methods run BEFORE the original method.
    /// 
    /// Return false to skip the original method.
    /// Return true (or void) to continue to the original.
    /// 
    /// Special parameters (HarmonyX compatible):
    /// - __instance: The object instance (IntPtr, null for static methods)
    /// - __0, __1, etc.: Original method parameters by index
    /// - ref __result: The return value (ref to modify when skipping original)
    /// - __state: Passed between prefix/postfix
    /// 
    /// Example - Skip original and set return value:
    /// <code>
    /// [Prefix]
    /// public static bool Prefix(IntPtr __instance, ref bool __result)
    /// {
    ///     __result = false;  // Set the return value
    ///     return false;       // Skip original method
    /// }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class PrefixAttribute : Attribute
    {
    }

    /// <summary>
    /// Marks a method as a postfix patch.
    /// Postfix methods run AFTER the original method.
    /// 
    /// Special parameters (HarmonyX compatible):
    /// - __instance: The object instance (IntPtr, null for static methods)
    /// - __0, __1, etc.: Original method parameters by index  
    /// - ref __result: The return value (ref to modify after original runs)
    /// - __state: Value from prefix
    /// 
    /// Example - Modify return value:
    /// <code>
    /// [Postfix]
    /// public static void Postfix(ref bool __result)
    /// {
    ///     __result = true;  // Override the return value
    /// }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class PostfixAttribute : Attribute
    {
    }

    /// <summary>
    /// Marks a method as a finalizer patch.
    /// Finalizer methods run even if the original method throws an exception.
    /// 
    /// Special parameters:
    /// - __exception: The exception that was thrown (null if none)
    /// Returns: A new exception to throw, or null to swallow the original
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class FinalizerAttribute : Attribute
    {
    }

    /// <summary>
    /// Specifies the target method name for a patch.
    /// Use this instead of a second [Patch] attribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class PatchMethodAttribute : Attribute
    {
        public string MethodName { get; }
        public int ParameterCount { get; } = -1;

        public PatchMethodAttribute(string methodName)
        {
            MethodName = methodName;
        }

        public PatchMethodAttribute(string methodName, int parameterCount)
        {
            MethodName = methodName;
            ParameterCount = parameterCount;
        }
    }

    /// <summary>
    /// Specifies the target method by RVA (for obfuscated methods).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class PatchRvaAttribute : Attribute
    {
        public ulong Rva { get; }

        public PatchRvaAttribute(ulong rva)
        {
            Rva = rva;
        }
    }
}
