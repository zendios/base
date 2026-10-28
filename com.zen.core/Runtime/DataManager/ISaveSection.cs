namespace Base
{
    /// <summary>
    /// Game data saved in Base's save file under its own key, so a game keeps data Base does not know
    /// (levels, cars, upgrades...) without editing the package. Register it with DataManager.RegisterSection
    /// before the save loads, e.g. from a [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] method.
    /// </summary>
    public interface ISaveSection
    {
        /// <summary>Unique key of this data in the save file. Never change it once released.</summary>
        string SaveKey { get; }

        /// <summary>The data to save, as JSON (JsonUtility.ToJson works).</summary>
        string ToJson();

        /// <summary>Called on load only when the save holds this key (JsonUtility.FromJsonOverwrite works).</summary>
        void FromJson(string json);
    }
}
