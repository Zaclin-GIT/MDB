---
layout: default
title: IL2CPP Bridge API
---

# IL2CPP Bridge API

The `Il2CppBridge` provides low-level direct access to the IL2CPP runtime through native P/Invoke calls. It enables method invocation, field access, object creation, runtime reflection, and method hooking at the IL2CPP layer.

**Namespace:** `GameSDK`

---

## Introduction

The IL2CPP Bridge is the foundation layer of the MDB Framework, providing direct access to Unity's IL2CPP runtime. It wraps the native `MDB_Bridge.dll` which interfaces with `GameAssembly.dll` and IL2CPP APIs.

**Key capabilities:**
- Find and invoke IL2CPP methods
- Read and write object fields
- Create IL2CPP objects and strings
- Enumerate class members at runtime
- Hook methods using MinHook
- Access obfuscated methods by RVA
- Work with Unity GameObjects, Transforms, and Scenes

**When to use IL2CPP Bridge:**
- Direct IL2CPP runtime manipulation
- Dynamic method resolution and invocation
- Working with obfuscated code
- Building tools and inspectors
- Advanced patching scenarios

**When to use higher-level APIs:**
- Use `[Patch]` attributes for declarative patching
- Use `HookManager` for managed hook lifecycle
- Use Unity interop helpers for GameObject/Component access

---

## Overview

### Architecture

```
Your Mod (C#)
    ↓
Il2CppBridge (Managed P/Invoke)
    ↓
MDB_Bridge.dll (Native C++)
    ↓
GameAssembly.dll (IL2CPP Runtime)
```

### Error Handling

Most bridge functions return `IntPtr.Zero`, `null`, or negative values on error. Always check return values and use error helpers:

```csharp
IntPtr klass = Il2CppBridge.mdb_find_class("Assembly-CSharp", "Game", "Player");
if (klass == IntPtr.Zero)
{
    string error = Il2CppBridge.GetLastError();
    MdbErrorCode code = Il2CppBridge.GetLastErrorCode();
    Logger.Error($"Failed to find class: {error} (code: {code})");
    return;
}
```

---

## Initialization Functions

### mdb_init

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern int mdb_init();
```

Initialize the IL2CPP bridge. Must be called before any other bridge functions.

**Returns:** `0` on success, non-zero error code on failure.

**Note:** The MDB Framework calls this automatically during initialization.

---

### mdb_domain_get

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern IntPtr mdb_domain_get();
```

Get the IL2CPP domain pointer.

**Returns:** Pointer to the IL2CPP domain, or `IntPtr.Zero` on failure.

**Example:**
```csharp
IntPtr domain = Il2CppBridge.mdb_domain_get();
if (domain == IntPtr.Zero)
{
    Logger.Error("Failed to get IL2CPP domain");
}
```

---

### mdb_thread_attach

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern IntPtr mdb_thread_attach(IntPtr domain);
```

Attach the current thread to the IL2CPP domain. Required for invoking IL2CPP methods from new threads.

**Parameters:**
- `domain` - The IL2CPP domain pointer from `mdb_domain_get()`

**Returns:** Thread handle, or `IntPtr.Zero` on failure.

**Example:**
```csharp
// When creating a new thread that needs IL2CPP access
var thread = new Thread(() =>
{
    IntPtr domain = Il2CppBridge.mdb_domain_get();
    IntPtr threadHandle = Il2CppBridge.mdb_thread_attach(domain);
    
    if (threadHandle != IntPtr.Zero)
    {
        // Now you can safely call IL2CPP methods
    }
});
```

---

## Class Resolution Functions

### mdb_find_class

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
public static extern IntPtr mdb_find_class(string assembly, string ns, string name);
```

Find an IL2CPP class by assembly, namespace, and name.

**Parameters:**
- `assembly` - Assembly name (e.g., `"Assembly-CSharp"`, `"UnityEngine.CoreModule"`)
- `ns` - Namespace (e.g., `"UnityEngine"`, can be `""` or `null` for no namespace)
- `name` - Class name (e.g., `"GameObject"`, `"Player"`)

**Returns:** Pointer to `Il2CppClass`, or `IntPtr.Zero` if not found.

**Example:**
```csharp
// Find a game class
IntPtr playerClass = Il2CppBridge.mdb_find_class("Assembly-CSharp", "Game", "Player");

// Find a Unity class
IntPtr gameObjectClass = Il2CppBridge.mdb_find_class("UnityEngine.CoreModule", "UnityEngine", "GameObject");

// Class with no namespace
IntPtr globalClass = Il2CppBridge.mdb_find_class("Assembly-CSharp", "", "GlobalManager");
```

---

### mdb_get_class_size

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern int mdb_get_class_size(IntPtr klass);
```

Get the instance size of a class in bytes.

**Parameters:**
- `klass` - Pointer to `Il2CppClass`

**Returns:** Instance size in bytes, or `-1` on error.

**Example:**
```csharp
IntPtr klass = Il2CppBridge.mdb_find_class("Assembly-CSharp", "Game", "Player");
int size = Il2CppBridge.mdb_get_class_size(klass);
Logger.Info($"Player instance size: {size} bytes");
```

---

### mdb_class_get_name

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern IntPtr mdb_class_get_name(IntPtr klass);
```

Get the name of a class.

**Parameters:**
- `klass` - Pointer to `Il2CppClass`

**Returns:** Pointer to class name string (marshal with `PtrToStringAnsi`).

**Helper Method:**
```csharp
public static string GetClassName(IntPtr klass)
```

Get the name of a class as a managed string.

**Example:**
```csharp
IntPtr klass = Il2CppBridge.mdb_find_class("Assembly-CSharp", "Game", "Player");
string name = Il2CppBridge.GetClassName(klass); // Helper method
Logger.Info($"Class name: {name}");

// Or manually:
IntPtr namePtr = Il2CppBridge.mdb_class_get_name(klass);
string nameManual = Marshal.PtrToStringAnsi(namePtr);
```

---

### mdb_class_get_namespace

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern IntPtr mdb_class_get_namespace(IntPtr klass);
```

Get the namespace of a class.

**Parameters:**
- `klass` - Pointer to `Il2CppClass`

**Returns:** Pointer to namespace string, or `IntPtr.Zero` if no namespace.

**Helper Method:**
```csharp
public static string GetClassFullName(IntPtr klass)
```

Get the full name (namespace.classname) of a class.

**Example:**
```csharp
IntPtr klass = Il2CppBridge.mdb_find_class("UnityEngine.CoreModule", "UnityEngine", "GameObject");
string fullName = Il2CppBridge.GetClassFullName(klass);
Logger.Info($"Full name: {fullName}"); // "UnityEngine.GameObject"
```

---

### mdb_class_get_parent

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern IntPtr mdb_class_get_parent(IntPtr klass);
```

Get the parent class of a class.

**Parameters:**
- `klass` - Pointer to `Il2CppClass`

**Returns:** Pointer to parent `Il2CppClass`, or `IntPtr.Zero` if no parent.

**Example:**
```csharp
IntPtr klass = Il2CppBridge.mdb_find_class("Assembly-CSharp", "Game", "EnemyZombie");
IntPtr parent = Il2CppBridge.mdb_class_get_parent(klass);
string parentName = Il2CppBridge.GetClassName(parent); // "Enemy"
```

---

### mdb_class_get_type

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern IntPtr mdb_class_get_type(IntPtr klass);
```

Get the `Il2CppType*` from a class.

**Parameters:**
- `klass` - Pointer to `Il2CppClass`

**Returns:** Pointer to `Il2CppType`.

---

### mdb_class_is_valuetype

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern int mdb_class_is_valuetype(IntPtr klass);
```

Check if a class is a value type (struct).

**Parameters:**
- `klass` - Pointer to `Il2CppClass`

**Returns:** `1` if value type, `0` if reference type, `-1` on error.

**Example:**
```csharp
IntPtr vector3 = Il2CppBridge.mdb_find_class("UnityEngine.CoreModule", "UnityEngine", "Vector3");
bool isStruct = Il2CppBridge.mdb_class_is_valuetype(vector3) == 1; // true
```

---

## Method Functions

### mdb_get_method

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
public static extern IntPtr mdb_get_method(IntPtr klass, string name, int paramCount);
```

Get a method from a class by name and parameter count.

**Parameters:**
- `klass` - Pointer to `Il2CppClass`
- `name` - Method name
- `paramCount` - Number of parameters (`-1` to find first match regardless of parameter count)

**Returns:** Pointer to `MethodInfo`, or `IntPtr.Zero` if not found.

**Example:**
```csharp
IntPtr klass = Il2CppBridge.mdb_find_class("Assembly-CSharp", "Game", "Player");

// Get specific method with 1 parameter
IntPtr takeDamage = Il2CppBridge.mdb_get_method(klass, "TakeDamage", 1);

// Get method with any parameter count
IntPtr update = Il2CppBridge.mdb_get_method(klass, "Update", -1);
```

---

### mdb_get_method_pointer

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern IntPtr mdb_get_method_pointer(IntPtr method);
```

Get the raw function pointer for a method. Used for hooking.

**Parameters:**
- `method` - Pointer to `MethodInfo`

**Returns:** Function pointer, or `IntPtr.Zero` if not available.

**Example:**
```csharp
IntPtr method = Il2CppBridge.mdb_get_method(klass, "TakeDamage", 1);
IntPtr methodPtr = Il2CppBridge.mdb_get_method_pointer(method);

// Use with HookManager
HookInfo hook = HookManager.CreateHook(methodPtr, MyDetour, out IntPtr original);
```

