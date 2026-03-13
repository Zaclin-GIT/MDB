// ==============================
// Il2CppStructs — IL2CPP memory layout structs
// ==============================
// StructLayout(Explicit) structs matching the exact memory layouts from checkpoint.md
// and il2cpp_resolver.hpp. All offsets validated against checkpoint Section 5.
//
// Target: Unity 2021+ / IL2CPP metadata v29+ / x64

using System;
using System.Runtime.InteropServices;

namespace GameSDK.Injection
{
    // ==============================
    // Constants
    // ==============================

    internal static class Il2CppConstants
    {
        public const byte IL2CPP_TYPE_CLASS = 0x12;
        public const byte IL2CPP_TYPE_VALUETYPE = 0x11;

        // Il2CppClass layout constants
        /// <summary>Offset of Il2CppClass_2 within Il2CppClass (after _1 + static_fields + rgctx_data).</summary>
        public const int CLASS_2_OFFSET = 0xC8; // 184 (_1) + 8 (static_fields) + 8 (rgctx_data)
        /// <summary>Offset of vtable within Il2CppClass.</summary>
        public const int VTABLE_OFFSET = 0x138; // CLASS_2_OFFSET + 112 (_2)

        // Il2CppImage offsets (v27_0 layout)
        public const int IMAGE_NAME_OFFSET = 0x00;
        public const int IMAGE_NAME_NO_EXT_OFFSET = 0x08;
        public const int IMAGE_ASSEMBLY_OFFSET = 0x10;
        public const int IMAGE_DYNAMIC_OFFSET = 0x44;
    }

    // ==============================
    // Il2CppType (16 bytes)
    // ==============================

    /// <summary>
    /// IL2CPP type descriptor. In v29+, Data is a direct pointer to Il2CppTypeDefinition.
    /// Layout: Data (8 bytes) + packed attrs/type/byref (4 bytes) + padding (4 bytes) = 16 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    internal struct Il2CppType
    {
        /// <summary>
        /// Union pointer — in v29+ this is Il2CppTypeDefinition* (direct pointer).
        /// For injected classes: points to our fake TypeDefinition copy.
        /// </summary>
        [FieldOffset(0x00)] public IntPtr Data;

        /// <summary>
        /// Packed bitfield: attrs (16 bits), type enum (8 bits), mods (3 bits),
        /// byref (1 bit), pinned (1 bit), valuetype (1 bit), pad (2 bits).
        /// </summary>
        [FieldOffset(0x08)] public uint Attrs;

        /// <summary>Get the type enum value (IL2CPP_TYPE_CLASS, IL2CPP_TYPE_VALUETYPE, etc.).</summary>
        public byte Type
        {
            get => (byte)((Attrs >> 16) & 0xFF);
            set => Attrs = (Attrs & 0xFF00FFFF) | ((uint)value << 16);
        }

        /// <summary>Get/set the byref flag.</summary>
        public bool ByRef
        {
            get => ((Attrs >> 27) & 1) != 0;
            set
            {
                if (value)
                    Attrs |= (1u << 27);
                else
                    Attrs &= ~(1u << 27);
            }
        }
    }

    // ==============================
    // Il2CppClass_1 (184 bytes, offset 0x00 of Il2CppClass)
    // ==============================

