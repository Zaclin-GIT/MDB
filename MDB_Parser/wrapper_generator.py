#!/usr/bin/env python3
"""
IL2CPP Dump Wrapper Generator
=============================
Generates C# wrapper classes from IL2CPP dump.cs files for any Unity/IL2CPP game.

This tool is designed to be game-agnostic and works across different:
- IL2CPP versions
- Unity versions  
- Game-specific third-party libraries

Configuration is loaded from generator_config.json for game-specific customization.
Universal .NET, Mono, and Unity internal namespaces are always skipped.
"""
import os
import re
import json
from dataclasses import dataclass, field
from typing import List, Optional, Dict, Set, Tuple


# ============================================================================
# GENERATOR CONFIGURATION
# Loads settings from generator_config.json for game-specific customization
# ============================================================================

class GeneratorConfig:
    """Configuration loaded from generator_config.json with sensible defaults."""
    
    # Universal namespaces that are ALWAYS skipped (framework types, not game-specific)
    # These exist in every IL2CPP dump regardless of game or Unity version
    UNIVERSAL_SKIP_NAMESPACES = {
        # .NET Framework / CoreCLR
        "System", "System.Collections", "System.Collections.Generic", "System.IO", "System.Text",
        "System.Threading", "System.Threading.Tasks", "System.Linq", "System.Linq.Expressions",
        "System.Reflection", "System.Runtime", "System.Runtime.CompilerServices",
        "System.Runtime.InteropServices", "System.Diagnostics", "System.Globalization",
        "System.Security", "System.ComponentModel", "System.Net", "System.Xml",
        # Mono runtime
        "Mono", "mscorlib",
        # Internal namespaces
        "Internal", "Microsoft",
        # Unity internal (not public API)
        "UnityEngine.Internal", "UnityEngineInternal",
    }
    
    # Universal namespace prefixes that are ALWAYS skipped
    UNIVERSAL_SKIP_NS_PREFIXES = (
        "System.",        # All System.* sub-namespaces
        "Mono.",          # Mono runtime internals
        "Internal.",      # Internal implementation details
        "Microsoft.",     # Microsoft runtime types
    )
    
    def __init__(self):
        # Output settings
        self.namespace_prefix = "GameSDK"
        self.output_directory = "MDB_Core/Generated"
        self.file_prefix = "GameSDK"
        
        # Game-specific skips (loaded from config)
        self.custom_skip_namespaces: Set[str] = set()
        self.custom_skip_ns_prefixes: List[str] = []
        self.custom_skip_types: Set[str] = set()
        self.custom_skip_base_types: Set[str] = set()
        
        # Auto-detection settings
        self.auto_detect_enabled = True
        self.auto_detect_patterns: List[str] = []
        
        # Detected third-party namespaces (populated during parsing)
        self.detected_third_party: Set[str] = set()
    
    @property
    def skip_namespaces(self) -> Set[str]:
        """Combined set of all namespaces to skip."""
        return self.UNIVERSAL_SKIP_NAMESPACES | self.custom_skip_namespaces | self.detected_third_party
    
    @property
    def skip_ns_prefixes(self) -> Tuple[str, ...]:
        """Combined tuple of all namespace prefixes to skip."""
        return self.UNIVERSAL_SKIP_NS_PREFIXES + tuple(self.custom_skip_ns_prefixes)
    
    def load(self, config_path: str = None) -> 'GeneratorConfig':
        """Load configuration from JSON file."""
        if config_path is None:
            search_paths = [
                "generator_config.json",
                os.path.join(os.path.dirname(__file__), "generator_config.json"),
            ]
            for path in search_paths:
                if os.path.exists(path):
                    config_path = path
                    break
        
        if config_path and os.path.exists(config_path):
            try:
                with open(config_path, 'r', encoding='utf-8') as f:
                    data = json.load(f)
                
                # Load output settings
                output = data.get("output", {})
                self.namespace_prefix = output.get("namespace_prefix", self.namespace_prefix)
                self.output_directory = output.get("output_directory", self.output_directory)
                self.file_prefix = output.get("file_prefix", self.file_prefix)
                
                # Load custom skip lists
                self.custom_skip_namespaces = set(data.get("skip_namespaces", {}).get("custom", []))
                self.custom_skip_ns_prefixes = data.get("skip_namespace_prefixes", {}).get("custom", [])
                self.custom_skip_types = set(data.get("skip_types", {}).get("custom", []))
                self.custom_skip_base_types = set(data.get("skip_base_types", {}).get("custom", []))
                
                # Load auto-detection settings
                auto_detect = data.get("auto_detect_third_party", {})
                self.auto_detect_enabled = auto_detect.get("enabled", True)
                self.auto_detect_patterns = auto_detect.get("patterns", [])
                
                print(f"[Config] Loaded configuration from {config_path}")
                if self.custom_skip_namespaces:
                    print(f"[Config] Custom skip namespaces: {len(self.custom_skip_namespaces)}")
                if self.custom_skip_ns_prefixes:
                    print(f"[Config] Custom skip prefixes: {len(self.custom_skip_ns_prefixes)}")
                    
            except Exception as e:
                print(f"[Config] Warning: Could not load config: {e}, using defaults")
        else:
            print("[Config] No generator_config.json found - using defaults")
        
        return self
    
    def detect_third_party_from_namespaces(self, all_namespaces: Set[str]) -> None:
        """Auto-detect third-party libraries from namespace names."""
        if not self.auto_detect_enabled:
            return
        
        detected = set()
        for ns in all_namespaces:
            if not ns:
                continue
            # Check against auto-detection patterns
            for pattern in self.auto_detect_patterns:
                if ns == pattern or ns.startswith(pattern + "."):
                    detected.add(ns)
                    break
        
        if detected:
            self.detected_third_party = detected
            print(f"[Config] Auto-detected {len(detected)} third-party namespaces to skip")
    
    def should_skip_namespace(self, ns: str) -> bool:
        """Check if a namespace should be skipped."""
        if not ns:
            return False
        if ns in self.skip_namespaces:
            return True
        if ns.startswith(self.skip_ns_prefixes):
            return True
        return False


# Global config instance
CONFIG = GeneratorConfig()


# ============================================================================
# DEOBFUSCATION MAPPING SUPPORT
# Load mappings.json to use friendly names in generated wrappers
# ============================================================================
DEOBFUSCATION_MAPPINGS: Dict[str, Dict] = {}  # obfuscated_name -> mapping dict

def load_deobfuscation_mappings(mappings_path: str = None):
    """Load deobfuscation mappings from JSON file."""
    global DEOBFUSCATION_MAPPINGS
    DEOBFUSCATION_MAPPINGS.clear()
    
    if mappings_path is None:
        # Look for mappings.json in common locations
        search_paths = [
            "mappings.json",
            "../mappings.json",
            os.path.join(os.path.dirname(__file__), "mappings.json"),
            os.path.join(os.path.dirname(__file__), "..", "mappings.json"),
        ]
        for path in search_paths:
            if os.path.exists(path):
                mappings_path = path
                break
    
    if mappings_path and os.path.exists(mappings_path):
        try:
            with open(mappings_path, 'r', encoding='utf-8') as f:
                mappings_list = json.load(f)
            for m in mappings_list:
                obf_name = m.get("ObfuscatedName")
                if obf_name:
                    DEOBFUSCATION_MAPPINGS[obf_name] = m
            print(f"[Deobfuscation] Loaded {len(DEOBFUSCATION_MAPPINGS)} mappings from {mappings_path}")
        except Exception as e:
            print(f"[Deobfuscation] Warning: Could not load mappings: {e}")
    else:
        print("[Deobfuscation] No mappings.json found - using obfuscated names")

def get_friendly_name(obfuscated_name: str) -> Optional[str]:
    """Get friendly name for an obfuscated symbol, or None if not mapped."""
    mapping = DEOBFUSCATION_MAPPINGS.get(obfuscated_name)
    if mapping:
        return mapping.get("FriendlyName")
    return None

def get_mapping(obfuscated_name: str) -> Optional[Dict]:
    """Get full mapping dict for an obfuscated symbol."""
    return DEOBFUSCATION_MAPPINGS.get(obfuscated_name)


# ============================================================================
# DEBUG CONFIGURATION
# Set DEBUG = True to enable verbose tracing of execution order and function calls
# ============================================================================
DEBUG = False  # Set to True to enable debug output
_DEBUG_STEP = 0  # Global step counter for order of operations
_FUNC_CALL_COUNTS = {}  # Track how many times each function is called

def _debug(msg: str, step: bool = True) -> None:
    """Print debug message with optional step counter. [DEBUG UTILITY]"""
    global _DEBUG_STEP
    if not DEBUG:
        return
    if step:
        _DEBUG_STEP += 1
        print(f"[DEBUG #{_DEBUG_STEP:04d}] {msg}")
    else:
        print(f"[DEBUG       ] {msg}")

def _track_call(func_name: str) -> None:
    """Track function call count for unused function detection. [DEBUG UTILITY]"""
    global _FUNC_CALL_COUNTS
    _FUNC_CALL_COUNTS[func_name] = _FUNC_CALL_COUNTS.get(func_name, 0) + 1
    if DEBUG:
        _debug(f"CALL: {func_name}() [call #{_FUNC_CALL_COUNTS[func_name]}]", step=False)

def print_function_usage_report() -> None:
    """Print report of function call counts to identify unused functions. [DEBUG UTILITY]"""
    print("\n" + "=" * 60)
    print("FUNCTION USAGE REPORT")
    print("=" * 60)
    all_funcs = [
        # Core parser functions
        "parse_dump_file", "_parse_type", "count_char",
        # Type registry
        "build_type_registry",
        # Code generation helpers  
        "generate_smart_usings", "is_type_resolvable", "is_obfuscated_type",
        "map_type", "has_valid_csharp_identifier_chars", "is_unicode_name",
        "sanitize_unicode_namespace", "sanitize_name", "is_generic_type_param",
        "get_generic_type_params_from_method", "has_generic_type_arg",
        "is_backtick_generic", "_check_type_valid", "is_valid_method",
        "is_valid_property", "compute_imported_namespaces", "_should_skip_type",
        "_add_type_with_rename", "get_renamed_class_name",
        # Main generator
        "generate_wrapper_code_per_namespace",
        # Build functions
        "find_csc_compiler", "check_dotnet_available", "get_framework_references",
        "build_project", "build_with_dotnet", "build_with_csc",
        # Main
        "main"
    ]
    for func in sorted(all_funcs):
        count = _FUNC_CALL_COUNTS.get(func, 0)
        status = "[USED]" if count > 0 else "[UNUSED]"
        print(f"  {status}: {func}() - called {count} times")
    print("=" * 60 + "\n")


# ---------- Global type registry (built during parsing) ----------
# Maps type name -> list of namespaces where this type exists (public types only)
TYPE_REGISTRY: Dict[str, List[str]] = {}

# Maps base type name -> list of namespaces where GENERIC versions exist
# e.g., "UniTask" -> ["Cysharp.Threading.Tasks"] means UniTask<T> exists there
GENERIC_TYPE_REGISTRY: Dict[str, List[str]] = {}

# Set of (type_name, namespace) tuples for types that will actually be generated
# This excludes empty classes that pass visibility checks but have no content
GENERATED_TYPES: Set[Tuple[str, str]] = set()

