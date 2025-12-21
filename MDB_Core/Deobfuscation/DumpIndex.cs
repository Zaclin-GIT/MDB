using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using GameSDK.ModHost;

namespace GameSDK.Deobfuscation
{
    /// <summary>
    /// Fast indexed parser for large dump.cs files.
    /// Builds a quick index of type locations, then parses details on demand.
    /// </summary>
    public class DumpIndex
    {
        private static readonly Regex TypeHeaderPattern = new Regex(
            @"^(public|internal|private|protected)?\s*(sealed\s+|abstract\s+|static\s+)*(class|struct|enum|interface)\s+(\w+)",
            RegexOptions.Compiled);
        private static readonly Regex NamespacePattern = new Regex(@"^// Namespace:\s*(.*)$", RegexOptions.Compiled);
        private static readonly Regex DllPattern = new Regex(@"^// Dll\s*:\s*(.+)$", RegexOptions.Compiled);
        
        private string _filePath;
        private Dictionary<string, TypeIndex> _typesByName = new Dictionary<string, TypeIndex>();
        private List<TypeIndex> _allTypes = new List<TypeIndex>();
        private Dictionary<string, List<TypeIndex>> _typesByNamespace = new Dictionary<string, List<TypeIndex>>();
        
        /// <summary>
        /// All indexed types (minimal info only).
        /// </summary>
        public IReadOnlyList<TypeIndex> Types => _allTypes;
        
        /// <summary>
        /// Number of indexed types.
        /// </summary>
        public int Count => _allTypes.Count;
        
        /// <summary>
        /// Gets all unique namespaces.
        /// </summary>
        public IEnumerable<string> Namespaces => _typesByNamespace.Keys;
        
        /// <summary>
        /// Builds an index of all types in the dump file.
        /// This is fast - just extracts type names and line numbers.
        /// </summary>
        public void BuildIndex(string filePath)
        {
            _filePath = filePath;
            _typesByName.Clear();
            _allTypes.Clear();
            _typesByNamespace.Clear();
            
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Dump file not found: {filePath}");
            
            string currentDll = null;
            string currentNamespace = "";
            int lineNumber = 0;
            
            using (var reader = new StreamReader(filePath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    lineNumber++;
                    
                    // Quick prefix checks to skip most lines
                    if (line.Length == 0) continue;
                    
                    char first = line[0];
                    
                    // Check for comments (Dll, Namespace)
                    if (first == '/')
                    {
                        if (line.StartsWith("// Dll"))
                        {
                            var match = DllPattern.Match(line);
                            if (match.Success)
                                currentDll = match.Groups[1].Value.Trim();
                        }
                        else if (line.StartsWith("// Namespace"))
                        {
                            var match = NamespacePattern.Match(line);
                            if (match.Success)
                                currentNamespace = match.Groups[1].Value.Trim();
                        }
                        continue;
                    }
                    
                    // Check for type declaration (starts with visibility or modifier)
                    if (first == 'p' || first == 'i' || first == 's' || first == 'a') // public/private/protected/internal/sealed/static/abstract
                    {
                        var match = TypeHeaderPattern.Match(line);
                        if (match.Success)
                        {
                            var typeName = match.Groups[4].Value;
                            var kind = match.Groups[3].Value;
                            
                            var index = new TypeIndex
                            {
                                Name = typeName,
                                Namespace = currentNamespace,
                                Dll = currentDll,
                                Kind = kind,
                                LineNumber = lineNumber,
                                IsSealed = line.Contains("sealed"),
                                IsAbstract = line.Contains("abstract"),
                                IsStatic = line.Contains("static")
                            };
                            
                            // Extract base type if present
                            int colonIdx = line.IndexOf(':');
                            if (colonIdx > 0)
                            {
                                var afterColon = line.Substring(colonIdx + 1).Trim();
                                // Remove trailing { if present
                                int braceIdx = afterColon.IndexOf('{');
                                if (braceIdx > 0)
                                    afterColon = afterColon.Substring(0, braceIdx).Trim();
                                index.BaseType = afterColon;
                            }
                            
                            _allTypes.Add(index);
                            
                            // Index by name (may have duplicates in different namespaces)
                            if (!_typesByName.ContainsKey(typeName))
                                _typesByName[typeName] = index;
                            
                            // Index by namespace
                            if (!_typesByNamespace.ContainsKey(currentNamespace))
                                _typesByNamespace[currentNamespace] = new List<TypeIndex>();
                            _typesByNamespace[currentNamespace].Add(index);
                        }
                    }
                }
            }
            
            ModLogger.LogInternal("DumpIndex", $"Indexed {_allTypes.Count} types from {filePath}");
        }
        
