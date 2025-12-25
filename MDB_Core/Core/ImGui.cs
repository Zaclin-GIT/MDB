// ==============================
// ImGui - Simplified Dear ImGui Bindings for MDB
// ==============================
// Provides P/Invoke bindings for Dear ImGui functions exported from MDB_Bridge.
// This is a simplified API designed for easy mod development.

using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace GameSDK
{
    #region ImGui Enums

    /// <summary>
    /// Flags for ImGui windows.
    /// </summary>
    [Flags]
    public enum ImGuiWindowFlags
    {
        None = 0,
        NoTitleBar = 1 << 0,
        NoResize = 1 << 1,
        NoMove = 1 << 2,
        NoScrollbar = 1 << 3,
        NoScrollWithMouse = 1 << 4,
        NoCollapse = 1 << 5,
        AlwaysAutoResize = 1 << 6,
        NoBackground = 1 << 7,
        NoSavedSettings = 1 << 8,
        NoMouseInputs = 1 << 9,
        MenuBar = 1 << 10,
        HorizontalScrollbar = 1 << 11,
        NoFocusOnAppearing = 1 << 12,
        NoBringToFrontOnFocus = 1 << 13,
        AlwaysVerticalScrollbar = 1 << 14,
        AlwaysHorizontalScrollbar = 1 << 15,
        NoNavInputs = 1 << 16,
        NoNavFocus = 1 << 17,
        UnsavedDocument = 1 << 18,
        NoNav = NoNavInputs | NoNavFocus,
        NoDecoration = NoTitleBar | NoResize | NoScrollbar | NoCollapse,
        NoInputs = NoMouseInputs | NoNavInputs | NoNavFocus,
    }

    /// <summary>
    /// Flags for tree nodes.
    /// </summary>
    [Flags]
    public enum ImGuiTreeNodeFlags
    {
        None = 0,
        Selected = 1 << 0,
        Framed = 1 << 1,
        AllowOverlap = 1 << 2,
        NoTreePushOnOpen = 1 << 3,
        NoAutoOpenOnLog = 1 << 4,
        DefaultOpen = 1 << 5,
        OpenOnDoubleClick = 1 << 6,
        OpenOnArrow = 1 << 7,
        Leaf = 1 << 8,
        Bullet = 1 << 9,
        FramePadding = 1 << 10,
        SpanAvailWidth = 1 << 11,
        SpanFullWidth = 1 << 12,
        SpanTextWidth = 1 << 13,
        SpanAllColumns = 1 << 14,
        NavLeftJumpsBackHere = 1 << 15,
        CollapsingHeader = Framed | NoTreePushOnOpen | NoAutoOpenOnLog,
    }

    /// <summary>
    /// Condition for setting values.
    /// </summary>
    public enum ImGuiCond
    {
        None = 0,
        Always = 1 << 0,
        Once = 1 << 1,
        FirstUseEver = 1 << 2,
        Appearing = 1 << 3,
    }

    /// <summary>
    /// Color indices for styling.
    /// </summary>
    public enum ImGuiCol
    {
        Text = 0,
        TextDisabled = 1,
        WindowBg = 2,
        ChildBg = 3,
        PopupBg = 4,
        Border = 5,
        BorderShadow = 6,
        FrameBg = 7,
        FrameBgHovered = 8,
        FrameBgActive = 9,
        TitleBg = 10,
        TitleBgActive = 11,
        TitleBgCollapsed = 12,
        MenuBarBg = 13,
        ScrollbarBg = 14,
        ScrollbarGrab = 15,
        ScrollbarGrabHovered = 16,
        ScrollbarGrabActive = 17,
        CheckMark = 18,
        SliderGrab = 19,
        SliderGrabActive = 20,
        Button = 21,
        ButtonHovered = 22,
        ButtonActive = 23,
        Header = 24,
        HeaderHovered = 25,
        HeaderActive = 26,
    }

    /// <summary>
    /// Input text flags.
    /// </summary>
    [Flags]
    public enum ImGuiInputTextFlags
    {
        None = 0,
        CharsDecimal = 1 << 0,
        CharsHexadecimal = 1 << 1,
        CharsUppercase = 1 << 2,
        CharsNoBlank = 1 << 3,
        AutoSelectAll = 1 << 4,
        EnterReturnsTrue = 1 << 5,
        CallbackCompletion = 1 << 6,
        CallbackHistory = 1 << 7,
        CallbackAlways = 1 << 8,
        CallbackCharFilter = 1 << 9,
        AllowTabInput = 1 << 10,
        CtrlEnterForNewLine = 1 << 11,
        NoHorizontalScroll = 1 << 12,
        AlwaysOverwrite = 1 << 13,
        ReadOnly = 1 << 14,
        Password = 1 << 15,
        NoUndoRedo = 1 << 16,
        CharsScientific = 1 << 17,
        EscapeClearsAll = 1 << 20,
    }

    /// <summary>
    /// Combo box flags.
    /// </summary>
    [Flags]
    public enum ImGuiComboFlags
    {
        None = 0,
        PopupAlignLeft = 1 << 0,
        HeightSmall = 1 << 1,
        HeightRegular = 1 << 2,
        HeightLarge = 1 << 3,
        HeightLargest = 1 << 4,
        NoArrowButton = 1 << 5,
        NoPreview = 1 << 6,
    }

    /// <summary>
    /// Selectable flags.
    /// </summary>
    [Flags]
    public enum ImGuiSelectableFlags
    {
        None = 0,
        DontClosePopups = 1 << 0,
        SpanAllColumns = 1 << 1,
        AllowDoubleClick = 1 << 2,
        Disabled = 1 << 3,
        AllowOverlap = 1 << 4,
    }

    /// <summary>
    /// Slider flags.
    /// </summary>
    [Flags]
    public enum ImGuiSliderFlags
    {
        None = 0,
        AlwaysClamp = 1 << 4,
        Logarithmic = 1 << 5,
        NoRoundToFormat = 1 << 6,
        NoInput = 1 << 7,
    }

    #endregion

    /// <summary>
    /// P/Invoke bindings for Dear ImGui functions exported from MDB_Bridge.
    /// </summary>
    public static class ImGui
    {
        private const string DllName = "MDB_Bridge.dll";

        #region Windows

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igBegin")]
        private static extern byte igBegin([MarshalAs(UnmanagedType.LPStr)] string name, IntPtr p_open, ImGuiWindowFlags flags);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igBegin")]
        private static extern unsafe byte igBegin([MarshalAs(UnmanagedType.LPStr)] string name, byte* p_open, ImGuiWindowFlags flags);

        /// <summary>
        /// Begin a new ImGui window.
        /// </summary>
        public static bool Begin(string name, ImGuiWindowFlags flags = ImGuiWindowFlags.None)
        {
            return igBegin(name, IntPtr.Zero, flags) != 0;
        }

        /// <summary>
        /// Begin a new ImGui window with close button.
        /// </summary>
        public static unsafe bool Begin(string name, ref bool open, ImGuiWindowFlags flags = ImGuiWindowFlags.None)
        {
            byte openByte = open ? (byte)1 : (byte)0;
            byte result = igBegin(name, &openByte, flags);
            open = openByte != 0;
            return result != 0;
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igEnd")]
        public static extern void End();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igBeginChild_Str")]
        private static extern byte igBeginChild([MarshalAs(UnmanagedType.LPStr)] string str_id, Vector2 size, int child_flags, ImGuiWindowFlags window_flags);

        public static bool BeginChild(string id, Vector2 size = default, int childFlags = 0, ImGuiWindowFlags windowFlags = ImGuiWindowFlags.None)
        {
            return igBeginChild(id, size, childFlags, windowFlags) != 0;
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igEndChild")]
        public static extern void EndChild();

        #endregion

        #region Window Utilities

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igSetNextWindowPos")]
        public static extern void SetNextWindowPos(Vector2 pos, ImGuiCond cond = ImGuiCond.None, Vector2 pivot = default);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igSetNextWindowSize")]
        public static extern void SetNextWindowSize(Vector2 size, ImGuiCond cond = ImGuiCond.None);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igGetWindowWidth")]
        public static extern float GetWindowWidth();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igGetWindowHeight")]
        public static extern float GetWindowHeight();

        #endregion

        #region Text Widgets

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igTextUnformatted")]
        private static extern void igTextUnformatted([MarshalAs(UnmanagedType.LPStr)] string text, IntPtr text_end);

        /// <summary>
        /// Display text.
        /// </summary>
        public static void Text(string text)
        {
            igTextUnformatted(text ?? "", IntPtr.Zero);
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igTextColored")]
        private static extern void igTextColored(Vector4 col, [MarshalAs(UnmanagedType.LPStr)] string text);

        /// <summary>
        /// Display colored text.
        /// </summary>
        public static void TextColored(Vector4 color, string text)
        {
            igTextColored(color, text ?? "");
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igTextDisabled")]
        private static extern void igTextDisabled([MarshalAs(UnmanagedType.LPStr)] string text);

        /// <summary>
        /// Display greyed-out text.
        /// </summary>
        public static void TextDisabled(string text)
        {
            igTextDisabled(text ?? "");
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igTextWrapped")]
        private static extern void igTextWrapped([MarshalAs(UnmanagedType.LPStr)] string text);

        /// <summary>
        /// Display text with word-wrapping.
        /// </summary>
        public static void TextWrapped(string text)
        {
            igTextWrapped(text ?? "");
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igBulletText")]
        private static extern void igBulletText([MarshalAs(UnmanagedType.LPStr)] string text);

        /// <summary>
        /// Display bullet point and text.
        /// </summary>
        public static void BulletText(string text)
        {
            igBulletText(text ?? "");
        }

        #endregion

        #region Main Widgets

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igButton")]
        private static extern byte igButton([MarshalAs(UnmanagedType.LPStr)] string label, Vector2 size);

        /// <summary>
        /// Create a button. Returns true when clicked.
        /// </summary>
        public static bool Button(string label, Vector2 size = default)
        {
            return igButton(label, size) != 0;
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igSmallButton")]
        private static extern byte igSmallButton([MarshalAs(UnmanagedType.LPStr)] string label);

        /// <summary>
        /// Create a small button.
        /// </summary>
        public static bool SmallButton(string label)
        {
            return igSmallButton(label) != 0;
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igCheckbox")]
        private static extern unsafe byte igCheckbox([MarshalAs(UnmanagedType.LPStr)] string label, byte* v);

        /// <summary>
        /// Create a checkbox.
        /// </summary>
        public static unsafe bool Checkbox(string label, ref bool value)
        {
            byte v = value ? (byte)1 : (byte)0;
            byte result = igCheckbox(label, &v);
            value = v != 0;
            return result != 0;
        }

        #endregion

        #region Input Widgets

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igInputText")]
        private static extern unsafe byte igInputText(
            byte* label, byte* buf, UIntPtr buf_size,
            ImGuiInputTextFlags flags, IntPtr callback, IntPtr user_data);

        /// <summary>
        /// Create a text input field.
        /// </summary>
        public static unsafe bool InputText(string label, ref string text, uint maxLength = 256, ImGuiInputTextFlags flags = ImGuiInputTextFlags.None)
        {
            byte[] buffer = new byte[maxLength];
            if (!string.IsNullOrEmpty(text))
            {
                byte[] textBytes = Encoding.UTF8.GetBytes(text);
                Array.Copy(textBytes, buffer, Math.Min(textBytes.Length, buffer.Length - 1));
            }

            byte[] labelBytes = Encoding.UTF8.GetBytes(label + '\0');
            bool result;
            fixed (byte* bufPtr = buffer)
            fixed (byte* labelPtr = labelBytes)
            {
                result = igInputText(labelPtr, bufPtr, new UIntPtr((uint)buffer.Length), flags, IntPtr.Zero, IntPtr.Zero) != 0;
            }

            if (result)
            {
                int nullIndex = Array.IndexOf(buffer, (byte)0);
                text = Encoding.UTF8.GetString(buffer, 0, nullIndex >= 0 ? nullIndex : buffer.Length);
            }

            return result;
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igInputFloat")]
        private static extern unsafe byte igInputFloat([MarshalAs(UnmanagedType.LPStr)] string label, float* v, float step, float step_fast, [MarshalAs(UnmanagedType.LPStr)] string format, ImGuiInputTextFlags flags);

        /// <summary>
        /// Create a float input field.
        /// </summary>
        public static unsafe bool InputFloat(string label, ref float value, float step = 0, float stepFast = 0, string format = "%.3f", ImGuiInputTextFlags flags = ImGuiInputTextFlags.None)
        {
            fixed (float* ptr = &value)
            {
                return igInputFloat(label, ptr, step, stepFast, format, flags) != 0;
            }
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igInputInt")]
        private static extern unsafe byte igInputInt([MarshalAs(UnmanagedType.LPStr)] string label, int* v, int step, int step_fast, ImGuiInputTextFlags flags);

        /// <summary>
        /// Create an int input field.
        /// </summary>
        public static unsafe bool InputInt(string label, ref int value, int step = 1, int stepFast = 100, ImGuiInputTextFlags flags = ImGuiInputTextFlags.None)
        {
            fixed (int* ptr = &value)
            {
                return igInputInt(label, ptr, step, stepFast, flags) != 0;
            }
        }

        #endregion

        #region Sliders

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igSliderFloat")]
        private static extern unsafe byte igSliderFloat([MarshalAs(UnmanagedType.LPStr)] string label, float* v, float v_min, float v_max, [MarshalAs(UnmanagedType.LPStr)] string format, ImGuiSliderFlags flags);

        /// <summary>
        /// Create a float slider.
        /// </summary>
        public static unsafe bool SliderFloat(string label, ref float value, float min, float max, string format = "%.3f", ImGuiSliderFlags flags = ImGuiSliderFlags.None)
        {
            fixed (float* ptr = &value)
            {
                return igSliderFloat(label, ptr, min, max, format, flags) != 0;
            }
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igSliderInt")]
        private static extern unsafe byte igSliderInt([MarshalAs(UnmanagedType.LPStr)] string label, int* v, int v_min, int v_max, [MarshalAs(UnmanagedType.LPStr)] string format, ImGuiSliderFlags flags);

        /// <summary>
        /// Create an int slider.
        /// </summary>
        public static unsafe bool SliderInt(string label, ref int value, int min, int max, string format = "%d", ImGuiSliderFlags flags = ImGuiSliderFlags.None)
        {
            fixed (int* ptr = &value)
            {
                return igSliderInt(label, ptr, min, max, format, flags) != 0;
            }
        }

        #endregion

        #region Trees

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igTreeNode_Str")]
        private static extern byte igTreeNode([MarshalAs(UnmanagedType.LPStr)] string label);

        /// <summary>
        /// Create a tree node.
        /// </summary>
        public static bool TreeNode(string label)
        {
            return igTreeNode(label) != 0;
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igTreeNodeEx_Str")]
        private static extern byte igTreeNodeEx([MarshalAs(UnmanagedType.LPStr)] string label, ImGuiTreeNodeFlags flags);

        /// <summary>
        /// Create a tree node with flags.
        /// </summary>
        public static bool TreeNodeEx(string label, ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.None)
        {
            return igTreeNodeEx(label, flags) != 0;
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igTreePop")]
        public static extern void TreePop();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igCollapsingHeader_TreeNodeFlags")]
        private static extern byte igCollapsingHeader([MarshalAs(UnmanagedType.LPStr)] string label, ImGuiTreeNodeFlags flags);

        /// <summary>
        /// Create a collapsing header.
        /// </summary>
        public static bool CollapsingHeader(string label, ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.None)
        {
            return igCollapsingHeader(label, flags) != 0;
        }

        #endregion

        #region Selectables & Combos

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igSelectable_Bool")]
        private static extern byte igSelectable([MarshalAs(UnmanagedType.LPStr)] string label, byte selected, ImGuiSelectableFlags flags, Vector2 size);

        /// <summary>
        /// Create a selectable item.
        /// </summary>
        public static bool Selectable(string label, bool selected = false, ImGuiSelectableFlags flags = ImGuiSelectableFlags.None, Vector2 size = default)
        {
            return igSelectable(label, selected ? (byte)1 : (byte)0, flags, size) != 0;
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igBeginCombo")]
        private static extern byte igBeginCombo([MarshalAs(UnmanagedType.LPStr)] string label, [MarshalAs(UnmanagedType.LPStr)] string preview_value, ImGuiComboFlags flags);

        /// <summary>
        /// Begin a combo box.
        /// </summary>
        public static bool BeginCombo(string label, string previewValue, ImGuiComboFlags flags = ImGuiComboFlags.None)
        {
            return igBeginCombo(label, previewValue ?? "", flags) != 0;
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igEndCombo")]
        public static extern void EndCombo();

        #endregion

        #region Menus

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igBeginMainMenuBar")]
        private static extern byte igBeginMainMenuBar();

        public static bool BeginMainMenuBar() => igBeginMainMenuBar() != 0;

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igEndMainMenuBar")]
        public static extern void EndMainMenuBar();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igBeginMenu")]
        private static extern byte igBeginMenu([MarshalAs(UnmanagedType.LPStr)] string label, byte enabled);

        public static bool BeginMenu(string label, bool enabled = true) => igBeginMenu(label, enabled ? (byte)1 : (byte)0) != 0;

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igEndMenu")]
        public static extern void EndMenu();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igMenuItem_Bool")]
        private static extern byte igMenuItem([MarshalAs(UnmanagedType.LPStr)] string label, [MarshalAs(UnmanagedType.LPStr)] string shortcut, byte selected, byte enabled);

        public static bool MenuItem(string label, string shortcut = null, bool selected = false, bool enabled = true)
        {
            return igMenuItem(label, shortcut, selected ? (byte)1 : (byte)0, enabled ? (byte)1 : (byte)0) != 0;
        }

        #endregion

        #region Layout

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igSeparator")]
        public static extern void Separator();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igSameLine")]
        public static extern void SameLine(float offsetFromStartX = 0, float spacing = -1);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igIndent")]
        public static extern void Indent(float indentW = 0);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igUnindent")]
        public static extern void Unindent(float indentW = 0);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igSetNextItemWidth")]
        public static extern void SetNextItemWidth(float itemWidth);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igSpacing")]
        public static extern void Spacing();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igDummy")]
        public static extern void Dummy(Vector2 size);

        #endregion

        #region ID Stack

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igPushID_Str")]
        private static extern void igPushIDStr([MarshalAs(UnmanagedType.LPStr)] string id);

        public static void PushID(string id) => igPushIDStr(id ?? "");

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igPushID_Int")]
        public static extern void PushID(int id);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igPopID")]
        public static extern void PopID();

        #endregion

        #region Styling

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igPushStyleColor_Vec4")]
        public static extern void PushStyleColor(ImGuiCol idx, Vector4 color);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igPopStyleColor")]
        public static extern void PopStyleColor(int count = 1);

        #endregion

        #region Tooltips

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igSetTooltip")]
        private static extern void igSetTooltip([MarshalAs(UnmanagedType.LPStr)] string text);

        public static void SetTooltip(string text) => igSetTooltip(text ?? "");

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igBeginTooltip")]
        private static extern byte igBeginTooltip();

        public static bool BeginTooltip() => igBeginTooltip() != 0;

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igEndTooltip")]
        public static extern void EndTooltip();

        #endregion

        #region Popups

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igBeginPopup")]
        private static extern byte igBeginPopup([MarshalAs(UnmanagedType.LPStr)] string str_id, ImGuiWindowFlags flags);

        public static bool BeginPopup(string id, ImGuiWindowFlags flags = ImGuiWindowFlags.None)
        {
            return igBeginPopup(id, flags) != 0;
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igBeginPopupContextItem")]
        private static extern byte igBeginPopupContextItem([MarshalAs(UnmanagedType.LPStr)] string str_id, int popup_flags);

        public static bool BeginPopupContextItem(string id = null, int popupFlags = 1) // 1 = right click
        {
            return igBeginPopupContextItem(id, popupFlags) != 0;
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igEndPopup")]
        public static extern void EndPopup();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igOpenPopup_Str")]
        public static extern void OpenPopup([MarshalAs(UnmanagedType.LPStr)] string id, int popupFlags = 0);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igCloseCurrentPopup")]
        public static extern void CloseCurrentPopup();

        #endregion

        #region Item Utilities

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igIsItemClicked")]
        private static extern byte igIsItemClicked(int mouseButton);

        public static bool IsItemClicked(int mouseButton = 0) => igIsItemClicked(mouseButton) != 0;

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igIsItemHovered")]
        private static extern byte igIsItemHovered(int flags);

        public static bool IsItemHovered(int flags = 0) => igIsItemHovered(flags) != 0;

        #endregion

        #region Clipboard

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igSetClipboardText")]
        public static extern void SetClipboardText([MarshalAs(UnmanagedType.LPStr)] string text);

        #endregion

        #region Helpers

        /// <summary>
        /// Helper to create a red-colored text for errors.
        /// </summary>
        public static void TextError(string text)
        {
            TextColored(new Vector4(1f, 0.3f, 0.3f, 1f), text);
        }

        /// <summary>
        /// Helper to create a yellow-colored text for warnings.
        /// </summary>
        public static void TextWarning(string text)
        {
            TextColored(new Vector4(1f, 0.9f, 0.3f, 1f), text);
        }

        /// <summary>
        /// Helper to create a green-colored text for success.
        /// </summary>
        public static void TextSuccess(string text)
        {
            TextColored(new Vector4(0.3f, 1f, 0.3f, 1f), text);
        }

        /// <summary>
        /// Helper to create a cyan-colored text for info.
        /// </summary>
        public static void TextInfo(string text)
        {
            TextColored(new Vector4(0.3f, 0.9f, 1f, 1f), text);
        }

        #endregion
    }
}
