// ==============================
// DeobfuscationPanel - Live IL2CPP assembly/class browser & mapping editor
// ==============================
// Replaces the old dump.cs-based panel with a fully live runtime browser.
// Uses bridge enumeration exports to walk assemblies → images → classes
// at runtime and allows assigning friendly names with multi-layer signatures.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using GameSDK;
using GameSDK.Deobfuscation;
using GameSDK.ModHost;

namespace MDB.Explorer.ImGui
{
    // ===== Lightweight cached data for the browser =====

    internal class CachedAssembly
    {
        public IntPtr Ptr;
        public IntPtr ImagePtr;
        public string Name;
        public int ClassCount;
        public bool Expanded;
    }

    internal class CachedClass
    {
        public IntPtr Ptr;
        public string Name;
        public string Namespace;
        public string FullName;          // Namespace.Name or just Name
        public string FriendlyName;      // from mapping DB, or null
        public bool IsObfuscated;
    }

    internal class CachedMember
    {
        public IntPtr Ptr;
        public string Name;
        public string TypeName;           // Field/return type
        public string Extra;              // e.g. param count for methods
        public SymbolType Kind;
        public bool IsObfuscated;
        public string FriendlyName;       // from mapping DB, or null
    }

    /// <summary>
    /// ImGui panel: live IL2CPP assembly/class browser with deobfuscation mapping editor.
    /// </summary>
    public class DeobfuscationPanel
    {
        private const string LOG_TAG = "DeobfuscationPanel";

        // Assembly cache
        private List<CachedAssembly> _assemblies = new List<CachedAssembly>();
        private Dictionary<IntPtr, List<CachedClass>> _classCache = new Dictionary<IntPtr, List<CachedClass>>();
        private bool _assembliesLoaded;

        // Selection
        private CachedAssembly _selectedAssembly;
        private CachedClass _selectedClass;
        private IntPtr _selectedClassPtr;

        // Member lists for selected class
        private List<CachedMember> _fields = new List<CachedMember>();
        private List<CachedMember> _methods = new List<CachedMember>();
        private List<CachedMember> _properties = new List<CachedMember>();

        // UI state
        private bool _isVisible = true;
        private string _assemblyFilter = "";
        private string _classFilter = "";
        private string _memberFilter = "";
        private bool _showOnlyObfuscated = true;
        private bool _showOnlyMapped;

        // Mapping editor state
        private string _editFriendlyName = "";
        private string _editNotes = "";
        private int _editMemberIndex = -1;
        private SymbolType _editMemberKind = SymbolType.Type;

        // Signature state for selected class
        private string _structuralSig = "";
        private string _byteSigPreview = "";

        // Status
        private string _statusMsg = "";
        private float _statusTimer;

        public bool IsVisible { get => _isVisible; set => _isVisible = value; }
        public int AssemblyCount => _assemblies.Count;
        public int MappingCount => DeobfuscationHelper.MappingCount;

        // ==============================
        // Public API
        // ==============================

        /// <summary>
        /// Initialize the panel — call once after bridge is ready.
        /// No arguments required; everything is read live from runtime.
        /// </summary>
        public void Initialize()
        {
            // Assembly enumeration will happen lazily on first Render().
        }

        /// <summary>
        /// Main render entry-point (called each frame from ExplorerMod).
        /// </summary>
        public void Render()
        {
            if (!_assembliesLoaded)
            {
                LoadAssemblies();
            }

            // Toolbar
            DrawToolbar();

            ImGui.Separator();

            // Two-column layout: browser on left, details on right
            float panelWidth = ImGui.GetContentRegionAvail().X;
            float leftWidth = Math.Max(200f, panelWidth * 0.42f);

            // Left pane: assembly/class browser
            if (ImGui.BeginChild("##BrowserPane", new Vector2(leftWidth, -1), 1))
            {
                DrawAssemblyBrowser();
            }
            ImGui.EndChild();

            ImGui.SameLine();

            // Right pane: class detail + mapping editor
            if (ImGui.BeginChild("##DetailPane", new Vector2(0, -1), 1))
            {
                if (_selectedClass != null)
                    DrawClassDetail();
                else
                    ImGui.TextDisabled("Select a class from the left panel.");
            }
            ImGui.EndChild();
        }

        // ==============================
        // Assembly enumeration (one-shot, cached)
        // ==============================

