#pragma once

// ==============================
// il2cpp_resolver.hpp (v2.2)
// ==============================

#include <cstdint>
#include <string>
#include <string_view>
#include <optional>
#include <unordered_map>
#include <mutex>
#include <type_traits>
#include <fstream>
#include <sstream>
#include <vector>
#include <windows.h>
#include <Psapi.h>

#pragma comment(lib, "psapi.lib")

#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif

#define IL2CPP_GAMEASSEMBLY_FILE "GameAssembly.dll"
#define IL2CPP_FALLBACK_ASSEMBLY "Assembly-CSharp"

enum class Il2CppStatus : uint32_t {
	OK = 0,

	// Loading / Plattform
	GameAssemblyNotFound,
	GetProcAddressFailed,

	// IL2CPP Exports
	Missing_domain_get,
	Missing_thread_attach,
	Missing_domain_get_assemblies,
	Missing_class_from_name,
	Missing_class_get_method_from_name,
	Missing_class_get_field_from_name,
	Missing_field_get_set,

	// Resolver
	DomainUnavailable,
	AssemblyNotFound,
	ImageUnavailable,
	ClassNotFound,
	MethodNotFound,
	FieldNotFound,
	InvalidArgs,

	// Thread / Calls
	ThreadAttachUnavailable,
	MethodPointerNull,
};

template <typename T>
struct Result {
	Il2CppStatus status{ Il2CppStatus::OK };
	T            value{};
	explicit operator bool() const { return status == Il2CppStatus::OK; }
};

template <>
struct Result<void> {
	Il2CppStatus status{ Il2CppStatus::OK };
	explicit operator bool() const { return status == Il2CppStatus::OK; }
};

inline const char* to_string(Il2CppStatus s) {
	switch (s) {
	case Il2CppStatus::OK: return "OK";
	case Il2CppStatus::GameAssemblyNotFound: return "GameAssemblyNotFound";
	case Il2CppStatus::GetProcAddressFailed: return "GetProcAddressFailed";
	case Il2CppStatus::Missing_domain_get: return "Missing_domain_get";
	case Il2CppStatus::Missing_thread_attach: return "Missing_thread_attach";
	case Il2CppStatus::Missing_domain_get_assemblies: return "Missing_domain_get_assemblies";
	case Il2CppStatus::Missing_class_from_name: return "Missing_class_from_name";
	case Il2CppStatus::Missing_class_get_method_from_name: return "Missing_class_get_method_from_name";
	case Il2CppStatus::Missing_class_get_field_from_name: return "Missing_class_get_field_from_name";
	case Il2CppStatus::Missing_field_get_set: return "Missing_field_get_set";
	case Il2CppStatus::DomainUnavailable: return "DomainUnavailable";
	case Il2CppStatus::AssemblyNotFound: return "AssemblyNotFound";
	case Il2CppStatus::ImageUnavailable: return "ImageUnavailable";
	case Il2CppStatus::ClassNotFound: return "ClassNotFound";
	case Il2CppStatus::MethodNotFound: return "MethodNotFound";
	case Il2CppStatus::FieldNotFound: return "FieldNotFound";
	case Il2CppStatus::InvalidArgs: return "InvalidArgs";
	case Il2CppStatus::ThreadAttachUnavailable: return "ThreadAttachUnavailable";
	case Il2CppStatus::MethodPointerNull: return "MethodPointerNull";
	default: return "Unknown";
	}
}

namespace il2cpp {
	namespace _internal {
		namespace unity_structs {
			struct il2cppImage {
				const char* m_pName;
				const char* m_oNameNoExt;
			};

			struct il2cppAssemblyName {
				const char* m_pName{};
				const char* m_pCulture{};
				const char* m_pHash{};
				const char* m_pPublicKey{};
				unsigned int m_uHash{};
				int m_iHashLength{};
				unsigned int m_uFlags{};
				int m_iMajor{};
				int m_iMinor{};
				int m_iBuild{};
				int m_bRevision{};
				unsigned char m_uPublicKeyToken[8]{};
			};

			struct il2cppAssembly {
				il2cppImage* m_pImage{};
				unsigned int m_uToken{};
				int m_ReferencedAssemblyStart{};
				int m_ReferencedAssemblyCount{};
				il2cppAssemblyName m_aName{};
			};

			struct il2cppClass {
				void* m_pImage{};
				void* m_pGC{};
				const char* m_pName{};
				const char* m_pNamespace{};
				void* m_pValue{};
				void* m_pArgs{};
				il2cppClass* m_pElementClass{};
				il2cppClass* m_pCastClass{};
				il2cppClass* m_pDeclareClass{};
				il2cppClass* m_pParentClass{};
				void* m_pGenericClass{};
				void* m_pTypeDefinition{};
				void* m_pInteropData{};
				void* m_pFields{};
				void* m_pEvents{};
				void* m_pProperties{};
				void** m_pMethods{};
				il2cppClass** m_pNestedTypes{};
				il2cppClass** m_ImplementedInterfaces{};
				void* m_pInterfaceOffsets{};
				void* m_pStaticFields{};
				void* m_pRGCTX{};
			};

			struct il2cppObject {
				il2cppClass* m_pClass = nullptr;
				void* m_pMonitor = nullptr;
			};

			// Forward declarations for generic structs (circular with il2cppType)
			struct il2cppGenericClass;

			struct il2cppType {
				union {
					void* m_pDummy;
					unsigned int m_uClassIndex;
					il2cppType* m_pType;
					void* m_pArray;
					unsigned int m_uGenericParameterIndex;
					il2cppGenericClass* m_pGenericClass;
				};
				unsigned int m_uAttributes : 16;
				unsigned int m_uType : 8;
				unsigned int m_uMods : 6;
				unsigned int m_uByref : 1;
				unsigned int m_uPinned : 1;
			};

			// Generic instantiation structures (after il2cppType so we can use il2cppType*)
			struct il2cppGenericInst {
				uint32_t        m_uTypeArgc;      // Number of generic type arguments
				il2cppType**    m_pTypeArgv;       // Array of pointers to il2cppType
			};

			struct il2cppGenericContext {
				il2cppGenericInst* m_pClassInst;   // Class-level generic args
				il2cppGenericInst* m_pMethodInst;  // Method-level generic args
			};

			struct il2cppGenericClass {
				uint32_t              m_uTypeDefinitionIndex;
				il2cppGenericContext   m_Context;
				il2cppClass*          m_pCachedClass;
			};

			struct il2cppFieldInfo {
				const char* m_pName{};
				il2cppType* m_pType{};
				il2cppClass* m_pParentClass{};
				int m_iOffset{};
				int m_iAttributeIndex{};
				unsigned int m_uToken{};
			};

			struct il2cppParameterInfo {
				const char* m_pName{};
				int m_iPosition{};
				unsigned int m_uToken{};
				il2cppType* m_pParameterType{};
			};

