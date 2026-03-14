// ==============================
// InjectorHelpers — Hook management, fake assembly/image, token tracking
// ==============================
// Manages the three hooks on IL2CPP internal functions:
//   - Class::FromIl2CppType
//   - Class::FromName
//   - MetadataCache::GetTypeInfoFromTypeDefinitionIndex
// Also creates and manages the fake assembly/image for injected classes.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using GameSDK.ModHost;

namespace GameSDK.Injection
{
    /// <summary>
    /// Static class managing hooks, fake assembly/image, and registration
    /// of injected IL2CPP classes.
    /// </summary>
    internal static class InjectorHelpers
    {
        private static readonly ModLogger _logger = new ModLogger("INJECT");

        /// <summary>Log a message only when ClassInjector.VerboseLogging is enabled.</summary>
        private static void LogVerbose(string message)
        {
            if (ClassInjector.VerboseLogging)
                _logger.Info(message);
        }

        // ==============================
        // Token tracking
        // ==============================

        /// <summary>Map of (negative) token → injected class pointer.</summary>
        private static readonly ConcurrentDictionary<long, IntPtr> s_InjectedClasses = new ConcurrentDictionary<long, IntPtr>();

        // (s_FakeTypeDefs removed — now using negative tokens in s_InjectedClasses)

        // ==============================
        // Name lookup
        // ==============================

        /// <summary>
        /// Map of (namespace, name, imagePtr) → class pointer for FromName hook.
        /// </summary>
        private static readonly Dictionary<(string ns, string name, IntPtr image), IntPtr> s_ClassNameLookup
            = new Dictionary<(string ns, string name, IntPtr image), IntPtr>();

        // ==============================
        // Fake assembly/image
        // ==============================

        private static IntPtr s_FakeImage = IntPtr.Zero;
        private static IntPtr s_FakeAssembly = IntPtr.Zero;
        private static IntPtr s_ImageNamePtr = IntPtr.Zero;

        /// <summary>Get the fake image pointer for injected classes.</summary>
        public static IntPtr FakeImage => s_FakeImage;

        // ==============================
        // Hook state
        // ==============================

        private static bool s_initialized = false;

        // Internal function addresses (resolved via xref from exports)
        private static IntPtr s_internalFromIl2CppType;
        private static IntPtr s_internalFromName;
        private static IntPtr s_internalGetTypeInfo;

        // Trampoline pointers (for calling original functions from hooks)
        private static IntPtr s_originalFromIl2CppType;
        private static IntPtr s_originalFromName;
        private static IntPtr s_originalGetTypeInfo;

        // Cached original delegates (created once at hook install, not per-call)
        private static FromIl2CppTypeDelegate s_cachedOriginalFromType;
        private static FromNameDelegate s_cachedOriginalFromName;
        private static GetTypeInfoDelegate s_cachedOriginalGetTypeInfo;

        // Hook handles (for cleanup)
        private static long s_hookFromIl2CppType;
        private static long s_hookFromName;
        private static long s_hookGetTypeInfo;

        // Pinned delegates to prevent GC
        private static GCHandle s_fromIl2CppTypeDelegateHandle;
        private static GCHandle s_fromNameDelegateHandle;
        private static GCHandle s_getTypeInfoDelegateHandle;

        // ==============================
        // Hook delegate types
        // ==============================

        /// <summary>
        /// Class::FromIl2CppType(Il2CppType* type, bool throwOnError) → Il2CppClass*
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr FromIl2CppTypeDelegate(IntPtr typePtr, [MarshalAs(UnmanagedType.U1)] bool throwOnError);

        /// <summary>
        /// Class::FromName(Il2CppImage* image, const char* ns, const char* name) → Il2CppClass*
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr FromNameDelegate(IntPtr imagePtr, IntPtr namespacePtr, IntPtr namePtr);

        /// <summary>
        /// MetadataCache::GetTypeInfoFromTypeDefinitionIndex(int32_t index) → Il2CppClass*
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr GetTypeInfoDelegate(int index);

        // ==============================
        // Setup (idempotent)
        // ==============================