# Set of type names that are sealed - cannot be inherited from
SEALED_TYPES: Set[str] = set()


# ---------- Skip Lists (Global Constants) ----------

# Types that cannot be base classes (no IntPtr ctor, sealed, abstract, or cause conflicts)
SKIP_BASE_TYPES = {
    "MulticastDelegate", "Delegate", "Enum", "ValueType", "Array", "Attribute", "Exception",
    "ApplicationException", "SystemException", "EventArgs", "ArrayList", "Hashtable", "Dictionary",
    "SynchronizationContext", "PropertyDescriptor", "MemberDescriptor", "TypeConverter",
    "SerializationBinder", "Stream", "MemoryStream", "PropertyAttribute", "PreserveAttribute",
    "UnityException", "InputDevice", "Pointer", "Toggle", "Sensor", "UxmlTraits", "UxmlFactory",
    "JsonContainerAttribute", "JsonException", "MaterialReference", "Match", "Capture", "Group",
    "unitytls_tlsctx_read_callback", "unitytls_tlsctx_write_callback", "CaptureResultType",
    "AssetReferenceUIRestriction", "Space", "Action",
}

# Generic nested enum names that cause conflicts (likely nested enums)
SKIP_NESTED_ENUM_NAMES = {
    "Type", "Direction", "Mode", "Status", "Button", "Flags", "Axis", "Sign", "Unit", "State",
    "Kind", "Options", "Result", "Action", "Event", "Value", "Index", "UpdateMode",
    "CaptureResultType", "ContentType", "InputType", "CharacterValidation", "LineType",
    "WorldUpType", "FpsCounterAnchorPositions"
}


# ---------- Data models ----------

@dataclass
class ParameterDef:
    modifier: str
    type: str
    name: str

@dataclass
class MethodDef:
    name: str
    return_type: str
    is_static: bool
    visibility: str
    parameters: List[ParameterDef] = field(default_factory=list)
    rva: Optional[str] = None  # RVA address like "0x52f1e0" for direct method calls

@dataclass
class PropertyDef:
    name: str
    type: str
    visibility: str
    has_get: bool
    has_set: bool

@dataclass
class FieldDef:
    name: str
    type: str
    visibility: str
    is_const: bool = False
    value: Optional[str] = None

@dataclass
class TypeDef:
    dll: Optional[str]
    namespace: Optional[str]
    kind: str
    name: str
    base_type: Optional[str]
    visibility: str
    is_sealed: bool = False  # Track sealed types to prevent inheritance from them
    properties: List[PropertyDef] = field(default_factory=list)
    methods: List[MethodDef] = field(default_factory=list)
    fields: List['FieldDef'] = field(default_factory=list)


# ---------- Regexes tuned for IL2CPP dump.cs ----------

DLL_RE = re.compile(r'^//\s*Dll\s*:\s*(.+)$')
NS_RE = re.compile(r'^//\s*Namespace:\s*(.*)$')

CLASS_HEADER_RE = re.compile(
    r'^(public|internal|private)\s+'
    r'((?:sealed\s+|abstract\s+|static\s+)*)'
    r'(class|interface|enum|struct)\s+'
    r'(\S+)'                         
    r'(?:\s*:\s*(\S+))?'             
)

PROPERTY_RE = re.compile(
    r'^\s*(public|internal|private)\s+'
    r'([\w\.`\[\]]+)\s+'
    r'(\w+)\s*'
    r'\{([^}]*)\}'
)

METHOD_RE = re.compile(
    r'^\s*(public|internal|private)\s+'
    r'(static\s+|virtual\s+|override\s+|abstract\s+|sealed\s+)?'
    r'([\w\.`\[\]]+)\s+'
    r'(\S+)\s*'
    r'\(([^)]*)\)'
)

PARAM_RE = re.compile(
    r'^\s*(out|ref|in)?\s*'
    r'([^ ]+)\s+'
    r'([^ ]+)\s*$'
)

# Matches enum/class fields like: public const OGEFGINAMLN None = 0;
# Also matches protected fields for IL2CPP modding access
FIELD_RE = re.compile(
    r'^\s*(public|protected|internal|private)\s+'
    r'(const\s+)?'
    r'([\w\.`\[\]]+)\s+'
    r'(\w+)\s*'
    r'(?:=\s*([^;]+))?;'
)


def count_char(s: str, ch: str) -> int:
    # DEBUG STATS: ~1,050,914 calls - HIGHEST usage (angle bracket counting for generics)
    """Count occurrences of a character in string. [STEP 2.1: Called during type parsing]"""
    _track_call("count_char")
    return s.count(ch)


# ---------- Core parser ----------
# EXECUTION ORDER:
#   Step 1: main() called
#   Step 2: parse_dump_file() reads and parses dump.cs
#   Step 2.1: _parse_type() called for each type found
#   Step 3: build_type_registry() builds lookup tables
#   Step 4: generate_wrapper_code_per_namespace() generates C# files
#   Step 5: build_project() compiles the generated code

def parse_dump_file(path: str) -> List[TypeDef]:
    """Parse IL2CPP dump file into TypeDef list. [STEP 2: Main parsing entry point]"""
    _track_call("parse_dump_file")
    _debug(f"STEP 2: Parsing dump file: {path}")
    with open(path, "r", encoding="utf-8", errors="ignore") as f:
        lines = f.readlines()

    types: List[TypeDef] = []
    current_dll = None
    current_ns = None

    i = 0
    n = len(lines)

    while i < n:
        raw = lines[i]
        line = raw.strip()

        m = DLL_RE.match(line)
        if m:
            current_dll = m.group(1).strip()
            i += 1
            continue

        m = NS_RE.match(line)
        if m:
            ns = m.group(1).strip()
            current_ns = ns if ns else None
            i += 1
            continue

        m = CLASS_HEADER_RE.match(line)
        if m:
            type_def, new_i = _parse_type(lines, i, current_dll, current_ns, m)
            types.append(type_def)
            i = new_i + 1
            continue

        i += 1

    return types


def _parse_type(lines, start_index, dll, ns, header_match):
    """Parse a single type definition. [STEP 2.1: Called per type in dump]"""
    _track_call("_parse_type")
    visibility = header_match.group(1)
    modifiers = header_match.group(2) or ""  # sealed/abstract/static modifiers
    kind = header_match.group(3)
    name = header_match.group(4)
    base_type = header_match.group(5) if header_match.group(5) else None
    is_sealed = "sealed" in modifiers

    type_def = TypeDef(
        dll=dll,
        namespace=ns,
        kind=kind,
        name=name,
        base_type=base_type,
        visibility=visibility,
        is_sealed=is_sealed,
    )

    i = start_index
    brace_depth = 0
    type_started = False
    n = len(lines)

    while i < n:
        line = lines[i]
        if not type_started:
            if '{' in line:
                type_started = True
                brace_depth += count_char(line, '{')
                brace_depth -= count_char(line, '}')
                if brace_depth == 0:
                    return type_def, i
                i += 1
                break
            i += 1
            continue
        else:
            break

    in_properties = False
    in_methods = False
    in_fields = False
    pending_rva = None  # Stores RVA from comment line for next method

    while i < n:
        raw = lines[i]
        trimmed = raw.strip()

        brace_depth += count_char(raw, '{')
        brace_depth -= count_char(raw, '}')

        if brace_depth <= 0:
            return type_def, i

        if trimmed.startswith("// Fields"):
            in_fields = True
            in_properties = False
            in_methods = False
            i += 1
            continue

        if trimmed.startswith("// Properties"):
            in_properties = True
            in_methods = False
            in_fields = False
            i += 1
            continue

        if trimmed.startswith("// Methods"):
            in_methods = True
            in_properties = False
            in_fields = False
            i += 1
            continue

        if in_fields:
            fm = FIELD_RE.match(trimmed)
            if fm:
                vis = fm.group(1)
                is_const = fm.group(2) is not None
                f_type = fm.group(3)
                f_name = fm.group(4)
                f_value = fm.group(5).strip() if fm.group(5) else None
                
                type_def.fields.append(
                    FieldDef(
                        name=f_name,
                        type=f_type,
                        visibility=vis,
                        is_const=is_const,
                        value=f_value,
                    )
                )
                i += 1
                continue

        if in_properties:
            pm = PROPERTY_RE.match(trimmed)
            if pm:
                vis = pm.group(1)
                p_type = pm.group(2)
                p_name = pm.group(3)
                accessor = pm.group(4)

                type_def.properties.append(
                    PropertyDef(
                        name=p_name,
                        type=p_type,
                        visibility=vis,
                        has_get=("get;" in accessor),
                        has_set=("set;" in accessor),
                    )
                )
                i += 1
                continue

        if in_methods:
            # Parse RVA comment line and store for next method
            if trimmed.startswith("// RVA:"):
                # Extract RVA value, e.g., "// RVA: 0x52f1e0 VA: 0x7ffb2a8cf1e0" -> "0x52f1e0"
                rva_match = re.search(r'RVA:\s*(0x[0-9A-Fa-f]+)', trimmed)
                if rva_match:
                    pending_rva = rva_match.group(1)
                else:
                    pending_rva = None
                i += 1
                continue

            mm = METHOD_RE.match(trimmed)
            if mm:
                vis = mm.group(1)
                is_static = mm.group(2) is not None
                ret = mm.group(3)
                m_name = mm.group(4)
                param_block = mm.group(5).strip()

                method = MethodDef(
                    name=m_name,
                    return_type=ret,
                    is_static=is_static,
                    visibility=vis,
                    rva=pending_rva,  # Assign captured RVA
                )
                pending_rva = None  # Clear for next method

                if param_block:
                    parts = [p.strip() for p in param_block.split(',') if p.strip()]
                    for p in parts:
                        pm = PARAM_RE.match(p)
                        if pm:
                            captured_modifier = pm.group(1) or ""
                            captured_type = pm.group(2)
                            captured_name = pm.group(3)
                            
                            # Handle edge case: parameters without names like "ref TYPE"
                            # In this case, regex captures ref as modifier=None, type="ref", name="TYPE"
                            # We need to detect this and fix it
                            if captured_type in ("ref", "out", "in"):
                                # The "type" is actually a modifier, and "name" is actually the type
                                # This means there's no parameter name - skip this parameter
                                # by marking it with a special modifier so is_valid_method can reject it
                                captured_modifier = captured_type
                                captured_type = captured_name
                                captured_name = "__no_name__"
                            
                            method.parameters.append(
                                ParameterDef(
                                    modifier=captured_modifier,
                                    type=captured_type,
                                    name=captured_name,
                                )
                            )

                type_def.methods.append(method)

        i += 1

    return type_def, n - 1


# ---------- Type Registry Builder ----------

