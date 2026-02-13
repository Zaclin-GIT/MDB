// ==============================
// SignatureGenerator - Multi-layer stable signatures
// ==============================
// Layer A: Structural fingerprint (normalized — obfuscated names → ?OBF?)
// Layer B: Byte-array method body patterns (IDA-style with wildcards)
// Layer C: RVA offsets (quick hint, changes every build)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using GameSDK.ModHost;

namespace GameSDK.Deobfuscation
{
    /// <summary>
    /// Generates multi-layer signatures for IL2CPP types and members.
    /// Works against live runtime metadata via the bridge.
    /// </summary>
    public static class SignatureGenerator
    {
        private const string LOG_TAG = "SignatureGenerator";

        // ==============================
        // Layer A: Structural Signatures
        // ==============================

        /// <summary>
        /// Generate a structural fingerprint for a class.
        /// Obfuscated type references are normalized to ?OBF? so the signature
        /// survives when the obfuscator renames types.
        /// </summary>
        public static string GenerateTypeSignature(IntPtr klass)
        {
            if (klass == IntPtr.Zero) return "";

            var sb = new StringBuilder();

            // Class flags
            int flags = Il2CppBridge.mdb_class_get_flags(klass);
            bool isSealed = (flags & 0x100) != 0;     // TYPE_ATTRIBUTE_SEALED
            bool isAbstract = (flags & 0x80) != 0;     // TYPE_ATTRIBUTE_ABSTRACT
            bool isInterface = (flags & 0x20) != 0;     // TYPE_ATTRIBUTE_INTERFACE

            if (isSealed) sb.Append("sealed:");
            if (isAbstract) sb.Append("abstract:");
            if (isInterface) sb.Append("interface");
            else sb.Append("class");

            // Base type (normalized)
            IntPtr parent = Il2CppBridge.mdb_class_get_parent(klass);
            if (parent != IntPtr.Zero)
            {
                string baseName = MarshalString(Il2CppBridge.mdb_class_get_name(parent));
                sb.Append(":").Append(NormalizeName(baseName));
            }

            // Field fingerprint: non-static fields sorted by offset
            sb.Append("|");
            sb.Append(GenerateFieldFingerprint(klass));

            // Method fingerprint: parameter pattern counts
            sb.Append("|");
            sb.Append(GenerateMethodFingerprint(klass));

            // Property fingerprint: type pattern counts
            sb.Append("|");
            sb.Append(GeneratePropertyFingerprint(klass));

            return sb.ToString();
        }

