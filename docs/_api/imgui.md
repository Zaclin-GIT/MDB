---
layout: default
title: Dear ImGui Bindings
---

# Dear ImGui Bindings

Complete P/Invoke bindings for Dear ImGui functions in MDB. These bindings provide a simplified, C#-friendly API for building immediate-mode GUI elements in your mods.

**Namespace:** `GameSDK`

---

## Overview

Dear ImGui is an immediate-mode GUI library perfect for debugging tools, in-game editors, and mod interfaces. MDB provides direct C# bindings to ImGui functions exported from `MDB_Bridge.dll`.

### Key Concepts

- **Immediate Mode:** UI is rebuilt every frame by calling functions
- **No State Management:** No need to track UI state - just call functions
- **Automatic Layout:** ImGui handles positioning, sizing, and rendering
- **Begin/End Pairs:** Most UI elements require Begin/End pairs

---

## Table of Contents

1. [Windows](#windows)
2. [Text Widgets](#text-widgets)
3. [Main Widgets](#main-widgets)
4. [Input Widgets](#input-widgets)
5. [Sliders](#sliders)
6. [Trees & Headers](#trees--headers)
7. [Selectables & Combos](#selectables--combos)
8. [Menus](#menus)
9. [Layout](#layout)
10. [Styling](#styling)
11. [Tooltips](#tooltips)
12. [Popups](#popups)
13. [Drawing](#drawing-overlay)
14. [Utilities](#utilities)
15. [Complete Examples](#complete-examples)

---

## Windows

### Begin() / End()

```csharp
public static bool Begin(string name, ImGuiWindowFlags flags = ImGuiWindowFlags.None)
public static bool Begin(string name, ref bool open, ImGuiWindowFlags flags = ImGuiWindowFlags.None)
public static void End()
```

Begin a new ImGui window. Must be paired with `End()`.

**Parameters:**
- `name` - Window title (also used as unique ID)
- `open` - Optional close button state. When user clicks X, set to false
- `flags` - Window behavior flags

**Returns:** `true` if window is visible (not collapsed)

**Example:**
```csharp
// Simple window
if (ImGui.Begin("My Window"))
{
    ImGui.Text("Content here");
}
ImGui.End();

// With close button
bool windowOpen = true;
if (ImGui.Begin("Closable Window", ref windowOpen))
{
    ImGui.Text("Click X to close");
}
ImGui.End();

if (!windowOpen)
{
    // Window was closed
}
```

---

### BeginChild() / EndChild()

```csharp
public static bool BeginChild(string id, Vector2 size = default, 
    int childFlags = 0, ImGuiWindowFlags windowFlags = ImGuiWindowFlags.None)
public static void EndChild()
```

Begin a scrollable child region within a window.

**Example:**
```csharp
if (ImGui.Begin("Parent Window"))
{
    // Create scrollable area
    if (ImGui.BeginChild("ScrollArea", new Vector2(0, 300)))
    {
        for (int i = 0; i < 100; i++)
        {
            ImGui.Text($"Line {i}");
        }
    }
    ImGui.EndChild();
}
ImGui.End();
```

---

### Window Utilities

```csharp
public static void SetNextWindowPos(Vector2 pos, ImGuiCond cond = ImGuiCond.None, Vector2 pivot = default)
public static void SetNextWindowSize(Vector2 size, ImGuiCond cond = ImGuiCond.None)
public static float GetWindowWidth()
public static float GetWindowHeight()
```

Set window position and size before calling `Begin()`.

**Example:**
```csharp
// Position at top-left on first appearance
ImGui.SetNextWindowPos(new Vector2(10, 10), ImGuiCond.FirstUseEver);

// Set initial size
ImGui.SetNextWindowSize(new Vector2(400, 300), ImGuiCond.FirstUseEver);

if (ImGui.Begin("Positioned Window"))
{
    float width = ImGui.GetWindowWidth();
    ImGui.Text($"Window width: {width}");
}
ImGui.End();
```

---

### ImGuiWindowFlags

```csharp
[Flags]
public enum ImGuiWindowFlags
{
    None = 0,
    NoTitleBar = 1 << 0,           // No title bar
    NoResize = 1 << 1,             // Cannot resize
    NoMove = 1 << 2,               // Cannot move
    NoScrollbar = 1 << 3,          // No scrollbars
    NoScrollWithMouse = 1 << 4,    // No scroll with mouse wheel
    NoCollapse = 1 << 5,           // No collapse button
    AlwaysAutoResize = 1 << 6,     // Auto-resize to fit content
    NoBackground = 1 << 7,         // Transparent background
    NoSavedSettings = 1 << 8,      // Don't save position/size
    NoMouseInputs = 1 << 9,        // Ignore mouse input
    MenuBar = 1 << 10,             // Has menu bar
    HorizontalScrollbar = 1 << 11, // Show horizontal scrollbar
    NoFocusOnAppearing = 1 << 12,  // Don't take focus when appearing
    NoBringToFrontOnFocus = 1 << 13, // Don't bring to front on focus
    AlwaysVerticalScrollbar = 1 << 14,
    AlwaysHorizontalScrollbar = 1 << 15,
    NoNavInputs = 1 << 16,
    NoNavFocus = 1 << 17,
    UnsavedDocument = 1 << 18,
    
    // Shortcuts
    NoNav = NoNavInputs | NoNavFocus,
    NoDecoration = NoTitleBar | NoResize | NoScrollbar | NoCollapse,
    NoInputs = NoMouseInputs | NoNavInputs | NoNavFocus,
}
```

**Example:**
```csharp
// Overlay window (no decoration, transparent)
if (ImGui.Begin("Overlay", 
    ImGuiWindowFlags.NoDecoration | 
    ImGuiWindowFlags.NoBackground | 
    ImGuiWindowFlags.NoMove))
{
    ImGui.Text("Overlay text");
}
ImGui.End();
```

---

## Text Widgets

### Text()

```csharp
public static void Text(string text)
```

Display plain text.

**Example:**
```csharp
ImGui.Text("Hello World!");
ImGui.Text($"Player Health: {health}");
```

---

### TextColored()

```csharp
public static void TextColored(Vector4 color, string text)
```

Display colored text. Color components are RGBA in range [0, 1].

**Example:**
```csharp
// Red text
ImGui.TextColored(new Vector4(1, 0, 0, 1), "Error!");

// Yellow text
ImGui.TextColored(new Vector4(1, 1, 0, 1), "Warning!");

// Green text with alpha
ImGui.TextColored(new Vector4(0, 1, 0, 0.8f), "Success");
```

---

### Text Helpers

```csharp
public static void TextDisabled(string text)    // Greyed-out text
public static void TextWrapped(string text)     // Word-wrapped text
public static void BulletText(string text)      // Bullet point + text
public static void TextError(string text)       // Red text (helper)
public static void TextWarning(string text)     // Yellow text (helper)
public static void TextSuccess(string text)     // Green text (helper)
public static void TextInfo(string text)        // Cyan text (helper)
```

**Example:**
```csharp
ImGui.Text("Normal text");
ImGui.TextDisabled("Disabled feature");
ImGui.TextWrapped("This is a long text that will wrap to multiple lines automatically");
ImGui.BulletText("Bullet point 1");
ImGui.BulletText("Bullet point 2");

ImGui.Separator();

ImGui.TextError("Something went wrong!");
ImGui.TextWarning("Be careful!");
ImGui.TextSuccess("Operation completed!");
ImGui.TextInfo("Information message");
```

---

## Main Widgets

### Button()

```csharp
public static bool Button(string label, Vector2 size = default)
public static bool SmallButton(string label)
```

Create a button. Returns `true` when clicked.

**Example:**
```csharp
if (ImGui.Button("Click Me"))
{
    Logger.Info("Button clicked!");
}

// Custom size button
if (ImGui.Button("Wide Button", new Vector2(200, 40)))
{
    // Clicked
}

// Small button (no frame padding)
if (ImGui.SmallButton("Small"))
{
    // Clicked
}
```

---

### Checkbox()

```csharp
public static bool Checkbox(string label, ref bool value)
```

Create a checkbox. Returns `true` when value changes.

**Example:**
```csharp
private bool _enabled = true;
private bool _godMode = false;

void DrawUI()
{
    if (ImGui.Checkbox("Enabled", ref _enabled))
    {
        Logger.Info($"Enabled: {_enabled}");
    }
    
    if (ImGui.Checkbox("God Mode", ref _godMode))
    {
        ApplyGodMode(_godMode);
    }
}
```

---

## Input Widgets

### InputText()

```csharp
public static bool InputText(string label, ref string text, 
    uint maxLength = 256, ImGuiInputTextFlags flags = ImGuiInputTextFlags.None)
```

Create a text input field. Returns `true` when value changes.

**Example:**
```csharp
private string _playerName = "Player";
private string _searchQuery = "";

void DrawUI()
{
    if (ImGui.InputText("Player Name", ref _playerName, 64))
    {
        Logger.Info($"Name changed to: {_playerName}");
    }
    
    ImGui.InputText("Search", ref _searchQuery, 256);
}
```

---

### InputFloat()

```csharp
public static bool InputFloat(string label, ref float value, 
    float step = 0, float stepFast = 0, string format = "%.3f", 
    ImGuiInputTextFlags flags = ImGuiInputTextFlags.None)
```

Create a float input field with optional step buttons.

**Example:**
```csharp
private float _speed = 1.0f;
private float _health = 100f;

void DrawUI()
{
    // Basic float input
    ImGui.InputFloat("Speed", ref _speed);
    
    // With step buttons (±0.1, ±1.0 on fast)
    ImGui.InputFloat("Health", ref _health, 0.1f, 1.0f);
    
    // Custom format (2 decimals)
    ImGui.InputFloat("Value", ref _speed, 0, 0, "%.2f");
}
```

---

### InputInt()

```csharp
public static bool InputInt(string label, ref int value, 
    int step = 1, int stepFast = 100, 
    ImGuiInputTextFlags flags = ImGuiInputTextFlags.None)
```

Create an integer input field.

**Example:**
```csharp
private int _level = 1;
private int _gold = 1000;

void DrawUI()
{
    // Basic int input
    ImGui.InputInt("Level", ref _level);
    
    // With step buttons (±10, ±100 on fast)
    ImGui.InputInt("Gold", ref _gold, 10, 100);
}
```

---

### ImGuiInputTextFlags

```csharp
[Flags]
public enum ImGuiInputTextFlags
{
    None = 0,
    CharsDecimal = 1 << 0,         // Allow 0-9, ., +, -
    CharsHexadecimal = 1 << 1,     // Allow 0-9, a-f, A-F
    CharsUppercase = 1 << 2,       // Convert to uppercase
    CharsNoBlank = 1 << 3,         // Filter out spaces, tabs
    AutoSelectAll = 1 << 4,        // Select all text on focus
    EnterReturnsTrue = 1 << 5,     // Return true on Enter press
    ReadOnly = 1 << 14,            // Read-only mode
    Password = 1 << 15,            // Display as dots/asterisks
    // ... (more flags)
}
```

**Example:**
```csharp
private string _hexValue = "";
private string _password = "";

void DrawUI()
{
    // Hexadecimal only
    ImGui.InputText("Hex", ref _hexValue, 32, 
        ImGuiInputTextFlags.CharsHexadecimal | 
        ImGuiInputTextFlags.CharsUppercase);
    
    // Password field
    ImGui.InputText("Password", ref _password, 128, 
        ImGuiInputTextFlags.Password);
}
```

---

## Sliders

### SliderFloat()

```csharp
public static bool SliderFloat(string label, ref float value, 
    float min, float max, string format = "%.3f", 
    ImGuiSliderFlags flags = ImGuiSliderFlags.None)
```

Create a float slider.

**Example:**
```csharp
private float _volume = 0.5f;
private float _brightness = 1.0f;

void DrawUI()
{
    // Volume slider (0 to 1)
    if (ImGui.SliderFloat("Volume", ref _volume, 0.0f, 1.0f))
    {
        SetVolume(_volume);
    }
    
    // Brightness with percentage display
    ImGui.SliderFloat("Brightness", ref _brightness, 0.0f, 2.0f, "%.0f%%");
}
```

---

### SliderInt()

```csharp
public static bool SliderInt(string label, ref int value, 
    int min, int max, string format = "%d", 
    ImGuiSliderFlags flags = ImGuiSliderFlags.None)
```

Create an integer slider.

**Example:**
```csharp
private int _quality = 2;
private int _teamSize = 4;

void DrawUI()
{
    // Quality setting (0-4)
    ImGui.SliderInt("Quality", ref _quality, 0, 4);
    
    // Team size (1-10)
    ImGui.SliderInt("Team Size", ref _teamSize, 1, 10);
}
```

---

## Trees & Headers

### TreeNode() / TreePop()

```csharp
public static bool TreeNode(string label)
public static bool TreeNodeEx(string label, ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.None)
public static void TreePop()
```

Create expandable tree nodes. Must call `TreePop()` if `TreeNode()` returns true.

**Example:**
```csharp
if (ImGui.TreeNode("Settings"))
{
    ImGui.Text("Setting 1");
    ImGui.Text("Setting 2");
    
    if (ImGui.TreeNode("Advanced"))
    {
        ImGui.Text("Advanced setting");
        ImGui.TreePop();
    }
    
    ImGui.TreePop();
}
```

---

### CollapsingHeader()

```csharp
public static bool CollapsingHeader(string label, 
    ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.None)
```

Create a collapsing header (doesn't require TreePop).

**Example:**
```csharp
if (ImGui.CollapsingHeader("Player Stats"))
{
    ImGui.Text($"Health: {health}");
    ImGui.Text($"Mana: {mana}");
    ImGui.Text($"Level: {level}");
}

if (ImGui.CollapsingHeader("Inventory"))
{
    // Inventory items...
}
```

---

### ImGuiTreeNodeFlags

```csharp
[Flags]
public enum ImGuiTreeNodeFlags
{
    None = 0,
    Selected = 1 << 0,          // Highlighted
    Framed = 1 << 1,            // With frame background
    DefaultOpen = 1 << 5,       // Open by default
    OpenOnDoubleClick = 1 << 6, // Open on double-click
    OpenOnArrow = 1 << 7,       // Open only on arrow click
    Leaf = 1 << 8,              // No collapsing, no arrow
    Bullet = 1 << 9,            // Display bullet instead of arrow
    // ...
}
```

**Example:**
```csharp
// Default open, with bullet
if (ImGui.TreeNodeEx("Important", 
    ImGuiTreeNodeFlags.DefaultOpen | 
    ImGuiTreeNodeFlags.Bullet))
{
    ImGui.Text("Important content");
    ImGui.TreePop();
}
```

---

## Selectables & Combos

### Selectable()

```csharp
public static bool Selectable(string label, bool selected = false, 
    ImGuiSelectableFlags flags = ImGuiSelectableFlags.None, 
    Vector2 size = default)
```

Create a selectable item. Returns `true` when clicked.

**Example:**
```csharp
private int _selectedIndex = 0;

void DrawUI()
{
    if (ImGui.Selectable("Option 1", _selectedIndex == 0))
        _selectedIndex = 0;
    
    if (ImGui.Selectable("Option 2", _selectedIndex == 1))
        _selectedIndex = 1;
    
    if (ImGui.Selectable("Option 3", _selectedIndex == 2))
        _selectedIndex = 2;
    
    ImGui.Text($"Selected: {_selectedIndex}");
}
```

---

### BeginCombo() / EndCombo()

```csharp
public static bool BeginCombo(string label, string previewValue, 
    ImGuiComboFlags flags = ImGuiComboFlags.None)
public static void EndCombo()
```

Create a combo box (dropdown menu).

**Example:**
```csharp
private string[] _items = { "Item 1", "Item 2", "Item 3", "Item 4" };
private int _currentItem = 0;

void DrawUI()
{
    if (ImGui.BeginCombo("Select Item", _items[_currentItem]))
    {
        for (int i = 0; i < _items.Length; i++)
        {
            bool isSelected = (_currentItem == i);
            
            if (ImGui.Selectable(_items[i], isSelected))
            {
                _currentItem = i;
            }
            
            // Set initial focus on selected item
            if (isSelected)
            {
                ImGui.SetItemDefaultFocus();
            }
        }
        ImGui.EndCombo();
    }
    
    ImGui.Text($"Selected: {_items[_currentItem]}");
}
```

---

## Menus

### Menu Bars

```csharp
public static bool BeginMainMenuBar()
public static void EndMainMenuBar()
public static bool BeginMenuBar()  // Within window (requires MenuBar flag)
public static void EndMenuBar()
```

Create menu bars at the top of the screen or within a window.

---

### BeginMenu() / EndMenu()

```csharp
public static bool BeginMenu(string label, bool enabled = true)
public static void EndMenu()
```

Create a menu. Must call `EndMenu()` if returns true.

---

### MenuItem()

```csharp
public static bool MenuItem(string label, string shortcut = null, 
    bool selected = false, bool enabled = true)
```

Create a menu item. Returns `true` when clicked.

**Example:**
```csharp
private bool _showStats = true;

void DrawUI()
{
    if (ImGui.BeginMainMenuBar())
    {
        if (ImGui.BeginMenu("File"))
        {
            if (ImGui.MenuItem("New", "Ctrl+N"))
            {
                CreateNew();
            }
            
            if (ImGui.MenuItem("Open", "Ctrl+O"))
            {
                OpenFile();
            }
            
            ImGui.Separator();
            
            if (ImGui.MenuItem("Exit"))
            {
                Exit();
            }
            
            ImGui.EndMenu();
        }
        
        if (ImGui.BeginMenu("View"))
        {
            ImGui.MenuItem("Show Stats", null, ref _showStats);
            ImGui.EndMenu();
        }
        
        ImGui.EndMainMenuBar();
    }
}
```

**Window Menu Bar Example:**
```csharp
if (ImGui.Begin("Window", ImGuiWindowFlags.MenuBar))
{
    if (ImGui.BeginMenuBar())
    {
        if (ImGui.BeginMenu("Options"))
        {
            ImGui.MenuItem("Setting 1");
            ImGui.MenuItem("Setting 2");
            ImGui.EndMenu();
        }
        ImGui.EndMenuBar();
    }
    
    // Window content...
}
ImGui.End();
```

---

## Layout

### Separator()

```csharp
public static void Separator()
```

Draw a horizontal line separator.

**Example:**
```csharp
ImGui.Text("Section 1");
ImGui.Separator();
ImGui.Text("Section 2");
```

---

### SameLine()

```csharp
public static void SameLine(float offsetFromStartX = 0, float spacing = -1)
```

Place next item on the same line.

**Example:**
```csharp
ImGui.Text("Label:");
ImGui.SameLine();
ImGui.Button("Button");

// With spacing
ImGui.Text("Left");
ImGui.SameLine(0, 20);  // 20px spacing
ImGui.Text("Right");
```

---

### Spacing & Dummy

```csharp
public static void Spacing()                    // Add vertical spacing
public static void Dummy(Vector2 size)          // Add invisible space
public static void Indent(float indentW = 0)    // Increase indentation
public static void Unindent(float indentW = 0)  // Decrease indentation
```

**Example:**
```csharp
ImGui.Text("Line 1");
ImGui.Spacing();
ImGui.Spacing();
ImGui.Text("Line 2 (with extra spacing)");

// Dummy space
ImGui.Dummy(new Vector2(0, 20));  // 20px vertical space

// Indentation
ImGui.Text("Not indented");
ImGui.Indent();
ImGui.Text("Indented");
ImGui.Unindent();
ImGui.Text("Not indented");
```

---

### SetNextItemWidth()

```csharp
public static void SetNextItemWidth(float itemWidth)
```

Set width for the next item.

**Example:**
```csharp
// Narrow input
ImGui.SetNextItemWidth(100);
ImGui.InputFloat("Value", ref value);

// Full width
ImGui.SetNextItemWidth(-1);
ImGui.InputText("Text", ref text, 256);
```

---

## Styling

### PushStyleColor() / PopStyleColor()

```csharp
public static void PushStyleColor(ImGuiCol idx, Vector4 color)
public static void PopStyleColor(int count = 1)
```

Temporarily change UI colors. Always pop after pushing!

**Example:**
```csharp
// Red button
ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1, 0, 0, 1));
ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1, 0.3f, 0.3f, 1));
ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.8f, 0, 0, 1));

if (ImGui.Button("Delete"))
{
    // Clicked
}

ImGui.PopStyleColor(3);  // Pop all 3 colors
```

---

### ImGuiCol

```csharp
public enum ImGuiCol
{
    Text = 0,
    TextDisabled = 1,
    WindowBg = 2,
    ChildBg = 3,
    PopupBg = 4,
    Border = 5,
    FrameBg = 7,
    FrameBgHovered = 8,
    FrameBgActive = 9,
    TitleBg = 10,
    TitleBgActive = 11,
    MenuBarBg = 13,
    Button = 21,
    ButtonHovered = 22,
    ButtonActive = 23,
    // ... (more colors)
}
```

**Example:**
```csharp
// Green success button
ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0, 0.7f, 0, 1));
ImGui.Button("Success");
ImGui.PopStyleColor();

// Transparent window background
ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0, 0, 0, 0.5f));
if (ImGui.Begin("Transparent"))
{
    // ...
}
ImGui.End();
ImGui.PopStyleColor();
```

---

## Tooltips

### SetTooltip()

```csharp
public static void SetTooltip(string text)
```

Set tooltip for the last item when hovered.

**Example:**
```csharp
ImGui.Button("Hover Me");
if (ImGui.IsItemHovered())
{
    ImGui.SetTooltip("This is a tooltip!");
}
```

---

### BeginTooltip() / EndTooltip()

```csharp
public static bool BeginTooltip()
public static void EndTooltip()
```

Create a custom tooltip with multiple elements.

**Example:**
```csharp
ImGui.Text("Item Name");
if (ImGui.IsItemHovered())
{
    if (ImGui.BeginTooltip())
    {
        ImGui.Text("Item Details");
        ImGui.Separator();
        ImGui.TextColored(new Vector4(1, 1, 0, 1), "Rare Item");
        ImGui.Text("Damage: 50");
        ImGui.Text("Speed: 1.5");
        ImGui.EndTooltip();
    }
}
```

---

## Popups

### BeginPopup() / EndPopup()

```csharp
public static bool BeginPopup(string id, ImGuiWindowFlags flags = ImGuiWindowFlags.None)
public static void EndPopup()
public static void OpenPopup(string id, int popupFlags = 0)
public static void CloseCurrentPopup()
```

Create modal popups and context menus.

**Example:**
```csharp
if (ImGui.Button("Open Popup"))
{
    ImGui.OpenPopup("MyPopup");
}

if (ImGui.BeginPopup("MyPopup"))
{
    ImGui.Text("This is a popup!");
    ImGui.Separator();
    
    if (ImGui.Button("OK"))
    {
        ImGui.CloseCurrentPopup();
    }
    
    ImGui.EndPopup();
}
```

---

### BeginPopupContextItem()

```csharp
public static bool BeginPopupContextItem(string id = null, int popupFlags = 1)
```

Create a context menu (right-click popup) for the last item.

**Example:**
```csharp
ImGui.Text("Right-click me");

if (ImGui.BeginPopupContextItem())
{
    if (ImGui.MenuItem("Copy"))
    {
        Copy();
    }
    
    if (ImGui.MenuItem("Delete"))
    {
        Delete();
    }
    
    ImGui.EndPopup();
}
```

---

## Utilities

### Item State

```csharp
public static bool IsItemClicked(int mouseButton = 0)
public static bool IsItemHovered(int flags = 0)
```

Query state of the last item.

**Example:**
```csharp
ImGui.Text("Click or hover me");

if (ImGui.IsItemClicked())
{
    Logger.Info("Text was clicked!");
}

if (ImGui.IsItemHovered())
{
    ImGui.SetTooltip("Hovering!");
}
```

---

### Clipboard

```csharp
public static void SetClipboardText(string text)
```

Copy text to clipboard.

**Example:**
```csharp
if (ImGui.Button("Copy to Clipboard"))
{
    ImGui.SetClipboardText("Hello World!");
}
```

---

### ID Stack

```csharp
public static void PushID(string id)
public static void PushID(int id)
public static void PopID()
```

Create unique IDs for widgets in loops.

**Example:**
```csharp
for (int i = 0; i < items.Length; i++)
{
    ImGui.PushID(i);
    
    if (ImGui.Button("Delete"))
    {
        DeleteItem(i);
    }
    
    ImGui.SameLine();
    ImGui.Text(items[i]);
    
    ImGui.PopID();
}
```

---

## Drawing (Overlay)

Draw shapes and text directly on screen overlay.

### Color Conversion

```csharp
public static uint ColorToU32(float r, float g, float b, float a = 1f)
public static uint ColorToU32(Vector4 color)
```

Convert RGBA to packed color format.

**Example:**
```csharp
uint red = ImGui.ColorToU32(1, 0, 0, 1);
uint greenTransparent = ImGui.ColorToU32(0, 1, 0, 0.5f);
uint blue = ImGui.ColorToU32(new Vector4(0, 0, 1, 1));
```

---

### Drawing Functions

```csharp
public static void DrawLine(Vector2 p1, Vector2 p2, uint color, float thickness = 1f)
public static void DrawRect(Vector2 min, Vector2 max, uint color, float thickness = 1f, float rounding = 0f)
public static void DrawRectFilled(Vector2 min, Vector2 max, uint color, float rounding = 0f)
public static void DrawCircle(Vector2 center, float radius, uint color, float thickness = 1f, int segments = 0)
public static void DrawCircleFilled(Vector2 center, float radius, uint color, int segments = 0)
public static void DrawText(Vector2 pos, uint color, string text)
```

**Example:**
```csharp
void DrawOverlay()
{
    uint red = ImGui.ColorToU32(1, 0, 0, 1);
    uint green = ImGui.ColorToU32(0, 1, 0, 0.5f);
    uint white = ImGui.ColorToU32(1, 1, 1, 1);
    
    // Draw line
    ImGui.DrawLine(
        new Vector2(100, 100), 
        new Vector2(200, 200), 
        red, 
        2f
    );
    
    // Draw rectangle
    ImGui.DrawRect(
        new Vector2(50, 50), 
        new Vector2(150, 100), 
        green, 
        1f
    );
    
    // Draw filled circle
    ImGui.DrawCircleFilled(
        new Vector2(300, 300), 
        50, 
        red
    );
    
    // Draw text
    ImGui.DrawText(
        new Vector2(10, 10), 
        white, 
        "Overlay Text"
    );
}
```

---

## Complete Examples

### Example 1: Basic UI Window

```csharp
using GameSDK;
using GameSDK.ModHost;
using System.Numerics;

[Mod("Author.BasicUI", "Basic UI Example", "1.0.0")]
public class BasicUIMod : ModBase
{
    private bool _enabled = true;
    private float _speed = 1.0f;
    private string _playerName = "Player";

    public override void OnLoad()
    {
        ImGuiManager.RegisterCallback("Basic UI", DrawUI);
    }

    private void DrawUI()
    {
        ImGui.SetNextWindowSize(new Vector2(300, 200), ImGuiCond.FirstUseEver);
        
        if (ImGui.Begin("Basic UI"))
        {
            ImGui.Text("Simple UI Example");
            ImGui.Separator();
            
            ImGui.Checkbox("Enabled", ref _enabled);
            ImGui.SliderFloat("Speed", ref _speed, 0f, 10f);
            ImGui.InputText("Name", ref _playerName, 64);
            
            if (ImGui.Button("Apply"))
            {
                Logger.Info($"Applied: {_playerName}, Speed: {_speed}, Enabled: {_enabled}");
            }
        }
        ImGui.End();
    }
}
```

---

### Example 2: Multi-Section UI

```csharp
using GameSDK;
using GameSDK.ModHost;
using System.Numerics;

[Mod("Author.MultiSection", "Multi-Section UI", "1.0.0")]
public class MultiSectionMod : ModBase
{
    private int _health = 100;
    private int _mana = 50;
    private bool _invincible = false;
    private string[] _weapons = { "Sword", "Bow", "Staff" };
    private int _currentWeapon = 0;

    public override void OnLoad()
    {
        ImGuiManager.RegisterCallback("Multi-Section", DrawUI);
    }

    private void DrawUI()
    {
        if (ImGui.Begin("Character Editor"))
        {
            // Stats Section
            if (ImGui.CollapsingHeader("Stats", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.InputInt("Health", ref _health, 1, 10);
                ImGui.InputInt("Mana", ref _mana, 1, 10);
                ImGui.Checkbox("Invincible", ref _invincible);
                
                if (ImGui.Button("Restore"))
                {
                    _health = 100;
                    _mana = 100;
                }
            }
            
            // Equipment Section
            if (ImGui.CollapsingHeader("Equipment"))
            {
                if (ImGui.BeginCombo("Weapon", _weapons[_currentWeapon]))
                {
                    for (int i = 0; i < _weapons.Length; i++)
                    {
                        if (ImGui.Selectable(_weapons[i], _currentWeapon == i))
                        {
                            _currentWeapon = i;
                            Logger.Info($"Weapon changed to: {_weapons[i]}");
                        }
                    }
                    ImGui.EndCombo();
                }
            }
            
            // Actions Section
            if (ImGui.CollapsingHeader("Actions"))
            {
                if (ImGui.Button("Save"))
                {
                    SaveSettings();
                }
                
                ImGui.SameLine();
                
                if (ImGui.Button("Load"))
                {
                    LoadSettings();
                }
            }
        }
        ImGui.End();
    }

    private void SaveSettings() { Logger.Info("Settings saved"); }
    private void LoadSettings() { Logger.Info("Settings loaded"); }
}
```

---

### Example 3: Advanced UI with Tables & Menus

```csharp
using GameSDK;
using GameSDK.ModHost;
using System.Numerics;

[Mod("Author.Advanced", "Advanced UI", "1.0.0")]
public class AdvancedMod : ModBase
{
    private bool _showStats = true;
    private bool _showLog = true;
    private List<string> _logMessages = new List<string>();

    public override void OnLoad()
    {
        ImGuiManager.RegisterCallback("Advanced", DrawUI);
        _logMessages.Add("Mod loaded");
    }

    private void DrawUI()
    {
        // Main menu bar
        if (ImGui.BeginMainMenuBar())
        {
            if (ImGui.BeginMenu("View"))
            {
                ImGui.MenuItem("Stats", null, ref _showStats);
                ImGui.MenuItem("Log", null, ref _showLog);
                ImGui.EndMenu();
            }
            
            if (ImGui.BeginMenu("Actions"))
            {
                if (ImGui.MenuItem("Clear Log"))
                {
                    _logMessages.Clear();
                }
                
                ImGui.Separator();
                
                if (ImGui.MenuItem("Exit"))
                {
                    _showStats = false;
                    _showLog = false;
                }
                
                ImGui.EndMenu();
            }
            
            ImGui.EndMainMenuBar();
        }
        
        // Stats window
        if (_showStats)
        {
            DrawStatsWindow();
        }
        
        // Log window
        if (_showLog)
        {
            DrawLogWindow();
        }
    }

    private void DrawStatsWindow()
    {
        ImGui.SetNextWindowSize(new Vector2(300, 200), ImGuiCond.FirstUseEver);
        
        if (ImGui.Begin("Stats", ref _showStats))
        {
            ImGui.TextColored(new Vector4(0, 1, 1, 1), "Game Statistics");
            ImGui.Separator();
            
            ImGui.BulletText($"FPS: 60");
            ImGui.BulletText($"Frame: {Time.frameCount}");
            ImGui.BulletText($"Time: {Time.time:F2}");
            
            ImGui.Spacing();
            
            if (ImGui.Button("Log Message"))
            {
                _logMessages.Add($"Message at {Time.time:F2}");
            }
        }
        ImGui.End();
    }

    private void DrawLogWindow()
    {
        ImGui.SetNextWindowSize(new Vector2(400, 300), ImGuiCond.FirstUseEver);
        
        if (ImGui.Begin("Log", ref _showLog))
        {
            if (ImGui.Button("Clear"))
            {
                _logMessages.Clear();
            }
            
            ImGui.Separator();
            
            if (ImGui.BeginChild("LogScroll"))
            {
                for (int i = 0; i < _logMessages.Count; i++)
                {
                    ImGui.PushID(i);
                    
                    ImGui.Text($"[{i}] {_logMessages[i]}");
                    
                    if (ImGui.BeginPopupContextItem())
                    {
                        if (ImGui.MenuItem("Copy"))
                        {
                            ImGui.SetClipboardText(_logMessages[i]);
                        }
                        
                        if (ImGui.MenuItem("Remove"))
                        {
                            _logMessages.RemoveAt(i);
                        }
                        
                        ImGui.EndPopup();
                    }
                    
                    ImGui.PopID();
                }
            }
            ImGui.EndChild();
        }
        ImGui.End();
    }
}
```

---

### Example 4: Overlay HUD

```csharp
using GameSDK;
using GameSDK.ModHost;
using System.Numerics;

[Mod("Author.OverlayHUD", "Overlay HUD", "1.0.0")]
public class OverlayHUDMod : ModBase
{
    public override void OnLoad()
    {
        ImGuiManager.RegisterCallback("Overlay", DrawOverlay, ImGuiPriority.Overlay);
    }

    private void DrawOverlay()
    {
        // Top-right overlay (no decoration, transparent)
        ImGui.SetNextWindowPos(
            new Vector2(Screen.width - 250, 10), 
            ImGuiCond.Always
        );
        
        ImGui.SetNextWindowSize(new Vector2(240, 0), ImGuiCond.Always);
        
        if (ImGui.Begin("HUD", 
            ImGuiWindowFlags.NoTitleBar | 
            ImGuiWindowFlags.NoResize | 
            ImGuiWindowFlags.NoMove | 
            ImGuiWindowFlags.NoScrollbar | 
            ImGuiWindowFlags.AlwaysAutoResize |
            ImGuiWindowFlags.NoBackground))
        {
            // Semi-transparent background
            ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0, 0, 0, 0.7f));
            
            if (ImGui.BeginChild("HUDContent", new Vector2(230, 0)))
            {
                ImGui.TextColored(new Vector4(1, 1, 0, 1), "=== HUD ===");
                ImGui.Separator();
                
                ImGui.Text($"FPS: 60");
                ImGui.Text($"Position: {GetPlayerPos()}");
                ImGui.Text($"Health: 100/100");
                
                ImGui.Spacing();
                ImGui.TextDisabled("Press F2 for input");
            }
            ImGui.EndChild();
            
            ImGui.PopStyleColor();
        }
        ImGui.End();
        
        // Draw shapes on overlay
        DrawShapes();
    }

    private void DrawShapes()
    {
        uint redColor = ImGui.ColorToU32(1, 0, 0, 1);
        uint greenColor = ImGui.ColorToU32(0, 1, 0, 0.5f);
        
        // Draw crosshair at center
        Vector2 center = new Vector2(Screen.width / 2, Screen.height / 2);
        
        ImGui.DrawLine(
            new Vector2(center.X - 10, center.Y),
            new Vector2(center.X + 10, center.Y),
            redColor, 2f
        );
        
        ImGui.DrawLine(
            new Vector2(center.X, center.Y - 10),
            new Vector2(center.X, center.Y + 10),
            redColor, 2f
        );
    }

    private string GetPlayerPos()
    {
        return "(0, 0, 0)";
    }
}
```

---

## Best Practices

### ✅ Do

- **Always pair Begin/End calls** - Unbalanced calls cause crashes
- **Check Begin() return value** - Skip rendering if window is collapsed
- **Use ImGuiCond** for initialization - Set size/position once, then allow user control
- **Use unique IDs in loops** - PushID/PopID for duplicate labels
- **Cache expensive data** - Don't recalculate every frame
- **Use CollapsingHeader** for organization - Group related settings
- **Test with input disabled** - UI should work without mouse capture

### ❌ Don't

- **Don't forget End() calls** - Always pair with Begin()
- **Don't render heavy UI every frame** - Throttle or cache
- **Don't use same label twice** - Causes ID conflicts (use ## suffix or PushID)
- **Don't modify state in draw code** - Keep UI and logic separate
- **Don't create too many windows** - Combine related UI

---

## ImGuiCond Values

```csharp
public enum ImGuiCond
{
    None = 0,           // No condition (always set)
    Always = 1 << 0,    // Always set (same as None)
    Once = 1 << 1,      // Set once (first call)
    FirstUseEver = 1 << 2,  // Set if no saved data
    Appearing = 1 << 3,     // Set when window appears
}
```

**Example:**
```csharp
// Set size only on first use (allows user to resize)
ImGui.SetNextWindowSize(new Vector2(400, 300), ImGuiCond.FirstUseEver);

// Always override position
ImGui.SetNextWindowPos(new Vector2(10, 10), ImGuiCond.Always);
```

---

## Troubleshooting

### UI not visible?

- Check `ImGuiManager.IsInitialized`
- Verify callback is registered
- Ensure Begin() is called and returns true
- Check window position (might be off-screen)

### Input not working?

- Press F2 to enable input capture
- Check `ImGuiManager.IsInputEnabled`

### Crashes?

- Ensure all Begin() calls have matching End()
- Check for null strings passed to ImGui functions
- Verify buffer sizes for InputText

### Performance issues?

- Reduce UI complexity (fewer windows/widgets)
- Throttle expensive operations (cache data)
- Use conditional rendering (don't draw hidden UI)

---

## See Also

- [ImGuiManager API](imgui-manager) - Callback management and initialization
- [ModBase](modbase) - Base class with OnGUI callback
- [Logger](logger) - Debugging UI issues
- [Hello World Example](../../Documentation/Examples/HelloWorld/) - Complete working example
- [Dear ImGui Documentation](https://github.com/ocornut/imgui) - Official ImGui docs

---

[← ImGuiManager](imgui-manager) | [Back to API Index]({{ '/api' | relative_url }})
