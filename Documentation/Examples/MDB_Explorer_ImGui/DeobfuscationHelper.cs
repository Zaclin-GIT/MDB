// ==============================
// DeobfuscationHelper - Translates obfuscated names to friendly names
// ==============================
// Integrates with MappingDatabase to provide friendly name lookup
// for display in the Explorer UI while keeping IL2CPP internals using
// the original obfuscated names.

using System;
using System.IO;
using GameSDK.Deobfuscation;
using GameSDK.ModHost;

namespace MDB.Explorer.ImGui
{
    /// <summary>
    /// Provides deobfuscation name translation for the Explorer UI.
    /// </summary>
    public static class DeobfuscationHelper
    {
        private const string LOG_TAG = "DeobfuscationHelper";
        
        private static MappingDatabase _database;
        private static bool _initialized;
        private static bool _enabled = true;
        
        /// <summary>
        /// Whether deobfuscation is enabled.
        /// </summary>
        public static bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }
        
        /// <summary>
        /// Number of loaded mappings.
        /// </summary>
        public static int MappingCount => _database?.Count ?? 0;
        
        /// <summary>
        /// Whether the helper is initialized with a mapping database.
        /// </summary>
        public static bool IsInitialized => _initialized && _database != null;
        
        /// <summary>
        /// Initialize the deobfuscation helper with a mappings file.
        /// </summary>
        public static bool Initialize(string mappingsPath = null)
        {
            if (_initialized) return true;
            
            try
            {
                // Get the folder where this assembly (the mod DLL) is located
                // Structure: Game/MDB/Mods/MDB_Explorer.dll
                var assemblyLocation = typeof(DeobfuscationHelper).Assembly.Location;
                var modsFolder = !string.IsNullOrEmpty(assemblyLocation) 
                    ? Path.GetDirectoryName(assemblyLocation) 
                    : null;
                
                // MDB folder is parent of Mods folder
                var mdbFolder = !string.IsNullOrEmpty(modsFolder) 
                    ? Path.GetDirectoryName(modsFolder) 
                    : null;
                
                // Game folder is parent of MDB folder
                var gameFolder = !string.IsNullOrEmpty(mdbFolder) 
                    ? Path.GetDirectoryName(mdbFolder) 
                    : null;
                
                // Default path for creating new mappings file
                var defaultMappingsPath = !string.IsNullOrEmpty(mdbFolder)
                    ? Path.Combine(mdbFolder, "Dump", "mappings.json")
                    : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MDB", "Dump", "mappings.json");
                
                // Try default paths if not specified
                if (string.IsNullOrEmpty(mappingsPath))
                {
                    // Try common locations based on folder structure:
                    // Game/MDB/Dump/dump.cs, wrapper_generator.py, mappings.json
                    // Game/MDB/Mods/MDB_Explorer.dll
                    var possiblePaths = new System.Collections.Generic.List<string>();
                    
                    // 1. MDB/Dump folder (where dump.cs and wrapper_generator.py are)
                    if (!string.IsNullOrEmpty(mdbFolder))
                    {
                        possiblePaths.Add(Path.Combine(mdbFolder, "Dump", "mappings.json"));
                    }
                    
                    // 2. MDB folder root
                    if (!string.IsNullOrEmpty(mdbFolder))
                    {
                        possiblePaths.Add(Path.Combine(mdbFolder, "mappings.json"));
                    }
                    
                    // 3. Same folder as the mod DLL (Mods folder)
                    if (!string.IsNullOrEmpty(modsFolder))
                    {
                        possiblePaths.Add(Path.Combine(modsFolder, "mappings.json"));
                    }
                    
                    // 4. Game root folder
                    if (!string.IsNullOrEmpty(gameFolder))
                    {
                        possiblePaths.Add(Path.Combine(gameFolder, "mappings.json"));
                    }
                    
                    // 5. Fallback to AppDomain base (may be game folder)
                    possiblePaths.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MDB", "Dump", "mappings.json"));
                    possiblePaths.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mappings.json"));
                    
                    foreach (var path in possiblePaths)
                    {
                        if (File.Exists(path))
                        {
                            mappingsPath = path;
                            ModLogger.LogInternal(LOG_TAG, $"[INFO] Found mappings at: {path}");
                            break;
                        }
                    }
                }
                
                // If no mappings file found, create an empty one at the default location
                if (string.IsNullOrEmpty(mappingsPath) || !File.Exists(mappingsPath))
                {
                    mappingsPath = defaultMappingsPath;
                    
                    // Ensure directory exists
                    var dir = Path.GetDirectoryName(mappingsPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    
                    // Create empty mappings file
                    File.WriteAllText(mappingsPath, "[]");
                    ModLogger.LogInternal(LOG_TAG, $"[INFO] Created new mappings file at: {mappingsPath}");
                }
                
                _database = new MappingDatabase(mappingsPath);
                _database.Load();
                
                _initialized = true;
                ModLogger.LogInternal(LOG_TAG, $"[INFO] Loaded {_database.Count} deobfuscation mappings from {mappingsPath}");
                
                return true;
            }
            catch (Exception ex)
            {
                ModLogger.LogInternal(LOG_TAG, $"[ERROR] Failed to initialize: {ex.Message}");
                _initialized = true;
                return false;
            }
        }
        
        /// <summary>
        /// Reload mappings from file.
        /// </summary>
        public static void Reload()
        {
            if (_database != null)
            {
                _database.Load();
                ModLogger.LogInternal(LOG_TAG, $"[INFO] Reloaded {_database.Count} mappings");
            }
        }
        
        /// <summary>
        /// Get the display name for a type (class name).
        /// Returns the friendly name if mapped, otherwise the original name.
        /// </summary>
        public static string GetTypeName(string obfuscatedName)
        {
            if (!_enabled || _database == null || string.IsNullOrEmpty(obfuscatedName))
                return obfuscatedName;
            
            var mapping = _database.GetByObfuscatedName(obfuscatedName);
            if (mapping != null && !string.IsNullOrEmpty(mapping.FriendlyName))
            {
                return mapping.FriendlyName;
            }
            
            return obfuscatedName;
        }
        
        /// <summary>
        /// Get the display name for a type, with optional suffix showing the obfuscated name.
        /// </summary>
        public static string GetTypeNameWithHint(string obfuscatedName, bool showObfuscatedHint = true)
        {
            if (!_enabled || _database == null || string.IsNullOrEmpty(obfuscatedName))
                return obfuscatedName;
            
            var mapping = _database.GetByObfuscatedName(obfuscatedName);
            if (mapping != null && !string.IsNullOrEmpty(mapping.FriendlyName))
            {
                if (showObfuscatedHint && IsObfuscatedName(obfuscatedName))
                {
                    return $"{mapping.FriendlyName} [{obfuscatedName}]";
                }
                return mapping.FriendlyName;
            }
            
            return obfuscatedName;
        }
        
        /// <summary>
        /// Get the display name for a field.
        /// Looks up "TypeName.FieldName" pattern.
        /// </summary>
        public static string GetFieldName(string typeName, string fieldName)
        {
            if (!_enabled || _database == null || string.IsNullOrEmpty(fieldName))
                return fieldName;
            
            // Try fully qualified lookup first
            if (!string.IsNullOrEmpty(typeName))
            {
                var mapping = _database.GetByObfuscatedName($"{typeName}.{fieldName}");
                if (mapping != null && !string.IsNullOrEmpty(mapping.FriendlyName))
                {
                    return mapping.FriendlyName;
                }
            }
            
            // Try just the field name
            var fieldMapping = _database.GetByObfuscatedName(fieldName);
            if (fieldMapping != null && 
                fieldMapping.SymbolType == SymbolType.Field && 
                !string.IsNullOrEmpty(fieldMapping.FriendlyName))
            {
                return fieldMapping.FriendlyName;
            }
            
            return fieldName;
        }
        
        /// <summary>
        /// Get the display name for a property.
        /// </summary>
        public static string GetPropertyName(string typeName, string propertyName)
        {
            if (!_enabled || _database == null || string.IsNullOrEmpty(propertyName))
                return propertyName;
            
            // Try fully qualified lookup first
            if (!string.IsNullOrEmpty(typeName))
            {
                var mapping = _database.GetByObfuscatedName($"{typeName}.{propertyName}");
                if (mapping != null && !string.IsNullOrEmpty(mapping.FriendlyName))
                {
                    return mapping.FriendlyName;
                }
            }
            
            // Try just the property name
            var propMapping = _database.GetByObfuscatedName(propertyName);
            if (propMapping != null && 
                propMapping.SymbolType == SymbolType.Property && 
                !string.IsNullOrEmpty(propMapping.FriendlyName))
            {
                return propMapping.FriendlyName;
            }
            
            return propertyName;
        }
        
        /// <summary>
        /// Get the display name for a method.
        /// </summary>
        public static string GetMethodName(string typeName, string methodName)
        {
            if (!_enabled || _database == null || string.IsNullOrEmpty(methodName))
                return methodName;
            
            // Try fully qualified lookup first
            if (!string.IsNullOrEmpty(typeName))
            {
                var mapping = _database.GetByObfuscatedName($"{typeName}.{methodName}");
                if (mapping != null && !string.IsNullOrEmpty(mapping.FriendlyName))
                {
                    return mapping.FriendlyName;
                }
            }
            
            // Try just the method name
            var methodMapping = _database.GetByObfuscatedName(methodName);
            if (methodMapping != null && 
                methodMapping.SymbolType == SymbolType.Method && 
                !string.IsNullOrEmpty(methodMapping.FriendlyName))
            {
                return methodMapping.FriendlyName;
            }
            
            return methodName;
        }
        
        /// <summary>
        /// Check if a name appears to be obfuscated (Beebyte pattern: 8-15 uppercase letters).
        /// </summary>
        public static bool IsObfuscatedName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (name.Length < 8 || name.Length > 15) return false;
            
            foreach (char c in name)
            {
                if (c < 'A' || c > 'Z') return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Get the mapping database for direct access.
        /// </summary>
        public static MappingDatabase GetDatabase() => _database;
    }
}
