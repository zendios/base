using System;
using Base;
using UnityEngine;

/// <summary>
/// Player fields the game adds on top of Base's UserData (which lives in the package and is not edited).
/// One serializable class, saved as one ISaveSection. Read and write it through SamplePlayerProgress.Current.
/// </summary>
[Serializable]
public class SamplePlayerProgress : ISaveSection
{
    /// <summary>The loaded progress of this session.</summary>
    public static SamplePlayerProgress Current { get; } = new SamplePlayerProgress();

    [SerializeField] private float _levelPoint = 0.5f;
    public int nitro;
    public float shield;
    public LevelData levelData = new LevelData { level = 1, levelMax = 10, processBase = 1000 };

    public float LevelPoint
    {
        get => _levelPoint;
        set => _levelPoint = Mathf.Max(0, value);
    }

    public string SaveKey => "sample_player";

    public string ToJson() => JsonUtility.ToJson(this);

    public void FromJson(string json) => JsonUtility.FromJsonOverwrite(json, this);
}
