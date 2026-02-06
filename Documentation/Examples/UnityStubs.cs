// ==============================
// Universal Unity IL2CPP Wrapper Stubs
// ==============================
// Stripped-down subset of auto-generated Il2Cpp wrapper classes.
// Contains only universal Unity Engine types that work across all Unity games.
//
// These are copied from the generated wrapper output (MDB_Core/Generated/GameSDK.UnityEngine.cs
// and GameSDK.UnityEngine_SceneManagement.cs). In a real mod project the full generated
// wrappers are used instead. This file exists so the example mods compile standalone.
//
// Do not edit manually — regenerate from the IL2CPP dump if the game updates.

#pragma warning disable 0108, 0114, 0162, 0168, 0219

using System;
using System.Collections;
using System.Collections.Generic;
using GameSDK;

// System namespaces for common types
using System.Text;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;

namespace UnityEngine
{
    // ═══════════════════════════════════════
    //  Enums
    // ═══════════════════════════════════════

    public enum SendMessageOptions
    {
        RequireReceiver = 0,
        DontRequireReceiver = 1
    }

    public enum PrimitiveType
    {
        Sphere = 0,
        Capsule = 1,
        Cylinder = 2,
        Cube = 3,
        Plane = 4,
        Quad = 5
    }

    public enum Space
    {
        World = 0,
        Self = 1
    }

    public enum HideFlags
    {
        None = 0,
        HideInHierarchy = 1,
        HideInInspector = 2,
        DontSaveInEditor = 4,
        NotEditable = 8,
        DontSaveInBuild = 16,
        DontUnloadUnusedAsset = 32,
        DontSave = 52,
        HideAndDontSave = 61
    }

    public enum FindObjectsSortMode
    {
        None = 0,
        InstanceID = 1
    }

    public enum FindObjectsInactive
    {
        Exclude = 0,
        Include = 1
    }

    public enum CameraClearFlags
    {
        Skybox = 1,
        Color = 2,
        SolidColor = 2,
        Depth = 3,
        Nothing = 4
    }

    public enum LogType
    {
        Error = 0,
        Assert = 1,
        Warning = 2,
        Log = 3,
        Exception = 4
    }

    public enum FullScreenMode
    {
        ExclusiveFullScreen = 0,
        FullScreenWindow = 1,
        MaximizedWindow = 2,
        Windowed = 3
    }

    public enum RuntimePlatform
    {
        OSXEditor = 0,
        OSXPlayer = 1,
        WindowsPlayer = 2,
        WindowsEditor = 7,
        IPhonePlayer = 8,
        Android = 11,
        LinuxPlayer = 13,
        LinuxEditor = 16,
        WebGLPlayer = 17,
        PS4 = 25,
        XboxOne = 27,
        Switch = 32,
        PS5 = 44
    }

    public enum SystemLanguage
    {
        Afrikaans = 0,
        Arabic = 1,
        Basque = 2,
        Belarusian = 3,
        Bulgarian = 4,
        Catalan = 5,
        Chinese = 6,
        Czech = 7,
        Danish = 8,
        Dutch = 9,
        English = 10,
        Estonian = 11,
        Faroese = 12,
        Finnish = 13,
        French = 14,
        German = 15,
        Greek = 16,
        Hebrew = 17,
        Hungarian = 18,
        Icelandic = 19,
        Indonesian = 20,
        Italian = 21,
        Japanese = 22,
        Korean = 23,
        Latvian = 24,
        Lithuanian = 25,
        Norwegian = 26,
        Polish = 27,
        Portuguese = 28,
        Romanian = 29,
        Russian = 30,
        SerboCroatian = 31,
        Slovak = 32,
        Slovenian = 33,
        Spanish = 34,
        Swedish = 35,
        Thai = 36,
        Turkish = 37,
        Ukrainian = 38,
        Vietnamese = 39,
        ChineseSimplified = 40,
        ChineseTraditional = 41,
        Unknown = 42
    }

    // ═══════════════════════════════════════
    //  Value Types / Structs
    // ═══════════════════════════════════════

    public struct Vector2
    {
        public float x;
        public float y;
    }

    public struct Vector3
    {
        public float x;
        public float y;
        public float z;
    }

    public struct Vector4
    {
        public float x;
        public float y;
        public float z;
        public float w;
    }

    public struct Quaternion
    {
        public float x;
        public float y;
        public float z;
        public float w;
    }

    public struct Color
    {
        public float r;
        public float g;
        public float b;
        public float a;
    }

    public struct Color32
    {
        public int rgba;
        public byte r;
        public byte g;
        public byte b;
        public byte a;
    }

    public struct Rect
    {
        public float m_XMin;
        public float m_YMin;
        public float m_Width;
        public float m_Height;
    }

    public struct Matrix4x4
    {
        public float m00;
        public float m10;
        public float m20;
        public float m30;
        public float m01;
        public float m11;
        public float m21;
        public float m31;
        public float m02;
        public float m12;
        public float m22;
        public float m32;
        public float m03;
        public float m13;
        public float m23;
        public float m33;
    }

    public struct RefreshRate
    {
        public uint numerator;
        public uint denominator;
    }

    public struct Resolution
    {
        public int m_Width;
        public int m_Height;
        public RefreshRate m_RefreshRate;
    }

    // ═══════════════════════════════════════
    //  Core Object Hierarchy
    //  Object → Component → Behaviour → MonoBehaviour
    // ═══════════════════════════════════════