        /// <summary>
        /// Gets a type index by name.
        /// </summary>
        public TypeIndex GetType(string name)
        {
            _typesByName.TryGetValue(name, out var type);
            return type;
        }
        
        /// <summary>
        /// Gets types in a namespace.
        /// </summary>
        public IReadOnlyList<TypeIndex> GetTypesInNamespace(string ns)
        {
            if (_typesByNamespace.TryGetValue(ns ?? "", out var types))
                return types;
            return Array.Empty<TypeIndex>();
        }
        
        /// <summary>
        /// Searches types by name (case-insensitive).
        /// </summary>
        public IEnumerable<TypeIndex> SearchTypes(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                foreach (var t in _allTypes)
                    yield return t;
                yield break;
            }
            
            var lowerQuery = query.ToLowerInvariant();
            foreach (var t in _allTypes)
            {
                if (t.Name.ToLowerInvariant().Contains(lowerQuery) ||
                    (t.FriendlyName?.ToLowerInvariant().Contains(lowerQuery) ?? false))
                {
                    yield return t;
                }
            }
        }
        
        /// <summary>
        /// Parses full type details on demand.
        /// </summary>
        public TypeDefinition LoadTypeDetails(TypeIndex typeIndex)
        {
            if (string.IsNullOrEmpty(_filePath) || !File.Exists(_filePath))
                return null;
            
            var type = new TypeDefinition
            {
                Name = typeIndex.Name,
                Namespace = typeIndex.Namespace,
                Dll = typeIndex.Dll,
                Kind = typeIndex.Kind,
                IsSealed = typeIndex.IsSealed,
                IsAbstract = typeIndex.IsAbstract,
                IsStatic = typeIndex.IsStatic,
                BaseType = typeIndex.BaseType
            };
            
            // Read from the type's line until we hit the closing brace
            using (var reader = new StreamReader(_filePath))
            {
                // Skip to the type's line
                for (int i = 1; i < typeIndex.LineNumber; i++)
                    reader.ReadLine();
                
                ParseTypeBody(reader, type);
            }
            
            // Signature is computed automatically via the property getter
            return type;
        }
        
        private static readonly Regex FieldPattern = new Regex(
            @"^\s*(public|private|protected|internal)?\s*(static\s+|const\s+|readonly\s+)*([\w\[\]`<>,\s]+?)\s+(\w+);\s*//\s*(.+)$",
            RegexOptions.Compiled);
        private static readonly Regex PropertyPattern = new Regex(
            @"^\s*(public|private|protected|internal)?\s*(virtual\s+|override\s+|abstract\s+|static\s+)*([\w\[\]`<>,\s]+?)\s+(\w+)\s*\{(.+)\}$",
            RegexOptions.Compiled);
        private static readonly Regex MethodRvaPattern = new Regex(
            @"^\s*// RVA:\s*(0x[\da-fA-F]+)\s+VA:\s*(0x[\da-fA-F]+)",
            RegexOptions.Compiled);
        private static readonly Regex MethodSigPattern = new Regex(
            @"^\s*(public|private|protected|internal)?\s*(virtual\s+|override\s+|abstract\s+|static\s+)*([\w\[\]`<>,\s]+?)\s+(\w+)\s*\(([^)]*)\)",
            RegexOptions.Compiled);
        