        /// <summary>
        /// Generate a structural signature for a field within a class.
        /// Includes: static/instance mark, type, offset, ordinal position among
        /// same-type fields, total same-type count, and neighbor field types for context.
        /// </summary>
        public static string GenerateFieldSignature(IntPtr klass, IntPtr field)
        {
            if (klass == IntPtr.Zero || field == IntPtr.Zero) return "";

            string parentHash = ShortHash(GenerateTypeSignature(klass));
            int offset = Il2CppBridge.mdb_field_get_offset(field);
            bool isStatic = Il2CppBridge.mdb_field_is_static(field);
            string staticMark = isStatic ? "s" : "i";

            IntPtr fieldType = Il2CppBridge.mdb_field_get_type(field);
            string typeName = fieldType != IntPtr.Zero ? MarshalString(Il2CppBridge.mdb_type_get_name(fieldType)) : "?";
            string normType = NormalizeName(typeName);

            // Enumerate all fields to find ordinal position and neighbors
            int fieldCount = Il2CppBridge.mdb_class_get_field_count(klass);
            var allFields = new List<(IntPtr ptr, string type, int offset, bool isStatic)>();
            for (int i = 0; i < fieldCount; i++)
            {
                IntPtr f = Il2CppBridge.mdb_class_get_field_by_index(klass, i);
                if (f == IntPtr.Zero) continue;

                IntPtr ft = Il2CppBridge.mdb_field_get_type(f);
                string tn = ft != IntPtr.Zero ? MarshalString(Il2CppBridge.mdb_type_get_name(ft)) : "?";
                int fo = Il2CppBridge.mdb_field_get_offset(f);
                bool fs = Il2CppBridge.mdb_field_is_static(f);
                allFields.Add((f, NormalizeName(tn), fo, fs));
            }

            // Sort by offset for neighbor lookup
            allFields.Sort((a, b) => a.offset.CompareTo(b.offset));

            // Find this field's index in the sorted list
            int myIndex = -1;
            for (int i = 0; i < allFields.Count; i++)
            {
                if (allFields[i].ptr == field) { myIndex = i; break; }
            }

            // Ordinal among same-type fields (e.g. 2nd System.String field)
            int sameTypeOrdinal = 0;
            int sameTypeTotal = 0;
            for (int i = 0; i < allFields.Count; i++)
            {
                if (allFields[i].type == normType && allFields[i].isStatic == isStatic)
                {
                    sameTypeTotal++;
                    if (allFields[i].ptr == field)
                        sameTypeOrdinal = sameTypeTotal;
                }
            }

            // Neighbor context: types of prev/next fields by offset
            string prevType = myIndex > 0 ? allFields[myIndex - 1].type : "_";
            string nextType = myIndex >= 0 && myIndex < allFields.Count - 1 ? allFields[myIndex + 1].type : "_";

            return $"{parentHash}|f:{staticMark}:{normType}@0x{offset:X}[{sameTypeOrdinal}/{sameTypeTotal}]|ctx:{prevType},{nextType}|n:{allFields.Count}";
        }

        /// <summary>
        /// Generate a structural signature for a method within a class.
        /// </summary>
        public static string GenerateMethodSignature(IntPtr klass, IntPtr method)
        {
            if (klass == IntPtr.Zero || method == IntPtr.Zero) return "";

            string parentHash = ShortHash(GenerateTypeSignature(klass));

            // Return type
            IntPtr retType = Il2CppBridge.mdb_method_get_return_type(method);
            string retName = retType != IntPtr.Zero ? MarshalString(Il2CppBridge.mdb_type_get_name(retType)) : "void";

            // Parameters
            int paramCount = Il2CppBridge.mdb_method_get_param_count(method);
            var paramTypes = new List<string>();
            for (int i = 0; i < paramCount; i++)
            {
                IntPtr pt = Il2CppBridge.mdb_method_get_param_type(method, i);
                string pn = pt != IntPtr.Zero ? MarshalString(Il2CppBridge.mdb_type_get_name(pt)) : "?";
                paramTypes.Add(NormalizeName(pn));
            }

            // Method flags for disambiguation
            int mflags = Il2CppBridge.mdb_method_get_flags(method);
            bool isStatic = (mflags & 0x10) != 0;
            string staticMark = isStatic ? "s" : "i";

            return $"{parentHash}|m:{staticMark}:{NormalizeName(retName)}({string.Join(",", paramTypes)})";
        }

