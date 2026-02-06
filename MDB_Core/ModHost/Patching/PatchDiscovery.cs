// ==============================
// PatchDiscovery - Patch Attribute Discovery and Scanning
// ==============================
// Discovers [Patch], [Prefix], [Postfix] attributes from mod assemblies

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace GameSDK.ModHost.Patching
{
    /// <summary>
    /// Handles discovery and scanning of patch attributes in mod assemblies.
    /// </summary>
    public static partial class PatchProcessor
    {
        /// <summary>
        /// Process and apply all patches from an assembly.
        /// </summary>
        public static int ProcessAssembly(Assembly assembly)
        {
            List<PatchInfo> appliedPatches = new List<PatchInfo>();

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).ToArray();
            }

            foreach (Type type in types)
            {
                try
                {
                    ProcessPatchClass(type, appliedPatches);
                }
                catch (Exception ex)
                {
                    _logger.Error($"Failed to process patch class {type.Name}: {ex.Message}");
                }
            }

            if (appliedPatches.Count > 0)
            {
                _patchesByAssembly[assembly] = appliedPatches;
            }

            return appliedPatches.Count;
        }

        /// <summary>
        /// Process a single patch class and extract patch information.
        /// </summary>
        private static void ProcessPatchClass(Type patchClass, List<PatchInfo> appliedPatches)
        {
            PatchAttribute[] patchAttrs = patchClass.GetCustomAttributes<PatchAttribute>().ToArray();
            if (patchAttrs.Length == 0)
                return;

            PatchMethodAttribute patchMethodAttr = patchClass.GetCustomAttribute<PatchMethodAttribute>();
            PatchRvaAttribute patchRvaAttr = patchClass.GetCustomAttribute<PatchRvaAttribute>();

            string targetNs = null;
            string targetType = null;
            string targetMethod = null;
            string targetAssembly = "Assembly-CSharp";

            foreach (PatchAttribute attr in patchAttrs)
            {
                if (attr.TargetType != null)
                {
                    targetNs = attr.Namespace;
                    targetType = attr.TypeName;
                    targetAssembly = attr.Assembly;
                }
                else if (attr.TypeName != null && string.IsNullOrEmpty(attr.MethodName))
                {
                    targetNs = attr.Namespace;
                    targetType = attr.TypeName;
                    targetAssembly = attr.Assembly;
                }
                else if (!string.IsNullOrEmpty(attr.MethodName))
                {
                    targetMethod = attr.MethodName;
                }
            }

            if (patchMethodAttr != null)
            {
                targetMethod = patchMethodAttr.MethodName;
            }

            if (string.IsNullOrEmpty(targetType))
            {
                _logger.Warning($"Patch class {patchClass.Name} has no target type specified");
                return;
            }

            if (string.IsNullOrEmpty(targetMethod) && patchRvaAttr == null)
            {
                _logger.Warning($"Patch class {patchClass.Name} has no target method specified");
                return;
            }

            MethodInfo prefixMethod = FindPatchMethod(patchClass, typeof(PrefixAttribute));
            MethodInfo postfixMethod = FindPatchMethod(patchClass, typeof(PostfixAttribute));
            MethodInfo finalizerMethod = FindPatchMethod(patchClass, typeof(FinalizerAttribute));

            if (prefixMethod == null && postfixMethod == null && finalizerMethod == null)
            {
                _logger.Warning($"Patch class {patchClass.Name} has no [Prefix], [Postfix], or [Finalizer] methods");
                return;
            }

            int paramCount = patchMethodAttr?.ParameterCount ?? -1;
            
            // Infer parameter count from patch method if not specified
            if (paramCount < 0 && prefixMethod != null)
            {
                paramCount = InferParameterCount(prefixMethod);
            }
            if (paramCount < 0 && postfixMethod != null)
            {
                paramCount = InferParameterCount(postfixMethod);
            }

            PatchInfo patchInfo = new PatchInfo
            {
                PatchClass = patchClass,
                TargetNamespace = targetNs ?? "",
                TargetTypeName = targetType,
                TargetMethodName = targetMethod,
                TargetRva = patchRvaAttr?.Rva,
                ParameterCount = paramCount,
                PrefixMethod = prefixMethod,
                PostfixMethod = postfixMethod,
                FinalizerMethod = finalizerMethod
            };

            if (ApplyPatch(patchInfo, targetAssembly))
            {
                appliedPatches.Add(patchInfo);

                if (!_patchesByClass.ContainsKey(patchClass))
                    _patchesByClass[patchClass] = new List<PatchInfo>();
                _patchesByClass[patchClass].Add(patchInfo);
            }
        }

        /// <summary>
        /// Infer the number of parameters from a patch method's __N parameters.
        /// </summary>
        private static int InferParameterCount(MethodInfo method)
        {
            int maxIndex = -1;
            foreach (var param in method.GetParameters())
            {
                if (param.Name.StartsWith("__") && param.Name != "__instance" && 
                    param.Name != "__result" && param.Name != "__exception")
                {
                    if (int.TryParse(param.Name.Substring(2), out int idx))
                    {
                        if (idx > maxIndex) maxIndex = idx;
                    }
                }
            }
            return maxIndex + 1;  // __0 means 1 param, __1 means 2 params, etc.
        }

        /// <summary>
        /// Find a patch method (Prefix, Postfix, or Finalizer) in a patch class.
        /// </summary>
        private static MethodInfo FindPatchMethod(Type patchClass, Type attributeType)
        {
            return patchClass.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.GetCustomAttribute(attributeType) != null);
        }

        /// <summary>
        /// Find an IL2CPP class by namespace and type name.
        /// </summary>
        private static IntPtr FindClass(string assembly, string ns, string typeName)
        {
            IntPtr klass = Il2CppBridge.mdb_find_class(assembly, ns, typeName);
            if (klass != IntPtr.Zero) return klass;

            string[] assemblies = { "Assembly-CSharp", "UnityEngine.CoreModule", "UnityEngine", "mscorlib" };
            foreach (string asm in assemblies)
            {
                klass = Il2CppBridge.mdb_find_class(asm, ns, typeName);
                if (klass != IntPtr.Zero) return klass;
            }
            return IntPtr.Zero;
        }

        /// <summary>
        /// Remove all patches from an assembly.
        /// </summary>
        public static void RemovePatchesFromAssembly(Assembly assembly)
        {
            if (!_patchesByAssembly.TryGetValue(assembly, out List<PatchInfo> patches))
                return;

            foreach (PatchInfo patch in patches)
            {
                if (patch.HookHandle > 0)
                {
                    string target = GetPatchTargetName(patch);
                    Il2CppBridge.mdb_remove_hook(patch.HookHandle);
                    _hookToPatch.Remove(patch.HookHandle);
                    _logger.Info($"x {target}", ConsoleColor.Red);
                }
            }

            _patchesByAssembly.Remove(assembly);
        }

        /// <summary>
        /// Get all patches from all assemblies.
        /// </summary>
        public static IEnumerable<PatchInfo> GetAllPatches()
        {
            return _patchesByClass.Values.SelectMany(p => p);
        }

        /// <summary>
        /// Get patches for a specific patch class.
        /// </summary>
        public static IEnumerable<PatchInfo> GetPatchesForClass(Type patchClass)
        {
            return _patchesByClass.TryGetValue(patchClass, out List<PatchInfo> patches)
                ? patches
                : Enumerable.Empty<PatchInfo>();
        }

        /// <summary>
        /// Get the fully qualified name of a patch target.
        /// </summary>
        private static string GetPatchTargetName(PatchInfo patch)
        {
            string ns = patch.TargetNamespace;
            string type = patch.TargetTypeName;
            string method = patch.TargetMethodName;
            
            if (!string.IsNullOrEmpty(ns))
                return $"{ns}.{type}.{method}";
            return $"{type}.{method}";
        }
    }
}
