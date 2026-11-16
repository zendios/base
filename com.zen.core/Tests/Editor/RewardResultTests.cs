using Base.Ads;
using NUnit.Framework;

public class RewardResultTests
{
    [Test]
    public void RewardThenClose_IsSuccessAtOnce()
    {
        var result = Feed(AdState.ShowSuccess, AdState.Close);
        Assert.IsTrue(result.IsDone);
        Assert.AreEqual(AdState.ShowSuccess, result.State);
    }

    [Test]
    public void CloseThenRewardWithinWait_IsSuccess()
    {
        var result = Feed(AdState.Close);
        result.Wait(0.5f);
        Assert.IsFalse(result.IsDone, "a close alone waits for a late reward");
        result.Collect(AdType.Reward, AdState.ShowSuccess);
        Assert.IsTrue(result.IsDone);
        Assert.AreEqual(AdState.ShowSuccess, result.State);
    }

    [Test]
    public void CloseWithoutReward_IsCloseAfterWait()
    {
        var result = Feed(AdState.Close);
        result.Wait(RewardResult.AfterCloseSeconds);
        Assert.IsTrue(result.IsDone);
        Assert.AreEqual(AdState.Close, result.State);
    }

    [Test]
    public void LoadAvailable_IsNotAResult()
    {
        var result = Feed(AdState.LoadAvailable);
        Assert.IsFalse(result.IsDone, "the ad still has to show");
    }

    [Test]
    public void FailedStepThenFill_KeepsWaitingForTheShow()
    {
        var result = Feed(AdState.LoadNotAvailable, AdState.LoadAvailable);
        Assert.IsFalse(result.IsDone);
        result.Collect(AdType.Reward, AdState.ShowSuccess);
        Assert.AreEqual(AdState.ShowSuccess, result.State);
    }

    [TestCase(AdState.LoadNotAvailable)]
    [TestCase(AdState.LoadTimeOut)]
    [TestCase(AdState.ShowFailed)]
    [TestCase(AdState.ShowNotAvailable)]
    [TestCase(AdState.Exception)]
    public void Failure_IsReturnedAtOnce(AdState failure)
    {
        var result = Feed(failure);
        Assert.IsTrue(result.IsDone);
        Assert.AreEqual(failure, result.State);
    }

    [Test]
    public void ShowFailedAfterClose_IsClose()
    {
        var result = Feed(AdState.Close, AdState.ShowFailed);
        result.Wait(RewardResult.AfterCloseSeconds);
        Assert.AreEqual(AdState.Close, result.State);
    }

    [Test]
    public void NothingHappens_IsShowNotAvailableAfterTimeout()
    {
        var result = new RewardResult();
        Assert.IsFalse(result.IsDone, "an ad on screen sends nothing until it closes");
        result.Wait(RewardResult.TimeoutSeconds);
        Assert.IsTrue(result.IsDone);
        Assert.AreEqual(AdState.ShowNotAvailable, result.State);
    }

    [Test]
    public void Type_IsTheLastPlacementType()
    {
        var result = new RewardResult();
        result.Collect(AdType.RewardInter, AdState.ShowSuccess);
        Assert.AreEqual(AdType.RewardInter, result.Type);
    }

    private static RewardResult Feed(params AdState[] states)
    {
        var result = new RewardResult();
        foreach (var state in states)
            result.Collect(AdType.Reward, state);
        return result;
    }
}
