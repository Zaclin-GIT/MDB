using System;
using System.Collections.Generic;

namespace MDB.Explorer.ImGui
{
    /// <summary>
    /// Represents an IL2CPP field with cached metadata.
    /// </summary>
    public class FieldInfo
    {
        public IntPtr Pointer { get; set; }
        public string Name { get; set; }
        public IntPtr Type { get; set; }
        public string TypeName { get; set; }
        public int TypeEnum { get; set; }
        public bool IsStatic { get; set; }
        public bool IsValueType { get; set; }

        /// <summary>
        /// Parent class name for qualified lookups.
        /// </summary>
        public string ParentClassName { get; set; }

        /// <summary>
        /// Display name (deobfuscated if available).
        /// </summary>
        public string DisplayName => DeobfuscationHelper.GetFieldName(ParentClassName, Name);

        /// <summary>
        /// Display type name (deobfuscated if available).
        /// </summary>
        public string DisplayTypeName => DeobfuscationHelper.GetTypeName(TypeName);
    }

    /// <summary>
    /// Represents an IL2CPP property with cached metadata.
    /// </summary>
    public class PropertyInfo
    {
        public IntPtr Pointer { get; set; }
        public string Name { get; set; }
        public IntPtr GetterMethod { get; set; }
        public IntPtr SetterMethod { get; set; }
        public IntPtr ReturnType { get; set; }
        public string TypeName { get; set; }
        public int TypeEnum { get; set; }
        public bool CanRead => GetterMethod != IntPtr.Zero;
        public bool CanWrite => SetterMethod != IntPtr.Zero;

        /// <summary>
        /// Parent class name for qualified lookups.
        /// </summary>
        public string ParentClassName { get; set; }

        /// <summary>
        /// Display name (deobfuscated if available).
        /// </summary>
        public string DisplayName => DeobfuscationHelper.GetPropertyName(ParentClassName, Name);

        /// <summary>
        /// Display type name (deobfuscated if available).
        /// </summary>
        public string DisplayTypeName => DeobfuscationHelper.GetTypeName(TypeName);
    }

    /// <summary>
    /// Represents an IL2CPP method with cached metadata.
    /// </summary>
    public class MethodInfo
    {
        public IntPtr Pointer { get; set; }
        public string Name { get; set; }
        public int ParameterCount { get; set; }
        public IntPtr ReturnType { get; set; }
        public string ReturnTypeName { get; set; }
        public int Flags { get; set; }
        
        /// <summary>
        /// Human-readable parameter signature, e.g. "int, string, float".
        /// Built at enumeration time from IL2CPP type info.
        /// </summary>
        public string ParameterSignature { get; set; }
        
        public bool IsStatic => (Flags & 0x0010) != 0;  // METHOD_ATTRIBUTE_STATIC
        public bool IsPublic => (Flags & 0x0006) == 0x0006;  // METHOD_ATTRIBUTE_PUBLIC
        public bool IsGetter => Name?.StartsWith("get_") ?? false;
        public bool IsSetter => Name?.StartsWith("set_") ?? false;
        public bool IsSpecialName => IsGetter || IsSetter ||
            (Name?.StartsWith("add_") ?? false) ||
            (Name?.StartsWith("remove_") ?? false);

        /// <summary>
        /// Parent class name for qualified lookups.
        /// </summary>
        public string ParentClassName { get; set; }

        /// <summary>
        /// Display name (deobfuscated if available).
        /// </summary>
        public string DisplayName => DeobfuscationHelper.GetMethodName(ParentClassName, Name);

        /// <summary>
        /// Display return type name (deobfuscated if available).
        /// </summary>
        public string DisplayReturnTypeName => DeobfuscationHelper.GetTypeName(ReturnTypeName);
    }

    /// <summary>
    /// Cached reflection data for a component.
    /// </summary>
    public class ComponentReflectionData
    {
        public IntPtr ClassPointer { get; set; }
        public string ClassName { get; set; }
        public List<FieldInfo> Fields { get; set; } = new List<FieldInfo>();
        public List<PropertyInfo> Properties { get; set; } = new List<PropertyInfo>();
        public List<MethodInfo> Methods { get; set; } = new List<MethodInfo>();

        /// <summary>
        /// Ordered list of class names in inheritance hierarchy (most derived first).
        /// </summary>
        public List<string> ClassHierarchy { get; set; } = new List<string>();

        /// <summary>
        /// Display class name (deobfuscated if available).
        /// </summary>
        public string DisplayClassName => DeobfuscationHelper.GetTypeName(ClassName);

        /// <summary>
        /// Display class name with obfuscated hint if applicable.
        /// </summary>
        public string DisplayClassNameWithHint => DeobfuscationHelper.GetTypeNameWithHint(ClassName);

        /// <summary>
        /// Gets fields grouped by their declaring class, ordered by inheritance (most derived first).
        /// </summary>
        public IEnumerable<(string ClassName, string DisplayClassName, List<FieldInfo> Fields)> GetFieldsByClass()
        {
            var grouped = new Dictionary<string, List<FieldInfo>>();
            foreach (var field in Fields)
            {
                var key = field.ParentClassName ?? ClassName;
                if (!grouped.ContainsKey(key))
                    grouped[key] = new List<FieldInfo>();
                grouped[key].Add(field);
            }

            foreach (var className in ClassHierarchy)
            {
                if (grouped.TryGetValue(className, out var fields))
                    yield return (className, DeobfuscationHelper.GetTypeName(className), fields);
            }
        }

        /// <summary>
        /// Gets properties grouped by their declaring class, ordered by inheritance (most derived first).
        /// </summary>
        public IEnumerable<(string ClassName, string DisplayClassName, List<PropertyInfo> Properties)> GetPropertiesByClass()
        {
            var grouped = new Dictionary<string, List<PropertyInfo>>();
            foreach (var prop in Properties)
            {
                var key = prop.ParentClassName ?? ClassName;
                if (!grouped.ContainsKey(key))
                    grouped[key] = new List<PropertyInfo>();
                grouped[key].Add(prop);
            }

            foreach (var className in ClassHierarchy)
            {
                if (grouped.TryGetValue(className, out var props))
                    yield return (className, DeobfuscationHelper.GetTypeName(className), props);
            }
        }

        /// <summary>
        /// Gets methods grouped by their declaring class, ordered by inheritance (most derived first).
        /// </summary>
        public IEnumerable<(string ClassName, string DisplayClassName, List<MethodInfo> Methods)> GetMethodsByClass()
        {
            var grouped = new Dictionary<string, List<MethodInfo>>();
            foreach (var method in Methods)
            {
                var key = method.ParentClassName ?? ClassName;
                if (!grouped.ContainsKey(key))
                    grouped[key] = new List<MethodInfo>();
                grouped[key].Add(method);
            }

            foreach (var className in ClassHierarchy)
            {
                if (grouped.TryGetValue(className, out var methods))
                    yield return (className, DeobfuscationHelper.GetTypeName(className), methods);
            }
        }
    }
}