        private void LoadAssemblies()
        {
            _assemblies.Clear();
            _classCache.Clear();

            try
            {
                int count = Il2CppBridge.mdb_get_assembly_count();
                for (int i = 0; i < count; i++)
                {
                    IntPtr asm = Il2CppBridge.mdb_get_assembly(i);
                    if (asm == IntPtr.Zero) continue;

                    IntPtr img = Il2CppBridge.mdb_assembly_get_image(asm);
                    if (img == IntPtr.Zero) continue;

                    string name = Il2CppBridge.GetImageName(img) ?? $"assembly_{i}";
                    int classCount = Il2CppBridge.mdb_image_get_class_count(img);

                    _assemblies.Add(new CachedAssembly
                    {
                        Ptr = asm,
                        ImagePtr = img,
                        Name = name,
                        ClassCount = classCount,
                        Expanded = false
                    });
                }

                _assemblies.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
                _assembliesLoaded = true;
                SetStatus($"Loaded {_assemblies.Count} assemblies");
            }
            catch (Exception ex)
            {
                ModLogger.LogInternal(LOG_TAG, $"[ERROR] LoadAssemblies: {ex.Message}");
                _assembliesLoaded = true; // Don't retry forever
            }
        }

        private List<CachedClass> GetClassesForImage(CachedAssembly asm)
        {
            if (_classCache.TryGetValue(asm.ImagePtr, out var cached))
                return cached;

            var list = new List<CachedClass>();
            try
            {
                for (int i = 0; i < asm.ClassCount; i++)
                {
                    IntPtr klass = Il2CppBridge.mdb_image_get_class(asm.ImagePtr, i);
                    if (klass == IntPtr.Zero) continue;

                    string name = MarshalStr(Il2CppBridge.mdb_class_get_name(klass));
                    string ns = MarshalStr(Il2CppBridge.mdb_class_get_namespace(klass));
                    bool obf = SignatureGenerator.IsObfuscatedName(name);

                    string fullName = string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";

                    // Check mapping DB for friendly name
                    string friendly = null;
                    var mapping = DeobfuscationHelper.GetDatabase()?.GetByObfuscatedName(name);
                    if (mapping != null) friendly = mapping.FriendlyName;

                    list.Add(new CachedClass
                    {
                        Ptr = klass,
                        Name = name,
                        Namespace = ns,
                        FullName = fullName,
                        FriendlyName = friendly,
                        IsObfuscated = obf
                    });
                }

                list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                ModLogger.LogInternal(LOG_TAG, $"[ERROR] GetClasses: {ex.Message}");
            }

            _classCache[asm.ImagePtr] = list;
            return list;
        }

        // ==============================
        // Member enumeration (on-demand per class)
        // ==============================

