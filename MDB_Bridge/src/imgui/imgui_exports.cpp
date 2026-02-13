// ==============================
// ImGui Exports for P/Invoke
// ==============================
// Exports Dear ImGui functions with C linkage for C# P/Invoke.
// Uses the same function names as cimgui for compatibility.

#include "imgui_exports.h"
#include "imgui.h"

// ===== Windows =====

IMGUI_EXPORT bool igBegin(const char* name, bool* p_open, ImGuiWindowFlags flags)
{
    return ImGui::Begin(name, p_open, flags);
}

IMGUI_EXPORT void igEnd()
{
    ImGui::End();
}

IMGUI_EXPORT bool igBeginChild_Str(const char* str_id, ImVec2 size, ImGuiChildFlags child_flags, ImGuiWindowFlags window_flags)
{
    return ImGui::BeginChild(str_id, size, child_flags, window_flags);
}

IMGUI_EXPORT void igEndChild()
{
    ImGui::EndChild();
}

// ===== Window utilities =====

IMGUI_EXPORT void igSetNextWindowPos(ImVec2 pos, ImGuiCond cond, ImVec2 pivot)
{
    ImGui::SetNextWindowPos(pos, cond, pivot);
}

IMGUI_EXPORT void igSetNextWindowSize(ImVec2 size, ImGuiCond cond)
{
    ImGui::SetNextWindowSize(size, cond);
}

IMGUI_EXPORT float igGetWindowWidth()
{
    return ImGui::GetWindowWidth();
}

IMGUI_EXPORT float igGetWindowHeight()
{
    return ImGui::GetWindowHeight();
}

// ===== Widgets: Text =====

IMGUI_EXPORT void igTextUnformatted(const char* text, const char* text_end)
{
    ImGui::TextUnformatted(text, text_end);
}

IMGUI_EXPORT void igTextDisabled(const char* fmt, ...)
{
    va_list args;
    va_start(args, fmt);
    ImGui::TextDisabledV(fmt, args);
    va_end(args);
}

// ===== Widgets: Main =====

IMGUI_EXPORT bool igButton(const char* label, ImVec2 size)
{
    return ImGui::Button(label, size);
}

IMGUI_EXPORT bool igCheckbox(const char* label, bool* v)
{
    return ImGui::Checkbox(label, v);
}

// ===== Widgets: Input =====

IMGUI_EXPORT bool igInputText(const char* label, char* buf, size_t buf_size, ImGuiInputTextFlags flags, ImGuiInputTextCallback callback, void* user_data)
{
    return ImGui::InputText(label, buf, buf_size, flags, callback, user_data);
}

IMGUI_EXPORT bool igInputTextWithHint(const char* label, const char* hint, char* buf, size_t buf_size, ImGuiInputTextFlags flags, ImGuiInputTextCallback callback, void* user_data)
{
    return ImGui::InputTextWithHint(label, hint, buf, buf_size, flags, callback, user_data);
}

IMGUI_EXPORT bool igInputFloat(const char* label, float* v, float step, float step_fast, const char* format, ImGuiInputTextFlags flags)
{
    return ImGui::InputFloat(label, v, step, step_fast, format, flags);
}

IMGUI_EXPORT bool igInputInt(const char* label, int* v, int step, int step_fast, ImGuiInputTextFlags flags)
{
    return ImGui::InputInt(label, v, step, step_fast, flags);
}

// ===== Widgets: Trees =====

IMGUI_EXPORT bool igTreeNode_Str(const char* label)
{
    return ImGui::TreeNode(label);
}

IMGUI_EXPORT bool igTreeNodeEx_Str(const char* label, ImGuiTreeNodeFlags flags)
{
    return ImGui::TreeNodeEx(label, flags);
}

IMGUI_EXPORT void igTreePop()
{
    ImGui::TreePop();
}