        /// <summary>
        /// Generate a structural signature for a property within a class.
        /// Includes: type, accessor info, ordinal among same-type properties,
        /// getter/setter parameter signatures, and neighbor property types.
        /// </summary>
        public static string GeneratePropertySignature(IntPtr klass, IntPtr property)
        {
            if (klass == IntPtr.Zero || property == IntPtr.Zero) return "";

            string parentHash = ShortHash(GenerateTypeSignature(klass));

            // Determine property type from getter return type
            IntPtr getter = Il2CppBridge.mdb_property_get_get_method(property);
            IntPtr setter = Il2CppBridge.mdb_property_get_set_method(property);

            string propType = "?";
            if (getter != IntPtr.Zero)
            {
                IntPtr retType = Il2CppBridge.mdb_method_get_return_type(getter);
                if (retType != IntPtr.Zero)
                    propType = MarshalString(Il2CppBridge.mdb_type_get_name(retType));
            }
            else if (setter != IntPtr.Zero)
            {
                IntPtr paramType = Il2CppBridge.mdb_method_get_param_type(setter, 0);
                if (paramType != IntPtr.Zero)
                    propType = MarshalString(Il2CppBridge.mdb_type_get_name(paramType));
            }

            string normPropType = NormalizeName(propType);

            string access = "";
            if (getter != IntPtr.Zero) access += "g";
            if (setter != IntPtr.Zero) access += "s";

            // Enumerate all properties to find ordinal position and neighbors
            int propCount = Il2CppBridge.mdb_class_get_property_count(klass);
            var allProps = new List<(IntPtr ptr, string type, string access)>();
            for (int i = 0; i < propCount; i++)
            {
                IntPtr p = Il2CppBridge.mdb_class_get_property_by_index(klass, i);
                if (p == IntPtr.Zero) continue;

                IntPtr pg = Il2CppBridge.mdb_property_get_get_method(p);
                IntPtr ps = Il2CppBridge.mdb_property_get_set_method(p);
                string pt = "?";
                if (pg != IntPtr.Zero)
                {
                    IntPtr rt = Il2CppBridge.mdb_method_get_return_type(pg);
                    if (rt != IntPtr.Zero) pt = NormalizeName(MarshalString(Il2CppBridge.mdb_type_get_name(rt)));
                }
                else if (ps != IntPtr.Zero)
                {
                    IntPtr ppt = Il2CppBridge.mdb_method_get_param_type(ps, 0);
                    if (ppt != IntPtr.Zero) pt = NormalizeName(MarshalString(Il2CppBridge.mdb_type_get_name(ppt)));
                }

                string pa = "";
                if (pg != IntPtr.Zero) pa += "g";
                if (ps != IntPtr.Zero) pa += "s";
                allProps.Add((p, pt, pa));
            }

            // Ordinal among same-type properties
            int sameTypeOrdinal = 0;
            int sameTypeTotal = 0;
            for (int i = 0; i < allProps.Count; i++)
            {
                if (allProps[i].type == normPropType)
                {
                    sameTypeTotal++;
                    if (allProps[i].ptr == property)
                        sameTypeOrdinal = sameTypeTotal;
                }
            }

            // Find this property's index for neighbor context
            int myIndex = -1;
            for (int i = 0; i < allProps.Count; i++)
            {
                if (allProps[i].ptr == property) { myIndex = i; break; }
            }

            string prevType = myIndex > 0 ? allProps[myIndex - 1].type : "_";
            string nextType = myIndex >= 0 && myIndex < allProps.Count - 1 ? allProps[myIndex + 1].type : "_";

            // Getter/setter parameter count for extra disambiguation
            string getterSig = "";
            if (getter != IntPtr.Zero)
            {
                int gpc = Il2CppBridge.mdb_method_get_param_count(getter);
                getterSig = $"get({gpc})";
            }
            string setterSig = "";
            if (setter != IntPtr.Zero)
            {
                int spc = Il2CppBridge.mdb_method_get_param_count(setter);
                setterSig = $"set({spc})";
            }
            string accessorSig = string.Join(",", new[] { getterSig, setterSig }.Where(s => s.Length > 0));

            return $"{parentHash}|p:{normPropType}[{access}][{sameTypeOrdinal}/{sameTypeTotal}]|acc:{accessorSig}|ctx:{prevType},{nextType}|n:{allProps.Count}";
        }

        // ==============================
        // Layer B: Byte Signatures
        // ==============================

