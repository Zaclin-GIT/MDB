// ==============================
// ClassInjector — IL2CPP runtime class injection
// ==============================
// Creates a fake MonoBehaviour subclass in the IL2CPP runtime by:
//   1. Copying MonoBehaviour's full class memory (including vtable)
//   2. Overriding identity fields (name, namespace, image, types)
//   3. Creating method infos with managed trampolines
//   4. Registering via InjectorHelpers hooks
//
// This is the managed C# equivalent of the C++ class_injector.cpp,
// using the fake TypeDefinition approach (not negative tokens)
// to avoid v29+ crashes where type->data is dereferenced directly.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using GameSDK.ModHost;

namespace GameSDK.Injection
{
    /// <summary>
    /// Injects a fake MonoBehaviour subclass into the IL2CPP runtime.
    /// </summary>
    internal static class ClassInjector
    {
        private static readonly ModLogger _logger = new ModLogger("INJECT");

        // Size of fake TypeDefinition copy (generous to cover all fields)
        private const int TYPE_DEF_COPY_SIZE = 256;

        // ==============================
        // Trampoline delegates
        // ==============================

        // IL2CPP instance method with 0 params: void Method(Il2CppObject* this, MethodInfo* method)
        // On x64 Windows: __fastcall → RCX=this, RDX=methodInfo
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void InstanceMethodDelegate(IntPtr thisPtr, IntPtr methodInfo);

        // v29 invoker: void Invoke(void* methodPointer, MethodInfo* method, void* obj, void** args, void* returnValue)
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate void InvokerDelegate(IntPtr methodPointer, IntPtr methodInfo, IntPtr obj, IntPtr args, IntPtr returnValue);

        // Pinned delegates (prevent GC collection)
        private static readonly List<GCHandle> s_pinnedDelegates = new List<GCHandle>();

        // Trampoline delegate instances
        private static InstanceMethodDelegate s_trampoline_ctor;
        private static InstanceMethodDelegate s_trampoline_finalize;
        private static InstanceMethodDelegate s_trampoline_update;
        private static InstanceMethodDelegate s_trampoline_fixedUpdate;
        private static InstanceMethodDelegate s_trampoline_lateUpdate;
        private static InvokerDelegate s_invoker;

        // Function pointers for the trampolines
        private static IntPtr s_ctorFnPtr;
        private static IntPtr s_finalizeFnPtr;
        private static IntPtr s_updateFnPtr;
        private static IntPtr s_fixedUpdateFnPtr;
        private static IntPtr s_lateUpdateFnPtr;
        private static IntPtr s_invokerFnPtr;

        // ==============================
        // Callbacks (set by MDBRunner)
        // ==============================

        /// <summary>Called on Unity Update tick.</summary>
        public static Action OnUpdateCallback;

        /// <summary>Called on Unity FixedUpdate tick.</summary>
        public static Action OnFixedUpdateCallback;

        /// <summary>Called on Unity LateUpdate tick.</summary>
        public static Action OnLateUpdateCallback;

        // ==============================
        // Trampoline implementations
        // ==============================

        private static void Trampoline_Ctor(IntPtr thisPtr, IntPtr methodInfo)
        {
            // No-op — Unity calls this after il2cpp_object_new
        }

        private static void Trampoline_Finalize(IntPtr thisPtr, IntPtr methodInfo)
        {
            // No-op — prevents GC crash
        }

        private static void Trampoline_Update(IntPtr thisPtr, IntPtr methodInfo)
        {
            try
            {
                OnUpdateCallback?.Invoke();
            }
            catch (Exception ex)
            {
                // Don't let exceptions escape to native code
                System.Console.WriteLine($"[INJECT] Update exception: {ex.Message}");
            }
        }

        private static void Trampoline_FixedUpdate(IntPtr thisPtr, IntPtr methodInfo)
        {
            try
            {
                OnFixedUpdateCallback?.Invoke();
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[INJECT] FixedUpdate exception: {ex.Message}");
            }
        }

        private static void Trampoline_LateUpdate(IntPtr thisPtr, IntPtr methodInfo)
        {
            try
            {
                OnLateUpdateCallback?.Invoke();
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[INJECT] LateUpdate exception: {ex.Message}");
            }
        }

