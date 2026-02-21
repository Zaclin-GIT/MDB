using System;

namespace MDB.Explorer.ImGui
{
    /// <summary>
    /// Context for a drill-down inspection of an object instance.
    /// </summary>
    public class InspectionContext
    {
        public IntPtr Instance { get; set; }
        public string TypeName { get; set; }
        public ComponentReflectionData ReflectionData { get; set; }

        /// <summary>
        /// Display type name (deobfuscated if available).
        /// </summary>
        public string DisplayTypeName => DeobfuscationHelper.GetTypeName(TypeName);
    }
}
