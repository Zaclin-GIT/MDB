using System;
using System.Collections.Generic;

namespace MDB.Explorer.ImGui
{
    /// <summary>
    /// Represents a GameObject in the hierarchy.
    /// </summary>
    public class HierarchyNode
    {
        public IntPtr Pointer { get; set; }
        public string Name { get; set; }
        public bool IsActive { get; set; }
        public int Depth { get; set; }
        public bool IsExpanded { get; set; }
        public bool HasChildren { get; set; }
        public int ChildCount { get; set; }
        public HierarchyNode Parent { get; set; }
        public List<HierarchyNode> Children { get; set; } = new List<HierarchyNode>();
        public bool ChildrenLoaded { get; set; }
        public int SceneHandle { get; set; }

        public bool IsValid => Pointer != IntPtr.Zero;

        public string DisplayName => HasChildren ? $"{Name} ({ChildCount})" : Name;
    }
}