    public partial class Object : Il2CppObject
    {
        public Object(IntPtr nativePtr) : base(nativePtr) { }

        // Properties
        public string name
        {
            get => Il2CppRuntime.Call<string>(this, "get_name", global::System.Type.EmptyTypes);
            set => Il2CppRuntime.InvokeVoid(this, "set_name", new[] { typeof(string) }, value);
        }

        public HideFlags hideFlags
        {
            get => Il2CppRuntime.Call<HideFlags>(this, "get_hideFlags", global::System.Type.EmptyTypes);
            set => Il2CppRuntime.InvokeVoid(this, "set_hideFlags", new[] { typeof(HideFlags) }, value);
        }

        // Methods
        public int GetInstanceID()
        {
            return Il2CppRuntime.Call<int>(this, "GetInstanceID", global::System.Type.EmptyTypes);
        }

        public override string ToString()
        {
            return Il2CppRuntime.Call<string>(this, "ToString", global::System.Type.EmptyTypes);
        }

        public static Object Instantiate(Object original)
        {
            return Il2CppRuntime.CallStatic<Object>("UnityEngine", "Object", "Instantiate", new global::System.Type[] { typeof(Object) }, original);
        }

        public static Object Instantiate(Object original, Transform parent)
        {
            return Il2CppRuntime.CallStatic<Object>("UnityEngine", "Object", "Instantiate", new global::System.Type[] { typeof(Object), typeof(Transform) }, original, parent);
        }

        public static Object Instantiate(Object original, Transform parent, bool instantiateInWorldSpace)
        {
            return Il2CppRuntime.CallStatic<Object>("UnityEngine", "Object", "Instantiate", new global::System.Type[] { typeof(Object), typeof(Transform), typeof(bool) }, original, parent, instantiateInWorldSpace);
        }

        public static Object Instantiate(Object original, Vector3 position, Quaternion rotation)
        {
            return Il2CppRuntime.CallStatic<Object>("UnityEngine", "Object", "Instantiate", new global::System.Type[] { typeof(Object), typeof(Vector3), typeof(Quaternion) }, original, position, rotation);
        }

        public static Object Instantiate(Object original, Vector3 position, Quaternion rotation, Transform parent)
        {
            return Il2CppRuntime.CallStatic<Object>("UnityEngine", "Object", "Instantiate", new global::System.Type[] { typeof(Object), typeof(Vector3), typeof(Quaternion), typeof(Transform) }, original, position, rotation, parent);
        }

        public static void Destroy(Object obj, float t)
        {
            Il2CppRuntime.InvokeStaticVoid("UnityEngine", "Object", "Destroy", new global::System.Type[] { typeof(Object), typeof(float) }, obj, t);
        }

        public static void Destroy(Object obj)
        {
            Il2CppRuntime.InvokeStaticVoid("UnityEngine", "Object", "Destroy", new global::System.Type[] { typeof(Object) }, obj);
        }

        public static void DestroyImmediate(Object obj, bool allowDestroyingAssets)
        {
            Il2CppRuntime.InvokeStaticVoid("UnityEngine", "Object", "DestroyImmediate", new global::System.Type[] { typeof(Object), typeof(bool) }, obj, allowDestroyingAssets);
        }

        public static void DestroyImmediate(Object obj)
        {
            Il2CppRuntime.InvokeStaticVoid("UnityEngine", "Object", "DestroyImmediate", new global::System.Type[] { typeof(Object) }, obj);
        }

        public static Object[] FindObjectsOfType(Type type)
        {
            return Il2CppRuntime.CallStatic<Object[]>("UnityEngine", "Object", "FindObjectsOfType", new global::System.Type[] { typeof(Type) }, type);
        }

        public static Object[] FindObjectsByType(Type type, FindObjectsSortMode sortMode)
        {
            return Il2CppRuntime.CallStatic<Object[]>("UnityEngine", "Object", "FindObjectsByType", new global::System.Type[] { typeof(Type), typeof(FindObjectsSortMode) }, type, sortMode);
        }

        public static Object[] FindObjectsByType(Type type, FindObjectsInactive findObjectsInactive, FindObjectsSortMode sortMode)
        {
            return Il2CppRuntime.CallStatic<Object[]>("UnityEngine", "Object", "FindObjectsByType", new global::System.Type[] { typeof(Type), typeof(FindObjectsInactive), typeof(FindObjectsSortMode) }, type, findObjectsInactive, sortMode);
        }

        public static void DontDestroyOnLoad(Object target)
        {
            Il2CppRuntime.InvokeStaticVoid("UnityEngine", "Object", "DontDestroyOnLoad", new global::System.Type[] { typeof(Object) }, target);
        }

        public static Object FindObjectOfType(Type type)
        {
            return Il2CppRuntime.CallStatic<Object>("UnityEngine", "Object", "FindObjectOfType", new global::System.Type[] { typeof(Type) }, type);
        }

        public static Object[] FindObjectsOfTypeAll(Type type)
        {
            return Il2CppRuntime.CallStatic<Object[]>("UnityEngine", "Object", "FindObjectsOfTypeAll", new global::System.Type[] { typeof(Type) }, type);
        }
    }

    public partial class Component : Object
    {
        public Component(IntPtr nativePtr) : base(nativePtr) { }

        // Properties
        public Transform transform
        {
            get => Il2CppRuntime.Call<Transform>(this, "get_transform", global::System.Type.EmptyTypes);
        }

