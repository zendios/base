using Base;
using NUnit.Framework;

public class GameSettingsTests
{
    [SetUp]
    public void SetUp() => GameSettings.ResetStatics();

    [TearDown]
    public void TearDown() => GameSettings.ResetStatics();

    [Test]
    public void Defaults_AreOn()
    {
        Assert.IsTrue(GameSettings.SoundOn);
        Assert.IsTrue(GameSettings.MusicOn);
    }

    [Test]
    public void Change_RaisesEvent_OnlyWhenTheValueChanges()
    {
        int calls = 0;
        bool last = true;
        GameSettings.OnSoundChanged += on => { calls++; last = on; };

        GameSettings.SoundOn = false;
        GameSettings.SoundOn = false;

        Assert.AreEqual(1, calls);
        Assert.IsFalse(last);
        Assert.IsTrue(GameSettings.MusicOn);
    }

    [Test]
    public void SaveSection_RoundTrip_RestoresValuesAndRaisesEvents()
    {
        GameSettings.SoundOn = false;
        string json = GameSettings.SaveSection.ToJson();
        GameSettings.ResetStatics();
        bool? sound = null, music = null;
        GameSettings.OnSoundChanged += on => sound = on;
        GameSettings.OnMusicChanged += on => music = on;

        GameSettings.SaveSection.FromJson(json);

        Assert.IsFalse(GameSettings.SoundOn);
        Assert.IsTrue(GameSettings.MusicOn);
        Assert.AreEqual(false, sound);
        Assert.AreEqual(true, music);
    }

    [Test]
    public void SaveSection_KeyIsSettings()
    {
        Assert.AreEqual("settings", GameSettings.SaveSection.SaveKey);
    }
}
