// ==============================
// ModAttribute - Mod Metadata Attribute
// ==============================
// Apply this attribute to your mod class to provide metadata

using System;

namespace GameSDK.ModHost
{
    /// <summary>
    /// Attribute to define mod metadata.
    /// Apply this to your class that inherits from ModBase.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class ModAttribute : Attribute
    {
        /// <summary>
        /// The unique identifier for this mod.
        /// Should be in format "AuthorName.ModName" (e.g., "MyName.CoolMod").
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// The display name of this mod.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The version of this mod (e.g., "1.0.0").
        /// </summary>
        public string Version { get; }

        /// <summary>
        /// The author of this mod.
        /// </summary>
        public string Author { get; set; }

        /// <summary>
        /// A brief description of this mod.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Create a new ModAttribute with required parameters.
        /// </summary>
        /// <param name="id">Unique identifier (e.g., "MyName.CoolMod")</param>
        /// <param name="name">Display name</param>
        /// <param name="version">Version string (e.g., "1.0.0")</param>
        public ModAttribute(string id, string name, string version)
        {
            Id = id;
            Name = name;
            Version = version;
            Author = "Unknown";
            Description = "";
        }
    }
}