    /// <summary>
    /// First part of Il2CppClass — identity, type args, parent, methods, etc.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 0xB8)]
    internal struct Il2CppClass_1
    {
        [FieldOffset(0x00)] public IntPtr image;                  // Il2CppImage*
        [FieldOffset(0x08)] public IntPtr gc_desc;
        [FieldOffset(0x10)] public IntPtr name;                   // const char*
        [FieldOffset(0x18)] public IntPtr namespaze;              // const char*
        [FieldOffset(0x20)] public Il2CppType byval_arg;          // 16 bytes
        [FieldOffset(0x30)] public Il2CppType this_arg;           // 16 bytes
        [FieldOffset(0x40)] public IntPtr element_class;          // Il2CppClass*
        [FieldOffset(0x48)] public IntPtr castClass;              // Il2CppClass*
        [FieldOffset(0x50)] public IntPtr declaringType;          // Il2CppClass*
        [FieldOffset(0x58)] public IntPtr parent;                 // Il2CppClass*
        [FieldOffset(0x60)] public IntPtr generic_class;          // Il2CppGenericClass*
        [FieldOffset(0x68)] public IntPtr typeMetadataHandle;     // Il2CppTypeDefinition* (v29+)
        [FieldOffset(0x70)] public IntPtr interopData;
        [FieldOffset(0x78)] public IntPtr klass;                  // Self-pointer
        [FieldOffset(0x80)] public IntPtr fields;                 // FieldInfo*
        [FieldOffset(0x88)] public IntPtr events;
        [FieldOffset(0x90)] public IntPtr properties;
        [FieldOffset(0x98)] public IntPtr methods;                // Il2CppMethodInfo**
        [FieldOffset(0xA0)] public IntPtr nestedTypes;            // Il2CppClass**
        [FieldOffset(0xA8)] public IntPtr implementedInterfaces;  // Il2CppClass**
        [FieldOffset(0xB0)] public IntPtr interfaceOffsets;       // Il2CppRuntimeInterfaceOffsetPair*
    }

    // ==============================
    // Il2CppClass_2 (112 bytes, offset 0xC8 of Il2CppClass)
    // Unity 2021+ layout WITH stack_slot_size at 0x34
    // ==============================

    /// <summary>
    /// Second part of Il2CppClass — hierarchy, sizes, flags, counts.
    /// Starts at Il2CppClass + 0xC8.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 0x70)]
    internal struct Il2CppClass_2
    {
        [FieldOffset(0x00)] public IntPtr typeHierarchy;           // Il2CppClass**
        [FieldOffset(0x08)] public IntPtr unity_user_data;
        [FieldOffset(0x10)] public uint initializationExceptionGCHandle;
        [FieldOffset(0x14)] public uint cctor_started;
        [FieldOffset(0x18)] public uint cctor_finished;
        [FieldOffset(0x20)] public ulong cctor_thread;             // size_t on x64
        [FieldOffset(0x28)] public IntPtr genericContainerHandle;
        [FieldOffset(0x30)] public uint instance_size;
        [FieldOffset(0x34)] public uint stack_slot_size;           // Unity 2021+ ONLY
        [FieldOffset(0x38)] public uint actualSize;
        [FieldOffset(0x3C)] public uint element_size;
        [FieldOffset(0x40)] public int native_size;
        [FieldOffset(0x44)] public uint static_fields_size;
        [FieldOffset(0x48)] public uint thread_static_fields_size;
        [FieldOffset(0x4C)] public int thread_static_fields_offset;
        [FieldOffset(0x50)] public uint flags;
        [FieldOffset(0x54)] public uint token;
        [FieldOffset(0x58)] public ushort method_count;
        [FieldOffset(0x5A)] public ushort property_count;
        [FieldOffset(0x5C)] public ushort field_count;
        [FieldOffset(0x5E)] public ushort event_count;
        [FieldOffset(0x60)] public ushort nested_type_count;
        [FieldOffset(0x62)] public ushort vtable_count;
        [FieldOffset(0x64)] public ushort interfaces_count;
        [FieldOffset(0x66)] public ushort interface_offsets_count;
        [FieldOffset(0x68)] public byte typeHierarchyDepth;
        [FieldOffset(0x69)] public byte genericRecursionDepth;
        [FieldOffset(0x6A)] public byte rank;
        [FieldOffset(0x6B)] public byte minimumAlignment;
        [FieldOffset(0x6C)] public byte naturalAligment;
        [FieldOffset(0x6D)] public byte packingSize;
        [FieldOffset(0x6E)] public byte bitflags1;                // initialized_and_no_error | initialized | size_inited
        [FieldOffset(0x6F)] public byte bitflags2;                // has_finalize | is_vtable_initialized
    }

    // ==============================
    // Il2CppMethodInfo (~88 bytes)
    // ==============================