        /// <summary>
        /// Resolve IL2CPP exports, find internal functions via xref scanning,
        /// install hooks. Idempotent — safe to call multiple times.
        /// </summary>
        public static bool Setup()
        {
            if (s_initialized) return true;

            LogVerbose("[INJECT] InjectorHelpers.Setup() starting...");

            // Step 1: Resolve IL2CPP exports
            if (!Il2CppExports.Resolve())
            {
                _logger.Error("[INJECT] Failed to resolve IL2CPP exports");
                return false;
            }

            // Step 2: Create fake assembly/image
            CreateFakeAssemblyImage();

            // Step 3: Resolve internal functions via xref scanning
            if (!ResolveInternalFunctions())
            {
                _logger.Error("[INJECT] Failed to resolve internal functions");
                return false;
            }

            // Step 4: Install hooks
            if (!InstallHooks())
            {
                _logger.Error("[INJECT] Failed to install hooks");
                return false;
            }

            s_initialized = true;
            LogVerbose("[INJECT] InjectorHelpers.Setup() complete");
            return true;
        }

        // ==============================
        // Fake assembly/image creation
        // ==============================

        private static void CreateFakeAssemblyImage()
        {
            if (s_FakeImage != IntPtr.Zero) return;

            // Allocate and zero fake image (256 bytes to cover all fields)
            s_FakeImage = Marshal.AllocHGlobal(256);
            unsafe
            {
                byte* p = (byte*)s_FakeImage.ToPointer();
                for (int i = 0; i < 256; i++) p[i] = 0;
            }

            // Allocate and zero fake assembly (256 bytes)
            s_FakeAssembly = Marshal.AllocHGlobal(256);
            unsafe
            {
                byte* p = (byte*)s_FakeAssembly.ToPointer();
                for (int i = 0; i < 256; i++) p[i] = 0;
            }

            // Create the image name string
            s_ImageNamePtr = Marshal.StringToHGlobalAnsi("MdbInjectedTypes");

            // Set image fields
            Marshal.WriteIntPtr(s_FakeImage, Il2CppConstants.IMAGE_NAME_OFFSET, s_ImageNamePtr);      // name
            Marshal.WriteIntPtr(s_FakeImage, Il2CppConstants.IMAGE_NAME_NO_EXT_OFFSET, s_ImageNamePtr); // nameNoExt
            Marshal.WriteIntPtr(s_FakeImage, Il2CppConstants.IMAGE_ASSEMBLY_OFFSET, s_FakeAssembly);    // assembly
            Marshal.WriteByte(s_FakeImage, Il2CppConstants.IMAGE_DYNAMIC_OFFSET, 1);                    // dynamic = 1

            // Set assembly fields — assembly.name.name and assembly.image
            // Il2CppAssemblyName is at offset 0x00 of Il2CppAssembly, with name at offset 0x00
            Marshal.WriteIntPtr(s_FakeAssembly, 0x00, s_ImageNamePtr); // aname.name
            Marshal.WriteIntPtr(s_FakeAssembly, 0x48, s_FakeImage);    // image (typical offset)

            LogVerbose($"[INJECT] Fake image @ 0x{s_FakeImage.ToInt64():X}, assembly @ 0x{s_FakeAssembly.ToInt64():X}");
        }

        // ==============================
        // Internal function resolution
        // ==============================

        private static bool ResolveInternalFunctions()
        {
            // FromIl2CppType: export → single call/jmp → internal function
            s_internalFromIl2CppType = XrefScanner.GetFirstTarget(Il2CppExports.il2cpp_class_from_il2cpp_type);
            if (s_internalFromIl2CppType == IntPtr.Zero)
            {
                _logger.Error("[INJECT] Failed to resolve internal Class::FromIl2CppType via xref");
                return false;
            }
            LogVerbose($"[INJECT] Internal FromIl2CppType @ 0x{s_internalFromIl2CppType.ToInt64():X}");

            // FromName: export → single call/jmp → internal function
            s_internalFromName = XrefScanner.GetFirstTarget(Il2CppExports.il2cpp_class_from_name);
            if (s_internalFromName == IntPtr.Zero)
            {
                _logger.Error("[INJECT] Failed to resolve internal Class::FromName via xref");
                return false;
            }
            LogVerbose($"[INJECT] Internal FromName @ 0x{s_internalFromName.ToInt64():X}");

            // GetTypeInfoFromTypeDefinitionIndex: multi-level xref from il2cpp_image_get_class
            s_internalGetTypeInfo = XrefScanner.ResolveGetTypeInfoFromTypeDefinitionIndex(Il2CppExports.il2cpp_image_get_class);
            if (s_internalGetTypeInfo == IntPtr.Zero)
            {
                _logger.Error("[INJECT] Failed to resolve GetTypeInfoFromTypeDefinitionIndex via xref");
                return false;
            }
            LogVerbose($"[INJECT] Internal GetTypeInfoFromTypeDefinitionIndex @ 0x{s_internalGetTypeInfo.ToInt64():X}");

            return true;
        }

