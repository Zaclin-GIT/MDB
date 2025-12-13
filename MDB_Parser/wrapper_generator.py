#!/usr/bin/env python3
import os
import re
from dataclasses import dataclass, field
from typing import List, Optional, Dict, Set, Tuple


# ---------- Global type registry (built during parsing) ----------
# Maps type name -> list of namespaces where this type exists (public types only)
TYPE_REGISTRY: Dict[str, List[str]] = {}

# Maps base type name -> list of namespaces where GENERIC versions exist
# e.g., "UniTask" -> ["Cysharp.Threading.Tasks"] means UniTask<T> exists there
GENERIC_TYPE_REGISTRY: Dict[str, List[str]] = {}

# Set of (type_name, namespace) tuples for types that will actually be generated
# This excludes empty classes that pass visibility checks but have no content
GENERATED_TYPES: Set[Tuple[str, str]] = set()


# ---------- Skip Lists (Global Constants) ----------

# Special .NET base types that cannot be inherited from
# Classes with these base types will be skipped, and classes inheriting FROM skipped classes
# will fall back to Il2CppObject as their base
SKIP_BASE_TYPES = {
    # .NET core types with no IntPtr constructor
    "MulticastDelegate", "Delegate", "Enum", "ValueType", "Array",
    "Attribute", "Exception", "ApplicationException", "SystemException",
    "EventArgs", "ArrayList", "Hashtable", "Dictionary",
    "SynchronizationContext",
    # Abstract classes that require implementing members
    "PropertyDescriptor", "MemberDescriptor", "TypeConverter", "SerializationBinder",
    # Stream base class (abstract, requires implementing members)
    "Stream", "MemoryStream",
    # Unity attribute base classes (derived from Attribute)
    "PropertyAttribute", "PreserveAttribute", "UnityException",
    # Ambiguous base types that conflict between namespaces
    "InputDevice", "Pointer",  # InputSystem vs XR conflict
    "Toggle",  # UI vs UIElements conflict
    # Unity InputSystem sensor types (inherit from InputDevice)
    "Sensor",  # All sensor types like Accelerometer, Gyroscope inherit from Sensor
    # UIElements nested types (inner types in Global namespace)
    "UxmlTraits", "UxmlFactory",
    # JSON serialization types
    "JsonContainerAttribute", "JsonException",
    # TMPro types
    "MaterialReference",
    # Regex types that may be from System.Text
    "Match", "Capture", "Group",
    # Unity SSL/TLS callbacks (native types)
    "unitytls_tlsctx_read_callback", "unitytls_tlsctx_write_callback",
    # Types that may be internal/nested
    "CaptureResultType",
    # Addressables/AssetBundle types
    "AssetReferenceUIRestriction",
    # Sealed types that cannot be base classes
    "Space",  # OpenXR sealed type
}

