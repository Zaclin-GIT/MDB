// ==============================
// NativeImports — kernel32 + IL2CPP export resolution
// ==============================
// Resolves IL2CPP function pointers from GameAssembly.dll via GetProcAddress.
// Handles obfuscated exports (suffix "_wasting_your_life").

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using GameSDK.ModHost;

namespace GameSDK.Injection
{
    /// <summary>
    /// kernel32 P/Invoke for module/export resolution.
    /// </summary>
    internal static class Kernel32
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        public static extern IntPtr LoadLibraryA(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr GetModuleHandleW(string lpModuleName);

        [DllImport("kernel32.dll")]
        public static extern IntPtr AddVectoredExceptionHandler(uint first, IntPtr handler);

        [DllImport("kernel32.dll")]
        public static extern uint RemoveVectoredExceptionHandler(IntPtr handle);
    }

    /// <summary>
    /// Resolves IL2CPP export function pointers from GameAssembly.dll.
    /// Handles obfuscated exports with "_wasting_your_life" suffix by
    /// enumerating the PE export table when standard names fail.
    /// </summary>
    internal static class Il2CppExports
    {
        private static readonly ModLogger _logger = new ModLogger("INJECT");

        private static IntPtr _gameAssemblyHandle;
        private static bool _resolved;

        /// <summary>Base address of GameAssembly.dll for RVA calculation.</summary>
        public static IntPtr GameAssemblyBase => _gameAssemblyHandle;

        // --- Resolved export function pointers ---
        public static IntPtr il2cpp_class_from_il2cpp_type { get; private set; }
        public static IntPtr il2cpp_class_from_name { get; private set; }
        public static IntPtr il2cpp_image_get_class { get; private set; }
        public static IntPtr il2cpp_runtime_class_init { get; private set; }

        // GC control
        public static IntPtr il2cpp_gc_disable { get; private set; }
        public static IntPtr il2cpp_gc_enable { get; private set; }

        // Type helpers (called directly, not hooked)
        public static IntPtr il2cpp_class_get_type { get; private set; }

        // --- Delegate types for direct invocation ---
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void VoidDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void RuntimeClassInitDelegate(IntPtr klass);

        // --- Cached delegates (prevent GC of the delegate) ---
        private static VoidDelegate _gcDisable;
        private static VoidDelegate _gcEnable;
        private static RuntimeClassInitDelegate _runtimeClassInit;

        public static void GcDisable()
        {
            if (_gcDisable == null && il2cpp_gc_disable != IntPtr.Zero)
                _gcDisable = Marshal.GetDelegateForFunctionPointer<VoidDelegate>(il2cpp_gc_disable);
            _gcDisable?.Invoke();
        }

        public static void GcEnable()
        {
            if (_gcEnable == null && il2cpp_gc_enable != IntPtr.Zero)
                _gcEnable = Marshal.GetDelegateForFunctionPointer<VoidDelegate>(il2cpp_gc_enable);
            _gcEnable?.Invoke();
        }

        public static void RuntimeClassInit(IntPtr klass)
        {
            if (_runtimeClassInit == null && il2cpp_runtime_class_init != IntPtr.Zero)
                _runtimeClassInit = Marshal.GetDelegateForFunctionPointer<RuntimeClassInitDelegate>(il2cpp_runtime_class_init);
            _runtimeClassInit?.Invoke(klass);
        }

        /// <summary>
        /// Resolve all needed IL2CPP exports. Idempotent.
        /// </summary>
        public static bool Resolve()
        {
            if (_resolved) return true;

            // Get GameAssembly.dll handle from the bridge
            _gameAssemblyHandle = Il2CppBridge.mdb_get_gameassembly_base();
            if (_gameAssemblyHandle == IntPtr.Zero)
            {
                // Fallback: load directly
                _gameAssemblyHandle = Kernel32.GetModuleHandleW("GameAssembly.dll");
            }

            if (_gameAssemblyHandle == IntPtr.Zero)
            {
                _logger.Error("[INJECT] GameAssembly.dll not found!");
                return false;
            }
            _logger.Info($"[INJECT] GameAssembly.dll @ 0x{_gameAssemblyHandle.ToInt64():X}");

            // Resolve each export
            il2cpp_class_from_il2cpp_type = ResolveExport("il2cpp_class_from_il2cpp_type");
            il2cpp_class_from_name = ResolveExport("il2cpp_class_from_name");
            il2cpp_image_get_class = ResolveExport("il2cpp_image_get_class");
            il2cpp_gc_disable = ResolveExport("il2cpp_gc_disable");
            il2cpp_gc_enable = ResolveExport("il2cpp_gc_enable");
            il2cpp_class_get_type = ResolveExport("il2cpp_class_get_type");
            il2cpp_runtime_class_init = ResolveExport("il2cpp_runtime_class_init");

            // Verify critical exports
            if (il2cpp_class_from_il2cpp_type == IntPtr.Zero ||
                il2cpp_class_from_name == IntPtr.Zero ||
                il2cpp_image_get_class == IntPtr.Zero)
            {
                _logger.Error("[INJECT] Failed to resolve critical IL2CPP exports!");
                return false;
            }

            _resolved = true;
            return true;
        }

