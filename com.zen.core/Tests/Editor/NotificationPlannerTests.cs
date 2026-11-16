using System;
using System.Collections.Generic;
using Base;
using NUnit.Framework;

public class NotificationPlannerTests
{
    private static readonly DateTime Morning = new DateTime(2026, 10, 30, 12, 30, 0);
    private static readonly DateTime Evening = new DateTime(2026, 10, 30, 20, 15, 0);

    [Test]
    public void NothingToRemind_NothingPlanned()
    {
        Assert.AreEqual(0, NotificationPlanner.Plan(Morning, 19, false, false, false, 0).Count);
    }

    [Test]
    public void AllClaimed_DayAfter_Day3_Day7_AtTheUsualHour()
    {
        var plan = NotificationPlanner.Plan(Evening, 20, true, false, true, 0);

        CollectionAssert.AreEqual(new[] { NotificationPlanner.TypeDailyReady, NotificationPlanner.TypeComebackD3,
            NotificationPlanner.TypeComebackD7 }, Types(plan));
        Assert.AreEqual(new DateTime(2026, 10, 31, 20, 0, 0), plan[0].fireTime);
        Assert.AreEqual(new DateTime(2026, 11, 2, 20, 0, 0), plan[1].fireTime);
        Assert.AreEqual(new DateTime(2026, 11, 6, 20, 0, 0), plan[2].fireTime);
        Assert.AreEqual(NotificationPlanner.RouteDailyLogin, plan[0].route);
    }

    [Test]
    public void OnePerDay()
    {
        var plan = NotificationPlanner.Plan(Morning, 19, true, true, true, 2);

        var days = new HashSet<DateTime>();
        foreach (var notification in plan)
            Assert.IsTrue(days.Add(notification.fireTime.Date));
    }

    [Test]
    public void RewardsWaiting_Tonight_ToTheMissions()
    {
        var plan = NotificationPlanner.Plan(Morning, 19, true, false, true, 2);

        Assert.AreEqual(NotificationPlanner.TypeRewardsWaiting, plan[0].type);
        Assert.AreEqual(new DateTime(2026, 10, 30, 20, 0, 0), plan[0].fireTime);
        Assert.AreEqual(NotificationPlanner.RouteMissions, plan[0].route);
    }

    [Test]
    public void LoginNotClaimed_RewardsWaiting_ToTheCalendar()
    {
        var plan = NotificationPlanner.Plan(Morning, 19, true, true, true, 0);

        Assert.AreEqual(NotificationPlanner.TypeRewardsWaiting, plan[0].type);
        Assert.AreEqual(NotificationPlanner.RouteDailyLogin, plan[0].route);
    }

    [Test]
    public void RewardsWaiting_NotWhenLeavingAfter19()
    {
        var plan = NotificationPlanner.Plan(Evening, 19, true, true, true, 2);

        CollectionAssert.DoesNotContain(Types(plan), NotificationPlanner.TypeRewardsWaiting);
    }

    [Test]
    public void QuietHours_MoveTheUsualHourInto8To21()
    {
        Assert.AreEqual(21, NotificationPlanner.Plan(Evening, 23, true, false, true, 0)[0].fireTime.Hour);
        Assert.AreEqual(8, NotificationPlanner.Plan(Evening, 2, true, false, true, 0)[0].fireTime.Hour);
    }

    [Test]
    public void MissionsOnly_RouteToMissions()
    {
        var plan = NotificationPlanner.Plan(Evening, 19, false, false, true, 0);

        Assert.AreEqual(NotificationPlanner.RouteMissions, plan[0].route);
    }

    [Test]
    public void UsualHour_IsTheMedianOfTheLast7()
    {
        Assert.AreEqual(NotificationPlanner.DefaultHour, NotificationPlanner.UsualHour(new List<int>()));
        Assert.AreEqual(12, NotificationPlanner.UsualHour(new List<int> { 20, 12, 8 }));
        Assert.AreEqual(20, NotificationPlanner.UsualHour(new List<int> { 1, 1, 1, 20, 20, 20, 20, 12, 12 }));
    }

    [Test]
    public void Offer_AfterDay1_3_7_AtMost3Times()
    {
        Assert.IsTrue(NotificationPlanner.ShouldOffer(1, 0, false, false, false));
        Assert.IsTrue(NotificationPlanner.ShouldOffer(3, 1, false, false, false));
        Assert.IsTrue(NotificationPlanner.ShouldOffer(7, 2, false, false, false));
        Assert.IsFalse(NotificationPlanner.ShouldOffer(2, 0, false, false, false));
        Assert.IsFalse(NotificationPlanner.ShouldOffer(7, 3, false, false, false));
    }

    [Test]
    public void Offer_NotTwiceADay_NotWhenOn_NotWhenTheOsRefused()
    {
        Assert.IsFalse(NotificationPlanner.ShouldOffer(1, 0, true, false, false));
        Assert.IsFalse(NotificationPlanner.ShouldOffer(1, 0, false, true, false));
        Assert.IsFalse(NotificationPlanner.ShouldOffer(1, 0, false, false, true));
    }

    private static List<string> Types(List<PlannedNotification> plan)
    {
        var types = new List<string>();
        foreach (var notification in plan)
            types.Add(notification.type);
        return types;
    }
}
