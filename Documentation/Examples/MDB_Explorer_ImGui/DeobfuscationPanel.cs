// ==============================
// DeobfuscationPanel - ImGui UI for the deobfuscation toolset
// ==============================
// Provides:
// - Type browser with fast search
// - Mapping editor for setting friendly names
// - Signature generation and verification
// - JSON import/export

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using GameSDK.Deobfuscation;
using GameSDK.ModHost;

namespace MDB.Explorer.ImGui
{
    /// <summary>
    /// ImGui panel for deobfuscation mapping management.
    /// </summary>
    public class DeobfuscationPanel
    {
        // State
        private bool _isVisible = true;
        private DumpIndex _dumpIndex;
        private MappingDatabase _mappingDb;
        private string _dumpPath;
        private string _mappingsPath;
        
        // UI State
        private string _searchQuery = "";
        private string _lastSearchQuery = "";
        private List<TypeIndex> _filteredTypes = new List<TypeIndex>();
        private int _selectedTypeIndex = -1;
        private TypeDefinition _selectedTypeDetails;
        private string _namespaceFilter = "";
        private bool _showOnlyObfuscated = true;
        private bool _showOnlyMapped = false;
        
        // Member search/filter
        private string _memberSearchQuery = "";
        
        // Mapping editor state
        private string _editFriendlyName = "";
        private string _editNamespace = "";
        private string _editNotes = "";
        private int _selectedMemberIndex = -1;
        private MemberType _selectedMemberType = MemberType.None;
        
        // Status
        private string _statusMessage = "";
        private float _statusMessageTime = 0;
        
        public bool IsVisible
        {
            get => _isVisible;
            set => _isVisible = value;
        }
        
        /// <summary>
        /// Gets the number of types indexed from the dump file.
        /// </summary>
        public int TypeCount => _dumpIndex?.Count ?? 0;
        
        /// <summary>
        /// Gets the number of mappings in the database.
        /// </summary>
        public int MappingCount => _mappingDb?.Count ?? 0;
        
        public DeobfuscationPanel()
        {
        }
        
        /// <summary>
        /// Initializes the panel with dump and mappings paths.
        /// </summary>
        public void Initialize(string dumpPath, string mappingsPath = null)
        {
            _dumpPath = dumpPath;
            _mappingsPath = mappingsPath ?? Path.Combine(Path.GetDirectoryName(dumpPath), "mappings.json");
            
            // Load dump index
            if (File.Exists(dumpPath))
            {
                _dumpIndex = new DumpIndex();
                _dumpIndex.BuildIndex(dumpPath);
                SetStatus($"Indexed {_dumpIndex.Count} types");
            }
            else
            {
                SetStatus($"Dump file not found: {dumpPath}");
            }
            
            // Load mappings
            _mappingDb = new MappingDatabase(_mappingsPath);
            if (File.Exists(_mappingsPath))
            {
                _mappingDb.Load();
                SetStatus($"Loaded {_mappingDb.Count} mappings");
                
                // Apply mappings to dump index
                if (_dumpIndex != null)
                {
                    _dumpIndex.ApplyMappings(_mappingDb);
                }
            }
            
            RefreshFilteredTypes();
        }
        
        /// <summary>
        /// Renders the deobfuscation panel.
        /// </summary>
        public void Render()
        {
            if (!_isVisible) return;
            
            // Note: We don't create our own window here - ExplorerMod already created one for us
            // Just render the content
            
            RenderToolbar();
            RenderFilters();
            
            // Split view: Type list on left, details on right
            float leftPaneWidth = 350;
            
            if (ImGui.BeginChild("TypeList", new Vector2(leftPaneWidth, -25), 1))
            {
                RenderTypeList();
            }
            ImGui.EndChild();
            
            ImGui.SameLine();
            
            if (ImGui.BeginChild("Details", new Vector2(0, -25), 1))
            {
                RenderDetails();
            }
            ImGui.EndChild();
            
            // Status bar
            RenderStatusBar();
        }
        
        private void RenderToolbar()
        {
            // Simple button toolbar instead of menu bar
            if (ImGui.Button("Save"))
            {
                SaveMappings();
            }
            ImGui.SameLine();
            if (ImGui.Button("Reload"))
            {
                ReloadMappings();
            }
            ImGui.SameLine();
            if (ImGui.Button("Export JSON"))
            {
                ExportMappings();
            }
            ImGui.SameLine();
            ImGui.TextDisabled("|");
            ImGui.SameLine();
            if (ImGui.Button("Verify Signatures"))
            {
                VerifyAllSignatures();
            }
            ImGui.SameLine();
            if (ImGui.Button("Fix Namespaces"))
            {
                FixMappingNamespaces();
            }
            
            ImGui.Separator();
        }
        
