namespace AVAPirates.Rules;

/// <summary>What the isles trade in. Each port grows some of them cheap and wants others dear.</summary>
public enum Good { Sugar, Tobacco, Spice, Cloth }

public static class Goods
{
    /// <summary>A fair price for a ton of each, before a port's needs move it.</summary>
    public static readonly int[] Base = [30, 40, 70, 55];

    public static string Name(Good g) => g.ToString();
}
