using System.Collections.Generic;
using Base;
using NUnit.Framework;
using UnityEngine;

public class AchievementTests
{
    private AchievementDatas datas;

    [SetUp]
    public void SetUp()
    {
        Achievements.ResetStatics();
        datas = ScriptableObject.CreateInstance<AchievementDatas>();
        datas.list = new List<MissionData>
        {
            Tier("racer_1", "racer", 1, Missions.EventPlay, 2),
            Tier("racer_2", "racer", 2, Missions.EventPlay, 5),
            Tier("racer_3", "racer", 3, Missions.EventPlay, 10),
            Tier("champion_1", "champion", 1, Missions.EventWin, 1),
        };
        Achievements.SetDatasForTests(datas);
    }

    [TearDown]
    public void TearDown()
    {
        Achievements.ResetStatics();
        Object.DestroyImmediate(datas);
    }

    [Test]
    public void WithoutDatas_NothingVisible()
    {
        Achievements.SetDatasForTests(null);

        Assert.IsFalse(Achievements.IsAvailable);
        Assert.AreEqual(0, Achievements.Visible.Count);
        Assert.IsFalse(Achievements.HasClaimable);
    }

    [Test]
    public void EachGroup_ShowsItsLowestTier()
    {
        CollectionAssert.AreEqual(new[] { "racer_1", "champion_1" }, Ids());
    }

    [Test]
    public void Progress_IsLifetime_AndCappedAtTheTarget()
    {
        Achievements.Add(Missions.EventPlay, 3);
        Achievements.Add(Missions.EventPlay, 1);

        Assert.AreEqual(2, Achievements.Progress(datas.list[0]));
        Assert.AreEqual(4, Achievements.Progress(datas.list[1]));
        Assert.IsTrue(Achievements.CanClaim(datas.list[0]));
        Assert.IsFalse(Achievements.CanClaim(datas.list[1]));
        Assert.IsTrue(Achievements.HasClaimable);
    }

    [Test]
    public void Claim_OnlyWhenDone_ThenTheNextTierShows()
    {
        Assert.IsFalse(Achievements.Claim("racer_1", null));
        Achievements.Add(Missions.EventPlay, 2);

        Assert.IsTrue(Achievements.Claim("racer_1", null));
        Assert.IsFalse(Achievements.Claim("racer_1", null));
        CollectionAssert.AreEqual(new[] { "racer_2", "champion_1" }, Ids());
    }

    [Test]
    public void AllTiersClaimed_TheLastStays()
    {
        Achievements.Add(Missions.EventPlay, 10);
        Achievements.Claim("racer_1", null);
        Achievements.Claim("racer_2", null);
        Achievements.Claim("racer_3", null);

        Assert.AreEqual("racer_3", Ids()[0]);
        Assert.IsTrue(Achievements.IsClaimed(datas.list[2]));
    }

    [Test]
    public void CrossingATarget_RaisesOnCompleted_OncePerTier()
    {
        var completed = new List<string>();
        Missions.OnCompleted += (mission, isAchievement) => completed.Add(mission.id + (isAchievement ? "/a" : "/d"));

        Achievements.Add(Missions.EventPlay, 2);
        Achievements.Add(Missions.EventPlay, 4);
        Achievements.Add(Missions.EventPlay, 1);

        CollectionAssert.AreEqual(new[] { "racer_1/a", "racer_2/a" }, completed);
        Missions.ResetStatics();
    }

    [Test]
    public void AnAchievementAddedLater_CountsWhatWasDone()
    {
        Achievements.Add("jump_over", 7);   // no achievement listens yet

        datas.list.Add(Tier("jumper_1", "jumper", 1, "jump_over", 5));

        Assert.IsTrue(Achievements.CanClaim(datas.list[4]));
    }

    [Test]
    public void Save_KeepsCountersAndClaims()
    {
        Achievements.Add(Missions.EventPlay, 6);
        Achievements.Claim("racer_1", null);
        string json = Achievements.ToJsonForTests();
        Achievements.ResetStatics();
        Achievements.SetDatasForTests(datas);

        Achievements.FromJsonForTests(json);

        Assert.AreEqual(6, Achievements.Progress(datas.list[2]));
        Assert.IsTrue(Achievements.IsClaimed(datas.list[0]));
        CollectionAssert.AreEqual(new[] { "racer_2", "champion_1" }, Ids());
    }

    private static MissionData Tier(string id, string group, int tier, string eventKey, int target) =>
        new MissionData
        {
            id = id, group = group, tier = tier, eventKey = eventKey, target = target,
            reward = new Reward { type = CurrencyType.Soft, amount = 50 },
        };

    private static List<string> Ids()
    {
        var ids = new List<string>();
        foreach (var achievement in Achievements.Visible)
            ids.Add(achievement.id);
        return ids;
    }
}