        private void RenderFilters()
        {
            // Search box
            ImGui.SetNextItemWidth(200);
            if (ImGui.InputText("Search", ref _searchQuery, 256))
            {
                if (_searchQuery != _lastSearchQuery)
                {
                    _lastSearchQuery = _searchQuery;
                    RefreshFilteredTypes();
                }
            }
            
            ImGui.SameLine();
            
            if (ImGui.Checkbox("Obfuscated Only", ref _showOnlyObfuscated))
            {
                RefreshFilteredTypes();
            }
            
            ImGui.SameLine();
            
            if (ImGui.Checkbox("Mapped Only", ref _showOnlyMapped))
            {
                RefreshFilteredTypes();
            }
            
            ImGui.SameLine();
            ImGui.Text($"| {_filteredTypes.Count} types");
            
            ImGui.Separator();
        }
        
        private void RenderTypeList()
        {
            for (int i = 0; i < Math.Min(_filteredTypes.Count, 500); i++) // Limit for performance
            {
                var type = _filteredTypes[i];
                
                bool isSelected = (i == _selectedTypeIndex);
                string displayName = type.FriendlyName != null 
                    ? $"{type.FriendlyName} [{type.Name}]" 
                    : type.Name;
                
                // Color based on mapping status
                if (type.FriendlyName != null)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 1.0f, 0.5f, 1.0f)); // Green for mapped
                }
                else if (type.IsObfuscated)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.7f, 0.3f, 1.0f)); // Orange for obfuscated
                }
                
                ImGui.PushID(i);
                if (ImGui.Selectable(displayName, isSelected))
                {
                    _selectedTypeIndex = i;
                    _selectedMemberIndex = -1;
                    _selectedMemberType = MemberType.None;
                    _memberSearchQuery = ""; // Clear member search when selecting new type
                    LoadTypeDetails(type);
                }
                
                // Right-click context menu for type
                if (ImGui.BeginPopupContextItem("type_ctx"))
                {
                    if (ImGui.MenuItem("Copy Name"))
                        ImGui.SetClipboardText(type.Name);
                    if (type.FriendlyName != null && ImGui.MenuItem("Copy Friendly Name"))
                        ImGui.SetClipboardText(type.FriendlyName);
                    if (type.Namespace != null && ImGui.MenuItem("Copy Namespace"))
                        ImGui.SetClipboardText(type.Namespace);
                    if (ImGui.MenuItem("Copy Full Name"))
                        ImGui.SetClipboardText($"{type.Namespace}.{type.Name}");
                    ImGui.EndPopup();
                }
                ImGui.PopID();
                
                if (type.FriendlyName != null || type.IsObfuscated)
                {
                    ImGui.PopStyleColor();
                }
                
                // Tooltip with namespace
                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip($"Namespace: {type.Namespace ?? "(global)"}\nKind: {type.Kind}\nBase: {type.BaseType ?? "none"}");
                }
            }
            
            if (_filteredTypes.Count > 500)
            {
                ImGui.Text($"... and {_filteredTypes.Count - 500} more (refine search)");
            }
        }
        
        private void RenderDetails()
        {
            if (_selectedTypeIndex < 0 || _selectedTypeIndex >= _filteredTypes.Count)
            {
                ImGui.Text("Select a type from the list");
                return;
            }
            
            var typeIndex = _filteredTypes[_selectedTypeIndex];
            
            // Header
            ImGui.Text($"Type: {typeIndex.Name}");
            if (typeIndex.FriendlyName != null)
            {
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(0.5f, 1.0f, 0.5f, 1.0f), $" -> {typeIndex.FriendlyName}");
            }
            
            ImGui.Text($"Namespace: {typeIndex.Namespace ?? "(global)"}");
            ImGui.Text($"Kind: {typeIndex.Kind} | Base: {typeIndex.BaseType ?? "none"}");
            ImGui.Text($"Line: {typeIndex.LineNumber}");
            
            ImGui.Separator();
            
            // Mapping editor - context-aware (type or selected member)
            RenderMappingEditor(typeIndex);
            
            ImGui.Separator();
            
            // Type details (loaded on demand)
            if (_selectedTypeDetails != null)
            {
                RenderTypeDetails();
            }
            else
            {
                if (ImGui.Button("Load Full Details"))
                {
                    LoadTypeDetails(typeIndex);
                }
            }
        }
        
        private void RenderMappingEditor(TypeIndex typeIndex)
        {
            // Determine what we're mapping based on selection
            string targetLabel;
            string currentName;
            string currentFriendlyName = null;
            
            if (_selectedMemberType == MemberType.Field && _selectedMemberIndex >= 0 && 
                _selectedTypeDetails != null && _selectedMemberIndex < _selectedTypeDetails.Fields.Count)
            {
                var field = _selectedTypeDetails.Fields[_selectedMemberIndex];
                targetLabel = $"Field: {field.Name}";
                currentName = field.Name;
                currentFriendlyName = field.FriendlyName;
            }
            else if (_selectedMemberType == MemberType.Property && _selectedMemberIndex >= 0 &&
                     _selectedTypeDetails != null && _selectedMemberIndex < _selectedTypeDetails.Properties.Count)
            {
                var prop = _selectedTypeDetails.Properties[_selectedMemberIndex];
                targetLabel = $"Property: {prop.Name}";
                currentName = prop.Name;
                currentFriendlyName = prop.FriendlyName;
            }
            else if (_selectedMemberType == MemberType.Method && _selectedMemberIndex >= 0 &&
                     _selectedTypeDetails != null && _selectedMemberIndex < _selectedTypeDetails.Methods.Count)
            {
                var method = _selectedTypeDetails.Methods[_selectedMemberIndex];
                targetLabel = $"Method: {method.Name}";
                currentName = method.Name;
                currentFriendlyName = method.FriendlyName;
            }
            else
            {
                // Default to type mapping
                targetLabel = $"Type: {typeIndex.Name}";
                currentName = typeIndex.Name;
                currentFriendlyName = typeIndex.FriendlyName;
            }
            
            ImGui.Text($"Mapping: {targetLabel}");
            if (currentFriendlyName != null)
            {
                ImGui.SameLine();
                ImGui.TextColored(new Vector4(0.5f, 1.0f, 0.5f, 1.0f), $" -> {currentFriendlyName}");
            }
            
            ImGui.Text("Set Friendly Name:");
            ImGui.SetNextItemWidth(200);
            ImGui.InputText("##FriendlyName", ref _editFriendlyName, 128);
            
            ImGui.Text("Set Namespace:");
            ImGui.SetNextItemWidth(200);
            ImGui.InputText("##Namespace", ref _editNamespace, 256);
            ImGui.SameLine();
            ImGui.TextDisabled("(e.g. DecaGames.RotMG.Objects.Map.Data)");
            
            if (ImGui.Button("Apply"))
            {
                ApplyCurrentMapping(typeIndex);
            }
            
            ImGui.SameLine();
            if (ImGui.Button("Clear"))
            {
                ClearCurrentMapping(typeIndex);
            }
            
            // Button to deselect member and go back to type
            if (_selectedMemberType != MemberType.None)
            {
                ImGui.SameLine();
                if (ImGui.Button("Select Type"))
                {
                    _selectedMemberType = MemberType.None;
                    _selectedMemberIndex = -1;
                    _editFriendlyName = typeIndex.FriendlyName ?? "";
                }
            }
        }
        
        private void RenderTypeDetails()
        {
            if (_selectedTypeDetails == null) return;
            
            ImGui.Text($"Signature: {TruncateSignature(_selectedTypeDetails.Signature, 60)}");
            
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(_selectedTypeDetails.Signature);
            }
            
            // Right-click to copy signature
            RenderCopyContextMenu("sig_ctx", _selectedTypeDetails.Signature);
            
            // Member search box
            ImGui.Text("Search Members:");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(150);
            ImGui.InputText("##MemberSearch", ref _memberSearchQuery, 128);
            ImGui.SameLine();
            if (ImGui.SmallButton("Clear##ClearMemberSearch"))
            {
                _memberSearchQuery = "";
            }
            
            // Fields
            int fieldMatchCount = CountMatchingMembers(_selectedTypeDetails.Fields, f => f.Name);
            if (ImGui.CollapsingHeader($"Fields ({fieldMatchCount}/{_selectedTypeDetails.Fields.Count})##Fields", ImGuiTreeNodeFlags.DefaultOpen))
            {
                int displayedIndex = 0;
                for (int i = 0; i < _selectedTypeDetails.Fields.Count; i++)
                {
                    var field = _selectedTypeDetails.Fields[i];
                    
                    // Filter by search query
                    if (!MatchesMemberSearch(field.Name, field.FriendlyName, field.FieldType))
                        continue;
                    
                    bool isSelected = (_selectedMemberType == MemberType.Field && _selectedMemberIndex == i);
                    
                    string display = field.FriendlyName != null
                        ? $"{field.FriendlyName} [{field.Name}] : {field.FieldType} @ {field.Offset}"
                        : $"{field.Name} : {field.FieldType} @ {field.Offset}";
                    
                    if (field.FriendlyName != null)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 1.0f, 0.5f, 1.0f));
                    }
                    
                    ImGui.PushID(i);
                    if (ImGui.Selectable(display, isSelected))
                    {
                        _selectedMemberIndex = i;
                        _selectedMemberType = MemberType.Field;
                        _editFriendlyName = field.FriendlyName ?? "";
                    }
                    
                    // Right-click context menu
                    if (ImGui.BeginPopupContextItem("field_ctx"))
                    {
                        if (ImGui.MenuItem("Copy Name"))
                            ImGui.SetClipboardText(field.Name);
                        if (ImGui.MenuItem("Copy Type"))
                            ImGui.SetClipboardText(field.FieldType);
                        if (field.FriendlyName != null && ImGui.MenuItem("Copy Friendly Name"))
                            ImGui.SetClipboardText(field.FriendlyName);
                        if (ImGui.MenuItem("Copy Full"))
                            ImGui.SetClipboardText(display);
                        ImGui.EndPopup();
                    }
                    ImGui.PopID();
                    
                    if (field.FriendlyName != null)
                    {
                        ImGui.PopStyleColor();
                    }
                    
                    displayedIndex++;
                }
            }
            
            // Properties
            int propMatchCount = CountMatchingMembers(_selectedTypeDetails.Properties, p => p.Name);
            if (ImGui.CollapsingHeader($"Properties ({propMatchCount}/{_selectedTypeDetails.Properties.Count})##Properties"))
            {
                for (int i = 0; i < _selectedTypeDetails.Properties.Count; i++)
                {
                    var prop = _selectedTypeDetails.Properties[i];
                    
                    // Filter by search query
                    if (!MatchesMemberSearch(prop.Name, prop.FriendlyName, prop.PropertyType))
                        continue;
                    
                    bool isSelected = (_selectedMemberType == MemberType.Property && _selectedMemberIndex == i);
                    
                    string accessors = "";
                    if (prop.HasGetter) accessors += "get; ";
                    if (prop.HasSetter) accessors += "set; ";
                    
                    string display = prop.FriendlyName != null
                        ? $"{prop.FriendlyName} [{prop.Name}] : {prop.PropertyType} {{ {accessors}}}"
                        : $"{prop.Name} : {prop.PropertyType} {{ {accessors}}}";
                    
                    if (prop.FriendlyName != null)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 1.0f, 0.5f, 1.0f));
                    }
                    
                    ImGui.PushID(i + 10000); // Offset to avoid ID collision with fields
                    if (ImGui.Selectable(display, isSelected))
                    {
                        _selectedMemberIndex = i;
                        _selectedMemberType = MemberType.Property;
                        _editFriendlyName = prop.FriendlyName ?? "";
                    }
                    
                    // Right-click context menu
                    if (ImGui.BeginPopupContextItem("prop_ctx"))
                    {
                        if (ImGui.MenuItem("Copy Name"))
                            ImGui.SetClipboardText(prop.Name);
                        if (ImGui.MenuItem("Copy Type"))
                            ImGui.SetClipboardText(prop.PropertyType);
                        if (prop.FriendlyName != null && ImGui.MenuItem("Copy Friendly Name"))
                            ImGui.SetClipboardText(prop.FriendlyName);
                        if (ImGui.MenuItem("Copy Full"))
                            ImGui.SetClipboardText(display);
                        ImGui.EndPopup();
                    }
                    ImGui.PopID();
                    
                    if (prop.FriendlyName != null)
                    {
                        ImGui.PopStyleColor();
                    }
                }
            }
            
            // Methods
            int methodMatchCount = CountMatchingMembers(_selectedTypeDetails.Methods, m => m.Name);
            if (ImGui.CollapsingHeader($"Methods ({methodMatchCount}/{_selectedTypeDetails.Methods.Count})##Methods"))
            {
                int displayedMethods = 0;
                for (int i = 0; i < _selectedTypeDetails.Methods.Count && displayedMethods < 200; i++)
                {
                    var method = _selectedTypeDetails.Methods[i];
                    
                    // Filter by search query
                    if (!MatchesMemberSearch(method.Name, method.FriendlyName, method.ReturnType))
                        continue;
                    
                    bool isSelected = (_selectedMemberType == MemberType.Method && _selectedMemberIndex == i);
                    
                    string paramList = "";
                    foreach (var p in method.Parameters)
                    {
                        if (paramList.Length > 0) paramList += ", ";
                        paramList += p.ParameterType;
                    }
                    
                    string display = method.FriendlyName != null
                        ? $"{method.FriendlyName} [{method.Name}]({paramList}) : {method.ReturnType}"
                        : $"{method.Name}({paramList}) : {method.ReturnType}";
                    if (method.Rva != null)
                    {
                        display += $" @ {method.Rva}";
                    }
                    
                    if (method.FriendlyName != null)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 1.0f, 0.5f, 1.0f));
                    }
                    
                    ImGui.PushID(i + 20000); // Offset to avoid ID collision
                    if (ImGui.Selectable(display, isSelected))
                    {
                        _selectedMemberIndex = i;
                        _selectedMemberType = MemberType.Method;
                        _editFriendlyName = method.FriendlyName ?? "";
                    }
                    
                    // Right-click context menu
                    if (ImGui.BeginPopupContextItem("method_ctx"))
                    {
                        if (ImGui.MenuItem("Copy Name"))
                            ImGui.SetClipboardText(method.Name);
                        if (ImGui.MenuItem("Copy Return Type"))
                            ImGui.SetClipboardText(method.ReturnType);
                        if (method.Rva != null && ImGui.MenuItem("Copy RVA"))
                            ImGui.SetClipboardText(method.Rva);
                        if (method.FriendlyName != null && ImGui.MenuItem("Copy Friendly Name"))
                            ImGui.SetClipboardText(method.FriendlyName);
                        if (ImGui.MenuItem("Copy Full"))
                            ImGui.SetClipboardText(display);
                        ImGui.EndPopup();
                    }
                    ImGui.PopID();
                    
                    if (method.FriendlyName != null)
                    {
                        ImGui.PopStyleColor();
                    }
                    
                    displayedMethods++;
                }
                
                if (methodMatchCount > displayedMethods)
                {
                    ImGui.Text($"... and {methodMatchCount - displayedMethods} more (refine search)");
                }
            }
        }
        
        private bool MatchesMemberSearch(string name, string friendlyName, string typeName)
        {
            if (string.IsNullOrEmpty(_memberSearchQuery))
                return true;
            
            string query = _memberSearchQuery.ToLowerInvariant();
            
            if (name?.ToLowerInvariant().Contains(query) == true)
                return true;
            if (friendlyName?.ToLowerInvariant().Contains(query) == true)
                return true;
            if (typeName?.ToLowerInvariant().Contains(query) == true)
                return true;
            
            return false;
        }
        
        private int CountMatchingMembers<T>(List<T> members, Func<T, string> getName)
        {
            if (string.IsNullOrEmpty(_memberSearchQuery))
                return members.Count;
            
            int count = 0;
            foreach (var m in members)
            {
                string name = getName(m);
                if (name?.ToLowerInvariant().Contains(_memberSearchQuery.ToLowerInvariant()) == true)
                    count++;
            }
            return count;
        }
        
        private void RenderCopyContextMenu(string id, string textToCopy)
        {
            if (ImGui.BeginPopupContextItem(id))
            {
                if (ImGui.MenuItem("Copy"))
                {
                    ImGui.SetClipboardText(textToCopy ?? "");
                }
                ImGui.EndPopup();
            }
        }
        
        private void RenderStatusBar()
        {
            ImGui.Separator();
            if (!string.IsNullOrEmpty(_statusMessage))
            {
                ImGui.Text(_statusMessage);
            }
            else
            {
                ImGui.Text($"Mappings: {_mappingDb?.Count ?? 0} | Types: {_dumpIndex?.Count ?? 0}");
            }
        }
        
        private void RefreshFilteredTypes()
        {
            _filteredTypes.Clear();
            if (_dumpIndex == null) return;
            
            foreach (var type in _dumpIndex.SearchTypes(_searchQuery))
            {
                // Apply filters
                if (_showOnlyObfuscated && !type.IsObfuscated)
                    continue;
                if (_showOnlyMapped && type.FriendlyName == null)
                    continue;
                
                _filteredTypes.Add(type);
            }
        }
        
        private void LoadTypeDetails(TypeIndex typeIndex)
        {
            if (_dumpIndex == null) return;
            
            _selectedTypeDetails = _dumpIndex.LoadTypeDetails(typeIndex);
            
            // Apply mappings to members using stable signatures
            if (_selectedTypeDetails != null && _mappingDb != null)
            {
                foreach (var field in _selectedTypeDetails.Fields)
                {
                    var signature = SignatureGenerator.GenerateFieldSignature(_selectedTypeDetails, field);
                    var mapping = _mappingDb.GetBySignature(signature);
                    if (mapping != null)
                    {
                        field.FriendlyName = mapping.FriendlyName;
                    }
                }
                
                foreach (var prop in _selectedTypeDetails.Properties)
                {
                    var signature = SignatureGenerator.GeneratePropertySignature(_selectedTypeDetails, prop);
                    var mapping = _mappingDb.GetBySignature(signature);
                    if (mapping != null)
                    {
                        prop.FriendlyName = mapping.FriendlyName;
                    }
                }
                
                foreach (var method in _selectedTypeDetails.Methods)
                {
                    var signature = SignatureGenerator.GenerateMethodSignature(_selectedTypeDetails, method);
                    var mapping = _mappingDb.GetBySignature(signature);
                    if (mapping != null)
                    {
                        method.FriendlyName = mapping.FriendlyName;
                    }
                }
            }
            
            _editFriendlyName = typeIndex.FriendlyName ?? "";
        }
        
        private void ApplyCurrentMapping(TypeIndex typeIndex)
        {
            if (string.IsNullOrWhiteSpace(_editFriendlyName)) return;
            if (_mappingDb == null) return;
            
            if (_selectedMemberType == MemberType.Field && _selectedMemberIndex >= 0 &&
                _selectedTypeDetails != null && _selectedMemberIndex < _selectedTypeDetails.Fields.Count)
            {
                // Apply field mapping with stable signature
                var field = _selectedTypeDetails.Fields[_selectedMemberIndex];
                var signature = SignatureGenerator.GenerateFieldSignature(_selectedTypeDetails, field);
                ApplyMemberMapping($"{typeIndex.Name}.{field.Name}", signature, _editFriendlyName, SymbolType.Field, typeIndex.Namespace);
                field.FriendlyName = _editFriendlyName;
                SetStatus($"Mapped {typeIndex.Name}.{field.Name} -> {_editFriendlyName}");
            }
            else if (_selectedMemberType == MemberType.Property && _selectedMemberIndex >= 0 &&
                     _selectedTypeDetails != null && _selectedMemberIndex < _selectedTypeDetails.Properties.Count)
            {
                // Apply property mapping with stable signature
                var prop = _selectedTypeDetails.Properties[_selectedMemberIndex];
                var signature = SignatureGenerator.GeneratePropertySignature(_selectedTypeDetails, prop);
                ApplyMemberMapping($"{typeIndex.Name}.{prop.Name}", signature, _editFriendlyName, SymbolType.Property, typeIndex.Namespace);
                prop.FriendlyName = _editFriendlyName;
                SetStatus($"Mapped {typeIndex.Name}.{prop.Name} -> {_editFriendlyName}");
            }
            else if (_selectedMemberType == MemberType.Method && _selectedMemberIndex >= 0 &&
                     _selectedTypeDetails != null && _selectedMemberIndex < _selectedTypeDetails.Methods.Count)
            {
                // Apply method mapping with stable signature
                var method = _selectedTypeDetails.Methods[_selectedMemberIndex];
                var signature = SignatureGenerator.GenerateMethodSignature(_selectedTypeDetails, method);
                ApplyMemberMapping($"{typeIndex.Name}.{method.Name}", signature, _editFriendlyName, SymbolType.Method, typeIndex.Namespace);
                method.FriendlyName = _editFriendlyName;
                SetStatus($"Mapped {typeIndex.Name}.{method.Name} -> {_editFriendlyName}");
            }
            else
            {
                // Apply type mapping
                ApplyMapping(typeIndex);
            }
        }
        
        private void ApplyMemberMapping(string obfuscatedName, string signature, string friendlyName, SymbolType symbolType, string namespaceName)
        {
            var mapping = new SymbolMapping
            {
                ObfuscatedName = obfuscatedName,
                FriendlyName = friendlyName,
                Signature = signature,
                SymbolType = symbolType,
                Namespace = namespaceName ?? "",
                VerificationScore = 1.0
            };
            
            _mappingDb.SetMapping(mapping);
        }
        
        private void ClearCurrentMapping(TypeIndex typeIndex)
        {
            if (_mappingDb == null) return;
            
            if (_selectedMemberType == MemberType.Field && _selectedMemberIndex >= 0 &&
                _selectedTypeDetails != null && _selectedMemberIndex < _selectedTypeDetails.Fields.Count)
            {
                var field = _selectedTypeDetails.Fields[_selectedMemberIndex];
                var signature = SignatureGenerator.GenerateFieldSignature(_selectedTypeDetails, field);
                _mappingDb.RemoveMapping(signature);
                field.FriendlyName = null;
                _editFriendlyName = "";
                SetStatus($"Cleared mapping for {typeIndex.Name}.{field.Name}");
            }
            else if (_selectedMemberType == MemberType.Property && _selectedMemberIndex >= 0 &&
                     _selectedTypeDetails != null && _selectedMemberIndex < _selectedTypeDetails.Properties.Count)
            {
                var prop = _selectedTypeDetails.Properties[_selectedMemberIndex];
                var signature = SignatureGenerator.GeneratePropertySignature(_selectedTypeDetails, prop);
                _mappingDb.RemoveMapping(signature);
                prop.FriendlyName = null;
                _editFriendlyName = "";
                SetStatus($"Cleared mapping for {typeIndex.Name}.{prop.Name}");
            }
            else if (_selectedMemberType == MemberType.Method && _selectedMemberIndex >= 0 &&
                     _selectedTypeDetails != null && _selectedMemberIndex < _selectedTypeDetails.Methods.Count)
            {
                var method = _selectedTypeDetails.Methods[_selectedMemberIndex];
                var signature = SignatureGenerator.GenerateMethodSignature(_selectedTypeDetails, method);
                _mappingDb.RemoveMapping(signature);
                method.FriendlyName = null;
                _editFriendlyName = "";
                SetStatus($"Cleared mapping for {typeIndex.Name}.{method.Name}");
            }
            else
            {
                ClearMapping(typeIndex);
            }
        }
        
        private void ApplyMapping(TypeIndex typeIndex)
        {
            if (string.IsNullOrWhiteSpace(_editFriendlyName)) return;
            if (_mappingDb == null) return;
            
            // Generate signature if we have details
            string signature = _selectedTypeDetails?.Signature ?? "";
            
            var mapping = new SymbolMapping
            {
                ObfuscatedName = typeIndex.Name,
                FriendlyName = _editFriendlyName,
                Signature = signature,
                SymbolType = SymbolType.Type,
                Namespace = typeIndex.Namespace ?? "",
                VerificationScore = 1.0
            };
            
            _mappingDb.SetMapping(mapping);
            typeIndex.FriendlyName = _editFriendlyName;
            
            SetStatus($"Mapped {typeIndex.Name} -> {_editFriendlyName}");
        }
        
        private void ClearMapping(TypeIndex typeIndex)
        {
            if (_mappingDb == null) return;
            
            _mappingDb.RemoveMapping(typeIndex.Name);
            typeIndex.FriendlyName = null;
            _editFriendlyName = "";
            
            SetStatus($"Cleared mapping for {typeIndex.Name}");
        }
        
        private void SaveMappings()
        {
            if (_mappingDb == null) return;
            
            if (_mappingDb.Save())
            {
                // Also reload the DeobfuscationHelper so the Inspector uses updated mappings
                DeobfuscationHelper.Reload();
                SetStatus($"Saved {_mappingDb.Count} mappings to {_mappingsPath}");
            }
            else
            {
                SetStatus("Failed to save mappings!");
            }
        }
        
        private void ReloadMappings()
        {
            if (_mappingDb == null) return;
            
            _mappingDb.Load();
            if (_dumpIndex != null)
            {
                _dumpIndex.ApplyMappings(_mappingDb);
            }
            RefreshFilteredTypes();
            
            SetStatus($"Reloaded {_mappingDb.Count} mappings");
        }
        
        private void ExportMappings()
        {
            SaveMappings(); // Just save to the current file
        }
        
        private void VerifyAllSignatures()
        {
            if (_mappingDb == null || _dumpIndex == null) return;
            
            int verified = 0;
            int failed = 0;
            
            foreach (var mapping in _mappingDb.AllMappings)
            {
                var typeIndex = _dumpIndex.GetType(mapping.ObfuscatedName);
                if (typeIndex != null)
                {
                    var details = _dumpIndex.LoadTypeDetails(typeIndex);
                    if (details != null)
                    {
                        double similarity = SignatureGenerator.ComputeSignatureSimilarity(
                            mapping.Signature, details.Signature);
                        
                        if (similarity >= 0.8)
                        {
                            verified++;
                            mapping.VerificationScore = similarity;
                        }
                        else
                        {
                            failed++;
                            mapping.VerificationScore = similarity;
                        }
                    }
                }
            }
            
            SetStatus($"Verified: {verified} passed, {failed} failed (threshold 80%)");
        }
        
        private void FixMappingNamespaces()
        {
            if (_mappingDb == null || _dumpIndex == null) return;
            
            const string LOG_TAG = "DeobfuscationPanel";
            
            int updated = 0;
            int notFound = 0;
            int alreadyCorrect = 0;
            int emptyInDump = 0;
            
            // Build a lookup dictionary from type name to TypeIndex using SearchTypes
            var typeNameToTypeIndex = new Dictionary<string, TypeIndex>();
            foreach (var type in _dumpIndex.SearchTypes(""))
            {
                if (!string.IsNullOrEmpty(type.Name) && !typeNameToTypeIndex.ContainsKey(type.Name))
                {
                    typeNameToTypeIndex[type.Name] = type;
                }
            }
            
            ModLogger.LogInternal(LOG_TAG, $"[INFO] FixNamespaces: Built lookup with {typeNameToTypeIndex.Count} types");
            
            // Debug: Log first 10 types with their namespaces
            int logged = 0;
            foreach (var kvp in typeNameToTypeIndex)
            {
                if (logged < 10)
                {
                    ModLogger.LogInternal(LOG_TAG, $"[DEBUG] Type: {kvp.Key} | Namespace: '{kvp.Value.Namespace ?? "(null)"}'");
                    logged++;
                }
                else break;
            }
            
            // Debug: Find and log sample types with non-empty namespace
            int withNsCount = 0;
            foreach (var kvp in typeNameToTypeIndex)
            {
                if (!string.IsNullOrEmpty(kvp.Value.Namespace))
                {
                    if (withNsCount < 5)
                    {
                        ModLogger.LogInternal(LOG_TAG, $"[DEBUG] Type WITH namespace: {kvp.Key} -> '{kvp.Value.Namespace}'");
                    }
                    withNsCount++;
                }
            }
            ModLogger.LogInternal(LOG_TAG, $"[INFO] Types with non-empty namespace: {withNsCount} / {typeNameToTypeIndex.Count}");
            
            foreach (var mapping in _mappingDb.AllMappings)
            {
                // For type mappings, look up by ObfuscatedName directly
                // For member mappings (Field, Property, Method), extract the type name from "TypeName.MemberName"
                string typeName = mapping.ObfuscatedName;
                
                if (mapping.SymbolType == SymbolType.Field || 
                    mapping.SymbolType == SymbolType.Property || 
                    mapping.SymbolType == SymbolType.Method)
                {
                    // Extract type name from "TypeName.MemberName"
                    int dotIndex = mapping.ObfuscatedName.LastIndexOf('.');
                    if (dotIndex > 0)
                    {
                        typeName = mapping.ObfuscatedName.Substring(0, dotIndex);
                    }
                }
                
                if (typeNameToTypeIndex.TryGetValue(typeName, out TypeIndex foundType))
                {
                    string foundNamespace = foundType.Namespace ?? "";
                    
                    if (string.IsNullOrEmpty(foundNamespace))
                    {
                        emptyInDump++;
                    }
                    else if (mapping.Namespace != foundNamespace)
                    {
                        ModLogger.LogInternal(LOG_TAG, $"[INFO] Updating: {mapping.ObfuscatedName} namespace '{mapping.Namespace}' -> '{foundNamespace}'");
                        mapping.Namespace = foundNamespace;
                        updated++;
                    }
                    else
                    {
                        alreadyCorrect++;
                    }
                }
                else
                {
                    ModLogger.LogInternal(LOG_TAG, $"[WARN] Type not found in dump: {typeName}");
                    notFound++;
                }
            }
            
            ModLogger.LogInternal(LOG_TAG, $"[INFO] Results: updated={updated}, correct={alreadyCorrect}, emptyInDump={emptyInDump}, notFound={notFound}");
            
            if (updated > 0)
            {
                SetStatus($"Updated {updated}, already correct {alreadyCorrect}, empty in dump {emptyInDump}, not found {notFound}. Save to persist.");
            }
            else
            {
                SetStatus($"Empty in dump: {emptyInDump}, correct: {alreadyCorrect}, not found: {notFound}. Check console for details.");
            }
        }
        
        private void SetStatus(string message)
        {
            _statusMessage = message;
            _statusMessageTime = 5.0f;
        }
        
        private string TruncateSignature(string sig, int maxLen)
        {
            if (string.IsNullOrEmpty(sig)) return "";
            if (sig.Length <= maxLen) return sig;
            return sig.Substring(0, maxLen) + "...";
        }
        
        private enum MemberType
        {
            None,
            Field,
            Property,
            Method
        }
    }
}