        // ==============================
        // Hook installation
        // ==============================

        private static bool InstallHooks()
        {
            LogVerbose("[INJECT] Installing hooks...");

            // Hook 1: FromIl2CppType
            var fromTypeDelegate = new FromIl2CppTypeDelegate(Hook_FromIl2CppType);
            s_fromIl2CppTypeDelegateHandle = GCHandle.Alloc(fromTypeDelegate);
            IntPtr fromTypeDetour = Marshal.GetFunctionPointerForDelegate(fromTypeDelegate);

            s_hookFromIl2CppType = Il2CppBridge.mdb_create_hook_ptr(
                s_internalFromIl2CppType,
                fromTypeDetour,
                out s_originalFromIl2CppType);

            if (s_hookFromIl2CppType <= 0)
            {
                _logger.Error($"[INJECT] Hook install failed: FromIl2CppType (error={s_hookFromIl2CppType})");
                return false;
            }
            s_cachedOriginalFromType = Marshal.GetDelegateForFunctionPointer<FromIl2CppTypeDelegate>(s_originalFromIl2CppType);
            LogVerbose($"[INJECT] Hook installed: FromIl2CppType (handle={s_hookFromIl2CppType})");

            // Hook 2: FromName
            var fromNameDelegate = new FromNameDelegate(Hook_FromName);
            s_fromNameDelegateHandle = GCHandle.Alloc(fromNameDelegate);
            IntPtr fromNameDetour = Marshal.GetFunctionPointerForDelegate(fromNameDelegate);

            s_hookFromName = Il2CppBridge.mdb_create_hook_ptr(
                s_internalFromName,
                fromNameDetour,
                out s_originalFromName);

            if (s_hookFromName <= 0)
            {
                _logger.Error($"[INJECT] Hook install failed: FromName (error={s_hookFromName})");
                return false;
            }
            s_cachedOriginalFromName = Marshal.GetDelegateForFunctionPointer<FromNameDelegate>(s_originalFromName);
            LogVerbose($"[INJECT] Hook installed: FromName (handle={s_hookFromName})");

            // Hook 3: GetTypeInfoFromTypeDefinitionIndex — SKIPPED
            // This hook is NOT installed because:
            //   1. FromIl2CppType already intercepts all negative tokens in byval_arg.data/this_arg.data
            //   2. We keep MonoBehaviour's original typeMetadataHandle (no negative index in metadata)
            //   3. GetTypeInfo is an extremely hot path (thousands of calls/frame) — the
            //      native→managed→native transition overhead on every call causes
            //      AccessViolationExceptions in hosted .NET Framework 4.7.2 CLR
            //   4. Confirmed via testing: GetTypeInfo hook never fires for our injected class
            // If needed in the future, this hook should be implemented in native C++ (MDB_Bridge).
            LogVerbose($"[INJECT] GetTypeInfo hook SKIPPED (not needed — FromIl2CppType handles all token lookups)");

            return true;
        }

        // ==============================
        // Hook implementations
        // ==============================

        /// <summary>
        /// Hook for Class::FromIl2CppType.
        /// If the type's data is a negative token (injected class), look up in
        /// s_InjectedClasses and return the corresponding class pointer.
        /// Matches Il2CppInterop's approach — negative values are never valid
        /// TypeDefinitionIndex values (which are always >= 0).
        /// </summary>
        private static int s_fromTypeLogCount = 0;
        [HandleProcessCorruptedStateExceptions, SecurityCritical]
        private static IntPtr Hook_FromIl2CppType(IntPtr typePtr, bool throwOnError)
        {
            try
            {
                if (typePtr != IntPtr.Zero)
                {
                    // Read type->data (first 8 bytes of Il2CppType)
                    IntPtr data = Marshal.ReadIntPtr(typePtr);
                    long dataValue = data.ToInt64();

                    // Check for negative token (injected class)
                    if (dataValue < 0)
                    {
                        IntPtr classPtr;
                        if (s_InjectedClasses.TryGetValue(dataValue, out classPtr))
                        {
                            int count = Interlocked.Increment(ref s_fromTypeLogCount);
                            if (count <= 10) // Log first 10 interceptions
                            {
                                LogVerbose($"[INJECT] Hook_FromIl2CppType INTERCEPTED #{count}: type=0x{typePtr.ToInt64():X} data=0x{data.ToInt64():X} token={dataValue} → class=0x{classPtr.ToInt64():X}");
                            }
                            return classPtr;
                        }
                    }
                }

                // Call original (cached delegate — no per-call allocation)
                return s_cachedOriginalFromType(typePtr, throwOnError);
            }
            catch (Exception ex)
            {
                // Safety: never let the hook crash the runtime
                return IntPtr.Zero;
            }
        }

