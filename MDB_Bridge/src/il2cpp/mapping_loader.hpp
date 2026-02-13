#pragma once

// ============================================================================
// Deobfuscation Mapping Loader for SDK Wrapper Generation
// ============================================================================
// Loads mappings.json (produced by MDB_Core's MappingDatabase) at dump time
// so the C++ wrapper generator can emit friendly C# identifiers while keeping
// obfuscated names in IL2CPP runtime call strings.
//
// JSON format (DataContractJsonSerializer, pretty-printed):
//   [ { "ObfuscatedName": "ABCDEF", "FriendlyName": "Player", "SymbolType": 0,
//       "ParentType": null, ... }, ... ]
//
// SymbolType: 0=Type, 1=Field, 2=Property, 3=Method

#include <string>
#include <unordered_map>
#include <fstream>
#include <sstream>
#include <vector>

namespace MDB {
namespace Mappings {

class MappingLookup {
public:
    /// Load mappings from a JSON file. Returns true if successfully parsed.
    bool Load(const std::string& path) {
        std::ifstream file(path);
        if (!file.is_open()) return false;

        std::stringstream buf;
        buf << file.rdbuf();
        std::string json = buf.str();
        file.close();

        type_map_.clear();
        member_map_.clear();

        auto objects = SplitJsonObjects(json);
        for (const auto& obj : objects) {
            std::string obfName     = ExtractJsonString(obj, "ObfuscatedName");
            std::string friendlyName = ExtractJsonString(obj, "FriendlyName");
            int symbolType           = ExtractJsonInt(obj, "SymbolType");
            std::string parentType   = ExtractJsonString(obj, "ParentType");

            if (obfName.empty() || friendlyName.empty()) continue;

            if (symbolType == 0) {
                // Type (class, enum, interface, struct, delegate)
                type_map_[obfName] = friendlyName;
            } else {
                // Member (field=1, property=2, method=3)
                std::string key = parentType.empty()
                    ? obfName
                    : (parentType + "::" + obfName);
                member_map_[key] = friendlyName;
            }
        }

        return true;
    }

    /// Look up a type's friendly name by its obfuscated name.
    /// Returns empty string if not found.
    std::string ResolveType(const std::string& obfuscated_name) const {
        auto it = type_map_.find(obfuscated_name);
        return (it != type_map_.end()) ? it->second : std::string();
    }

    /// Look up a member's friendly name by parent type + member obfuscated name.
    /// Returns empty string if not found.
    std::string ResolveMember(const std::string& parent_obf, const std::string& member_obf) const {
        // Try with parent context first
        if (!parent_obf.empty()) {
            auto it = member_map_.find(parent_obf + "::" + member_obf);
            if (it != member_map_.end()) return it->second;
        }
        // Fall back to standalone lookup (no parent context)
        auto it = member_map_.find(member_obf);
        return (it != member_map_.end()) ? it->second : std::string();
    }

    bool HasMappings() const { return !type_map_.empty() || !member_map_.empty(); }
    size_t TypeCount() const { return type_map_.size(); }
    size_t MemberCount() const { return member_map_.size(); }
    size_t TotalCount() const { return type_map_.size() + member_map_.size(); }

private:
    std::unordered_map<std::string, std::string> type_map_;    // obf_name -> friendly
    std::unordered_map<std::string, std::string> member_map_;  // "parent::member" -> friendly

    // ---- Minimal JSON helpers (no external dependency) ----

    /// Split a JSON array string into individual object strings.
    static std::vector<std::string> SplitJsonObjects(const std::string& json) {
        std::vector<std::string> objects;
        int depth = 0;
        size_t start = 0;
        bool inString = false;
        bool escape = false;

        for (size_t i = 0; i < json.size(); ++i) {
            char c = json[i];
            if (escape) { escape = false; continue; }
            if (c == '\\') { escape = true; continue; }
            if (c == '"') { inString = !inString; continue; }
            if (inString) continue;

            if (c == '{') {
                if (depth == 0) start = i;
                depth++;
            } else if (c == '}') {
                depth--;
                if (depth == 0) {
                    objects.push_back(json.substr(start, i - start + 1));
                }
            }
        }
        return objects;
    }

    /// Extract a string value for a given key from a JSON object string.
    /// Returns empty string if key not found or value is null.
    static std::string ExtractJsonString(const std::string& json, const std::string& key) {
        std::string pattern = "\"" + key + "\"";
        size_t pos = json.find(pattern);
        if (pos == std::string::npos) return "";

        pos += pattern.size();
        pos = json.find(':', pos);
        if (pos == std::string::npos) return "";
        pos++;

        // Skip whitespace
        while (pos < json.size() && (json[pos] == ' ' || json[pos] == '\t' ||
               json[pos] == '\n' || json[pos] == '\r'))
            pos++;

        if (pos >= json.size()) return "";
        if (json[pos] == 'n') return "";  // null
        if (json[pos] != '"') return "";  // not a string value

        pos++;  // skip opening quote
        std::string result;
        while (pos < json.size() && json[pos] != '"') {
            if (json[pos] == '\\' && pos + 1 < json.size()) {
                pos++;
                switch (json[pos]) {
                case '"':  result += '"';  break;
                case '\\': result += '\\'; break;
                case '/':  result += '/';  break;
                case 'n':  result += '\n'; break;
                case 't':  result += '\t'; break;
                case 'r':  result += '\r'; break;
                default:   result += json[pos]; break;
                }
            } else {
                result += json[pos];
            }
            pos++;
        }
        return result;
    }

    /// Extract an integer value for a given key from a JSON object string.
    static int ExtractJsonInt(const std::string& json, const std::string& key, int defaultVal = -1) {
        std::string pattern = "\"" + key + "\"";
        size_t pos = json.find(pattern);
        if (pos == std::string::npos) return defaultVal;

        pos += pattern.size();
        pos = json.find(':', pos);
        if (pos == std::string::npos) return defaultVal;
        pos++;

        while (pos < json.size() && (json[pos] == ' ' || json[pos] == '\t' ||
               json[pos] == '\n' || json[pos] == '\r'))
            pos++;

        if (pos >= json.size()) return defaultVal;

        try {
            return std::stoi(json.substr(pos));
        } catch (...) {
            return defaultVal;
        }
    }
};

} // namespace Mappings
} // namespace MDB
