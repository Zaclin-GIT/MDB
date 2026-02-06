// ==============================
// ImGuiBindings - Complete ImGui integration for MDB
// ==============================
// This file provides:
// - P/Invoke bindings for MDB_Bridge ImGui controller functions
// - P/Invoke bindings for Dear ImGui rendering functions
// - ImGuiController for lifecycle management
// 
// No external ImGui.NET dependency required - all bindings are self-contained.

using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using GameSDK.ModHost;

namespace MDB.Explorer.ImGui
{
    #region Enums

    /// <summary>
    /// DirectX version detected by the bridge.
    /// </summary>
    public enum DxVersion : int
    {
        Unknown = 0,
        DX11 = 11,
        DX12 = 12
    }

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
        CallbackResize = 1 << 18,
        CallbackEdit = 1 << 19,
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
        WidthFitPreview = 1 << 7,
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

    #region ImGui Native Bindings

    /// <summary>
    /// P/Invoke bindings for Dear ImGui functions exported from MDB_Bridge.
    /// </summary>
    public static class ImGui
    {
        private const string DllName = "MDB_Bridge.dll";

        // ===== Windows =====

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igBegin")]
        private static extern byte igBegin([MarshalAs(UnmanagedType.LPStr)] string name, IntPtr p_open, ImGuiWindowFlags flags);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igBegin")]
        private static extern unsafe byte igBegin([MarshalAs(UnmanagedType.LPStr)] string name, byte* p_open, ImGuiWindowFlags flags);

        public static bool Begin(string name)
        {
            return igBegin(name, IntPtr.Zero, ImGuiWindowFlags.None) != 0;
        }

