// ==============================
// ModBase - Abstract Base Class for Mods
// ==============================
// All user mods should inherit from this class

using System;

namespace GameSDK.ModHost
{
    /// <summary>
    /// Abstract base class for all MDB mods.
    /// Inherit from this class and override the virtual methods to create a mod.
    /// </summary>
    public abstract class ModBase
    {
        /// <summary>
        /// Information about this mod (set from ModAttribute or defaults).
        /// </summary>
        public ModInfo Info { get; internal set; }

        /// <summary>
        /// Logger instance for this mod.
        /// </summary>
        public ModLogger Logger { get; internal set; }

        /// <summary>
        /// Called once when the mod is loaded. Use for initialization.
        /// This is called as soon as the mod DLL is loaded, before the game fully starts.
        /// </summary>
        public virtual void OnLoad() { }

        /// <summary>
        /// Called every frame during Unity's Update loop.
        /// Use for regular game logic that needs to run each frame.
        /// </summary>
        public virtual void OnUpdate() { }

        /// <summary>
        /// Called at fixed time intervals during Unity's FixedUpdate loop.
        /// Use for physics-related updates.
        /// </summary>
        public virtual void OnFixedUpdate() { }

        /// <summary>
        /// Called after all Update calls during Unity's LateUpdate loop.
        /// Use for camera follow logic or other updates that should happen after other updates.
        /// </summary>
        public virtual void OnLateUpdate() { }
    }

    /// <summary>
    /// Contains metadata information about a mod.
    /// </summary>
    public class ModInfo
    {
        /// <summary>
        /// The unique identifier for this mod.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// The display name of this mod.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The version of this mod.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// The author of this mod.
        /// </summary>
        public string Author { get; set; }

        /// <summary>
        /// A brief description of this mod.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The file path of the mod DLL.
        /// </summary>
        public string FilePath { get; set; }

        public ModInfo()
        {
            Id = "unknown";
            Name = "Unknown Mod";
            Version = "1.0.0";
            Author = "Unknown";
            Description = "";
        }

        public override string ToString()
        {
            return $"{Name} v{Version} by {Author}";
        }
    }
}