---

### mdb_invoke_method

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern IntPtr mdb_invoke_method(IntPtr method, IntPtr instance, IntPtr[] args, out IntPtr exception);
```

Invoke a method on an instance.

**Parameters:**
- `method` - Pointer to `MethodInfo`
- `instance` - Pointer to object instance (`IntPtr.Zero` for static methods)
- `args` - Array of argument pointers (each element is a pointer to the argument value)
- `exception` - Output: exception pointer if thrown

**Returns:** Return value pointer.

**Example:**
```csharp
// Call static method: Debug.Log("Hello")
IntPtr debugClass = Il2CppBridge.mdb_find_class("UnityEngine.CoreModule", "UnityEngine", "Debug");
IntPtr logMethod = Il2CppBridge.mdb_get_method(debugClass, "Log", 1);
IntPtr messageStr = Il2CppBridge.mdb_string_new("Hello from IL2CPP Bridge!");

IntPtr[] args = new IntPtr[] { messageStr };
IntPtr result = Il2CppBridge.mdb_invoke_method(logMethod, IntPtr.Zero, args, out IntPtr ex);

if (ex != IntPtr.Zero)
{
    Logger.Error("Exception occurred during method invocation");
}
```

---

### mdb_get_method_info

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern int mdb_get_method_info(IntPtr method, out int paramCount, 
    [MarshalAs(UnmanagedType.I1)] out bool isStatic, 
    [MarshalAs(UnmanagedType.I1)] out bool hasReturn);
```

Get information about a method.

**Parameters:**
- `method` - Pointer to `MethodInfo`
- `paramCount` - Output: number of parameters
- `isStatic` - Output: true if static method
- `hasReturn` - Output: true if method has return value

**Returns:** `0` on success, non-zero on failure.

**Example:**
```csharp
IntPtr method = Il2CppBridge.mdb_get_method(klass, "TakeDamage", -1);
int result = Il2CppBridge.mdb_get_method_info(method, out int paramCount, out bool isStatic, out bool hasReturn);

if (result == 0)
{
    Logger.Info($"Method has {paramCount} parameters, static={isStatic}, returns={hasReturn}");
}
```

---

### mdb_method_get_name

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
private static extern IntPtr mdb_method_get_name(IntPtr method);
```

Get the name of a method.

**Helper Method:**
```csharp
public static string GetMethodName(IntPtr method)
```

Get the name of a method as a managed string.

**Example:**
```csharp
IntPtr method = Il2CppBridge.mdb_get_method(klass, "TakeDamage", 1);
string name = Il2CppBridge.GetMethodName(method); // "TakeDamage"
```

---

### mdb_method_get_param_type

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern IntPtr mdb_method_get_param_type(IntPtr method, int index);
```

Get the parameter type for a method at a specific index.

**Parameters:**
- `method` - Pointer to `MethodInfo`
- `index` - Parameter index (0-based)

**Returns:** Pointer to `Il2CppType`, or `IntPtr.Zero` if invalid.

**Example:**
```csharp
IntPtr method = Il2CppBridge.mdb_get_method(klass, "TakeDamage", 2);
IntPtr param0Type = Il2CppBridge.mdb_method_get_param_type(method, 0);
string typeName = Il2CppBridge.GetTypeName(param0Type);
Logger.Info($"First parameter type: {typeName}");
```

---

### mdb_method_get_return_type

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern IntPtr mdb_method_get_return_type(IntPtr method);
```

Get the return type for a method.

**Parameters:**
- `method` - Pointer to `MethodInfo`

**Returns:** Pointer to `Il2CppType`, or `IntPtr.Zero` if void.

---

### mdb_method_get_param_count

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern int mdb_method_get_param_count(IntPtr method);
```

Get the number of parameters in a method.

**Parameters:**
- `method` - Pointer to `MethodInfo`

**Returns:** Parameter count, or `-1` on error.

---

### mdb_method_get_flags

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern int mdb_method_get_flags(IntPtr method);
```

Get the flags for a method (access modifiers, virtual, abstract, etc.).

**Parameters:**
- `method` - Pointer to `MethodInfo`

**Returns:** Method flags bitmask.

---

## Field Functions

### mdb_get_field

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
public static extern IntPtr mdb_get_field(IntPtr klass, string name);
```

Get a field from a class by name.

**Parameters:**
- `klass` - Pointer to `Il2CppClass`
- `name` - Field name

**Returns:** Pointer to `FieldInfo`, or `IntPtr.Zero` if not found.

**Example:**
```csharp
IntPtr playerClass = Il2CppBridge.mdb_find_class("Assembly-CSharp", "Game", "Player");
IntPtr healthField = Il2CppBridge.mdb_get_field(playerClass, "health");
```

---

### mdb_get_field_offset

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern int mdb_get_field_offset(IntPtr field);
```

Get the byte offset of a field within its containing type.

**Parameters:**
- `field` - Pointer to `FieldInfo`

**Returns:** Field offset in bytes, or `-1` on error.

**Example:**
```csharp
IntPtr field = Il2CppBridge.mdb_get_field(playerClass, "health");
int offset = Il2CppBridge.mdb_get_field_offset(field);
Logger.Info($"Health field is at offset {offset} bytes");
```

---

### mdb_field_get_value

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern void mdb_field_get_value(IntPtr instance, IntPtr field, IntPtr outValue);
```

Get the value of an instance field.

**Parameters:**
- `instance` - Pointer to object instance
- `field` - Pointer to `FieldInfo`
- `outValue` - Pointer to buffer to receive the value

**Example:**
```csharp
// Get int field value
IntPtr playerInstance = /* ... */;
IntPtr healthField = Il2CppBridge.mdb_get_field(playerClass, "health");

int health = 0;
IntPtr healthPtr = Marshal.AllocHGlobal(sizeof(int));
Il2CppBridge.mdb_field_get_value(playerInstance, healthField, healthPtr);
health = Marshal.ReadInt32(healthPtr);
Marshal.FreeHGlobal(healthPtr);

Logger.Info($"Player health: {health}");
```

---

### mdb_field_set_value

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern void mdb_field_set_value(IntPtr instance, IntPtr field, IntPtr value);
```

Set the value of an instance field.

**Parameters:**
- `instance` - Pointer to object instance
- `field` - Pointer to `FieldInfo`
- `value` - Pointer to the new value

**Example:**
```csharp
// Set int field value
IntPtr playerInstance = /* ... */;
IntPtr healthField = Il2CppBridge.mdb_get_field(playerClass, "health");

int newHealth = 100;
IntPtr healthPtr = Marshal.AllocHGlobal(sizeof(int));
Marshal.WriteInt32(healthPtr, newHealth);
Il2CppBridge.mdb_field_set_value(playerInstance, healthField, healthPtr);
Marshal.FreeHGlobal(healthPtr);

Logger.Info("Set player health to 100");
```

---

### mdb_field_static_get_value

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern void mdb_field_static_get_value(IntPtr field, IntPtr outValue);
```

Get the value of a static field.

**Parameters:**
- `field` - Pointer to `FieldInfo`
- `outValue` - Pointer to buffer to receive the value

**Example:**
```csharp
IntPtr gameManagerClass = Il2CppBridge.mdb_find_class("Assembly-CSharp", "Game", "GameManager");
IntPtr instanceField = Il2CppBridge.mdb_get_field(gameManagerClass, "Instance");

IntPtr instancePtr = Marshal.AllocHGlobal(IntPtr.Size);
Il2CppBridge.mdb_field_static_get_value(instanceField, instancePtr);
IntPtr instance = Marshal.ReadIntPtr(instancePtr);
Marshal.FreeHGlobal(instancePtr);
```

---

### mdb_field_static_set_value

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern void mdb_field_static_set_value(IntPtr field, IntPtr value);
```

Set the value of a static field.

**Parameters:**
- `field` - Pointer to `FieldInfo`
- `value` - Pointer to the new value

---

### mdb_field_get_name

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
private static extern IntPtr mdb_field_get_name(IntPtr field);
```

Get the name of a field.

**Helper Method:**
```csharp
public static string GetFieldName(IntPtr field)
```

Get the name of a field as a managed string.

**Example:**
```csharp
IntPtr field = Il2CppBridge.mdb_get_field(playerClass, "health");
string name = Il2CppBridge.GetFieldName(field); // "health"
```

---

### mdb_field_get_type

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern IntPtr mdb_field_get_type(IntPtr field);
```

Get the type of a field.

**Parameters:**
- `field` - Pointer to `FieldInfo`

**Returns:** Pointer to `Il2CppType`.

---

### mdb_field_is_static

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
[return: MarshalAs(UnmanagedType.I1)]
public static extern bool mdb_field_is_static(IntPtr field);
```

Check if a field is static.

**Parameters:**
- `field` - Pointer to `FieldInfo`

**Returns:** `true` if static, `false` if instance.

---

## String Functions

### mdb_string_new

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
public static extern IntPtr mdb_string_new(string str);
```

Create a new IL2CPP string from a UTF-8 managed string.

**Parameters:**
- `str` - Managed C# string

**Returns:** Pointer to `Il2CppString`, or `IntPtr.Zero` on failure.

**Example:**
```csharp
IntPtr il2cppStr = Il2CppBridge.mdb_string_new("Hello, IL2CPP!");