			struct il2cppMethodInfo {
				void* m_pMethodPointer{};
				void* m_pInvokerMethod{};
				const char* m_pName{};
				il2cppClass* m_pClass{};
				il2cppType* m_pReturnType{};
				il2cppParameterInfo* m_pParameters{};
				union {
					void* m_pRGCTX;
					void* m_pMethodDefinition;
				};
				// Unity 2021+ added a field here (virtual method pointer or interop data).
				// Must be present to align the generic container and tail fields correctly.
				void* m_pVirtualCallMethodPointer{};
				union {
					void* m_pGenericMethod;
					void* m_pGenericContainer;
				};
				unsigned int m_uToken{};
				unsigned short m_uFlags{};
				unsigned short m_uFlags2{};
				unsigned short m_uSlot{};
				unsigned char m_uArgsCount{};
				unsigned char m_uGeneric : 1;
				unsigned char m_uInflated : 1;
				unsigned char m_uWrapperType : 1;
				unsigned char m_uMarshaledFromNative : 1;
			};

			/// Represents an IL2CPP generic parameter container.
			/// Used to determine the number of type parameters on a generic method/class definition.
			struct il2cppGenericContainer {
				int32_t m_iOwnerIndex;   // Index of owning type/method definition
				int32_t m_iTypeArgc;     // Number of generic type parameters
				// Remaining fields (is_method, genericParameterStart) omitted —
				// we only need the type argument count for codegen.
			};

			struct il2cppPropertyInfo {
				il2cppClass* m_pParentClass;
				const char* m_pName;
				il2cppMethodInfo* m_pGet;
				il2cppMethodInfo* m_pSet;
				unsigned int m_uAttributes;
				unsigned int m_uToken;
			};

			struct il2cppArrayBounds {
				unsigned long long m_uLength;
				int m_iLowerBound;
			};

			struct Il2CppRuntimeInterfaceOffsetPair {
				il2cppClass* interfaceType;
				int32_t offset;
			};

			struct Il2CppClass_1 {
				void* image;
				void* gc_desc;
				const char* name;
				const char* namespaze;
				il2cppType byval_arg;
				il2cppType this_arg;
				il2cppClass* element_class;
				il2cppClass* castClass;
				il2cppClass* declaringType;
				il2cppClass* parent;
				void* generic_class;
				void* typeMetadataHandle;
				void* interopData;
				il2cppClass* klass;
				void* fields;
				void* events;
				void* properties;
				void* methods;
				il2cppClass** nestedTypes;
				il2cppClass** implementedInterfaces;
				Il2CppRuntimeInterfaceOffsetPair* interfaceOffsets;
			};

			struct Il2CppClass_2
			{
				il2cppClass** typeHierarchy;
				void* unity_user_data;
				std::uint32_t initializationExceptionGCHandle;
				std::uint32_t cctor_started;
				std::uint32_t cctor_finished;
				size_t cctor_thread;
				void* genericContainerHandle;
				std::uint32_t instance_size;
				std::uint32_t actualSize;
				std::uint32_t element_size;
				std::int32_t native_size;
				std::uint32_t static_fields_size;
				std::uint32_t thread_static_fields_size;
				std::int32_t thread_static_fields_offset;
				std::uint32_t flags;
				std::uint32_t token;
				std::uint16_t method_count;
				std::uint16_t property_count;
				std::uint16_t field_count;
				std::uint16_t event_count;
				std::uint16_t nested_type_count;
				std::uint16_t vtable_count;
				std::uint16_t interfaces_count;
				std::uint16_t interface_offsets_count;
				std::uint8_t typeHierarchyDepth;
				std::uint8_t genericRecursionDepth;
				std::uint8_t rank;
				std::uint8_t minimumAlignment;
				std::uint8_t naturalAligment;
				std::uint8_t packingSize;
				std::uint8_t bitflags1;
				std::uint8_t bitflags2;
			};

			typedef void(*Il2CppMethodPointer)();
			struct VirtualInvokeData {
				Il2CppMethodPointer methodPtr;
				const il2cppMethodInfo* method;
			};

			struct System_String_VTable {
				VirtualInvokeData _0_Equals;
				VirtualInvokeData _1_Finalize;
				VirtualInvokeData _2_GetHashCode;
				VirtualInvokeData _3_ToString;
				VirtualInvokeData _4_CompareTo;
				VirtualInvokeData _5_System_Collections_IEnumerable_GetEnumerator;
				VirtualInvokeData _6_System_Collections_Generic_IEnumerable_System_Char__GetEnumerator;
				VirtualInvokeData _7_CompareTo;
				VirtualInvokeData _8_Equals;
				VirtualInvokeData _9_GetTypeCode;
				VirtualInvokeData _10_System_IConvertible_ToBoolean;
				VirtualInvokeData _11_System_IConvertible_ToChar;
				VirtualInvokeData _12_System_IConvertible_ToSByte;
				VirtualInvokeData _13_System_IConvertible_ToByte;
				VirtualInvokeData _14_System_IConvertible_ToInt16;
				VirtualInvokeData _15_System_IConvertible_ToUInt16;
				VirtualInvokeData _16_System_IConvertible_ToInt32;
				VirtualInvokeData _17_System_IConvertible_ToUInt32;
				VirtualInvokeData _18_System_IConvertible_ToInt64;
				VirtualInvokeData _19_System_IConvertible_ToUInt64;
				VirtualInvokeData _20_System_IConvertible_ToSingle;
				VirtualInvokeData _21_System_IConvertible_ToDouble;
				VirtualInvokeData _22_System_IConvertible_ToDecimal;
				VirtualInvokeData _23_System_IConvertible_ToDateTime;
				VirtualInvokeData _24_ToString;
				VirtualInvokeData _25_System_IConvertible_ToType;
				VirtualInvokeData _26_Clone;
			};

			struct System_String_c {
				Il2CppClass_1 _1;
				struct System_String_StaticFields* static_fields;
				void* rgctx_data;
				Il2CppClass_2 _2;
				System_String_VTable vtable;
			};

			struct __declspec(align(8)) System_String_Fields
			{
				std::uint32_t _stringLength;
				std::uint16_t _firstChar;
			};

			struct System_String_o {
				System_String_c* klass;
				void* monitor;
				System_String_Fields fields;
			};
		}

		inline HMODULE p_game_assembly = nullptr;

		inline std::unordered_map<std::string, unity_structs::il2cppAssembly*> g_assembly_cache;
		inline std::mutex g_cache_mtx;

		// Forward declaration
		inline Result<HMODULE> ensure_game_assembly();

		// =============================================
		// PE Export Scanner for Obfuscated Names
		// =============================================
		// Some games obfuscate IL2CPP exports by renaming them.
		// e.g., "il2cpp_domain_get_assemblies" -> "xyz123_wasting_your_time"
		// This scanner searches for exports matching a suffix pattern.

		// Map of original export names to their obfuscated equivalents
		inline std::unordered_map<std::string, std::string> g_obfuscated_exports;
		inline bool g_exports_scanned = false;