        private void ParseTypeBody(StreamReader reader, TypeDefinition type)
        {
            bool inFields = false, inProperties = false, inMethods = false;
            string pendingRva = null, pendingVa = null;
            int braceDepth = 0;
            bool foundOpen = false;
            
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                var trimmed = line.Trim();
                
                // Track braces
                if (trimmed.Contains("{"))
                {
                    foundOpen = true;
                    braceDepth++;
                }
                if (trimmed.Contains("}"))
                {
                    braceDepth--;
                    if (foundOpen && braceDepth <= 0)
                        break; // End of type
                }
                
                // Section markers
                if (trimmed == "// Fields") { inFields = true; inProperties = false; inMethods = false; continue; }
                if (trimmed == "// Properties") { inFields = false; inProperties = true; inMethods = false; continue; }
                if (trimmed == "// Methods") { inFields = false; inProperties = false; inMethods = true; continue; }
                
                // Parse fields
                if (inFields)
                {
                    var match = FieldPattern.Match(line);
                    if (match.Success)
                    {
                        type.Fields.Add(new FieldDefinition
                        {
                            Visibility = match.Groups[1].Value.Trim(),
                            FieldType = match.Groups[3].Value.Trim(),
                            Name = match.Groups[4].Value.Trim(),
                            Offset = match.Groups[5].Value.Trim(),
                            IsStatic = match.Groups[2].Value.Contains("static"),
                            IsConst = match.Groups[2].Value.Contains("const"),
                            IsReadOnly = match.Groups[2].Value.Contains("readonly")
                        });
                    }
                    continue;
                }
                
                // Parse properties
                if (inProperties)
                {
                    var match = PropertyPattern.Match(line);
                    if (match.Success)
                    {
                        var accessors = match.Groups[5].Value;
                        type.Properties.Add(new PropertyDefinition
                        {
                            Visibility = match.Groups[1].Value.Trim(),
                            PropertyType = match.Groups[3].Value.Trim(),
                            Name = match.Groups[4].Value.Trim(),
                            HasGetter = accessors.Contains("get"),
                            HasSetter = accessors.Contains("set")
                        });
                    }
                    continue;
                }
                
                // Parse methods
                if (inMethods)
                {
                    // Check for RVA comment
                    var rvaMatch = MethodRvaPattern.Match(line);
                    if (rvaMatch.Success)
                    {
                        pendingRva = rvaMatch.Groups[1].Value;
                        pendingVa = rvaMatch.Groups[2].Value;
                        continue;
                    }
                    
                    // Check for method signature
                    var sigMatch = MethodSigPattern.Match(line);
                    if (sigMatch.Success)
                    {
                        var method = new MethodDefinition
                        {
                            Visibility = sigMatch.Groups[1].Value.Trim(),
                            ReturnType = sigMatch.Groups[3].Value.Trim(),
                            Name = sigMatch.Groups[4].Value.Trim(),
                            Rva = pendingRva,
                            Va = pendingVa
                        };
                        
                        var mods = sigMatch.Groups[2].Value;
                        method.IsVirtual = mods.Contains("virtual");
                        method.IsOverride = mods.Contains("override");
                        method.IsAbstract = mods.Contains("abstract");
                        method.IsStatic = mods.Contains("static");
                        
                        // Parse parameters
                        var paramStr = sigMatch.Groups[5].Value.Trim();
                        if (!string.IsNullOrEmpty(paramStr))
                        {
                            foreach (var param in paramStr.Split(','))
                            {
                                var parts = param.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                if (parts.Length >= 2)
                                {
                                    method.Parameters.Add(new ParameterDefinition
                                    {
                                        ParameterType = parts[0],
                                        Name = parts[parts.Length - 1]
                                    });
                                }
                            }
                        }
                        
                        type.Methods.Add(method);
                        pendingRva = null;
                        pendingVa = null;
                    }
                }
            }
        }
        
        /// <summary>
        /// Applies friendly names from a mapping database.
        /// </summary>
        public void ApplyMappings(MappingDatabase database)
        {
            foreach (var type in _allTypes)
            {
                var mapping = database.GetByObfuscatedName(type.Name);
                if (mapping != null)
                {
                    type.FriendlyName = mapping.FriendlyName;
                }
            }
        }
    }
    
    /// <summary>
    /// Minimal type info for index - fast to load.
    /// </summary>
    public class TypeIndex
    {
        public string Name { get; set; }
        public string FriendlyName { get; set; }
        public string Namespace { get; set; }
        public string Dll { get; set; }
        public string Kind { get; set; }
        public string BaseType { get; set; }
        public int LineNumber { get; set; }
        public bool IsSealed { get; set; }
        public bool IsAbstract { get; set; }
        public bool IsStatic { get; set; }
        
        /// <summary>
        /// Display name (friendly name if mapped, otherwise obfuscated).
        /// </summary>
        public string DisplayName => FriendlyName ?? Name;
        
        /// <summary>
        /// Full name including namespace.
        /// </summary>
        public string FullName => string.IsNullOrEmpty(Namespace) ? Name : $"{Namespace}.{Name}";
        
        /// <summary>
        /// Whether this type appears to be obfuscated (all caps random letters).
        /// </summary>
        public bool IsObfuscated => Name.Length >= 8 && Name.Length <= 15 && 
                                     System.Text.RegularExpressions.Regex.IsMatch(Name, "^[A-Z]+$");
    }
}
