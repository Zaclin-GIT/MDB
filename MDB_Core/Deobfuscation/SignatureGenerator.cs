using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace GameSDK.Deobfuscation
{
    /// <summary>
    /// Generates stable signatures for IL2CPP types that persist across game updates.
    /// Signatures are based on structural features rather than names or RVAs.
    /// </summary>
    public static class SignatureGenerator
    {
        /// <summary>
        /// Generates a signature for a type based on its structure.
        /// </summary>
        public static string GenerateTypeSignature(TypeDefinition type)
        {
            var sb = new StringBuilder();
            
            // 1. Class modifiers and kind
            sb.Append(type.IsSealed ? "sealed:" : "");
            sb.Append(type.IsAbstract ? "abstract:" : "");
            sb.Append(type.Kind);
            
            // 2. Base type (use base type's field count as identifier if obfuscated)
            if (!string.IsNullOrEmpty(type.BaseType))
            {
                sb.Append(":");
                sb.Append(type.BaseType);
            }
            
            // 3. Field fingerprint (sorted by offset for stability)
            var fieldFingerprint = GenerateFieldFingerprint(type.Fields);
            if (!string.IsNullOrEmpty(fieldFingerprint))
            {
                sb.Append("|");
                sb.Append(fieldFingerprint);
            }
            
            // 4. Method parameter pattern fingerprint
            var methodFingerprint = GenerateMethodFingerprint(type.Methods);
            if (!string.IsNullOrEmpty(methodFingerprint))
            {
                sb.Append("|");
                sb.Append(methodFingerprint);
            }
            
            // 5. Property count and types
            var propFingerprint = GeneratePropertyFingerprint(type.Properties);
            if (!string.IsNullOrEmpty(propFingerprint))
            {
                sb.Append("|");
                sb.Append(propFingerprint);
            }
            
            return sb.ToString();
        }
        
        /// <summary>
        /// Generates a signature for a field based on its position and type.
        /// </summary>
        public static string GenerateFieldSignature(TypeDefinition parentType, FieldDefinition field)
        {
            // Field signature: parent signature hash + field offset + type
            var parentSig = GenerateTypeSignature(parentType);
            var parentHash = ComputeShortHash(parentSig);
            
            return $"{parentHash}|f:{NormalizeTypeName(field.FieldType)}@{field.Offset}";
        }
        
        /// <summary>
        /// Generates a signature for a method based on its parameters and return type.
        /// </summary>
        public static string GenerateMethodSignature(TypeDefinition parentType, MethodDefinition method)
        {
            var parentSig = GenerateTypeSignature(parentType);
            var parentHash = ComputeShortHash(parentSig);
            
            var paramTypes = string.Join(",", method.Parameters.Select(p => NormalizeTypeName(p.ParameterType)));
            var returnType = NormalizeTypeName(method.ReturnType);
            
            return $"{parentHash}|m:{returnType}({paramTypes})@{method.ParameterCount}";
        }
        
        /// <summary>
        /// Generates a signature for a property.
        /// </summary>
        public static string GeneratePropertySignature(TypeDefinition parentType, PropertyDefinition property)
        {
            var parentSig = GenerateTypeSignature(parentType);
            var parentHash = ComputeShortHash(parentSig);
            
            var propType = NormalizeTypeName(property.PropertyType);
            var accessors = "";
            if (property.HasGetter) accessors += "g";
            if (property.HasSetter) accessors += "s";
            
            return $"{parentHash}|p:{propType}[{accessors}]";
        }
        
        /// <summary>
        /// Creates a fingerprint from field layout (offset + type pattern).
        /// </summary>
        private static string GenerateFieldFingerprint(List<FieldDefinition> fields)
        {
            if (fields == null || fields.Count == 0)
                return "";
            
            // Sort by offset and take key fields for fingerprint
            var sortedFields = fields
                .Where(f => !f.IsStatic && !string.IsNullOrEmpty(f.Offset))
                .OrderBy(f => ParseOffset(f.Offset))
                .Take(10) // Top 10 instance fields for fingerprint
                .ToList();
            
            if (sortedFields.Count == 0)
                return "";
            
            var parts = sortedFields.Select(f => $"{NormalizeTypeName(f.FieldType)}@{f.Offset}");
            return "F:" + string.Join(";", parts);
        }
        
        /// <summary>
        /// Creates a fingerprint from method parameter patterns.
        /// </summary>
        private static string GenerateMethodFingerprint(List<MethodDefinition> methods)
        {
            if (methods == null || methods.Count == 0)
                return "";
            
            // Count methods by parameter signature pattern
            var patterns = methods
                .Where(m => !m.Name.StartsWith(".")) // Exclude constructors
                .GroupBy(m => $"{NormalizeTypeName(m.ReturnType)}({m.ParameterCount})")
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => $"{g.Key}x{g.Count()}");
            
            var result = string.Join(";", patterns);
            return string.IsNullOrEmpty(result) ? "" : "M:" + result;
        }
        
        /// <summary>
        /// Creates a fingerprint from property types.
        /// </summary>
        private static string GeneratePropertyFingerprint(List<PropertyDefinition> properties)
        {
            if (properties == null || properties.Count == 0)
                return "";
            
            var typeCount = properties
                .GroupBy(p => NormalizeTypeName(p.PropertyType))
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => $"{g.Key}x{g.Count()}");
            
            var result = string.Join(";", typeCount);
            return string.IsNullOrEmpty(result) ? "" : "P:" + result;
        }
        
        /// <summary>
        /// Normalizes a type name by stripping generic parameters and keeping base type.
        /// Obfuscated names are kept as-is since they're part of the structure.
        /// </summary>
        private static string NormalizeTypeName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return "void";
            
            // Remove generic backtick notation (List`1 -> List)
            var backtickIdx = typeName.IndexOf('`');
            if (backtickIdx > 0)
                typeName = typeName.Substring(0, backtickIdx);
            
            // Remove array notation for fingerprint (keep [] count)
            var arrayDepth = typeName.Count(c => c == '[');
            typeName = typeName.Replace("[]", "").Replace("[", "").Replace("]", "");
            if (arrayDepth > 0)
                typeName += $"[{arrayDepth}]";
            
            return typeName.Trim();
        }
        
        /// <summary>
        /// Parses a hex offset string like "0x5E0" to an integer.
        /// </summary>
        private static int ParseOffset(string offset)
        {
            if (string.IsNullOrEmpty(offset))
                return 0;
            
            offset = offset.Trim();
            if (offset.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                offset = offset.Substring(2);
            
            if (int.TryParse(offset, System.Globalization.NumberStyles.HexNumber, null, out int result))
                return result;
            
            return 0;
        }
        
        /// <summary>
        /// Computes a short hash of a string for use in composite signatures.
        /// </summary>
        private static string ComputeShortHash(string input)
        {
            using (var md5 = MD5.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(input);
                var hash = md5.ComputeHash(bytes);
                // Take first 8 chars of hex hash
                return BitConverter.ToString(hash).Replace("-", "").Substring(0, 8);
            }
        }
        
        /// <summary>
        /// Computes a similarity score between two signatures (0.0 to 1.0).
        /// Used for runtime verification of mappings.
        /// </summary>
        public static double ComputeSignatureSimilarity(string sig1, string sig2)
        {
            if (string.IsNullOrEmpty(sig1) || string.IsNullOrEmpty(sig2))
                return 0.0;
            
            if (sig1 == sig2)
                return 1.0;
            
            // Split into components and compare
            var parts1 = sig1.Split('|');
            var parts2 = sig2.Split('|');
            
            int matchCount = 0;
            int totalParts = Math.Max(parts1.Length, parts2.Length);
            
            foreach (var part in parts1)
            {
                if (parts2.Contains(part))
                    matchCount++;
            }
            
            return (double)matchCount / totalParts;
        }
    }
    
    // Data model classes for parsed dump.cs content
    
    public class TypeDefinition
    {
        public string Dll { get; set; }
        public string Namespace { get; set; }
        public string Name { get; set; }
        public string Kind { get; set; } // class, struct, enum, interface
        public string BaseType { get; set; }
        public string Visibility { get; set; }
        public bool IsSealed { get; set; }
        public bool IsAbstract { get; set; }
        public bool IsStatic { get; set; }
        public List<FieldDefinition> Fields { get; set; } = new List<FieldDefinition>();
        public List<PropertyDefinition> Properties { get; set; } = new List<PropertyDefinition>();
        public List<MethodDefinition> Methods { get; set; } = new List<MethodDefinition>();
        
        // Computed signature (cached)
        private string _signature;
        public string Signature => _signature ?? (_signature = SignatureGenerator.GenerateTypeSignature(this));
        
        // Friendly name from mapping (null if not mapped)
        public string FriendlyName { get; set; }
        
        public string DisplayName => FriendlyName ?? Name;
        
        public override string ToString() => $"{Namespace}.{Name}";
    }
    
    public class FieldDefinition
    {
        public string Name { get; set; }
        public string FieldType { get; set; }
        public string Visibility { get; set; }
        public bool IsStatic { get; set; }
        public bool IsConst { get; set; }
        public bool IsReadOnly { get; set; }
        public string Offset { get; set; } // e.g., "0x5E0"
        public string Value { get; set; } // For const/enum values
        
        // Friendly name from mapping
        public string FriendlyName { get; set; }
        
        public string DisplayName => FriendlyName ?? Name;
    }
    
    public class PropertyDefinition
    {
        public string Name { get; set; }
        public string PropertyType { get; set; }
        public bool HasGetter { get; set; }
        public bool HasSetter { get; set; }
        public string Visibility { get; set; }
        
        // Friendly name from mapping
        public string FriendlyName { get; set; }
        
        public string DisplayName => FriendlyName ?? Name;
    }
    
    public class MethodDefinition
    {
        public string Name { get; set; }
        public string ReturnType { get; set; }
        public string Visibility { get; set; }
        public bool IsStatic { get; set; }
        public bool IsVirtual { get; set; }
        public bool IsAbstract { get; set; }
        public bool IsOverride { get; set; }
        public string Rva { get; set; } // e.g., "0x52f1e0"
        public string Va { get; set; }
        public List<ParameterDefinition> Parameters { get; set; } = new List<ParameterDefinition>();
        
        public int ParameterCount => Parameters?.Count ?? 0;
        
        // Friendly name from mapping
        public string FriendlyName { get; set; }
        
        public string DisplayName => FriendlyName ?? Name;
    }
    
    public class ParameterDefinition
    {
        public string Name { get; set; }
        public string ParameterType { get; set; }
    }
}