// Use in method call
IntPtr[] args = new IntPtr[] { il2cppStr };
Il2CppBridge.mdb_invoke_method(logMethod, IntPtr.Zero, args, out IntPtr ex);
```

---

### mdb_string_to_utf8

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern int mdb_string_to_utf8(IntPtr str, StringBuilder buffer, int bufferSize);
```

Convert an IL2CPP string to UTF-8.

**Parameters:**
- `str` - Pointer to `Il2CppString`
- `buffer` - StringBuilder to receive the UTF-8 string
- `bufferSize` - Size of the buffer

**Returns:** Number of bytes written, or `-1` on error.

**Example:**
```csharp
IntPtr il2cppStr = /* ... */;
StringBuilder buffer = new StringBuilder(256);
int length = Il2CppBridge.mdb_string_to_utf8(il2cppStr, buffer, buffer.Capacity);

if (length > 0)
{
    string result = buffer.ToString(0, length);
    Logger.Info($"String value: {result}");
}
```

---

### Il2CppStringToManaged

```csharp
public static string Il2CppStringToManaged(IntPtr il2cppString)
```

Convert an IL2CPP string to a managed C# string (helper method).

**Parameters:**
- `il2cppString` - Pointer to `Il2CppString`

**Returns:** Managed string, or `null` if conversion failed.

**Example:**
```csharp
// Get name field (which is a string)
IntPtr nameField = Il2CppBridge.mdb_get_field(playerClass, "playerName");
IntPtr nameStrPtr = Marshal.AllocHGlobal(IntPtr.Size);
Il2CppBridge.mdb_field_get_value(playerInstance, nameField, nameStrPtr);
IntPtr il2cppStr = Marshal.ReadIntPtr(nameStrPtr);
Marshal.FreeHGlobal(nameStrPtr);

string name = Il2CppBridge.Il2CppStringToManaged(il2cppStr);
Logger.Info($"Player name: {name}");
```

---

### ManagedStringToIl2Cpp

```csharp
public static IntPtr ManagedStringToIl2Cpp(string managedString)
```

Convert a managed C# string to an IL2CPP string (helper method).

**Parameters:**
- `managedString` - Managed C# string

**Returns:** Pointer to `Il2CppString`, or `IntPtr.Zero` if null input.

**Example:**
```csharp
// Same as mdb_string_new, but handles null gracefully
string message = "Hello!";
IntPtr il2cppStr = Il2CppBridge.ManagedStringToIl2Cpp(message);
```

---

## Array Functions

### mdb_array_length

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern int mdb_array_length(IntPtr array);
```

Get the length of an IL2CPP array.

**Parameters:**
- `array` - Pointer to IL2CPP array

**Returns:** Length of the array, or `-1` if null.

**Example:**
```csharp
IntPtr itemsArray = /* ... */;
int length = Il2CppBridge.mdb_array_length(itemsArray);
Logger.Info($"Array has {length} elements");
```

---

### mdb_array_get_element

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern IntPtr mdb_array_get_element(IntPtr array, int index);
```

Get an element from an IL2CPP array.

**Parameters:**
- `array` - Pointer to IL2CPP array
- `index` - Element index (0-based)

**Returns:** Pointer to the element, or `IntPtr.Zero` if out of bounds.

**Example:**
```csharp
IntPtr itemsArray = /* ... */;
int length = Il2CppBridge.mdb_array_length(itemsArray);

for (int i = 0; i < length; i++)
{
    IntPtr element = Il2CppBridge.mdb_array_get_element(itemsArray, i);
    // Process element...
}
```

---

### mdb_array_get_element_class

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern IntPtr mdb_array_get_element_class(IntPtr array);
```

Get the element class of an IL2CPP array.

**Parameters:**
- `array` - Pointer to IL2CPP array

**Returns:** Pointer to the element's `Il2CppClass`, or `IntPtr.Zero` on error.

**Example:**
```csharp
IntPtr itemsArray = /* ... */;
IntPtr elementClass = Il2CppBridge.mdb_array_get_element_class(itemsArray);
string elementType = Il2CppBridge.GetClassName(elementClass);
Logger.Info($"Array contains elements of type: {elementType}");
```

---

## Object Creation Functions

### mdb_object_new

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern IntPtr mdb_object_new(IntPtr klass);
```

Allocate a new IL2CPP object (does not call constructor).

**Parameters:**
- `klass` - Pointer to `Il2CppClass`

**Returns:** Pointer to new object, or `IntPtr.Zero` on failure.

**Example:**
```csharp
IntPtr playerClass = Il2CppBridge.mdb_find_class("Assembly-CSharp", "Game", "Player");
IntPtr newPlayer = Il2CppBridge.mdb_object_new(playerClass);

// Call constructor manually if needed
IntPtr ctor = Il2CppBridge.mdb_get_method(playerClass, ".ctor", 0);
Il2CppBridge.mdb_invoke_method(ctor, newPlayer, null, out IntPtr ex);
```

---

### mdb_object_get_class

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern IntPtr mdb_object_get_class(IntPtr instance);
```

Get the class of an object instance.

**Parameters:**
- `instance` - Pointer to object instance

**Returns:** Pointer to `Il2CppClass`.

**Example:**
```csharp
IntPtr obj = /* ... */;
IntPtr klass = Il2CppBridge.mdb_object_get_class(obj);
string className = Il2CppBridge.GetClassName(klass);
Logger.Info($"Object is of type: {className}");
```

---

## Unity-Specific Helper Functions

### GameObject Functions

#### mdb_gameobject_get_components

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern IntPtr mdb_gameobject_get_components(IntPtr gameObject);
```

Get all components on a GameObject.

**Parameters:**
- `gameObject` - Pointer to GameObject IL2CPP object

**Returns:** Pointer to `Component[]` array, or `IntPtr.Zero` on failure.

---

#### mdb_components_array_length

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern int mdb_components_array_length(IntPtr componentsArray);
```

Get the number of components in the array returned by `mdb_gameobject_get_components`.

**Parameters:**
- `componentsArray` - Pointer to `Component[]`

**Returns:** Number of components, or `0` on error.

---

#### mdb_components_array_get

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern IntPtr mdb_components_array_get(IntPtr componentsArray, int index);
```

Get a component from the array at the specified index.

**Parameters:**
- `componentsArray` - Pointer to `Component[]`
- `index` - Component index

**Returns:** Pointer to Component, or `IntPtr.Zero` on error.

**Example:**
```csharp
IntPtr gameObject = /* ... */;
IntPtr components = Il2CppBridge.mdb_gameobject_get_components(gameObject);
int count = Il2CppBridge.mdb_components_array_length(components);

Logger.Info($"GameObject has {count} components:");
for (int i = 0; i < count; i++)
{
    IntPtr component = Il2CppBridge.mdb_components_array_get(components, i);
    IntPtr componentClass = Il2CppBridge.mdb_object_get_class(component);
    string componentType = Il2CppBridge.GetClassName(componentClass);
    Logger.Info($"  - {componentType}");
}
```

---

#### mdb_gameobject_set_active

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
[return: MarshalAs(UnmanagedType.I1)]
public static extern bool mdb_gameobject_set_active(IntPtr gameObject, [MarshalAs(UnmanagedType.I1)] bool active);
```

Set the active state of a GameObject.

**Parameters:**
- `gameObject` - Pointer to GameObject IL2CPP object
- `active` - `true` to activate, `false` to deactivate

**Returns:** `true` on success, `false` on failure.

**Example:**
```csharp
IntPtr gameObject = /* ... */;
bool success = Il2CppBridge.mdb_gameobject_set_active(gameObject, false);
Logger.Info(success ? "GameObject deactivated" : "Failed to deactivate");
```

---

#### mdb_gameobject_get_active_self

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
[return: MarshalAs(UnmanagedType.I1)]
public static extern bool mdb_gameobject_get_active_self(IntPtr gameObject);
```

Get the activeSelf state of a GameObject.

**Parameters:**
- `gameObject` - Pointer to GameObject IL2CPP object

**Returns:** `true` if active, `false` if inactive or on error.

---

#### mdb_gameobject_get_scene_handle

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern int mdb_gameobject_get_scene_handle(IntPtr gameObject);
```

Get the scene handle that a GameObject belongs to.

**Parameters:**
- `gameObject` - Pointer to GameObject IL2CPP object

**Returns:** Scene handle, or `0` on error.

**Use case:** Identify DontDestroyOnLoad objects (their scene handle won't match any loaded scene).

---

### Transform Functions

#### mdb_transform_get_child_count

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern int mdb_transform_get_child_count(IntPtr transform);
```

Get the child count of a Transform.

**Parameters:**
- `transform` - Pointer to Transform IL2CPP object

**Returns:** Number of children, or `0` on error.

---

#### mdb_transform_get_child

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern IntPtr mdb_transform_get_child(IntPtr transform, int index);
```

Get a child Transform at the specified index.

**Parameters:**
- `transform` - Pointer to Transform IL2CPP object
- `index` - Child index (0-based)

**Returns:** Pointer to child Transform, or `IntPtr.Zero` on error.

**Example:**
```csharp
IntPtr transform = /* ... */;
int childCount = Il2CppBridge.mdb_transform_get_child_count(transform);

for (int i = 0; i < childCount; i++)
{
    IntPtr child = Il2CppBridge.mdb_transform_get_child(transform, i);
    // Process child transform...
}
```

---

#### mdb_transform_get_local_position

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
[return: MarshalAs(UnmanagedType.I1)]
public static extern bool mdb_transform_get_local_position(IntPtr transform, out float x, out float y, out float z);
```