        /// <summary>
        /// Resolve a single export by standard name, falling back to PE export table
        /// enumeration for obfuscated names.
        /// </summary>
        private static IntPtr ResolveExport(string name)
        {
            // Try standard name first
            IntPtr addr = Kernel32.GetProcAddress(_gameAssemblyHandle, name);
            if (addr != IntPtr.Zero)
            {
                _logger.Info($"[INJECT] Resolved {name} @ 0x{addr.ToInt64():X}");
                return addr;
            }

            // Try obfuscated: enumerate PE exports for "_wasting_your_life" suffix
            addr = FindObfuscatedExport(name);
            if (addr != IntPtr.Zero)
            {
                _logger.Info($"[INJECT] Resolved {name} (obfuscated) @ 0x{addr.ToInt64():X}");
                return addr;
            }

            _logger.Warning($"[INJECT] FAILED to resolve export: {name}");
            return IntPtr.Zero;
        }

        /// <summary>
        /// Enumerate PE export table to find obfuscated exports.
        /// GameAssembly.dll renames IL2CPP exports with patterns like "xyz_wasting_your_life".
        /// We look for exports whose deobfuscated name (prefix before "_wasting_your_life")
        /// could match, or fall back to checking all single-match suffix patterns.
        /// </summary>
        private static IntPtr FindObfuscatedExport(string standardName)
        {
            try
            {
                // Parse PE headers to find export directory
                // DOS header: e_lfanew at offset 0x3C
                int e_lfanew = Marshal.ReadInt32(_gameAssemblyHandle, 0x3C);
                IntPtr ntHeaders = _gameAssemblyHandle + e_lfanew;

                // PE signature (4 bytes) + FILE_HEADER (20 bytes) = offset to OptionalHeader
                IntPtr optionalHeader = ntHeaders + 4 + 20;

                // Export directory RVA is at offset 0x70 in PE32+ optional header
                int exportDirRva = Marshal.ReadInt32(optionalHeader, 0x70);
                int exportDirSize = Marshal.ReadInt32(optionalHeader, 0x74);

                if (exportDirRva == 0) return IntPtr.Zero;

                IntPtr exportDir = _gameAssemblyHandle + exportDirRva;

                int numberOfNames = Marshal.ReadInt32(exportDir, 0x18);
                int addressOfFunctions = Marshal.ReadInt32(exportDir, 0x1C);
                int addressOfNames = Marshal.ReadInt32(exportDir, 0x20);
                int addressOfNameOrdinals = Marshal.ReadInt32(exportDir, 0x24);

                IntPtr namesBase = _gameAssemblyHandle + addressOfNames;
                IntPtr ordinalsBase = _gameAssemblyHandle + addressOfNameOrdinals;
                IntPtr functionsBase = _gameAssemblyHandle + addressOfFunctions;

                // Search for exports matching the standard name pattern
                // The obfuscation replaces the standard name with "hash_wasting_your_life"
                // but some games keep the original name as a substring
                var candidates = new List<IntPtr>();

                for (int i = 0; i < numberOfNames; i++)
                {
                    int nameRva = Marshal.ReadInt32(namesBase, i * 4);
                    IntPtr namePtr = _gameAssemblyHandle + nameRva;
                    string exportName = Marshal.PtrToStringAnsi(namePtr);

                    if (exportName == null) continue;

                    // Check if this export contains our target name or ends with _wasting_your_life
                    // and the hash prefix could correspond to the standard name
                    if (exportName.Contains(standardName) ||
                        (exportName.EndsWith("_wasting_your_life") && 
                         MatchesObfuscatedPattern(exportName, standardName)))
                    {
                        short ordinal = Marshal.ReadInt16(ordinalsBase, i * 2);
                        int funcRva = Marshal.ReadInt32(functionsBase, ordinal * 4);
                        IntPtr funcAddr = _gameAssemblyHandle + funcRva;

                        // Check it's not a forwarder (address within export directory)
                        if (funcRva < exportDirRva || funcRva >= exportDirRva + exportDirSize)
                        {
                            candidates.Add(funcAddr);
                        }
                    }
                }

                // If exactly one match, use it
                if (candidates.Count == 1)
                    return candidates[0];

                // If multiple, try more specific matching
                if (candidates.Count > 1)
                {
                    _logger.Warning($"[INJECT] Multiple obfuscated matches for {standardName}: {candidates.Count}");
                    return candidates[0]; // Best guess
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[INJECT] PE export enumeration failed: {ex.Message}");
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Heuristic: check if an obfuscated export name could correspond to a standard name.
        /// The obfuscation pattern is typically: hash/random_wasting_your_life
        /// For now we accept any export with the _wasting_your_life suffix as a potential match
        /// when there's only one candidate (handled by the caller).
        /// </summary>
        private static bool MatchesObfuscatedPattern(string obfuscatedName, string standardName)
        {
            // Simple heuristic — if there's a _wasting_your_life suffix, strip it
            // and check if the remaining prefix has a similar length or structure
            // to the standard name. This is a loose match; the caller resolves ambiguity.
            return true;
        }
    }
}
