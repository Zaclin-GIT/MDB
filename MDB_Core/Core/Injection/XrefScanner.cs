// ==============================
// XrefScanner — Minimal E8/E9 call/jmp target scanner
// ==============================
// Scans function prologues to find CALL/JMP rel32 targets.
// Used to follow the xref chain from exported IL2CPP functions to internal ones.
// Mirrors IL2CppInterop's XrefScannerLowLevel.JumpTargets() and
// MDB_Bridge's collect_call_targets().

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using GameSDK.ModHost;

namespace GameSDK.Injection
{
    /// <summary>
    /// Minimal x86-64 scanner that decodes E8 (CALL rel32) and E9 (JMP rel32)
    /// instructions from a function start address. Stops at C3 (RET) or CC (INT3).
    /// </summary>
    internal static class XrefScanner
    {
        private static readonly ModLogger _logger = new ModLogger("INJECT");

        /// <summary>
        /// Enumerate all CALL/JMP rel32 targets from a function start.
        /// </summary>
        /// <param name="functionStart">Address of the function to scan.</param>
        /// <param name="maxBytes">Maximum number of bytes to scan (default 512).</param>
        /// <returns>Enumerable of target addresses.</returns>
        public static IEnumerable<IntPtr> GetCallTargets(IntPtr functionStart, int maxBytes = 512)
        {
            if (functionStart == IntPtr.Zero)
                yield break;

            long start = functionStart.ToInt64();

            for (int offset = 0; offset < maxBytes; )
            {
                byte opcode = Marshal.ReadByte(functionStart, offset);

                // RET or INT3 — end of function
                if (opcode == 0xC3 || opcode == 0xCC)
                    break;

                // E8 = CALL rel32, E9 = JMP rel32
                if (opcode == 0xE8 || opcode == 0xE9)
                {
                    if (offset + 5 > maxBytes)
                        break;

                    int disp32 = Marshal.ReadInt32(functionStart, offset + 1);
                    long targetAddr = start + offset + 5 + disp32;

                    yield return new IntPtr(targetAddr);

                    offset += 5;
                    continue;
                }

                // Skip other instructions (we don't have a full disassembler,
                // so we use a simple heuristic: walk byte-by-byte for unknown opcodes).
                // This is safe because E8/E9/C3/CC are unambiguous single-byte opcode prefixes.
                // In practice, IL2CPP export stubs are short (< 20 bytes) and consist of
                // a few MOV/LEA instructions followed by a JMP or CALL.
                offset++;
            }
        }

        /// <summary>
        /// Get the first CALL/JMP target from a function. Returns IntPtr.Zero if none found.
        /// </summary>
        public static IntPtr GetFirstTarget(IntPtr functionStart, int maxBytes = 512)
        {
            foreach (var target in GetCallTargets(functionStart, maxBytes))
                return target;
            return IntPtr.Zero;
        }

        /// <summary>
        /// Follow a single xref chain: functionStart → first call/jmp target.
        /// Repeats up to maxDepth times.
        /// </summary>
        /// <param name="functionStart">Starting address.</param>
        /// <param name="maxDepth">Maximum number of levels to follow.</param>
        /// <returns>The final resolved address, or IntPtr.Zero if the chain breaks.</returns>
        public static IntPtr FollowSingleXref(IntPtr functionStart, int maxDepth = 3)
        {
            IntPtr current = functionStart;
            for (int i = 0; i < maxDepth; i++)
            {
                IntPtr next = GetFirstTarget(current);
                if (next == IntPtr.Zero)
                    return current; // End of chain — return last valid address
                
                _logger.Info($"[INJECT] Xref level {i}: 0x{current.ToInt64():X} → 0x{next.ToInt64():X}");
                current = next;
            }
            return current;
        }

