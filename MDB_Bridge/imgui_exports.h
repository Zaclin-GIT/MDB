// ==============================
// ImGui Exports Header
// ==============================
// Declares exported ImGui functions for P/Invoke.

#pragma once

#include "imgui.h"

#ifdef __cplusplus
extern "C" {
#endif

#ifdef MDB_BRIDGE_EXPORTS
#define IMGUI_EXPORT __declspec(dllexport)
#else
#define IMGUI_EXPORT __declspec(dllimport)
#endif

// ===== Windows =====
IMGUI_EXPORT bool igBegin(const char* name, bool* p_open, ImGuiWindowFlags flags);
IMGUI_EXPORT void igEnd();
IMGUI_EXPORT bool igBeginChild_Str(const char* str_id, ImVec2 size, ImGuiChildFlags child_flags, ImGuiWindowFlags window_flags);
IMGUI_EXPORT void igEndChild();

// ===== Window utilities =====
IMGUI_EXPORT void igSetNextWindowPos(ImVec2 pos, ImGuiCond cond, ImVec2 pivot);
IMGUI_EXPORT void igSetNextWindowSize(ImVec2 size, ImGuiCond cond);
IMGUI_EXPORT float igGetWindowWidth();
IMGUI_EXPORT float igGetWindowHeight();

// ===== Widgets: Text =====
IMGUI_EXPORT void igTextUnformatted(const char* text, const char* text_end);
IMGUI_EXPORT void igTextDisabled(const char* fmt, ...);

// ===== Widgets: Main =====
IMGUI_EXPORT bool igButton(const char* label, ImVec2 size);
IMGUI_EXPORT bool igCheckbox(const char* label, bool* v);

// ===== Widgets: Input =====
IMGUI_EXPORT bool igInputText(const char* label, char* buf, size_t buf_size, ImGuiInputTextFlags flags, ImGuiInputTextCallback callback, void* user_data);
IMGUI_EXPORT bool igInputTextWithHint(const char* label, const char* hint, char* buf, size_t buf_size, ImGuiInputTextFlags flags, ImGuiInputTextCallback callback, void* user_data);

// ===== Widgets: Trees =====
IMGUI_EXPORT bool igTreeNode_Str(const char* label);
IMGUI_EXPORT bool igTreeNodeEx_Str(const char* label, ImGuiTreeNodeFlags flags);
IMGUI_EXPORT void igTreePop();
IMGUI_EXPORT bool igCollapsingHeader_TreeNodeFlags(const char* label, ImGuiTreeNodeFlags flags);

// ===== Widgets: Menus =====
IMGUI_EXPORT bool igBeginMainMenuBar();
IMGUI_EXPORT void igEndMainMenuBar();
IMGUI_EXPORT bool igBeginMenu(const char* label, bool enabled);
IMGUI_EXPORT void igEndMenu();
IMGUI_EXPORT bool igMenuItem_Bool(const char* label, const char* shortcut, bool selected, bool enabled);

// ===== Layout =====
IMGUI_EXPORT void igSeparator();
IMGUI_EXPORT void igSameLine(float offset_from_start_x, float spacing);
IMGUI_EXPORT void igIndent(float indent_w);
IMGUI_EXPORT void igUnindent(float indent_w);
IMGUI_EXPORT void igSetNextItemWidth(float item_width);

// ===== Style =====
IMGUI_EXPORT void igPushStyleColor_Vec4(ImGuiCol idx, ImVec4 col);
IMGUI_EXPORT void igPopStyleColor(int count);

// ===== Item/Widget utilities =====
IMGUI_EXPORT bool igIsItemClicked(ImGuiMouseButton mouse_button);
IMGUI_EXPORT bool igIsItemHovered(ImGuiHoveredFlags flags);
IMGUI_EXPORT bool igIsItemToggledOpen();

// ===== Tooltips =====
IMGUI_EXPORT void igSetTooltip(const char* fmt, ...);

// ===== Widgets: Combo =====
IMGUI_EXPORT bool igBeginCombo(const char* label, const char* preview_value, ImGuiComboFlags flags);
IMGUI_EXPORT void igEndCombo();
IMGUI_EXPORT bool igSelectable_Bool(const char* label, bool selected, ImGuiSelectableFlags flags, ImVec2 size);

// ===== Widgets: Drag/Slider =====
IMGUI_EXPORT bool igDragFloat3(const char* label, float v[3], float v_speed, float v_min, float v_max, const char* format, ImGuiSliderFlags flags);
IMGUI_EXPORT bool igInputFloat3(const char* label, float v[3], const char* format, ImGuiInputTextFlags flags);
IMGUI_EXPORT bool igSliderFloat(const char* label, float* v, float v_min, float v_max, const char* format, ImGuiSliderFlags flags);
IMGUI_EXPORT bool igSliderInt(const char* label, int* v, int v_min, int v_max, const char* format, ImGuiSliderFlags flags);

// ===== Misc =====
IMGUI_EXPORT void igTextColored(ImVec4 col, const char* fmt, ...);
IMGUI_EXPORT void igBulletText(const char* fmt, ...);
IMGUI_EXPORT void igTextWrapped(const char* fmt, ...);

// ===== ID stack =====
IMGUI_EXPORT void igPushID_Str(const char* str_id);
IMGUI_EXPORT void igPushID_Int(int int_id);
IMGUI_EXPORT void igPopID();

// ===== Widgets: Drag =====
IMGUI_EXPORT bool igDragInt(const char* label, int* v, float v_speed, int v_min, int v_max, const char* format, ImGuiSliderFlags flags);
IMGUI_EXPORT bool igDragFloat(const char* label, float* v, float v_speed, float v_min, float v_max, const char* format, ImGuiSliderFlags flags);

// ===== Widgets: Color =====
IMGUI_EXPORT bool igColorButton(const char* desc_id, ImVec4 col, ImGuiColorEditFlags flags, ImVec2 size);

// ===== Widgets: Buttons =====
IMGUI_EXPORT bool igSmallButton(const char* label);

// ===== Context Menus / Popups =====
IMGUI_EXPORT bool igBeginPopupContextItem(const char* str_id, ImGuiPopupFlags popup_flags);
IMGUI_EXPORT bool igBeginPopup(const char* str_id, ImGuiWindowFlags flags);
IMGUI_EXPORT void igEndPopup();
IMGUI_EXPORT void igOpenPopup_Str(const char* str_id, ImGuiPopupFlags popup_flags);
IMGUI_EXPORT void igCloseCurrentPopup();

// ===== Clipboard =====
IMGUI_EXPORT void igSetClipboardText(const char* text);

// ===== DrawList (Overlay Drawing) =====
IMGUI_EXPORT ImDrawList* igGetForegroundDrawList();
IMGUI_EXPORT ImDrawList* igGetBackgroundDrawList();
IMGUI_EXPORT void ImDrawList_AddLine(ImDrawList* self, ImVec2 p1, ImVec2 p2, ImU32 col, float thickness);
IMGUI_EXPORT void ImDrawList_AddRect(ImDrawList* self, ImVec2 p_min, ImVec2 p_max, ImU32 col, float rounding, int flags, float thickness);
IMGUI_EXPORT void ImDrawList_AddRectFilled(ImDrawList* self, ImVec2 p_min, ImVec2 p_max, ImU32 col, float rounding, int flags);
IMGUI_EXPORT void ImDrawList_AddCircle(ImDrawList* self, ImVec2 center, float radius, ImU32 col, int num_segments, float thickness);
IMGUI_EXPORT void ImDrawList_AddCircleFilled(ImDrawList* self, ImVec2 center, float radius, ImU32 col, int num_segments);
IMGUI_EXPORT void ImDrawList_AddText(ImDrawList* self, ImVec2 pos, ImU32 col, const char* text_begin, const char* text_end);

#ifdef __cplusplus
}
#endif