		// Known obfuscation suffix patterns (expanded list)
		inline const char* OBFUSCATION_SUFFIXES[] = {
			"_wasting_your_life",
			nullptr
		};

		// Map obfuscated suffix to original IL2CPP export suffix
		inline std::unordered_map<std::string, std::string> SUFFIX_TO_ORIGINAL = {
			{ "_wasting_your_life", "_domain_get_assemblies" },
		};

		// Export resolution log entries
		inline std::vector<std::string> g_export_log;
		inline std::mutex g_export_log_mtx;

		// Log an export resolution
		inline void log_export_resolution(const std::string& original_name, const std::string& resolved_name, uintptr_t address, const std::string& method) {
			std::scoped_lock lk(g_export_log_mtx);
			std::stringstream ss;
			ss << original_name << " -> " << resolved_name << " @ 0x" << std::hex << address << " [" << method << "]";
			g_export_log.push_back(ss.str());
		}

		// Write export log to file
		inline void write_export_log() {
			// Get game executable path
			char exePath[MAX_PATH];
			GetModuleFileNameA(nullptr, exePath, MAX_PATH);
			std::string exeDir(exePath);
			size_t lastSlash = exeDir.find_last_of("\\/");
			if (lastSlash != std::string::npos) {
				exeDir = exeDir.substr(0, lastSlash);
			}

			// Create MDB/Dump folder if needed
			std::string mdbDir = exeDir + "\\MDB";
			std::string dumpDir = mdbDir + "\\Dump";
			CreateDirectoryA(mdbDir.c_str(), nullptr);
			CreateDirectoryA(dumpDir.c_str(), nullptr);

			// Write export log
			std::string logPath = dumpDir + "\\resolved_exports.txt";
			std::ofstream file(logPath);
			if (file.is_open()) {
				file << "// IL2CPP Export Resolution Log\n";
				file << "// Format: original_name -> resolved_name @ address [resolution_method]\n\n";
				std::scoped_lock lk(g_export_log_mtx);
				for (const auto& entry : g_export_log) {
					file << entry << "\n";
				}
				file.close();
			}
		}

		// Find export by suffix pattern
		inline uintptr_t find_export_by_suffix(HMODULE hModule, const char* suffix) {
			auto dosHeader = reinterpret_cast<PIMAGE_DOS_HEADER>(hModule);
			if (dosHeader->e_magic != IMAGE_DOS_SIGNATURE) return 0;

			auto ntHeaders = reinterpret_cast<PIMAGE_NT_HEADERS>(
				reinterpret_cast<BYTE*>(hModule) + dosHeader->e_lfanew);
			if (ntHeaders->Signature != IMAGE_NT_SIGNATURE) return 0;

			auto& exportDir = ntHeaders->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_EXPORT];
			if (exportDir.VirtualAddress == 0) return 0;

			auto exports = reinterpret_cast<PIMAGE_EXPORT_DIRECTORY>(
				reinterpret_cast<BYTE*>(hModule) + exportDir.VirtualAddress);

			auto names = reinterpret_cast<DWORD*>(
				reinterpret_cast<BYTE*>(hModule) + exports->AddressOfNames);
			auto ordinals = reinterpret_cast<WORD*>(
				reinterpret_cast<BYTE*>(hModule) + exports->AddressOfNameOrdinals);
			auto functions = reinterpret_cast<DWORD*>(
				reinterpret_cast<BYTE*>(hModule) + exports->AddressOfFunctions);

			size_t suffixLen = strlen(suffix);
			for (DWORD i = 0; i < exports->NumberOfNames; ++i) {
				const char* name = reinterpret_cast<const char*>(
					reinterpret_cast<BYTE*>(hModule) + names[i]);
				size_t nameLen = strlen(name);

				if (nameLen > suffixLen && strcmp(name + nameLen - suffixLen, suffix) == 0) {
					// Store the found obfuscated name for logging
					auto it = SUFFIX_TO_ORIGINAL.find(suffix);
					if (it != SUFFIX_TO_ORIGINAL.end()) {
						std::string originalName = "il2cpp" + it->second;
						g_obfuscated_exports[originalName] = name;
					}
					return reinterpret_cast<uintptr_t>(hModule) + functions[ordinals[i]];
				}
			}
			return 0;
		}

		// Scan PE exports and build obfuscation map
		inline void scan_pe_exports() {
			if (g_exports_scanned) return;
			g_exports_scanned = true;

			auto mod = ensure_game_assembly();
			if (!mod) return;

			HMODULE hModule = mod.value;
			auto dosHeader = reinterpret_cast<PIMAGE_DOS_HEADER>(hModule);
			if (dosHeader->e_magic != IMAGE_DOS_SIGNATURE) return;

			auto ntHeaders = reinterpret_cast<PIMAGE_NT_HEADERS>(
				reinterpret_cast<BYTE*>(hModule) + dosHeader->e_lfanew);
			if (ntHeaders->Signature != IMAGE_NT_SIGNATURE) return;

			auto& exportDir = ntHeaders->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_EXPORT];
			if (exportDir.VirtualAddress == 0) return;

			auto exports = reinterpret_cast<PIMAGE_EXPORT_DIRECTORY>(
				reinterpret_cast<BYTE*>(hModule) + exportDir.VirtualAddress);

			auto names = reinterpret_cast<DWORD*>(
				reinterpret_cast<BYTE*>(hModule) + exports->AddressOfNames);