IMGUI_EXPORT bool igCollapsingHeader_TreeNodeFlags(const char* label, ImGuiTreeNodeFlags flags)
{
    return ImGui::CollapsingHeader(label, flags);
}

// ===== Widgets: Menus =====

IMGUI_EXPORT bool igBeginMainMenuBar()
{
    return ImGui::BeginMainMenuBar();
}

IMGUI_EXPORT void igEndMainMenuBar()
{
    ImGui::EndMainMenuBar();
}

IMGUI_EXPORT bool igBeginMenuBar()
{
    return ImGui::BeginMenuBar();
}

IMGUI_EXPORT void igEndMenuBar()
{
    ImGui::EndMenuBar();
}

IMGUI_EXPORT bool igBeginMenu(const char* label, bool enabled)
{
    return ImGui::BeginMenu(label, enabled);
}

IMGUI_EXPORT void igEndMenu()
{
    ImGui::EndMenu();
}

IMGUI_EXPORT bool igMenuItem_Bool(const char* label, const char* shortcut, bool selected, bool enabled)
{
    return ImGui::MenuItem(label, shortcut, selected, enabled);
}

// ===== Layout =====

IMGUI_EXPORT void igSeparator()
{
    ImGui::Separator();
}

IMGUI_EXPORT void igSameLine(float offset_from_start_x, float spacing)
{
    ImGui::SameLine(offset_from_start_x, spacing);
}

IMGUI_EXPORT void igIndent(float indent_w)
{
    ImGui::Indent(indent_w);
}

IMGUI_EXPORT void igUnindent(float indent_w)
{
    ImGui::Unindent(indent_w);
}

IMGUI_EXPORT void igSpacing()
{
    ImGui::Spacing();
}

IMGUI_EXPORT void igDummy(ImVec2 size)
{
    ImGui::Dummy(size);
}

IMGUI_EXPORT void igSetNextItemWidth(float item_width)
{
    ImGui::SetNextItemWidth(item_width);
}

// ===== Style =====

IMGUI_EXPORT void igPushStyleColor_Vec4(ImGuiCol idx, ImVec4 col)
{
    ImGui::PushStyleColor(idx, col);
}

IMGUI_EXPORT void igPopStyleColor(int count)
{
    ImGui::PopStyleColor(count);
}

// ===== Item/Widget utilities =====

IMGUI_EXPORT bool igIsItemClicked(ImGuiMouseButton mouse_button)
{
    return ImGui::IsItemClicked(mouse_button);
}

IMGUI_EXPORT bool igIsItemHovered(ImGuiHoveredFlags flags)
{
    return ImGui::IsItemHovered(flags);
}

IMGUI_EXPORT bool igIsItemToggledOpen()
{
    return ImGui::IsItemToggledOpen();
}

IMGUI_EXPORT void igBeginDisabled(bool disabled)
{
    ImGui::BeginDisabled(disabled);
}

IMGUI_EXPORT void igEndDisabled()
{
    ImGui::EndDisabled();
}

// ===== Tooltips =====

IMGUI_EXPORT void igSetTooltip(const char* fmt, ...)
{
    va_list args;
    va_start(args, fmt);
    ImGui::SetTooltipV(fmt, args);
    va_end(args);
}
IMGUI_EXPORT bool igBeginTooltip()
{
    return ImGui::BeginTooltip();
}

IMGUI_EXPORT void igEndTooltip()
{
    ImGui::EndTooltip();
}
// ===== Widgets: Combo =====

IMGUI_EXPORT bool igBeginCombo(const char* label, const char* preview_value, ImGuiComboFlags flags)
{
    return ImGui::BeginCombo(label, preview_value, flags);
}

IMGUI_EXPORT void igEndCombo()
{
    ImGui::EndCombo();
}

IMGUI_EXPORT bool igSelectable_Bool(const char* label, bool selected, ImGuiSelectableFlags flags, ImVec2 size)
{
    return ImGui::Selectable(label, selected, flags, size);
}