    /// <summary>
    /// IL2CPP method metadata structure.
    /// Unity 2021+ layout: virtualMethodPointer is at 0x08 (after methodPointer),
    /// shifting all subsequent fields by +8 compared to older IL2CPP versions.
    /// The bridge struct incorrectly puts it at 0x38 but works via API functions.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 0x58)]
    internal struct Il2CppMethodInfo
    {
        [FieldOffset(0x00)] public IntPtr methodPointer;           // Native function pointer
        [FieldOffset(0x08)] public IntPtr virtualMethodPointer;    // Virtual method pointer (Unity 2021+)
        [FieldOffset(0x10)] public IntPtr invokerMethod;           // Invoker for il2cpp_runtime_invoke
        [FieldOffset(0x18)] public IntPtr name;                    // const char*
        [FieldOffset(0x20)] public IntPtr klass;                   // Il2CppClass*
        [FieldOffset(0x28)] public IntPtr returnType;              // Il2CppType*
        [FieldOffset(0x30)] public IntPtr parameters;              // Il2CppParameterInfo* or Il2CppType**
        [FieldOffset(0x38)] public IntPtr rgctxOrMethodDefinition; // Union: rgctx_data / methodDefinition
        [FieldOffset(0x40)] public IntPtr genericMethodOrContainer; // Union: genericMethod / genericContainer
        [FieldOffset(0x48)] public uint token;
        [FieldOffset(0x4C)] public ushort flags;
        [FieldOffset(0x4E)] public ushort flags2;
        [FieldOffset(0x50)] public ushort slot;
        [FieldOffset(0x52)] public byte argsCount;
        // Bitfields follow: generic(1), inflated(1), wrapperType(1), marshaledFromNative(1)...
    }

    // ==============================
    // VirtualInvokeData (16 bytes)
    // ==============================

    /// <summary>
    /// Entry in the Il2CppClass vtable.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    internal struct VirtualInvokeData
    {
        [FieldOffset(0x00)] public IntPtr methodPtr;  // The actual function pointer
        [FieldOffset(0x08)] public IntPtr method;     // Il2CppMethodInfo*
    }

    // ==============================
    // Il2CppObject (16 bytes)
    // ==============================

    /// <summary>
    /// Base IL2CPP object header.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    internal struct Il2CppObject
    {
        [FieldOffset(0x00)] public IntPtr klass;     // Il2CppClass*
        [FieldOffset(0x08)] public IntPtr monitor;   // Il2CppMonitor*
    }

    // ==============================
    // Helper methods for reading/writing structs from raw memory
    // ==============================

    internal static class Il2CppMemory
    {
        /// <summary>Read an Il2CppClass_1 from a class pointer.</summary>
        public static Il2CppClass_1 ReadClass1(IntPtr classPtr)
        {
            return Marshal.PtrToStructure<Il2CppClass_1>(classPtr);
        }

        /// <summary>Read Il2CppClass_2 from a class pointer (at offset 0xC8).</summary>
        public static Il2CppClass_2 ReadClass2(IntPtr classPtr)
        {
            return Marshal.PtrToStructure<Il2CppClass_2>(classPtr + Il2CppConstants.CLASS_2_OFFSET);
        }

        /// <summary>Read a VirtualInvokeData from the vtable at a given slot index.</summary>
        public static VirtualInvokeData ReadVtableEntry(IntPtr classPtr, int slotIndex)
        {
            IntPtr vtableBase = classPtr + Il2CppConstants.VTABLE_OFFSET;
            return Marshal.PtrToStructure<VirtualInvokeData>(vtableBase + slotIndex * 16);
        }

        /// <summary>Write a VirtualInvokeData to the vtable at a given slot index.</summary>
        public static void WriteVtableEntry(IntPtr classPtr, int slotIndex, VirtualInvokeData entry)
        {
            IntPtr vtableBase = classPtr + Il2CppConstants.VTABLE_OFFSET;
            Marshal.StructureToPtr(entry, vtableBase + slotIndex * 16, false);
        }

        /// <summary>Read an Il2CppMethodInfo from a pointer.</summary>
        public static Il2CppMethodInfo ReadMethodInfo(IntPtr methodPtr)
        {
            return Marshal.PtrToStructure<Il2CppMethodInfo>(methodPtr);
        }

        /// <summary>Read a C string from a native pointer.</summary>
        public static string ReadCString(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero) return null;
            return Marshal.PtrToStringAnsi(ptr);
        }
    }
}