Get the local position of a Transform.

**Parameters:**
- `transform` - Pointer to Transform IL2CPP object
- `x` - Output: X coordinate
- `y` - Output: Y coordinate
- `z` - Output: Z coordinate

**Returns:** `true` on success, `false` on failure.

**Example:**
```csharp
IntPtr transform = /* ... */;
if (Il2CppBridge.mdb_transform_get_local_position(transform, out float x, out float y, out float z))
{
    Logger.Info($"Local position: ({x}, {y}, {z})");
}
```

---

#### mdb_transform_set_local_position

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
[return: MarshalAs(UnmanagedType.I1)]
public static extern bool mdb_transform_set_local_position(IntPtr transform, float x, float y, float z);
```

Set the local position of a Transform.

**Parameters:**
- `transform` - Pointer to Transform IL2CPP object
- `x` - X coordinate
- `y` - Y coordinate
- `z` - Z coordinate

**Returns:** `true` on success, `false` on failure.

**Example:**
```csharp
IntPtr transform = /* ... */;
bool success = Il2CppBridge.mdb_transform_set_local_position(transform, 10.0f, 5.0f, 0.0f);
```

---

#### mdb_transform_get_local_euler_angles

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
[return: MarshalAs(UnmanagedType.I1)]
public static extern bool mdb_transform_get_local_euler_angles(IntPtr transform, out float x, out float y, out float z);
```

Get the local euler angles of a Transform.

---

#### mdb_transform_set_local_euler_angles

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
[return: MarshalAs(UnmanagedType.I1)]
public static extern bool mdb_transform_set_local_euler_angles(IntPtr transform, float x, float y, float z);
```

Set the local euler angles of a Transform.

---

#### mdb_transform_get_local_scale

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
[return: MarshalAs(UnmanagedType.I1)]
public static extern bool mdb_transform_get_local_scale(IntPtr transform, out float x, out float y, out float z);
```

Get the local scale of a Transform.

---

#### mdb_transform_set_local_scale

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
[return: MarshalAs(UnmanagedType.I1)]
public static extern bool mdb_transform_set_local_scale(IntPtr transform, float x, float y, float z);
```

Set the local scale of a Transform.

---

### SceneManager Functions

#### mdb_scenemanager_get_scene_count

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern int mdb_scenemanager_get_scene_count();
```

Get the number of loaded scenes.

**Returns:** Number of loaded scenes, or `0` on error.

---

#### mdb_scenemanager_get_scene_name

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern int mdb_scenemanager_get_scene_name(int sceneIndex, [Out] byte[] buffer, int bufferSize);
```

Get the name of a scene at the specified index.

**Parameters:**
- `sceneIndex` - Index of the scene (0 to sceneCount-1)
- `buffer` - Buffer to write the scene name to
- `bufferSize` - Size of the buffer

**Returns:** Length of the name written, or `0` on error.

**Helper Method:**
```csharp
public static string GetSceneName(int sceneIndex)
```

Get the name of a scene as a managed string.

**Example:**
```csharp
int sceneCount = Il2CppBridge.mdb_scenemanager_get_scene_count();
Logger.Info($"Loaded scenes: {sceneCount}");

for (int i = 0; i < sceneCount; i++)
{
    string sceneName = Il2CppBridge.GetSceneName(i);
    Logger.Info($"  Scene {i}: {sceneName}");
}
```

---

#### mdb_scenemanager_get_scene_handle

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern int mdb_scenemanager_get_scene_handle(int sceneIndex);
```

Get the handle of a scene at the specified index.

**Parameters:**
- `sceneIndex` - Index of the scene (0 to sceneCount-1)

**Returns:** Scene handle, or `-1` on error.

---

#### mdb_scenemanager_get_scene_root_count

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern int mdb_scenemanager_get_scene_root_count(int sceneIndex);
```

Get the root GameObject count of a scene.

**Parameters:**
- `sceneIndex` - Index of the scene (0 to sceneCount-1)

**Returns:** Number of root GameObjects in the scene, or `0` on error.

---

#### mdb_get_dontdestroyonload_scene_handle

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern int mdb_get_dontdestroyonload_scene_handle();
```

Get the DontDestroyOnLoad scene handle.

**Returns:** Scene handle for DDOL scene, or `-1` if not found.

---

## Enumeration Functions

### mdb_class_get_field_count

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern int mdb_class_get_field_count(IntPtr klass);
```

Get the number of fields in a class.

**Parameters:**
- `klass` - Pointer to `Il2CppClass`

**Returns:** Field count, or `0` on error.

---

### mdb_class_get_field_by_index

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern IntPtr mdb_class_get_field_by_index(IntPtr klass, int index);
```

Get a field by index.

**Parameters:**
- `klass` - Pointer to `Il2CppClass`
- `index` - Field index (0-based)

**Returns:** Pointer to `FieldInfo`, or `IntPtr.Zero` on error.

**Example:**
```csharp
IntPtr klass = Il2CppBridge.mdb_find_class("Assembly-CSharp", "Game", "Player");
int fieldCount = Il2CppBridge.mdb_class_get_field_count(klass);

Logger.Info($"Player class has {fieldCount} fields:");
for (int i = 0; i < fieldCount; i++)
{
    IntPtr field = Il2CppBridge.mdb_class_get_field_by_index(klass, i);
    string fieldName = Il2CppBridge.GetFieldName(field);
    bool isStatic = Il2CppBridge.mdb_field_is_static(field);
    IntPtr fieldType = Il2CppBridge.mdb_field_get_type(field);
    string typeName = Il2CppBridge.GetTypeName(fieldType);
    
    Logger.Info($"  {(isStatic ? "static" : "instance")} {typeName} {fieldName}");
}
```

---

### mdb_class_get_method_count

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern int mdb_class_get_method_count(IntPtr klass);
```

Get the number of methods in a class.

**Parameters:**
- `klass` - Pointer to `Il2CppClass`

**Returns:** Method count, or `0` on error.

---

### mdb_class_get_method_by_index

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern IntPtr mdb_class_get_method_by_index(IntPtr klass, int index);
```

Get a method by index.

**Parameters:**
- `klass` - Pointer to `Il2CppClass`
- `index` - Method index (0-based)

**Returns:** Pointer to `MethodInfo`, or `IntPtr.Zero` on error.

**Example:**
```csharp
IntPtr klass = Il2CppBridge.mdb_find_class("Assembly-CSharp", "Game", "Player");
int methodCount = Il2CppBridge.mdb_class_get_method_count(klass);

Logger.Info($"Player class has {methodCount} methods:");
for (int i = 0; i < methodCount; i++)
{
    IntPtr method = Il2CppBridge.mdb_class_get_method_by_index(klass, i);
    string methodName = Il2CppBridge.GetMethodName(method);
    int paramCount = Il2CppBridge.mdb_method_get_param_count(method);
    
    Logger.Info($"  {methodName}({paramCount} params)");
}
```

---

### mdb_class_get_property_count

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern int mdb_class_get_property_count(IntPtr klass);
```

Get the number of properties in a class.

**Parameters:**
- `klass` - Pointer to `Il2CppClass`

**Returns:** Property count, or `0` on error.

---

### mdb_class_get_property_by_index

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern IntPtr mdb_class_get_property_by_index(IntPtr klass, int index);
```

Get a property by index.

**Parameters:**
- `klass` - Pointer to `Il2CppClass`
- `index` - Property index (0-based)

**Returns:** Pointer to `PropertyInfo`, or `IntPtr.Zero` on error.

---

### mdb_property_get_name

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
private static extern IntPtr mdb_property_get_name(IntPtr prop);
```

Get the name of a property.

**Helper Method:**
```csharp
public static string GetPropertyName(IntPtr prop)
```

**Example:**
```csharp
IntPtr prop = Il2CppBridge.mdb_class_get_property_by_index(klass, 0);
string propName = Il2CppBridge.GetPropertyName(prop);
```

---

### mdb_property_get_get_method

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern IntPtr mdb_property_get_get_method(IntPtr prop);
```

Get the getter method of a property.

**Parameters:**
- `prop` - Pointer to `PropertyInfo`

**Returns:** Pointer to getter `MethodInfo`, or `IntPtr.Zero` if no getter.

---

### mdb_property_get_set_method

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern IntPtr mdb_property_get_set_method(IntPtr prop);
```

Get the setter method of a property.

**Parameters:**
- `prop` - Pointer to `PropertyInfo`

**Returns:** Pointer to setter `MethodInfo`, or `IntPtr.Zero` if no setter.

---

## Hook Functions

### mdb_create_hook

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern long mdb_create_hook(IntPtr method, IntPtr callback, out IntPtr original);
```

Create a hook on an IL2CPP method.

**Parameters:**
- `method` - Pointer to MethodInfo
- `callback` - Function pointer to the detour callback
- `original` - Output: pointer to trampoline for calling original

**Returns:** Hook handle (`>0` on success), or negative error code.

**Note:** Use `HookManager` for managed hook lifecycle instead of calling this directly.

---

### mdb_create_hook_rva

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern long mdb_create_hook_rva(ulong rva, IntPtr callback, out IntPtr original);
```

Create a hook on a method by RVA offset.

**Parameters:**
- `rva` - The RVA offset of the method
- `callback` - Function pointer to the detour callback
- `original` - Output: pointer to trampoline for calling original

**Returns:** Hook handle (`>0` on success), or negative error code.

---

### mdb_create_hook_ptr

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern long mdb_create_hook_ptr(IntPtr target, IntPtr detour, out IntPtr original);
```

