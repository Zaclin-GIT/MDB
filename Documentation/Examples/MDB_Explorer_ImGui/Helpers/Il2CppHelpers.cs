// ==============================
// Il2CppHelpers - Shared IL2CPP utility methods
// ==============================
// Centralizes duplicated Transform/GameObject helpers that were
// previously in both SceneHierarchy and GameObjectInspector.

using System;
using GameSDK;
using GameSDK.ModHost;

namespace MDB.Explorer.ImGui
{
    /// <summary>
    /// Shared IL2CPP helper methods for common Unity operations.
    /// Requires class pointers to be provided (resolved by the caller).
    /// </summary>
    public static class Il2CppHelpers
    {
        private const string LOG_TAG = "Il2CppHelpers";

        // Cached class pointers (resolved once)
        private static IntPtr _gameObjectClass;
        private static IntPtr _transformClass;
        private static IntPtr _componentClass;
        private static bool _resolved;

        /// <summary>
        /// Cached GameObject class pointer.
        /// </summary>
        public static IntPtr GameObjectClass => _gameObjectClass;

        /// <summary>
        /// Cached Transform class pointer.
        /// </summary>
        public static IntPtr TransformClass => _transformClass;

        /// <summary>
        /// Cached Component class pointer.
        /// </summary>
        public static IntPtr ComponentClass => _componentClass;

        /// <summary>
        /// Whether the core class pointers have been resolved.
        /// </summary>
        public static bool IsResolved => _resolved;

        /// <summary>
        /// Resolve core Unity class pointers. Returns true if all resolved successfully.
        /// </summary>
        public static bool ResolveClasses()
        {
            if (_resolved) return true;

            try
            {
                _gameObjectClass = Il2CppBridge.mdb_find_class("UnityEngine.CoreModule", "UnityEngine", "GameObject");
                _transformClass = Il2CppBridge.mdb_find_class("UnityEngine.CoreModule", "UnityEngine", "Transform");
                _componentClass = Il2CppBridge.mdb_find_class("UnityEngine.CoreModule", "UnityEngine", "Component");

                _resolved = _gameObjectClass != IntPtr.Zero &&
                            _transformClass != IntPtr.Zero &&
                            _componentClass != IntPtr.Zero;

                if (_resolved)
                    ModLogger.LogInternal(LOG_TAG, "[INFO] Core Unity classes resolved");
                else
                    ModLogger.LogInternal(LOG_TAG, "[WARN] Some core Unity classes failed to resolve");

                return _resolved;
            }
            catch (Exception ex)
            {
                ModLogger.LogInternal(LOG_TAG, $"[ERROR] ResolveClasses failed: {ex.Message}");
                return false;
            }
        }

        // ===== Transform helpers =====

        /// <summary>
        /// Get the Transform component from a GameObject pointer.
        /// </summary>
        public static IntPtr GetTransform(IntPtr goPtr)
        {
            if (goPtr == IntPtr.Zero || _gameObjectClass == IntPtr.Zero) return IntPtr.Zero;
            try
            {
                IntPtr method = Il2CppBridge.mdb_get_method(_gameObjectClass, "get_transform", 0);
                if (method == IntPtr.Zero) return IntPtr.Zero;

                IntPtr exception;
                return Il2CppBridge.mdb_invoke_method(method, goPtr, Array.Empty<IntPtr>(), out exception);
            }
            catch { return IntPtr.Zero; }
        }

        /// <summary>
        /// Get the parent Transform of a Transform.
        /// </summary>
        public static IntPtr GetParentTransform(IntPtr transform)
        {
            if (transform == IntPtr.Zero || _transformClass == IntPtr.Zero) return IntPtr.Zero;
            try
            {
                IntPtr method = Il2CppBridge.mdb_get_method(_transformClass, "get_parent", 0);
                if (method == IntPtr.Zero) return IntPtr.Zero;

                IntPtr exception;
                return Il2CppBridge.mdb_invoke_method(method, transform, Array.Empty<IntPtr>(), out exception);
            }
            catch { return IntPtr.Zero; }
        }

        /// <summary>
        /// Get the child count of a Transform.
        /// </summary>
        public static int GetChildCount(IntPtr transform)
        {
            if (transform == IntPtr.Zero) return 0;
            return Il2CppBridge.mdb_transform_get_child_count(transform);
        }

        /// <summary>
        /// Get a child Transform by index.
        /// </summary>
        public static IntPtr GetChildTransform(IntPtr transform, int index)
        {
            if (transform == IntPtr.Zero) return IntPtr.Zero;
            return Il2CppBridge.mdb_transform_get_child(transform, index);
        }

        /// <summary>
        /// Get the GameObject that a Transform belongs to.
        /// </summary>
        public static IntPtr GetGameObject(IntPtr transform)
        {
            if (transform == IntPtr.Zero || _transformClass == IntPtr.Zero) return IntPtr.Zero;
            try
            {
                IntPtr method = Il2CppBridge.mdb_get_method(_transformClass, "get_gameObject", 0);
                if (method == IntPtr.Zero) return IntPtr.Zero;

                IntPtr exception;
                return Il2CppBridge.mdb_invoke_method(method, transform, Array.Empty<IntPtr>(), out exception);
            }
            catch { return IntPtr.Zero; }
        }

        // ===== GameObject helpers =====

        /// <summary>
        /// Get the name of a GameObject.
        /// </summary>
        public static string GetGameObjectName(IntPtr goPtr)
        {
            if (goPtr == IntPtr.Zero || _gameObjectClass == IntPtr.Zero) return null;
            try
            {
                IntPtr method = Il2CppBridge.mdb_get_method(_gameObjectClass, "get_name", 0);
                if (method == IntPtr.Zero) return null;

                IntPtr exception;
                IntPtr result = Il2CppBridge.mdb_invoke_method(method, goPtr, Array.Empty<IntPtr>(), out exception);
                if (result == IntPtr.Zero) return null;

                return Il2CppBridge.Il2CppStringToManaged(result);
            }
            catch { return null; }
        }

        /// <summary>
        /// Get whether a GameObject is active (activeSelf).
        /// </summary>
        public static bool GetGameObjectActive(IntPtr goPtr)
        {
            if (goPtr == IntPtr.Zero) return false;
            return Il2CppBridge.mdb_gameobject_get_active_self(goPtr);
        }

        /// <summary>
        /// Set a GameObject's active state.
        /// </summary>
        public static bool SetGameObjectActive(IntPtr goPtr, bool active)
        {
            if (goPtr == IntPtr.Zero) return false;
            try
            {
                return Il2CppBridge.mdb_gameobject_set_active(goPtr, active);
            }
            catch (Exception ex)
            {
                ModLogger.LogInternal(LOG_TAG, $"[ERROR] SetActive failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get the type name of a component instance.
        /// </summary>
        public static string GetComponentTypeName(IntPtr compPtr)
        {
            if (compPtr == IntPtr.Zero) return null;
            try
            {
                IntPtr klass = Il2CppBridge.mdb_object_get_class(compPtr);
                if (klass == IntPtr.Zero) return null;
                return Il2CppBridge.GetClassName(klass);
            }
            catch { return null; }
        }

        /// <summary>
        /// Check if a GameObject has child transforms.
        /// </summary>
        public static bool HasChildTransforms(IntPtr goPtr)
        {
            IntPtr transform = GetTransform(goPtr);
            if (transform == IntPtr.Zero) return false;
            return GetChildCount(transform) > 0;
        }
    }
}