        /// <summary>
        /// v29 invoker: dispatches through methodPointer(obj, methodInfo).
        /// Required for il2cpp_runtime_invoke to work.
        /// </summary>
        private static void VoidMethodInvoker(IntPtr methodPointer, IntPtr methodInfo, IntPtr obj, IntPtr args, IntPtr returnValue)
        {
            if (methodPointer == IntPtr.Zero) return;

            // Call the actual method: fn(this, methodInfo)
            var fn = Marshal.GetDelegateForFunctionPointer<InstanceMethodDelegate>(methodPointer);
            fn(obj, methodInfo);
        }

        // ==============================
        // Initialization
        // ==============================

        private static void InitializeTrampolines()
        {
            if (s_ctorFnPtr != IntPtr.Zero) return; // Already initialized

            // Create delegate instances
            s_trampoline_ctor = new InstanceMethodDelegate(Trampoline_Ctor);
            s_trampoline_finalize = new InstanceMethodDelegate(Trampoline_Finalize);
            s_trampoline_update = new InstanceMethodDelegate(Trampoline_Update);
            s_trampoline_fixedUpdate = new InstanceMethodDelegate(Trampoline_FixedUpdate);
            s_trampoline_lateUpdate = new InstanceMethodDelegate(Trampoline_LateUpdate);
            s_invoker = new InvokerDelegate(VoidMethodInvoker);

            // Pin delegates to prevent GC
            s_pinnedDelegates.Add(GCHandle.Alloc(s_trampoline_ctor));
            s_pinnedDelegates.Add(GCHandle.Alloc(s_trampoline_finalize));
            s_pinnedDelegates.Add(GCHandle.Alloc(s_trampoline_update));
            s_pinnedDelegates.Add(GCHandle.Alloc(s_trampoline_fixedUpdate));
            s_pinnedDelegates.Add(GCHandle.Alloc(s_trampoline_lateUpdate));
            s_pinnedDelegates.Add(GCHandle.Alloc(s_invoker));

            // Get function pointers
            s_ctorFnPtr = Marshal.GetFunctionPointerForDelegate(s_trampoline_ctor);
            s_finalizeFnPtr = Marshal.GetFunctionPointerForDelegate(s_trampoline_finalize);
            s_updateFnPtr = Marshal.GetFunctionPointerForDelegate(s_trampoline_update);
            s_fixedUpdateFnPtr = Marshal.GetFunctionPointerForDelegate(s_trampoline_fixedUpdate);
            s_lateUpdateFnPtr = Marshal.GetFunctionPointerForDelegate(s_trampoline_lateUpdate);
            s_invokerFnPtr = Marshal.GetFunctionPointerForDelegate(s_invoker);

            _logger.Info($"[INJECT] Trampolines initialized:");
            _logger.Info($"[INJECT]   .ctor         @ 0x{s_ctorFnPtr.ToInt64():X}");
            _logger.Info($"[INJECT]   Finalize      @ 0x{s_finalizeFnPtr.ToInt64():X}");
            _logger.Info($"[INJECT]   Update        @ 0x{s_updateFnPtr.ToInt64():X}");
            _logger.Info($"[INJECT]   FixedUpdate   @ 0x{s_fixedUpdateFnPtr.ToInt64():X}");
            _logger.Info($"[INJECT]   LateUpdate    @ 0x{s_lateUpdateFnPtr.ToInt64():X}");
            _logger.Info($"[INJECT]   Invoker(v29)  @ 0x{s_invokerFnPtr.ToInt64():X}");
        }

        // ==============================
        // Core injection
        // ==============================