        public static unsafe bool Begin(string name, ref bool p_open, ImGuiWindowFlags flags = ImGuiWindowFlags.None)
        {
            byte open = p_open ? (byte)1 : (byte)0;
            byte result = igBegin(name, &open, flags);
            p_open = open != 0;
            return result != 0;
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igEnd")]
        public static extern void End();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igBeginChild_Str")]
        private static extern byte igBeginChild([MarshalAs(UnmanagedType.LPStr)] string str_id, Vector2 size, int child_flags, ImGuiWindowFlags window_flags);

        public static bool BeginChild(string str_id, Vector2 size = default, int child_flags = 0, ImGuiWindowFlags window_flags = ImGuiWindowFlags.None)
        {
            return igBeginChild(str_id, size, child_flags, window_flags) != 0;
        }

        public static bool BeginChild(string str_id) => BeginChild(str_id, Vector2.Zero);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igEndChild")]
        public static extern void EndChild();

        // ===== Window utilities =====

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igSetNextWindowPos")]
        public static extern void SetNextWindowPos(Vector2 pos, ImGuiCond cond = ImGuiCond.None, Vector2 pivot = default);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igSetNextWindowSize")]
        public static extern void SetNextWindowSize(Vector2 size, ImGuiCond cond = ImGuiCond.None);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igGetWindowWidth")]
        public static extern float GetWindowWidth();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igGetWindowHeight")]
        public static extern float GetWindowHeight();

        // ===== Widgets: Text =====

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igTextUnformatted")]
        private static extern void igTextUnformatted([MarshalAs(UnmanagedType.LPStr)] string text, IntPtr text_end);

        public static void Text(string text)
        {
            igTextUnformatted(text ?? "", IntPtr.Zero);
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igTextDisabled")]
        private static extern void igTextDisabled([MarshalAs(UnmanagedType.LPStr)] string fmt);

        public static void TextDisabled(string text)
        {
            igTextDisabled(text ?? "");
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igTextWrapped")]
        private static extern void igTextWrapped([MarshalAs(UnmanagedType.LPStr)] string fmt);

        public static void TextWrapped(string text)
        {
            igTextWrapped(text ?? "");
        }

        // ===== Widgets: Main =====

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igButton")]
        private static extern byte igButton([MarshalAs(UnmanagedType.LPStr)] string label, Vector2 size);

        public static bool Button(string label, Vector2 size = default)
        {
            return igButton(label, size) != 0;
        }

        public static bool Button(string label) => Button(label, Vector2.Zero);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igSmallButton")]
        private static extern byte igSmallButton([MarshalAs(UnmanagedType.LPStr)] string label);

        public static bool SmallButton(string label)
        {
            return igSmallButton(label) != 0;
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igCheckbox")]
        private static extern unsafe byte igCheckbox([MarshalAs(UnmanagedType.LPStr)] string label, byte* v);

        public static unsafe bool Checkbox(string label, ref bool v)
        {
            byte val = v ? (byte)1 : (byte)0;
            byte result = igCheckbox(label, &val);
            v = val != 0;
            return result != 0;
        }

        // ===== Widgets: Input =====

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igInputText")]
        private static extern unsafe byte igInputText(
            byte* label,
            byte* buf,
            UIntPtr buf_size,
            ImGuiInputTextFlags flags,
            IntPtr callback,
            IntPtr user_data);

        public static unsafe bool InputText(string label, ref string text, uint maxLength, ImGuiInputTextFlags flags = ImGuiInputTextFlags.None)
        {
            byte[] buffer = new byte[maxLength];
            if (!string.IsNullOrEmpty(text))
            {
                byte[] textBytes = Encoding.UTF8.GetBytes(text);
                Array.Copy(textBytes, buffer, Math.Min(textBytes.Length, buffer.Length - 1));
            }

            bool result;
            byte[] labelBytes = Encoding.UTF8.GetBytes(label + '\0');
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

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igInputTextWithHint")]
        private static extern unsafe byte igInputTextWithHint(
            byte* label,
            byte* hint,
            byte* buf,
            UIntPtr buf_size,
            ImGuiInputTextFlags flags,
            IntPtr callback,
            IntPtr user_data);

        public static unsafe bool InputTextWithHint(string label, string hint, ref string text, uint maxLength, ImGuiInputTextFlags flags = ImGuiInputTextFlags.None)
        {
            byte[] buffer = new byte[maxLength];
            if (!string.IsNullOrEmpty(text))
            {
                byte[] textBytes = Encoding.UTF8.GetBytes(text);
                Array.Copy(textBytes, buffer, Math.Min(textBytes.Length, buffer.Length - 1));
            }

            bool result;
            byte[] labelBytes = Encoding.UTF8.GetBytes(label + '\0');
            byte[] hintBytes = Encoding.UTF8.GetBytes((hint ?? "") + '\0');
            fixed (byte* bufPtr = buffer)
            fixed (byte* labelPtr = labelBytes)
            fixed (byte* hintPtr = hintBytes)
            {
                result = igInputTextWithHint(labelPtr, hintPtr, bufPtr, new UIntPtr((uint)buffer.Length), flags, IntPtr.Zero, IntPtr.Zero) != 0;
            }

            if (result)
            {
                int nullIndex = Array.IndexOf(buffer, (byte)0);
                text = Encoding.UTF8.GetString(buffer, 0, nullIndex >= 0 ? nullIndex : buffer.Length);
            }

            return result;
        }

        // ===== Widgets: Trees =====

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igTreeNode_Str")]
        private static extern byte igTreeNode([MarshalAs(UnmanagedType.LPStr)] string label);

        public static bool TreeNode(string label)
        {
            return igTreeNode(label) != 0;
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igTreeNodeEx_Str")]
        private static extern byte igTreeNodeEx([MarshalAs(UnmanagedType.LPStr)] string label, ImGuiTreeNodeFlags flags);

        public static bool TreeNodeEx(string label, ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.None)
        {
            return igTreeNodeEx(label, flags) != 0;
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igTreePop")]
        public static extern void TreePop();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igCollapsingHeader_TreeNodeFlags")]
        private static extern byte igCollapsingHeader([MarshalAs(UnmanagedType.LPStr)] string label, ImGuiTreeNodeFlags flags);

        public static bool CollapsingHeader(string label, ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.None)
        {
            return igCollapsingHeader(label, flags) != 0;
        }

        // ===== Widgets: Menus =====

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igBeginMainMenuBar")]
        private static extern byte igBeginMainMenuBar();

        public static bool BeginMainMenuBar()
        {
            return igBeginMainMenuBar() != 0;
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igEndMainMenuBar")]
        public static extern void EndMainMenuBar();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igBeginMenuBar")]
        private static extern byte igBeginMenuBar();

        public static bool BeginMenuBar()
        {
            return igBeginMenuBar() != 0;
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igEndMenuBar")]
        public static extern void EndMenuBar();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igBeginMenu")]
        private static extern byte igBeginMenu([MarshalAs(UnmanagedType.LPStr)] string label, byte enabled);

        public static bool BeginMenu(string label, bool enabled = true)
        {
            return igBeginMenu(label, enabled ? (byte)1 : (byte)0) != 0;
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igEndMenu")]
        public static extern void EndMenu();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igMenuItem_Bool")]
        private static extern byte igMenuItem([MarshalAs(UnmanagedType.LPStr)] string label, [MarshalAs(UnmanagedType.LPStr)] string shortcut, byte selected, byte enabled);

        public static bool MenuItem(string label, string shortcut = null, bool selected = false, bool enabled = true)
        {
            return igMenuItem(label, shortcut, selected ? (byte)1 : (byte)0, enabled ? (byte)1 : (byte)0) != 0;
        }

        // ===== Layout =====

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igSeparator")]
        public static extern void Separator();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igSameLine")]
        public static extern void SameLine(float offset_from_start_x = 0.0f, float spacing = -1.0f);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igIndent")]
        public static extern void Indent(float indent_w = 0.0f);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igUnindent")]
        public static extern void Unindent(float indent_w = 0.0f);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igSetNextItemWidth")]
        public static extern void SetNextItemWidth(float item_width);

        // ===== ID stack =====

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igPushID_Str")]
        private static extern void igPushID([MarshalAs(UnmanagedType.LPStr)] string str_id);

        public static void PushID(string str_id)
        {
            igPushID(str_id ?? "");
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igPushID_Int")]
        public static extern void PushID(int int_id);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igPopID")]
        public static extern void PopID();

        // ===== Style =====

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igPushStyleColor_Vec4")]
        public static extern void PushStyleColor(ImGuiCol idx, Vector4 col);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igPopStyleColor")]
        public static extern void PopStyleColor(int count = 1);

        // ===== Item/Widget utilities =====

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igIsItemClicked")]
        private static extern byte igIsItemClicked(int mouse_button);

        public static bool IsItemClicked(int mouse_button = 0)
        {
            return igIsItemClicked(mouse_button) != 0;
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igIsItemHovered")]
        private static extern byte igIsItemHovered(int flags);

        public static bool IsItemHovered(int flags = 0)
        {
            return igIsItemHovered(flags) != 0;
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igIsItemToggledOpen")]
        private static extern byte igIsItemToggledOpen();

        public static bool IsItemToggledOpen()
        {
            return igIsItemToggledOpen() != 0;
        }

        // ===== Widgets: Combo =====

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igBeginCombo")]
        private static extern byte igBeginCombo([MarshalAs(UnmanagedType.LPStr)] string label, [MarshalAs(UnmanagedType.LPStr)] string preview_value, ImGuiComboFlags flags);

        public static bool BeginCombo(string label, string preview_value, ImGuiComboFlags flags = ImGuiComboFlags.None)
        {
            return igBeginCombo(label, preview_value ?? "", flags) != 0;
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igEndCombo")]
        public static extern void EndCombo();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igSelectable_Bool")]
        private static extern byte igSelectable([MarshalAs(UnmanagedType.LPStr)] string label, byte selected, ImGuiSelectableFlags flags, Vector2 size);

        public static bool Selectable(string label, bool selected = false, ImGuiSelectableFlags flags = ImGuiSelectableFlags.None, Vector2 size = default)
        {
            return igSelectable(label, selected ? (byte)1 : (byte)0, flags, size) != 0;
        }

        // ===== Widgets: Drag/Slider =====

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igDragFloat")]
        private static extern unsafe byte igDragFloat([MarshalAs(UnmanagedType.LPStr)] string label, float* v, float v_speed, float v_min, float v_max, [MarshalAs(UnmanagedType.LPStr)] string format, ImGuiSliderFlags flags);

        public static unsafe bool DragFloat(string label, ref float v, float v_speed = 1.0f, float v_min = 0.0f, float v_max = 0.0f, string format = "%.3f", ImGuiSliderFlags flags = ImGuiSliderFlags.None)
        {
            fixed (float* ptr = &v)
            {
                return igDragFloat(label, ptr, v_speed, v_min, v_max, format, flags) != 0;
            }
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igDragFloat3")]
        private static extern unsafe byte igDragFloat3([MarshalAs(UnmanagedType.LPStr)] string label, float* v, float v_speed, float v_min, float v_max, [MarshalAs(UnmanagedType.LPStr)] string format, ImGuiSliderFlags flags);

        public static unsafe bool DragFloat3(string label, ref Vector3 v, float v_speed = 1.0f, float v_min = 0.0f, float v_max = 0.0f, string format = "%.3f", ImGuiSliderFlags flags = ImGuiSliderFlags.None)
        {
            fixed (Vector3* ptr = &v)
            {
                return igDragFloat3(label, (float*)ptr, v_speed, v_min, v_max, format, flags) != 0;
            }
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igDragInt")]
        private static extern unsafe byte igDragInt([MarshalAs(UnmanagedType.LPStr)] string label, int* v, float v_speed, int v_min, int v_max, [MarshalAs(UnmanagedType.LPStr)] string format, ImGuiSliderFlags flags);

        public static unsafe bool DragInt(string label, ref int v, float v_speed = 1.0f, int v_min = 0, int v_max = 0, string format = "%d", ImGuiSliderFlags flags = ImGuiSliderFlags.None)
        {
            fixed (int* ptr = &v)
            {
                return igDragInt(label, ptr, v_speed, v_min, v_max, format, flags) != 0;
            }
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igInputFloat3")]
        private static extern unsafe byte igInputFloat3([MarshalAs(UnmanagedType.LPStr)] string label, float* v, [MarshalAs(UnmanagedType.LPStr)] string format, ImGuiInputTextFlags flags);

        public static unsafe bool InputFloat3(string label, ref Vector3 v, string format = "%.3f", ImGuiInputTextFlags flags = ImGuiInputTextFlags.None)
        {
            fixed (Vector3* ptr = &v)
            {
                return igInputFloat3(label, (float*)ptr, format, flags) != 0;
            }
        }

        // ===== Widgets: Color =====

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igColorButton")]
        private static extern byte igColorButton([MarshalAs(UnmanagedType.LPStr)] string desc_id, Vector4 col, int flags, Vector2 size);

        public static bool ColorButton(string desc_id, Vector4 col, int flags = 0, Vector2 size = default)
        {
            return igColorButton(desc_id, col, flags, size) != 0;
        }

        // ===== Misc =====

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igTextColored")]
        private static extern void igTextColored(Vector4 col, [MarshalAs(UnmanagedType.LPStr)] string fmt);

        public static void TextColored(Vector4 col, string text)
        {
            igTextColored(col, text ?? "");
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igBulletText")]
        private static extern void igBulletText([MarshalAs(UnmanagedType.LPStr)] string fmt);

        public static void BulletText(string text)
        {
            igBulletText(text ?? "");
        }

        // ===== Tooltips =====

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igSetTooltip")]
        private static extern void igSetTooltip([MarshalAs(UnmanagedType.LPStr)] string fmt);

        public static void SetTooltip(string text)
        {
            igSetTooltip(text ?? "");
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igBeginTooltip")]
        private static extern byte igBeginTooltip();

        public static bool BeginTooltip()
        {
            return igBeginTooltip() != 0;
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igEndTooltip")]
        public static extern void EndTooltip();

        // ===== Context Menus / Popups =====

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igBeginPopupContextItem")]
        private static extern byte igBeginPopupContextItem([MarshalAs(UnmanagedType.LPStr)] string str_id, int popup_flags);

        public static bool BeginPopupContextItem(string str_id = null, int popup_flags = 1) // 1 = ImGuiPopupFlags_MouseButtonRight
        {
            return igBeginPopupContextItem(str_id, popup_flags) != 0;
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igBeginPopup")]
        private static extern byte igBeginPopup([MarshalAs(UnmanagedType.LPStr)] string str_id, ImGuiWindowFlags flags);

        public static bool BeginPopup(string str_id, ImGuiWindowFlags flags = ImGuiWindowFlags.None)
        {
            return igBeginPopup(str_id, flags) != 0;
        }

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igEndPopup")]
        public static extern void EndPopup();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igOpenPopup_Str")]
        public static extern void OpenPopup([MarshalAs(UnmanagedType.LPStr)] string str_id, int popup_flags = 0);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igCloseCurrentPopup")]
        public static extern void CloseCurrentPopup();

        // ===== Clipboard =====

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "igSetClipboardText")]
        public static extern void SetClipboardText([MarshalAs(UnmanagedType.LPStr)] string text);
    }

    #endregion

    #region ImGui Bridge (Controller functions)

    /// <summary>
    /// Delegate type for ImGui draw callbacks.
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void ImGuiDrawCallback();

    /// <summary>
    /// P/Invoke declarations for the native ImGui controller in MDB_Bridge.
    /// </summary>
    public static class ImGuiBridge
    {
        private const string DllName = "MDB_Bridge.dll";

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern DxVersion mdb_imgui_get_dx_version();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool mdb_imgui_init();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void mdb_imgui_shutdown();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool mdb_imgui_is_initialized();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void mdb_imgui_register_draw_callback(IntPtr callback);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void mdb_imgui_set_input_enabled([MarshalAs(UnmanagedType.I1)] bool enabled);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        public static extern bool mdb_imgui_is_input_enabled();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void mdb_imgui_set_toggle_key(int vkCode);
    }

    #endregion

    #region ImGui Controller

    /// <summary>
    /// Controller that manages the ImGui integration between native and managed code.
    /// </summary>
    public class ImGuiController : IDisposable
    {
        private const string LOG_TAG = "ImGuiController";

        private ImGuiDrawCallback _drawCallback;
        private IntPtr _drawCallbackPtr;
        private bool _initialized;
        private bool _disposed;

        /// <summary>
        /// Action called each frame to draw ImGui content.
        /// </summary>
        public Action OnDraw { get; set; }

        /// <summary>
        /// Whether ImGui is initialized and ready to draw.
        /// </summary>
        public bool IsInitialized => _initialized && ImGuiBridge.mdb_imgui_is_initialized();

        /// <summary>
        /// Whether ImGui is capturing input.
        /// </summary>
        public bool IsInputEnabled
        {
            get => ImGuiBridge.mdb_imgui_is_input_enabled();
            set => ImGuiBridge.mdb_imgui_set_input_enabled(value);
        }

        /// <summary>
        /// The detected DirectX version.
        /// </summary>
        public DxVersion DirectXVersion => ImGuiBridge.mdb_imgui_get_dx_version();

        /// <summary>
        /// Initialize the ImGui controller.
        /// </summary>
        public bool Initialize()
        {
            if (_initialized)
            {
                ModLogger.LogInternal(LOG_TAG, "[WARN] Already initialized");
                return true;
            }

            ModLogger.LogInternal(LOG_TAG, "[INFO] Initializing ImGui...");

            try
            {
                if (!ImGuiBridge.mdb_imgui_init())
                {
                    var dxVersion = ImGuiBridge.mdb_imgui_get_dx_version();
                    ModLogger.LogInternal(LOG_TAG, $"[ERROR] Failed to initialize native ImGui. DX Version: {dxVersion}");
                    return false;
                }

                _drawCallback = DrawFrame;
                _drawCallbackPtr = Marshal.GetFunctionPointerForDelegate(_drawCallback);
                ImGuiBridge.mdb_imgui_register_draw_callback(_drawCallbackPtr);

                _initialized = true;
                ModLogger.LogInternal(LOG_TAG, $"[INFO] ImGui initialized successfully. DX Version: {DirectXVersion}");
                return true;
            }
            catch (Exception ex)
            {
                ModLogger.LogInternal(LOG_TAG, $"[ERROR] Exception during initialization: {ex.Message}");
                return false;
            }
        }

        private void DrawFrame()
        {
            try
            {
                OnDraw?.Invoke();
            }
            catch (Exception ex)
            {
                ModLogger.LogInternal(LOG_TAG, $"[ERROR] Exception in DrawFrame: {ex.Message}");
            }
        }

        /// <summary>
        /// Set the toggle key for ImGui input capture.
        /// </summary>
        public void SetToggleKey(int vkCode)
        {
            ImGuiBridge.mdb_imgui_set_toggle_key(vkCode);
        }

        /// <summary>
        /// Shutdown ImGui and cleanup resources.
        /// </summary>
        public void Shutdown()
        {
            if (!_initialized) return;

            ModLogger.LogInternal(LOG_TAG, "[INFO] Shutting down ImGui...");

            try
            {
                ImGuiBridge.mdb_imgui_register_draw_callback(IntPtr.Zero);
                ImGuiBridge.mdb_imgui_shutdown();

                _initialized = false;
                _drawCallback = null;
                _drawCallbackPtr = IntPtr.Zero;

                ModLogger.LogInternal(LOG_TAG, "[INFO] ImGui shutdown complete");
            }
            catch (Exception ex)
            {
                ModLogger.LogInternal(LOG_TAG, $"[ERROR] Exception during shutdown: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Shutdown();
            GC.SuppressFinalize(this);
        }

        ~ImGuiController()
        {
            Dispose();
        }
    }

    #endregion
}
