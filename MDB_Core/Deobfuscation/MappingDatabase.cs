using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace GameSDK.Deobfuscation
{
    /// <summary>
    /// The type of symbol being mapped.
    /// </summary>
    public enum SymbolType
    {
        Type,
        Field,
        Property,
        Method
    }
    
    /// <summary>
    /// Represents a mapping from an obfuscated name to a friendly name.
    /// </summary>
    [DataContract]
    public class SymbolMapping
    {
        /// <summary>
        /// The obfuscated name as it appears in IL2CPP (e.g., "FKALGHJIADI").
        /// </summary>
        [DataMember]
        public string ObfuscatedName { get; set; }
        
        /// <summary>
        /// The human-readable name (e.g., "Player").
        /// </summary>
        [DataMember]
        public string FriendlyName { get; set; }
        
        /// <summary>
        /// Structural signature for re-identification after updates.
        /// </summary>
        [DataMember]
        public string Signature { get; set; }
        
        /// <summary>
        /// The type of symbol (Type, Field, Property, Method).
        /// </summary>
        [DataMember]
        public SymbolType SymbolType { get; set; }
        
        /// <summary>
        /// The namespace of this symbol (for types).
        /// </summary>
        [DataMember]
        public string Namespace { get; set; }
        
        /// <summary>
        /// For members, the parent type's obfuscated name.
        /// </summary>
        [DataMember]
        public string ParentTypeName { get; set; }
        
        /// <summary>
        /// Timestamp when this mapping was created/updated.
        /// </summary>
        [DataMember]
        public DateTime LastUpdated { get; set; }
        
        /// <summary>
        /// Optional notes about this mapping.
        /// </summary>
        [DataMember]
        public string Notes { get; set; }
        
        /// <summary>
        /// Confidence score from last runtime verification (0.0 to 1.0).
        /// </summary>
        [DataMember]
        public double VerificationScore { get; set; } = 1.0;
    }
    
    /// <summary>
    /// Database of obfuscated-to-friendly name mappings with JSON persistence.
    /// </summary>
    public class MappingDatabase
    {
        private readonly string _filePath;
        private Dictionary<string, SymbolMapping> _mappingsBySignature;
        private Dictionary<string, SymbolMapping> _mappingsByObfuscatedName;
        private Dictionary<string, SymbolMapping> _mappingsByFriendlyName;
        
        /// <summary>
        /// All mappings in the database.
        /// </summary>
        public IReadOnlyCollection<SymbolMapping> Mappings => _mappingsBySignature.Values;
        
        /// <summary>
        /// All mappings as an enumerable (for iteration).
        /// </summary>
        public IEnumerable<SymbolMapping> AllMappings => _mappingsBySignature.Values;
        
        /// <summary>
        /// Number of mappings in the database.
        /// </summary>
        public int Count => _mappingsBySignature.Count;
        
        /// <summary>
        /// Event fired when mappings change.
        /// </summary>
        public event Action OnMappingsChanged;
        
        public MappingDatabase(string filePath)
        {
            _filePath = filePath;
            _mappingsBySignature = new Dictionary<string, SymbolMapping>();
            _mappingsByObfuscatedName = new Dictionary<string, SymbolMapping>();
            _mappingsByFriendlyName = new Dictionary<string, SymbolMapping>();
        }
        
        /// <summary>
        /// Loads mappings from the JSON file.
        /// </summary>
        public bool Load()
        {
            try
            {
                if (!File.Exists(_filePath))
                {
                    return false;
                }
                
                var json = File.ReadAllText(_filePath);
                var serializer = new DataContractJsonSerializer(typeof(List<SymbolMapping>));
                using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    var mappings = (List<SymbolMapping>)serializer.ReadObject(ms);
                    
                    _mappingsBySignature.Clear();
                    _mappingsByObfuscatedName.Clear();
                    _mappingsByFriendlyName.Clear();
                    
                    if (mappings != null)
                    {
                        foreach (var mapping in mappings)
                        {
                            AddToIndices(mapping);
                        }
                    }
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MappingDatabase] Error loading mappings: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Saves mappings to the JSON file.
        /// </summary>
        public bool Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                var serializer = new DataContractJsonSerializer(typeof(List<SymbolMapping>));
                using (var ms = new MemoryStream())
                {
                    serializer.WriteObject(ms, _mappingsBySignature.Values.ToList());
                    var json = Encoding.UTF8.GetString(ms.ToArray());
                    File.WriteAllText(_filePath, json);
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MappingDatabase] Error saving mappings: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Adds or updates a mapping.
        /// </summary>
        public void SetMapping(SymbolMapping mapping)
        {
            if (string.IsNullOrEmpty(mapping.Signature))
                throw new ArgumentException("Mapping must have a signature");
            
            // Remove old entries if updating
            if (_mappingsBySignature.TryGetValue(mapping.Signature, out var existing))
            {
                _mappingsByObfuscatedName.Remove(existing.ObfuscatedName);
                if (!string.IsNullOrEmpty(existing.FriendlyName))
                    _mappingsByFriendlyName.Remove(existing.FriendlyName);
            }
            
            mapping.LastUpdated = DateTime.Now;
            AddToIndices(mapping);
            
            OnMappingsChanged?.Invoke();
        }
        
        /// <summary>
        /// Removes a mapping by signature.
        /// </summary>
        public bool RemoveMapping(string signature)
        {
            if (_mappingsBySignature.TryGetValue(signature, out var mapping))
            {
                _mappingsBySignature.Remove(signature);
                _mappingsByObfuscatedName.Remove(mapping.ObfuscatedName);
                if (!string.IsNullOrEmpty(mapping.FriendlyName))
                    _mappingsByFriendlyName.Remove(mapping.FriendlyName);
                
                OnMappingsChanged?.Invoke();
                return true;
            }
            
            // Also try removing by obfuscated name
            if (_mappingsByObfuscatedName.TryGetValue(signature, out mapping))
            {
                _mappingsBySignature.Remove(mapping.Signature);
                _mappingsByObfuscatedName.Remove(mapping.ObfuscatedName);
                if (!string.IsNullOrEmpty(mapping.FriendlyName))
                    _mappingsByFriendlyName.Remove(mapping.FriendlyName);
                
                OnMappingsChanged?.Invoke();
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Gets a mapping by its signature.
        /// </summary>
        public SymbolMapping GetBySignature(string signature)
        {
            _mappingsBySignature.TryGetValue(signature, out var mapping);
            return mapping;
        }
        
        /// <summary>
        /// Gets a mapping by obfuscated name.
        /// </summary>
        public SymbolMapping GetByObfuscatedName(string obfuscatedName)
        {
            _mappingsByObfuscatedName.TryGetValue(obfuscatedName, out var mapping);
            return mapping;
        }
        
        /// <summary>
        /// Gets a mapping by friendly name.
        /// </summary>
        public SymbolMapping GetByFriendlyName(string friendlyName)
        {
            _mappingsByFriendlyName.TryGetValue(friendlyName, out var mapping);
            return mapping;
        }
        
        /// <summary>
        /// Resolves the current obfuscated name for a signature.
        /// Used after game updates to find the new obfuscated name.
        /// </summary>
        public string ResolveObfuscatedName(string signature)
        {
            return GetBySignature(signature)?.ObfuscatedName;
        }
        
        /// <summary>
        /// Resolves the friendly name for an obfuscated name.
        /// </summary>
        public string ResolveFriendlyName(string obfuscatedName)
        {
            return GetByObfuscatedName(obfuscatedName)?.FriendlyName;
        }
        
        /// <summary>
        /// Gets all mappings for a specific symbol type.
        /// </summary>
        public IEnumerable<SymbolMapping> GetMappingsByType(SymbolType type)
        {
            return _mappingsBySignature.Values.Where(m => m.SymbolType == type);
        }
        
        /// <summary>
        /// Gets all mappings for members of a specific type.
        /// </summary>
        public IEnumerable<SymbolMapping> GetMemberMappings(string parentTypeName)
        {
            return _mappingsBySignature.Values.Where(m => m.ParentTypeName == parentTypeName);
        }
        
        /// <summary>
        /// Updates the obfuscated name for a signature (used after re-identification).
        /// </summary>
        public void UpdateObfuscatedName(string signature, string newObfuscatedName, double verificationScore = 1.0)
        {
            if (_mappingsBySignature.TryGetValue(signature, out var mapping))
            {
                // Remove old obfuscated name index
                _mappingsByObfuscatedName.Remove(mapping.ObfuscatedName);
                
                // Update
                mapping.ObfuscatedName = newObfuscatedName;
                mapping.VerificationScore = verificationScore;
                mapping.LastUpdated = DateTime.Now;
                
                // Add new obfuscated name index
                _mappingsByObfuscatedName[newObfuscatedName] = mapping;
                
                OnMappingsChanged?.Invoke();
            }
        }
        
        /// <summary>
        /// Creates a mapping from a TypeDefinition.
        /// </summary>
        public SymbolMapping CreateTypeMapping(TypeDefinition type, string friendlyName)
        {
            return new SymbolMapping
            {
                ObfuscatedName = type.Name,
                FriendlyName = friendlyName,
                Signature = type.Signature,
                SymbolType = SymbolType.Type,
                ParentTypeName = null,
                LastUpdated = DateTime.Now,
                VerificationScore = 1.0
            };
        }
        
        /// <summary>
        /// Creates a mapping from a FieldDefinition.
        /// </summary>
        public SymbolMapping CreateFieldMapping(TypeDefinition parentType, FieldDefinition field, string friendlyName)
        {
            return new SymbolMapping
            {
                ObfuscatedName = field.Name,
                FriendlyName = friendlyName,
                Signature = SignatureGenerator.GenerateFieldSignature(parentType, field),
                SymbolType = SymbolType.Field,
                ParentTypeName = parentType.Name,
                LastUpdated = DateTime.Now,
                VerificationScore = 1.0
            };
        }
        
        /// <summary>
        /// Creates a mapping from a PropertyDefinition.
        /// </summary>
        public SymbolMapping CreatePropertyMapping(TypeDefinition parentType, PropertyDefinition property, string friendlyName)
        {
            return new SymbolMapping
            {
                ObfuscatedName = property.Name,
                FriendlyName = friendlyName,
                Signature = SignatureGenerator.GeneratePropertySignature(parentType, property),
                SymbolType = SymbolType.Property,
                ParentTypeName = parentType.Name,
                LastUpdated = DateTime.Now,
                VerificationScore = 1.0
            };
        }
        
        /// <summary>
        /// Creates a mapping from a MethodDefinition.
        /// </summary>
        public SymbolMapping CreateMethodMapping(TypeDefinition parentType, MethodDefinition method, string friendlyName)
        {
            return new SymbolMapping
            {
                ObfuscatedName = method.Name,
                FriendlyName = friendlyName,
                Signature = SignatureGenerator.GenerateMethodSignature(parentType, method),
                SymbolType = SymbolType.Method,
                ParentTypeName = parentType.Name,
                LastUpdated = DateTime.Now,
                VerificationScore = 1.0
            };
        }
        
        private void AddToIndices(SymbolMapping mapping)
        {
            _mappingsBySignature[mapping.Signature] = mapping;
            _mappingsByObfuscatedName[mapping.ObfuscatedName] = mapping;
            if (!string.IsNullOrEmpty(mapping.FriendlyName))
            {
                _mappingsByFriendlyName[mapping.FriendlyName] = mapping;
            }
        }
    }
}
