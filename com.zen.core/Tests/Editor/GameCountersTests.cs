using Base;
using NUnit.Framework;

public class GameCountersTests
{
    [Test]
    public void NewGame_CountsAPlay_ResumeAndReviveDoNot()
    {
        var user = new UserData();

        GameCounters.Count(user, GameState.Play, GameState.Main);
        GameCounters.Count(user, GameState.Play, GameState.Pause);
        GameCounters.Count(user, GameState.Play, GameState.ReviveContinue);
        GameCounters.Count(user, GameState.Play, GameState.ReviveCheckPoint);
        GameCounters.Count(user, GameState.Play, GameState.Lose);

        Assert.AreEqual(2, user.totalPlay);
    }

    [Test]
    public void WinAndLose_CountTotalsAndStreaks()
    {
        var user = new UserData();

        GameCounters.Count(user, GameState.Win, GameState.Play);
        GameCounters.Count(user, GameState.Win, GameState.Play);
        Assert.AreEqual(2, user.winStreak);

        GameCounters.Count(user, GameState.Lose, GameState.Play);

        Assert.AreEqual(2, user.totalWin);
        Assert.AreEqual(1, user.totalLose);
        Assert.AreEqual(1, user.loseStreak);
        Assert.AreEqual(0, user.winStreak);
    }

    [Test]
    public void OtherStates_CountNothing()
    {
        var user = new UserData();

        Assert.IsFalse(GameCounters.Count(user, GameState.Pause, GameState.Play));
        Assert.IsFalse(GameCounters.Count(user, GameState.Main, GameState.Win));
        Assert.AreEqual(0, user.totalPlay + user.totalWin + user.totalLose);
    }
}