// ===== Widgets: Drag/Slider =====

IMGUI_EXPORT bool igDragFloat3(const char* label, float v[3], float v_speed, float v_min, float v_max, const char* format, ImGuiSliderFlags flags)
{
    return ImGui::DragFloat3(label, v, v_speed, v_min, v_max, format, flags);
}

IMGUI_EXPORT bool igInputFloat3(const char* label, float v[3], const char* format, ImGuiInputTextFlags flags)
{
    return ImGui::InputFloat3(label, v, format, flags);
}

IMGUI_EXPORT bool igSliderFloat(const char* label, float* v, float v_min, float v_max, const char* format, ImGuiSliderFlags flags)
{
    return ImGui::SliderFloat(label, v, v_min, v_max, format, flags);
}

IMGUI_EXPORT bool igSliderInt(const char* label, int* v, int v_min, int v_max, const char* format, ImGuiSliderFlags flags)
{
    return ImGui::SliderInt(label, v, v_min, v_max, format, flags);
}

// ===== Misc =====

IMGUI_EXPORT void igTextColored(ImVec4 col, const char* fmt, ...)
{
    va_list args;
    va_start(args, fmt);
    ImGui::TextColoredV(col, fmt, args);
    va_end(args);
}

IMGUI_EXPORT void igBulletText(const char* fmt, ...)
{
    va_list args;
    va_start(args, fmt);
    ImGui::BulletTextV(fmt, args);
    va_end(args);
}

IMGUI_EXPORT void igTextWrapped(const char* fmt, ...)
{
    va_list args;
    va_start(args, fmt);
    ImGui::TextWrappedV(fmt, args);
    va_end(args);
}

// ===== ID stack =====

IMGUI_EXPORT void igPushID_Str(const char* str_id)
{
    ImGui::PushID(str_id);
}

IMGUI_EXPORT void igPushID_Int(int int_id)
{
    ImGui::PushID(int_id);
}

IMGUI_EXPORT void igPopID()
{
    ImGui::PopID();
}

// ===== Widgets: Drag =====

IMGUI_EXPORT bool igDragInt(const char* label, int* v, float v_speed, int v_min, int v_max, const char* format, ImGuiSliderFlags flags)
{
    return ImGui::DragInt(label, v, v_speed, v_min, v_max, format, flags);
}

IMGUI_EXPORT bool igDragFloat(const char* label, float* v, float v_speed, float v_min, float v_max, const char* format, ImGuiSliderFlags flags)
{
    return ImGui::DragFloat(label, v, v_speed, v_min, v_max, format, flags);
}

// ===== Widgets: Color =====

IMGUI_EXPORT bool igColorButton(const char* desc_id, ImVec4 col, ImGuiColorEditFlags flags, ImVec2 size)
{
    return ImGui::ColorButton(desc_id, col, flags, size);
}

// ===== Widgets: Buttons =====

IMGUI_EXPORT bool igSmallButton(const char* label)
{
    return ImGui::SmallButton(label);
}

// ===== Context Menus / Popups =====

IMGUI_EXPORT bool igBeginPopupContextItem(const char* str_id, ImGuiPopupFlags popup_flags)
{
    return ImGui::BeginPopupContextItem(str_id, popup_flags);
}

IMGUI_EXPORT bool igBeginPopup(const char* str_id, ImGuiWindowFlags flags)
{
    return ImGui::BeginPopup(str_id, flags);
}

IMGUI_EXPORT void igEndPopup()
{
    ImGui::EndPopup();
}

IMGUI_EXPORT void igOpenPopup_Str(const char* str_id, ImGuiPopupFlags popup_flags)
{
    ImGui::OpenPopup(str_id, popup_flags);
}

IMGUI_EXPORT void igCloseCurrentPopup()
{
    ImGui::CloseCurrentPopup();
}

// ===== Clipboard =====

IMGUI_EXPORT void igSetClipboardText(const char* text)
{
    ImGui::SetClipboardText(text);
}