Create a hook on a direct function pointer.

**Parameters:**
- `target` - Target function pointer to hook
- `detour` - Detour function pointer
- `original` - Output: pointer to trampoline for calling original

**Returns:** Hook handle (`>0` on success), or negative error code.

---

### mdb_remove_hook

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern int mdb_remove_hook(long hookHandle);
```

Remove a hook by handle.

**Parameters:**
- `hookHandle` - The hook handle returned by `mdb_create_hook*`

**Returns:** `0` on success, non-zero on failure.

---

### mdb_set_hook_enabled

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern int mdb_set_hook_enabled(long hookHandle, [MarshalAs(UnmanagedType.I1)] bool enabled);
```

Enable or disable a hook.

**Parameters:**
- `hookHandle` - The hook handle
- `enabled` - `true` to enable, `false` to disable

**Returns:** `0` on success, non-zero on failure.

---

### mdb_hook_get_count

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern int mdb_hook_get_count();
```

Get the number of active hooks.

**Returns:** Number of active hooks.

**Example:**
```csharp
int hookCount = Il2CppBridge.mdb_hook_get_count();
Logger.Info($"Active hooks: {hookCount}");
```

---

### mdb_hook_set_debug_enabled

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern void mdb_hook_set_debug_enabled([MarshalAs(UnmanagedType.I1)] bool enabled);
```

Enable or disable verbose hook debugging.

**Parameters:**
- `enabled` - `true` to enable debug output, `false` to disable

---

### mdb_hook_is_debug_enabled

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
[return: MarshalAs(UnmanagedType.I1)]
public static extern bool mdb_hook_is_debug_enabled();
```

Check if hook debugging is enabled.

**Returns:** `true` if debugging is enabled.

---

### mdb_hook_dump_all

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern void mdb_hook_dump_all();
```

Dump all hook information to the debug log.

**Example:**
```csharp
// Enable debug mode and dump all hooks
Il2CppBridge.mdb_hook_set_debug_enabled(true);
Il2CppBridge.mdb_hook_dump_all();
```

---

## RVA-Based Access Functions

### mdb_get_gameassembly_base

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern IntPtr mdb_get_gameassembly_base();
```

Get the base address of GameAssembly.dll.

**Returns:** Base address, or `IntPtr.Zero` if not loaded.

**Example:**
```csharp
IntPtr baseAddr = Il2CppBridge.mdb_get_gameassembly_base();
Logger.Info($"GameAssembly.dll base: 0x{baseAddr.ToInt64():X}");
```

---

### mdb_get_method_pointer_from_rva

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern IntPtr mdb_get_method_pointer_from_rva(ulong rva);
```

Get a function pointer directly from an RVA offset.

**Parameters:**
- `rva` - The RVA offset from the IL2CPP dump (e.g., `0x52f1e0`)

**Returns:** Function pointer at base + RVA.

**Use case:** Hooking obfuscated methods that can't be found by name.

**Example:**
```csharp
// Hook method at RVA 0x1A3B5C0 from IL2CPP dump
ulong rva = 0x1A3B5C0;
IntPtr methodPtr = Il2CppBridge.mdb_get_method_pointer_from_rva(rva);

HookInfo hook = HookManager.CreateHookByPtr(methodPtr, MyDetour, out IntPtr original, "ObfuscatedMethod");
```

---

## Error Handling Functions