        /// <summary>
        /// Hook for Class::FromName.
        /// Call original first. If it returns null, check our registered classes.
        /// </summary>
        [HandleProcessCorruptedStateExceptions, SecurityCritical]
        private static IntPtr Hook_FromName(IntPtr imagePtr, IntPtr namespacePtr, IntPtr namePtr)
        {
            try
            {
                // Call original first (cached delegate — no per-call allocation)
                IntPtr result = s_cachedOriginalFromName(imagePtr, namespacePtr, namePtr);

                if (result != IntPtr.Zero)
                    return result;

                string ns = Marshal.PtrToStringAnsi(namespacePtr) ?? string.Empty;
                string name = Marshal.PtrToStringAnsi(namePtr) ?? string.Empty;

                IntPtr classPtr;
                lock (s_ClassNameLookup)
                {
                    if (s_ClassNameLookup.TryGetValue((ns, name, imagePtr), out classPtr))
                        return classPtr;
                }
            }
            catch (Exception ex)
            {
                // Safety: never let the hook crash
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Hook for GetTypeInfoFromTypeDefinitionIndex.
        /// If the index is negative and matches a registered class, return it.
        /// Otherwise call original.
        /// </summary>
        internal static volatile bool s_getTypeInfoGate = false;
        private static int s_getTypeInfoLogCount = 0;
        [HandleProcessCorruptedStateExceptions, SecurityCritical]
        private static IntPtr Hook_GetTypeInfo(int index)
        {
            try
            {
                // Check for negative token (injected class)
                IntPtr classPtr;
                if (s_InjectedClasses.TryGetValue((long)index, out classPtr))
                {
                    LogVerbose($"[INJECT] Hook_GetTypeInfo INTERCEPTED: index={index} → class=0x{classPtr.ToInt64():X}");
                    return classPtr;
                }

                // Call original (cached delegate — no per-call allocation)
                return s_cachedOriginalGetTypeInfo(index);
            }
            catch (Exception ex)
            {
                // Safety: never let the hook crash the runtime
                return IntPtr.Zero;
            }
        }

        // ==============================
        // Registration helpers
        // ==============================

        /// <summary>
        /// Create a negative token for a new injected class and register it.
        /// </summary>
        public static long CreateClassToken(IntPtr classPtr)
        {
            long token = Interlocked.Decrement(ref s_tokenCounter);
            s_InjectedClasses[token] = classPtr;
            LogVerbose($"[INJECT] Created class token {token} → 0x{classPtr.ToInt64():X}");
            return token;
        }

        private static long s_tokenCounter = -1; // first Decrement → -2

        // RegisterFakeTypeDef removed — now using negative tokens in s_InjectedClasses

        /// <summary>
        /// Register the class in s_ClassNameLookup for every loaded assembly image.
        /// This allows Class::FromName to find our injected class regardless of which
        /// image the lookup originates from.
        /// </summary>
        public static void AddTypeToLookup(IntPtr classPtr, string ns, string name)
        {
            int assemblyCount = Il2CppBridge.mdb_get_assembly_count();
            LogVerbose($"[INJECT] Registering {ns}.{name} in {assemblyCount} assembly images");

            lock (s_ClassNameLookup)
            {
                for (int i = 0; i < assemblyCount; i++)
                {
                    IntPtr assembly = Il2CppBridge.mdb_get_assembly(i);
                    if (assembly == IntPtr.Zero) continue;

                    IntPtr image = Il2CppBridge.mdb_assembly_get_image(assembly);
                    if (image == IntPtr.Zero) continue;

                    s_ClassNameLookup[(ns, name, image)] = classPtr;
                }

                // Also register against our fake image
                s_ClassNameLookup[(ns, name, s_FakeImage)] = classPtr;
            }
        }
    }
}