        /// <summary>
        /// Register a new MonoBehaviour subclass in the IL2CPP runtime.
        /// Pure memory allocation — safe from any thread.
        /// </summary>
        /// <param name="name">Class name (e.g., "MDBRunner").</param>
        /// <param name="ns">Namespace (e.g., "MDB.Internal").</param>
        /// <returns>Pointer to the new Il2CppClass, or IntPtr.Zero on failure.</returns>
        public static IntPtr RegisterMonoBehaviourSubclass(string name, string ns)
        {
            _logger.Info($"[INJECT] RegisterMonoBehaviourSubclass({ns}.{name})");

            try
            {
                // Initialize trampolines
                InitializeTrampolines();

                // Step 1: Find MonoBehaviour class
                IntPtr monoBehaviour = Il2CppBridge.mdb_find_class("UnityEngine.CoreModule", "UnityEngine", "MonoBehaviour");
                if (monoBehaviour == IntPtr.Zero)
                {
                    _logger.Error("[INJECT] Failed to find MonoBehaviour class!");
                    return IntPtr.Zero;
                }
                _logger.Info($"[INJECT] MonoBehaviour @ 0x{monoBehaviour.ToInt64():X}");

                // Step 2: Install hooks (idempotent)
                if (!InjectorHelpers.Setup())
                {
                    _logger.Error("[INJECT] InjectorHelpers.Setup() failed!");
                    return IntPtr.Zero;
                }

                // Step 3: Force parent class init and disable GC
                Il2CppExports.RuntimeClassInit(monoBehaviour);
                Il2CppExports.GcDisable();

                try
                {
                    return CreateClass(monoBehaviour, name, ns);
                }
                finally
                {
                    // Step 16: Re-enable GC
                    Il2CppExports.GcEnable();
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[INJECT] RegisterMonoBehaviourSubclass failed: {ex}");
                return IntPtr.Zero;
            }
        }

        private static IntPtr CreateClass(IntPtr parentClass, string name, string ns)
        {
            // Step 4: Read parent class layout
            Il2CppClass_2 parentPart2 = Il2CppMemory.ReadClass2(parentClass);
            ushort vtableCount = parentPart2.vtable_count;
            int totalClassSize = Il2CppConstants.VTABLE_OFFSET + vtableCount * 16;

            _logger.Info($"[INJECT] Parent layout: instance_size={parentPart2.instance_size}, vtable_count={vtableCount}, total={totalClassSize}");

            // Step 5: Allocate new class — full copy of parent including vtable
            IntPtr newClass = Marshal.AllocHGlobal(totalClassSize);
            unsafe
            {
                // Zero the allocation
                byte* p = (byte*)newClass.ToPointer();
                for (int i = 0; i < totalClassSize; i++) p[i] = 0;

                // Copy parent's full memory
                byte* src = (byte*)parentClass.ToPointer();
                for (int i = 0; i < totalClassSize; i++) p[i] = src[i];
            }
            _logger.Info($"[INJECT] New class allocated @ 0x{newClass.ToInt64():X} (size={totalClassSize})");

            // Step 5b: Populate fake image with parent's internal metadata
            // The fake image was created with only name/assembly/dynamic set.
            // Internal fields (typeStart, typeCount, codeGenModule, etc.) are all NULL/zero.
            // Unity's AddComponent code path reads image->codeGenModule (+0x28) as a pointer
            // and dereferences it — crash if NULL. Copy parent's image internals so the fake
            // image has valid metadata pointers while keeping our custom name/assembly/dynamic.
            IntPtr parentImage = Marshal.ReadIntPtr(parentClass, 0x00); // parent's Il2CppImage*
            if (parentImage != IntPtr.Zero)
            {
                unsafe
                {
                    byte* dst = (byte*)InjectorHelpers.FakeImage.ToPointer();
                    byte* src = (byte*)parentImage.ToPointer();
                    // Copy bytes 0x18 through 0x43 (internal metadata fields):
                    //   0x18: typeStart (4), 0x1C: typeCount (4),
                    //   0x20: exportedTypeStart (4), 0x24: exportedTypeCount (4),
                    //   0x28: codeGenModule (8), 0x30+: other internal fields
                    // Skip 0x00-0x17 (name, nameNoExt, assembly — already set)
                    // Skip 0x44+ (dynamic flag — already set to 1)
                    for (int i = 0x18; i < 0x44; i++) dst[i] = src[i];
                }
                _logger.Info($"[INJECT] Copied image internals from parent image @ 0x{parentImage.ToInt64():X}");
                _logger.Info($"[INJECT]   codeGenModule = 0x{Marshal.ReadIntPtr(InjectorHelpers.FakeImage, 0x28).ToInt64():X}");
            }
            else
            {
                _logger.Warning("[INJECT] Parent image is NULL — cannot populate fake image internals");
            }

            // Step 6: Override identity fields
            IntPtr namePtr = Marshal.StringToHGlobalAnsi(name);
            IntPtr nsPtr = Marshal.StringToHGlobalAnsi(ns);

            Marshal.WriteIntPtr(newClass, 0x10, namePtr);                // name
            Marshal.WriteIntPtr(newClass, 0x18, nsPtr);                  // namespaze
            Marshal.WriteIntPtr(newClass, 0x00, InjectorHelpers.FakeImage); // image
            Marshal.WriteIntPtr(newClass, 0x78, newClass);               // klass = self
            Marshal.WriteIntPtr(newClass, 0x40, newClass);               // element_class = self
            Marshal.WriteIntPtr(newClass, 0x48, newClass);               // castClass = self
            Marshal.WriteIntPtr(newClass, 0x58, parentClass);            // parent = MonoBehaviour (NOT Behaviour!)

            // Diagnostic: verify parent is correct
            IntPtr parentParent = Marshal.ReadIntPtr(parentClass, 0x58);
            string parentParentName = "(?)";
            try { parentParentName = Il2CppMemory.ReadCString(Marshal.ReadIntPtr(parentParent, 0x10)) ?? "(?)"; } catch {}
            _logger.Info($"[INJECT] Identity set: name={name}, ns={ns}");
            _logger.Info($"[INJECT] parent → 0x{parentClass.ToInt64():X} (MonoBehaviour), parent.parent → 0x{parentParent.ToInt64():X} ({parentParentName})");

            // Step 7: Set type identity — negative token approach (matching Il2CppInterop)
            // Instead of a fake TypeDefinition pointer, use a negative integer token
            // in byval_arg.data and this_arg.data. All internal IL2CPP code paths that
            // read type->data go through either FromIl2CppType or GetTypeInfoFromTypeDefinitionIndex,
            // both of which we hook to intercept negative tokens.
            long token = InjectorHelpers.CreateClassToken(newClass);
            IntPtr tokenAsPtr = new IntPtr(token); // e.g. -2 → 0xFFFFFFFFFFFFFFFE
            _logger.Info($"[INJECT] Class token: {token} (0x{tokenAsPtr.ToInt64():X})");

            // Write byval_arg (offset 0x20, 16 bytes)
            // Il2CppType: Data (8 bytes) + Attrs packed (4 bytes)
            Marshal.WriteIntPtr(newClass, 0x20, tokenAsPtr); // byval_arg.data = negative token
            // Set type enum = IL2CPP_TYPE_CLASS (0x12), byref = 0
            // Attrs layout: attrs(16) | type(8) | pad(3) | byref(1) | pinned(1) | valuetype(1) | pad(2)
            // For class: type=0x12, byref=0 → packed = 0x00120000
            uint byvalAttrs = ((uint)Il2CppConstants.IL2CPP_TYPE_CLASS << 16);
            Marshal.WriteInt32(newClass, 0x28, (int)byvalAttrs);

            // Write this_arg (offset 0x30, 16 bytes) — same but byref = 1
            Marshal.WriteIntPtr(newClass, 0x30, tokenAsPtr); // this_arg.data = same negative token
            uint thisAttrs = ((uint)Il2CppConstants.IL2CPP_TYPE_CLASS << 16) | (1u << 27);
            Marshal.WriteInt32(newClass, 0x38, (int)thisAttrs);

            // Keep typeMetadataHandle as parent's original value (from memcpy)
            // Don't override offset 0x68 — it points to MonoBehaviour's real TypeDefinition
            IntPtr parentTypeMetadataHandle = Marshal.ReadIntPtr(newClass, 0x68);
            _logger.Info($"[INJECT] typeMetadataHandle (inherited) @ 0x{parentTypeMetadataHandle.ToInt64():X}");

            // DO NOT write negative token to Il2CppClass_2.token (offset 0xC8 + 0x54 = 0x11C)
            // Unity's AddComponent reads klass->token as a metadata index. Writing -2 here
            // causes it to index into a table with 0xFFFFFFFE, returning NULL → crash.
            // Keep MonoBehaviour's original token value (from memcpy) — it's valid and
            // the classloader code that uses it goes through our FromIl2CppType hook.
            uint inheritedToken = (uint)Marshal.ReadInt32(newClass, Il2CppConstants.CLASS_2_OFFSET + 0x54);
            _logger.Info($"[INJECT] Keeping inherited token: 0x{inheritedToken:X}");

            // Step 8: Create method infos (was step 9)
            IntPtr voidReturnType = GetVoidType(parentClass);
            CreateMethodInfos(newClass, voidReturnType);

            // Step 12: Override vtable entries
            OverrideVtableEntries(newClass, parentClass, vtableCount);

            // Step 13: Build typeHierarchy
            BuildTypeHierarchy(newClass, parentClass);

            // Step 14: Set bitflags
            int class2Base = Il2CppConstants.CLASS_2_OFFSET;

            // Read parent flags and clear abstract bit
            uint flags = (uint)Marshal.ReadInt32(newClass, class2Base + 0x50);
            flags &= ~0x80u; // Clear abstract
            Marshal.WriteInt32(newClass, class2Base + 0x50, (int)flags);

            // Read parent bitflags (inherited via memcpy) — OR in needed bits, don't overwrite
            byte bitflags1 = Marshal.ReadByte(newClass, class2Base + 0x6E);
            byte bitflags2 = Marshal.ReadByte(newClass, class2Base + 0x6F);
            _logger.Info($"[INJECT] Parent bitflags: bf1=0x{bitflags1:X2} bf2=0x{bitflags2:X2}");

            // Standard IL2CPP bit layout (C compiler LSB-first packing):
            // bf1: bit0=initialized, bit1=enumtype, bit2=is_generic, bit3=has_references,
            //      bit4=init_pending, bit5=size_inited, bit6=has_finalize, bit7=has_cctor
            // bf2: bit0=is_blittable, bit1=is_import/winrt, bit2=is_vtable_initialized,
            //      bit3=has_initialization_error, bit4=initialized_and_no_error
            // Parent (MonoBehaviour) has bf1=0x21 = initialized(0x01) + size_inited(0x20)
            // We add: has_finalize(0x40) since we override Finalize
            bitflags1 |= 0x40; // Add has_finalize (bit 6)
            // Parent has bf2=0x00. We need vtable_initialized + initialized_and_no_error
            bitflags2 |= 0x14; // Add is_vtable_initialized (bit 2) + initialized_and_no_error (bit 4)

            Marshal.WriteByte(newClass, class2Base + 0x6E, bitflags1);
            Marshal.WriteByte(newClass, class2Base + 0x6F, bitflags2);

            _logger.Info($"[INJECT] Flags set: flags=0x{flags:X}, bitflags1=0x{bitflags1:X2}, bitflags2=0x{bitflags2:X2}");

            // Step 15: Register
            InjectorHelpers.AddTypeToLookup(newClass, ns, name);

            _logger.Info($"[INJECT] Class allocated: {ns}.{name} @ 0x{newClass.ToInt64():X} (size={totalClassSize} vtable={vtableCount})");

            return newClass;
        }

        // ==============================
        // Method info creation
        // ==============================

        private static void CreateMethodInfos(IntPtr newClass, IntPtr voidReturnType)
        {
            const int METHOD_INFO_SIZE = 0x58; // 88 bytes

            // Allocate 5 method infos
            IntPtr[] methods = new IntPtr[5];
            string[] methodNames = { ".ctor", "Finalize", "Update", "FixedUpdate", "LateUpdate" };
            IntPtr[] methodFnPtrs = { s_ctorFnPtr, s_finalizeFnPtr, s_updateFnPtr, s_fixedUpdateFnPtr, s_lateUpdateFnPtr };
            ushort[] methodFlags = {
                0x1886, // .ctor: RTSpecialName | SpecialName | HideBySig | Public
                0x00C4, // Finalize: Family | Virtual | HideBySig
                0x0086, // Update: HideBySig | Public | Virtual
                0x0086, // FixedUpdate: HideBySig | Public | Virtual
                0x0086  // LateUpdate: HideBySig | Public | Virtual
            };
            ushort[] methodSlots = {
                0xFFFF, // .ctor — no slot
                0xFFFF, // Finalize — will be set in vtable override
                0xFFFF, // Update — will be set in vtable override
                0xFFFF, // FixedUpdate
                0xFFFF  // LateUpdate
            };

            for (int i = 0; i < 5; i++)
            {
                IntPtr mi = Marshal.AllocHGlobal(METHOD_INFO_SIZE);
                unsafe
                {
                    byte* p = (byte*)mi.ToPointer();
                    for (int j = 0; j < METHOD_INFO_SIZE; j++) p[j] = 0;
                }

                IntPtr namePtr = Marshal.StringToHGlobalAnsi(methodNames[i]);

                // Unity 2021+ MethodInfo layout — virtualMethodPointer at 0x08 shifts everything
                Marshal.WriteIntPtr(mi, 0x00, methodFnPtrs[i]);    // methodPointer
                Marshal.WriteIntPtr(mi, 0x08, methodFnPtrs[i]);    // virtualMethodPointer (same as methodPointer)
                Marshal.WriteIntPtr(mi, 0x10, s_invokerFnPtr);     // invokerMethod (v29 invoker)
                Marshal.WriteIntPtr(mi, 0x18, namePtr);             // name
                Marshal.WriteIntPtr(mi, 0x20, newClass);            // klass
                Marshal.WriteIntPtr(mi, 0x28, voidReturnType);      // returnType
                // 0x30: parameters = IntPtr.Zero (already zeroed — 0 params)
                Marshal.WriteInt16(mi, 0x4C, (short)methodFlags[i]); // flags
                Marshal.WriteInt16(mi, 0x50, (short)methodSlots[i]); // slot
                Marshal.WriteByte(mi, 0x52, 0);                      // argsCount = 0

                methods[i] = mi;

                _logger.Info($"[INJECT] MethodInfo[{i}] '{methodNames[i]}' @ 0x{mi.ToInt64():X} ptr=0x{methodFnPtrs[i].ToInt64():X}");
            }

            // Step 10: Build methods array (pointer array)
            IntPtr methodsArray = Marshal.AllocHGlobal(5 * IntPtr.Size);
            for (int i = 0; i < 5; i++)
            {
                Marshal.WriteIntPtr(methodsArray, i * IntPtr.Size, methods[i]);
            }

            // Write to class offset 0x98 (methods pointer)
            Marshal.WriteIntPtr(newClass, 0x98, methodsArray);

            // Step 11: Set method_count in Il2CppClass_2 at offset 0xC8 + 0x58
            Marshal.WriteInt16(newClass, Il2CppConstants.CLASS_2_OFFSET + 0x58, 5);

            // Also zero out field/event/property/nested type counts so the runtime
            // doesn't try to enumerate parent's entries through our class
            Marshal.WriteInt16(newClass, Il2CppConstants.CLASS_2_OFFSET + 0x5A, 0); // property_count
            Marshal.WriteInt16(newClass, Il2CppConstants.CLASS_2_OFFSET + 0x5C, 0); // field_count
            Marshal.WriteInt16(newClass, Il2CppConstants.CLASS_2_OFFSET + 0x5E, 0); // event_count
            Marshal.WriteInt16(newClass, Il2CppConstants.CLASS_2_OFFSET + 0x60, 0); // nested_type_count

            // Zero out the pointers for fields/events/properties we don't have
            Marshal.WriteIntPtr(newClass, 0x80, IntPtr.Zero); // fields
            Marshal.WriteIntPtr(newClass, 0x88, IntPtr.Zero); // events
            Marshal.WriteIntPtr(newClass, 0x90, IntPtr.Zero); // properties
            Marshal.WriteIntPtr(newClass, 0xA0, IntPtr.Zero); // nestedTypes

            _logger.Info($"[INJECT] Methods array @ 0x{methodsArray.ToInt64():X}, count=5");
        }

        // ==============================
        // Vtable override
        // ==============================

        private static void OverrideVtableEntries(IntPtr newClass, IntPtr parentClass, ushort vtableCount)
        {
            _logger.Info($"[INJECT] Scanning vtable ({vtableCount} entries) for override targets...");

            // Diagnostic: dump ALL vtable entries to understand what's there
            // First, probe vtable[0] at every 8-byte offset to find the name field
            if (vtableCount > 0)
            {
                VirtualInvokeData probe = Il2CppMemory.ReadVtableEntry(newClass, 0);
                if (probe.method != IntPtr.Zero)
                {
                    _logger.Info($"[INJECT] Probing vtable[0] method @ 0x{probe.method.ToInt64():X} for name field:");
                    for (int off = 0; off <= 0x48; off += 0x08)
                    {
                        IntPtr val = Marshal.ReadIntPtr(probe.method, off);
                        string attempt = "(null)";
                        bool isCodeAddr = (val.ToInt64() >> 40) == 0x7FF9 >> 8; // heuristic: high addresses are code
                        if (val != IntPtr.Zero && !isCodeAddr)
                        {
                            try
                            {
                                string raw = Marshal.PtrToStringAnsi(val);
                                if (raw != null && raw.Length > 0 && raw.Length < 120)
                                    attempt = raw;
                                else
                                    attempt = $"(len={raw?.Length ?? -1})";
                            }
                            catch { attempt = "(unreadable)"; }
                        }
                        else if (isCodeAddr)
                        {
                            attempt = "(code addr)";
                        }
                        _logger.Info($"[INJECT]   [0x{off:X2}] = 0x{val.ToInt64():X} → {attempt}");
                    }
                }
            }

            for (int slot = 0; slot < vtableCount; slot++)
            {
                VirtualInvokeData vid = Il2CppMemory.ReadVtableEntry(newClass, slot);
                string diagName = "(null method)";
                IntPtr diagKlass = IntPtr.Zero;
                if (vid.method != IntPtr.Zero)
                {
                    IntPtr namePtr = Marshal.ReadIntPtr(vid.method, 0x18); // name at 0x18 (Unity 2021+)
                    diagName = Il2CppMemory.ReadCString(namePtr) ?? "(null name)";
                    diagKlass = Marshal.ReadIntPtr(vid.method, 0x20); // klass at 0x20
                }
                _logger.Info($"[INJECT]   vtable[{slot}]: method=0x{vid.method.ToInt64():X} ptr=0x{vid.methodPtr.ToInt64():X} klass=0x{diagKlass.ToInt64():X} name='{diagName}'");
            }

            // Only Finalize needs vtable override — Update/FixedUpdate/LateUpdate
            // are Unity messages (NOT virtual methods), found via method name lookup.
            IntPtr methodsArrayPtr = Marshal.ReadIntPtr(newClass, 0x98);
            bool finalizeOverridden = false;

            for (int slot = 0; slot < vtableCount; slot++)
            {
                VirtualInvokeData vid = Il2CppMemory.ReadVtableEntry(newClass, slot);
                if (vid.method == IntPtr.Zero) continue;

                IntPtr methodNamePtr = Marshal.ReadIntPtr(vid.method, 0x18); // name at 0x18 (Unity 2021+)
                string methodName = Il2CppMemory.ReadCString(methodNamePtr);
                if (methodName == null) continue;

                if (methodName == "Finalize")
                {
                    // Override Finalize vtable entry
                    IntPtr ourFinalizeMethodInfo = Marshal.ReadIntPtr(methodsArrayPtr, 1 * IntPtr.Size); // index 1 = Finalize
                    Marshal.WriteInt16(ourFinalizeMethodInfo, 0x50, (short)slot);

                    VirtualInvokeData newVid;
                    newVid.methodPtr = s_finalizeFnPtr;
                    newVid.method = ourFinalizeMethodInfo;
                    Il2CppMemory.WriteVtableEntry(newClass, slot, newVid);

                    _logger.Info($"[INJECT] Vtable[{slot}] overridden: Finalize → 0x{s_finalizeFnPtr.ToInt64():X}");
                    finalizeOverridden = true;
                    break;
                }
            }

            if (!finalizeOverridden)
            {
                _logger.Warning("[INJECT] Finalize NOT found in vtable — scanning parent class directly for comparison");
                // Diagnostic: scan parent class vtable too
                for (int slot = 0; slot < vtableCount; slot++)
                {
                    VirtualInvokeData vid = Il2CppMemory.ReadVtableEntry(parentClass, slot);
                    string pName = "(null)";
                    if (vid.method != IntPtr.Zero)
                    {
                        IntPtr np = Marshal.ReadIntPtr(vid.method, 0x18); // name at 0x18 (Unity 2021+)
                        pName = Il2CppMemory.ReadCString(np) ?? "(null name)";
                    }
                    _logger.Info($"[INJECT]   parent vtable[{slot}]: method=0x{vid.method.ToInt64():X} name='{pName}'");
                }
            }

            _logger.Info($"[INJECT] Update/FixedUpdate/LateUpdate are Unity messages, not virtual — registered via methods array only");
        }

        // ==============================
        // Type hierarchy
        // ==============================

        private static void BuildTypeHierarchy(IntPtr newClass, IntPtr parentClass)
        {
            int class2Offset = Il2CppConstants.CLASS_2_OFFSET;

            // Read parent's hierarchy depth
            byte parentDepth = Marshal.ReadByte(parentClass, class2Offset + 0x68);
            byte newDepth = (byte)(parentDepth + 1);

            // Read parent's typeHierarchy pointer
            IntPtr parentHierarchy = Marshal.ReadIntPtr(parentClass, class2Offset);

            // Allocate new hierarchy: (parentDepth + 1) pointers
            int hierarchySize = newDepth * IntPtr.Size;
            IntPtr newHierarchy = Marshal.AllocHGlobal(hierarchySize);

            // Copy parent's hierarchy entries
            for (int i = 0; i < parentDepth; i++)
            {
                IntPtr entry = Marshal.ReadIntPtr(parentHierarchy, i * IntPtr.Size);
                Marshal.WriteIntPtr(newHierarchy, i * IntPtr.Size, entry);
            }

            // Append self
            Marshal.WriteIntPtr(newHierarchy, parentDepth * IntPtr.Size, newClass);

            // Write to new class
            Marshal.WriteIntPtr(newClass, class2Offset, newHierarchy);          // typeHierarchy
            Marshal.WriteByte(newClass, class2Offset + 0x68, newDepth);          // typeHierarchyDepth

            _logger.Info($"[INJECT] TypeHierarchy built: depth {parentDepth} → {newDepth}");
        }

        // ==============================
        // Helpers
        // ==============================

        /// <summary>
        /// Get a void Il2CppType* pointer by finding a void-returning method on the parent class.
        /// </summary>
        private static IntPtr GetVoidType(IntPtr parentClass)
        {
            // Try to find a method with void return type from the parent
            // We can use any method — .ctor always returns void
            IntPtr ctorMethod = Il2CppBridge.mdb_get_method(parentClass, ".ctor", 0);
            if (ctorMethod != IntPtr.Zero)
            {
                IntPtr returnType = Il2CppBridge.mdb_method_get_return_type(ctorMethod);
                if (returnType != IntPtr.Zero)
                {
                    _logger.Info($"[INJECT] Void type found via .ctor return type @ 0x{returnType.ToInt64():X}");
                    return returnType;
                }
            }

            // Fallback: try Finalize
            IntPtr finalizeMethod = Il2CppBridge.mdb_get_method(parentClass, "Finalize", 0);
            if (finalizeMethod != IntPtr.Zero)
            {
                IntPtr returnType = Il2CppBridge.mdb_method_get_return_type(finalizeMethod);
                if (returnType != IntPtr.Zero)
                {
                    _logger.Info($"[INJECT] Void type found via Finalize return type @ 0x{returnType.ToInt64():X}");
                    return returnType;
                }
            }

            _logger.Warning("[INJECT] Could not find void Il2CppType — methods may not work correctly");
            return IntPtr.Zero;
        }
    }
}
