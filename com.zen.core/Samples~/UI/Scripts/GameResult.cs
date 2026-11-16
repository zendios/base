using System;
using System.Collections.Generic;

/// <summary>
/// One game, passed as the data of GameState WaitWin / Win / WaitLose / Lose: UIPopupResult shows it, GameFlow and
/// UIPopupRevive read the revive part. GameFlow builds it for the mode of the sample (Casual: level, stars, score;
/// Racing: place, distance, coins picked); a game builds its own the same way.
/// </summary>
[Serializable]
public class GameResult
{
    /// <summary>One line of the result: "Score", "1,250", +125 coins.</summary>
    [Serializable]
    public class Row
    {
        public string title;
        public string detail;
        public int coins;
    }

    public bool isWin;
    /// <summary>0 to 3 (0 = no star shown).</summary>
    public int stars;
    public string title;
    public string description;
    /// <summary>The game so far, shown by the revive popup ("350 m", "Score 1,200").</summary>
    public string progress;
    public List<Row> rows = new List<Row>();
    public int revives;
    /// <summary>Revives still allowed (set when the player fails): GameFlow offers one when above 0.</summary>
    public int revivesLeft;

    /// <summary>Coins of every row.</summary>
    public int Total
    {
        get
        {
            int total = 0;
            foreach (var row in rows)
                total += row.coins;
            return total;
        }
    }

    public void Add(string rowTitle, string detail, int coins) =>
        rows.Add(new Row { title = rowTitle, detail = detail, coins = coins });
}