// ===== DrawList (Overlay Drawing) =====

IMGUI_EXPORT ImDrawList* igGetForegroundDrawList()
{
    return ImGui::GetForegroundDrawList();
}

IMGUI_EXPORT ImDrawList* igGetBackgroundDrawList()
{
    return ImGui::GetBackgroundDrawList();
}

IMGUI_EXPORT void ImDrawList_AddLine(ImDrawList* self, ImVec2 p1, ImVec2 p2, ImU32 col, float thickness)
{
    if (self) self->AddLine(p1, p2, col, thickness);
}

IMGUI_EXPORT void ImDrawList_AddRect(ImDrawList* self, ImVec2 p_min, ImVec2 p_max, ImU32 col, float rounding, int flags, float thickness)
{
    if (self) self->AddRect(p_min, p_max, col, rounding, (ImDrawFlags)flags, thickness);
}

IMGUI_EXPORT void ImDrawList_AddRectFilled(ImDrawList* self, ImVec2 p_min, ImVec2 p_max, ImU32 col, float rounding, int flags)
{
    if (self) self->AddRectFilled(p_min, p_max, col, rounding, (ImDrawFlags)flags);
}

IMGUI_EXPORT void ImDrawList_AddCircle(ImDrawList* self, ImVec2 center, float radius, ImU32 col, int num_segments, float thickness)
{
    if (self) self->AddCircle(center, radius, col, num_segments, thickness);
}

IMGUI_EXPORT void ImDrawList_AddCircleFilled(ImDrawList* self, ImVec2 center, float radius, ImU32 col, int num_segments)
{
    if (self) self->AddCircleFilled(center, radius, col, num_segments);
}

IMGUI_EXPORT void ImDrawList_AddText(ImDrawList* self, ImVec2 pos, ImU32 col, const char* text_begin, const char* text_end)
{
    if (self) self->AddText(pos, col, text_begin, text_end);
}

// ===== Layout Utilities =====

IMGUI_EXPORT void igCalcTextSize(ImVec2* out, const char* text, const char* text_end, bool hide_text_after_double_hash, float wrap_width)
{
    if (out) *out = ImGui::CalcTextSize(text, text_end, hide_text_after_double_hash, wrap_width);
}

IMGUI_EXPORT float igGetCursorPosX()
{
    return ImGui::GetCursorPosX();
}

IMGUI_EXPORT void igSetCursorPosX(float local_x)
{
    ImGui::SetCursorPosX(local_x);
}

IMGUI_EXPORT float igGetContentRegionAvailX()
{
    return ImGui::GetContentRegionAvail().x;
}

IMGUI_EXPORT void igSetNextWindowSizeConstraints(ImVec2 size_min, ImVec2 size_max)
{
    ImGui::SetNextWindowSizeConstraints(size_min, size_max);
}

IMGUI_EXPORT void igGetWindowSize(ImVec2* out)
{
    if (out) *out = ImGui::GetWindowSize();
}

IMGUI_EXPORT void igGetWindowPos(ImVec2* out)
{
    if (out) *out = ImGui::GetWindowPos();
}

// ===== Content Region (Vector2) =====
IMGUI_EXPORT void igGetContentRegionAvail(ImVec2* out)
{
    if (out) *out = ImGui::GetContentRegionAvail();
}

// ===== Tab Bar =====
IMGUI_EXPORT bool igBeginTabBar(const char* str_id, int flags)
{
    return ImGui::BeginTabBar(str_id, flags);
}

IMGUI_EXPORT void igEndTabBar()
{
    ImGui::EndTabBar();
}

IMGUI_EXPORT bool igBeginTabItem(const char* label, bool* p_open, int flags)
{
    return ImGui::BeginTabItem(label, p_open, flags);
}

IMGUI_EXPORT void igEndTabItem()
{
    ImGui::EndTabItem();
}
