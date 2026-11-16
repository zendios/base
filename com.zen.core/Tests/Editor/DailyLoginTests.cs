using System;
using System.Collections.Generic;
using Base;
using NUnit.Framework;
using UnityEngine;

public class DailyLoginTests
{
    private static readonly DateTime Day1 = new DateTime(2026, 10, 30);

    private DailyLoginDatas config;

    [SetUp]
    public void SetUp()
    {
        DailyLogin.ResetStatics();
        config = ScriptableObject.CreateInstance<DailyLoginDatas>();
        config.list = new List<DailyLoginData>
        {
            Day(CurrencyType.Soft, 100), Day(CurrencyType.Booster, 2), Day(CurrencyType.Hard, 30),
        };
        DailyLogin.SetDatasForTests(config);
    }

    [TearDown]
    public void TearDown()
    {
        DailyLogin.ResetStatics();
        UnityEngine.Object.DestroyImmediate(config);
    }

    [Test]
    public void WithoutDatas_NothingToClaim()
    {
        DailyLogin.SetDatasForTests(null);

        Assert.IsFalse(DailyLogin.IsAvailable);
        Assert.IsFalse(DailyLogin.CanClaimOn(Day1));
        Assert.AreEqual(-1, DailyLogin.ClaimOn(Day1));
    }

    [Test]
    public void FirstClaim_IsDay1_OnceADay()
    {
        Assert.IsTrue(DailyLogin.CanClaimOn(Day1));
        Assert.AreEqual(0, DailyLogin.ClaimOn(Day1));
        Assert.IsFalse(DailyLogin.CanClaimOn(Day1));
        Assert.AreEqual(-1, DailyLogin.ClaimOn(Day1));
        Assert.AreEqual(0, DailyLogin.ClaimedTodayOn(Day1));
        Assert.AreEqual(1, DailyLogin.DayIndex);
    }

    [Test]
    public void NextDay_IsDay2()
    {
        DailyLogin.ClaimOn(Day1);

        Assert.AreEqual(1, DailyLogin.ClaimOn(Day1.AddDays(1)));
    }

    [Test]
    public void AfterTheLastDay_TheCalendarStartsAgain()
    {
        DailyLogin.ClaimOn(Day1);
        DailyLogin.ClaimOn(Day1.AddDays(1));
        Assert.AreEqual(2, DailyLogin.ClaimOn(Day1.AddDays(2)));

        Assert.AreEqual(0, DailyLogin.DayIndex);
        Assert.AreEqual(1, DailyLogin.Cycle);
        Assert.AreEqual(2, DailyLogin.ClaimedTodayOn(Day1.AddDays(2)));
        Assert.AreEqual(0, DailyLogin.ClaimOn(Day1.AddDays(3)));
    }

    [Test]
    public void MissedDay_Continues_WithoutResetOnMiss()
    {
        DailyLogin.ClaimOn(Day1);

        Assert.AreEqual(1, DailyLogin.ClaimOn(Day1.AddDays(5)));
    }

    [Test]
    public void MissedDay_StartsAgain_WithResetOnMiss()
    {
        config.resetOnMiss = true;
        DailyLogin.ClaimOn(Day1);
        Assert.AreEqual(1, DailyLogin.ClaimOn(Day1.AddDays(1)));   // the next day is not a miss

        Assert.AreEqual(0, DailyLogin.ClaimOn(Day1.AddDays(3)));
    }

    [Test]
    public void ClockMovedBack_OpensNoClaim()
    {
        DailyLogin.ClaimOn(Day1);
        DailyLogin.ClaimOn(Day1.AddDays(1));

        Assert.IsFalse(DailyLogin.CanClaimOn(Day1));
        Assert.AreEqual(-1, DailyLogin.ClaimOn(Day1));
        Assert.AreEqual(-1, DailyLogin.ClaimOn(Day1.AddDays(1)));
        Assert.AreEqual(2, DailyLogin.ClaimOn(Day1.AddDays(2)));
    }

    [Test]
    public void SetLastClaimYesterday_MakesTodayClaimable()
    {
        DailyLogin.ClaimOn(Day1);

        DailyLogin.SetLastClaim(Day1.AddDays(-1));

        Assert.IsTrue(DailyLogin.CanClaimOn(Day1));
    }

    [Test]
    public void ClaimDouble_OnlyForCoinsAndBoosters()
    {
        Assert.IsTrue(DailyLogin.CanClaimDoubleOn(Day1));   // day 1: coins
        DailyLogin.ClaimOn(Day1);
        DailyLogin.SetLastClaim(Day1.AddDays(-1));
        Assert.IsTrue(DailyLogin.CanClaimDoubleOn(Day1));   // day 2: booster
        DailyLogin.ClaimOn(Day1);
        DailyLogin.SetLastClaim(Day1.AddDays(-1));
        Assert.IsFalse(DailyLogin.CanClaimDoubleOn(Day1));  // day 3: gems

        config.adDouble = false;
        DailyLogin.ClaimOn(Day1);
        DailyLogin.SetLastClaim(Day1.AddDays(-1));
        Assert.IsFalse(DailyLogin.CanClaimDoubleOn(Day1));  // day 1 again, but adDouble off
    }

    [Test]
    public void DayKey_IsYearMonthDay()
    {
        Assert.AreEqual(20261030, DailyLogin.DayKey(Day1));
        Assert.Less(DailyLogin.DayKey(Day1), DailyLogin.DayKey(Day1.AddDays(1)));
    }

    private static DailyLoginData Day(CurrencyType type, int amount) =>
        new DailyLoginData { reward = new Reward { type = type, id = type == CurrencyType.Booster ? "magnet" : null, amount = amount } };
}