### mdb_get_last_error

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern IntPtr mdb_get_last_error();
```

Get the last error message from the bridge (native pointer).

**Helper Method:**
```csharp
public static string GetLastError()
```

Get the last error as a managed string.

**Returns:** Error message, or `"Unknown error"` if no error.

**Example:**
```csharp
IntPtr klass = Il2CppBridge.mdb_find_class("Assembly-CSharp", "Invalid", "Class");
if (klass == IntPtr.Zero)
{
    string error = Il2CppBridge.GetLastError();
    Logger.Error($"Failed to find class: {error}");
}
```

---

### mdb_get_last_error_code

```csharp
[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
public static extern int mdb_get_last_error_code();
```

Get the last error code from the bridge.

**Helper Method:**
```csharp
public static MdbErrorCode GetLastErrorCode()
```

Get the last error code as a managed enum.

**Returns:** `MdbErrorCode` enum value.

**Example:**
```csharp
IntPtr method = Il2CppBridge.mdb_get_method(klass, "NonExistentMethod", 0);
if (method == IntPtr.Zero)
{
    MdbErrorCode code = Il2CppBridge.GetLastErrorCode();
    if (code == MdbErrorCode.MethodNotFound)
    {
        Logger.Warning("Method not found, using fallback");
    }
}
```

---

### MdbErrorCode Enum

```csharp
public enum MdbErrorCode : int
{
    Success = 0,
    
    // Initialization errors (1-99)
    NotInitialized = 1,
    InitFailed = 2,
    GameAssemblyNotFound = 3,
    ExportNotFound = 4,
    
    // Argument errors (100-199)
    InvalidArgument = 100,
    NullPointer = 101,
    InvalidClass = 102,
    InvalidMethod = 103,
    InvalidField = 104,
    
    // Resolution errors (200-299)
    ClassNotFound = 200,
    MethodNotFound = 201,
    FieldNotFound = 202,
    AssemblyNotFound = 203,
    
    // Invocation errors (300-399)
    InvocationFailed = 300,
    ExceptionThrown = 301,
    ThreadNotAttached = 302,
    
    // Memory errors (400-499)
    AllocationFailed = 400,
    BufferTooSmall = 401,
    
    // Unknown error
    Unknown = -1
}
```

---

## Type System Constants

### IL2CPP Type Enum Values

```csharp
public static class Il2CppTypeEnum
{
    public const int IL2CPP_TYPE_END = 0x00;           // End of type list marker
    public const int IL2CPP_TYPE_VOID = 0x01;          // Void type
    public const int IL2CPP_TYPE_BOOLEAN = 0x02;       // Boolean type
    public const int IL2CPP_TYPE_CHAR = 0x03;          // Character type
    public const int IL2CPP_TYPE_I1 = 0x04;            // Signed byte type
    public const int IL2CPP_TYPE_U1 = 0x05;            // Unsigned byte type
    public const int IL2CPP_TYPE_I2 = 0x06;            // Short type
    public const int IL2CPP_TYPE_U2 = 0x07;            // Unsigned short type
    public const int IL2CPP_TYPE_I4 = 0x08;            // Int type
    public const int IL2CPP_TYPE_U4 = 0x09;            // Unsigned int type
    public const int IL2CPP_TYPE_I8 = 0x0a;            // Long type
    public const int IL2CPP_TYPE_U8 = 0x0b;            // Unsigned long type
    public const int IL2CPP_TYPE_R4 = 0x0c;            // Float type
    public const int IL2CPP_TYPE_R8 = 0x0d;            // Double type
    public const int IL2CPP_TYPE_STRING = 0x0e;        // String type
    public const int IL2CPP_TYPE_PTR = 0x0f;           // Pointer type
    public const int IL2CPP_TYPE_BYREF = 0x10;         // By-reference type
    public const int IL2CPP_TYPE_VALUETYPE = 0x11;     // Value type (struct)
    public const int IL2CPP_TYPE_CLASS = 0x12;         // Class type
    public const int IL2CPP_TYPE_ARRAY = 0x14;         // Array type
    public const int IL2CPP_TYPE_GENERICINST = 0x15;   // Generic instance type
    public const int IL2CPP_TYPE_I = 0x18;             // Native int pointer (IntPtr)
    public const int IL2CPP_TYPE_U = 0x19;             // Native unsigned int pointer (UIntPtr)
    public const int IL2CPP_TYPE_OBJECT = 0x1c;        // System.Object type
    public const int IL2CPP_TYPE_SZARRAY = 0x1d;       // Single-dimension zero-based array
    public const int IL2CPP_TYPE_ENUM = 0x55;          // Enumeration type
}
```

**Example:**
```csharp
IntPtr paramType = Il2CppBridge.mdb_method_get_param_type(method, 0);
int typeEnum = Il2CppBridge.mdb_type_get_type_enum(paramType);

if (typeEnum == Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_I4)
{
    Logger.Info("Parameter 0 is an int");
}
else if (typeEnum == Il2CppBridge.Il2CppTypeEnum.IL2CPP_TYPE_STRING)
{
    Logger.Info("Parameter 0 is a string");
}
```

---

## Common Workflows

### Finding and Calling a Method

```csharp
// 1. Find the class
IntPtr playerClass = Il2CppBridge.mdb_find_class("Assembly-CSharp", "Game", "Player");
if (playerClass == IntPtr.Zero)
{
    Logger.Error($"Failed to find Player class: {Il2CppBridge.GetLastError()}");
    return;
}

// 2. Find the method
IntPtr takeDamageMethod = Il2CppBridge.mdb_get_method(playerClass, "TakeDamage", 1);
if (takeDamageMethod == IntPtr.Zero)
{
    Logger.Error("Failed to find TakeDamage method");
    return;
}

// 3. Prepare arguments
int damage = 25;
IntPtr damagePtr = Marshal.AllocHGlobal(sizeof(int));
Marshal.WriteInt32(damagePtr, damage);
IntPtr[] args = new IntPtr[] { damagePtr };

// 4. Invoke the method
IntPtr playerInstance = /* get player instance */;
IntPtr result = Il2CppBridge.mdb_invoke_method(takeDamageMethod, playerInstance, args, out IntPtr exception);

// 5. Check for exceptions
if (exception != IntPtr.Zero)
{
    Logger.Error("Exception occurred during method invocation");
}

// 6. Clean up
Marshal.FreeHGlobal(damagePtr);
```

---

### Getting and Setting Field Values

```csharp
// Find class and field
IntPtr playerClass = Il2CppBridge.mdb_find_class("Assembly-CSharp", "Game", "Player");
IntPtr healthField = Il2CppBridge.mdb_get_field(playerClass, "health");

// Get field value
IntPtr playerInstance = /* ... */;
IntPtr healthPtr = Marshal.AllocHGlobal(sizeof(int));
Il2CppBridge.mdb_field_get_value(playerInstance, healthField, healthPtr);
int currentHealth = Marshal.ReadInt32(healthPtr);
Marshal.FreeHGlobal(healthPtr);

Logger.Info($"Current health: {currentHealth}");

// Set field value
int newHealth = 100;
IntPtr newHealthPtr = Marshal.AllocHGlobal(sizeof(int));
Marshal.WriteInt32(newHealthPtr, newHealth);
Il2CppBridge.mdb_field_set_value(playerInstance, healthField, newHealthPtr);
Marshal.FreeHGlobal(newHealthPtr);

Logger.Info("Health set to 100");
```

---

### Working with Strings

```csharp
// Create IL2CPP string
string message = "Hello from mod!";
IntPtr il2cppString = Il2CppBridge.mdb_string_new(message);

// Use in method call (e.g., Debug.Log)
IntPtr debugClass = Il2CppBridge.mdb_find_class("UnityEngine.CoreModule", "UnityEngine", "Debug");
IntPtr logMethod = Il2CppBridge.mdb_get_method(debugClass, "Log", 1);
IntPtr[] args = new IntPtr[] { il2cppString };
Il2CppBridge.mdb_invoke_method(logMethod, IntPtr.Zero, args, out IntPtr ex);

// Convert IL2CPP string to managed string
IntPtr nameField = Il2CppBridge.mdb_get_field(playerClass, "playerName");
IntPtr nameStrPtr = Marshal.AllocHGlobal(IntPtr.Size);
Il2CppBridge.mdb_field_get_value(playerInstance, nameField, nameStrPtr);
IntPtr il2cppName = Marshal.ReadIntPtr(nameStrPtr);
Marshal.FreeHGlobal(nameStrPtr);

string playerName = Il2CppBridge.Il2CppStringToManaged(il2cppName);
Logger.Info($"Player name: {playerName}");
```

---

### Iterating Over Class Members

```csharp
IntPtr klass = Il2CppBridge.mdb_find_class("Assembly-CSharp", "Game", "Player");

// Iterate over fields
int fieldCount = Il2CppBridge.mdb_class_get_field_count(klass);
Logger.Info($"Fields ({fieldCount}):");
for (int i = 0; i < fieldCount; i++)
{
    IntPtr field = Il2CppBridge.mdb_class_get_field_by_index(klass, i);
    string fieldName = Il2CppBridge.GetFieldName(field);
    bool isStatic = Il2CppBridge.mdb_field_is_static(field);
    IntPtr fieldType = Il2CppBridge.mdb_field_get_type(field);
    string typeName = Il2CppBridge.GetTypeName(fieldType);
    
    Logger.Info($"  {(isStatic ? "static" : "      ")} {typeName} {fieldName}");
}

// Iterate over methods
int methodCount = Il2CppBridge.mdb_class_get_method_count(klass);
Logger.Info($"Methods ({methodCount}):");
for (int i = 0; i < methodCount; i++)
{
    IntPtr method = Il2CppBridge.mdb_class_get_method_by_index(klass, i);
    string methodName = Il2CppBridge.GetMethodName(method);
    int paramCount = Il2CppBridge.mdb_method_get_param_count(method);
    
    Logger.Info($"  {methodName}({paramCount} params)");
}
```

---

### Working with Arrays

```csharp
// Get an array field
IntPtr inventoryField = Il2CppBridge.mdb_get_field(playerClass, "inventory");
IntPtr inventoryArrayPtr = Marshal.AllocHGlobal(IntPtr.Size);
Il2CppBridge.mdb_field_get_value(playerInstance, inventoryField, inventoryArrayPtr);
IntPtr inventoryArray = Marshal.ReadIntPtr(inventoryArrayPtr);
Marshal.FreeHGlobal(inventoryArrayPtr);

// Get array information
int arrayLength = Il2CppBridge.mdb_array_length(inventoryArray);
IntPtr elementClass = Il2CppBridge.mdb_array_get_element_class(inventoryArray);
string elementType = Il2CppBridge.GetClassName(elementClass);

Logger.Info($"Inventory array has {arrayLength} items of type {elementType}");

// Iterate over array elements
for (int i = 0; i < arrayLength; i++)
{
    IntPtr element = Il2CppBridge.mdb_array_get_element(inventoryArray, i);
    
    // Process element based on type
    if (Il2CppBridge.mdb_class_is_valuetype(elementClass) == 1)
    {
        // Value type - element is pointer to struct data
    }
    else
    {
        // Reference type - element is object pointer
        IntPtr objClass = Il2CppBridge.mdb_object_get_class(element);
        string objType = Il2CppBridge.GetClassName(objClass);
        Logger.Info($"  Item {i}: {objType}");
    }
}
```

---

### Traversing GameObject Hierarchy

```csharp
void TraverseGameObject(IntPtr gameObject, int depth = 0)
{
    string indent = new string(' ', depth * 2);
    
    // Get GameObject name
    IntPtr goClass = Il2CppBridge.mdb_object_get_class(gameObject);
    IntPtr nameMethod = Il2CppBridge.mdb_get_method(goClass, "get_name", 0);
    IntPtr nameStr = Il2CppBridge.mdb_invoke_method(nameMethod, gameObject, null, out IntPtr ex);
    string name = Il2CppBridge.Il2CppStringToManaged(nameStr);
    
    // Get active state
    bool isActive = Il2CppBridge.mdb_gameobject_get_active_self(gameObject);
    
    Logger.Info($"{indent}{name} {(isActive ? "[Active]" : "[Inactive]")}");
    
    // Get Transform
    IntPtr transformMethod = Il2CppBridge.mdb_get_method(goClass, "get_transform", 0);
    IntPtr transform = Il2CppBridge.mdb_invoke_method(transformMethod, gameObject, null, out ex);
    
    // Get child count
    int childCount = Il2CppBridge.mdb_transform_get_child_count(transform);
    
    // Traverse children
    for (int i = 0; i < childCount; i++)
    {
        IntPtr childTransform = Il2CppBridge.mdb_transform_get_child(transform, i);
        
        // Get GameObject from Transform
        IntPtr transformClass = Il2CppBridge.mdb_object_get_class(childTransform);
        IntPtr gameObjectMethod = Il2CppBridge.mdb_get_method(transformClass, "get_gameObject", 0);
        IntPtr childGameObject = Il2CppBridge.mdb_invoke_method(gameObjectMethod, childTransform, null, out ex);
        
        // Recurse
        TraverseGameObject(childGameObject, depth + 1);
    }
}
```

---

## Complete Examples

### Example 1: Class Inspector

```csharp
using System;
using GameSDK.ModHost;

[Mod("Author.Inspector", "Class Inspector", "1.0.0")]
public class ClassInspector : ModBase
{
    public override void OnLoad()
    {
        InspectClass("Assembly-CSharp", "Game", "Player");
    }
    
    private void InspectClass(string assembly, string ns, string className)
    {
        IntPtr klass = Il2CppBridge.mdb_find_class(assembly, ns, className);
        if (klass == IntPtr.Zero)
        {
            Logger.Error($"Class not found: {ns}.{className}");
            return;
        }
        
        string fullName = Il2CppBridge.GetClassFullName(klass);
        int size = Il2CppBridge.mdb_get_class_size(klass);
        
        Logger.Info($"=== Class: {fullName} ===");
        Logger.Info($"Instance size: {size} bytes");
        
        // Parent class
        IntPtr parent = Il2CppBridge.mdb_class_get_parent(klass);
        if (parent != IntPtr.Zero)
        {
            string parentName = Il2CppBridge.GetClassFullName(parent);
            Logger.Info($"Inherits from: {parentName}");
        }
        
        // Fields
        int fieldCount = Il2CppBridge.mdb_class_get_field_count(klass);
        Logger.Info($"\nFields ({fieldCount}):");
        for (int i = 0; i < fieldCount; i++)
        {
            IntPtr field = Il2CppBridge.mdb_class_get_field_by_index(klass, i);
            string fieldName = Il2CppBridge.GetFieldName(field);
            bool isStatic = Il2CppBridge.mdb_field_is_static(field);
            int offset = Il2CppBridge.mdb_get_field_offset(field);
            IntPtr fieldType = Il2CppBridge.mdb_field_get_type(field);
            string typeName = Il2CppBridge.GetTypeName(fieldType);
            
            Logger.Info($"  [{offset:X3}] {(isStatic ? "static" : "      ")} {typeName} {fieldName}");
        }
        
        // Methods
        int methodCount = Il2CppBridge.mdb_class_get_method_count(klass);
        Logger.Info($"\nMethods ({methodCount}):");
        for (int i = 0; i < methodCount; i++)
        {
            IntPtr method = Il2CppBridge.mdb_class_get_method_by_index(klass, i);
            string methodName = Il2CppBridge.GetMethodName(method);
            int paramCount = Il2CppBridge.mdb_method_get_param_count(method);
            
            // Get method info
            Il2CppBridge.mdb_get_method_info(method, out int pc, out bool isStatic, out bool hasReturn);
            
            Logger.Info($"  {(isStatic ? "static" : "      ")} {methodName}({paramCount} params) {(hasReturn ? "-> returns" : "")}");
        }
    }
}
```

---

### Example 2: Health Monitor

```csharp
using System;
using System.Runtime.InteropServices;
using GameSDK.ModHost;

[Mod("Author.HealthMonitor", "Health Monitor", "1.0.0")]
public class HealthMonitor : ModBase
{
    private IntPtr playerClass;
    private IntPtr healthField;
    
    public override void OnLoad()
    {
        // Find player class and health field
        playerClass = Il2CppBridge.mdb_find_class("Assembly-CSharp", "Game", "Player");
        if (playerClass == IntPtr.Zero)
        {
            Logger.Error("Failed to find Player class");
            return;
        }
        
        healthField = Il2CppBridge.mdb_get_field(playerClass, "health");
        if (healthField == IntPtr.Zero)
        {
            Logger.Error("Failed to find health field");
            return;
        }
        
        Logger.Info("Health monitor initialized");
    }
    
    public override void OnUpdate()
    {
        // Get player instance (example - actual implementation depends on game)
        IntPtr playerInstance = GetPlayerInstance();
        if (playerInstance == IntPtr.Zero)
            return;
        
        // Read health value
        IntPtr healthPtr = Marshal.AllocHGlobal(sizeof(int));
        Il2CppBridge.mdb_field_get_value(playerInstance, healthField, healthPtr);
        int health = Marshal.ReadInt32(healthPtr);
        Marshal.FreeHGlobal(healthPtr);
        
        // Heal player if health is low
        if (health < 20)
        {
            Logger.Warning($"Health is low ({health}), healing to 100");
            SetPlayerHealth(playerInstance, 100);
        }
    }
    
    private void SetPlayerHealth(IntPtr playerInstance, int newHealth)
    {
        IntPtr healthPtr = Marshal.AllocHGlobal(sizeof(int));
        Marshal.WriteInt32(healthPtr, newHealth);
        Il2CppBridge.mdb_field_set_value(playerInstance, healthField, healthPtr);
        Marshal.FreeHGlobal(healthPtr);
    }
    
    private IntPtr GetPlayerInstance()
    {
        // Example implementation - get static Player.Instance
        IntPtr instanceField = Il2CppBridge.mdb_get_field(playerClass, "Instance");
        if (instanceField == IntPtr.Zero)
            return IntPtr.Zero;
        
        IntPtr instancePtr = Marshal.AllocHGlobal(IntPtr.Size);
        Il2CppBridge.mdb_field_static_get_value(instanceField, instancePtr);
        IntPtr instance = Marshal.ReadIntPtr(instancePtr);
        Marshal.FreeHGlobal(instancePtr);
        
        return instance;
    }
}
```

---

### Example 3: Scene Explorer

```csharp
using System;
using GameSDK.ModHost;
using GameSDK.ModHost.ImGui;

[Mod("Author.SceneExplorer", "Scene Explorer", "1.0.0")]
public class SceneExplorer : ModBase
{
    private bool showWindow = true;
    
    public override void OnLoad()
    {
        ImGuiManager.RegisterCallback(DrawUI, "Scene Explorer");
        Logger.Info("Scene Explorer loaded");
    }
    
    private void DrawUI()
    {
        if (!showWindow) return;
        
        if (ImGui.Begin("Scene Explorer", ref showWindow))
        {
            int sceneCount = Il2CppBridge.mdb_scenemanager_get_scene_count();
            ImGui.Text($"Loaded Scenes: {sceneCount}");
            ImGui.Separator();
            
            for (int i = 0; i < sceneCount; i++)
            {
                string sceneName = Il2CppBridge.GetSceneName(i);
                int sceneHandle = Il2CppBridge.mdb_scenemanager_get_scene_handle(i);
                int rootCount = Il2CppBridge.mdb_scenemanager_get_scene_root_count(i);
                
                if (ImGui.TreeNode($"Scene {i}: {sceneName}"))
                {
                    ImGui.Text($"Handle: {sceneHandle}");
                    ImGui.Text($"Root Objects: {rootCount}");
                    ImGui.TreePop();
                }
            }
            
            ImGui.Separator();
            
            // DontDestroyOnLoad scene
            int ddolHandle = Il2CppBridge.mdb_get_dontdestroyonload_scene_handle();
            ImGui.Text($"DontDestroyOnLoad Handle: {ddolHandle}");
            
            ImGui.Separator();
            
            int hookCount = Il2CppBridge.mdb_hook_get_count();
            ImGui.Text($"Active Hooks: {hookCount}");
            
            if (ImGui.Button("Dump All Hooks"))
            {
                Il2CppBridge.mdb_hook_dump_all();
                Logger.Info("Hook information dumped to log");
            }
        }
        ImGui.End();
    }
}
```

---

## Best Practices

### ✅ Do

- **Always check return values** - Most functions return `IntPtr.Zero` or negative values on error
- **Use error helpers** - Call `GetLastError()` and `GetLastErrorCode()` when operations fail
- **Free unmanaged memory** - Always `Marshal.FreeHGlobal()` after allocating memory
- **Cache class and method pointers** - Finding them is expensive, store them for reuse
- **Use helper methods** - `GetClassName()`, `Il2CppStringToManaged()` etc. handle marshalling
- **Attach threads** - Call `mdb_thread_attach()` when invoking IL2CPP from new threads
- **Check for null IL2CPP strings** - Always validate string pointers before converting
- **Use the right calling convention** - Always use `CallingConvention.Cdecl` for P/Invoke
- **Marshal booleans correctly** - Use `[MarshalAs(UnmanagedType.I1)]` for bool parameters/returns
- **Wrap operations in try-catch** - Marshalling and unmanaged code can throw exceptions

### ❌ Don't

- **Don't leak memory** - Always free allocated memory even if operations fail
- **Don't call bridge functions before `mdb_init()`** - The framework initializes automatically
- **Don't ignore error codes** - Check return values to catch problems early
- **Don't assume methods exist** - Games may be updated, obfuscated, or platform-specific
- **Don't invoke methods without checking for exceptions** - Check the `exception` out parameter
- **Don't use the wrong assembly name** - Unity assemblies may vary (`UnityEngine` vs `UnityEngine.CoreModule`)
- **Don't forget parameter counts** - `mdb_get_method()` requires exact parameter count (or `-1`)
- **Don't hook critical Unity methods** without testing - Can crash the game
- **Don't call IL2CPP from arbitrary threads** - Attach the thread first
- **Don't forget to handle value types differently** - They require different marshalling

---

## Common Mistakes

### Mistake 1: Not Checking Return Values

```csharp
// ❌ WRONG - Doesn't check if class was found
IntPtr klass = Il2CppBridge.mdb_find_class("Assembly-CSharp", "Game", "Player");
IntPtr method = Il2CppBridge.mdb_get_method(klass, "TakeDamage", 1); // Will fail if klass is Zero
```

```csharp
// ✅ CORRECT - Checks return values
IntPtr klass = Il2CppBridge.mdb_find_class("Assembly-CSharp", "Game", "Player");
if (klass == IntPtr.Zero)
{
    Logger.Error($"Failed to find Player class: {Il2CppBridge.GetLastError()}");
    return;
}

IntPtr method = Il2CppBridge.mdb_get_method(klass, "TakeDamage", 1);
if (method == IntPtr.Zero)
{
    Logger.Error("Failed to find TakeDamage method");
    return;
}
```

---

### Mistake 2: Memory Leaks

```csharp
// ❌ WRONG - Leaks memory
IntPtr healthPtr = Marshal.AllocHGlobal(sizeof(int));
Il2CppBridge.mdb_field_get_value(playerInstance, healthField, healthPtr);
int health = Marshal.ReadInt32(healthPtr);
// Forgot to free!
```

```csharp
// ✅ CORRECT - Frees allocated memory
IntPtr healthPtr = Marshal.AllocHGlobal(sizeof(int));
try
{
    Il2CppBridge.mdb_field_get_value(playerInstance, healthField, healthPtr);
    int health = Marshal.ReadInt32(healthPtr);
    Logger.Info($"Health: {health}");
}
finally
{
    Marshal.FreeHGlobal(healthPtr);
}
```

---

### Mistake 3: Wrong Assembly Name

```csharp
// ❌ WRONG - UnityEngine doesn't exist as an assembly in newer Unity
IntPtr goClass = Il2CppBridge.mdb_find_class("UnityEngine", "UnityEngine", "GameObject");
```

```csharp
// ✅ CORRECT - Use the correct assembly name
IntPtr goClass = Il2CppBridge.mdb_find_class("UnityEngine.CoreModule", "UnityEngine", "GameObject");

// For game classes, typically:
IntPtr playerClass = Il2CppBridge.mdb_find_class("Assembly-CSharp", "Game", "Player");
```

---

### Mistake 4: Not Handling Exceptions

```csharp
// ❌ WRONG - Doesn't check for exceptions
IntPtr result = Il2CppBridge.mdb_invoke_method(method, instance, args, out IntPtr exception);
// Exception might have been thrown!
```

```csharp
// ✅ CORRECT - Checks for exceptions
IntPtr result = Il2CppBridge.mdb_invoke_method(method, instance, args, out IntPtr exception);
if (exception != IntPtr.Zero)
{
    // Get exception details if needed
    IntPtr exClass = Il2CppBridge.mdb_object_get_class(exception);
    string exType = Il2CppBridge.GetClassName(exClass);
    Logger.Error($"Exception thrown: {exType}");
    return;
}
```

---

### Mistake 5: Wrong Parameter Count

```csharp
// ❌ WRONG - Method has 2 parameters, but we search for 1
IntPtr method = Il2CppBridge.mdb_get_method(klass, "TakeDamage", 1);
// Returns IntPtr.Zero if the method actually has 2 parameters
```

```csharp
// ✅ CORRECT - Use exact parameter count or -1 to find any
IntPtr method = Il2CppBridge.mdb_get_method(klass, "TakeDamage", 2); // Exact count

// OR find first match regardless of parameter count
IntPtr method = Il2CppBridge.mdb_get_method(klass, "TakeDamage", -1);
```

---

### Mistake 6: Forgetting to Cache Pointers

```csharp
// ❌ WRONG - Finds class and method every frame
public override void OnUpdate()
{
    IntPtr klass = Il2CppBridge.mdb_find_class("Assembly-CSharp", "Game", "Player");
    IntPtr method = Il2CppBridge.mdb_get_method(klass, "GetHealth", 0);
    // Expensive operations repeated every frame!
}
```

```csharp
// ✅ CORRECT - Cache pointers in OnLoad
private IntPtr playerClass;
private IntPtr getHealthMethod;

public override void OnLoad()
{
    playerClass = Il2CppBridge.mdb_find_class("Assembly-CSharp", "Game", "Player");
    getHealthMethod = Il2CppBridge.mdb_get_method(playerClass, "GetHealth", 0);
}

public override void OnUpdate()
{
    // Use cached pointers - much faster
    if (getHealthMethod != IntPtr.Zero)
    {
        // Use the method...
    }
}
```

---

### Mistake 7: Incorrect Value Type Handling

```csharp
// ❌ WRONG - Treating value type array element as object pointer
IntPtr element = Il2CppBridge.mdb_array_get_element(array, 0);
IntPtr objClass = Il2CppBridge.mdb_object_get_class(element); // Crash! Element is struct data
```

```csharp
// ✅ CORRECT - Check if element class is value type first
IntPtr elementClass = Il2CppBridge.mdb_array_get_element_class(array);
bool isValueType = Il2CppBridge.mdb_class_is_valuetype(elementClass) == 1;

IntPtr element = Il2CppBridge.mdb_array_get_element(array, 0);

if (isValueType)
{
    // Element points to struct data, read directly
    int value = Marshal.ReadInt32(element);
}
else
{
    // Element is object pointer, can get class
    IntPtr objClass = Il2CppBridge.mdb_object_get_class(element);
}
```

---

## Performance Considerations

### Expensive Operations

The following operations are relatively expensive and should be minimized:

1. **Class resolution** (`mdb_find_class`) - Cache the result
2. **Method resolution** (`mdb_get_method`) - Cache the result
3. **Field resolution** (`mdb_get_field`) - Cache the result
4. **Method invocation** (`mdb_invoke_method`) - Use sparingly in hot paths
5. **String conversion** - Minimize conversions in tight loops

### Optimization Tips

```csharp
// ✅ Cache expensive lookups in OnLoad
private IntPtr playerClass;
private IntPtr healthField;
private IntPtr takeDamageMethod;

public override void OnLoad()
{
    playerClass = Il2CppBridge.mdb_find_class("Assembly-CSharp", "Game", "Player");
    healthField = Il2CppBridge.mdb_get_field(playerClass, "health");
    takeDamageMethod = Il2CppBridge.mdb_get_method(playerClass, "TakeDamage", 1);
}

// ✅ Throttle expensive operations
private int frameCounter = 0;

public override void OnUpdate()
{
    frameCounter++;
    
    // Only check health every 60 frames (1 second @ 60fps)
    if (frameCounter % 60 == 0)
    {
        CheckPlayerHealth();
    }
}

// ✅ Reuse allocated memory when possible
private IntPtr healthBuffer = IntPtr.Zero;

public override void OnLoad()
{
    healthBuffer = Marshal.AllocHGlobal(sizeof(int));
}

private int GetPlayerHealth(IntPtr playerInstance)
{
    Il2CppBridge.mdb_field_get_value(playerInstance, healthField, healthBuffer);
    return Marshal.ReadInt32(healthBuffer);
}
```

---

## Thread Safety

### IL2CPP Thread Attachment

IL2CPP requires threads to be attached to the domain before calling runtime functions:

```csharp
using System.Threading;

private void BackgroundTask()
{
    // Attach thread to IL2CPP domain
    IntPtr domain = Il2CppBridge.mdb_domain_get();
    IntPtr threadHandle = Il2CppBridge.mdb_thread_attach(domain);
    
    if (threadHandle == IntPtr.Zero)
    {
        Logger.Error("Failed to attach thread to IL2CPP domain");
        return;
    }
    
    try
    {
        // Now safe to call IL2CPP functions
        IntPtr klass = Il2CppBridge.mdb_find_class("Assembly-CSharp", "Game", "Player");
        // ... more IL2CPP operations
    }
    catch (Exception ex)
    {
        Logger.Error($"Error in background task: {ex.Message}");
    }
}

public override void OnLoad()
{
    Thread thread = new Thread(BackgroundTask);
    thread.Start();
}
```

**Important:** The main Unity thread is already attached - you only need to attach new threads you create.

---

## Debugging

### Enable Hook Debugging

```csharp
// Enable verbose hook debugging
Il2CppBridge.mdb_hook_set_debug_enabled(true);

// Check if debugging is enabled
bool isDebugEnabled = Il2CppBridge.mdb_hook_is_debug_enabled();
Logger.Info($"Hook debugging: {isDebugEnabled}");

// Dump all hook information to log
Il2CppBridge.mdb_hook_dump_all();

// Check hook count
int hookCount = Il2CppBridge.mdb_hook_get_count();
Logger.Info($"Active hooks: {hookCount}");
```

### Error Debugging

```csharp
IntPtr klass = Il2CppBridge.mdb_find_class("Assembly-CSharp", "Game", "InvalidClass");
if (klass == IntPtr.Zero)
{
    string error = Il2CppBridge.GetLastError();
    MdbErrorCode code = Il2CppBridge.GetLastErrorCode();
    
    Logger.Error($"Failed to find class:");
    Logger.Error($"  Error: {error}");
    Logger.Error($"  Code: {code} ({(int)code})");
    
    // Take action based on error code
    if (code == MdbErrorCode.ClassNotFound)
    {
        Logger.Warning("Class may have been renamed or obfuscated");
    }
    else if (code == MdbErrorCode.AssemblyNotFound)
    {
        Logger.Warning("Assembly name may be incorrect");
    }
}
```

---

## Troubleshooting

### Class Not Found

**Problem:** `mdb_find_class` returns `IntPtr.Zero`.

**Solutions:**
1. Verify assembly name (use IL2CPP dumper to find correct name)
2. Check namespace spelling (case-sensitive)
3. Check class name spelling (case-sensitive)
4. For Unity classes, use assembly like `UnityEngine.CoreModule` not `UnityEngine`
5. Class may be obfuscated - use RVA-based access instead

### Method Not Found

**Problem:** `mdb_get_method` returns `IntPtr.Zero`.

**Solutions:**
1. Verify method name spelling (case-sensitive)
2. Check parameter count (use `-1` if unsure)
3. Method may be obfuscated - use `mdb_get_method_pointer_from_rva`
4. Use enumeration to list all methods: `mdb_class_get_method_count` + `mdb_class_get_method_by_index`

### Method Invocation Fails

**Problem:** `mdb_invoke_method` throws exception or returns unexpected result.

**Solutions:**
1. Check if exception was thrown: `if (exception != IntPtr.Zero)`
2. Verify argument types and count match method signature
3. Ensure each argument pointer points to the actual value, not the pointer itself
4. For static methods, pass `IntPtr.Zero` as instance
5. Thread must be attached to IL2CPP domain

### Memory Access Violations

**Problem:** Application crashes with access violation.

**Solutions:**
1. Always check pointers for `IntPtr.Zero` before using
2. Don't access freed memory
3. Ensure correct marshalling of value types vs reference types
4. Don't read past array bounds
5. Verify field offsets are correct

---

## See Also

- [HookManager API](hookmanager) - Managed hook lifecycle and control
- [Patch Attributes](patch-attributes) - Declarative patching system
- [ModBase API](modbase) - Mod lifecycle and base class
- [Examples](/docs/examples) - Working mod examples
- [Getting Started](/docs/getting-started) - Creating your first mod

---

## Related Resources

### External Documentation

- [IL2CPP Runtime API](https://docs.unity3d.com/Manual/IL2CPP.html) - Official Unity IL2CPP documentation
- [IL2CPP Dumper](https://github.com/Perfare/Il2CppDumper) - Tool for dumping IL2CPP metadata
- [MinHook](https://github.com/TsudaKageyu/minhook) - Hooking library used by MDB Bridge

### MDB Framework Components

- **MDB_Bridge.dll** - Native bridge DLL (C++)
- **GameAssembly.dll** - IL2CPP runtime (Unity-generated)
- **Il2CppBridge.cs** - Managed P/Invoke wrapper (C#)

---

[← Back to API Index](../api) | [HookManager →](hookmanager)