        /// <summary>
        /// Generate an IDA-style byte pattern signature from a method's native code.
        /// Reads the first N bytes and masks out likely-relocatable operands.
        /// </summary>
        /// <param name="method">MethodInfo pointer</param>
        /// <param name="maxBytes">Number of leading bytes to capture (default 48)</param>
        /// <returns>Pattern like "48 89 5C 24 ?? 48 89 74 24 ??" or empty if unavailable</returns>
        public static string GenerateByteSignature(IntPtr method, int maxBytes = 48)
        {
            if (method == IntPtr.Zero) return "";

            IntPtr methodPtr = Il2CppBridge.mdb_get_method_pointer(method);
            if (methodPtr == IntPtr.Zero) return "";

            byte[] raw = new byte[maxBytes];
            int read = Il2CppBridge.mdb_read_memory(methodPtr, raw, maxBytes);
            if (read < 8) return ""; // Too small to be useful

            // Build pattern with wildcard mask
            bool[] mask = BuildRelocationMask(raw, read);

            var sb = new StringBuilder();
            for (int i = 0; i < read; i++)
            {
                if (i > 0) sb.Append(' ');
                if (mask[i])
                    sb.Append("??");
                else
                    sb.AppendFormat("{0:X2}", raw[i]);
            }

            return sb.ToString();
        }

        /// <summary>
        /// Match a byte signature pattern against a method's native code.
        /// </summary>
        /// <returns>true if the pattern matches</returns>
        public static bool MatchByteSignature(IntPtr method, string pattern)
        {
            if (method == IntPtr.Zero || string.IsNullOrEmpty(pattern)) return false;

            IntPtr methodPtr = Il2CppBridge.mdb_get_method_pointer(method);
            if (methodPtr == IntPtr.Zero) return false;

            string[] tokens = pattern.Split(' ');
            byte[] raw = new byte[tokens.Length];
            int read = Il2CppBridge.mdb_read_memory(methodPtr, raw, tokens.Length);
            if (read < tokens.Length) return false;

            for (int i = 0; i < tokens.Length; i++)
            {
                if (tokens[i] == "??") continue; // Wildcard
                if (!byte.TryParse(tokens[i], System.Globalization.NumberStyles.HexNumber, null, out byte expected))
                    return false;
                if (raw[i] != expected) return false;
            }

            return true;
        }

        // ==============================
        // Layer C: RVA
        // ==============================

        /// <summary>
        /// Get the RVA of a method (offset from GameAssembly.dll base).
        /// </summary>
        public static string GetMethodRva(IntPtr method)
        {
            if (method == IntPtr.Zero) return "";

            IntPtr methodPtr = Il2CppBridge.mdb_get_method_pointer(method);
            IntPtr baseAddr = Il2CppBridge.mdb_get_gameassembly_base();
            if (methodPtr == IntPtr.Zero || baseAddr == IntPtr.Zero) return "";

            long rva = methodPtr.ToInt64() - baseAddr.ToInt64();
            return rva > 0 ? $"0x{rva:X}" : "";
        }

        // ==============================
        // Similarity scoring
        // ==============================

        /// <summary>
        /// Compare two structural signatures and return a similarity score (0.0-1.0).
        /// </summary>
        public static double ComputeStructuralSimilarity(string sig1, string sig2)
        {
            if (string.IsNullOrEmpty(sig1) || string.IsNullOrEmpty(sig2)) return 0.0;
            if (sig1 == sig2) return 1.0;

            var parts1 = sig1.Split('|');
            var parts2 = sig2.Split('|');

            int total = Math.Max(parts1.Length, parts2.Length);
            if (total == 0) return 0.0;

            int matches = 0;
            foreach (var p in parts1)
            {
                if (parts2.Contains(p)) matches++;
            }

            return (double)matches / total;
        }

        // ==============================
        // Helpers
        // ==============================

