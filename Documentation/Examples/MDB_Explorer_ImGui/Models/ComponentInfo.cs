using System;

namespace MDB.Explorer.ImGui
{
    /// <summary>
    /// Represents a component attached to a GameObject.
    /// </summary>
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
}