# Skip enums that are commonly nested types (generic names that cause conflicts)
# These are likely nested enums like ParentClass.Type, ParentClass.Direction, etc.
# They exist as public enums but are ambiguous or commonly conflict with other types
SKIP_NESTED_ENUM_NAMES = {
    "Type", "Direction", "Mode", "Status", "Button", "Flags", "Axis", "Sign", "Unit",
    "State", "Kind", "Options", "Result", "Action", "Event", "Value", "Index",
    "UpdateMode", "CaptureResultType", "ContentType", "InputType", "CharacterValidation",
    "LineType", "WorldUpType", "FpsCounterAnchorPositions"
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
    properties: List[PropertyDef] = field(default_factory=list)
    methods: List[MethodDef] = field(default_factory=list)
    fields: List['FieldDef'] = field(default_factory=list)


# ---------- Regexes tuned for IL2CPP dump.cs ----------

DLL_RE = re.compile(r'^//\s*Dll\s*:\s*(.+)$')
NS_RE = re.compile(r'^//\s*Namespace:\s*(.*)$')

CLASS_HEADER_RE = re.compile(
    r'^(public|internal|private)\s+'
    r'(?:sealed\s+|abstract\s+|static\s+)*'
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
FIELD_RE = re.compile(
    r'^\s*(public|internal|private)\s+'
    r'(const\s+)?'
    r'([\w\.`\[\]]+)\s+'
    r'(\w+)\s*'
    r'(?:=\s*([^;]+))?;'
)


def count_char(s: str, ch: str) -> int:
    return s.count(ch)


# ---------- Core parser ----------

def parse_dump_file(path: str) -> List[TypeDef]:
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
    visibility = header_match.group(1)
    kind = header_match.group(2)
    name = header_match.group(3)
    base_type = header_match.group(4) if header_match.group(4) else None

    type_def = TypeDef(
        dll=dll,
        namespace=ns,
        kind=kind,
        name=name,
        base_type=base_type,
        visibility=visibility,
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
    """Build a global registry of type names -> namespaces for using statement resolution.
    Only includes PUBLIC types since internal/private types can't be referenced from other namespaces.
    Also builds GENERATED_TYPES set for types that will actually have generated code.
    """
    global TYPE_REGISTRY, GENERIC_TYPE_REGISTRY, GENERATED_TYPES
    TYPE_REGISTRY.clear()
    GENERIC_TYPE_REGISTRY.clear()
    GENERATED_TYPES.clear()
    
    for t in types:
        if not t.name:
            continue
        # Skip non-public types - they can't be referenced from other namespaces
        if t.visibility != "public":
            continue
        # Skip Unicode types (non-ASCII characters like Malayalam script)
        # Note: We only skip true Unicode types, not uppercase-only obfuscated names (e.g., DCBCCBKEIHN)
        # because those are still generated and should be usable as parameter/return types
        if is_unicode_name(t.name.split("`")[0].split("<")[0]):
            continue
            
        ns = t.namespace if t.namespace else "Global"
        
        # Check if this type will actually be generated (has content or is enum)
        # Exclude delegates (MulticastDelegate) - they get validated during actual generation
        # since we can't easily validate their parameter types at this stage
        # Also exclude types in SKIP_TYPES as they won't be generated
        has_content = False
        if t.name in SKIP_TYPES:
            # Skip types that we explicitly exclude
            has_content = False
        elif t.base_type == "MulticastDelegate":
            # Skip delegates - they'll be validated during generation
            has_content = False
        elif t.kind == "enum" and t.fields:
            # Skip nested enum names that cause conflicts
            if t.name in SKIP_NESTED_ENUM_NAMES:
                has_content = False
            else:
                has_content = True
        elif t.methods or t.properties or t.fields:
            # Check if this type's base type is in SKIP_BASE_TYPES
            # If so, the type won't actually be generated
            clean_base = t.base_type.rstrip(",").strip() if t.base_type else None
            if clean_base and clean_base in SKIP_BASE_TYPES:
                has_content = False
            else:
                has_content = True
        
        if has_content:
            GENERATED_TYPES.add((t.name, ns))
        
        # Check if this is a generic type
        if "`" in t.name or "<" in t.name:
            # Extract base name (e.g., "UniTask`1" -> "UniTask")
            base_name = t.name.split("`")[0].split("<")[0]
            if base_name not in GENERIC_TYPE_REGISTRY:
                GENERIC_TYPE_REGISTRY[base_name] = []
            if ns not in GENERIC_TYPE_REGISTRY[base_name]:
                GENERIC_TYPE_REGISTRY[base_name].append(ns)
        else:
            # Non-generic type
            if t.name not in TYPE_REGISTRY:
                TYPE_REGISTRY[t.name] = []
            if ns not in TYPE_REGISTRY[t.name]:
                TYPE_REGISTRY[t.name].append(ns)
    
    print(f"[+] Built type registry with {len(TYPE_REGISTRY)} non-generic and {len(GENERIC_TYPE_REGISTRY)} generic type names")
    print(f"[+] Types with content (will be generated): {len(GENERATED_TYPES)}")


def find_type_namespace(type_name: str) -> Optional[str]:
    """Find the namespace(s) where a type is defined. Returns None if not found."""
    # Get base type name (strip generics, arrays, etc.)
    base_name = type_name.split("<")[0].split("[")[0].split("`")[0]
    
    if base_name in TYPE_REGISTRY:
        namespaces = TYPE_REGISTRY[base_name]
        if len(namespaces) == 1:
            return namespaces[0]
        # If multiple namespaces, we can't auto-resolve - return None
        return None
    return None


# ---------- Smart Using Statement Generation ----------

def extract_type_references_from_code(code_lines: List[str]) -> set:
    """Extract all type names referenced in the generated code."""
    import re
    type_refs = set()
    
    # Patterns to match type references
    # Match types in: ClassName, Type[], typeof(Type), new Type[], List<Type>, etc.
    type_pattern = re.compile(r'\b([A-Z][A-Za-z0-9_]+)\b')
    
    # Keywords and built-in types to ignore
    ignore_types = {
        # C# keywords
        "public", "private", "protected", "internal", "static", "virtual", "override",
        "abstract", "sealed", "partial", "class", "struct", "enum", "interface", 
        "namespace", "using", "new", "return", "get", "set", "void", "null",
        "true", "false", "this", "base", "if", "else", "for", "foreach", "while",
        "throw", "try", "catch", "finally", "typeof", "sizeof", "default",
        # Built-in types
        "bool", "byte", "sbyte", "char", "decimal", "double", "float", "int", "uint",
        "long", "ulong", "short", "ushort", "object", "string", "IntPtr", "UIntPtr",
        # Common .NET types we always have
        "Type", "Array", "String", "Object", "Int32", "Boolean", "Exception",
        "List", "Dictionary", "Action", "Func", "Task", "Void",
        # GameSDK types always available
        "Il2CppObject", "Il2CppRuntime", "System", "Collections", "Generic",
        # Comment markers
        "Auto", "Il2Cpp", "Properties", "Methods", "Stub", "TODO", "NOTE",
        "RVA", "VA", "Namespace", "Do", "NOT", "Generic", "IL2CPP", "EmptyTypes",
    }
    
    for line in code_lines:
        # Skip comment lines
        if line.strip().startswith("//"):
            continue
        # Skip using statements (we'll handle those separately)
        if line.strip().startswith("using "):
            continue
            
        matches = type_pattern.findall(line)
        for match in matches:
            if match not in ignore_types and len(match) > 1:
                type_refs.add(match)
    
    return type_refs


def get_types_in_namespace(ns: str, all_types: List[TypeDef]) -> set:
    """Get all type names defined in a specific namespace."""
    type_names = set()
    for t in all_types:
        type_ns = t.namespace if t.namespace else "Global"
        if type_ns == ns and t.name:
            type_names.add(t.name)
    return type_names


def build_namespace_type_map(all_types: List[TypeDef]) -> dict:
    """Build a map of namespace -> set of type names defined in that namespace."""
    ns_types = {}
    for t in all_types:
        ns = t.namespace if t.namespace else "Global"
        if ns not in ns_types:
            ns_types[ns] = set()
        if t.name and not is_obfuscated_type(t.name):
            ns_types[ns].add(t.name)
    return ns_types


def find_required_namespaces(type_refs: set, current_ns: str, ns_type_map: dict, 
                              core_namespaces: set) -> set:
    """
    Find which namespaces are required to resolve the type references.
    Returns set of namespace names that contain at least one referenced type.
    """
    required = set()
    
    for type_name in type_refs:
        # Check each namespace for this type
        for ns, types_in_ns in ns_type_map.items():
            if type_name in types_in_ns and ns != current_ns:
                required.add(ns)
    
    return required


def detect_ambiguous_types(type_refs: set, candidate_namespaces: set, 
                           ns_type_map: dict, current_ns: str) -> dict:
    """
    Detect which types would be ambiguous if all candidate namespaces were imported.
    Returns dict of type_name -> list of namespaces that define it.
    """
    ambiguous = {}
    
    for type_name in type_refs:
        defining_namespaces = []
        
        # Check current namespace first
        if current_ns in ns_type_map and type_name in ns_type_map[current_ns]:
            defining_namespaces.append(current_ns)
        
        # Check candidate namespaces
        for ns in candidate_namespaces:
            if ns in ns_type_map and type_name in ns_type_map[ns]:
                if ns not in defining_namespaces:
                    defining_namespaces.append(ns)
        
        # If defined in multiple namespaces (including current), it's ambiguous
        if len(defining_namespaces) > 1:
            ambiguous[type_name] = defining_namespaces
    
    return ambiguous


def resolve_using_conflicts(type_refs: set, candidate_namespaces: set, 
                            ns_type_map: dict, current_ns: str, 
                            core_namespaces: set) -> tuple:
    """
    Resolve conflicts between namespaces. Returns (safe_namespaces, excluded_namespaces).
    
    Priority order:
    1. Core Unity namespaces (always included)
    2. Namespaces that provide unique types we need
    3. Exclude namespaces that only add ambiguity
    """
    safe = set()
    excluded = set()
    
    # Always include core namespaces
    safe.update(core_namespaces)
    
    # Find ambiguous types
    all_candidates = candidate_namespaces | core_namespaces
    ambiguous = detect_ambiguous_types(type_refs, all_candidates, ns_type_map, current_ns)
    
    # For each candidate namespace, check if it's worth including
    for ns in candidate_namespaces:
        if ns in core_namespaces:
            continue  # Already included
        if ns == current_ns:
            continue  # Skip self
        if ns not in ns_type_map:
            continue
            
        types_in_ns = ns_type_map[ns]
        types_we_need = type_refs & types_in_ns
        
        if not types_we_need:
            # This namespace provides no types we use - skip it
            excluded.add(ns)
            continue
        
        # Check if any needed types would cause ambiguity
        causes_ambiguity = False
        for type_name in types_we_need:
            if type_name in ambiguous:
                # This type is ambiguous - check if this namespace is lower priority
                other_namespaces = [n for n in ambiguous[type_name] if n != ns]
                
                # Prefer core namespaces over generated ones
                core_has_it = any(n in core_namespaces for n in other_namespaces)
                if core_has_it:
                    causes_ambiguity = True
                    break
                    
                # Prefer current namespace over imported ones
                if current_ns in other_namespaces:
                    causes_ambiguity = True
                    break
        
        if causes_ambiguity:
            excluded.add(ns)
        else:
            safe.add(ns)
    
    return safe, excluded


def generate_smart_usings(code_body_lines: List[str], current_ns: str, 
                          all_types: List[TypeDef], valid_namespaces: set) -> List[str]:
    """
    Generate smart using statements based on actual type usage in the code.
    This is a two-pass approach:
    1. Extract all type references from the generated code body
    2. Determine which namespaces are needed and resolve conflicts
    """
    using_lines = []
    
    # Core system usings (always included)
    using_lines.append("using System;")
    using_lines.append("using System.Collections;")
    using_lines.append("using System.Collections.Generic;")
    using_lines.append("using GameSDK;")
    using_lines.append("")
    
    # Core Unity namespaces (high priority, always included if not current)
    core_unity_namespaces = {
        "UnityEngine", "UnityEngine.UI", "UnityEngine.Events", 
        "UnityEngine.EventSystems", "UnityEngine.Rendering",
        "UnityEngine.SceneManagement", "UnityEngine.Audio", "UnityEngine.AI",
        "UnityEngine.Animations", "TMPro"
    }
    
    using_lines.append("// Core Unity namespace references")
    for ns in sorted(core_unity_namespaces):
        if ns != current_ns:
            using_lines.append(f"using {ns};")
    using_lines.append("")
    
    # Extract type references from generated code
    type_refs = extract_type_references_from_code(code_body_lines)
    
    # Build namespace -> types map
    ns_type_map = build_namespace_type_map(all_types)
    
    # Find candidate namespaces (namespaces that have types we might need)
    candidate_namespaces = valid_namespaces - {current_ns, "Global"}
    
    # Resolve conflicts and get safe namespaces
    safe_namespaces, excluded_namespaces = resolve_using_conflicts(
        type_refs, candidate_namespaces, ns_type_map, current_ns, core_unity_namespaces
    )
    
    # Filter to only include namespaces that have types we actually use
    final_namespaces = set()
    for ns in safe_namespaces:
        if ns in core_unity_namespaces:
            continue  # Already added above
        if ns not in ns_type_map:
            continue
        types_in_ns = ns_type_map[ns]
        if type_refs & types_in_ns:  # We use at least one type from this namespace
            final_namespaces.add(ns)
    
    if final_namespaces:
        using_lines.append("// Generated namespace references (based on type usage)")
        for ns in sorted(final_namespaces):
            using_lines.append(f"using {ns};")
        using_lines.append("")
    
    # System namespaces needed for common types
    # We use full qualified names in some cases to avoid conflicts
    using_lines.append("// System namespaces for common types")
    using_lines.append("using System.Text;")  # StringBuilder, Encoding
    using_lines.append("using System.IO;")  # Stream, BinaryWriter (Path conflicts but less common)
    using_lines.append("using System.Xml;")  # XmlNode
    using_lines.append("using System.Reflection;")  # MemberInfo, MethodInfo
    using_lines.append("using System.Globalization;")  # CultureInfo
    using_lines.append("using System.Runtime.Serialization;")  # StreamingContext
    using_lines.append("using System.Threading;")  # CancellationToken
    using_lines.append("using System.Threading.Tasks;")  # Task
    using_lines.append("")
    
    return using_lines


# ---------- Code Generator ----------

# Well-known types that are always available - only includes C# built-ins and interfaces.
# Unity types should NOT be here - they should be generated from the dump.
KNOWN_TYPES = {
    # C# built-in types
    "void", "bool", "byte", "sbyte", "char", "decimal", "double", "float", "int", "uint",
    "long", "ulong", "short", "ushort", "object", "string", "IntPtr", "UIntPtr",
    # Common .NET types that are always available
    "Type", "Array", "Exception", "EventArgs", "Delegate", "MulticastDelegate",
    "Action", "Func", "Predicate", "Comparison", "Converter",
    "List", "Dictionary", "HashSet", "Queue", "Stack", "KeyValuePair",
    "Task", "ValueTask", "CancellationToken", "CancellationTokenSource",
    "TimeSpan", "DateTime", "DateTimeOffset", "Guid", "Uri", "Version",
    "Nullable", "Lazy", "WeakReference", "Tuple", "ValueTuple",
    "Stream", "MemoryStream", "StreamReader", "StreamWriter", "BinaryReader", "BinaryWriter",
    "StringBuilder", "Encoding",
    # Note: Regex/Match/Group are NOT included because they require System.Text.RegularExpressions
    # which is not always imported
    "XmlNode", "XmlDocument", "XmlElement", "XmlAttribute",
    "IEnumerator", "IEnumerable", "ICollection", "IList", "IDictionary",
    "IDisposable", "ICloneable", "IComparable", "IEquatable", "IFormattable",
    # These specific Unity/Il2Cpp types are defined in our Core stubs
    "Il2CppObject",
}


def is_type_resolvable(type_name: str, current_namespace: str = None, imported_namespaces: set = None) -> bool:
    """
    Check if a type can be resolved at compile time.
    
    A type is resolvable if:
    1. It's a built-in/primitive type (int, string, etc.)
    2. It's a generic type parameter (T, TKey, etc.)
    3. It's a known Unity/System type that we have stubs for
    4. It's in a namespace that will be imported
    5. It's in the current namespace being generated
    6. If used with generic parameters, a generic version must exist
    
    Args:
        type_name: The type name to check
        current_namespace: The namespace we're generating code for (optional)
        imported_namespaces: Set of namespaces that will be imported (optional)
    """
    if not type_name:
        return False
    
    # Check if this type is used with generic parameters
    # IL2CPP dump uses backtick notation: List`1, Dictionary`2, UniTask`1
    # C# uses angle brackets: List<T>, Dictionary<K,V>, UniTask<T>
    is_used_as_generic = ("<" in type_name and ">" in type_name) or "`" in type_name
    
    # Extract base type name (strip generics, arrays, etc.)
    base_name = type_name.split("<")[0].split("[")[0].split("`")[0].strip("?")
    
    # Check if it's a generic type parameter
    if is_generic_type_param(base_name):
        return True
    
    # Check if it's in TYPE_MAP (IL2CPP primitives like Int32 -> int)
    if base_name in TYPE_MAP:
        return True
    
    # Check if it's a known built-in type
    if base_name in KNOWN_TYPES:
        return True
    
    # If the type is used with generic parameters (e.g., List<T>, Dictionary<K,V>)
    if is_used_as_generic:
        # We only generate non-generic wrapper classes, so we can only allow:
        # 1. Known generic types from System/Unity (List<T>, Task<T>, etc.)
        # 2. Nullable<T> (T?)
        known_generics = {
            # System.Collections.Generic
            "List", "Dictionary", "HashSet", "Queue", "Stack", "LinkedList",
            "SortedList", "SortedDictionary", "SortedSet", "KeyValuePair",
            "IList", "IDictionary", "ICollection", "IEnumerable", "IEnumerator",
            "IReadOnlyList", "IReadOnlyCollection", "IReadOnlyDictionary",
            "ISet", "IComparer", "IEqualityComparer",
            # System
            "Nullable", "Tuple", "ValueTuple", "Lazy", "WeakReference",
            "Action", "Func", "Predicate", "Comparison", "Converter",
            "EventHandler", "ArraySegment", "Memory", "Span", "ReadOnlyMemory", "ReadOnlySpan",
            # System.Threading.Tasks
            "Task", "ValueTask",
            # Unity
            "NativeArray", "NativeList", "NativeHashMap", "NativeQueue",
        }
        
        if base_name not in known_generics:
            # We don't generate generic wrapper classes for game types
            # Using UniTask<T>, TweenCallback<T>, etc. will cause CS0308 errors
            return False
        
        # Also verify the generic type arguments themselves are resolvable
        inner = type_name[type_name.find("<")+1:type_name.rfind(">")]
        # Split by comma, but be careful of nested generics
        depth = 0
        current = ""
        args = []
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
        # If we have namespace context, check if the type is accessible
        if current_namespace is not None and imported_namespaces is not None:
            type_namespaces = TYPE_REGISTRY.get(base_name, [])
            for ns in type_namespaces:
                # Type is in current namespace - always accessible
                if ns == current_namespace:
                    # Also verify the type will actually be generated (has content)
                    if (base_name, ns) in GENERATED_TYPES:
                        return True
                # Type is in an imported namespace
                if ns in imported_namespaces:
                    # Also verify the type will actually be generated (has content)
                    if (base_name, ns) in GENERATED_TYPES:
                        return True
            # Type exists but not in accessible namespace or won't be generated
            return False
        else:
            # No namespace context - assume it's resolvable if it exists and will be generated
            for ns in TYPE_REGISTRY.get(base_name, []):
                if (base_name, ns) in GENERATED_TYPES:
                    return True
            return False
    
    # Type not found anywhere
    return False


# Namespaces that are always imported (core Unity/System)
ALWAYS_IMPORTED_NAMESPACES = {
    "System", "System.Collections", "System.Collections.Generic",
    "System.Text", "System.IO", "System.Xml", "System.Reflection",
    "System.Globalization", "System.Runtime.Serialization",
    "System.Threading", "System.Threading.Tasks",
    "UnityEngine", "UnityEngine.UI", "UnityEngine.Events",
    "UnityEngine.EventSystems", "UnityEngine.Rendering",
    "UnityEngine.SceneManagement", "UnityEngine.Audio", "UnityEngine.AI",
    "UnityEngine.Animations", "TMPro",
    "GameSDK",  # Our base namespace
    "Global",  # Types with no namespace are in the Global namespace and always accessible
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
    "Char": "char",
    "IntPtr": "IntPtr",
    "UIntPtr": "UIntPtr",
}

# Types that are already defined in UnityValueTypes.cs - skip generating these
SKIP_TYPES = {
    # Generic nested types that cause conflicts (same name used in multiple parent types)
    "Enumerator", "ConfiguredTaskAwaiter", "ControlBuilder", "Record", "Style",
    # Types that have circular base class dependencies
    "UxmlFactory", "UxmlTraits", "Recursion",
    # Native TLS callback types (Unity internal delegates with unresolvable param types)
    "unitytls_tlsctx_read_callback", "unitytls_tlsctx_write_callback",
    "unitytls_x509verify_callback", "unitytls_tlsctx_certificate_callback",
    # Unity capture result types (nested enum-like types from Global namespace)
    "CaptureResultType",
    # Types that reference external types from non-imported namespaces
    "MaterialReference",  # References TMP types, may have visibility issues
    "Match",  # System.Text.RegularExpressions.Match - namespace not imported
    # OpenVR binding types that have complex interface/type structure issues
    "OpenVR",  # Static class with complex CVR* type references causing CS1061
    # SynchronizationContext derived types that have constructor issues
    "UnitySynchronizationContext",  # Constructor parameter mismatch CS1729
    # Ambiguous types that conflict with System.Reflection types
    "Pointer",  # Conflicts with System.Reflection.Pointer
    # SDK types with complex field dependencies that may not be fully generated
    "SDKTexture",  # LIV SDK struct with unresolved field types
    # Types from VR/recording namespaces that aren't being generated
    "VisualRecordingIndicators",  # Recording indicator type not generated
    # Types that cause Map.Map circular resolution issues
    "Map",  # Multiple Map classes in Global namespace cause resolution issues
}

# Property names that conflict with C# keywords or System types
SKIP_PROPERTY_NAMES = {"Type", "Object", "String", "Int32", "Boolean", "Array"}

# Types that need to be fully qualified when used to avoid ambiguity
AMBIGUOUS_TYPES = {
    "EventHandler": "System.EventHandler",  # Conflicts with Global.EventHandler
    "Object": "UnityEngine.Object",  # In Unity dump, Object typically refers to UnityEngine.Object
    # These could conflict between UnityEngine.UI and other namespaces
    "Image": "UnityEngine.UI.Image",
    "Button": "UnityEngine.UI.Button",
    "Toggle": "UnityEngine.UI.Toggle",
    "Renderer": "UnityEngine.Renderer",
    "TextAsset": "UnityEngine.TextAsset",
    # System types that should be fully qualified
    "BigInteger": "System.Numerics.BigInteger",
    # Riptide/Global conflict
    "Message": "Riptide.Message",  # Prefer Riptide.Message since Global.Message is likely internal
    # Riptide types that conflict with namespaces (Multiplayer.Client, Multiplayer.Server)
    "Client": "Riptide.Client",
    "Server": "Riptide.Server",
    # System types
    "ErrorEventArgs": "System.IO.ErrorEventArgs",
    "IFormatter": "System.Runtime.Serialization.IFormatter",
    # Additional ambiguous types from remaining errors
    "SocketManager": "DecaGames.RotMG.Managers.Net.SocketManager",  # vs Steamworks.SocketManager
    "Random": "UnityEngine.Random",  # vs System.Random
    # Photon vs System.Collections conflict
    "Hashtable": "System.Collections.Hashtable",  # Prefer System.Collections.Hashtable
    # Version conflict between VisualDesignCafe.Packages and System
    "Version": "System.Version",  # Prefer System.Version
    # Note: Path is NOT added here because DG.Tweening.Plugins.Core.PathCore.Path is a valid game type
}

def is_obfuscated_type(type_name: str) -> bool:
    """Check if a type name looks like an obfuscated IL2CPP type (e.g., BNHDCIPKPDL or ಠ_ಠ)"""
    # Get the base type name without generics or arrays
    base = type_name.split("<")[0].split("[")[0].split("`")[0]
    
    # Check for non-ASCII characters (Malayalam, Greek, etc. obfuscation)
    if not has_valid_csharp_identifier_chars(base):
        return True
    
    # Check if it's all uppercase letters and >= 8 chars (obfuscated types are usually long)
    if len(base) >= 8 and base.isupper() and base.isalpha():
        return True
    return False

def map_type(il2cpp_type: str) -> str:
    """Convert IL2CPP type name to C# type name"""
    if not il2cpp_type:
        return None
    
    # Skip pointer types entirely - they can't be used without unsafe context
    if "*" in il2cpp_type:
        return None  # Signal to skip this type
    
    # Handle nested type references that should be simplified
    # InputAction.CallbackContext -> CallbackContext (it's in Global namespace)
    if "InputAction.CallbackContext" in il2cpp_type:
        return il2cpp_type.replace("InputAction.CallbackContext", "CallbackContext")
    
    # Handle generic types with angle brackets like InputControl<TValue>
    # If the type parameter is a generic type param (T, TValue, etc.), skip this type
    if "<" in il2cpp_type and ">" in il2cpp_type:
        # Extract the type arguments
        start = il2cpp_type.index("<")
        end = il2cpp_type.rindex(">")
        type_args_str = il2cpp_type[start+1:end]
        # Check if any type arg is a generic type param
        for arg in type_args_str.split(","):
            arg = arg.strip().rstrip("[]")
            if arg in GENERIC_TYPE_PARAMS:
                return None  # Skip types with unresolved generic params
    
    # Handle Nullable types - Nullable<object> is invalid, only value types allowed
    if il2cpp_type.startswith("Nullable`1") or "Nullable<" in il2cpp_type:
        # Nullable<T> where T is erased to object - skip these
        return None
    
    # Handle array types with ambiguous base type (e.g., Object[] -> UnityEngine.Object[])
    if il2cpp_type.endswith("[]"):
        array_base = il2cpp_type[:-2]
        if array_base in AMBIGUOUS_TYPES:
            return AMBIGUOUS_TYPES[array_base] + "[]"
    
    # Handle ambiguous types - return fully qualified name
    if il2cpp_type in AMBIGUOUS_TYPES:
        return AMBIGUOUS_TYPES[il2cpp_type]
    
    # Handle backtick generic notation (e.g., List`1 -> List<object>, Dictionary`2 -> Dictionary<object, object>)
    if "`" in il2cpp_type:
        parts = il2cpp_type.split("`")
        base_type = parts[0]
        # Check if base type is ambiguous
        if base_type in AMBIGUOUS_TYPES:
            base_type = AMBIGUOUS_TYPES[base_type]
        try:
            # Get the number of generic type parameters
            num_params = int(parts[1].split("[")[0].split("<")[0])
            type_args = ", ".join(["object"] * num_params)
            result = f"{base_type}<{type_args}>"
            # Preserve array suffix if present
            if il2cpp_type.endswith("[]"):
                result += "[]"
            return result
        except (ValueError, IndexError):
            # If parsing fails, just remove the backtick part
            return base_type
    
    return TYPE_MAP.get(il2cpp_type, il2cpp_type)

# C# reserved keywords that cannot be used as identifiers
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
    """
    Check if a name contains only valid C# identifier characters.
    C# allows: letters (a-z, A-Z), digits (0-9), underscores, and certain Unicode categories.
    For safety, we only allow ASCII letters, digits, and underscores.
    """
    if not name:
        return False
    for char in name:
        # Allow ASCII letters, digits, and underscores
        if char.isascii() and (char.isalnum() or char == '_'):
            continue
        # Reject all other characters including non-ASCII Unicode, $, etc.
        return False
    return True


def is_unicode_name(name: str) -> bool:
    """
    Check if a name contains non-ASCII Unicode characters (obfuscated names).
    """
    if not name:
        return False
    # Strip common generic markers before checking
    clean_name = name.replace("<", "").replace(">", "").replace(".", "").replace("|", "")
    return not has_valid_csharp_identifier_chars(clean_name)


def sanitize_unicode_namespace(ns: str) -> str:
    """
    Sanitize a namespace that may contain Unicode characters.
    Returns a valid C# namespace name.
    """
    if not ns or ns == "Global":
        return ns
    
    # Split by dots and check each part
    parts = ns.split(".")
    sanitized_parts = []
    unicode_ns_counter = 0
    
    for part in parts:
        if is_unicode_name(part):
            unicode_ns_counter += 1
            sanitized_parts.append(f"unicode_ns_{unicode_ns_counter}")
        else:
            sanitized_parts.append(part)
    
    return ".".join(sanitized_parts)


def sanitize_name(name: str, return_original: bool = False) -> str:
    """
    Sanitize method/parameter names to be valid C# identifiers.
    
    Args:
        name: The original name from IL2CPP dump
        return_original: If True, returns tuple (sanitized_name, original_name) for method calls
    
    Returns:
        Sanitized name as valid C# identifier, or None if should be skipped.
        If return_original=True, returns tuple (sanitized, original) or (None, None).
    """
    if not name:
        return (None, None) if return_original else None
        
    # Handle .ctor -> ctor (will be handled specially)
    if name == ".ctor":
        return (None, None) if return_original else None  # Skip constructors, we generate our own
    if name == ".cctor":
        return (None, None) if return_original else None  # Skip static constructors
    
    original_name = name
    
    # Check for non-ASCII characters (obfuscated names like Malayalam script)
    if not has_valid_csharp_identifier_chars(name.replace("<", "").replace(">", "").replace(".", "").replace("|", "")):
        # Name contains invalid characters - skip it
        # TODO: In future, could generate sanitized name like "Method_0x1234" using hash
        return (None, None) if return_original else None
    
    # Replace invalid characters
    name = name.replace("<", "_").replace(">", "_").replace(".", "_").replace("|", "_")
    
    # If starts with digit, prefix with underscore
    if name and name[0].isdigit():
        name = "_" + name
    
    # If name is a C# keyword, prefix with @
    if name in CSHARP_KEYWORDS:
        name = "@" + name
    
    if return_original:
        return (name, original_name)
    return name

# Generic type parameters - common patterns. We also use heuristic detection below.
GENERIC_TYPE_PARAMS = {"T", "T1", "T2", "T3", "T4", "TKey", "TValue", "TResult", "TSource", "TElement", "TControl", "TDevice", "TState", "TProcessor", "TObject", "U", "V", "TAttribute", "TData", "TDescriptor", "TEnum"}

def is_generic_type_param(type_name: str) -> bool:
    """
    Check if a type name is a generic type parameter like T, T1, TKey, TValueType, etc.
    Uses heuristic detection: starts with T followed by uppercase or is single uppercase letter.
    """
    base = type_name.rstrip("[]").rstrip("?")  # Remove array brackets and nullable
    
    # Check known generic params
    if base in GENERIC_TYPE_PARAMS:
        return True
    
    # Heuristic: Single uppercase letter (T, U, V, etc.)
    if len(base) == 1 and base.isupper():
        return True
    
    # Heuristic: Starts with T followed by uppercase letter (TKey, TValue, TResult, TValueType, TFieldValue, etc.)
    if len(base) >= 2 and base[0] == 'T' and base[1].isupper():
        # Make sure it's not a real type name like "TMPro" or "Transform"
        # Real Unity/game types usually have more than 4 chars after T
        # or are well-known like Texture, Transform, etc.
        known_t_types = {"TMPro", "Transform", "Texture", "Texture2D", "Texture3D", "TextMesh", 
                         "TextMeshPro", "TextMeshProUGUI", "Tween", "Tweener", "TweenParams",
                         "Thread", "Timer", "TimeSpan", "Task", "Type", "Tuple", "Toggle", 
                         "Tile", "Tilemap", "Touch", "TrailRenderer", "Terrain", "Tree"}
        if base not in known_t_types:
            return True
    
    return False

def get_generic_type_params_from_method(method) -> set:
    """Extract all generic type parameters used in method signature"""
    params = set()
    # Check return type
    base_return = method.return_type.rstrip("[]")
    if base_return in GENERIC_TYPE_PARAMS:
        params.add(base_return)
    # Check parameter types
    for p in method.parameters:
        base_param = p.type.rstrip("[]")
        if base_param in GENERIC_TYPE_PARAMS:
            params.add(base_param)
    return params

def has_generic_type_arg(type_str: str) -> bool:
    """Check if a type has generic type arguments that are generic params (like InputControl<TValue>)"""
    if "<" not in type_str:
        return False
    # Extract generic arguments
    start = type_str.index("<")
    end = type_str.rindex(">")
    generic_args = type_str[start+1:end]
    # Check if any generic argument is a generic type parameter
    for arg in generic_args.split(","):
        arg = arg.strip()
        if is_generic_type_param(arg):
            return True
    return False


def is_backtick_generic(type_str: str) -> bool:
    """Check if a type uses backtick generic notation like InputControl`1"""
    return "`" in type_str and not "<" in type_str


def is_valid_method(method: MethodDef, current_ns: str = None, imported_ns: set = None) -> bool:
    """Check if method should be included in output"""
    # Skip constructors
    if method.name in [".ctor", ".cctor"]:
        return False
    # Skip compiler-generated methods (contain | or other special chars)
    if "|" in method.name:
        return False
    # Skip explicit interface implementations for now
    if "." in method.name and method.name.count(".") > 0:
        # Check if it looks like System.IDisposable.Dispose
        parts = method.name.split(".")
        if len(parts) > 1 and parts[0][0].isupper():
            return False
    
    # Get generic type parameters used in this method
    generic_params = get_generic_type_params_from_method(method)
    method_is_generic = len(generic_params) > 0
    
    # Skip methods with generic type param return types (we can't generate proper wrappers for these)
    if is_generic_type_param(method.return_type):
        return False
    
    # Check return type is valid
    mapped_return = map_type(method.return_type)
    if mapped_return is None:
        return False
    # Skip methods with pointer types in return
    if "*" in method.return_type:
        return False
    # Check if return type is Unicode (non-ASCII) - these can't be used in C#
    return_base = method.return_type.split("<")[0].split("[")[0].split("`")[0]
    if is_unicode_name(return_base):
        return False
    # Skip methods where return type is not resolvable
    if not is_type_resolvable(method.return_type, current_ns, imported_ns):
        return False
    # Skip methods where return type is a generic class with generic param arguments (e.g., List<TValue>)
    if has_generic_type_arg(method.return_type):
        return False
    # Skip generic methods with backtick generic return types (e.g., List`1)
    # because we can't properly instantiate them without knowing the actual type args
    if method_is_generic and is_backtick_generic(method.return_type):
        return False
    
    for p in method.parameters:
        # Skip parameters without names (edge case from dump)
        if p.name == "__no_name__":
            return False
        # Skip methods with generic type param parameters (we can't generate proper wrappers for these)
        if is_generic_type_param(p.type):
            return False
        # Skip methods where param type is a generic class with generic param arguments (e.g., InputControl<TValue>)
        if has_generic_type_arg(p.type):
            return False
        # Skip generic methods with backtick generic param types (e.g., InputControl`1)
        # because we can't properly instantiate them without knowing the actual type args
        if method_is_generic and is_backtick_generic(p.type):
            return False
        # Check if param type is valid
        mapped_param = map_type(p.type)
        if mapped_param is None:
            return False
        # Skip methods with pointer types in parameters
        if "*" in p.type:
            return False
        # Check if param type is Unicode (non-ASCII) - these can't be used in C#
        param_base = p.type.split("<")[0].split("[")[0].split("`")[0]
        if is_unicode_name(param_base):
            return False
        # Skip methods with param types that aren't resolvable
        if not is_type_resolvable(p.type, current_ns, imported_ns):
            return False
        # Skip methods with out/ref/in parameters
        if p.modifier in ["out", "ref", "in"]:
            return False
    return True

def is_valid_property(prop: PropertyDef, current_ns: str = None, imported_ns: set = None) -> bool:
    """Check if property should be included in output"""
    # Skip properties with generic type params
    if is_generic_type_param(prop.type):
        return False
    # Check if property type is valid
    mapped_type = map_type(prop.type)
    if mapped_type is None:
        return False
    # Check if property type is Unicode (non-ASCII) - these can't be used in C#
    prop_base = prop.type.split("<")[0].split("[")[0].split("`")[0]
    if is_unicode_name(prop_base):
        return False
    # Skip properties with types that aren't resolvable
    if not is_type_resolvable(prop.type, current_ns, imported_ns):
        return False
    return True


def compute_imported_namespaces(current_ns: str, valid_namespaces: set) -> set:
    """
    Compute the set of namespaces that will be imported for a given file.
    
    IMPORTANT: We only import CORE namespaces (System, UnityEngine, etc.) to avoid
    type conflicts between game namespaces. Game types from other namespaces
    will not be accessible, which is intentional - we only generate wrappers
    for types that can be resolved without cross-namespace game dependencies.
    """
    # Only import core namespaces - no game namespaces
    # This prevents type conflicts like DecaGames.Edge vs Unity's Edge
    return set(ALWAYS_IMPORTED_NAMESPACES)
    
    return imported


def generate_wrapper_code_per_namespace(types: List[TypeDef], output_dir: str) -> dict:
    """Generate separate files for each namespace. Returns dict of namespace -> file path."""
    
    # Namespaces to skip (they conflict with actual .NET types or are internal)
    SKIP_NAMESPACES = {
        "System", "System.Collections", "System.Collections.Generic", 
        "System.IO", "System.Text", "System.Threading", "System.Threading.Tasks",
        "System.Linq", "System.Linq.Expressions", "System.Reflection",
        "System.Runtime", "System.Runtime.CompilerServices", "System.Runtime.InteropServices",
        "System.Diagnostics", "System.Globalization", "System.Security",
        "System.ComponentModel", "System.Net", "System.Xml", "mscorlib",
        # Mono internal namespaces
        "Mono", "Mono.Btls", "Mono.Net", "Mono.Net.Security", "Mono.Security",
        "Mono.Security.X509", "Mono.Security.X509.Extensions", "Mono.Security.Cryptography",
        # .NET internal namespaces
        "Internal", "Internal.Cryptography", "Internal.Runtime", "Internal.Runtime.Augments",
        "Microsoft", "Microsoft.Win32", "Microsoft.Win32.SafeHandles",
        # Unity internal namespaces
        "UnityEngine.Internal", "UnityEngineInternal",
    }

    # Group types by namespace
    namespaces = {}
    for t in types:
        ns = t.namespace if t.namespace else "Global"
        # Skip types from System namespaces
        if ns in SKIP_NAMESPACES or ns.startswith("System.") or ns.startswith("Mono.") or ns.startswith("Internal.") or ns.startswith("Microsoft."):
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
        # Deduplicate by name to avoid CS0101 errors
        seen_delegate_names = set()
        for t in ns_types:
            if t.kind != "class":
                continue
            if t.visibility not in ("public",):
                continue
            if t.base_type != "MulticastDelegate":
                continue
            if "`" in t.name or "<" in t.name or ">" in t.name:
                continue
            # Skip delegate types with invalid characters in their names ($ etc.)
            if not has_valid_csharp_identifier_chars(t.name):
                continue
            if t.name in seen_delegate_names:
                continue
            # Skip types that are already defined elsewhere
            if t.name in SKIP_TYPES:
                continue
            
            # Find the Invoke method to get the delegate signature
            invoke_method = None
            for m in t.methods:
                if m.name == "Invoke":
                    invoke_method = m
                    break
            
            if invoke_method:
                # Check for pointer types in delegate signature
                has_pointer = "*" in invoke_method.return_type
                for p in invoke_method.parameters:
                    if "*" in p.type:
                        has_pointer = True
                        break
                if has_pointer:
                    continue
                
                # Skip delegates with generic type parameters (T, TValue, etc.)
                has_generic_param = is_generic_type_param(invoke_method.return_type)
                for p in invoke_method.parameters:
                    if is_generic_type_param(p.type):
                        has_generic_param = True
                        break
                if has_generic_param:
                    continue  # Skip generic delegates for now
                
                # Check return type is valid and resolvable
                return_type = map_type(invoke_method.return_type)
                if return_type is None:
                    continue
                if not is_type_resolvable(invoke_method.return_type, ns, imported_ns):
                    continue
                
                # Check all parameter types are valid and resolvable
                valid_params = True
                for p in invoke_method.parameters:
                    mapped = map_type(p.type)
                    if mapped is None:
                        valid_params = False
                        break
                    if not is_type_resolvable(p.type, ns, imported_ns):
                        valid_params = False
                        break
                if not valid_params:
                    continue
                
                seen_delegate_names.add(t.name)
                type_count += 1
                params_str = ", ".join(
                    f"{map_type(p.type)} {sanitize_name(p.name) or p.name}"
                    for p in invoke_method.parameters
                )
                body_lines.append(f"    {t.visibility} delegate {return_type} {t.name}({params_str});")
                body_lines.append("")

        # Then, generate enums
        # Track seen type names to avoid duplicates
        seen_type_names = set()
        for t in ns_types:
            if t.kind != "enum":
                continue
            if t.visibility not in ("public",):
                continue
            if "`" in t.name or "<" in t.name or ">" in t.name:
                continue
            # Skip types with Unicode/non-ASCII characters in their names (obfuscated types)
            if not has_valid_csharp_identifier_chars(t.name):
                continue
            # Skip generic nested enum names that cause conflicts
            if t.name in SKIP_NESTED_ENUM_NAMES:
                continue
            # Skip types that are already defined elsewhere
            if t.name in SKIP_TYPES:
                continue
            # Skip if already seen in this namespace
            if t.name in seen_type_names:
                continue
            seen_type_names.add(t.name)

            type_count += 1
            body_lines.append(f"    {t.visibility} enum {t.name}")
            body_lines.append("    {")
            
            # Output enum values (const fields)
            enum_values = [f for f in t.fields if f.is_const and f.type == t.name]
            valid_values = []
            for ev in enum_values:
                if ev.value is not None:
                    # Extract just the numeric part (e.g., "0" from "0 // 0x0")
                    val = ev.value.split("//")[0].strip()
                    # Skip values that exceed int max (enums default to int)
                    try:
                        int_val = int(val)
                        if int_val > 2147483647 or int_val < -2147483648:
                            continue  # Skip values outside int range
                    except ValueError:
                        pass  # Keep non-numeric values as-is
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
            if t.kind != "interface":
                continue
            if t.visibility not in ("public",):
                continue
            if "`" in t.name or "<" in t.name or ">" in t.name:
                continue
            # Skip types with Unicode/non-ASCII characters in their names (obfuscated types)
            if not has_valid_csharp_identifier_chars(t.name):
                continue
            # Skip types that are already defined elsewhere
            if t.name in SKIP_TYPES:
                continue
            # Skip if already seen in this namespace
            if t.name in seen_type_names:
                continue
            seen_type_names.add(t.name)

            type_count += 1
            body_lines.append(f"    {t.visibility} interface {t.name}")
            body_lines.append("    {")
            body_lines.append("        // Stub interface")
            body_lines.append("    }")
            body_lines.append("")

        # Then, generate structs (as simple stub structs with fields)
        for t in ns_types:
            if t.kind != "struct":
                continue
            if t.visibility not in ("public",):
                continue
            if "`" in t.name or "<" in t.name or ">" in t.name:
                continue
            # Skip types with Unicode/non-ASCII characters in their names (obfuscated types)
            if not has_valid_csharp_identifier_chars(t.name):
                continue
            # Skip types that are already defined elsewhere (UnityValueTypes.cs)
            if t.name in SKIP_TYPES:
                continue
            # Skip if already seen in this namespace
            if t.name in seen_type_names:
                continue
            
            # Check if struct has any fields with generic type params - skip those structs
            has_generic_fields = False
            for fld in t.fields:
                if fld.visibility == "public" and not fld.is_const:
                    if is_generic_type_param(fld.type):
                        has_generic_fields = True
                        break
            if has_generic_fields:
                continue  # Skip structs with generic type param fields
            
            seen_type_names.add(t.name)

            type_count += 1
            body_lines.append(f"    {t.visibility} struct {t.name}")
            body_lines.append("    {")
            
            # Output public fields (skip const fields which are enum-like)
            # Also skip fields with invalid types or unresolvable types
            public_fields = [f for f in t.fields if f.visibility == "public" and not f.is_const]
            valid_field_count = 0
            for fld in public_fields:
                field_type = map_type(fld.type)
                if field_type is None:
                    continue  # Skip fields with invalid types
                # Also check if the field type is resolvable from this namespace
                if not is_type_resolvable(fld.type, ns, imported_ns):
                    continue  # Skip fields with unresolvable types
                valid_field_count += 1
                body_lines.append(f"        public {field_type} {fld.name};")
            
            # If no public fields, add a placeholder to make it valid
            if valid_field_count == 0:
                body_lines.append("        // Stub struct")
            
            body_lines.append("    }")
            body_lines.append("")

        # Then, generate classes
        unicode_class_counter = 0  # Counter for unicode classes in this namespace
        
        for t in ns_types:
            if t.kind != "class":
                continue

            # Skip non-public classes (private, protected, internal can't be top-level)
            if t.visibility not in ("public",):
                continue

            # Skip generic type definitions (names with backticks like Iterator`1)
            if "`" in t.name:
                continue

            # Skip compiler-generated types (angle brackets in name like <>c__DisplayClass)
            if "<" in t.name or ">" in t.name:
                continue

            # Check if this is a Unicode class name
            is_unicode_class = is_unicode_name(t.name)
            original_class_name = t.name  # Store original for IL2CPP calls
            
            if is_unicode_class:
                unicode_class_counter += 1
                class_name = f"unicode_class_{unicode_class_counter}"
            else:
                class_name = t.name

            # Skip types that derive from special .NET types
            # Clean base type first (remove trailing comma from multi-inheritance)
            clean_base = t.base_type.rstrip(",").strip() if t.base_type else None
            if clean_base and clean_base in SKIP_BASE_TYPES:
                continue

            # Skip types that are already defined elsewhere
            if t.name in SKIP_TYPES:
                continue
            
            # Skip if already seen in this namespace (use sanitized name)
            if class_name in seen_type_names:
                continue
            seen_type_names.add(class_name)

            type_count += 1

            # Clean up base type (handle generic syntax issues)
            base_type = t.base_type
            if base_type:
                # Skip generic base types (we can't inherit from them without proper type args)
                if "`" in base_type:
                    base_type = None
                else:
                    # Remove trailing commas or invalid chars
                    base_type = base_type.rstrip(",").strip()
                    # Skip interface base types (start with I followed by uppercase)
                    # This avoids CS0535 errors where we don't implement interface members
                    if base_type.startswith("I") and len(base_type) > 1 and base_type[1].isupper():
                        base_type = None
                    # Skip problematic base types
                    if base_type in {"IEnumerator", "IDisposable", "IComparer", "IEnumerable", "ICollection"}:
                        base_type = None
                    # Skip obfuscated base types (they might not be generated)
                    if base_type and is_obfuscated_type(base_type):
                        base_type = None
                    # Check if the base type is resolvable from this namespace
                    # This catches cases where base type is in a non-imported namespace
                    if base_type and not is_type_resolvable(base_type, ns, imported_ns):
                        # Check if it's a well-known Unity/System base type that's always available
                        known_base_types = {
                            "MonoBehaviour", "ScriptableObject", "Component", "Behaviour", 
                            "Object", "UnityEvent", "UnityEventBase", "Selectable", "UIBehaviour",
                            "Graphic", "MaskableGraphic", "Image", "Text", "Texture", "Texture2D",
                            "Material", "Mesh", "Sprite", "Camera", "Transform", "RectTransform",
                            "Canvas", "CanvasGroup", "EventTrigger", "AudioSource", "AudioClip",
                            "Animator", "Animation", "ParticleSystem", "Renderer", "Collider",
                            "Rigidbody", "Rigidbody2D", "Il2CppObject"
                        }
                        if base_type not in known_base_types:
                            base_type = None

            # Class declaration
            base_part = f" : {base_type}" if base_type else " : Il2CppObject"
            
            # Add XML doc for unicode classes
            if is_unicode_class:
                body_lines.append(f"    /// <summary>Obfuscated class. Original name: '{original_class_name}'</summary>")
            
            body_lines.append(f"    {t.visibility} partial class {class_name}{base_part}")
            body_lines.append("    {")

            # For unicode classes or unicode namespaces, store original name and namespace for IL2CPP lookups
            if is_unicode_class or is_unicode_ns:
                body_lines.append(f"        /// <summary>Original IL2CPP class name for runtime lookups</summary>")
                body_lines.append(f"        private const string _il2cppClassName = \"{original_class_name}\";")
                body_lines.append(f"        private const string _il2cppNamespace = \"{original_ns}\";")
                body_lines.append("")

            # Constructor - always call base(nativePtr) since we inherit from Il2CppObject at minimum
            body_lines.append(f"        public {class_name}(IntPtr nativePtr) : base(nativePtr) {{ }}")
            body_lines.append("")

            # Build a set of method names to avoid property conflicts
            method_names = {m.name for m in t.methods}
            
            # ============================================================
            # Property Generation - Convert get_/set_ methods to properties
            # ============================================================
            # Find get_X and set_X methods and consolidate them into proper C# properties
            property_methods = {}  # property_name -> {"get": method, "set": method}
            methods_used_as_properties = set()  # Track which methods become properties
            
            for m in t.methods:
                if not is_valid_method(m, ns, imported_ns):
                    continue
                # Detect get_PropertyName() methods (no parameters, has return type)
                if m.name.startswith("get_") and len(m.parameters) == 0 and m.return_type != "Void":
                    prop_name = m.name[4:]  # Strip "get_"
                    if prop_name not in property_methods:
                        property_methods[prop_name] = {"get": None, "set": None, "type": None}
                    property_methods[prop_name]["get"] = m
                    property_methods[prop_name]["type"] = m.return_type
                    methods_used_as_properties.add(m.name)
                # Detect set_PropertyName(value) methods (one parameter, returns void)
                elif m.name.startswith("set_") and len(m.parameters) == 1 and m.return_type == "Void":
                    prop_name = m.name[4:]  # Strip "set_"
                    if prop_name not in property_methods:
                        property_methods[prop_name] = {"get": None, "set": None, "type": None}
                    property_methods[prop_name]["set"] = m
                    # Use setter param type if no getter type yet
                    if property_methods[prop_name]["type"] is None:
                        property_methods[prop_name]["type"] = m.parameters[0].type
                    methods_used_as_properties.add(m.name)
            
            # Generate properties from consolidated get_/set_ methods
            unicode_property_counter = 0  # Counter for unicode properties in this class
            
            if property_methods:
                body_lines.append("        // Properties")
                for prop_name, prop_info in sorted(property_methods.items()):
                    original_prop_name = prop_name  # Store original for IL2CPP calls
                    
                    # Check if property name has Unicode characters
                    is_unicode_prop = is_unicode_name(prop_name)
                    if is_unicode_prop:
                        unicode_property_counter += 1
                        display_prop_name = f"unicode_property_{unicode_property_counter}"
                    else:
                        display_prop_name = prop_name
                    
                    # Skip properties with names that conflict with System types
                    # These will be generated as regular get_X/set_X methods instead
                    if prop_name in SKIP_PROPERTY_NAMES:
                        # Remove from methods_used_as_properties so they get generated as methods
                        if prop_info["get"]:
                            methods_used_as_properties.discard(f"get_{prop_name}")
                        if prop_info["set"]:
                            methods_used_as_properties.discard(f"set_{prop_name}")
                        continue
                    
                    prop_type = map_type(prop_info["type"])
                    if prop_type is None:
                        continue  # Skip properties with invalid types
                    
                    # Skip properties with types that aren't resolvable (e.g., private nested types)
                    if not is_type_resolvable(prop_info["type"], ns, imported_ns):
                        # Remove from methods_used_as_properties so they get skipped
                        if prop_info["get"]:
                            methods_used_as_properties.discard(f"get_{prop_name}")
                        if prop_info["set"]:
                            methods_used_as_properties.discard(f"set_{prop_name}")
                        continue
                    
                    # Determine visibility (use getter visibility if available, else setter)
                    visibility = "public"
                    if prop_info["get"]:
                        visibility = prop_info["get"].visibility
                    elif prop_info["set"]:
                        visibility = prop_info["set"].visibility
                    
                    # Check for static (both get and set should match)
                    is_static = False
                    if prop_info["get"] and prop_info["get"].is_static:
                        is_static = True
                    elif prop_info["set"] and prop_info["set"].is_static:
                        is_static = True
                    
                    static_keyword = "static " if is_static else ""
                    
                    # Get RVA for unicode properties
                    getter_rva = prop_info["get"].rva if prop_info["get"] else None
                    setter_rva = prop_info["set"].rva if prop_info["set"] else None
                    
                    # Add XML doc for unicode properties
                    if is_unicode_prop:
                        body_lines.append(f"        /// <summary>Obfuscated property. Original name: '{original_prop_name}'</summary>")
                    
                    body_lines.append(f"        {visibility} {static_keyword}{prop_type} {display_prop_name}")
                    body_lines.append("        {")
                    
                    if prop_info["get"]:
                        if is_unicode_prop and getter_rva:
                            # Use RVA-based call for unicode property getter
                            if is_static:
                                body_lines.append(f"            get => Il2CppRuntime.CallStaticByRva<{prop_type}>({getter_rva}, global::System.Type.EmptyTypes);")
                            else:
                                body_lines.append(f"            get => Il2CppRuntime.CallByRva<{prop_type}>(this, {getter_rva}, global::System.Type.EmptyTypes);")
                        else:
                            # Use original property name and namespace for IL2CPP lookup
                            if is_static:
                                body_lines.append(f"            get => Il2CppRuntime.CallStatic<{prop_type}>(\"{original_ns}\", \"{original_class_name}\", \"get_{original_prop_name}\", global::System.Type.EmptyTypes);")
                            else:
                                body_lines.append(f"            get => Il2CppRuntime.Call<{prop_type}>(this, \"get_{original_prop_name}\", global::System.Type.EmptyTypes);")
                    
                    if prop_info["set"]:
                        if is_unicode_prop and setter_rva:
                            # Use RVA-based call for unicode property setter
                            if is_static:
                                body_lines.append(f"            set => Il2CppRuntime.InvokeStaticVoidByRva({setter_rva}, new[] {{ typeof({prop_type}) }}, value);")
                            else:
                                body_lines.append(f"            set => Il2CppRuntime.InvokeVoidByRva(this, {setter_rva}, new[] {{ typeof({prop_type}) }}, value);")
                        else:
                            # Use original names and namespace for IL2CPP lookup
                            if is_static:
                                body_lines.append(f"            set => Il2CppRuntime.InvokeStaticVoid(\"{original_ns}\", \"{original_class_name}\", \"set_{original_prop_name}\", new[] {{ typeof({prop_type}) }}, value);")
                            else:
                                body_lines.append(f"            set => Il2CppRuntime.InvokeVoid(this, \"set_{original_prop_name}\", new[] {{ typeof({prop_type}) }}, value);")
                    
                    body_lines.append("        }")
                    body_lines.append("")
                body_lines.append("")

            # Methods - deduplicate by signature and exclude methods used as properties
            # Only include public methods
            valid_methods = [m for m in t.methods if m.visibility == "public" and is_valid_method(m, ns, imported_ns) and m.name not in methods_used_as_properties]
            seen_signatures = set()
            deduped_methods = []
            for method in valid_methods:
                # For Unicode methods, use a placeholder name for deduplication
                if is_unicode_name(method.name):
                    if method.rva:
                        method_name = f"__unicode_method_rva_{method.rva}__"
                    else:
                        continue  # Skip Unicode methods without RVA
                else:
                    method_name = sanitize_name(method.name)
                    if not method_name:
                        continue
                # Create signature key: name + parameter types (map generic params to 'object' for dedup)
                param_types = tuple(
                    'object' if is_generic_type_param(p.type) else map_type(p.type)
                    for p in method.parameters
                )
                sig_key = (method_name, param_types)
                if sig_key not in seen_signatures:
                    seen_signatures.add(sig_key)
                    deduped_methods.append(method)
            
            if deduped_methods:
                body_lines.append("        // Methods")
                unicode_method_counter = 0  # Counter for unicode methods in this class
                
                for method in deduped_methods:
                    # Check if method has Unicode name
                    is_unicode_method = is_unicode_name(method.name)
                    
                    if is_unicode_method:
                        # Generate sanitized name for Unicode methods
                        if method.rva:
                            unicode_method_counter += 1
                            method_name = f"unicode_method_{unicode_method_counter}"
                            use_rva = True
                        else:
                            # No RVA available, skip this method
                            continue
                    else:
                        method_name = sanitize_name(method.name)
                        if not method_name:
                            continue
                        use_rva = False

                    # Check if this is a generic method
                    generic_params = get_generic_type_params_from_method(method)
                    is_generic = len(generic_params) > 0
                    
                    # Build type parameter clause for generic methods
                    type_params_clause = ""
                    if is_generic:
                        sorted_params = sorted(generic_params)  # Consistent ordering
                        type_params_clause = f"<{', '.join(sorted_params)}>"
                    
                    # Map return type (use generic param name for generic types)
                    if is_generic_type_param(method.return_type):
                        return_type = method.return_type  # Keep as T, T[], etc.
                    else:
                        return_type = map_type(method.return_type)

                    # Build parameter list
                    param_parts = []
                    for idx, p in enumerate(method.parameters):
                        mod_prefix = f"{p.modifier} " if p.modifier else ""
                        if is_generic_type_param(p.type):
                            ptype = p.type  # Keep as T, T[], etc.
                        else:
                            ptype = map_type(p.type)
                        # Sanitize parameter name, or use fallback arg0, arg1, etc.
                        pname = sanitize_name(p.name)
                        if pname is None:
                            pname = f"arg{idx}"
                        param_parts.append(f"{mod_prefix}{ptype} {pname}")
                    param_list = ", ".join(param_parts)

                    # Build parameter names for the call
                    param_names_list = []
                    for idx, p in enumerate(method.parameters):
                        pname = sanitize_name(p.name)
                        if pname is None:
                            pname = f"arg{idx}"
                        param_names_list.append(pname)
                    param_names = ", ".join(param_names_list)

                    # Build paramTypes array (for generic params, use typeof(object))
                    if method.parameters:
                        param_types_items = []
                        for p in method.parameters:
                            if is_generic_type_param(p.type):
                                # For generic params, we can use typeof(T) in C#
                                param_types_items.append(f'typeof({p.type.rstrip("[]")})')
                            else:
                                param_types_items.append(f'typeof({map_type(p.type)})')
                        param_types_decl = f"new Type[] {{ {', '.join(param_types_items)} }}"
                    else:
                        param_types_decl = "global::System.Type.EmptyTypes"

                    static_keyword = "static " if method.is_static else ""
                    
                    # Add comment for Unicode methods showing original name and RVA
                    if use_rva:
                        body_lines.append(f"        /// <summary>Obfuscated method. Original name: {repr(method.name)}, RVA: {method.rva}</summary>")
                    
                    body_lines.append(f"        {method.visibility} {static_keyword}{return_type} {method_name}{type_params_clause}({param_list})")
                    body_lines.append("        {")

                    # For generic methods, generate a stub with NotImplementedException for now
                    # because IL2CPP requires concrete type instantiation at compile time
                    if is_generic:
                        body_lines.append(f"            // TODO: Generic method - IL2CPP requires specific type instantiation")
                        body_lines.append(f"            // Consider using Il2CppRuntime with a concrete type wrapper")
                        body_lines.append(f"            throw new System.NotImplementedException(\"Generic method {method_name}{type_params_clause} requires IL2CPP generic instantiation\");")
                    elif use_rva:
                        # Use RVA-based call for Unicode methods
                        rva_value = method.rva  # e.g., "0x52f1e0"
                        if return_type == "void":
                            if method.is_static:
                                call_method = "Il2CppRuntime.InvokeStaticVoidByRva"
                                call_args = f'{rva_value}, {param_types_decl}'
                                if param_names:
                                    call_args += f", {param_names}"
                                body_lines.append(f"            {call_method}({call_args});")
                            else:
                                call_method = "Il2CppRuntime.InvokeVoidByRva"
                                call_args = f'this, {rva_value}, {param_types_decl}'
                                if param_names:
                                    call_args += f", {param_names}"
                                body_lines.append(f"            {call_method}({call_args});")
                        else:
                            if method.is_static:
                                call_method = f"Il2CppRuntime.CallStaticByRva<{return_type}>"
                                call_args = f'{rva_value}, {param_types_decl}'
                                if param_names:
                                    call_args += f", {param_names}"
                                body_lines.append(f"            return {call_method}({call_args});")
                            else:
                                call_method = f"Il2CppRuntime.CallByRva<{return_type}>"
                                call_args = f'this, {rva_value}, {param_types_decl}'
                                if param_names:
                                    call_args += f", {param_names}"
                                body_lines.append(f"            return {call_method}({call_args});")
                    else:
                        # Determine call type for non-generic methods
                        # Use original_class_name and original_ns for IL2CPP lookup (handles unicode names)
                        if return_type == "void":
                            if method.is_static:
                                call_method = "Il2CppRuntime.InvokeStaticVoid"
                                call_args = f'"{original_ns}", "{original_class_name}", "{method.name}", {param_types_decl}'
                                if param_names:
                                    call_args += f", {param_names}"
                                body_lines.append(f"            {call_method}({call_args});")
                            else:
                                call_method = "Il2CppRuntime.InvokeVoid"
                                call_args = f'this, "{method.name}", {param_types_decl}'
                                if param_names:
                                    call_args += f", {param_names}"
                                body_lines.append(f"            {call_method}({call_args});")
                        else:
                            if method.is_static:
                                call_method = f"Il2CppRuntime.CallStatic<{return_type}>"
                                call_args = f'"{original_ns}", "{original_class_name}", "{method.name}", {param_types_decl}'
                                if param_names:
                                    call_args += f", {param_names}"
                                body_lines.append(f"            return {call_method}({call_args});")
                            else:
                                call_method = f"Il2CppRuntime.Call<{return_type}>"
                                call_args = f'this, "{method.name}", {param_types_decl}'
                                if param_names:
                                    call_args += f", {param_names}"
                                body_lines.append(f"            return {call_method}({call_args});")

                    body_lines.append("        }")
                    body_lines.append("")

            body_lines.append("    }")
            body_lines.append("")

        body_lines.append("}")
        body_lines.append("")

        # Only write file if we have types
        if type_count > 0:
            # PHASE 2: Generate smart using statements based on type usage
            using_lines = generate_smart_usings(body_lines, ns, all_types_list, valid_namespaces_for_using)
            
            # PHASE 3: Assemble final output
            output_lines = []
            output_lines.append("// Auto-generated Il2Cpp wrapper classes")
            output_lines.append(f"// Namespace: {ns}")
            output_lines.append("// Do not edit manually")
            output_lines.append("")
            output_lines.extend(using_lines)
            output_lines.extend(body_lines)
            
            # Create safe filename from namespace
            safe_ns = ns.replace(".", "_")
            filename = f"GameSDK.{safe_ns}.cs"
            filepath = os.path.join(output_dir, filename)
            
            with open(filepath, "w", encoding="utf-8") as f:
                f.write("\n".join(output_lines))
            
            generated_files[ns] = filepath

    return generated_files


# ---------- Build functionality using Roslyn csc.exe or dotnet CLI ----------

def find_csc_compiler():
    """Find the C# compiler - prefer Roslyn (modern) over legacy .NET Framework csc."""
    import glob
    
    # Priority 1: Roslyn compiler from Visual Studio Build Tools or VS installation
    # These support modern C# features
    vs_paths = [
        r"C:\Program Files\Microsoft Visual Studio\2022\*\MSBuild\Current\Bin\Roslyn\csc.exe",
        r"C:\Program Files (x86)\Microsoft Visual Studio\2022\*\MSBuild\Current\Bin\Roslyn\csc.exe",
        r"C:\Program Files\Microsoft Visual Studio\2019\*\MSBuild\Current\Bin\Roslyn\csc.exe",
        r"C:\Program Files (x86)\Microsoft Visual Studio\2019\*\MSBuild\Current\Bin\Roslyn\csc.exe",
    ]
    
    for pattern in vs_paths:
        matches = glob.glob(pattern)
        if matches:
            return matches[0]
    
    # Priority 2: .NET SDK csc (if available)
    sdk_paths = glob.glob(r"C:\Program Files\dotnet\sdk\*\Roslyn\bincore\csc.dll")
    if sdk_paths:
        # Can't use csc.dll directly, need dotnet CLI
        pass
    
    # Priority 3: Legacy .NET Framework csc (only supports C# 5)
    # This won't work with modern syntax, but we return it as a fallback
    framework_paths = [
        r"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
        r"C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe",
    ]
    
    for path in framework_paths:
        if os.path.exists(path):
            return path
    
    return None


def check_dotnet_available():
    """Check if dotnet CLI is available."""
    import subprocess
    try:
        result = subprocess.run(["dotnet", "--version"], capture_output=True, text=True)
        return result.returncode == 0
    except:
        return False


def get_framework_references():
    """Get the standard .NET Framework 4.7.2 reference assemblies."""
    # Reference assemblies location for .NET Framework 4.7.2
    ref_path = r"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2"
    
    if not os.path.exists(ref_path):
        # Fallback to 4.8 or 4.6.1
        alternatives = [
            r"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8",
            r"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.6.1",
            r"C:\Windows\Microsoft.NET\Framework64\v4.0.30319",
        ]
        for alt in alternatives:
            if os.path.exists(alt):
                ref_path = alt
                break
    
    # Core references needed for the project
    refs = [
        "mscorlib.dll",
        "System.dll",
        "System.Core.dll",
        "System.Runtime.dll",
    ]
    
    ref_args = []
    for ref in refs:
        full_path = os.path.join(ref_path, ref)
        if os.path.exists(full_path):
            ref_args.append(f'/reference:"{full_path}"')
        else:
            # Try without path - csc will find it in GAC
            ref_args.append(f'/reference:{ref}')
    
    return ref_args


def build_project(dump_dir, log_file_path):
    """Build the MDB_Core project using Roslyn csc.exe or dotnet CLI."""
    import subprocess
    import datetime
    
    core_dir = os.path.join(dump_dir, "MDB_Core")
    generated_dir = os.path.join(core_dir, "Generated")
    output_dir = os.path.join(dump_dir, "..", "Managed")  # GameDir/MDB/Managed
    output_dll = os.path.join(output_dir, "GameSDK.ModHost.dll")
    
    # Create output directory
    os.makedirs(output_dir, exist_ok=True)
    
    log_lines = []
    log_lines.append(f"MDB Framework Build Log")
    log_lines.append(f"=======================")
    log_lines.append(f"Build started: {datetime.datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    log_lines.append(f"")
    
    # Try dotnet CLI first (most reliable for modern C#)
    use_dotnet = check_dotnet_available()
    csc_path = find_csc_compiler()
    is_roslyn = csc_path and "Roslyn" in csc_path
    
    # Collect all source files
    source_files = []
    
    # Core files
    core_files_dir = os.path.join(core_dir, "Core")
    if os.path.exists(core_files_dir):
        for f in os.listdir(core_files_dir):
            if f.endswith(".cs"):
                source_files.append(os.path.join(core_files_dir, f))
    
    # ModHost files
    modhost_dir = os.path.join(core_dir, "ModHost")
    if os.path.exists(modhost_dir):
        for f in os.listdir(modhost_dir):
            if f.endswith(".cs"):
                source_files.append(os.path.join(modhost_dir, f))
    
    # Generated files
    if os.path.exists(generated_dir):
        for f in os.listdir(generated_dir):
            if f.endswith(".cs"):
                source_files.append(os.path.join(generated_dir, f))
    
    log_lines.append(f"Source files: {len(source_files)}")
    log_lines.append(f"  - Core: {len([f for f in source_files if 'Core' in f and 'Generated' not in f])}")
    log_lines.append(f"  - ModHost: {len([f for f in source_files if 'ModHost' in f])}")
    log_lines.append(f"  - Generated: {len([f for f in source_files if 'Generated' in f])}")
    log_lines.append(f"")
    
    if not source_files:
        error_msg = "ERROR: No source files found to compile."
        log_lines.append(error_msg)
        print(f"[!] {error_msg}")
        with open(log_file_path, "w", encoding="utf-8") as f:
            f.write("\n".join(log_lines))
        return False
    
    # Decide which build method to use
    if use_dotnet:
        log_lines.append(f"Build method: dotnet CLI (msbuild)")
        log_lines.append(f"")
        success = build_with_dotnet(core_dir, output_dir, log_lines)
    elif is_roslyn:
        log_lines.append(f"Build method: Roslyn csc.exe")
        log_lines.append(f"Compiler: {csc_path}")
        log_lines.append(f"")
        success = build_with_csc(csc_path, source_files, output_dll, core_dir, log_lines, use_modern_syntax=True)
    elif csc_path:
        log_lines.append(f"Build method: Legacy .NET Framework csc.exe (C# 5 only)")
        log_lines.append(f"Compiler: {csc_path}")
        log_lines.append(f"WARNING: Legacy compiler may not support all C# features!")
        log_lines.append(f"")
        success = build_with_csc(csc_path, source_files, output_dll, core_dir, log_lines, use_modern_syntax=False)
    else:
        error_msg = "ERROR: No C# compiler found. Install Visual Studio Build Tools, .NET SDK, or .NET Framework."
        log_lines.append(error_msg)
        print(f"[!] {error_msg}")
        success = False
    
    log_lines.append(f"")
    log_lines.append(f"Build finished: {datetime.datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    
    # Write log file
    with open(log_file_path, "w", encoding="utf-8") as f:
        f.write("\n".join(log_lines))
    
    return success


def build_with_dotnet(core_dir, output_dir, log_lines):
    """Build using dotnet CLI with the .csproj file."""
    import subprocess
    
    csproj_path = os.path.join(core_dir, "GameSDK.ModHost.csproj")
    
    if not os.path.exists(csproj_path):
        log_lines.append(f"ERROR: Project file not found: {csproj_path}")
        return False
    
    log_lines.append(f"Project: {csproj_path}")
    log_lines.append(f"Output: {output_dir}")
    log_lines.append(f"")
    log_lines.append(f"Build output:")
    log_lines.append(f"-" * 50)
    
    try:
        result = subprocess.run(
            ["dotnet", "build", csproj_path, "-c", "Release", "-v", "minimal"],
            capture_output=True,
            text=True,
            cwd=core_dir
        )
        
        if result.stdout:
            log_lines.append(result.stdout)
        if result.stderr:
            log_lines.append(result.stderr)
        
        log_lines.append(f"-" * 50)
        
        if result.returncode == 0:
            log_lines.append(f"BUILD SUCCEEDED")
            print(f"[+] Build succeeded")
            return True
        else:
            log_lines.append(f"BUILD FAILED (exit code: {result.returncode})")
            print(f"[!] Build failed. Check build_log.txt for details.")
            return False
            
    except Exception as e:
        log_lines.append(f"ERROR: {str(e)}")
        return False


def build_with_csc(csc_path, source_files, output_dll, core_dir, log_lines, use_modern_syntax=True):
    """Build using csc.exe directly."""
    import subprocess
    
    # Build compiler arguments
    args = [
        f'"{csc_path}"',
        "/target:library",
        "/optimize+",
        "/nowarn:CS0108,CS0114,CS0162,CS0168,CS0169,CS0219,CS0414,CS0649,CS0693,CS1030",
        f'/out:"{output_dll}"',
        "/nologo",
    ]
    
    # Add langversion for Roslyn
    if use_modern_syntax:
        args.append("/langversion:latest")
    
    # Add framework references
    args.extend(get_framework_references())
    
    # Add source files (use response file for long command lines)
    response_file = os.path.join(core_dir, "build.rsp")
    with open(response_file, "w", encoding="utf-8") as f:
        for src in source_files:
            f.write(f'"{src}"\n')
    
    args.append(f'@"{response_file}"')
    
    log_lines.append(f"Output: {output_dll}")
    log_lines.append(f"")
    log_lines.append(f"Build output:")
    log_lines.append(f"-" * 50)
    
    # Run the compiler
    try:
        result = subprocess.run(
            " ".join(args),
            shell=True,
            capture_output=True,
            text=True,
            cwd=core_dir
        )
        
        # Capture output
        if result.stdout:
            log_lines.append(result.stdout)
        if result.stderr:
            log_lines.append(result.stderr)
        
        log_lines.append(f"-" * 50)
        
        if result.returncode == 0:
            log_lines.append(f"BUILD SUCCEEDED")
            log_lines.append(f"Output: {output_dll}")
            print(f"[+] Build succeeded: {output_dll}")
            success = True
        else:
            log_lines.append(f"BUILD FAILED (exit code: {result.returncode})")
            print(f"[!] Build failed. Check build_log.txt for details.")
            success = False
            
    except Exception as e:
        error_msg = f"ERROR: Build exception: {str(e)}"
        log_lines.append(error_msg)
        print(f"[!] {error_msg}")
        success = False
    
    # Clean up response file
    if os.path.exists(response_file):
        os.remove(response_file)
    
    return success


# ---------- Entry point ----------

def main():
    import sys
    
    # Check if a dump.cs path was provided as argument
    if len(sys.argv) > 1:
        dump_path = os.path.abspath(sys.argv[1])
        # Use the directory containing dump.cs as the working directory
        dump_dir = os.path.dirname(dump_path)
    else:
        # Try to find dump.cs in:
        # 1. Current working directory
        # 2. Script's directory
        cwd = os.getcwd()
        cwd_dump = os.path.join(cwd, "dump.cs")
        script_dir = os.path.dirname(os.path.abspath(__file__))
        script_dump = os.path.join(script_dir, "dump.cs")
        
        if os.path.exists(cwd_dump):
            dump_dir = cwd
            dump_path = cwd_dump
        elif os.path.exists(script_dump):
            dump_dir = script_dir
            dump_path = script_dump
        else:
            print(f"[ERROR] Could not find dump.cs in:")
            print(f"  - Current directory: {cwd}")
            print(f"  - Script directory: {script_dir}")
            print("Make sure dump.cs is in the Dump folder (GameDir/MDB/Dump/dump.cs)")
            return
    
    # Output to MDB_Core/Generated in the dump directory
    output_dir = os.path.join(dump_dir, "MDB_Core", "Generated")
    # Build log file in the dump directory
    log_file_path = os.path.join(dump_dir, "build_log.txt")

    if not os.path.exists(dump_path):
        print(f"[ERROR] Could not find: {dump_path}")
        print("Make sure dump.cs is in the Dump folder (GameDir/MDB/Dump/dump.cs)")
        return
    
    # Create output directory if it doesn't exist
    os.makedirs(output_dir, exist_ok=True)

    print(f"[+] Parsing: {dump_path}")

    types = parse_dump_file(dump_path)
    print(f"[+] Parsed {len(types)} types")

    # Build type registry for namespace resolution
    build_type_registry(types)

    # Filter out types with no methods/properties/fields or invalid names
    valid_types = [t for t in types if t.name and (t.methods or t.properties or t.fields)]
    print(f"[+] Valid types with methods/properties/fields: {len(valid_types)}")

    # Generate wrapper code per namespace
    print(f"[+] Generating wrapper code...")
    generated_files = generate_wrapper_code_per_namespace(valid_types, output_dir)

    print(f"[+] Generated {len(generated_files)} namespace files:")
    for ns, filepath in sorted(generated_files.items()):
        print(f"    - {os.path.basename(filepath)}")

    print(f"[+] Output directory: {output_dir}")
    
    # Build the project
    print(f"")
    print(f"[+] Building GameSDK.ModHost.dll...")
    build_project(dump_dir, log_file_path)
    print(f"[+] Build log saved to: {log_file_path}")


if __name__ == "__main__":
    main()
