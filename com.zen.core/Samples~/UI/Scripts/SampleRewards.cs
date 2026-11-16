using Base;

/// <summary>Reward label of the sample: "150" (Soft, next to its icon), "10 Token" (Hard), "Magnet x1", "Ninja".</summary>
public static class SampleRewards
{
    public static string Label(Reward reward)
    {
        switch (reward.type)
        {
            case CurrencyType.Hard:
                return reward.amount + " " + CurrencyManager.NameOf(CurrencyType.Hard);
            case CurrencyType.Booster:
                return NameOf(reward.id) + " x" + reward.amount;
            case CurrencyType.Item:
                return NameOf(reward.id);
            default:
                return reward.amount.ToString();
        }
    }

    private static string NameOf(string id)
    {
        foreach (var datas in DataManager.ItemDatas)
        {
            var item = datas.Find(id);
            if (item != null)
                return item.name;
        }
        return id;
    }
}
