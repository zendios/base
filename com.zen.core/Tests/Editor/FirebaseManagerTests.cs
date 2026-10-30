using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Base;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class FirebaseManagerTests
{
    private static Dictionary<string, object> Values(Dictionary<string, object> dictionary, long session = 7) =>
        FirebaseManager.ToParameterValues(dictionary, session).ToDictionary(x => x.Key, x => x.Value);

    [Test]
    public void CallerSession_IsKept_AndDictionaryUnchanged()
    {
        var dictionary = new Dictionary<string, object> { { "session", 3 }, { "level", 1 } };

        var values = Values(dictionary);

        Assert.AreEqual(3L, values["session"]);
        Assert.AreEqual(2, dictionary.Count);
    }

    [Test]
    public void SameDictionary_TwiceSendsSession()
    {
        var dictionary = new Dictionary<string, object> { { "level", 1 } };

        Values(dictionary);
        var values = Values(dictionary);

        Assert.AreEqual(7L, values["session"]);
        Assert.IsFalse(dictionary.ContainsKey("session"));
    }

    [Test]
    public void NoDictionary_SendsSession()
    {
        Assert.AreEqual(7L, Values(null)["session"]);
    }

    [Test]
    public void Values_KeepTheirType_TextIsNotChanged()
    {
        var values = Values(new Dictionary<string, object>
        {
            { "won", true }, { "lost", false }, { "count", 2 }, { "time", 1.5f }, { "mode", GameState.Play },
            { "level_name", "Among Us - 2" },
        });

        Assert.AreEqual(1L, values["won"]);
        Assert.AreEqual(0L, values["lost"]);
        Assert.AreEqual(2L, values["count"]);
        Assert.AreEqual(1.5d, values["time"]);
        Assert.AreEqual("Play", values["mode"]);
        Assert.AreEqual("Among Us - 2", values["level_name"]);
    }

    [Test]
    public void LongText_IsCutTo100()
    {
        var values = Values(new Dictionary<string, object> { { "reason", new string('a', 150) } });

        Assert.AreEqual(FirebaseManager.MaxTextValueLength, ((string)values["reason"]).Length);
    }

    [Test]
    public void MoreThan25Parameters_SendsSessionAnd24()
    {
        var dictionary = Enumerable.Range(0, 30).ToDictionary(i => "p" + i, i => (object)i);
        LogAssert.Expect(LogType.Warning, new Regex("31 parameters"));

        var values = FirebaseManager.ToParameterValues(dictionary, 7);

        Assert.AreEqual(FirebaseManager.MaxParameters, values.Count);
        Assert.AreEqual("session", values[0].Key);
    }

    [Test]
    public void EventName_UpTo40_IsKept()
    {
        string name = "Level_Complete_With_A_Very_Long_Name_40c";

        Assert.AreEqual(name.ToLower(), FirebaseManager.ToFirebaseName(name));
    }

    [Test]
    public void EventName_Over40_IsCutWithoutDots()
    {
        LogAssert.Expect(LogType.Warning, new Regex("longer than 40"));

        string name = FirebaseManager.ToFirebaseName("level-complete with a very long name over 40");

        Assert.AreEqual(FirebaseManager.MaxNameLength, name.Length);
        Assert.IsTrue(Regex.IsMatch(name, "^[a-z0-9_]+$"), name);
    }
}