        public GameObject gameObject
        {
            get => Il2CppRuntime.Call<GameObject>(this, "get_gameObject", global::System.Type.EmptyTypes);
        }

        // Methods
        public Component GetComponent(Type type)
        {
            return Il2CppRuntime.Call<Component>(this, "GetComponent", new global::System.Type[] { typeof(Type) }, type);
        }

        public bool TryGetComponent(Type type, out Component component)
        {
            component = default;
            return Il2CppRuntime.Call<bool>(this, "TryGetComponent", new global::System.Type[] { typeof(Type), typeof(Component) }, type, component);
        }

        public Component GetComponentInChildren(Type t, bool includeInactive)
        {
            return Il2CppRuntime.Call<Component>(this, "GetComponentInChildren", new global::System.Type[] { typeof(Type), typeof(bool) }, t, includeInactive);
        }

        public Component GetComponentInParent(Type t, bool includeInactive)
        {
            return Il2CppRuntime.Call<Component>(this, "GetComponentInParent", new global::System.Type[] { typeof(Type), typeof(bool) }, t, includeInactive);
        }

        public Component[] GetComponents(Type type)
        {
            return Il2CppRuntime.Call<Component[]>(this, "GetComponents", new global::System.Type[] { typeof(Type) }, type);
        }

        public string tag
        {
            get => Il2CppRuntime.Call<string>(this, "get_tag", global::System.Type.EmptyTypes);
            set => Il2CppRuntime.InvokeVoid(this, "set_tag", new[] { typeof(string) }, value);
        }

        public bool CompareTag(string tag)
        {
            return Il2CppRuntime.Call<bool>(this, "CompareTag", new global::System.Type[] { typeof(string) }, tag);
        }

        public void SendMessage(string methodName, SendMessageOptions options)
        {
            Il2CppRuntime.InvokeVoid(this, "SendMessage", new global::System.Type[] { typeof(string), typeof(SendMessageOptions) }, methodName, options);
        }
    }

    public partial class Behaviour : Component
    {
        public Behaviour(IntPtr nativePtr) : base(nativePtr) { }

        // Properties
        public bool enabled
        {
            get => Il2CppRuntime.Call<bool>(this, "get_enabled", global::System.Type.EmptyTypes);
            set => Il2CppRuntime.InvokeVoid(this, "set_enabled", new[] { typeof(bool) }, value);
        }

        public bool isActiveAndEnabled
        {
            get => Il2CppRuntime.Call<bool>(this, "get_isActiveAndEnabled", global::System.Type.EmptyTypes);
        }
    }

    public partial class MonoBehaviour : Behaviour
    {
        public MonoBehaviour(IntPtr nativePtr) : base(nativePtr) { }

        // Methods
        public bool IsInvoking()
        {
            return Il2CppRuntime.Call<bool>(this, "IsInvoking", global::System.Type.EmptyTypes);
        }

        public void CancelInvoke()
        {
            Il2CppRuntime.InvokeVoid(this, "CancelInvoke", global::System.Type.EmptyTypes);
        }
    }

    // ═══════════════════════════════════════
    //  GameObject
    // ═══════════════════════════════════════

    public partial class GameObject : Object
    {
        public GameObject(IntPtr nativePtr) : base(nativePtr) { }

        // Properties
        public Transform transform
        {
            get => Il2CppRuntime.Call<Transform>(this, "get_transform", global::System.Type.EmptyTypes);
        }

        public int layer
        {
            get => Il2CppRuntime.Call<int>(this, "get_layer", global::System.Type.EmptyTypes);
            set => Il2CppRuntime.InvokeVoid(this, "set_layer", new[] { typeof(int) }, value);
        }

        public bool activeSelf
        {
            get => Il2CppRuntime.Call<bool>(this, "get_activeSelf", global::System.Type.EmptyTypes);
        }

        public bool activeInHierarchy
        {
            get => Il2CppRuntime.Call<bool>(this, "get_activeInHierarchy", global::System.Type.EmptyTypes);
        }

        public UnityEngine.SceneManagement.Scene scene
        {
            get => Il2CppRuntime.Call<UnityEngine.SceneManagement.Scene>(this, "get_scene", global::System.Type.EmptyTypes);
        }

        // Methods
        public static GameObject CreatePrimitive(PrimitiveType type)
        {
            return Il2CppRuntime.CallStatic<GameObject>("UnityEngine", "GameObject", "CreatePrimitive", new global::System.Type[] { typeof(PrimitiveType) }, type);
        }

        public Component GetComponent(Type type)
        {
            return Il2CppRuntime.Call<Component>(this, "GetComponent", new global::System.Type[] { typeof(Type) }, type);
        }

        public Component GetComponentInChildren(Type type, bool includeInactive)
        {
            return Il2CppRuntime.Call<Component>(this, "GetComponentInChildren", new global::System.Type[] { typeof(Type), typeof(bool) }, type, includeInactive);
        }

        public Component GetComponentInParent(Type type, bool includeInactive)
        {
            return Il2CppRuntime.Call<Component>(this, "GetComponentInParent", new global::System.Type[] { typeof(Type), typeof(bool) }, type, includeInactive);
        }

        public Component[] GetComponents(Type type)
        {
            return Il2CppRuntime.Call<Component[]>(this, "GetComponents", new global::System.Type[] { typeof(Type) }, type);
        }

        public Component AddComponent(Type componentType)
        {
            return Il2CppRuntime.Call<Component>(this, "AddComponent", new global::System.Type[] { typeof(Type) }, componentType);
        }