def build_type_registry(types: List[TypeDef]) -> None:
    """Build a global registry of type names -> namespaces. [STEP 3: Build lookup tables]"""
    _track_call("build_type_registry")
    _debug(f"STEP 3: Building type registry from {len(types)} types")
    global TYPE_REGISTRY, GENERIC_TYPE_REGISTRY, GENERATED_TYPES, SEALED_TYPES
    TYPE_REGISTRY.clear()
    GENERIC_TYPE_REGISTRY.clear()
    GENERATED_TYPES.clear()
    SEALED_TYPES.clear()
    
    # Auto-detect third-party namespaces if enabled
    if CONFIG.auto_detect_enabled:
        all_namespaces = {t.namespace for t in types if t.namespace}
        CONFIG.detect_third_party_from_namespaces(all_namespaces)
    
    # First pass: identify all sealed types
    for t in types:
        if t.is_sealed and t.name:
            SEALED_TYPES.add(t.name)
    
    for t in types:
        if not t.name or t.visibility != "public": continue
        if is_unicode_name(t.name.split("`")[0].split("<")[0]): continue
        ns = t.namespace or "Global"
        
        # Skip types from namespaces that won't be generated (uses CONFIG for dynamic skip lists)
        if CONFIG.should_skip_namespace(ns):
            continue
        
        # DEBUG: Track MaterialReference specifically
        debug_type = t.name == "MaterialReference" and ns == "TMPro"
        if debug_type:
            print(f"[DEBUG MaterialReference] Found: name={t.name}, ns={ns}, kind={t.kind}")
            print(f"[DEBUG MaterialReference] visibility={t.visibility}, base_type={t.base_type}")
            print(f"[DEBUG MaterialReference] methods={len(t.methods)}, properties={len(t.properties)}, fields={len(t.fields)}")
        
        # Check if this type will actually be generated (has content or is enum)
        has_content = False
        if t.name in SKIP_TYPES or t.base_type == "MulticastDelegate":
            has_content = False
            if debug_type: print(f"[DEBUG MaterialReference] Skipped: in SKIP_TYPES or MulticastDelegate")
        # Skip types that inherit from sealed types (CS0509 error)
        elif t.base_type and t.base_type.rstrip(",").strip() in SEALED_TYPES:
            has_content = False
            if debug_type: print(f"[DEBUG MaterialReference] Skipped: base_type in SEALED_TYPES")
        elif t.kind == "enum":
            has_content = bool(t.fields) and t.name not in SKIP_NESTED_ENUM_NAMES
            if debug_type: print(f"[DEBUG MaterialReference] Enum check: has_content={has_content}")
        elif t.methods or t.properties or t.fields:
            clean_base = t.base_type.rstrip(",").strip() if t.base_type else None
            has_content = not (clean_base and clean_base in SKIP_BASE_TYPES)
            if debug_type: print(f"[DEBUG MaterialReference] Content check: clean_base={clean_base}, has_content={has_content}")
        
        if has_content:
            GENERATED_TYPES.add((t.name, ns))
            # Also register friendly name (deobfuscated) if it exists
            friendly = get_friendly_name(t.name)
            if friendly:
                GENERATED_TYPES.add((friendly, ns))
        
        # Register type (generic or non-generic)
        if "`" in t.name or "<" in t.name:
            base_name = t.name.split("`")[0].split("<")[0]
            GENERIC_TYPE_REGISTRY.setdefault(base_name, [])
            if ns not in GENERIC_TYPE_REGISTRY[base_name]:
                GENERIC_TYPE_REGISTRY[base_name].append(ns)
        else:
            TYPE_REGISTRY.setdefault(t.name, [])
            if ns not in TYPE_REGISTRY[t.name]:
                TYPE_REGISTRY[t.name].append(ns)
            # Also register friendly name (deobfuscated) if it exists
            friendly = get_friendly_name(t.name)
            if friendly:
                TYPE_REGISTRY.setdefault(friendly, [])
                if ns not in TYPE_REGISTRY[friendly]:
                    TYPE_REGISTRY[friendly].append(ns)
    
    print(f"[+] Built type registry with {len(TYPE_REGISTRY)} non-generic and {len(GENERIC_TYPE_REGISTRY)} generic type names")
    print(f"[+] Types with content (will be generated): {len(GENERATED_TYPES)}")


def generate_smart_usings(code_body_lines: List[str], current_ns: str, 
                          all_types: List[TypeDef], valid_namespaces: set) -> List[str]:
    # DEBUG STATS: ~389 calls - Called once per namespace file generated
    """Generate using statements for namespace file. [STEP 4.5: Called per namespace file]"""
    _track_call("generate_smart_usings")
    using_lines = ["using System;", "using System.Collections;", "using System.Collections.Generic;",
                   "using GameSDK;", ""]
    
    # Core Unity namespaces
    core_unity = {"UnityEngine", "UnityEngine.UI", "UnityEngine.Events", "UnityEngine.EventSystems",
                  "UnityEngine.Rendering", "UnityEngine.SceneManagement", "UnityEngine.Audio",
                  "UnityEngine.AI", "UnityEngine.Animations", "TMPro", "Unity.Mathematics"}
    using_lines.append("// Core Unity namespace references")
    for ns in sorted(core_unity):
        if ns != current_ns:
            using_lines.append(f"using {ns};")
    using_lines.append("")
    
    # System namespaces for common types
    using_lines.extend(["// System namespaces for common types", "using System.Text;",
                        "using System.IO;", "using System.Xml;", "using System.Reflection;",
                        "using System.Globalization;", "using System.Runtime.Serialization;",
                        "using System.Threading;", "using System.Threading.Tasks;", ""])
    return using_lines


# ---------- Code Generator ----------

# Well-known types that are always available - C# built-ins and .NET types
# These are universal across all Unity/IL2CPP versions
KNOWN_TYPES = {
    "void", "bool", "byte", "sbyte", "char", "decimal", "double", "float", "int", "uint",
    "long", "ulong", "short", "ushort", "object", "string", "IntPtr", "UIntPtr", "Type",
    "Array", "Exception", "EventArgs", "Delegate", "MulticastDelegate", "Action", "Func",
    "Predicate", "Comparison", "Converter", "List", "Dictionary", "HashSet", "Queue", "Stack",
    "KeyValuePair", "Task", "ValueTask", "CancellationToken", "CancellationTokenSource",
    "TimeSpan", "DateTime", "DateTimeOffset", "Guid", "Uri", "Version", "Nullable", "Lazy",
    "WeakReference", "Tuple", "ValueTuple", "Stream", "MemoryStream", "StreamReader",
    "StreamWriter", "BinaryReader", "BinaryWriter", "StringBuilder", "Encoding", "XmlNode",
    "XmlDocument", "XmlElement", "XmlAttribute", "IEnumerator", "IEnumerable", "ICollection",
    "IList", "IDictionary", "IDisposable", "ICloneable", "IComparable", "IEquatable",
    "IFormattable", "Il2CppObject",
    # Unity.Mathematics types - commonly used in modern Unity games
    "float2", "float3", "float4", "float2x2", "float3x3", "float4x4",
    "int2", "int3", "int4", "uint2", "uint3", "uint4",
    "bool2", "bool3", "bool4", "half", "half2", "half3", "half4", "quaternion",
}

# Types that should be excluded (external runtime types not available in target framework)
# These are universal and exist in all IL2CPP dumps
UNRESOLVABLE_TYPES = {
    "Enumeration",  # System.IO.Enumeration doesn't exist in standard .NET
    "Path",  # System.IO.Path is static, can't be used as parameter type
    "UnityEngineInternal",  # Internal Unity namespace
}


def get_unresolvable_prefixes() -> Tuple[str, ...]:
    """Get namespace prefixes to exclude - uses CONFIG for game-specific additions."""
    return CONFIG.skip_ns_prefixes


def get_unresolvable_namespaces() -> Set[str]:
    """Get exact namespaces to exclude - uses CONFIG for game-specific additions."""
    return CONFIG.skip_namespaces


def is_type_resolvable(type_name: str, current_namespace: str = None, imported_namespaces: set = None) -> bool:
    # DEBUG STATS: ~142,057 calls - Critical for method/property validation
    """Check if a type can be resolved at compile time. [STEP 4.x: Type validation]"""
    _track_call("is_type_resolvable")
    if not type_name:
        return False
    
    # Check for unresolvable namespace prefixes (check full type name before extraction)
    unresolvable_prefixes = get_unresolvable_prefixes()
    for prefix in unresolvable_prefixes:
        if type_name.startswith(prefix):
            return False
    
    is_used_as_generic = ("<" in type_name and ">" in type_name) or "`" in type_name
    base_name = type_name.split("<")[0].split("[")[0].split("`")[0].strip("?")
    
    # Check for unresolvable types
    if base_name in UNRESOLVABLE_TYPES:
        return False
    
    if is_generic_type_param(base_name) or base_name in TYPE_MAP or base_name in KNOWN_TYPES:
        return True
    
    if is_used_as_generic:
        known_generics = {
            "List", "Dictionary", "HashSet", "Queue", "Stack", "LinkedList", "SortedList",
            "SortedDictionary", "SortedSet", "KeyValuePair", "IList", "IDictionary", "ICollection",
            "IEnumerable", "IEnumerator", "IReadOnlyList", "IReadOnlyCollection", "IReadOnlyDictionary",
            "ISet", "IComparer", "IEqualityComparer", "Nullable", "Tuple", "ValueTuple", "Lazy",
            "WeakReference", "Action", "Func", "Predicate", "Comparison", "Converter", "EventHandler",
            "ArraySegment", "Memory", "Span", "ReadOnlyMemory", "ReadOnlySpan", "Task", "ValueTask",
            "NativeArray", "NativeList", "NativeHashMap", "NativeQueue",
        }
        if base_name not in known_generics:
            return False
        # Parse and check generic type arguments
        inner = type_name[type_name.find("<")+1:type_name.rfind(">")]
        depth, current, args = 0, "", []
        for c in inner:
            if c == '<':
                depth += 1
                current += c
            elif c == '>':
                depth -= 1
                current += c
            elif c == ',' and depth == 0:
                args.append(current.strip())
                current = ""
            else:
                current += c
        if current.strip():
            args.append(current.strip())
        
        # Check each generic argument is resolvable
        for arg in args:
            if not is_type_resolvable(arg, current_namespace, imported_namespaces):
                return False
        return True
    
    # Check if it's in our generated type registry (non-generic)
    if base_name in TYPE_REGISTRY:
        type_namespaces = TYPE_REGISTRY.get(base_name, [])
        # Filter out unresolvable namespaces before checking
        unresolvable_ns = get_unresolvable_namespaces()
        unresolvable_prefixes = get_unresolvable_prefixes()
        resolvable_namespaces = []
        for ns in type_namespaces:
            # Check explicit namespace list
            if ns in unresolvable_ns:
                continue
            # Check prefix patterns
            if ns.startswith(unresolvable_prefixes):
                continue
            resolvable_namespaces.append(ns)
        # Type is resolvable if it exists in ANY resolvable namespace we're generating
        # Cross-namespace references will use fully-qualified names via get_qualified_type_name()
        return any((base_name, ns) in GENERATED_TYPES for ns in resolvable_namespaces)
    return False


