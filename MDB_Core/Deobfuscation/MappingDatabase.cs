// ==============================
// MappingDatabase - JSON-persisted deobfuscation mappings
// ==============================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace GameSDK.Deobfuscation
{
    /// <summary>Type of symbol being mapped.</summary>
    public enum SymbolType
    {
        Type = 0,
        Field = 1,
        Property = 2,
        Method = 3
    }

    /// <summary>
    /// A single deobfuscation mapping with multi-layer signatures.
    /// </summary>
    [DataContract]
    public class SymbolMapping
    {
        /// <summary>Current obfuscated name (e.g. "FKALGHJIADI").</summary>
        [DataMember] public string ObfuscatedName { get; set; }

        /// <summary>Human-readable name (e.g. "PlayerManager").</summary>
        [DataMember] public string FriendlyName { get; set; }

        /// <summary>Symbol type.</summary>
        [DataMember] public SymbolType SymbolType { get; set; }

        /// <summary>Assembly/image name (e.g. "Assembly-CSharp.dll").</summary>
        [DataMember] public string Assembly { get; set; }

        /// <summary>Namespace.</summary>
        [DataMember] public string Namespace { get; set; }

        /// <summary>For members: the parent type's obfuscated name.</summary>
        [DataMember] public string ParentType { get; set; }

        // --- Multi-layer signatures ---

        /// <summary>
        /// Layer A: Structural fingerprint with normalized types.
        /// Obfuscated type references replaced with ?OBF? placeholders.
        /// </summary>
        [DataMember] public string StructuralSignature { get; set; }

        /// <summary>
        /// Layer B: Byte-array pattern of method body (IDA-style with wildcards).
        /// Only populated for methods. Format: "48 89 5C 24 ?? 48 89 74 24 ??"
        /// </summary>
        [DataMember] public string ByteSignature { get; set; }

        /// <summary>
        /// Layer C: RVA offset from GameAssembly.dll base (for methods).
        /// Changes every build but useful as a quick-match hint.
        /// </summary>
        [DataMember] public string Rva { get; set; }

        /// <summary>When this mapping was last created or verified.</summary>
        [DataMember] public DateTime LastUpdated { get; set; }

        /// <summary>Optional user notes.</summary>
        [DataMember] public string Notes { get; set; }

        /// <summary>Confidence from last verification (0.0-1.0).</summary>
        [DataMember] public double Confidence { get; set; } = 1.0;
    }

    /// <summary>
    /// JSON-persisted database of deobfuscation mappings with triple indexing.
    /// </summary>
    public class MappingDatabase
    {
        private readonly string _filePath;
        private List<SymbolMapping> _mappings = new List<SymbolMapping>();

        // Indices
        private Dictionary<string, SymbolMapping> _byObfuscatedName = new Dictionary<string, SymbolMapping>(StringComparer.Ordinal);
        private Dictionary<string, SymbolMapping> _byFriendlyName = new Dictionary<string, SymbolMapping>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, SymbolMapping> _byStructuralSig = new Dictionary<string, SymbolMapping>(StringComparer.Ordinal);

        public string FilePath => _filePath;
        public int Count => _mappings.Count;
        public IReadOnlyList<SymbolMapping> All => _mappings;

        public event Action OnChanged;

        public MappingDatabase(string filePath)
        {
            _filePath = filePath;
        }

        // ===== Persistence =====

        public bool Load()
        {
            try
            {
                if (!File.Exists(_filePath)) return false;

                var json = File.ReadAllText(_filePath, Encoding.UTF8);
                var ser = new DataContractJsonSerializer(typeof(List<SymbolMapping>));
                using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    var list = (List<SymbolMapping>)ser.ReadObject(ms);
                    _mappings.Clear();
                    ClearIndices();
                    if (list != null)
                    {
                        foreach (var m in list)
                        {
                            _mappings.Add(m);
                            IndexMapping(m);
                        }
                    }
                }
                return true;
            }
            catch { return false; }
        }

        public bool Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var ser = new DataContractJsonSerializer(typeof(List<SymbolMapping>));
                using (var ms = new MemoryStream())
                {
                    using (var writer = System.Runtime.Serialization.Json.JsonReaderWriterFactory
                        .CreateJsonWriter(ms, Encoding.UTF8, true, true, "  "))
                    {
                        ser.WriteObject(writer, _mappings);
                        writer.Flush();
                    }
                    File.WriteAllText(_filePath, Encoding.UTF8.GetString(ms.ToArray()), Encoding.UTF8);
                }
                return true;
            }
            catch { return false; }
        }

        // ===== CRUD =====

        public void AddOrUpdate(SymbolMapping mapping)
        {
            mapping.LastUpdated = DateTime.Now;

            var existing = GetByObfuscatedName(mapping.ObfuscatedName);
            if (existing != null)
            {
                RemoveFromIndices(existing);
                _mappings.Remove(existing);
            }

            _mappings.Add(mapping);
            IndexMapping(mapping);
            OnChanged?.Invoke();
        }

        public bool Remove(string obfuscatedName)
        {
            var m = GetByObfuscatedName(obfuscatedName);
            if (m == null) return false;
            RemoveFromIndices(m);
            _mappings.Remove(m);
            OnChanged?.Invoke();
            return true;
        }

        // ===== Lookup =====

        public SymbolMapping GetByObfuscatedName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            _byObfuscatedName.TryGetValue(name, out var m);
            return m;
        }

        public SymbolMapping GetByFriendlyName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            _byFriendlyName.TryGetValue(name, out var m);
            return m;
        }

        public SymbolMapping GetByStructuralSignature(string sig)
        {
            if (string.IsNullOrEmpty(sig)) return null;
            _byStructuralSig.TryGetValue(sig, out var m);
            return m;
        }

        public IEnumerable<SymbolMapping> GetBySymbolType(SymbolType type)
        {
            return _mappings.Where(m => m.SymbolType == type);
        }

        public IEnumerable<SymbolMapping> GetMembers(string parentObfuscatedName)
        {
            return _mappings.Where(m => m.ParentType == parentObfuscatedName);
        }

        /// <summary>Resolve friendly name for display, returns original if unmapped.</summary>
        public string Resolve(string obfuscatedName)
        {
            var m = GetByObfuscatedName(obfuscatedName);
            return m?.FriendlyName ?? obfuscatedName;
        }

        // ===== Index management =====

        private void IndexMapping(SymbolMapping m)
        {
            if (!string.IsNullOrEmpty(m.ObfuscatedName))
                _byObfuscatedName[m.ObfuscatedName] = m;
            if (!string.IsNullOrEmpty(m.FriendlyName))
                _byFriendlyName[m.FriendlyName] = m;
            if (!string.IsNullOrEmpty(m.StructuralSignature))
                _byStructuralSig[m.StructuralSignature] = m;
        }

        private void RemoveFromIndices(SymbolMapping m)
        {
            if (!string.IsNullOrEmpty(m.ObfuscatedName))
                _byObfuscatedName.Remove(m.ObfuscatedName);
            if (!string.IsNullOrEmpty(m.FriendlyName))
                _byFriendlyName.Remove(m.FriendlyName);
            if (!string.IsNullOrEmpty(m.StructuralSignature))
                _byStructuralSig.Remove(m.StructuralSignature);
        }

        private void ClearIndices()
        {
            _byObfuscatedName.Clear();
            _byFriendlyName.Clear();
            _byStructuralSig.Clear();
        }
    }
}