        public void SetActive(bool value)
        {
            Il2CppRuntime.InvokeVoid(this, "SetActive", new global::System.Type[] { typeof(bool) }, value);
        }

        public bool CompareTag(string tag)
        {
            return Il2CppRuntime.Call<bool>(this, "CompareTag", new global::System.Type[] { typeof(string) }, tag);
        }

        public void SendMessage(string methodName, SendMessageOptions options)
        {
            Il2CppRuntime.InvokeVoid(this, "SendMessage", new global::System.Type[] { typeof(string), typeof(SendMessageOptions) }, methodName, options);
        }

        public static GameObject Find(string name)
        {
            return Il2CppRuntime.CallStatic<GameObject>("UnityEngine", "GameObject", "Find", new global::System.Type[] { typeof(string) }, name);
        }
    }

    // ═══════════════════════════════════════
    //  Transform
    // ═══════════════════════════════════════

    public partial class Transform : Component
    {
        public Transform(IntPtr nativePtr) : base(nativePtr) { }

        // Properties
        public Vector3 position
        {
            get => Il2CppRuntime.Call<Vector3>(this, "get_position", global::System.Type.EmptyTypes);
            set => Il2CppRuntime.InvokeVoid(this, "set_position", new[] { typeof(Vector3) }, value);
        }

        public Vector3 localPosition
        {
            get => Il2CppRuntime.Call<Vector3>(this, "get_localPosition", global::System.Type.EmptyTypes);
            set => Il2CppRuntime.InvokeVoid(this, "set_localPosition", new[] { typeof(Vector3) }, value);
        }

        public Vector3 eulerAngles
        {
            get => Il2CppRuntime.Call<Vector3>(this, "get_eulerAngles", global::System.Type.EmptyTypes);
            set => Il2CppRuntime.InvokeVoid(this, "set_eulerAngles", new[] { typeof(Vector3) }, value);
        }

        public Vector3 localEulerAngles
        {
            get => Il2CppRuntime.Call<Vector3>(this, "get_localEulerAngles", global::System.Type.EmptyTypes);
            set => Il2CppRuntime.InvokeVoid(this, "set_localEulerAngles", new[] { typeof(Vector3) }, value);
        }

        public Vector3 right
        {
            get => Il2CppRuntime.Call<Vector3>(this, "get_right", global::System.Type.EmptyTypes);
            set => Il2CppRuntime.InvokeVoid(this, "set_right", new[] { typeof(Vector3) }, value);
        }

        public Vector3 up
        {
            get => Il2CppRuntime.Call<Vector3>(this, "get_up", global::System.Type.EmptyTypes);
            set => Il2CppRuntime.InvokeVoid(this, "set_up", new[] { typeof(Vector3) }, value);
        }

        public Vector3 forward
        {
            get => Il2CppRuntime.Call<Vector3>(this, "get_forward", global::System.Type.EmptyTypes);
            set => Il2CppRuntime.InvokeVoid(this, "set_forward", new[] { typeof(Vector3) }, value);
        }

        public Quaternion rotation
        {
            get => Il2CppRuntime.Call<Quaternion>(this, "get_rotation", global::System.Type.EmptyTypes);
            set => Il2CppRuntime.InvokeVoid(this, "set_rotation", new[] { typeof(Quaternion) }, value);
        }

        public Quaternion localRotation
        {
            get => Il2CppRuntime.Call<Quaternion>(this, "get_localRotation", global::System.Type.EmptyTypes);
            set => Il2CppRuntime.InvokeVoid(this, "set_localRotation", new[] { typeof(Quaternion) }, value);
        }

        public Vector3 localScale
        {
            get => Il2CppRuntime.Call<Vector3>(this, "get_localScale", global::System.Type.EmptyTypes);
            set => Il2CppRuntime.InvokeVoid(this, "set_localScale", new[] { typeof(Vector3) }, value);
        }

        public Transform parent
        {
            get => Il2CppRuntime.Call<Transform>(this, "get_parent", global::System.Type.EmptyTypes);
            set => Il2CppRuntime.InvokeVoid(this, "set_parent", new[] { typeof(Transform) }, value);
        }

        public Transform root
        {
            get => Il2CppRuntime.Call<Transform>(this, "get_root", global::System.Type.EmptyTypes);
        }

        public int childCount
        {
            get => Il2CppRuntime.Call<int>(this, "get_childCount", global::System.Type.EmptyTypes);
        }

        public Vector3 lossyScale
        {
            get => Il2CppRuntime.Call<Vector3>(this, "get_lossyScale", global::System.Type.EmptyTypes);
        }

        public bool hasChanged
        {
            get => Il2CppRuntime.Call<bool>(this, "get_hasChanged", global::System.Type.EmptyTypes);
            set => Il2CppRuntime.InvokeVoid(this, "set_hasChanged", new[] { typeof(bool) }, value);
        }

        // Methods
        public void SetParent(Transform p)
        {
            Il2CppRuntime.InvokeVoid(this, "SetParent", new global::System.Type[] { typeof(Transform) }, p);
        }

        public void SetParent(Transform parent, bool worldPositionStays)
        {
            Il2CppRuntime.InvokeVoid(this, "SetParent", new global::System.Type[] { typeof(Transform), typeof(bool) }, parent, worldPositionStays);
        }

