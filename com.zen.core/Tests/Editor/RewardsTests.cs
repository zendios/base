using Base;
using NUnit.Framework;

public class RewardsTests
{
    [Test]
    public void Booster_AddsToTheCount_FromTheUnsetValue()
    {
        var item = new ItemData { id = "magnet" };   // count starts at -1
        var reward = new Reward { type = CurrencyType.Booster, id = "magnet", amount = 2 };

        Assert.IsTrue(Rewards.GrantItem(item, reward, 2));
        Assert.IsTrue(Rewards.GrantItem(item, reward, 4));

        Assert.AreEqual(6, item.count);
        Assert.IsTrue(item.isOwner);
    }

    [Test]
    public void Item_IsOwnedOnce()
    {
        var item = new ItemData { id = "car_pickup" };
        var reward = new Reward { type = CurrencyType.Item, id = "car_pickup", amount = 1 };

        Assert.IsTrue(Rewards.GrantItem(item, reward, 1));
        Assert.IsTrue(item.isOwner);
        Assert.IsTrue(item.isUnlocked);
        Assert.IsFalse(Rewards.GrantItem(item, reward, 1));
    }

    [Test]
    public void OnlyCoinsAndBoosters_CanDouble()
    {
        Assert.IsTrue(new Reward { type = CurrencyType.Soft }.CanDouble);
        Assert.IsTrue(new Reward { type = CurrencyType.Booster }.CanDouble);
        Assert.IsFalse(new Reward { type = CurrencyType.Hard }.CanDouble);
        Assert.IsFalse(new Reward { type = CurrencyType.Item }.CanDouble);
    }
}