			// Scan all export names for obfuscation patterns
			for (DWORD i = 0; i < exports->NumberOfNames; ++i) {
				const char* name = reinterpret_cast<const char*>(
					reinterpret_cast<BYTE*>(hModule) + names[i]);

				std::string exportName(name);

				// Check each obfuscation suffix
				for (int s = 0; OBFUSCATION_SUFFIXES[s] != nullptr; ++s) {
					const char* suffix = OBFUSCATION_SUFFIXES[s];
					size_t suffixLen = strlen(suffix);
					if (exportName.length() > suffixLen) {
						// Check if export ends with this suffix
						if (exportName.compare(exportName.length() - suffixLen, suffixLen, suffix) == 0) {
							// Found obfuscated export - map it to original
							auto it = SUFFIX_TO_ORIGINAL.find(suffix);
							if (it != SUFFIX_TO_ORIGINAL.end()) {
								std::string originalName = "il2cpp" + it->second;
								g_obfuscated_exports[originalName] = exportName;
							}
						}
					}
				}
			}
		}

		// Get the actual export name (obfuscated or original)
		inline std::string get_export_name(const std::string& originalName) {
			scan_pe_exports();

			auto it = g_obfuscated_exports.find(originalName);
			if (it != g_obfuscated_exports.end()) {
				return it->second;  // Return obfuscated name
			}
			return originalName;  // Return original name
		}

		inline Result<HMODULE> ensure_game_assembly() {
			if (p_game_assembly) return { Il2CppStatus::OK, p_game_assembly };
			for (int i = 0; i < 200 && !p_game_assembly; ++i) {
				p_game_assembly = ::GetModuleHandleA(IL2CPP_GAMEASSEMBLY_FILE);
				if (!p_game_assembly) ::Sleep(10);
			}
			if (!p_game_assembly) return { Il2CppStatus::GameAssemblyNotFound, nullptr };
			return { Il2CppStatus::OK, p_game_assembly };
		}

		// Unified export resolution with fallback chain:
		// 1. Standard GetProcAddress
		// 2. Suffix-based pattern matching for obfuscated exports
		template <class T>
		inline Result<T> resolve_export(std::string_view name) {
			auto mod = ensure_game_assembly();
			if (!mod) return { mod.status, nullptr };
			
			std::string exportName(name);
			
			// Strategy 1: Standard GetProcAddress
			auto p = reinterpret_cast<T>(::GetProcAddress(mod.value, exportName.c_str()));
			if (p) {
				log_export_resolution(exportName, exportName, reinterpret_cast<uintptr_t>(p), "GetProcAddress");
				return { Il2CppStatus::OK, p };
			}
			
			// Strategy 2: Check for cached obfuscated name
			scan_pe_exports();
			auto it = g_obfuscated_exports.find(exportName);
			if (it != g_obfuscated_exports.end()) {
				p = reinterpret_cast<T>(::GetProcAddress(mod.value, it->second.c_str()));
				if (p) {
					log_export_resolution(exportName, it->second, reinterpret_cast<uintptr_t>(p), "SuffixMatch");
					return { Il2CppStatus::OK, p };
				}
			}
			
			// Strategy 3: Direct suffix scan for this specific export
			for (int i = 0; OBFUSCATION_SUFFIXES[i] != nullptr; ++i) {
				uintptr_t addr = find_export_by_suffix(mod.value, OBFUSCATION_SUFFIXES[i]);
				if (addr) {
					// Get the actual name that was found
					std::string foundName = "<suffix:" + std::string(OBFUSCATION_SUFFIXES[i]) + ">";
					auto obfIt = g_obfuscated_exports.find(exportName);
					if (obfIt != g_obfuscated_exports.end()) {
						foundName = obfIt->second;
					}
					log_export_resolution(exportName, foundName, addr, "SuffixScan");
					return { Il2CppStatus::OK, reinterpret_cast<T>(addr) };
				}
			}
			
			return { Il2CppStatus::GetProcAddressFailed, nullptr };
		}

		// dereferenced function pointers (valid after ensure_exports())
		inline void* (__fastcall* il2cpp_domain_get) (void) = nullptr;
		inline void* (__fastcall* il2cpp_thread_attach)(void*) = nullptr;
		inline void* (__fastcall* il2cpp_thread_detach)(void) = nullptr;
		inline unity_structs::il2cppAssembly** (__fastcall* il2cpp_domain_get_assemblies)(void*, size_t*) = nullptr;
		inline unity_structs::il2cppClass* (__fastcall* il2cpp_class_from_name)(unity_structs::il2cppImage*, const char*, const char*) = nullptr;
		inline unity_structs::il2cppMethodInfo* (__fastcall* il2cpp_class_get_method_from_name)(unity_structs::il2cppClass*, const char*, int) = nullptr;
		inline unity_structs::il2cppFieldInfo* (__fastcall* il2cpp_class_get_field_from_name)(unity_structs::il2cppClass*, const char*) = nullptr;
		inline void(__fastcall* il2cpp_field_get_value)(void*, unity_structs::il2cppFieldInfo*, void*) = nullptr;
		inline void(__fastcall* il2cpp_field_set_value)(void*, unity_structs::il2cppFieldInfo*, void*) = nullptr;
		inline void(__fastcall* il2cpp_field_static_get_value)(unity_structs::il2cppFieldInfo*, void*) = nullptr;
		inline void(__fastcall* il2cpp_field_static_set_value)(unity_structs::il2cppFieldInfo*, void*) = nullptr;
		inline void* (__fastcall* il2cpp_string_new)(const char*) = nullptr;
		inline void* (__fastcall* il2cpp_object_new)(unity_structs::il2cppClass*) = nullptr;

		// -- Dumper/introspection APIs (used by il2cpp_dumper.cpp) --
		inline unity_structs::il2cppImage* (__fastcall* il2cpp_assembly_get_image)(const unity_structs::il2cppAssembly*) = nullptr;
		inline const char* (__fastcall* il2cpp_image_get_name)(const unity_structs::il2cppImage*) = nullptr;
		inline size_t (__fastcall* il2cpp_image_get_class_count)(const unity_structs::il2cppImage*) = nullptr;
		inline unity_structs::il2cppClass* (__fastcall* il2cpp_image_get_class)(const unity_structs::il2cppImage*, size_t) = nullptr;
		inline unity_structs::il2cppType* (__fastcall* il2cpp_class_get_type)(unity_structs::il2cppClass*) = nullptr;
		inline unity_structs::il2cppClass* (__fastcall* il2cpp_class_from_type)(const unity_structs::il2cppType*) = nullptr;
		inline const char* (__fastcall* il2cpp_class_get_namespace)(unity_structs::il2cppClass*) = nullptr;
		inline int (__fastcall* il2cpp_class_get_flags)(const unity_structs::il2cppClass*) = nullptr;
		inline bool (__fastcall* il2cpp_class_is_valuetype)(const unity_structs::il2cppClass*) = nullptr;
		inline bool (__fastcall* il2cpp_class_is_enum)(const unity_structs::il2cppClass*) = nullptr;
		inline const char* (__fastcall* il2cpp_class_get_name)(unity_structs::il2cppClass*) = nullptr;
		inline unity_structs::il2cppClass* (__fastcall* il2cpp_class_get_declaring_type)(unity_structs::il2cppClass*) = nullptr;
		inline unity_structs::il2cppClass* (__fastcall* il2cpp_class_get_parent)(unity_structs::il2cppClass*) = nullptr;
		inline unity_structs::il2cppClass* (__fastcall* il2cpp_class_get_interfaces)(unity_structs::il2cppClass*, void**) = nullptr;
		inline unity_structs::il2cppFieldInfo* (__fastcall* il2cpp_class_get_fields)(unity_structs::il2cppClass*, void**) = nullptr;
		inline int (__fastcall* il2cpp_field_get_flags)(unity_structs::il2cppFieldInfo*) = nullptr;
		inline const unity_structs::il2cppType* (__fastcall* il2cpp_field_get_type)(unity_structs::il2cppFieldInfo*) = nullptr;
		inline const char* (__fastcall* il2cpp_field_get_name)(unity_structs::il2cppFieldInfo*) = nullptr;
		inline size_t (__fastcall* il2cpp_field_get_offset)(unity_structs::il2cppFieldInfo*) = nullptr;
		inline const unity_structs::il2cppPropertyInfo* (__fastcall* il2cpp_class_get_properties)(unity_structs::il2cppClass*, void**) = nullptr;
		inline const unity_structs::il2cppMethodInfo* (__fastcall* il2cpp_property_get_get_method)(unity_structs::il2cppPropertyInfo*) = nullptr;
		inline const unity_structs::il2cppMethodInfo* (__fastcall* il2cpp_property_get_set_method)(unity_structs::il2cppPropertyInfo*) = nullptr;
		inline const char* (__fastcall* il2cpp_property_get_name)(unity_structs::il2cppPropertyInfo*) = nullptr;
		inline uint32_t (__fastcall* il2cpp_method_get_flags)(const unity_structs::il2cppMethodInfo*, uint32_t*) = nullptr;
		inline const unity_structs::il2cppType* (__fastcall* il2cpp_method_get_return_type)(const unity_structs::il2cppMethodInfo*) = nullptr;
		inline const unity_structs::il2cppType* (__fastcall* il2cpp_method_get_param)(const unity_structs::il2cppMethodInfo*, uint32_t) = nullptr;
		inline const unity_structs::il2cppMethodInfo* (__fastcall* il2cpp_class_get_methods)(unity_structs::il2cppClass*, void**) = nullptr;
		inline bool (__fastcall* il2cpp_type_is_byref)(const unity_structs::il2cppType*) = nullptr;
		inline const char* (__fastcall* il2cpp_method_get_name)(const unity_structs::il2cppMethodInfo*) = nullptr;
		inline uint32_t (__fastcall* il2cpp_method_get_param_count)(const unity_structs::il2cppMethodInfo*) = nullptr;
		inline const char* (__fastcall* il2cpp_method_get_param_name)(const unity_structs::il2cppMethodInfo*, uint32_t) = nullptr;

		// -- Generic method inflation APIs --
		inline void* (__fastcall* il2cpp_method_get_object)(const unity_structs::il2cppMethodInfo*, unity_structs::il2cppClass*) = nullptr;
		inline const unity_structs::il2cppMethodInfo* (__fastcall* il2cpp_method_get_from_reflection)(const void*) = nullptr;
		inline void* (__fastcall* il2cpp_type_get_object)(const unity_structs::il2cppType*) = nullptr;
		inline void* (__fastcall* il2cpp_object_get_class)(void*) = nullptr;
		inline void* (__fastcall* il2cpp_array_new)(unity_structs::il2cppClass*, size_t) = nullptr;
		inline void* (__fastcall* il2cpp_runtime_invoke)(const unity_structs::il2cppMethodInfo*, void*, void**, void**) = nullptr;

		// --------------------------
		// Validate exports & bind (LAZY RESOLUTION)
		// --------------------------
		inline Il2CppStatus ensure_exports() {
			// Skip if already initialized
			static bool s_initialized = false;
			if (s_initialized && il2cpp_domain_get) return Il2CppStatus::OK;
			
			// Resolve all exports now (lazy)
			auto r_il2cpp_domain_get = resolve_export<void* (__fastcall*)(void)>("il2cpp_domain_get");
			auto r_il2cpp_thread_attach = resolve_export<void* (__fastcall*)(void*)>("il2cpp_thread_attach");
			// Uses unified resolve_export with automatic suffix fallback
			auto r_il2cpp_domain_get_assemblies = resolve_export<unity_structs::il2cppAssembly** (__fastcall*)(void*, size_t*)>("il2cpp_domain_get_assemblies");
			auto r_il2cpp_class_from_name = resolve_export<unity_structs::il2cppClass* (__fastcall*)(unity_structs::il2cppImage*, const char*, const char*)>("il2cpp_class_from_name");
			auto r_il2cpp_class_get_method_from_name = resolve_export<unity_structs::il2cppMethodInfo* (__fastcall*)(unity_structs::il2cppClass*, const char*, int)>("il2cpp_class_get_method_from_name");
			auto r_il2cpp_class_get_field_from_name = resolve_export<unity_structs::il2cppFieldInfo* (__fastcall*)(unity_structs::il2cppClass*, const char*)>("il2cpp_class_get_field_from_name");
			auto r_il2cpp_field_get_value = resolve_export<void(__fastcall*)(void*, unity_structs::il2cppFieldInfo*, void*)>("il2cpp_field_get_value");
			auto r_il2cpp_field_set_value = resolve_export<void(__fastcall*)(void*, unity_structs::il2cppFieldInfo*, void*)>("il2cpp_field_set_value");
			auto r_il2cpp_field_static_get_value = resolve_export<void(__fastcall*)(unity_structs::il2cppFieldInfo*, void*)>("il2cpp_field_static_get_value");
			auto r_il2cpp_field_static_set_value = resolve_export<void(__fastcall*)(unity_structs::il2cppFieldInfo*, void*)>("il2cpp_field_static_set_value");
			auto r_il2cpp_string_new = resolve_export<void* (__fastcall*)(const char*)>("il2cpp_string_new");
			auto r_il2cpp_object_new = resolve_export<void* (__fastcall*)(unity_structs::il2cppClass*)>("il2cpp_object_new");
			
			auto bind = [](auto& dst, auto& res, Il2CppStatus err) -> Il2CppStatus {
				if (res.status != Il2CppStatus::OK || !res.value) return err;
				dst = res.value; return Il2CppStatus::OK;
				};

			if (auto s = bind(il2cpp_domain_get, r_il2cpp_domain_get, Il2CppStatus::Missing_domain_get); s != Il2CppStatus::OK) return s;
			if (auto s = bind(il2cpp_thread_attach, r_il2cpp_thread_attach, Il2CppStatus::Missing_thread_attach); s != Il2CppStatus::OK) return s;
			if (auto s = bind(il2cpp_domain_get_assemblies, r_il2cpp_domain_get_assemblies, Il2CppStatus::Missing_domain_get_assemblies); s != Il2CppStatus::OK) return s;
			if (auto s = bind(il2cpp_class_from_name, r_il2cpp_class_from_name, Il2CppStatus::Missing_class_from_name); s != Il2CppStatus::OK) return s;
			if (auto s = bind(il2cpp_class_get_method_from_name, r_il2cpp_class_get_method_from_name, Il2CppStatus::Missing_class_get_method_from_name); s != Il2CppStatus::OK) return s;
			if (auto s = bind(il2cpp_class_get_field_from_name, r_il2cpp_class_get_field_from_name, Il2CppStatus::Missing_class_get_field_from_name); s != Il2CppStatus::OK) return s;
			if (auto s = bind(il2cpp_field_get_value, r_il2cpp_field_get_value, Il2CppStatus::Missing_field_get_set); s != Il2CppStatus::OK) return s;
			if (auto s = bind(il2cpp_field_set_value, r_il2cpp_field_set_value, Il2CppStatus::Missing_field_get_set); s != Il2CppStatus::OK) return s;
			if (auto s = bind(il2cpp_field_static_get_value, r_il2cpp_field_static_get_value, Il2CppStatus::Missing_field_get_set); s != Il2CppStatus::OK) return s;
			if (auto s = bind(il2cpp_field_static_set_value, r_il2cpp_field_static_set_value, Il2CppStatus::Missing_field_get_set); s != Il2CppStatus::OK) return s;
			if (auto s = bind(il2cpp_object_new, r_il2cpp_object_new, Il2CppStatus::GetProcAddressFailed); s != Il2CppStatus::OK) return s;

			if (r_il2cpp_string_new) il2cpp_string_new = r_il2cpp_string_new.value;

			// -- Dumper/introspection APIs (best-effort, not required for bridge) --
			auto try_bind = [](auto& dst, const char* name) {
				auto r = resolve_export<std::remove_reference_t<decltype(dst)>>(name);
				if (r && r.value) dst = r.value;
			};
			try_bind(il2cpp_assembly_get_image,      "il2cpp_assembly_get_image");
			try_bind(il2cpp_image_get_name,           "il2cpp_image_get_name");
			try_bind(il2cpp_image_get_class_count,    "il2cpp_image_get_class_count");
			try_bind(il2cpp_image_get_class,          "il2cpp_image_get_class");
			try_bind(il2cpp_class_get_type,           "il2cpp_class_get_type");
			try_bind(il2cpp_class_from_type,          "il2cpp_class_from_type");
			try_bind(il2cpp_class_get_namespace,      "il2cpp_class_get_namespace");
			try_bind(il2cpp_class_get_flags,          "il2cpp_class_get_flags");
			try_bind(il2cpp_class_is_valuetype,       "il2cpp_class_is_valuetype");
			try_bind(il2cpp_class_is_enum,            "il2cpp_class_is_enum");
			try_bind(il2cpp_class_get_name,           "il2cpp_class_get_name");
			try_bind(il2cpp_class_get_declaring_type, "il2cpp_class_get_declaring_type");
			try_bind(il2cpp_class_get_parent,         "il2cpp_class_get_parent");
			try_bind(il2cpp_class_get_interfaces,     "il2cpp_class_get_interfaces");
			try_bind(il2cpp_class_get_fields,         "il2cpp_class_get_fields");
			try_bind(il2cpp_field_get_flags,          "il2cpp_field_get_flags");
			try_bind(il2cpp_field_get_type,           "il2cpp_field_get_type");
			try_bind(il2cpp_field_get_name,           "il2cpp_field_get_name");
			try_bind(il2cpp_field_get_offset,         "il2cpp_field_get_offset");
			try_bind(il2cpp_class_get_properties,     "il2cpp_class_get_properties");
			try_bind(il2cpp_property_get_get_method,  "il2cpp_property_get_get_method");
			try_bind(il2cpp_property_get_set_method,  "il2cpp_property_get_set_method");
			try_bind(il2cpp_property_get_name,        "il2cpp_property_get_name");
			try_bind(il2cpp_method_get_flags,         "il2cpp_method_get_flags");
			try_bind(il2cpp_method_get_return_type,   "il2cpp_method_get_return_type");
			try_bind(il2cpp_method_get_param,         "il2cpp_method_get_param");
			try_bind(il2cpp_class_get_methods,        "il2cpp_class_get_methods");
			try_bind(il2cpp_type_is_byref,            "il2cpp_type_is_byref");
			try_bind(il2cpp_method_get_name,          "il2cpp_method_get_name");
			try_bind(il2cpp_method_get_param_count,   "il2cpp_method_get_param_count");
			try_bind(il2cpp_method_get_param_name,    "il2cpp_method_get_param_name");

			// Generic method inflation
			try_bind(il2cpp_method_get_object,         "il2cpp_method_get_object");
			try_bind(il2cpp_method_get_from_reflection,"il2cpp_method_get_from_reflection");
			try_bind(il2cpp_type_get_object,           "il2cpp_type_get_object");
			try_bind(il2cpp_object_get_class,          "il2cpp_object_get_class");
			try_bind(il2cpp_array_new,                 "il2cpp_array_new");
			try_bind(il2cpp_runtime_invoke,            "il2cpp_runtime_invoke");

			// Write export resolution log to game folder
			write_export_log();
			
			s_initialized = true;
			return Il2CppStatus::OK;
		}

		// --------------------------
		// Assembly lookup + Cache
		// --------------------------
		inline Result<unity_structs::il2cppAssembly*>
			find_assembly(std::string_view assembly_name) {
			if (assembly_name.empty()) return { Il2CppStatus::InvalidArgs, nullptr };

			if (auto s = ensure_exports(); s != Il2CppStatus::OK)
				return { s, nullptr };

			{   // Cache
				std::scoped_lock lk(g_cache_mtx);
				if (auto it = g_assembly_cache.find(std::string(assembly_name)); it != g_assembly_cache.end())
					return { Il2CppStatus::OK, it->second };
			}

			auto domain = il2cpp_domain_get ? il2cpp_domain_get() : nullptr;
			if (!domain) return { Il2CppStatus::DomainUnavailable, nullptr };

			size_t count = 0;
			auto assemblies = il2cpp_domain_get_assemblies(domain, &count);
			if (!assemblies || count == 0) return { Il2CppStatus::AssemblyNotFound, nullptr };

			for (size_t i = 0; i < count; ++i) {
				const auto* a = assemblies[i];
				if (!a) continue;
				const char* meta = a->m_aName.m_pName;
				const char* noext = (a->m_pImage ? a->m_pImage->m_oNameNoExt : nullptr);
				if ((meta && assembly_name == meta) || (noext && assembly_name == noext)) {
					std::scoped_lock lk(g_cache_mtx);
					g_assembly_cache.emplace(std::string(assembly_name), assemblies[i]);
					return { Il2CppStatus::OK, assemblies[i] };
				}
			}
			return { Il2CppStatus::AssemblyNotFound, nullptr };
		}
	} // namespace _internal

	// ------------------------------------
	// Thread-Attach
	// ------------------------------------
	inline Il2CppStatus ensure_thread_attached() {
		auto s = _internal::ensure_exports();
		if (s != Il2CppStatus::OK) return s;
		if (!_internal::il2cpp_domain_get || !_internal::il2cpp_thread_attach)
			return Il2CppStatus::ThreadAttachUnavailable;

		thread_local bool t_attached = false;
		if (!t_attached) {
			auto domain = _internal::il2cpp_domain_get();
			if (!domain) return Il2CppStatus::DomainUnavailable;
			_internal::il2cpp_thread_attach(domain);
			t_attached = true;
		}
		return Il2CppStatus::OK;
	}

	// ------------------------------------
	// Class- & Method-Resolvers
	// ------------------------------------
	inline Result<_internal::unity_structs::il2cppClass*>
		find_class(const std::string& ns,
			const std::string& class_name,
			const std::string& assembly_name)
	{
		// Allow empty namespace for global classes (e.g., obfuscated game classes)
		if (class_name.empty() || assembly_name.empty())
			return { Il2CppStatus::InvalidArgs, nullptr };

		auto a = _internal::find_assembly(assembly_name);
		if (!a) return { a.status, nullptr };
		if (!a.value->m_pImage) return { Il2CppStatus::ImageUnavailable, nullptr };

		auto* klass = _internal::il2cpp_class_from_name(a.value->m_pImage, ns.c_str(), class_name.c_str());
		if (!klass) return { Il2CppStatus::ClassNotFound, nullptr };
		return { Il2CppStatus::OK, klass };
	}

	// Class size
	inline Result<size_t>
		get_class_size(const std::string& ns,
			const std::string& class_name,
			const std::string& assembly_name = IL2CPP_FALLBACK_ASSEMBLY)
	{
		// Allow empty namespace for global classes
		if (class_name.empty() || assembly_name.empty())
			return { Il2CppStatus::InvalidArgs, 0 };

		auto c = find_class(ns, class_name, assembly_name);
		if (!c) return { c.status, 0 };

		auto base_addr = reinterpret_cast<uintptr_t>(c.value);

		size_t offset = sizeof(_internal::unity_structs::Il2CppClass_1) + sizeof(void*) * 2;

		offset += offsetof(_internal::unity_structs::Il2CppClass_2, instance_size);

		auto instance_size = *reinterpret_cast<uint32_t*>(base_addr + offset);

		return { Il2CppStatus::OK, static_cast<size_t>(instance_size) };
	}

	inline Result<size_t>
		get_class_size(_internal::unity_structs::il2cppClass* klass)
	{
		if (!klass) return { Il2CppStatus::InvalidArgs, 0 };

		auto base_addr = reinterpret_cast<uintptr_t>(klass);

		size_t offset = sizeof(_internal::unity_structs::Il2CppClass_1) + sizeof(void*) * 2;

		offset += offsetof(_internal::unity_structs::Il2CppClass_2, instance_size);

		auto instance_size = *reinterpret_cast<uint32_t*>(base_addr + offset);

		return { Il2CppStatus::OK, static_cast<size_t>(instance_size) };
	}

	inline Result<_internal::unity_structs::il2cppMethodInfo*>
		get_method(const std::string& ns,
			const std::string& class_name,
			const std::string& method_name,
			const std::string& assembly_name,
			std::optional<int> param_count = std::nullopt)
	{
		// Allow empty namespace for global classes
		if (class_name.empty() || method_name.empty() || assembly_name.empty())
			return { Il2CppStatus::InvalidArgs, nullptr };

		auto c = find_class(ns, class_name, assembly_name);
		if (!c) return { c.status, nullptr };

		using MI = _internal::unity_structs::il2cppMethodInfo*;
		MI mi = nullptr;

		if (param_count.has_value()) {
			mi = _internal::il2cpp_class_get_method_from_name(c.value, method_name.c_str(), *param_count);
		}
		else {
			for (int i = 0; i <= 16 && !mi; ++i)
				mi = _internal::il2cpp_class_get_method_from_name(c.value, method_name.c_str(), i);
		}

		if (!mi) return { Il2CppStatus::MethodNotFound, nullptr };
		if (!mi->m_pMethodPointer) return { Il2CppStatus::MethodPointerNull, mi };
		return { Il2CppStatus::OK, mi };
	}

	// ------------------------------------
	// Field-Resolvers
	// ------------------------------------
	inline Result<int>
		get_field_offset(const std::string& ns,
			const std::string& class_name,
			const std::string& field_name,
			const std::string& assembly_name)
	{
		// Allow empty namespace for global classes
		if (class_name.empty() || field_name.empty() || assembly_name.empty())
			return { Il2CppStatus::InvalidArgs, -1 };

		auto c = find_class(ns, class_name, assembly_name);
		if (!c) return { c.status, -1 };

		auto* fld = _internal::il2cpp_class_get_field_from_name(c.value, field_name.c_str());
		if (!fld) return { Il2CppStatus::FieldNotFound, -1 };
		return { Il2CppStatus::OK, fld->m_iOffset };
	}

	template <class T>
	inline Result<T>
		get_object_field_value(void* instance,
			const std::string& ns,
			const std::string& class_name,
			const std::string& field_name,
			const std::string& assembly_name)
	{
		if (!instance) return { Il2CppStatus::InvalidArgs, T{} };

		auto c = find_class(ns, class_name, assembly_name);
		if (!c) return { c.status, T{} };

		auto* fld = _internal::il2cpp_class_get_field_from_name(c.value, field_name.c_str());
		if (!fld) return { Il2CppStatus::FieldNotFound, T{} };

		T out{};
		_internal::il2cpp_field_get_value(instance, fld, &out);
		return { Il2CppStatus::OK, out };
	}

	template <class T>
	inline Il2CppStatus
		set_object_field_value(void* instance,
			const std::string& ns,
			const std::string& class_name,
			const std::string& field_name,
			const T& value,
			const std::string& assembly_name)
	{
		if (!instance) return Il2CppStatus::InvalidArgs;

		auto c = find_class(ns, class_name, assembly_name);
		if (!c) return c.status;

		auto* fld = _internal::il2cpp_class_get_field_from_name(c.value, field_name.c_str());
		if (!fld) return Il2CppStatus::FieldNotFound;

		_internal::il2cpp_field_set_value(instance, fld, const_cast<T*>(&value));
		return Il2CppStatus::OK;
	}

	template <class T>
	inline Result<T>
		get_static_field_value(_internal::unity_structs::il2cppClass* klass,
			const std::string& field_name)
	{
		if (!klass || field_name.empty()) return { Il2CppStatus::InvalidArgs, T{} };
		auto* fld = _internal::il2cpp_class_get_field_from_name(klass, field_name.c_str());
		if (!fld) return { Il2CppStatus::FieldNotFound, T{} };

		T out{};
		_internal::il2cpp_field_static_get_value(fld, &out);
		return { Il2CppStatus::OK, out };
	}

	template <class T>
	inline Il2CppStatus
		set_static_field_value(_internal::unity_structs::il2cppClass* klass,
			const std::string& field_name,
			const T& value)
	{
		if (!klass || field_name.empty()) return Il2CppStatus::InvalidArgs;
		auto* fld = _internal::il2cpp_class_get_field_from_name(klass, field_name.c_str());
		if (!fld) return Il2CppStatus::FieldNotFound;

		_internal::il2cpp_field_static_set_value(fld, const_cast<T*>(&value));
		return Il2CppStatus::OK;
	}

	// ------------------------------------
	// Object Creation
	// ------------------------------------
	
	// Forward declaration for call_function (defined below)
	template <typename Ret, typename... Args>
	inline auto call_function(_internal::unity_structs::il2cppMethodInfo* method, Args... args)
		-> std::conditional_t<std::is_void_v<Ret>, Result<void>, Result<Ret>>;

	template <typename T = void*>
	inline Result<T> create_object(_internal::unity_structs::il2cppClass* klass) {
		if (!klass) return { Il2CppStatus::ClassNotFound, nullptr };
		if (!_internal::il2cpp_object_new) return { Il2CppStatus::GetProcAddressFailed, nullptr };

		auto obj = _internal::il2cpp_object_new(klass);
		if (!obj) return { Il2CppStatus::InvalidArgs, nullptr };

		return { Il2CppStatus::OK, reinterpret_cast<T>(obj) };
	}

	template <typename T = void*, typename... CtorArgs>
	inline Result<T> create_object(
		const std::string& ns,
		const std::string& class_name,
		const std::string& assembly_name,
		CtorArgs... ctor_args)
	{
		auto klass = find_class(ns, class_name, assembly_name);
		if (!klass) return { klass.status, nullptr };

		if (!_internal::il2cpp_object_new) return { Il2CppStatus::GetProcAddressFailed, nullptr };
		auto obj = _internal::il2cpp_object_new(klass.value);
		if (!obj) return { Il2CppStatus::InvalidArgs, nullptr };

		if constexpr (sizeof...(CtorArgs) > 0) {
			auto mi_ctor = get_method(ns, class_name, ".ctor", assembly_name, sizeof...(CtorArgs));
			if (!mi_ctor) return { mi_ctor.status, nullptr };

			auto ctor_result = call_function<void>(mi_ctor.value, obj, ctor_args...);
			if (!ctor_result) return { ctor_result.status, nullptr };
		}

		return { Il2CppStatus::OK, reinterpret_cast<T>(obj) };
	}

	// ------------------------------------
	// String Utilities
	// ------------------------------------
	namespace String {
		/*
		* creates a new System.String instance
		* str: value that the new System.String will contain
		* returns: references to the created System.String instance
		*/
		inline Result<void*>
			CreateNewString(const std::string& s) {
			if (s.empty()) return { Il2CppStatus::InvalidArgs, nullptr };
			if (!_internal::il2cpp_string_new) return { Il2CppStatus::GetProcAddressFailed, nullptr };
			if (auto st = ensure_thread_attached(); st != Il2CppStatus::OK) return { st, nullptr };
			return { Il2CppStatus::OK, _internal::il2cpp_string_new(s.c_str()) };
		}

		/*
		* converts a System.String to a std::string
		* p_sys_str: System.String instance to convert
		* returns: std::string with the content of the passed System.String
		*/
		inline std::string convert_to_std_string(void* p_sys_str) {
			if (!p_sys_str) return {};

			auto get_off = [](const std::string& ns,
				const std::string& cls,
				const std::string& field,
				const std::string& asmName) -> int {
					auto asmRes = _internal::find_assembly(asmName);
					if (!asmRes || !asmRes.value->m_pImage) return -1;
					auto* k = _internal::il2cpp_class_from_name(asmRes.value->m_pImage, ns.c_str(), cls.c_str());
					if (!k) return -1;
					auto* f = _internal::il2cpp_class_get_field_from_name(k, field.c_str());
					return f ? f->m_iOffset : -1;
				};

			int off_firstChar = get_off("System", "String", "_firstChar", "mscorlib");
			int off_len = get_off("System", "String", "_stringLength", "mscorlib");
			if (off_firstChar < 0 || off_len < 0) {
				off_firstChar = get_off("System", "String", "_firstChar", "System.Private.CoreLib");
				off_len = get_off("System", "String", "_stringLength", "System.Private.CoreLib");
			}
			if (off_firstChar < 0 || off_len < 0) return {};

			const char16_t* wstr = reinterpret_cast<const char16_t*>(
				reinterpret_cast<const char*>(p_sys_str) + off_firstChar);
			const int len = *reinterpret_cast<const int*>(
				reinterpret_cast<const char*>(p_sys_str) + off_len);

			if (len <= 0) return {};

			int required = ::WideCharToMultiByte(CP_UTF8, 0,
				reinterpret_cast<LPCWCH>(wstr), len,
				nullptr, 0, nullptr, nullptr);
			if (required <= 0) return {};
			std::string out(static_cast<size_t>(required), '\0');
			::WideCharToMultiByte(CP_UTF8, 0,
				reinterpret_cast<LPCWCH>(wstr), len,
				out.data(), required, nullptr, nullptr);
			return out;
		}
	} // namespace String

	inline Result<void*> CreateNewString(const std::string& s) { return String::CreateNewString(s); }
	inline std::string   convert_to_std_string(void* p_sys_str) { return String::convert_to_std_string(p_sys_str); }

	// ------------------------------------
	// Managed Calls
	// ------------------------------------
	template <typename Ret, typename... Args>
	inline auto call_function(_internal::unity_structs::il2cppMethodInfo* method, Args... args)
		-> std::conditional_t<std::is_void_v<Ret>, Result<void>, Result<Ret>>
	{
		using R = std::conditional_t<std::is_void_v<Ret>, Result<void>, Result<Ret>>;

		if (!method) {
			if constexpr (std::is_void_v<Ret>) return R{ Il2CppStatus::MethodNotFound };
			else                                 return R{ Il2CppStatus::MethodNotFound, Ret{} };
		}
		if (!method->m_pMethodPointer) {
			if constexpr (std::is_void_v<Ret>) return R{ Il2CppStatus::MethodPointerNull };
			else                                 return R{ Il2CppStatus::MethodPointerNull, Ret{} };
		}
		if (auto st = ensure_thread_attached(); st != Il2CppStatus::OK) {
			if constexpr (std::is_void_v<Ret>) return R{ st };
			else                                 return R{ st, Ret{} };
		}

		using Fn = Ret(__fastcall*)(Args...);
		auto fn = reinterpret_cast<Fn>(method->m_pMethodPointer);

		if constexpr (std::is_void_v<Ret>) {
			fn(args...);
			return R{ Il2CppStatus::OK };
		}
		else {
			return R{ Il2CppStatus::OK, fn(args...) };
		}
	}

	// ------------------------------------
	// Arrays & Helpers
	// ------------------------------------
	inline Result<int> array_get_length_1d(void* arr) {
		auto mi = get_method("System", "Array", "GetLength", "mscorlib", 1);
		if (!mi) mi = get_method("System", "Array", "GetLength", "System.Private.CoreLib", 1);
		if (!mi) return { mi.status, 0 };
		return call_function<int>(mi.value, arr, 0 /*dimension*/);
	}

	template <typename Ret>
	inline Result<Ret> array_get_element_1d(void* arr, long long idx) {
		auto mi = get_method("System", "Array", "GetValue", "mscorlib", 1);
		if (!mi) mi = get_method("System", "Array", "GetValue", "System.Private.CoreLib", 1);
		if (!mi) return { mi.status, Ret{} };
		return call_function<Ret>(mi.value, arr, idx);
	}

	// ------------------------------------
	// Init/Cleanup
	// ------------------------------------
	inline Il2CppStatus init() {
		auto mod = _internal::ensure_game_assembly();
		if (!mod) return mod.status;
		return _internal::ensure_exports();
	}

	inline void cleanup() {
		if (_internal::il2cpp_thread_detach) _internal::il2cpp_thread_detach();
		std::scoped_lock lk(_internal::g_cache_mtx);
		_internal::g_assembly_cache.clear();
	}
} // namespace il2cpp