        public void Translate(Vector3 translation, Space relativeTo)
        {
            Il2CppRuntime.InvokeVoid(this, "Translate", new global::System.Type[] { typeof(Vector3), typeof(Space) }, translation, relativeTo);
        }

        public void Translate(Vector3 translation)
        {
            Il2CppRuntime.InvokeVoid(this, "Translate", new global::System.Type[] { typeof(Vector3) }, translation);
        }

        public void Rotate(Vector3 eulers, Space relativeTo)
        {
            Il2CppRuntime.InvokeVoid(this, "Rotate", new global::System.Type[] { typeof(Vector3), typeof(Space) }, eulers, relativeTo);
        }

        public void Rotate(Vector3 eulers)
        {
            Il2CppRuntime.InvokeVoid(this, "Rotate", new global::System.Type[] { typeof(Vector3) }, eulers);
        }

        public Vector3 TransformDirection(Vector3 direction)
        {
            return Il2CppRuntime.Call<Vector3>(this, "TransformDirection", new global::System.Type[] { typeof(Vector3) }, direction);
        }

        public Vector3 InverseTransformDirection(Vector3 direction)
        {
            return Il2CppRuntime.Call<Vector3>(this, "InverseTransformDirection", new global::System.Type[] { typeof(Vector3) }, direction);
        }

        public Vector3 TransformPoint(Vector3 position)
        {
            return Il2CppRuntime.Call<Vector3>(this, "TransformPoint", new global::System.Type[] { typeof(Vector3) }, position);
        }

        public Vector3 InverseTransformPoint(Vector3 position)
        {
            return Il2CppRuntime.Call<Vector3>(this, "InverseTransformPoint", new global::System.Type[] { typeof(Vector3) }, position);
        }

        public void DetachChildren()
        {
            Il2CppRuntime.InvokeVoid(this, "DetachChildren", global::System.Type.EmptyTypes);
        }

        public void SetAsFirstSibling()
        {
            Il2CppRuntime.InvokeVoid(this, "SetAsFirstSibling", global::System.Type.EmptyTypes);
        }

        public void SetAsLastSibling()
        {
            Il2CppRuntime.InvokeVoid(this, "SetAsLastSibling", global::System.Type.EmptyTypes);
        }

        public int GetSiblingIndex()
        {
            return Il2CppRuntime.Call<int>(this, "GetSiblingIndex", global::System.Type.EmptyTypes);
        }

        public Transform Find(string n)
        {
            return Il2CppRuntime.Call<Transform>(this, "Find", new global::System.Type[] { typeof(string) }, n);
        }

        public bool IsChildOf(Transform parent)
        {
            return Il2CppRuntime.Call<bool>(this, "IsChildOf", new global::System.Type[] { typeof(Transform) }, parent);
        }

        public Transform GetChild(int index)
        {
            return Il2CppRuntime.Call<Transform>(this, "GetChild", new global::System.Type[] { typeof(int) }, index);
        }
    }

    // ═══════════════════════════════════════
    //  Camera
    // ═══════════════════════════════════════

    public partial class Camera : Behaviour
    {
        public Camera(IntPtr nativePtr) : base(nativePtr) { }

        // Properties
        public float nearClipPlane
        {
            get => Il2CppRuntime.Call<float>(this, "get_nearClipPlane", global::System.Type.EmptyTypes);
            set => Il2CppRuntime.InvokeVoid(this, "set_nearClipPlane", new[] { typeof(float) }, value);
        }

        public float farClipPlane
        {
            get => Il2CppRuntime.Call<float>(this, "get_farClipPlane", global::System.Type.EmptyTypes);
            set => Il2CppRuntime.InvokeVoid(this, "set_farClipPlane", new[] { typeof(float) }, value);
        }

        public float fieldOfView
        {
            get => Il2CppRuntime.Call<float>(this, "get_fieldOfView", global::System.Type.EmptyTypes);
            set => Il2CppRuntime.InvokeVoid(this, "set_fieldOfView", new[] { typeof(float) }, value);
        }

        public float orthographicSize
        {
            get => Il2CppRuntime.Call<float>(this, "get_orthographicSize", global::System.Type.EmptyTypes);
            set => Il2CppRuntime.InvokeVoid(this, "set_orthographicSize", new[] { typeof(float) }, value);
        }

        public bool orthographic
        {
            get => Il2CppRuntime.Call<bool>(this, "get_orthographic", global::System.Type.EmptyTypes);
            set => Il2CppRuntime.InvokeVoid(this, "set_orthographic", new[] { typeof(bool) }, value);
        }

        public float depth
        {
            get => Il2CppRuntime.Call<float>(this, "get_depth", global::System.Type.EmptyTypes);
            set => Il2CppRuntime.InvokeVoid(this, "set_depth", new[] { typeof(float) }, value);
        }

        public float aspect
        {
            get => Il2CppRuntime.Call<float>(this, "get_aspect", global::System.Type.EmptyTypes);
            set => Il2CppRuntime.InvokeVoid(this, "set_aspect", new[] { typeof(float) }, value);
        }

        public int cullingMask
        {
            get => Il2CppRuntime.Call<int>(this, "get_cullingMask", global::System.Type.EmptyTypes);
            set => Il2CppRuntime.InvokeVoid(this, "set_cullingMask", new[] { typeof(int) }, value);
        }

        public Color backgroundColor
        {
            get => Il2CppRuntime.Call<Color>(this, "get_backgroundColor", global::System.Type.EmptyTypes);
            set => Il2CppRuntime.InvokeVoid(this, "set_backgroundColor", new[] { typeof(Color) }, value);
        }

