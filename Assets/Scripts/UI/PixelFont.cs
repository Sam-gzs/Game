using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One switch for the whole UI font. Drop a pixel font at
///
///     Assets/Resources/Fonts/PixelFont.ttf   (or .otf)
///
/// and every label built by <see cref="UIFactory"/> plus every legacy Text already saved
/// in a scene picks it up - no inspector dragging, nothing to re-wire per scene.
/// While the file is missing the game quietly falls back to Unity's built-in font, so a
/// missing asset can never break a build.
/// </summary>
public static class PixelFont
{
    /// <summary>Resources-relative path, i.e. Assets/Resources/Fonts/PixelFont.ttf</summary>
    public const string ResourcePath = "Fonts/PixelFont";

    /// <summary>
    /// The font's native dot size in pixels (秋叶圆体 16). Text stays crisp only at whole
    /// multiples of this; swap the font and this number has to follow it.
    /// </summary>
    public const int PixelUnit = 16;

    static Font cached;
    static bool searched;

    public static Font Get()
    {
        if (searched) return cached;
        searched = true;

        cached = Resources.Load<Font>(ResourcePath);
        if (cached == null) cached = UIFactory.BuiltinFont;
        return cached;
    }

    /// <summary>
    /// Nearest whole multiple of <see cref="PixelUnit"/> (never smaller than one dot),
    /// so 30 -> 32, 28 -> 32, 68 -> 64.
    /// </summary>
    public static int Snap(int fontSize)
    {
        int steps = Mathf.Max(1, Mathf.RoundToInt(fontSize / (float)PixelUnit));
        return steps * PixelUnit;
    }

    /// <summary>Re-font every Text under <paramref name="root"/>, including inactive ones.</summary>
    /// <param name="snapSizes">Also pull each fontSize onto the pixel grid.</param>
    public static void ApplyTo(Transform root, bool snapSizes = false)
    {
        if (root == null) return;

        var font = Get();
        if (font == null) return;

        var texts = root.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            var t = texts[i];
            if (t == null) continue;
            if (t.font != font) t.font = font;
            if (snapSizes) t.fontSize = Snap(t.fontSize);
        }
    }

    /// <summary>
    /// Chunky 2-4px dark outline on the big labels only, which is what makes a pixel title
    /// read as a sticker instead of a system font. Small labels are left alone.
    /// </summary>
    public static void OutlineBigText(Transform root, int minFontSize = 48)
    {
        if (root == null) return;

        var texts = root.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            var t = texts[i];
            if (t == null || t.fontSize < minFontSize) continue;

            var o = t.GetComponent<Outline>();
            if (o == null) o = t.gameObject.AddComponent<Outline>();

            float d = Mathf.Clamp(Mathf.RoundToInt(t.fontSize / 24f), 2, 4);
            o.effectColor = new Color(0.06f, 0.08f, 0.13f, 0.92f);
            o.effectDistance = new Vector2(d, -d);
            o.useGraphicAlpha = true;
        }
    }
}
