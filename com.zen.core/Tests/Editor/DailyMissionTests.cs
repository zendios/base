using System;
using System.Collections.Generic;
using Base;
using NUnit.Framework;
using UnityEngine;

public class DailyMissionTests
{
    private static readonly DateTime Day1 = new DateTime(2026, 10, 30);

    private DailyMissionDatas config;
    private DateTime today;

    [SetUp]
    public void SetUp()
    {
        DailyMissions.ResetStatics();
        config = ScriptableObject.CreateInstance<DailyMissionDatas>();
        config.list = new List<MissionData>
        {
            new MissionData { id = "play_3", eventKey = Missions.EventPlay, target = 3, reward = new Reward { amount = 100 } },
            new MissionData { id = "win_1", eventKey = Missions.EventWin, target = 1, reward = new Reward { amount = 150 } },
            new MissionData { id = "ad_1", eventKey = Missions.EventWatchAd, target = 1, reward = new Reward { amount = 200 } },
            new MissionData { id = "earn_500", eventKey = Missions.EventEarnSoft, target = 500, reward = new Reward { amount = 100 } },
        };
        config.missionsPerDay = 3;
        DailyMissions.SetDatasForTests(config);
        today = Day1;
        DailyMissions.todayProvider = () => today;
    }

    [TearDown]
    public void TearDown()
    {
        DailyMissions.ResetStatics();
        UnityEngine.Object.DestroyImmediate(config);
    }

    [Test]
    public void WithoutDatas_NothingToDo()
    {
        DailyMissions.SetDatasForTests(null);

        Assert.IsFalse(DailyMissions.IsAvailable);
        Assert.AreEqual(0, DailyMissions.Today.Count);
        Assert.IsFalse(DailyMissions.HasClaimable);
        Assert.IsNull(DailyMissions.MarkClaimed("play_3"));
    }

    [Test]
    public void FirstDay_TakesTheFirstMissions_InOrder()
    {
        var ids = Ids();

        CollectionAssert.AreEqual(new[] { "play_3", "win_1", "ad_1" }, ids);
        Assert.AreEqual(0, DailyMissions.Today[0].value);
        Assert.IsFalse(DailyMissions.HasClaimable);
    }

    [Test]
    public void NextDay_RotatesThroughTheList_AndResetsProgress()
    {
        Missions.Report(Missions.EventPlay, 3);
        today = Day1.AddDays(1);

        CollectionAssert.AreEqual(new[] { "earn_500", "play_3", "win_1" }, Ids());
        Assert.AreEqual(0, DailyMissions.Today[1].value);
    }

    [Test]
    public void MissionsPerDayZero_TakesAll()
    {
        config.missionsPerDay = 0;

        Assert.AreEqual(4, DailyMissions.Today.Count);
    }

    [Test]
    public void PickRandom_IsDistinct_AndTheSameForTheSameDay()
    {
        config.pickRandom = true;
        var first = Ids();
        DailyMissions.ResetStatics();
        DailyMissions.SetDatasForTests(config);
        DailyMissions.todayProvider = () => today;

        Assert.AreEqual(3, new HashSet<string>(first).Count);
        CollectionAssert.AreEqual(first, Ids());
    }

    [Test]
    public void Report_AddsUpToTheTarget_OnlyForItsEvent()
    {
        int changed = 0;
        DailyMissions.OnChanged += () => changed++;

        Missions.Report(Missions.EventPlay);
        Missions.Report(Missions.EventPlay, 5);
        Missions.Report("collect_star", 10);   // no mission listens today

        Assert.AreEqual(3, DailyMissions.Today[0].value);
        Assert.AreEqual(0, DailyMissions.Today[1].value);
        Assert.IsTrue(DailyMissions.Today[0].IsDone);
        Assert.IsTrue(DailyMissions.HasClaimable);
        Assert.AreEqual(2, changed);
    }

    [Test]
    public void Claim_OnlyWhenDone_OnlyOnce()
    {
        Assert.IsNull(DailyMissions.MarkClaimed("play_3"));
        Missions.Report(Missions.EventPlay, 3);

        var mission = DailyMissions.MarkClaimed("play_3");

        Assert.AreEqual(100, mission.reward.amount);
        Assert.IsTrue(DailyMissions.Today[0].claimed);
        Assert.IsFalse(DailyMissions.HasClaimable);
        Assert.IsNull(DailyMissions.MarkClaimed("play_3"));
    }

    [Test]
    public void Save_KeepsProgressAndClaims_AndDropsMissionsNoLongerInTheDatas()
    {
        Missions.Report(Missions.EventPlay, 2);
        Missions.Report(Missions.EventWin);
        DailyMissions.MarkClaimed("win_1");
        string json = DailyMissions.ToJsonForTests();
        DailyMissions.ResetStatics();
        config.list.RemoveAt(2);   // "ad_1" removed by an update
        DailyMissions.SetDatasForTests(config);
        DailyMissions.todayProvider = () => today;

        DailyMissions.FromJsonForTests(json);

        CollectionAssert.AreEqual(new[] { "play_3", "win_1" }, Ids());
        Assert.AreEqual(2, DailyMissions.Today[0].value);
        Assert.IsTrue(DailyMissions.Today[1].claimed);
        Assert.AreSame(config.list[0], DailyMissions.Today[0].mission);
    }

    [Test]
    public void ClockMovedBack_KeepsTheList_AndItsProgress()
    {
        Ids();
        today = Day1.AddDays(1);
        Missions.Report(Missions.EventPlay, 2);

        today = Day1;
        CollectionAssert.AreEqual(new[] { "earn_500", "play_3", "win_1" }, Ids());
        Assert.AreEqual(2, DailyMissions.Today[1].value);

        today = Day1.AddDays(1);
        CollectionAssert.AreEqual(new[] { "earn_500", "play_3", "win_1" }, Ids());
        Assert.AreEqual(2, DailyMissions.Today[1].value);
    }

    [Test]
    public void ReachingTheTarget_RaisesOnCompletedOnce()
    {
        var completed = new List<string>();
        Missions.OnCompleted += (mission, isAchievement) => completed.Add(mission.id + (isAchievement ? "/a" : "/d"));
        Ids();

        Missions.Report(Missions.EventPlay, 2);
        Missions.Report(Missions.EventPlay, 1);
        Missions.Report(Missions.EventPlay, 1);   // already done

        CollectionAssert.AreEqual(new[] { "play_3/d" }, completed);
        Missions.ResetStatics();
    }

    [Test]
    public void ResetToday_StartsANewList()
    {
        Missions.Report(Missions.EventPlay, 3);

        DailyMissions.ResetToday();

        CollectionAssert.AreEqual(new[] { "earn_500", "play_3", "win_1" }, Ids());
        Assert.IsFalse(DailyMissions.HasClaimable);
    }

    private static List<string> Ids()
    {
        var ids = new List<string>();
        foreach (var progress in DailyMissions.Today)
            ids.Add(progress.id);
        return ids;
    }
}