        public CameraClearFlags clearFlags
        {
            get => Il2CppRuntime.Call<CameraClearFlags>(this, "get_clearFlags", global::System.Type.EmptyTypes);
            set => Il2CppRuntime.InvokeVoid(this, "set_clearFlags", new[] { typeof(CameraClearFlags) }, value);
        }

        public Rect rect
        {
            get => Il2CppRuntime.Call<Rect>(this, "get_rect", global::System.Type.EmptyTypes);
            set => Il2CppRuntime.InvokeVoid(this, "set_rect", new[] { typeof(Rect) }, value);
        }

        public Rect pixelRect
        {
            get => Il2CppRuntime.Call<Rect>(this, "get_pixelRect", global::System.Type.EmptyTypes);
            set => Il2CppRuntime.InvokeVoid(this, "set_pixelRect", new[] { typeof(Rect) }, value);
        }

        public int pixelWidth
        {
            get => Il2CppRuntime.Call<int>(this, "get_pixelWidth", global::System.Type.EmptyTypes);
        }

        public int pixelHeight
        {
            get => Il2CppRuntime.Call<int>(this, "get_pixelHeight", global::System.Type.EmptyTypes);
        }

        public Matrix4x4 cameraToWorldMatrix
        {
            get => Il2CppRuntime.Call<Matrix4x4>(this, "get_cameraToWorldMatrix", global::System.Type.EmptyTypes);
        }

        public Matrix4x4 worldToCameraMatrix
        {
            get => Il2CppRuntime.Call<Matrix4x4>(this, "get_worldToCameraMatrix", global::System.Type.EmptyTypes);
        }

        public Matrix4x4 projectionMatrix
        {
            get => Il2CppRuntime.Call<Matrix4x4>(this, "get_projectionMatrix", global::System.Type.EmptyTypes);
            set => Il2CppRuntime.InvokeVoid(this, "set_projectionMatrix", new[] { typeof(Matrix4x4) }, value);
        }

        // Static properties
        public static Camera main
        {
            get => Il2CppRuntime.CallStatic<Camera>("UnityEngine", "Camera", "get_main", global::System.Type.EmptyTypes);
        }

        public static Camera current
        {
            get => Il2CppRuntime.CallStatic<Camera>("UnityEngine", "Camera", "get_current", global::System.Type.EmptyTypes);
        }

        public static int allCamerasCount
        {
            get => Il2CppRuntime.CallStatic<int>("UnityEngine", "Camera", "get_allCamerasCount", global::System.Type.EmptyTypes);
        }

        // Methods
        public Vector3 WorldToScreenPoint(Vector3 position)
        {
            return Il2CppRuntime.Call<Vector3>(this, "WorldToScreenPoint", new global::System.Type[] { typeof(Vector3) }, position);
        }

        public Vector3 ScreenToWorldPoint(Vector3 position)
        {
            return Il2CppRuntime.Call<Vector3>(this, "ScreenToWorldPoint", new global::System.Type[] { typeof(Vector3) }, position);
        }

        public Vector3 WorldToViewportPoint(Vector3 position)
        {
            return Il2CppRuntime.Call<Vector3>(this, "WorldToViewportPoint", new global::System.Type[] { typeof(Vector3) }, position);
        }

        public Vector3 ViewportToWorldPoint(Vector3 position)
        {
            return Il2CppRuntime.Call<Vector3>(this, "ViewportToWorldPoint", new global::System.Type[] { typeof(Vector3) }, position);
        }
    }

    // ═══════════════════════════════════════
    //  Screen
    // ═══════════════════════════════════════

    public partial class Screen : Il2CppObject
    {
        public Screen(IntPtr nativePtr) : base(nativePtr) { }

        // Properties
        public static int width
        {
            get => Il2CppRuntime.CallStatic<int>("UnityEngine", "Screen", "get_width", global::System.Type.EmptyTypes);
        }

        public static int height
        {
            get => Il2CppRuntime.CallStatic<int>("UnityEngine", "Screen", "get_height", global::System.Type.EmptyTypes);
        }

        public static float dpi
        {
            get => Il2CppRuntime.CallStatic<float>("UnityEngine", "Screen", "get_dpi", global::System.Type.EmptyTypes);
        }

        public static Resolution currentResolution
        {
            get => Il2CppRuntime.CallStatic<Resolution>("UnityEngine", "Screen", "get_currentResolution", global::System.Type.EmptyTypes);
        }

        public static bool fullScreen
        {
            get => Il2CppRuntime.CallStatic<bool>("UnityEngine", "Screen", "get_fullScreen", global::System.Type.EmptyTypes);
        }

        public static FullScreenMode fullScreenMode
        {
            set => Il2CppRuntime.InvokeStaticVoid("UnityEngine", "Screen", "set_fullScreenMode", new[] { typeof(FullScreenMode) }, value);
        }

        // Methods
        public static void SetResolution(int width, int height, bool fullscreen)
        {
            Il2CppRuntime.InvokeStaticVoid("UnityEngine", "Screen", "SetResolution", new global::System.Type[] { typeof(int), typeof(int), typeof(bool) }, width, height, fullscreen);
        }
    }

    // ═══════════════════════════════════════
    //  Application
    // ═══════════════════════════════════════

    public partial class Application : Il2CppObject
    {
        public Application(IntPtr nativePtr) : base(nativePtr) { }