def get_qualified_type_name(type_name: str, current_ns: str, imported_ns: set) -> str:
    """
    Get fully-qualified type name if needed for cross-namespace references.
    Returns the type with namespace prefix if it's from a non-imported namespace.
    """
    # Handle array types
    is_array = type_name.endswith("[]")
    base_type = type_name[:-2] if is_array else type_name
    
    # Skip primitives and known .NET types - they don't need qualification
    if base_type in TYPE_MAP or base_type in KNOWN_TYPES:
        return type_name
    
    # Check if this type exists in our registry
    if base_type not in TYPE_REGISTRY:
        return type_name  # Not in registry, return as-is
    
    namespaces_with_type = TYPE_REGISTRY.get(base_type, [])
    
    # Check if type is already accessible (in current or imported namespace)
    for ns in namespaces_with_type:
        if (ns == current_ns or ns in imported_ns) and (base_type, ns) in GENERATED_TYPES:
            return type_name  # Already accessible, no qualification needed
    
    # Type exists but not in current/imported namespaces - need full qualification
    # Find the first namespace where this type is actually generated
    # Skip namespaces with unresolvable prefixes
    # Use global:: prefix to force absolute namespace resolution (avoids relative lookup issues)
    unresolvable_prefixes = get_unresolvable_prefixes()
    for ns in namespaces_with_type:
        # Skip unresolvable namespaces
        ns_with_dot = ns + "."
        if any(ns_with_dot.startswith(prefix) or ns == prefix.rstrip(".") for prefix in unresolvable_prefixes):
            continue
        if (base_type, ns) in GENERATED_TYPES:
            qualified = f"global::{ns}.{base_type}"
            return qualified + "[]" if is_array else qualified
    
    return type_name  # Fallback


# Core namespaces always imported (Global NOT included - types there shadow Unity/System types)
ALWAYS_IMPORTED_NAMESPACES = {
    "System", "System.Collections", "System.Collections.Generic", "System.Text", "System.IO",
    "System.Xml", "System.Reflection", "System.Globalization", "System.Runtime.Serialization",
    "System.Threading", "System.Threading.Tasks", "UnityEngine", "UnityEngine.UI",
    "UnityEngine.Events", "UnityEngine.EventSystems", "UnityEngine.Rendering",
    "UnityEngine.SceneManagement", "UnityEngine.Audio", "UnityEngine.AI",
    "UnityEngine.Animations", "TMPro", "GameSDK",
}


# Map IL2CPP type names to C# type names
TYPE_MAP = {
    "Void": "void",
    "Boolean": "bool",
    "Int32": "int",
    "Int64": "long",
    "UInt32": "uint",
    "UInt64": "ulong",
    "Single": "float",
    "Double": "double",
    "String": "string",
    "Object": "object",  # lowercase to avoid ambiguity with UnityEngine.Object
    "Byte": "byte",
    "SByte": "sbyte",
    "Int16": "short",
    "UInt16": "ushort",
    "Char": "char", "IntPtr": "IntPtr", "UIntPtr": "UIntPtr",
}

# Types to skip (cause conflicts or have unresolvable dependencies)
# Note: Types inheriting from sealed types are now auto-detected via SEALED_TYPES registry
# SKIP_TYPES is now empty - the generator handles all types automatically
# Previous entries were removed after fixing is_generic_type_param to not match underscore types
SKIP_TYPES = set()

def get_renamed_class_name(type_name: str, namespace: str) -> Tuple[str, str]:
    # DEBUG STATS: ~9,081 calls - Name resolution for type conflicts
    """Get renamed class name with deobfuscation support. [STEP 4.x: Name resolution]
    
    Returns (display_name, original_name) tuple.
    Uses friendly names from mappings.json when available.
    """
    _track_call("get_renamed_class_name")
    # Check for deobfuscation mapping (friendly name)
    friendly_name = get_friendly_name(type_name)
    if friendly_name:
        return friendly_name, type_name
    return type_name, type_name

# Property names that conflict with C# keywords or System types
SKIP_PROPERTY_NAMES = {"Type", "Object", "String", "Int32", "Boolean", "Array"}

# Types that need full qualification for INHERITANCE (base class disambiguation)
# Only include types where the base class meaning is unambiguous across all games
BASE_TYPE_DISAMBIGUATION = {
    "Object": "UnityEngine.Object",  # Classes inheriting Object always mean Unity's Object
}

def resolve_ambiguous_type(type_name: str, current_ns: str, imported_ns: set) -> str:
    """
    Resolve a type name to its fully qualified form if ambiguous.
    Uses TYPE_REGISTRY to determine if type exists in multiple namespaces.
    
    Strategy:
    1. Check if type name conflicts with a namespace part - fully qualify
    2. If type is in current namespace, use it unqualified
    3. If type exists in exactly one imported namespace, use it unqualified  
    4. If type exists in multiple imported namespaces, fully qualify it
    5. If type not found in registry, return as-is (it's a known type or will fail later)
    """
    # Handle array types
    is_array = type_name.endswith("[]")
    base_type = type_name[:-2] if is_array else type_name
    
    # Skip primitives and known .NET types - they don't need qualification
    if base_type in TYPE_MAP or base_type in KNOWN_TYPES:
        return type_name
    
    # Check if type name conflicts with any part of the current namespace
    # e.g., Camera in DecaGames.RotMG.Graphics.Camera namespace
    if current_ns:
        ns_parts = set(current_ns.split("."))
        if base_type in ns_parts:
            # Type name matches a namespace part - must fully qualify
            # Find where this type actually exists
            if base_type in TYPE_REGISTRY:
                namespaces_with_type = TYPE_REGISTRY.get(base_type, [])
                # Prefer UnityEngine namespace
                for ns in namespaces_with_type:
                    if ns.startswith("UnityEngine"):
                        qualified = f"{ns}.{base_type}"
                        return qualified + "[]" if is_array else qualified
                # Fallback to first match
                if namespaces_with_type:
                    qualified = f"{namespaces_with_type[0]}.{base_type}"
                    return qualified + "[]" if is_array else qualified
    
    # Check if this type exists in our registry
    if base_type not in TYPE_REGISTRY:
        return type_name  # Not in registry, return as-is
    
    namespaces_with_type = TYPE_REGISTRY.get(base_type, [])
    
    # If only one namespace has this type, no ambiguity
    if len(namespaces_with_type) <= 1:
        return type_name
    
    # Type exists in multiple namespaces - need to resolve
    # Priority 1: Current namespace
    if current_ns in namespaces_with_type:
        return type_name  # Use unqualified, current namespace takes precedence
    
    # Priority 2: Check imported namespaces
    matching_imported = [ns for ns in namespaces_with_type if ns in imported_ns]
    
    if len(matching_imported) == 1:
        # Exactly one imported namespace has it - use unqualified
        return type_name
    elif len(matching_imported) > 1:
        # Multiple imported namespaces have it - must qualify
        # Pick the first matching namespace (prefer Unity namespaces)
        for preferred_prefix in ["UnityEngine", "System", "TMPro"]:
            for ns in matching_imported:
                if ns.startswith(preferred_prefix):
                    qualified = f"{ns}.{base_type}"
                    return qualified + "[]" if is_array else qualified
        # Fallback to first match
        qualified = f"{matching_imported[0]}.{base_type}"
        return qualified + "[]" if is_array else qualified
    else:
        # Not in any imported namespace - qualify with first available
        # This type won't be resolvable anyway, but qualify for clarity
        qualified = f"{namespaces_with_type[0]}.{base_type}"
        return qualified + "[]" if is_array else qualified

def is_obfuscated_type(type_name: str) -> bool:
    # DEBUG STATS: ~2,168 calls - Detects obfuscation patterns (Malayalam, Greek, etc.)
    """Check if type name is obfuscated. [STEP 4.x: Type validation helper]"""
    _track_call("is_obfuscated_type")
    # Get the base type name without generics or arrays
    base = type_name.split("<")[0].split("[")[0].split("`")[0]
    
    # Check for non-ASCII characters (Malayalam, Greek, etc. obfuscation)
    if not has_valid_csharp_identifier_chars(base):
        return True
    
    # Check if it's all uppercase letters and >= 8 chars (obfuscated types are usually long)
    if len(base) >= 8 and base.isupper() and base.isalpha():
        return True
    return False

def map_type(il2cpp_type: str, current_ns: str = None, imported_ns: set = None) -> str:
    # DEBUG STATS: ~204,915 calls - Type conversion from IL2CPP to C#
    """Convert IL2CPP type to C# type. [STEP 4.x: Type mapping]
    
    If current_ns and imported_ns are provided, will resolve ambiguous types
    using the TYPE_REGISTRY instead of hardcoded mappings.
    """
    _track_call("map_type")
    if not il2cpp_type or "*" in il2cpp_type:
        return None
    if "InputAction.CallbackContext" in il2cpp_type:
        return il2cpp_type.replace("InputAction.CallbackContext", "CallbackContext")
    if "<" in il2cpp_type and ">" in il2cpp_type:
        start, end = il2cpp_type.index("<"), il2cpp_type.rindex(">")
        for arg in il2cpp_type[start+1:end].split(","):
            if arg.strip().rstrip("[]") in GENERIC_TYPE_PARAMS:
                return None
    if il2cpp_type.startswith("Nullable`1") or "Nullable<" in il2cpp_type:
        return None
    
    # Handle backtick generics like Dictionary`2
    if "`" in il2cpp_type:
        parts = il2cpp_type.split("`")
        base_type = parts[0]
        try:
            num_params = int(parts[1].split("[")[0].split("<")[0])
            result = f"{base_type}<{', '.join(['object'] * num_params)}>"
            return result + "[]" if il2cpp_type.endswith("[]") else result
        except (ValueError, IndexError):
            return base_type
    
    # Handle array types - map base type first, then re-add []
    is_array = il2cpp_type.endswith("[]")
    base_type = il2cpp_type[:-2] if is_array else il2cpp_type
    
    # Map primitive types
    mapped = TYPE_MAP.get(base_type, base_type)
    
    # Apply deobfuscation mapping if available
    # This ensures type references use friendly names, not just class declarations
    friendly = get_friendly_name(mapped)
    if friendly:
        mapped = friendly
    
    # Re-add array suffix
    if is_array:
        mapped = mapped + "[]"
    
    # If we have namespace context, resolve ambiguous types dynamically
    if current_ns is not None and imported_ns is not None:
        mapped = resolve_ambiguous_type(mapped, current_ns, imported_ns)
    
    return mapped

# C# reserved keywords
CSHARP_KEYWORDS = {
    "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
    "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
    "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
    "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
    "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
    "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
    "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
    "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual",
    "void", "volatile", "while"
}

def has_valid_csharp_identifier_chars(name: str) -> bool:
    # DEBUG STATS: ~228,716 calls - SECOND highest usage (validates every identifier)
    """Check if name has valid C# chars. [STEP 4.x: Name validation]"""
    _track_call("has_valid_csharp_identifier_chars")
    return bool(name) and all(c.isascii() and (c.isalnum() or c == '_') for c in name)

def is_unicode_name(name: str) -> bool:
    # DEBUG STATS: ~177,254 calls - Unicode detection for obfuscated names
    """Check if name has non-ASCII Unicode chars. [STEP 4.x: Name validation]"""
    _track_call("is_unicode_name")
    if not name:
        return False
    return not has_valid_csharp_identifier_chars(name.replace("<", "").replace(">", "").replace(".", "").replace("|", ""))

def sanitize_unicode_namespace(ns: str) -> str:
    # DEBUG STATS: 0 calls (CONDITIONAL) - Only called when namespace has Unicode chars
    # NOT dead code - required for games with obfuscated namespace names
    """Sanitize namespace with Unicode chars. [STEP 4.x: Namespace sanitization]"""
    _track_call("sanitize_unicode_namespace")
    if not ns or ns == "Global":
        return ns
    counter = 0
    parts = []
    for part in ns.split("."):
        if is_unicode_name(part):
            counter += 1
            parts.append(f"unicode_ns_{counter}")
        else:
            parts.append(part)
    return ".".join(parts)