        private void LoadMembers(IntPtr klass)
        {
            _fields.Clear();
            _methods.Clear();
            _properties.Clear();
            _editMemberIndex = -1;

            if (klass == IntPtr.Zero) return;

            string parentName = _selectedClass?.Name ?? "";

            // Fields
            int fc = Il2CppBridge.mdb_class_get_field_count(klass);
            for (int i = 0; i < fc; i++)
            {
                IntPtr field = Il2CppBridge.mdb_class_get_field_by_index(klass, i);
                if (field == IntPtr.Zero) continue;

                string fName = MarshalStr(Il2CppBridge.mdb_field_get_name(field));
                IntPtr ft = Il2CppBridge.mdb_field_get_type(field);
                string typeName = ft != IntPtr.Zero ? MarshalStr(Il2CppBridge.mdb_type_get_name(ft)) : "?";
                bool isStatic = Il2CppBridge.mdb_field_is_static(field);

                string friendly = null;
                var mapping = DeobfuscationHelper.GetDatabase()?.GetByObfuscatedName($"{parentName}.{fName}");
                if (mapping != null) friendly = mapping.FriendlyName;

                _fields.Add(new CachedMember
                {
                    Ptr = field,
                    Name = fName,
                    TypeName = typeName,
                    Extra = isStatic ? "static" : "",
                    Kind = SymbolType.Field,
                    IsObfuscated = SignatureGenerator.IsObfuscatedName(fName),
                    FriendlyName = friendly
                });
            }

            // Methods
            int mc = Il2CppBridge.mdb_class_get_method_count(klass);
            for (int i = 0; i < mc; i++)
            {
                IntPtr method = Il2CppBridge.mdb_class_get_method_by_index(klass, i);
                if (method == IntPtr.Zero) continue;

                string mName = MarshalStr(Il2CppBridge.mdb_method_get_name_str(method));
                IntPtr ret = Il2CppBridge.mdb_method_get_return_type(method);
                string retName = ret != IntPtr.Zero ? MarshalStr(Il2CppBridge.mdb_type_get_name(ret)) : "void";
                int paramCount = Il2CppBridge.mdb_method_get_param_count(method);

                string friendly = null;
                var mapping = DeobfuscationHelper.GetDatabase()?.GetByObfuscatedName($"{parentName}.{mName}");
                if (mapping != null) friendly = mapping.FriendlyName;

                _methods.Add(new CachedMember
                {
                    Ptr = method,
                    Name = mName,
                    TypeName = retName,
                    Extra = $"({paramCount} params)",
                    Kind = SymbolType.Method,
                    IsObfuscated = SignatureGenerator.IsObfuscatedName(mName),
                    FriendlyName = friendly
                });
            }

            // Properties
            int pc = Il2CppBridge.mdb_class_get_property_count(klass);
            for (int i = 0; i < pc; i++)
            {
                IntPtr prop = Il2CppBridge.mdb_class_get_property_by_index(klass, i);
                if (prop == IntPtr.Zero) continue;

                string pName = MarshalStr(Il2CppBridge.mdb_property_get_name(prop));
                IntPtr getter = Il2CppBridge.mdb_property_get_get_method(prop);
                IntPtr setter = Il2CppBridge.mdb_property_get_set_method(prop);

                string propType = "?";
                if (getter != IntPtr.Zero)
                {
                    IntPtr retType = Il2CppBridge.mdb_method_get_return_type(getter);
                    if (retType != IntPtr.Zero)
                        propType = MarshalStr(Il2CppBridge.mdb_type_get_name(retType));
                }

                string access = "";
                if (getter != IntPtr.Zero) access += "get";
                if (setter != IntPtr.Zero) access += (access.Length > 0 ? "/set" : "set");

                string friendly = null;
                var mapping = DeobfuscationHelper.GetDatabase()?.GetByObfuscatedName($"{parentName}.{pName}");
                if (mapping != null) friendly = mapping.FriendlyName;

                _properties.Add(new CachedMember
                {
                    Ptr = prop,
                    Name = pName,
                    TypeName = propType,
                    Extra = $"[{access}]",
                    Kind = SymbolType.Property,
                    IsObfuscated = SignatureGenerator.IsObfuscatedName(pName),
                    FriendlyName = friendly
                });
            }
        }

        // ==============================
        // Draw: Toolbar
        // ==============================

        private void DrawToolbar()
        {
            // Search / filter
            ImGui.SetNextItemWidth(180);
            ImGui.InputTextWithHint("##classFilter", "Filter classes...", ref _classFilter, 256);
            ImGui.SameLine();

            ImGui.Checkbox("Obfuscated only", ref _showOnlyObfuscated);
            ImGui.SameLine();
            ImGui.Checkbox("Mapped only", ref _showOnlyMapped);

            ImGui.SameLine(ImGui.GetContentRegionAvail().X - 80);
            if (ImGui.Button("Refresh"))
            {
                _assembliesLoaded = false;
                _classCache.Clear();
                _selectedClass = null;
                _selectedClassPtr = IntPtr.Zero;
            }

            // Status bar
            if (!string.IsNullOrEmpty(_statusMsg))
            {
                ImGui.TextColored(new Vector4(0.5f, 1f, 0.5f, 1f), _statusMsg);
            }
        }

        // ==============================
        // Draw: Assembly / class browser (left pane)
        // ==============================

