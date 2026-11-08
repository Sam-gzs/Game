using UnityEngine;

/// <summary>
/// The single source of truth for the charge / momentum colour ramp.
/// The POWER bar, the dashed aim preview and the thrown ball all read from here,
/// so they can never drift apart again.
///
/// Charge level 1 = yellow, 2 = orange, 3 = red.
/// </summary>
public static class ChargePalette
{
    public const int MaxCharge = 3;

    static readonly Color Level1 = new Color(1.00f, 0.86f, 0.28f, 1f); // yellow
    static readonly Color Level2 = new Color(1.00f, 0.58f, 0.16f, 1f); // orange
    static readonly Color Level3 = new Color(0.94f, 0.22f, 0.16f, 1f); // red

    /// <summary>Charge level 1..3 (values outside that range are clamped).</summary>
    public static Color ForLevel(int level)
    {
        if (level <= 1) return Level1;
        if (level == 2) return Level2;
        return Level3;
    }

    /// <summary>
    /// A ball's momentum, 0..5: 1..3 reuse the charge colours, 0/4/5 keep their old
    /// "spent / mirrored / maxed" tints. Reflection (mirror) can push a ball past 3.
    /// </summary>
    public static Color ForMomentum(int momentum)
    {
        if (momentum <= 0) return new Color(0.55f, 0.55f, 0.62f, 1f); // spent
        if (momentum >= 5) return new Color(0.55f, 0.95f, 1.00f, 1f); // max momentum
        if (momentum == 4) return new Color(1f, 0.98f, 0.80f, 1f);    // mirrored boost
        return ForLevel(momentum);
    }

    /// <summary>
    /// Ghost marker for a cell the ball cannot be released into. Kept dark and
    /// semi-transparent on purpose: it must not be mistaken for a level-3 (red) dash.
    /// </summary>
    public static readonly Color Denied = new Color(0.92f, 0.24f, 0.30f, 0.42f);

    public static Color WithAlpha(Color c, float alpha)
    {
        c.a = alpha;
        return c;
    }
}