def sanitize_name(name: str, return_original: bool = False) -> str:
    # DEBUG STATS: ~45,115 calls - Converts names to valid C# identifiers
    """Sanitize method/param names to valid C# identifiers. [STEP 4.x: Name sanitization]"""
    _track_call("sanitize_name")
    if not name or name in (".ctor", ".cctor"):
        return (None, None) if return_original else None
    original = name
    if not has_valid_csharp_identifier_chars(name.replace("<", "").replace(">", "").replace(".", "").replace("|", "")):
        return (None, None) if return_original else None
    name = name.replace("<", "_").replace(">", "_").replace(".", "_").replace("|", "_")
    if name and name[0].isdigit():
        name = "_" + name
    if name in CSHARP_KEYWORDS:
        name = "@" + name
    return (name, original) if return_original else name

# Generic type parameters - common patterns
GENERIC_TYPE_PARAMS = {"T", "T1", "T2", "T3", "T4", "TKey", "TValue", "TResult", "TSource", "TElement",
                       "TControl", "TDevice", "TState", "TProcessor", "TObject", "U", "V", "TAttribute",
                       "TData", "TDescriptor", "TEnum"}
# Known T-prefixed real types (not generic params)
KNOWN_T_TYPES = {"TMPro", "Transform", "Texture", "Texture2D", "Texture3D", "TextMesh", "TextMeshPro",
                 "TextMeshProUGUI", "Tween", "Tweener", "TweenParams", "Thread", "Timer", "TimeSpan",
                 "Task", "Type", "Tuple", "Toggle", "Tile", "Tilemap", "Touch", "TrailRenderer", "Terrain", "Tree"}

def is_generic_type_param(type_name: str) -> bool:
    # DEBUG STATS: ~342,078 calls - THIRD highest usage (generic param detection)
    """Check if type is a generic param (T, TKey, etc.). [STEP 4.x: Type validation]"""
    _track_call("is_generic_type_param")
    base = type_name.rstrip("[]").rstrip("?")
    if base in GENERIC_TYPE_PARAMS or (len(base) == 1 and base.isupper()):
        return True
    # Generic params never contain underscores (e.g., TMP_FontAsset is a real type, not T)
    if "_" in base:
        return False
    return len(base) >= 2 and base[0] == 'T' and base[1].isupper() and base not in KNOWN_T_TYPES

def get_generic_type_params_from_method(method) -> set:
    # DEBUG STATS: ~82,826 calls - Extracts T, TKey, etc. from method signatures
    """Extract generic type params from method. [STEP 4.x: Method analysis]"""
    _track_call("get_generic_type_params_from_method")
    params = set()
    if method.return_type.rstrip("[]") in GENERIC_TYPE_PARAMS:
        params.add(method.return_type.rstrip("[]"))
    for p in method.parameters:
        if p.type.rstrip("[]") in GENERIC_TYPE_PARAMS:
            params.add(p.type.rstrip("[]"))
    return params

def has_generic_type_arg(type_str: str) -> bool:
    # DEBUG STATS: ~127,444 calls - Validates generic type arguments
    """Check if type has generic param args. [STEP 4.x: Type validation]"""
    _track_call("has_generic_type_arg")
    if "<" not in type_str:
        return False
    inner = type_str[type_str.index("<")+1:type_str.rindex(">")]
    return any(is_generic_type_param(arg.strip()) for arg in inner.split(","))

def is_backtick_generic(type_str: str) -> bool:
    # DEBUG STATS: ~568 calls - Detects IL2CPP generic notation like List`1
    """Check if type uses backtick generic notation. [STEP 4.x: Type validation]"""
    _track_call("is_backtick_generic")
    return "`" in type_str and "<" not in type_str

def _check_type_valid(type_str: str, current_ns: str, imported_ns: set, is_generic_method: bool) -> bool:
    # DEBUG STATS: ~128,406 calls - Combined validation for method/property types
    """Validate type for method/property generation. [STEP 4.x: Type validation]"""
    _track_call("_check_type_valid")
    if is_generic_type_param(type_str) or has_generic_type_arg(type_str):
        return False
    if is_generic_method and is_backtick_generic(type_str):
        return False
    if map_type(type_str) is None or "*" in type_str:
        return False
    base = type_str.split("<")[0].split("[")[0].split("`")[0]
    return not is_unicode_name(base) and is_type_resolvable(type_str, current_ns, imported_ns)

def is_valid_method(method: MethodDef, current_ns: str = None, imported_ns: set = None) -> bool:
    # DEBUG STATS: ~77,437 calls - Validates every method for generation
    """Check if method should be included. [STEP 4.x: Method validation]"""
    _track_call("is_valid_method")
    if method.name in (".ctor", ".cctor") or "|" in method.name:
        return False
    if "." in method.name:
        parts = method.name.split(".")
        if len(parts) > 1 and parts[0] and parts[0][0].isupper():
            return False
    is_generic = bool(get_generic_type_params_from_method(method))
    if not _check_type_valid(method.return_type, current_ns, imported_ns, is_generic):
        return False
    for p in method.parameters:
        if p.name == "__no_name__" or p.modifier in ("out", "ref", "in"):
            return False
        if not _check_type_valid(p.type, current_ns, imported_ns, is_generic):
            return False
    return True

def is_valid_property(prop: PropertyDef, current_ns: str = None, imported_ns: set = None) -> bool:
    # DEBUG STATS: ~2 calls - Property validation (most skipped earlier by type filter)
    """Check if property should be included. [STEP 4.x: Property validation]"""
    _track_call("is_valid_property")
    return _check_type_valid(prop.type, current_ns, imported_ns, False)

def compute_imported_namespaces(current_ns: str, valid_namespaces: set) -> set:
    # DEBUG STATS: ~441 calls - Resolves namespace imports per file
    """Compute imported namespaces for a file. [STEP 4.x: Namespace resolution]"""
    _track_call("compute_imported_namespaces")
    return set(ALWAYS_IMPORTED_NAMESPACES)

def _should_skip_type(t: TypeDef, seen: set, extra_skip: set = None) -> bool:
    # DEBUG STATS: ~5,996 calls - Filters types before generation
    """Common skip logic for enums/interfaces/structs. [STEP 4.x: Type filtering]"""
    _track_call("_should_skip_type")
    if t.visibility != "public": return True
    if "`" in t.name or "<" in t.name or ">" in t.name: return True
    if not has_valid_csharp_identifier_chars(t.name): return True
    if t.name in SKIP_TYPES: return True
    if extra_skip and t.name in extra_skip: return True
    return False

def _add_type_with_rename(t: TypeDef, ns: str, seen: set, body: list, kind: str, content_fn=None):
    # DEBUG STATS: ~3,608 calls - Handles type renaming for conflicts
    """Add type with rename handling and XML doc. [STEP 4.x: Type generation helper]"""
    _track_call("_add_type_with_rename")
    name, orig = get_renamed_class_name(t.name, ns)
    if name in seen: return False
    seen.add(name)
    if name != orig:
        # Check if this is a deobfuscation (friendly name) vs a simple rename
        if get_friendly_name(orig):
            body.append(f"    /// <summary>Deobfuscated {kind}. IL2CPP name: '{orig}'</summary>")
        else:
            body.append(f"    /// <summary>Renamed to avoid conflict. Original IL2CPP name: '{orig}'</summary>")
    return name, orig