        private void DrawAssemblyBrowser()
        {
            foreach (var asm in _assemblies)
            {
                string label = $"{asm.Name} ({asm.ClassCount})";
                bool opened = ImGui.TreeNodeEx($"{label}##{asm.Ptr}",
                    ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth);

                if (opened)
                {
                    var classes = GetClassesForImage(asm);
                    int shown = 0;

                    foreach (var cls in classes)
                    {
                        if (!ClassMatchesFilter(cls)) continue;

                        shown++;
                        bool isSelected = _selectedClassPtr == cls.Ptr;

                        // Color: green if mapped, yellow if obfuscated, white otherwise
                        Vector4 color;
                        if (cls.FriendlyName != null)
                            color = new Vector4(0.4f, 1f, 0.4f, 1f);
                        else if (cls.IsObfuscated)
                            color = new Vector4(1f, 0.85f, 0.3f, 1f);
                        else
                            color = new Vector4(0.9f, 0.9f, 0.9f, 1f);

                        ImGui.PushStyleColor(ImGuiCol.Text, color);

                        string displayName = cls.FriendlyName != null
                            ? $"{cls.FriendlyName} [{cls.Name}]"
                            : cls.FullName;

                        if (ImGui.Selectable($"{displayName}##{cls.Ptr}", isSelected))
                        {
                            SelectClass(asm, cls);
                        }

                        ImGui.PopStyleColor();
                    }

                    if (shown == 0)
                    {
                        ImGui.TextDisabled("  (no matching classes)");
                    }

                    ImGui.TreePop();
                }
            }
        }

        private bool ClassMatchesFilter(CachedClass cls)
        {
            if (_showOnlyObfuscated && !cls.IsObfuscated) return false;
            if (_showOnlyMapped && cls.FriendlyName == null) return false;

            if (!string.IsNullOrEmpty(_classFilter))
            {
                bool match = cls.Name.IndexOf(_classFilter, StringComparison.OrdinalIgnoreCase) >= 0
                    || cls.FullName.IndexOf(_classFilter, StringComparison.OrdinalIgnoreCase) >= 0
                    || (cls.FriendlyName != null && cls.FriendlyName.IndexOf(_classFilter, StringComparison.OrdinalIgnoreCase) >= 0);
                if (!match) return false;
            }

            return true;
        }

        private void SelectClass(CachedAssembly asm, CachedClass cls)
        {
            _selectedAssembly = asm;
            _selectedClass = cls;
            _selectedClassPtr = cls.Ptr;

            // Load members
            LoadMembers(cls.Ptr);

            // Pre-fill editor
            _editFriendlyName = cls.FriendlyName ?? "";
            _editNotes = "";

            // Generate structural signature
            _structuralSig = SignatureGenerator.GenerateTypeSignature(cls.Ptr);

            // Preview first method byte sig
            _byteSigPreview = "";
            if (_methods.Count > 0)
            {
                _byteSigPreview = SignatureGenerator.GenerateByteSignature(_methods[0].Ptr);
            }
        }

        // ==============================
        // Draw: Class detail + mapping editor (right pane)
        // ==============================

        private void DrawClassDetail()
        {
            var cls = _selectedClass;

            // Header
            ImGui.TextColored(new Vector4(1f, 0.9f, 0.4f, 1f), cls.FullName);
            if (!string.IsNullOrEmpty(cls.FriendlyName))
            {
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(0.4f, 1f, 0.4f, 1f), $"  →  {cls.FriendlyName}");
            }
            ImGui.Separator();

            // === Mapping editor ===
            if (ImGui.CollapsingHeader("Mapping Editor", ImGuiTreeNodeFlags.DefaultOpen))
            {
                DrawMappingEditor();
            }

            // === Signatures ===
            if (ImGui.CollapsingHeader("Signatures"))
            {
                DrawSignatureSection();
            }

            ImGui.Separator();

            // === Members ===
            DrawMemberTabs();
        }

