using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace GameSDK.Deobfuscation
{
    /// <summary>
    /// Parses IL2CPP dump.cs files into structured TypeDefinition objects.
    /// </summary>
    public class DumpParser
    {
        // Regex patterns for parsing dump.cs
        private static readonly Regex DllPattern = new Regex(@"^// Dll\s*:\s*(.+)$", RegexOptions.Compiled);
        private static readonly Regex NamespacePattern = new Regex(@"^// Namespace:\s*(.*)$", RegexOptions.Compiled);
        private static readonly Regex TypePattern = new Regex(
            @"^(public|internal|private|protected)?\s*(sealed\s+|abstract\s+|static\s+)*(class|struct|enum|interface)\s+(\w+)(?:\s*:\s*(.+))?$",
            RegexOptions.Compiled);
        private static readonly Regex FieldPattern = new Regex(
            @"^\s*(public|private|protected|internal)?\s*(static\s+|const\s+|readonly\s+)*([\w\[\]`<>,\s]+?)\s+(\w+);\s*//\s*(.+)$",
            RegexOptions.Compiled);
        private static readonly Regex PropertyPattern = new Regex(
            @"^\s*(public|private|protected|internal)?\s*(virtual\s+|override\s+|abstract\s+|static\s+)*([\w\[\]`<>,\s]+?)\s+(\w+)\s*\{(.+)\}$",
            RegexOptions.Compiled);
        private static readonly Regex MethodPattern = new Regex(
            @"^\s*// RVA:\s*(0x[\da-fA-F]+)\s+VA:\s*(0x[\da-fA-F]+)\s*$",
            RegexOptions.Compiled);
        private static readonly Regex MethodSigPattern = new Regex(
            @"^\s*(public|private|protected|internal)?\s*(virtual\s+|override\s+|abstract\s+|static\s+)*([\w\[\]`<>,\s]+?)\s+(\w+)\s*\(([^)]*)\)\s*\{?\s*\}?$",
            RegexOptions.Compiled);
        
        private List<TypeDefinition> _types = new List<TypeDefinition>();
        private Dictionary<string, TypeDefinition> _typesByName = new Dictionary<string, TypeDefinition>();
        private Dictionary<string, TypeDefinition> _typesByFullName = new Dictionary<string, TypeDefinition>();
        
        /// <summary>
        /// All parsed types.
        /// </summary>
        public IReadOnlyList<TypeDefinition> Types => _types;
        
        /// <summary>
        /// Gets a type by name (without namespace).
        /// </summary>
        public TypeDefinition GetType(string name)
        {
            _typesByName.TryGetValue(name, out var type);
            return type;
        }
        
        /// <summary>
        /// Gets a type by full name (namespace.name).
        /// </summary>
        public TypeDefinition GetTypeByFullName(string fullName)
        {
            _typesByFullName.TryGetValue(fullName, out var type);
            return type;
        }
        
        /// <summary>
        /// Searches types by name pattern (case-insensitive).
        /// </summary>
        public IEnumerable<TypeDefinition> SearchTypes(string query, bool includeMembers = false)
        {
            if (string.IsNullOrWhiteSpace(query))
                return _types;
            
            var lowerQuery = query.ToLowerInvariant();
            
            return _types.Where(t =>
                t.Name.ToLowerInvariant().Contains(lowerQuery) ||
                (t.FriendlyName?.ToLowerInvariant().Contains(lowerQuery) ?? false) ||
                (t.Namespace?.ToLowerInvariant().Contains(lowerQuery) ?? false) ||
                (includeMembers && HasMatchingMember(t, lowerQuery)));
        }
        
        /// <summary>
        /// Filters types by namespace.
        /// </summary>
        public IEnumerable<TypeDefinition> GetTypesByNamespace(string ns)
        {
            return _types.Where(t => t.Namespace == ns);
        }
        
        /// <summary>
        /// Gets all unique namespaces.
        /// </summary>
        public IEnumerable<string> GetNamespaces()
        {
            return _types.Select(t => t.Namespace ?? "").Distinct().OrderBy(n => n);
        }
        
        /// <summary>
        /// Parses a dump.cs file.
        /// </summary>
        public void Parse(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Dump file not found: {filePath}");
            
            _types.Clear();
            _typesByName.Clear();
            _typesByFullName.Clear();
            
            using (var reader = new StreamReader(filePath))
            {
                ParseStream(reader);
            }
            
            Console.WriteLine($"[DumpParser] Parsed {_types.Count} types from {filePath}");
        }
        
        /// <summary>
        /// Parses dump content from a string.
        /// </summary>
        public void ParseContent(string content)
        {
            _types.Clear();
            _typesByName.Clear();
            _typesByFullName.Clear();
            
            using (var reader = new StringReader(content))
            {
                ParseStream(reader);
            }
            
            Console.WriteLine($"[DumpParser] Parsed {_types.Count} types");
        }
        
        /// <summary>
        /// Applies mappings from a MappingDatabase to set friendly names.
        /// </summary>
        public void ApplyMappings(MappingDatabase database)
        {
            foreach (var type in _types)
            {
                // Apply type mapping
                var typeMapping = database.GetBySignature(type.Signature);
                if (typeMapping != null)
                {
                    type.FriendlyName = typeMapping.FriendlyName;
                }
                
                // Apply field mappings
                foreach (var field in type.Fields)
                {
                    var fieldSig = SignatureGenerator.GenerateFieldSignature(type, field);
                    var fieldMapping = database.GetBySignature(fieldSig);
                    if (fieldMapping != null)
                    {
                        field.FriendlyName = fieldMapping.FriendlyName;
                    }
                }
                
                // Apply property mappings
                foreach (var prop in type.Properties)
                {
                    var propSig = SignatureGenerator.GeneratePropertySignature(type, prop);
                    var propMapping = database.GetBySignature(propSig);
                    if (propMapping != null)
                    {
                        prop.FriendlyName = propMapping.FriendlyName;
                    }
                }
                
                // Apply method mappings
                foreach (var method in type.Methods)
                {
                    var methodSig = SignatureGenerator.GenerateMethodSignature(type, method);
                    var methodMapping = database.GetBySignature(methodSig);
                    if (methodMapping != null)
                    {
                        method.FriendlyName = methodMapping.FriendlyName;
                    }
                }
            }
        }
        
        private void ParseStream(TextReader reader)
        {
            string currentDll = null;
            string currentNamespace = null;
            TypeDefinition currentType = null;
            string pendingRva = null;
            string pendingVa = null;
            bool inFieldsSection = false;
            bool inPropertiesSection = false;
            bool inMethodsSection = false;
            
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.TrimEnd();
                
                // Skip empty lines
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                
                // Check for section markers
                if (line.Trim() == "// Fields")
                {
                    inFieldsSection = true;
                    inPropertiesSection = false;
                    inMethodsSection = false;
                    continue;
                }
                if (line.Trim() == "// Properties")
                {
                    inFieldsSection = false;
                    inPropertiesSection = true;
                    inMethodsSection = false;
                    continue;
                }
                if (line.Trim() == "// Methods")
                {
                    inFieldsSection = false;
                    inPropertiesSection = false;
                    inMethodsSection = true;
                    continue;
                }
                
                // Parse DLL comment
                var dllMatch = DllPattern.Match(line);
                if (dllMatch.Success)
                {
                    currentDll = dllMatch.Groups[1].Value.Trim();
                    continue;
                }
                
                // Parse namespace comment
                var nsMatch = NamespacePattern.Match(line);
                if (nsMatch.Success)
                {
                    currentNamespace = nsMatch.Groups[1].Value.Trim();
                    if (string.IsNullOrEmpty(currentNamespace))
                        currentNamespace = null;
                    continue;
                }
                
                // Parse type declaration
                var typeMatch = TypePattern.Match(line);
                if (typeMatch.Success)
                {
                    // Save previous type
                    if (currentType != null)
                    {
                        AddType(currentType);
                    }
                    
                    currentType = new TypeDefinition
                    {
                        Dll = currentDll,
                        Namespace = currentNamespace,
                        Visibility = typeMatch.Groups[1].Value.Trim(),
                        Kind = typeMatch.Groups[3].Value.Trim(),
                        Name = typeMatch.Groups[4].Value.Trim(),
                        BaseType = typeMatch.Groups[5].Success ? typeMatch.Groups[5].Value.Trim() : null
                    };
                    
                    var modifiers = typeMatch.Groups[2].Value;
                    currentType.IsSealed = modifiers.Contains("sealed");
                    currentType.IsAbstract = modifiers.Contains("abstract");
                    currentType.IsStatic = modifiers.Contains("static");
                    
                    inFieldsSection = false;
                    inPropertiesSection = false;
                    inMethodsSection = false;
                    continue;
                }
                
                // Skip if no current type
                if (currentType == null)
                    continue;
                
                // Check for closing brace (end of type)
                if (line.Trim() == "}")
                {
                    if (currentType != null)
                    {
                        AddType(currentType);
                        currentType = null;
                    }
                    continue;
                }
                
                // Parse RVA/VA comment (before method)
                var rvaMatch = MethodPattern.Match(line);
                if (rvaMatch.Success)
                {
                    pendingRva = rvaMatch.Groups[1].Value;
                    pendingVa = rvaMatch.Groups[2].Value;
                    continue;
                }
                
                // Parse field
                if (inFieldsSection)
                {
                    var fieldMatch = FieldPattern.Match(line);
                    if (fieldMatch.Success)
                    {
                        var field = new FieldDefinition
                        {
                            Visibility = fieldMatch.Groups[1].Value.Trim(),
                            FieldType = fieldMatch.Groups[3].Value.Trim(),
                            Name = fieldMatch.Groups[4].Value.Trim(),
                            Offset = fieldMatch.Groups[5].Value.Trim()
                        };
                        
                        var modifiers = fieldMatch.Groups[2].Value;
                        field.IsStatic = modifiers.Contains("static");
                        field.IsConst = modifiers.Contains("const");
                        field.IsReadOnly = modifiers.Contains("readonly");
                        
                        currentType.Fields.Add(field);
                    }
                    continue;
                }
                
                // Parse property
                if (inPropertiesSection)
                {
                    var propMatch = PropertyPattern.Match(line);
                    if (propMatch.Success)
                    {
                        var accessors = propMatch.Groups[5].Value;
                        var prop = new PropertyDefinition
                        {
                            Visibility = propMatch.Groups[1].Value.Trim(),
                            PropertyType = propMatch.Groups[3].Value.Trim(),
                            Name = propMatch.Groups[4].Value.Trim(),
                            HasGetter = accessors.Contains("get"),
                            HasSetter = accessors.Contains("set")
                        };
                        
                        currentType.Properties.Add(prop);
                    }
                    continue;
                }
                
                // Parse method
                if (inMethodsSection)
                {
                    var methodSigMatch = MethodSigPattern.Match(line);
                    if (methodSigMatch.Success)
                    {
                        var method = new MethodDefinition
                        {
                            Visibility = methodSigMatch.Groups[1].Value.Trim(),
                            ReturnType = methodSigMatch.Groups[3].Value.Trim(),
                            Name = methodSigMatch.Groups[4].Value.Trim(),
                            Rva = pendingRva,
                            Va = pendingVa
                        };
                        
                        var modifiers = methodSigMatch.Groups[2].Value;
                        method.IsStatic = modifiers.Contains("static");
                        method.IsVirtual = modifiers.Contains("virtual");
                        method.IsAbstract = modifiers.Contains("abstract");
                        method.IsOverride = modifiers.Contains("override");
                        
                        // Parse parameters
                        var paramsStr = methodSigMatch.Groups[5].Value.Trim();
                        if (!string.IsNullOrEmpty(paramsStr))
                        {
                            method.Parameters = ParseParameters(paramsStr);
                        }
                        
                        currentType.Methods.Add(method);
                        
                        pendingRva = null;
                        pendingVa = null;
                    }
                }
            }
            
            // Add last type
            if (currentType != null)
            {
                AddType(currentType);
            }
        }
        
        private List<ParameterDefinition> ParseParameters(string paramsStr)
        {
            var result = new List<ParameterDefinition>();
            
            if (string.IsNullOrWhiteSpace(paramsStr))
                return result;
            
            // Simple split by comma (doesn't handle nested generics perfectly but works for most cases)
            var depth = 0;
            var current = "";
            
            foreach (var c in paramsStr)
            {
                if (c == '<' || c == '[')
                    depth++;
                else if (c == '>' || c == ']')
                    depth--;
                else if (c == ',' && depth == 0)
                {
                    AddParameter(result, current.Trim());
                    current = "";
                    continue;
                }
                current += c;
            }
            
            if (!string.IsNullOrWhiteSpace(current))
            {
                AddParameter(result, current.Trim());
            }
            
            return result;
        }
        
        private void AddParameter(List<ParameterDefinition> list, string param)
        {
            if (string.IsNullOrWhiteSpace(param))
                return;
            
            // Format: "Type name" or just "Type"
            var lastSpace = param.LastIndexOf(' ');
            if (lastSpace > 0)
            {
                list.Add(new ParameterDefinition
                {
                    ParameterType = param.Substring(0, lastSpace).Trim(),
                    Name = param.Substring(lastSpace + 1).Trim()
                });
            }
            else
            {
                list.Add(new ParameterDefinition
                {
                    ParameterType = param,
                    Name = "arg" + list.Count
                });
            }
        }
        
        private void AddType(TypeDefinition type)
        {
            _types.Add(type);
            
            // Index by simple name (may overwrite duplicates)
            _typesByName[type.Name] = type;
            
            // Index by full name
            var fullName = string.IsNullOrEmpty(type.Namespace)
                ? type.Name
                : $"{type.Namespace}.{type.Name}";
            _typesByFullName[fullName] = type;
        }
        
        private bool HasMatchingMember(TypeDefinition type, string lowerQuery)
        {
            return type.Fields.Any(f => 
                    f.Name.ToLowerInvariant().Contains(lowerQuery) ||
                    (f.FriendlyName?.ToLowerInvariant().Contains(lowerQuery) ?? false)) ||
                type.Properties.Any(p => 
                    p.Name.ToLowerInvariant().Contains(lowerQuery) ||
                    (p.FriendlyName?.ToLowerInvariant().Contains(lowerQuery) ?? false)) ||
                type.Methods.Any(m => 
                    m.Name.ToLowerInvariant().Contains(lowerQuery) ||
                    (m.FriendlyName?.ToLowerInvariant().Contains(lowerQuery) ?? false));
        }
    }
}