def generate_wrapper_code_per_namespace(types: List[TypeDef], output_dir: str) -> dict:
    """Generate C# wrapper files per namespace. [STEP 4: Main code generation]"""
    _track_call("generate_wrapper_code_per_namespace")
    _debug(f"STEP 4: Generating wrapper code for {len(types)} types to {output_dir}")
    
    # Group types by namespace (using CONFIG for dynamic skip lists)
    namespaces = {}
    for t in types:
        ns = t.namespace if t.namespace else "Global"
        if CONFIG.should_skip_namespace(ns):
            continue
        if ns not in namespaces:
            namespaces[ns] = []
        namespaces[ns].append(t)

    # Determine which namespaces will actually produce output (have valid types)
    # This prevents adding using statements for empty namespaces
    valid_namespaces_for_using = set()
    for ns, ns_types in namespaces.items():
        # Check if this namespace has at least one valid type that will produce output
        for t in ns_types:
            if t.visibility not in ("public",):
                continue
            # Check for valid methods, properties, or if it's a valid enum/delegate
            has_content = False
            if t.kind == "enum" and t.fields:
                has_content = True
            elif t.base_type == "MulticastDelegate":
                has_content = True
            elif any(is_valid_method(m) for m in t.methods):
                has_content = True
            elif any(is_valid_property(p) for p in t.properties):
                has_content = True
            if has_content:
                valid_namespaces_for_using.add(ns)
                break

    generated_files = {}
    
    # Build namespace -> types map once for smart using generation
    all_types_list = types  # Keep reference for smart usings
    
    for ns, ns_types in namespaces.items():
        original_ns = ns  # Store original namespace for IL2CPP calls
        
        # Check if namespace has Unicode characters and sanitize
        is_unicode_ns = is_unicode_name(ns) if ns != "Global" else False
        if is_unicode_ns:
            ns = sanitize_unicode_namespace(ns)
        
        # Compute which namespaces will be imported for this file
        imported_ns = compute_imported_namespaces(ns, valid_namespaces_for_using)
        
        # PHASE 1: Generate the code body first (without using statements)
        body_lines = []
        
        # Add comment for unicode namespaces
        if is_unicode_ns:
            body_lines.append(f"// Original namespace: {original_ns}")
        
        body_lines.append(f"namespace {ns}")
        body_lines.append("{")

        type_count = 0

        # First, generate delegates (types that derive from MulticastDelegate)
        seen_delegate_names = set()
        for t in ns_types:
            if t.kind != "class" or t.visibility != "public" or t.base_type != "MulticastDelegate": continue
            if "`" in t.name or "<" in t.name or ">" in t.name: continue
            if not has_valid_csharp_identifier_chars(t.name) or t.name in SKIP_TYPES: continue
            delegate_name, original_delegate_name = get_renamed_class_name(t.name, ns)
            if delegate_name in seen_delegate_names: continue
            invoke_method = next((m for m in t.methods if m.name == "Invoke"), None)
            if not invoke_method: continue
            # Check for pointer or generic types in signature
            if "*" in invoke_method.return_type or any("*" in p.type for p in invoke_method.parameters): continue
            if is_generic_type_param(invoke_method.return_type) or any(is_generic_type_param(p.type) for p in invoke_method.parameters): continue
            # Check return type is valid and resolvable
            return_type = map_type(invoke_method.return_type, ns, imported_ns)
            if return_type is None or not is_type_resolvable(invoke_method.return_type, ns, imported_ns): continue
            # Check all parameter types are valid and resolvable
            if not all(map_type(p.type, ns, imported_ns) and is_type_resolvable(p.type, ns, imported_ns) for p in invoke_method.parameters): continue
            seen_delegate_names.add(delegate_name)
            type_count += 1
            # Qualify cross-namespace types
            return_type = get_qualified_type_name(return_type, ns, imported_ns)
            params_str = ", ".join(f"{get_qualified_type_name(map_type(p.type, ns, imported_ns), ns, imported_ns)} {sanitize_name(p.name) or p.name}" for p in invoke_method.parameters)
            if delegate_name != original_delegate_name:
                body_lines.append(f"    /// <summary>Renamed to avoid conflict. Original IL2CPP name: '{original_delegate_name}'</summary>")
            body_lines.append(f"    {t.visibility} delegate {return_type} {delegate_name}({params_str});")
            body_lines.append("")

        # Then, generate enums
        seen_type_names = set()
        for t in ns_types:
            if t.kind != "enum" or _should_skip_type(t, seen_type_names, SKIP_NESTED_ENUM_NAMES): continue
            result = _add_type_with_rename(t, ns, seen_type_names, body_lines, "enum")
            if not result: continue
            enum_name, _ = result
            type_count += 1
            body_lines.append(f"    {t.visibility} enum {enum_name}")
            body_lines.append("    {")
            # Output enum values (const fields)
            enum_values = [f for f in t.fields if f.is_const and f.type == t.name]
            valid_values = []
            for ev in enum_values:
                if ev.value is not None:
                    val = ev.value.split("//")[0].strip()
                    try:
                        int_val = int(val)
                        if int_val > 2147483647 or int_val < -2147483648: continue
                    except ValueError: pass
                    valid_values.append((ev.name, val))
                else:
                    valid_values.append((ev.name, None))
            
            for idx, (name, val) in enumerate(valid_values):
                comma = "," if idx < len(valid_values) - 1 else ""
                if val is not None:
                    body_lines.append(f"        {name} = {val}{comma}")
                else:
                    body_lines.append(f"        {name}{comma}")
            
            body_lines.append("    }")
            body_lines.append("")

        # Then, generate interfaces (as empty stubs so they can be referenced)
        for t in ns_types:
            if t.kind != "interface" or _should_skip_type(t, seen_type_names): continue
            result = _add_type_with_rename(t, ns, seen_type_names, body_lines, "interface")
            if not result: continue
            interface_name, _ = result
            type_count += 1
            body_lines.append(f"    {t.visibility} interface {interface_name}")
            body_lines.append("    {")
            body_lines.append("        // Stub interface")
            body_lines.append("    }")
            body_lines.append("")

        # Then, generate structs (as simple stub structs with fields)
        for t in ns_types:
            if t.kind != "struct" or _should_skip_type(t, seen_type_names): continue
            # Check if struct has any fields with generic type params - skip those
            pub_fields = [f for f in t.fields if f.visibility == "public" and not f.is_const]
            generic_fields = [f for f in pub_fields if is_generic_type_param(f.type)]
            if generic_fields: continue
            result = _add_type_with_rename(t, ns, seen_type_names, body_lines, "struct")
            if not result: continue
            struct_name, _ = result
            type_count += 1
            body_lines.append(f"    {t.visibility} struct {struct_name}")
            body_lines.append("    {")
            # Output public fields (skip const and invalid/unresolvable types)
            valid_field_count = 0
            for fld in [f for f in t.fields if f.visibility == "public" and not f.is_const]:
                field_type = map_type(fld.type, ns, imported_ns)
                if field_type is None or not is_type_resolvable(fld.type, ns, imported_ns): continue
                # Qualify cross-namespace types
                field_type = get_qualified_type_name(field_type, ns, imported_ns)
                valid_field_count += 1
                body_lines.append(f"        public {field_type} {fld.name};")
            if valid_field_count == 0:
                body_lines.append("        // Stub struct")
            body_lines.append("    }")
            body_lines.append("")

        # Then, generate classes
        unicode_class_counter = 0
        for t in ns_types:
            if t.kind != "class" or t.visibility != "public": continue
            if "`" in t.name or "<" in t.name or ">" in t.name: continue
            
            original_class_name = t.name
            is_unicode_class = is_unicode_name(t.name)
            is_deobfuscated = False  # Track if we're using a friendly name from mappings
            
            # Check for deobfuscation mapping first
            friendly_name = get_friendly_name(t.name)
            if friendly_name:
                class_name = friendly_name
                is_deobfuscated = True
            elif is_unicode_class:
                unicode_class_counter += 1
                class_name = f"unicode_class_{unicode_class_counter}"
            else:
                class_name, original_class_name = get_renamed_class_name(t.name, ns)

            # Skip types that derive from special .NET types or sealed types
            clean_base = t.base_type.rstrip(",").strip() if t.base_type else None
            if clean_base and clean_base in SKIP_BASE_TYPES: continue
            if clean_base and clean_base in SEALED_TYPES: continue  # CS0509: cannot inherit from sealed
            if t.name in SKIP_TYPES or class_name in seen_type_names: continue
            seen_type_names.add(class_name)
            type_count += 1

            # Clean up base type (handle generic syntax issues)
            base_type = t.base_type
            if base_type:
                if "`" in base_type:
                    base_type = None
                else:
                    base_type = base_type.rstrip(",").strip()
                    # Skip interface/problematic/obfuscated base types
                    skip_interfaces = base_type.startswith("I") and len(base_type) > 1 and base_type[1].isupper()
                    skip_builtin = base_type in {"IEnumerator", "IDisposable", "IComparer", "IEnumerable", "ICollection"}
                    if skip_interfaces or skip_builtin or is_obfuscated_type(base_type):
                        base_type = None
                    elif base_type and not is_type_resolvable(base_type, ns, imported_ns):
                        known_base_types = {"MonoBehaviour", "ScriptableObject", "Component", "Behaviour", "Object", "UnityEvent", "UnityEventBase", "Selectable", "UIBehaviour", "Graphic", "MaskableGraphic", "Image", "Text", "Texture", "Texture2D", "Material", "Mesh", "Sprite", "Camera", "Transform", "RectTransform", "Canvas", "CanvasGroup", "EventTrigger", "AudioSource", "AudioClip", "Animator", "Animation", "ParticleSystem", "Renderer", "Collider", "Rigidbody", "Rigidbody2D", "Il2CppObject"}
                        if base_type not in known_base_types:
                            base_type = None

            # Class declaration
            # Apply BASE_TYPE_DISAMBIGUATION for inheritance (not full AMBIGUOUS_TYPES)
            # This avoids issues like Bone -> UnityEngine.XR.Bone when FinalIK Bone is intended
            if base_type and base_type in BASE_TYPE_DISAMBIGUATION:
                base_type = BASE_TYPE_DISAMBIGUATION[base_type]
            
            # Also resolve base type via deobfuscation mappings
            if base_type:
                base_friendly = get_friendly_name(base_type)
                if base_friendly:
                    base_type = base_friendly
            
            # Fully qualify base type for cross-namespace inheritance
            if base_type:
                base_type = get_qualified_type_name(base_type, ns, imported_ns)
            
            base_part = f" : {base_type}" if base_type else " : Il2CppObject"
            
            # Track if class was renamed due to conflicts
            is_renamed_class = (class_name != original_class_name) and not is_unicode_class and not is_deobfuscated
            
            # Add XML doc for deobfuscated, unicode, or renamed classes
            if is_deobfuscated:
                body_lines.append(f"    /// <summary>Deobfuscated class. IL2CPP name: '{original_class_name}'</summary>")
            elif is_unicode_class:
                body_lines.append(f"    /// <summary>Obfuscated class. Original name: '{original_class_name}'</summary>")
            elif is_renamed_class:
                body_lines.append(f"    /// <summary>Renamed to avoid conflict. Original IL2CPP name: '{original_class_name}'</summary>")
            
            body_lines.append(f"    {t.visibility} partial class {class_name}{base_part}")
            body_lines.append("    {")

            # For deobfuscated, unicode, renamed classes, or unicode namespaces, store original name for IL2CPP lookups
            if is_deobfuscated or is_unicode_class or is_unicode_ns or is_renamed_class:
                body_lines.append(f"        /// <summary>Original IL2CPP class name for runtime lookups</summary>")
                body_lines.append(f"        private const string _il2cppClassName = \"{original_class_name}\";")
                body_lines.append(f"        private const string _il2cppNamespace = \"{original_ns}\";")
                body_lines.append("")

            # Constructor - always call base(nativePtr) since we inherit from Il2CppObject at minimum
            body_lines.append(f"        public {class_name}(IntPtr nativePtr) : base(nativePtr) {{ }}")
            body_lines.append("")

            # Field Generation - Generate property accessors for public AND protected instance fields
            # Skip fields with generic type parameters (T, TValue, etc.)
            # Include protected fields since IL2CPP modding often requires accessing them
            # Also include private fields if they have a deobfuscation mapping (explicitly identified as useful)
            def should_include_field(f):
                if f.is_const or is_generic_type_param(f.type):
                    return False
                if f.visibility in ("public", "protected"):
                    return True
                # Include private fields only if they have a deobfuscation mapping
                if f.visibility == "private":
                    field_mapping = get_friendly_name(f"{t.name}.{f.name}") or get_friendly_name(f.name)
                    return field_mapping is not None
                return False
            accessible_fields = [f for f in t.fields if should_include_field(f)]
            if accessible_fields:
                body_lines.append("        // Fields")
                unicode_field_counter = 0
                for fld in accessible_fields:
                    # Skip generic type params in field type
                    if is_generic_type_param(fld.type):
                        continue
                    field_type = map_type(fld.type, ns, imported_ns)
                    if field_type is None or not is_type_resolvable(fld.type, ns, imported_ns): 
                        continue
                    
                    # Get fully-qualified type name for cross-namespace references
                    field_type = get_qualified_type_name(field_type, ns, imported_ns)
                    
                    original_field_name = fld.name
                    is_unicode_field = is_unicode_name(fld.name)
                    is_deobfuscated_field = False
                    
                    # Check for deobfuscation mapping for this field
                    field_friendly = get_friendly_name(f"{t.name}.{fld.name}") or get_friendly_name(fld.name)
                    if field_friendly:
                        display_field_name = field_friendly
                        is_deobfuscated_field = True
                    elif is_unicode_field:
                        unicode_field_counter += 1
                        display_field_name = f"unicode_field_{unicode_field_counter}"
                    else:
                        display_field_name = sanitize_name(fld.name)
                        if not display_field_name: continue
                    
                    # Add doc comment for deobfuscated or unicode fields
                    if is_deobfuscated_field:
                        body_lines.append(f"        /// <summary>Deobfuscated field. IL2CPP name: '{original_field_name}'</summary>")
                    elif is_unicode_field:
                        body_lines.append(f"        /// <summary>Obfuscated field. Original name: '{original_field_name}'</summary>")
                    
                    # Generate property with getter and setter that use Il2CppRuntime.GetField/SetField
                    body_lines.append(f"        public {field_type} {display_field_name}")
                    body_lines.append("        {")
                    body_lines.append(f"            get => Il2CppRuntime.GetField<{field_type}>(this, \"{original_field_name}\");")
                    body_lines.append(f"            set => Il2CppRuntime.SetField<{field_type}>(this, \"{original_field_name}\", value);")
                    body_lines.append("        }")
                    body_lines.append("")
                body_lines.append("")

            # Build a set of method names to avoid property conflicts
            method_names = {m.name for m in t.methods}
            
            # Property Generation - Convert get_/set_ methods to properties
            property_methods = {}  # property_name -> {"get": method, "set": method, "type": type}
            methods_used_as_properties = set()
            
            for m in t.methods:
                if not is_valid_method(m, ns, imported_ns): continue
                if m.name.startswith("get_") and len(m.parameters) == 0 and m.return_type != "Void":
                    prop_name = m.name[4:]
                    property_methods.setdefault(prop_name, {"get": None, "set": None, "type": None})
                    property_methods[prop_name]["get"] = m
                    property_methods[prop_name]["type"] = m.return_type
                    methods_used_as_properties.add(m.name)
                elif m.name.startswith("set_") and len(m.parameters) == 1 and m.return_type == "Void":
                    prop_name = m.name[4:]
                    property_methods.setdefault(prop_name, {"get": None, "set": None, "type": None})
                    property_methods[prop_name]["set"] = m
                    if property_methods[prop_name]["type"] is None:
                        property_methods[prop_name]["type"] = m.parameters[0].type
                    methods_used_as_properties.add(m.name)
            
            # Generate properties from consolidated get_/set_ methods
            unicode_property_counter = 0
            if property_methods:
                body_lines.append("        // Properties")
                for prop_name, prop_info in sorted(property_methods.items()):
                    original_prop_name = prop_name
                    is_unicode_prop = is_unicode_name(prop_name)
                    is_deobfuscated_prop = False
                    
                    # Check for deobfuscation mapping for this property (field-level mapping)
                    # Format: "ClassName.PropertyName" or just "PropertyName" 
                    prop_friendly = get_friendly_name(f"{t.name}.{prop_name}") or get_friendly_name(prop_name)
                    if prop_friendly:
                        display_prop_name = prop_friendly
                        is_deobfuscated_prop = True
                    elif is_unicode_prop:
                        unicode_property_counter += 1
                        display_prop_name = f"unicode_property_{unicode_property_counter}"
                    else:
                        display_prop_name = prop_name
                    
                    # Skip properties that conflict with System types
                    if prop_name in SKIP_PROPERTY_NAMES:
                        if prop_info["get"]: methods_used_as_properties.discard(f"get_{prop_name}")
                        if prop_info["set"]: methods_used_as_properties.discard(f"set_{prop_name}")
                        continue
                    
                    prop_type = map_type(prop_info["type"], ns, imported_ns)
                    if prop_type is None: continue
                    # Qualify cross-namespace types
                    prop_type = get_qualified_type_name(prop_type, ns, imported_ns)
                    
                    # Skip properties with types that aren't resolvable
                    if not is_type_resolvable(prop_info["type"], ns, imported_ns):
                        if prop_info["get"]: methods_used_as_properties.discard(f"get_{prop_name}")
                        if prop_info["set"]: methods_used_as_properties.discard(f"set_{prop_name}")
                        continue
                    
                    # Determine visibility
                    visibility = prop_info["get"].visibility if prop_info["get"] else (prop_info["set"].visibility if prop_info["set"] else "public")
                    is_static = (prop_info["get"] and prop_info["get"].is_static) or (prop_info["set"] and prop_info["set"].is_static)
                    static_keyword = "static " if is_static else ""
                    getter_rva = prop_info["get"].rva if prop_info["get"] else None
                    setter_rva = prop_info["set"].rva if prop_info["set"] else None
                    
                    # Add doc comment for deobfuscated or unicode properties
                    if is_deobfuscated_prop:
                        body_lines.append(f"        /// <summary>Deobfuscated property. IL2CPP name: '{original_prop_name}'</summary>")
                    elif is_unicode_prop:
                        body_lines.append(f"        /// <summary>Obfuscated property. Original name: '{original_prop_name}'</summary>")
                    
                    body_lines.append(f"        {visibility} {static_keyword}{prop_type} {display_prop_name}")
                    body_lines.append("        {")
                    if prop_info["get"]:
                        if is_unicode_prop and getter_rva:
                            meth = f"CallStaticByRva<{prop_type}>({getter_rva}" if is_static else f"CallByRva<{prop_type}>(this, {getter_rva}"
                        else:
                            meth = f'CallStatic<{prop_type}>("{original_ns}", "{original_class_name}", "get_{original_prop_name}"' if is_static else f'Call<{prop_type}>(this, "get_{original_prop_name}"'
                        body_lines.append(f"            get => Il2CppRuntime.{meth}, global::System.Type.EmptyTypes);")
                    if prop_info["set"]:
                        type_arr = f"new[] {{ typeof({prop_type}) }}"
                        if is_unicode_prop and setter_rva:
                            meth = f"InvokeStaticVoidByRva({setter_rva}" if is_static else f"InvokeVoidByRva(this, {setter_rva}"
                        else:
                            meth = f'InvokeStaticVoid("{original_ns}", "{original_class_name}", "set_{original_prop_name}"' if is_static else f'InvokeVoid(this, "set_{original_prop_name}"'
                        body_lines.append(f"            set => Il2CppRuntime.{meth}, {type_arr}, value);")
                    body_lines.append("        }")
                    body_lines.append("")
                body_lines.append("")

            # Methods - deduplicate by signature and exclude methods used as properties
            valid_methods = [m for m in t.methods if m.visibility == "public" and is_valid_method(m, ns, imported_ns) and m.name not in methods_used_as_properties]
            seen_signatures, deduped_methods = set(), []
            for method in valid_methods:
                if is_unicode_name(method.name):
                    if not method.rva: continue
                    method_name = f"__unicode_method_rva_{method.rva}__"
                else:
                    method_name = sanitize_name(method.name)
                    if not method_name: continue
                sig_key = (method_name, tuple('object' if is_generic_type_param(p.type) else map_type(p.type, ns, imported_ns) for p in method.parameters))
                if sig_key not in seen_signatures:
                    seen_signatures.add(sig_key)
                    deduped_methods.append(method)
            
            if deduped_methods:
                body_lines.append("        // Methods")
                unicode_method_counter = 0
                for method in deduped_methods:
                    is_unicode_method = is_unicode_name(method.name)
                    is_deobfuscated_method = False
                    original_method_name = method.name
                    
                    # Check for deobfuscation mapping for this method
                    method_friendly = get_friendly_name(f"{t.name}.{method.name}") or get_friendly_name(method.name)
                    if method_friendly:
                        method_name = method_friendly
                        is_deobfuscated_method = True
                        use_rva = False
                    elif is_unicode_method:
                        if not method.rva: continue
                        unicode_method_counter += 1
                        method_name, use_rva = f"unicode_method_{unicode_method_counter}", True
                    else:
                        method_name = sanitize_name(method.name)
                        if not method_name: continue
                        use_rva = False

                    # Check if this is a generic method
                    generic_params = get_generic_type_params_from_method(method)
                    is_generic = len(generic_params) > 0
                    type_params_clause = f"<{', '.join(sorted(generic_params))}>" if is_generic else ""
                    
                    # Map return type (use generic param name for generic types)
                    return_type = method.return_type if is_generic_type_param(method.return_type) else map_type(method.return_type, ns, imported_ns)
                    # Qualify cross-namespace types for return type
                    if not is_generic_type_param(method.return_type):
                        return_type = get_qualified_type_name(return_type, ns, imported_ns)

                    # Build parameter list and names
                    param_parts, param_names_list = [], []
                    for idx, p in enumerate(method.parameters):
                        mod_prefix = f"{p.modifier} " if p.modifier else ""
                        ptype = p.type if is_generic_type_param(p.type) else map_type(p.type, ns, imported_ns)
                        # Qualify cross-namespace types for parameters
                        if not is_generic_type_param(p.type):
                            ptype = get_qualified_type_name(ptype, ns, imported_ns)
                        pname = sanitize_name(p.name) or f"arg{idx}"
                        param_parts.append(f"{mod_prefix}{ptype} {pname}")
                        param_names_list.append(pname)
                    param_list = ", ".join(param_parts)
                    param_names = ", ".join(param_names_list)

                    # Build paramTypes array - also need qualified names for typeof()
                    if method.parameters:
                        param_types_items = []
                        for p in method.parameters:
                            if is_generic_type_param(p.type):
                                param_types_items.append(f'typeof({p.type.rstrip("[]")})')
                            else:
                                ptype_mapped = map_type(p.type, ns, imported_ns)
                                ptype_qualified = get_qualified_type_name(ptype_mapped, ns, imported_ns)
                                param_types_items.append(f'typeof({ptype_qualified})')
                        param_types_decl = f"new Type[] {{ {', '.join(param_types_items)} }}"
                    else:
                        param_types_decl = "global::System.Type.EmptyTypes"

                    static_keyword = "static " if method.is_static else ""
                    
                    # Add comment for deobfuscated or Unicode methods showing original name
                    if is_deobfuscated_method:
                        body_lines.append(f"        /// <summary>Deobfuscated method. IL2CPP name: '{original_method_name}'</summary>")
                    elif use_rva:
                        body_lines.append(f"        /// <summary>Obfuscated method. Original name: {repr(method.name)}, RVA: {method.rva}</summary>")
                    
                    body_lines.append(f"        {method.visibility} {static_keyword}{return_type} {method_name}{type_params_clause}({param_list})")
                    body_lines.append("        {")

                    # For generic methods, generate a stub with NotImplementedException for now
                    # because IL2CPP requires concrete type instantiation at compile time
                    if is_generic:
                        body_lines.append(f"            // TODO: Generic method - IL2CPP requires specific type instantiation")
                        body_lines.append(f"            throw new System.NotImplementedException(\"Generic method {method_name}{type_params_clause} requires IL2CPP generic instantiation\");")
                    elif use_rva:
                        rva_value = method.rva
                        is_void, is_stat = return_type == "void", method.is_static
                        args_suffix = f", {param_names}" if param_names else ""
                        if is_void:
                            meth = "InvokeStaticVoidByRva" if is_stat else "InvokeVoidByRva"
                            prefix = f'{rva_value}' if is_stat else f'this, {rva_value}'
                            body_lines.append(f"            Il2CppRuntime.{meth}({prefix}, {param_types_decl}{args_suffix});")
                        else:
                            meth = f"CallStaticByRva<{return_type}>" if is_stat else f"CallByRva<{return_type}>"
                            prefix = f'{rva_value}' if is_stat else f'this, {rva_value}'
                            body_lines.append(f"            return Il2CppRuntime.{meth}({prefix}, {param_types_decl}{args_suffix});")
                    else:
                        is_void, is_stat = return_type == "void", method.is_static
                        args_suffix = f", {param_names}" if param_names else ""
                        if is_void:
                            if is_stat:
                                body_lines.append(f'            Il2CppRuntime.InvokeStaticVoid("{original_ns}", "{original_class_name}", "{original_method_name}", {param_types_decl}{args_suffix});')
                            else:
                                body_lines.append(f'            Il2CppRuntime.InvokeVoid(this, "{original_method_name}", {param_types_decl}{args_suffix});')
                        else:
                            if is_stat:
                                body_lines.append(f'            return Il2CppRuntime.CallStatic<{return_type}>("{original_ns}", "{original_class_name}", "{original_method_name}", {param_types_decl}{args_suffix});')
                            else:
                                body_lines.append(f'            return Il2CppRuntime.Call<{return_type}>(this, "{original_method_name}", {param_types_decl}{args_suffix});')
                    body_lines.append("        }")
                    body_lines.append("")

            body_lines.append("    }")
            body_lines.append("")

        body_lines.append("}")
        body_lines.append("")

        # Only write file if we have types
        if type_count > 0:
            using_lines = generate_smart_usings(body_lines, ns, all_types_list, valid_namespaces_for_using)
            output_lines = ["// Auto-generated Il2Cpp wrapper classes", f"// Namespace: {ns}", "// Do not edit manually", ""]
            output_lines.extend(using_lines)
            output_lines.extend(body_lines)
            # Use configurable file prefix
            filepath = os.path.join(output_dir, f"{CONFIG.file_prefix}.{ns.replace('.', '_')}.cs")
            with open(filepath, "w", encoding="utf-8") as f:
                f.write("\n".join(output_lines))
            generated_files[ns] = filepath

    return generated_files


