namespace AVAPirates.Rules;

/// <summary>The four crowns with colonies in the Saltwind Isles, and the outlaws who answer to none.</summary>
public enum Nation { Valdoro, Fennick, Lusande, Brannmark, Outlaw }

public sealed record NationInfo(Nation Nation, string Name, string Adjective, uint Colour, uint Trim, string About);

public static class Nations
{
    public static readonly IReadOnlyList<NationInfo> All =
    [
        new(Nation.Valdoro, "Valdoro", "Valdoran", 0xFFE0B030, 0xFFB02020,
            "The oldest and richest crown in the isles. Its treasure galleons carry silver home twice a year."),
        new(Nation.Fennick, "Fennick", "Fennish", 0xFFD04040, 0xFFF0F0F0,
            "Young, hungry and quarrelsome. Fennick hands out letters of marque to anyone who will harry Valdoro."),
        new(Nation.Lusande, "Lusande", "Lusandish", 0xFF4070E0, 0xFFF0F0F0,
            "Elegant and ambitious, with sugar plantations on every island it can hold."),
        new(Nation.Brannmark, "Brannmark", "Brannish", 0xFFF08020, 0xFF203060,
            "A nation of traders. It would rather sell to both sides of a war than fight in it."),
        new(Nation.Outlaw, "the Brotherhood", "Outlaw", 0xFF262626, 0xFFF0F0F0,
            "Pirates and runaways, with two havens where no crown's law reaches."),
    ];

    public static NationInfo Get(Nation n) => All[(int)n];

    /// <summary>The crowns, without the outlaws.</summary>
    public static readonly Nation[] Crowns = [Nation.Valdoro, Nation.Fennick, Nation.Lusande, Nation.Brannmark];

    /// <summary>The titles a governor may grant, and the favour each needs.</summary>
    public static readonly (string Title, int Favour, int Land)[] Ranks =
    [
        ("none", 0, 0),
        ("Ensign", 2, 0),
        ("Captain", 5, 0),
        ("Major", 9, 50),
        ("Colonel", 14, 100),
        ("Admiral", 20, 200),
        ("Baron", 27, 400),
        ("Count", 35, 700),
        ("Marquis", 44, 1000),
        ("Duke", 54, 1500),
    ];

    public static int RankFor(int favour)
    {
        int r = 0;
        for (int i = 0; i < Ranks.Length; i++)
            if (favour >= Ranks[i].Favour) r = i;
        return r;
    }
}
