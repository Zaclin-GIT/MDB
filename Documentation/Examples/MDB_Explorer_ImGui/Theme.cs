// ==============================
// Theme - Centralized color and style constants
// ==============================

using System.Numerics;

namespace MDB.Explorer.ImGui
{
    /// <summary>
    /// Centralized color palette for the Explorer UI.
    /// All ImGui color constants live here so they can be tweaked in one place.
    /// </summary>
    public static class Theme
    {
        // ===== Text colors =====

        /// <summary>Green — mapped / deobfuscated names.</summary>
        public static readonly Vector4 Mapped = new Vector4(0.5f, 1.0f, 0.5f, 1.0f);

        /// <summary>Orange — obfuscated names that are not yet mapped.</summary>
        public static readonly Vector4 Obfuscated = new Vector4(1.0f, 0.7f, 0.3f, 1.0f);

        /// <summary>Grey — inactive or disabled items.</summary>
        public static readonly Vector4 Disabled = new Vector4(0.5f, 0.5f, 0.5f, 1.0f);

        /// <summary>Yellow — breadcrumb / navigation highlights.</summary>
        public static readonly Vector4 Highlight = new Vector4(1.0f, 0.8f, 0.2f, 1.0f);

        /// <summary>Orange — status bar warning (e.g. "Input Off").</summary>
        public static readonly Vector4 Warning = new Vector4(1.0f, 0.5f, 0.0f, 1.0f);

        /// <summary>Cyan — type names in the inspector.</summary>
        public static readonly Vector4 TypeName = new Vector4(0.4f, 0.7f, 1.0f, 1.0f);

        /// <summary>Light blue-grey — inherited class headers.</summary>
        public static readonly Vector4 InheritedClass = new Vector4(0.6f, 0.6f, 0.8f, 1.0f);

        /// <summary>Light green — property read/write indicators.</summary>
        public static readonly Vector4 PropertyAccess = new Vector4(0.5f, 1.0f, 0.5f, 1.0f);

        /// <summary>Light yellow — method names.</summary>
        public static readonly Vector4 MethodName = new Vector4(1.0f, 1.0f, 0.6f, 1.0f);

        /// <summary>Soft grey — null values.</summary>
        public static readonly Vector4 NullValue = new Vector4(0.6f, 0.6f, 0.6f, 1.0f);

        /// <summary>Orange-red — error text.</summary>
        public static readonly Vector4 Error = new Vector4(1.0f, 0.4f, 0.3f, 1.0f);
    }

    /// <summary>
    /// Centralized layout constants for the Explorer UI.
    /// Keeps magic numbers out of rendering code.
    /// </summary>
    public static class LayoutConstants
    {
        // ===== Window defaults =====

        /// <summary>Default hierarchy panel width.</summary>
        public const float HierarchyWidth = 350f;

        /// <summary>Default inspector panel width.</summary>
        public const float InspectorWidth = 400f;

        /// <summary>Default deobfuscation panel width.</summary>
        public const float DeobfuscationWidth = 500f;

        /// <summary>Default deobfuscation panel height.</summary>
        public const float DeobfuscationHeight = 600f;

        /// <summary>Default panel height.</summary>
        public const float DefaultPanelHeight = 500f;

        /// <summary>Y offset for panels below the menu bar.</summary>
        public const float MenuBarOffset = 30f;

        /// <summary>Left margin for the first panel.</summary>
        public const float LeftMargin = 10f;

        // ===== Inspector layout =====

        /// <summary>Column width for value display in the inspector.</summary>
        public const float ValueColumnWidth = 220f;

        /// <summary>Width of edit widgets (input fields, sliders).</summary>
        public const float EditWidgetWidth = 120f;

        /// <summary>Maximum characters for display names before truncation.</summary>
        public const int MaxNameChars = 32;

        /// <summary>Indentation for Transform labels (Position/Rotation/Scale).</summary>
        public const float TransformLabelIndent = 100f;

        // ===== Deobfuscation panel =====

        /// <summary>Width of the type list left pane.</summary>
        public const float TypeListPaneWidth = 350f;

        /// <summary>Width of search/filter input boxes.</summary>
        public const float SearchBoxWidth = 200f;

        /// <summary>Width of the member search input box.</summary>
        public const float MemberSearchWidth = 150f;

        /// <summary>Maximum items in type list before "refine search" message.</summary>
        public const int MaxTypeListItems = 500;

        /// <summary>Maximum methods displayed before truncation.</summary>
        public const int MaxMethodDisplayCount = 200;

        // ===== General =====

        /// <summary>Status bar height reservation.</summary>
        public const float StatusBarHeight = 25f;
    }
}