# ---------- Build functionality ----------

def find_csc_compiler():
    """Find C# compiler. [STEP 5.x: Build preparation]"""
    _track_call("find_csc_compiler")
    import glob
    for pattern in [r"C:\Program Files\Microsoft Visual Studio\2022\*\MSBuild\Current\Bin\Roslyn\csc.exe",
                    r"C:\Program Files (x86)\Microsoft Visual Studio\2022\*\MSBuild\Current\Bin\Roslyn\csc.exe",
                    r"C:\Program Files\Microsoft Visual Studio\2019\*\MSBuild\Current\Bin\Roslyn\csc.exe",
                    r"C:\Program Files (x86)\Microsoft Visual Studio\2019\*\MSBuild\Current\Bin\Roslyn\csc.exe"]:
        matches = glob.glob(pattern)
        if matches:
            return matches[0]
    for path in [r"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
                 r"C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"]:
        if os.path.exists(path):
            return path
    return None

def check_dotnet_available():
    """Check if dotnet CLI is available. [STEP 5.x: Build preparation]"""
    _track_call("check_dotnet_available")
    import subprocess
    try:
        return subprocess.run(["dotnet", "--version"], capture_output=True, text=True).returncode == 0
    except:
        return False

def get_framework_references():
    # DEBUG STATS: 0 calls (FALLBACK) - Only used by build_with_csc when dotnet unavailable
    # NOT dead code - required for systems without dotnet CLI installed
    """Get .NET Framework reference assemblies. [STEP 5.x: Build preparation]"""
    _track_call("get_framework_references")
    ref_path = r"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2"
    if not os.path.exists(ref_path):
        for alt in [r"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8",
                    r"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.1",
                    r"C:\Windows\Microsoft.NET\Framework64\v4.0.30319"]:
            if os.path.exists(alt):
                ref_path = alt
                break
    ref_args = []
    for ref in ["mscorlib.dll", "System.dll", "System.Core.dll", "System.Runtime.dll"]:
        full_path = os.path.join(ref_path, ref)
        ref_args.append(f'/reference:"{full_path}"' if os.path.exists(full_path) else f'/reference:{ref}')
    return ref_args