        // Properties
        public static bool isPlaying
        {
            get => Il2CppRuntime.CallStatic<bool>("UnityEngine", "Application", "get_isPlaying", global::System.Type.EmptyTypes);
        }

        public static bool isFocused
        {
            get => Il2CppRuntime.CallStatic<bool>("UnityEngine", "Application", "get_isFocused", global::System.Type.EmptyTypes);
        }

        public static string dataPath
        {
            get => Il2CppRuntime.CallStatic<string>("UnityEngine", "Application", "get_dataPath", global::System.Type.EmptyTypes);
        }

        public static string streamingAssetsPath
        {
            get => Il2CppRuntime.CallStatic<string>("UnityEngine", "Application", "get_streamingAssetsPath", global::System.Type.EmptyTypes);
        }

        public static string persistentDataPath
        {
            get => Il2CppRuntime.CallStatic<string>("UnityEngine", "Application", "get_persistentDataPath", global::System.Type.EmptyTypes);
        }

        public static string unityVersion
        {
            get => Il2CppRuntime.CallStatic<string>("UnityEngine", "Application", "get_unityVersion", global::System.Type.EmptyTypes);
        }

        public static string version
        {
            get => Il2CppRuntime.CallStatic<string>("UnityEngine", "Application", "get_version", global::System.Type.EmptyTypes);
        }

        public static string productName
        {
            get => Il2CppRuntime.CallStatic<string>("UnityEngine", "Application", "get_productName", global::System.Type.EmptyTypes);
        }

        public static string companyName
        {
            get => Il2CppRuntime.CallStatic<string>("UnityEngine", "Application", "get_companyName", global::System.Type.EmptyTypes);
        }

        public static RuntimePlatform platform
        {
            get => Il2CppRuntime.CallStatic<RuntimePlatform>("UnityEngine", "Application", "get_platform", global::System.Type.EmptyTypes);
        }

        public static bool isMobilePlatform
        {
            get => Il2CppRuntime.CallStatic<bool>("UnityEngine", "Application", "get_isMobilePlatform", global::System.Type.EmptyTypes);
        }

        public static SystemLanguage systemLanguage
        {
            get => Il2CppRuntime.CallStatic<SystemLanguage>("UnityEngine", "Application", "get_systemLanguage", global::System.Type.EmptyTypes);
        }

        public static int targetFrameRate
        {
            set => Il2CppRuntime.InvokeStaticVoid("UnityEngine", "Application", "set_targetFrameRate", new[] { typeof(int) }, value);
        }

        public static bool runInBackground
        {
            set => Il2CppRuntime.InvokeStaticVoid("UnityEngine", "Application", "set_runInBackground", new[] { typeof(bool) }, value);
        }

        // Methods
        public static void Quit()
        {
            Il2CppRuntime.InvokeStaticVoid("UnityEngine", "Application", "Quit", global::System.Type.EmptyTypes);
        }

        public static void Quit(int exitCode)
        {
            Il2CppRuntime.InvokeStaticVoid("UnityEngine", "Application", "Quit", new global::System.Type[] { typeof(int) }, exitCode);
        }

        public static void OpenURL(string url)
        {
            Il2CppRuntime.InvokeStaticVoid("UnityEngine", "Application", "OpenURL", new global::System.Type[] { typeof(string) }, url);
        }
    }

    // ═══════════════════════════════════════
    //  Time
    // ═══════════════════════════════════════

    public partial class Time : Il2CppObject
    {
        public Time(IntPtr nativePtr) : base(nativePtr) { }

        // Properties
        public static float time
        {
            get => Il2CppRuntime.CallStatic<float>("UnityEngine", "Time", "get_time", global::System.Type.EmptyTypes);
        }

        public static double timeAsDouble
        {
            get => Il2CppRuntime.CallStatic<double>("UnityEngine", "Time", "get_timeAsDouble", global::System.Type.EmptyTypes);
        }

        public static float deltaTime
        {
            get => Il2CppRuntime.CallStatic<float>("UnityEngine", "Time", "get_deltaTime", global::System.Type.EmptyTypes);
        }

        public static float unscaledTime
        {
            get => Il2CppRuntime.CallStatic<float>("UnityEngine", "Time", "get_unscaledTime", global::System.Type.EmptyTypes);
        }

        public static float unscaledDeltaTime
        {
            get => Il2CppRuntime.CallStatic<float>("UnityEngine", "Time", "get_unscaledDeltaTime", global::System.Type.EmptyTypes);
        }

        public static float fixedDeltaTime
        {
            get => Il2CppRuntime.CallStatic<float>("UnityEngine", "Time", "get_fixedDeltaTime", global::System.Type.EmptyTypes);
            set => Il2CppRuntime.InvokeStaticVoid("UnityEngine", "Time", "set_fixedDeltaTime", new[] { typeof(float) }, value);
        }

        public static float smoothDeltaTime
        {
            get => Il2CppRuntime.CallStatic<float>("UnityEngine", "Time", "get_smoothDeltaTime", global::System.Type.EmptyTypes);
        }

        public static float timeScale
        {
            get => Il2CppRuntime.CallStatic<float>("UnityEngine", "Time", "get_timeScale", global::System.Type.EmptyTypes);
            set => Il2CppRuntime.InvokeStaticVoid("UnityEngine", "Time", "set_timeScale", new[] { typeof(float) }, value);
        }

