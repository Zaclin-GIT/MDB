namespace MDB.Explorer.ImGui
{
    /// <summary>
    /// Represents a scene in Unity.
    /// </summary>
    public class SceneInfo
    {
        public int Handle { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        public bool IsLoaded { get; set; }
        public int RootCount { get; set; }
    }
}