        /// <summary>
        /// Resolve internal function via multi-level xref chain from il2cpp_image_get_class.
        /// Chain: il2cpp_image_get_class → Image::GetType → ... → GetTypeInfoFromTypeDefinitionIndex
        /// </summary>
        /// <param name="imageGetClass">Address of il2cpp_image_get_class export.</param>
        /// <returns>Address of GetTypeInfoFromTypeDefinitionIndex, or IntPtr.Zero.</returns>
        public static IntPtr ResolveGetTypeInfoFromTypeDefinitionIndex(IntPtr imageGetClass)
        {
            _logger.Info($"[INJECT] Resolving GetTypeInfoFromTypeDefinitionIndex from il2cpp_image_get_class @ 0x{imageGetClass.ToInt64():X}");

            // Level 0: il2cpp_image_get_class → first target = Image::GetType
            IntPtr imageGetType = GetFirstTarget(imageGetClass);
            if (imageGetType == IntPtr.Zero)
            {
                _logger.Error("[INJECT] Failed: no call target from il2cpp_image_get_class");
                return IntPtr.Zero;
            }
            _logger.Info($"[INJECT] Image::GetType @ 0x{imageGetType.ToInt64():X}");

            // Level 1: Image::GetType has multiple call targets.
            // We need GetTypeInfoFromHandle — typically the last of 2 xrefs,
            // or the one that itself has a single call target leading to
            // GetTypeInfoFromTypeDefinitionIndex.
            var level1Targets = new List<IntPtr>(GetCallTargets(imageGetType));
            _logger.Info($"[INJECT] Image::GetType has {level1Targets.Count} call targets");

            // Try each target — look for one that leads to the final function.
            // GetTypeInfoFromTypeDefinitionIndex is typically a leaf function (no E8 calls).
            // Strategy: prefer leaf sub-targets over chain sub-targets.
            IntPtr leafCandidate = IntPtr.Zero;
            IntPtr chainCandidate = IntPtr.Zero;

            foreach (var target in level1Targets)
            {
                _logger.Info($"[INJECT]   Checking target @ 0x{target.ToInt64():X}");
                var subTargets = new List<IntPtr>(GetCallTargets(target));
                _logger.Info($"[INJECT]     Sub-targets: {subTargets.Count}");

                foreach (var sub in subTargets)
                {
                    var finalTargets = new List<IntPtr>(GetCallTargets(sub));
                    if (finalTargets.Count == 0)
                    {
                        // Leaf function — no further E8/E9 calls.
                        // GetTypeInfoFromTypeDefinitionIndex is a low-level function
                        // that typically has no outgoing calls (uses table lookup).
                        if (leafCandidate == IntPtr.Zero)
                            leafCandidate = sub;
                        _logger.Info($"[INJECT]     Sub @ 0x{sub.ToInt64():X} is LEAF (0 targets) — strong candidate");
                    }
                    else
                    {
                        // This sub is a wrapper, follow the chain
                        if (chainCandidate == IntPtr.Zero)
                            chainCandidate = finalTargets[finalTargets.Count - 1];
                        _logger.Info($"[INJECT]     Sub @ 0x{sub.ToInt64():X} has {finalTargets.Count} targets → chain to 0x{finalTargets[finalTargets.Count - 1].ToInt64():X}");
                    }
                }
            }

            // Prefer leaf candidate (GetTypeInfoFromTypeDefinitionIndex is a leaf function)
            if (leafCandidate != IntPtr.Zero)
            {
                _logger.Info($"[INJECT] Selected LEAF candidate GetTypeInfoFromTypeDefinitionIndex @ 0x{leafCandidate.ToInt64():X}");
                return leafCandidate;
            }
            if (chainCandidate != IntPtr.Zero)
            {
                _logger.Info($"[INJECT] Selected chain candidate GetTypeInfoFromTypeDefinitionIndex @ 0x{chainCandidate.ToInt64():X}");
                return chainCandidate;
            }

            // Fallback: try the simplest chain
            if (level1Targets.Count >= 2)
            {
                IntPtr lastTarget = level1Targets[level1Targets.Count - 1];
                IntPtr resolved = FollowSingleXref(lastTarget, 2);
                if (resolved != IntPtr.Zero && resolved != lastTarget)
                {
                    _logger.Info($"[INJECT] Fallback resolved GetTypeInfoFromTypeDefinitionIndex @ 0x{resolved.ToInt64():X}");
                    return resolved;
                }
            }

            _logger.Error("[INJECT] Failed to resolve GetTypeInfoFromTypeDefinitionIndex");
            return IntPtr.Zero;
        }
    }
}