        private void DrawMappingEditor()
        {
            ImGui.Text("Friendly Name:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(-1);
            ImGui.InputText("##friendlyName", ref _editFriendlyName, 256);

            ImGui.Text("Notes:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(-1);
            ImGui.InputText("##notes", ref _editNotes, 512);

            if (ImGui.Button("Save Mapping"))
            {
                SaveCurrentMapping();
            }
            ImGui.SameLine();
            if (ImGui.Button("Remove Mapping"))
            {
                RemoveCurrentMapping();
            }
            ImGui.SameLine();
            if (ImGui.Button("Generate All Signatures"))
            {
                GenerateAllSignatures();
            }
        }

        private void DrawSignatureSection()
        {
            // Layer A: Structural
            ImGui.TextColored(new Vector4(0.6f, 0.8f, 1f, 1f), "Layer A: Structural Fingerprint");
            if (string.IsNullOrEmpty(_structuralSig))
            {
                ImGui.TextDisabled("  (not generated)");
            }
            else
            {
                ImGui.TextWrapped($"  {_structuralSig}");
            }

            ImGui.Spacing();

            // Layer B: Byte pattern (first method preview)
            ImGui.TextColored(new Vector4(0.6f, 0.8f, 1f, 1f), "Layer B: Byte Signature (first method)");
            if (string.IsNullOrEmpty(_byteSigPreview))
            {
                ImGui.TextDisabled("  (no methods or not available)");
            }
            else
            {
                ImGui.TextWrapped($"  {_byteSigPreview}");
            }

            ImGui.Spacing();

            // Layer C: RVA (first method)
            ImGui.TextColored(new Vector4(0.6f, 0.8f, 1f, 1f), "Layer C: RVA (first method)");
            if (_methods.Count > 0)
            {
                string rva = SignatureGenerator.GetMethodRva(_methods[0].Ptr);
                ImGui.Text($"  {(string.IsNullOrEmpty(rva) ? "(not available)" : rva)}");
            }
            else
            {
                ImGui.TextDisabled("  (no methods)");
            }
        }

        // ==============================
        // Draw: Member tabs (Fields / Methods / Properties)
        // ==============================

        private void DrawMemberTabs()
        {
            if (ImGui.BeginTabBar("##memberTabs"))
            {
                if (ImGui.BeginTabItem($"Fields ({_fields.Count})"))
                {
                    DrawMemberList(_fields, SymbolType.Field);
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem($"Methods ({_methods.Count})"))
                {
                    DrawMemberList(_methods, SymbolType.Method);
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem($"Properties ({_properties.Count})"))
                {
                    DrawMemberList(_properties, SymbolType.Property);
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }
        }

        private void DrawMemberList(List<CachedMember> members, SymbolType kind)
        {
            // Filter
            ImGui.SetNextItemWidth(180);
            ImGui.InputTextWithHint("##memberFilter", "Filter...", ref _memberFilter, 256);

            if (ImGui.BeginChild($"##memberList_{kind}", new Vector2(0, -1)))
            {
                for (int i = 0; i < members.Count; i++)
                {
                    var m = members[i];

                    if (!string.IsNullOrEmpty(_memberFilter) &&
                        m.Name.IndexOf(_memberFilter, StringComparison.OrdinalIgnoreCase) < 0 &&
                        (m.FriendlyName == null || m.FriendlyName.IndexOf(_memberFilter, StringComparison.OrdinalIgnoreCase) < 0))
                        continue;

                    // Color: green if mapped, yellow if obfuscated
                    Vector4 nameColor;
                    if (m.FriendlyName != null)
                        nameColor = new Vector4(0.4f, 1f, 0.4f, 1f);
                    else if (m.IsObfuscated)
                        nameColor = new Vector4(1f, 0.85f, 0.3f, 1f);
                    else
                        nameColor = new Vector4(0.85f, 0.85f, 0.85f, 1f);

                    string displayName = m.FriendlyName != null
                        ? $"{m.FriendlyName} [{m.Name}]"
                        : m.Name;

                    ImGui.PushStyleColor(ImGuiCol.Text, nameColor);
                    ImGui.Text(displayName);
                    ImGui.PopStyleColor();

                    ImGui.SameLine(ImGui.GetContentRegionAvail().X * 0.55f);
                    ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.8f, 1f), m.TypeName);
                    ImGui.SameLine();
                    ImGui.TextDisabled(m.Extra);

                    // Context menu for renaming
                    if (ImGui.BeginPopupContextItem($"##ctx_{kind}_{i}"))
                    {
                        ImGui.Text($"Rename: {m.Name}");
                        ImGui.Separator();

                        if (_editMemberIndex != i || _editMemberKind != kind)
                        {
                            _editMemberIndex = i;
                            _editMemberKind = kind;
                            _editFriendlyName = m.FriendlyName ?? "";
                        }

                        ImGui.SetNextItemWidth(200);
                        ImGui.InputText("##memberRename", ref _editFriendlyName, 256);

                        if (ImGui.Button("Save"))
                        {
                            SaveMemberMapping(m, _editFriendlyName);
                            m.FriendlyName = _editFriendlyName;
                            ImGui.CloseCurrentPopup();
                        }
                        ImGui.SameLine();
                        if (m.FriendlyName != null && ImGui.Button("Remove"))
                        {
                            string obfKey = $"{_selectedClass?.Name}.{m.Name}";
                            DeobfuscationHelper.RemoveMapping(obfKey);
                            m.FriendlyName = null;
                            ImGui.CloseCurrentPopup();
                        }
                        ImGui.SameLine();
                        if (ImGui.Button("Cancel"))
                        {
                            ImGui.CloseCurrentPopup();
                        }

                        ImGui.EndPopup();
                    }
                }
            }
            ImGui.EndChild();
        }

        // ==============================
        // Mapping operations
        // ==============================

        private void SaveCurrentMapping()
        {
            if (_selectedClass == null || string.IsNullOrWhiteSpace(_editFriendlyName)) return;

            var mapping = new SymbolMapping
            {
                ObfuscatedName = _selectedClass.Name,
                FriendlyName = _editFriendlyName.Trim(),
                SymbolType = SymbolType.Type,
                Assembly = _selectedAssembly?.Name ?? "",
                Namespace = _selectedClass.Namespace,
                StructuralSignature = _structuralSig,
                Notes = _editNotes,
                Confidence = 1.0f,
                LastUpdated = DateTime.UtcNow
            };

            // Generate byte signature from first method if available
            if (_methods.Count > 0)
            {
                mapping.ByteSignature = SignatureGenerator.GenerateByteSignature(_methods[0].Ptr);
                mapping.Rva = SignatureGenerator.GetMethodRva(_methods[0].Ptr);
            }

            DeobfuscationHelper.SetMapping(mapping);

            // Update cached friendly name
            _selectedClass.FriendlyName = _editFriendlyName.Trim();
            SetStatus($"Saved: {_selectedClass.Name} → {_editFriendlyName}");
        }

        private void RemoveCurrentMapping()
        {
            if (_selectedClass == null) return;

            if (DeobfuscationHelper.RemoveMapping(_selectedClass.Name))
            {
                _selectedClass.FriendlyName = null;
                _editFriendlyName = "";
                SetStatus($"Removed mapping for {_selectedClass.Name}");
            }
        }

        private void GenerateAllSignatures()
        {
            if (_selectedClass == null) return;

            _structuralSig = SignatureGenerator.GenerateTypeSignature(_selectedClass.Ptr);

            if (_methods.Count > 0)
            {
                _byteSigPreview = SignatureGenerator.GenerateByteSignature(_methods[0].Ptr);
            }

            SetStatus("Signatures regenerated");
        }

        private void SaveMemberMapping(CachedMember member, string friendlyName)
        {
            if (_selectedClass == null || member == null) return;

            string obfKey = $"{_selectedClass.Name}.{member.Name}";

            if (string.IsNullOrWhiteSpace(friendlyName))
            {
                DeobfuscationHelper.RemoveMapping(obfKey);
                return;
            }

            var mapping = new SymbolMapping
            {
                ObfuscatedName = obfKey,
                FriendlyName = friendlyName.Trim(),
                SymbolType = member.Kind,
                Assembly = _selectedAssembly?.Name ?? "",
                Namespace = _selectedClass.Namespace,
                ParentType = _selectedClass.Name,
                Confidence = 1.0f,
                LastUpdated = DateTime.UtcNow
            };

            // Generate signature based on member type
            if (member.Kind == SymbolType.Field)
                mapping.StructuralSignature = SignatureGenerator.GenerateFieldSignature(_selectedClass.Ptr, member.Ptr);
            else if (member.Kind == SymbolType.Method)
            {
                mapping.StructuralSignature = SignatureGenerator.GenerateMethodSignature(_selectedClass.Ptr, member.Ptr);
                mapping.ByteSignature = SignatureGenerator.GenerateByteSignature(member.Ptr);
                mapping.Rva = SignatureGenerator.GetMethodRva(member.Ptr);
            }
            else if (member.Kind == SymbolType.Property)
                mapping.StructuralSignature = SignatureGenerator.GeneratePropertySignature(_selectedClass.Ptr, member.Ptr);

            DeobfuscationHelper.SetMapping(mapping);
            SetStatus($"Saved: {obfKey} → {friendlyName}");
        }

        // ==============================
        // Helpers
        // ==============================

        private void SetStatus(string msg)
        {
            _statusMsg = msg;
            _statusTimer = 5.0f;
            ModLogger.LogInternal(LOG_TAG, $"[INFO] {msg}");
        }

        private static string MarshalStr(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero) return null;
            return Marshal.PtrToStringAnsi(ptr);
        }
    }
}
