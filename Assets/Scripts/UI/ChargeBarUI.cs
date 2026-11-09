using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The charge gauge, exactly as drawn in the art: a cat with a three segment bar next to it.
///
/// One sprite per state - 0 is the empty bar, 1..3 fill the yellow / orange / red segments -
/// so showing the charge is a sprite swap. Sprites live in Resources/UI/ChargeBar_&lt;n&gt;.
/// </summary>
public class ChargeBarUI : MonoBehaviour
{
    /// <summary>Empty + one sprite per charge level.</summary>
    public const int States = 4;

    /// <summary>Height in reference-resolution pixels; the canvas scales with the screen.</summary>
    public const float Height = 84f;

    static readonly Vector2 Margin = new Vector2(24f, 24f);

    static ChargeBarUI instance;

    Image image;
    Sprite[] sprites;
    int shown = -1;

    /// <summary>Builds the widget once per scene. Safe to call from every player Awake.</summary>
    public static void Ensure()
    {
        if (instance != null) return;

        var canvas = UIFactory.CreateCanvas("ChargeBarCanvas");

        var rt = UIFactory.Rect(canvas.transform, "ChargeBar");
        rt.anchorMin = rt.anchorMax = Vector2.zero;     // bottom left of the screen
        rt.pivot = Vector2.zero;
        rt.anchoredPosition = Margin;

        var img = rt.gameObject.AddComponent<Image>();
        img.raycastTarget = false;                      // the HUD must never eat a click
        img.preserveAspect = true;

        var ui = canvas.gameObject.AddComponent<ChargeBarUI>();
        ui.image = img;
        ui.sprites = LoadSprites();

        // Match the art's aspect so preserveAspect has nothing to letterbox.
        var first = ui.sprites.Length > 0 ? ui.sprites[0] : null;
        float aspect = first != null && first.rect.height > 0f
            ? first.rect.width / first.rect.height
            : 4f;
        rt.sizeDelta = new Vector2(Height * aspect, Height);

        instance = ui;
        ui.SetLevel(0);
    }

    /// <summary>0 = not charging, 1..3 = the level the player is holding.</summary>
    public static void SetCharge(int level)
    {
        if (instance == null) return;
        instance.SetLevel(level);
    }

    static Sprite[] LoadSprites()
    {
        var list = new Sprite[States];
        int found = 0;
        for (int i = 0; i < States; i++)
        {
            var s = Resources.Load<Sprite>("UI/ChargeBar_" + i);
            if (s == null) break;
            list[i] = s;
            found = i + 1;
        }

        if (found == States) return list;

        var trimmed = new Sprite[found];
        System.Array.Copy(list, trimmed, found);
        return trimmed;
    }

    void SetLevel(int level)
    {
        if (image == null || sprites == null || sprites.Length == 0) return;

        level = Mathf.Clamp(level, 0, sprites.Length - 1);
        if (level == shown) return;

        shown = level;
        var sprite = sprites[level];
        if (sprite != null) image.sprite = sprite;
    }
}