def build_project(dump_dir, log_file_path):
    """Build MDB_Core project. [STEP 5: Compilation]"""
    _track_call("build_project")
    _debug(f"STEP 5: Building project in {dump_dir}")
    import subprocess, datetime
    core_dir = os.path.join(dump_dir, "MDB_Core")
    output_dir = os.path.join(dump_dir, "..", "Managed")
    output_dll = os.path.join(output_dir, "GameSDK.ModHost.dll")
    os.makedirs(output_dir, exist_ok=True)
    
    log = [f"MDB Framework Build Log\n=======================\nBuild started: {datetime.datetime.now():%Y-%m-%d %H:%M:%S}\n"]
    
    # Collect source files
    source_files = []
    for subdir in ["Core", "ModHost", "Generated"]:
        d = os.path.join(core_dir, subdir)
        if os.path.exists(d):
            source_files.extend(os.path.join(d, f) for f in os.listdir(d) if f.endswith(".cs"))
    log.append(f"Source files: {len(source_files)}\n")
    
    if not source_files:
        log.append("ERROR: No source files found.")
        print("[!] No source files found.")
        open(log_file_path, "w").write("\n".join(log))
        return False
    
    use_dotnet = check_dotnet_available()
    csc_path = find_csc_compiler()
    
    if use_dotnet:
        log.append("Build method: dotnet CLI\n")
        success = build_with_dotnet(core_dir, log)
    elif csc_path:
        log.append(f"Build method: {'Roslyn' if 'Roslyn' in csc_path else 'Legacy'} csc.exe\nCompiler: {csc_path}\n")
        success = build_with_csc(csc_path, source_files, output_dll, core_dir, log, "Roslyn" in csc_path)
    else:
        log.append("ERROR: No C# compiler found.")
        print("[!] No C# compiler found.")
        success = False
    
    log.append(f"\nBuild finished: {datetime.datetime.now():%Y-%m-%d %H:%M:%S}")
    open(log_file_path, "w").write("\n".join(log))
    return success

def build_with_dotnet(core_dir, log):
    """Build using dotnet CLI. [STEP 5.1: dotnet build]"""
    _track_call("build_with_dotnet")
    _debug("STEP 5.1: Building with dotnet CLI")
    import subprocess
    csproj = os.path.join(core_dir, "GameSDK.ModHost.csproj")
    if not os.path.exists(csproj):
        log.append(f"ERROR: Project file not found: {csproj}")
        return False
    log.extend([f"Project: {csproj}", "-" * 50])
    try:
        r = subprocess.run(["dotnet", "build", csproj, "-c", "Release", "-v", "minimal"],
                          capture_output=True, text=True, cwd=core_dir)
        log.extend([r.stdout or "", r.stderr or "", "-" * 50])
        if r.returncode == 0:
            log.append("BUILD SUCCEEDED")
            print("[+] Build succeeded")
            return True
        log.append(f"BUILD FAILED (exit code: {r.returncode})")
        print("[!] Build failed. Check build_log.txt for details.")
        return False
    except Exception as e:
        log.append(f"ERROR: {e}")
        return False

def build_with_csc(csc_path, source_files, output_dll, core_dir, log, use_modern_syntax=True):
    # DEBUG STATS: 0 calls (FALLBACK) - Only used when dotnet CLI is unavailable
    # NOT dead code - required for systems with only csc.exe (Visual Studio/Windows SDK)
    """Build using csc.exe directly. [STEP 5.1: csc build]"""
    _track_call("build_with_csc")
    _debug(f"STEP 5.1: Building with csc.exe: {csc_path}")
    import subprocess
    args = [f'"{csc_path}"', "/target:library", "/optimize+",
            "/nowarn:CS0108,CS0114,CS0162,CS0168,CS0169,CS0219,CS0414,CS0649,CS0693,CS1030",
            f'/out:"{output_dll}"', "/nologo"]
    if use_modern_syntax:
        args.append("/langversion:latest")
    args.extend(get_framework_references())
    rsp = os.path.join(core_dir, "build.rsp")
    open(rsp, "w").write("\n".join(f'"{s}"' for s in source_files))
    args.append(f'@"{rsp}"')
    log.extend([f"Output: {output_dll}", "-" * 50])
    try:
        r = subprocess.run(" ".join(args), shell=True, capture_output=True, text=True, cwd=core_dir)
        log.extend([r.stdout or "", r.stderr or "", "-" * 50])
        success = r.returncode == 0
        log.append("BUILD SUCCEEDED" if success else f"BUILD FAILED (exit code: {r.returncode})")
        print(f"[+] Build succeeded" if success else "[!] Build failed. Check build_log.txt for details.")
    except Exception as e:
        log.append(f"ERROR: {e}")
        success = False
    if os.path.exists(rsp):
        os.remove(rsp)
    return success


def main():
    """Main entry point. [STEP 1: Program start]"""
    _track_call("main")
    _debug("STEP 1: Starting wrapper generator")
    import sys
    import argparse
    
    parser = argparse.ArgumentParser(
        description="IL2CPP Dump Wrapper Generator - Generate C# wrappers from IL2CPP dump.cs files",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  python wrapper_generator.py                    # Use dump.cs in current directory
  python wrapper_generator.py path/to/dump.cs    # Use specific dump file
  python wrapper_generator.py -c config.json     # Use specific config file
  python wrapper_generator.py --no-build         # Generate wrappers without building
  python wrapper_generator.py --list-detected    # Show auto-detected third-party libs
        """
    )
    parser.add_argument("dump", nargs="?", help="Path to dump.cs file (default: ./dump.cs)")
    parser.add_argument("-c", "--config", help="Path to generator_config.json")
    parser.add_argument("-m", "--mappings", help="Path to mappings.json for deobfuscation")
    parser.add_argument("-o", "--output", help="Output directory for generated files")
    parser.add_argument("--no-build", action="store_true", help="Skip building after generation")
    parser.add_argument("--list-detected", action="store_true", help="List auto-detected third-party namespaces and exit")
    
    args = parser.parse_args()
    
    # Find dump.cs
    if args.dump:
        dump_path = os.path.abspath(args.dump)
        dump_dir = os.path.dirname(dump_path)
    else:
        cwd, script_dir = os.getcwd(), os.path.dirname(os.path.abspath(__file__))
        dump_path = None
        for d in [cwd, script_dir]:
            p = os.path.join(d, "dump.cs")
            if os.path.exists(p):
                dump_dir, dump_path = d, p
                break
        if not dump_path:
            print(f"[ERROR] Could not find dump.cs in:\n  - {cwd}\n  - {script_dir}")
            print("Use: python wrapper_generator.py path/to/dump.cs")
            return
    
    if not os.path.exists(dump_path):
        print(f"[ERROR] Could not find: {dump_path}")
        return
    
    # Load generator configuration (game-specific settings)
    config_path = args.config or os.path.join(dump_dir, "generator_config.json")
    CONFIG.load(config_path)
    
    # Override output directory if specified
    if args.output:
        output_dir = os.path.abspath(args.output)
    else:
        output_dir = os.path.join(dump_dir, CONFIG.output_directory)
    
    log_file = os.path.join(dump_dir, "build_log.txt")
    os.makedirs(output_dir, exist_ok=True)

    # Load deobfuscation mappings if available
    mappings_path = args.mappings or os.path.join(dump_dir, "mappings.json")
    load_deobfuscation_mappings(mappings_path)

    print(f"[+] Parsing: {dump_path}")
    types = parse_dump_file(dump_path)
    print(f"[+] Parsed {len(types)} types")
    build_type_registry(types)
    
    # List detected third-party namespaces and exit if requested
    if args.list_detected:
        print("\n[+] Auto-detected third-party namespaces:")
        for ns in sorted(CONFIG.detected_third_party):
            print(f"    - {ns}")
        print(f"\nTotal: {len(CONFIG.detected_third_party)} namespaces")
        return
    
    valid_types = [t for t in types if t.name and (t.methods or t.properties or t.fields)]
    print(f"[+] Valid types: {len(valid_types)}")
    print("[+] Generating wrapper code...")
    generated = generate_wrapper_code_per_namespace(valid_types, output_dir)
    print(f"[+] Generated {len(generated)} namespace files")
    print(f"[+] Output directory: {output_dir}")
    
    if not args.no_build:
        print("[+] Building GameSDK.ModHost.dll...")
        build_project(dump_dir, log_file)


if __name__ == "__main__":
    main()
    # Print function usage report if debug is enabled
    if DEBUG:
        print_function_usage_report()