        public static int frameCount
        {
            get => Il2CppRuntime.CallStatic<int>("UnityEngine", "Time", "get_frameCount", global::System.Type.EmptyTypes);
        }

        public static float realtimeSinceStartup
        {
            get => Il2CppRuntime.CallStatic<float>("UnityEngine", "Time", "get_realtimeSinceStartup", global::System.Type.EmptyTypes);
        }
    }

    // ═══════════════════════════════════════
    //  Debug
    // ═══════════════════════════════════════

    public partial class Debug : Il2CppObject
    {
        public Debug(IntPtr nativePtr) : base(nativePtr) { }

        // Properties
        public static bool isDebugBuild
        {
            get => Il2CppRuntime.CallStatic<bool>("UnityEngine", "Debug", "get_isDebugBuild", global::System.Type.EmptyTypes);
        }

        // Methods
        public static void DrawLine(Vector3 start, Vector3 end, Color color, float duration, bool depthTest)
        {
            Il2CppRuntime.InvokeStaticVoid("UnityEngine", "Debug", "DrawLine", new global::System.Type[] { typeof(Vector3), typeof(Vector3), typeof(Color), typeof(float), typeof(bool) }, start, end, color, duration, depthTest);
        }

        public static void DrawRay(Vector3 start, Vector3 dir, Color color, float duration)
        {
            Il2CppRuntime.InvokeStaticVoid("UnityEngine", "Debug", "DrawRay", new global::System.Type[] { typeof(Vector3), typeof(Vector3), typeof(Color), typeof(float) }, start, dir, color, duration);
        }

        public static void Log(object message)
        {
            Il2CppRuntime.InvokeStaticVoid("UnityEngine", "Debug", "Log", new global::System.Type[] { typeof(object) }, message);
        }

        public static void LogWarning(object message)
        {
            Il2CppRuntime.InvokeStaticVoid("UnityEngine", "Debug", "LogWarning", new global::System.Type[] { typeof(object) }, message);
        }

        public static void LogError(object message)
        {
            Il2CppRuntime.InvokeStaticVoid("UnityEngine", "Debug", "LogError", new global::System.Type[] { typeof(object) }, message);
        }

        public static void Assert(bool condition)
        {
            Il2CppRuntime.InvokeStaticVoid("UnityEngine", "Debug", "Assert", new global::System.Type[] { typeof(bool) }, condition);
        }

        public static void Assert(bool condition, string message)
        {
            Il2CppRuntime.InvokeStaticVoid("UnityEngine", "Debug", "Assert", new global::System.Type[] { typeof(bool), typeof(string) }, condition, message);
        }
    }

    // ═══════════════════════════════════════
    //  AsyncOperation (stub for SceneManager returns)
    // ═══════════════════════════════════════

    public partial class AsyncOperation : Il2CppObject
    {
        public AsyncOperation(IntPtr nativePtr) : base(nativePtr) { }

        public bool isDone
        {
            get => Il2CppRuntime.Call<bool>(this, "get_isDone", global::System.Type.EmptyTypes);
        }

        public float progress
        {
            get => Il2CppRuntime.Call<float>(this, "get_progress", global::System.Type.EmptyTypes);
        }

        public bool allowSceneActivation
        {
            get => Il2CppRuntime.Call<bool>(this, "get_allowSceneActivation", global::System.Type.EmptyTypes);
            set => Il2CppRuntime.InvokeVoid(this, "set_allowSceneActivation", new[] { typeof(bool) }, value);
        }
    }
}

// ═══════════════════════════════════════
//  Scene Management
// ═══════════════════════════════════════

namespace UnityEngine.SceneManagement
{
    public enum LoadSceneMode
    {
        Single = 0,
        Additive = 1
    }

    public struct Scene
    {
        public int m_Handle;
    }

    public partial class SceneManager : Il2CppObject
    {
        public SceneManager(IntPtr nativePtr) : base(nativePtr) { }

        // Properties
        public static int sceneCount
        {
            get => Il2CppRuntime.CallStatic<int>("UnityEngine.SceneManagement", "SceneManager", "get_sceneCount", global::System.Type.EmptyTypes);
        }

        // Methods
        public static Scene GetActiveScene()
        {
            return Il2CppRuntime.CallStatic<Scene>("UnityEngine.SceneManagement", "SceneManager", "GetActiveScene", global::System.Type.EmptyTypes);
        }

        public static Scene GetSceneAt(int index)
        {
            return Il2CppRuntime.CallStatic<Scene>("UnityEngine.SceneManagement", "SceneManager", "GetSceneAt", new global::System.Type[] { typeof(int) }, index);
        }

        public static void LoadScene(string sceneName)
        {
            Il2CppRuntime.InvokeStaticVoid("UnityEngine.SceneManagement", "SceneManager", "LoadScene", new global::System.Type[] { typeof(string) }, sceneName);
        }

        public static AsyncOperation LoadSceneAsync(string sceneName, LoadSceneMode mode)
        {
            return Il2CppRuntime.CallStatic<AsyncOperation>("UnityEngine.SceneManagement", "SceneManager", "LoadSceneAsync", new global::System.Type[] { typeof(string), typeof(LoadSceneMode) }, sceneName, mode);
        }

        public static AsyncOperation LoadSceneAsync(string sceneName)
        {
            return Il2CppRuntime.CallStatic<AsyncOperation>("UnityEngine.SceneManagement", "SceneManager", "LoadSceneAsync", new global::System.Type[] { typeof(string) }, sceneName);
        }
    }
}