        /// <summary>
        /// Check if a name looks like a BeeByte obfuscated name (8-15 uppercase letters).
        /// </summary>
        public static bool IsObfuscatedName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length < 8 || name.Length > 15) return false;
            foreach (char c in name)
            {
                if (c < 'A' || c > 'Z') return false;
            }
            return true;
        }

        /// <summary>
        /// Normalize a type name: replace obfuscated names with ?OBF? so signatures
        /// survive obfuscator renames. Non-obfuscated names (Unity types, primitives) stay.
        /// </summary>
        private static string NormalizeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "?";

            // Strip generic backtick (List`1 → List)
            int bt = name.IndexOf('`');
            if (bt > 0) name = name.Substring(0, bt);

            // Check if this name (or inner part of generic/array) is obfuscated
            // Handle array notation
            string core = name.Replace("[]", "").Trim();
            int arrayDepth = name.Length - name.Replace("[]", "").Length; // counts removed chars
            arrayDepth /= 2; // each [] is 2 chars

            if (IsObfuscatedName(core))
            {
                return arrayDepth > 0 ? $"?OBF?[{arrayDepth}]" : "?OBF?";
            }

            return name.Trim();
        }

        private static string MarshalString(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero) return null;
            return Marshal.PtrToStringAnsi(ptr);
        }

        private static string ShortHash(string input)
        {
            using (var md5 = MD5.Create())
            {
                var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(input ?? ""));
                return BitConverter.ToString(hash).Replace("-", "").Substring(0, 8);
            }
        }

        /// <summary>
        /// Build a relocation mask for x64 code bytes.
        /// Marks bytes that are likely to change between builds (addresses, offsets).
        /// </summary>
        private static bool[] BuildRelocationMask(byte[] code, int length)
        {
            var mask = new bool[length];

            for (int i = 0; i < length; i++)
            {
                byte b = code[i];

                // E8 xx xx xx xx — CALL rel32
                if (b == 0xE8 && i + 4 < length)
                {
                    mask[i + 1] = true; mask[i + 2] = true;
                    mask[i + 3] = true; mask[i + 4] = true;
                    i += 4;
                    continue;
                }

                // E9 xx xx xx xx — JMP rel32
                if (b == 0xE9 && i + 4 < length)
                {
                    mask[i + 1] = true; mask[i + 2] = true;
                    mask[i + 3] = true; mask[i + 4] = true;
                    i += 4;
                    continue;
                }

                // 0F 8x xx xx xx xx — Jcc rel32 (conditional jumps)
                if (b == 0x0F && i + 5 < length && (code[i + 1] & 0xF0) == 0x80)
                {
                    mask[i + 2] = true; mask[i + 3] = true;
                    mask[i + 4] = true; mask[i + 5] = true;
                    i += 5;
                    continue;
                }

                // REX.W (48/49) + 8B/8D/89/3B + ModR/M with RIP-relative (mod=00, r/m=101)
                if ((b == 0x48 || b == 0x49) && i + 6 < length)
                {
                    byte op = code[i + 1];
                    if (op == 0x8B || op == 0x8D || op == 0x89 || op == 0x3B ||
                        op == 0x63 || op == 0x03 || op == 0x2B || op == 0x33)
                    {
                        byte modrm = code[i + 2];
                        if ((modrm & 0xC7) == 0x05) // mod=00, r/m=101 = RIP-relative
                        {
                            mask[i + 3] = true; mask[i + 4] = true;
                            mask[i + 5] = true; mask[i + 6] = true;
                            i += 6;
                            continue;
                        }
                    }

                    // REX.W + LEA with RIP-relative
                    if (op == 0x8D)
                    {
                        byte modrm = code[i + 2];
                        if ((modrm & 0xC7) == 0x05)
                        {
                            mask[i + 3] = true; mask[i + 4] = true;
                            mask[i + 5] = true; mask[i + 6] = true;
                            i += 6;
                            continue;
                        }
                    }
                }

                // FF 15 xx xx xx xx — CALL [rip+disp32]
                // FF 25 xx xx xx xx — JMP  [rip+disp32]
                if (b == 0xFF && i + 5 < length && (code[i + 1] == 0x15 || code[i + 1] == 0x25))
                {
                    mask[i + 2] = true; mask[i + 3] = true;
                    mask[i + 4] = true; mask[i + 5] = true;
                    i += 5;
                    continue;
                }

                // 48 B8-BF xx*8 — MOV reg, imm64 (absolute address)
                if (b == 0x48 && i + 9 < length && (code[i + 1] >= 0xB8 && code[i + 1] <= 0xBF))
                {
                    for (int j = 2; j <= 9; j++) mask[i + j] = true;
                    i += 9;
                    continue;
                }
            }

            return mask;
        }

        // ===== Fingerprint helpers =====

        private static string GenerateFieldFingerprint(IntPtr klass)
        {
            int count = Il2CppBridge.mdb_class_get_field_count(klass);
            if (count == 0) return "F:0";

            var fields = new List<(string type, int offset)>();
            for (int i = 0; i < count; i++)
            {
                IntPtr field = Il2CppBridge.mdb_class_get_field_by_index(klass, i);
                if (field == IntPtr.Zero) continue;
                if (Il2CppBridge.mdb_field_is_static(field)) continue;

                int offset = Il2CppBridge.mdb_field_get_offset(field);
                IntPtr ft = Il2CppBridge.mdb_field_get_type(field);
                string tn = ft != IntPtr.Zero ? NormalizeName(MarshalString(Il2CppBridge.mdb_type_get_name(ft))) : "?";
                fields.Add((tn, offset));
            }

            fields.Sort((a, b) => a.offset.CompareTo(b.offset));
            var top = fields.Take(10);
            return "F:" + string.Join(";", top.Select(f => $"{f.type}@0x{f.offset:X}"));
        }

        private static string GenerateMethodFingerprint(IntPtr klass)
        {
            int count = Il2CppBridge.mdb_class_get_method_count(klass);
            if (count == 0) return "M:0";

            var patterns = new Dictionary<string, int>();
            for (int i = 0; i < count; i++)
            {
                IntPtr method = Il2CppBridge.mdb_class_get_method_by_index(klass, i);
                if (method == IntPtr.Zero) continue;

                string name = MarshalString(Il2CppBridge.mdb_method_get_name_str(method));
                if (name != null && name.StartsWith(".")) continue; // Skip constructors

                IntPtr ret = Il2CppBridge.mdb_method_get_return_type(method);
                string retName = ret != IntPtr.Zero ? NormalizeName(MarshalString(Il2CppBridge.mdb_type_get_name(ret))) : "void";
                int pc = Il2CppBridge.mdb_method_get_param_count(method);

                string key = $"{retName}({pc})";
                patterns.TryGetValue(key, out int c);
                patterns[key] = c + 1;
            }

            var top = patterns.OrderByDescending(kv => kv.Value).Take(5);
            return "M:" + string.Join(";", top.Select(kv => $"{kv.Key}x{kv.Value}"));
        }

        private static string GeneratePropertyFingerprint(IntPtr klass)
        {
            int count = Il2CppBridge.mdb_class_get_property_count(klass);
            if (count == 0) return "P:0";

            var typeCounts = new Dictionary<string, int>();
            for (int i = 0; i < count; i++)
            {
                IntPtr prop = Il2CppBridge.mdb_class_get_property_by_index(klass, i);
                if (prop == IntPtr.Zero) continue;

                IntPtr getter = Il2CppBridge.mdb_property_get_get_method(prop);
                string propType = "?";
                if (getter != IntPtr.Zero)
                {
                    IntPtr ret = Il2CppBridge.mdb_method_get_return_type(getter);
                    if (ret != IntPtr.Zero)
                        propType = NormalizeName(MarshalString(Il2CppBridge.mdb_type_get_name(ret)));
                }

                typeCounts.TryGetValue(propType, out int c);
                typeCounts[propType] = c + 1;
            }

            var top = typeCounts.OrderByDescending(kv => kv.Value).Take(5);
            return "P:" + string.Join(";", top.Select(kv => $"{kv.Key}x{kv.Value}"));
        }
    }
}
