using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Controls guide, split across the first few levels.
///
/// A hint is either a line of text in screen space, or - when the art drop has key caps
/// for it - a row of key-cap images drawn at the SAME anchor, so swapping text for art
/// never moves the prompt or changes how much of the screen it covers. Hints without
/// key-cap art (e.g. R, which has no art yet) keep the old text panel.
/// </summary>
public class HudText : MonoBehaviour
{
    public const string DefaultText = "A / D  move     W / SPACE  jump     HOLD LEFT MOUSE  charge 1-3, release to throw (8 directions, aim line)     HOLD Q  follow ball     E  recall ball     R  restart";

    public string message = DefaultText;

    /// <summary>New scenes and old ones alike show the current control hints.</summary>
    public bool useDefaults = true;

    [Tooltip("Key cap height as a fraction of the screen height.")]
    public float keyCapScreenFraction = 0.075f;

    GUIStyle style;
    // Top-left hints sit below the charge bar (which is ~60 px tall at 1080p).
    Vector2 anchor = new Vector2(0.03f, 0.19f);

    /// <summary>Key-cap art names for the current hint; null keeps the text panel.</summary>
    string[] keyCaps;

    PlayerController player;

    // Level 1 only: once the player reaches the first three-tall wall, the move/jump prompt
    // is replaced by "hold left mouse to charge". That throw tutorial used to live in level 2
    // and showed up long after the player had already met the wall.
    string[] advanceKeys;
    string advanceText;
    float advanceAtX = float.MaxValue;
    bool advanceShown;

    static readonly Dictionary<string, Texture2D> s_keyTextures = new Dictionary<string, Texture2D>();

    struct Hint
    {
        public int level;
        public string text;
        public Vector2 anchor;
        public string[] keys;

        public Hint(int level, string text, Vector2 anchor, string[] keys = null)
        {
            this.level = level;
            this.text = text;
            this.anchor = anchor;
            this.keys = keys;
        }
    }

    // Tutorial order: move/jump -> charge+throw -> retry -> Q camera.
    // Coordinates are viewport (0..1) with (0,0) at the top-left.
    static readonly Hint[] Hints =
    {
        new Hint(0, "A / D  移动\nW / SPACE  跳跃", new Vector2(0.03f, 0.19f),
            new[] { "key_a", "key_d", "key_w" }),
        new Hint(1, "按住左键蓄力 1~3\n松开朝鼠标 8 向发射", new Vector2(0.03f, 0.19f),
            new[] { "mouse_left" }),
        new Hint(2, "按 R 重试本关", new Vector2(0.03f, 0.82f),
            new[] { "key_r" }),
        new Hint(3, "按住 Q 跟随球\n松开回到玩家", new Vector2(0.66f, 0.13f),
            new[] { "key_q" }),
    };

    void Awake()
    {
        int level = CurrentLevelIndex();
        Hint? hint = FindHint(level);
        if (hint.HasValue)
        {
            message = hint.Value.text;
            anchor = hint.Value.anchor;
            keyCaps = hint.Value.keys;
        }
        else if (useDefaults)
        {
            message = DefaultText;
            anchor = new Vector2(0.03f, 0.19f);
        }
    }

    void Start()
    {
        if (CurrentLevelIndex() != 0) return;   // the throw prompt is a level-1 thing

        float wallX = FindFirstTallWallX();
        if (wallX >= float.MaxValue) return;

        advanceKeys = new[] { "mouse_left" };
        advanceText = "按住左键蓄力";
        advanceAtX = wallX - 3.5f;   // show it while the wall is still a couple of steps away
    }

    void Update()
    {
        if (advanceShown || advanceKeys == null) return;

        if (player == null) player = Object.FindFirstObjectByType<PlayerController>();
        if (player == null || player.transform.position.x < advanceAtX) return;

        advanceShown = true;
        keyCaps = advanceKeys;
        message = advanceText;
    }

    /// <summary>
    /// X of the first column whose stack of tiles ABOVE the floor is three tall - the first
    /// wall the player cannot simply walk or jump over. The level frame is ignored on
    /// purpose: the floor row, the ceiling row and the two side walls are the outline, not
    /// an obstacle. A two-tall step must therefore not count as three just because the
    /// floor tile underneath it is included.
    /// </summary>
    static float FindFirstTallWallX()
    {
        var tiles = Object.FindObjectsByType<GridTile>(FindObjectsSortMode.None);
        if (tiles.Length == 0) return float.MaxValue;

        var columns = new Dictionary<int, SortedSet<int>>();
        int floorY = int.MaxValue, ceilY = int.MinValue;

        for (int i = 0; i < tiles.Length; i++)
        {
            var t = tiles[i];
            if (t == null) continue;

            SortedSet<int> ys;
            if (!columns.TryGetValue(t.cell.x, out ys))
            {
                ys = new SortedSet<int>();
                columns[t.cell.x] = ys;
            }
            ys.Add(t.cell.y);

            if (t.cell.y < floorY) floorY = t.cell.y;
            if (t.cell.y > ceilY) ceilY = t.cell.y;
        }

        int span = ceilY - floorY + 1;
        int best = int.MaxValue;

        foreach (var kv in columns)
        {
            // A side wall fills the whole frame; it is part of the outline, never the wall
            // the prompt is about.
            if (span > 0 && kv.Value.Count >= span) continue;

            int run = 0, bestRun = 0, prev = int.MinValue;
            foreach (int y in kv.Value)
            {
                if (y <= floorY || y >= ceilY)
                {
                    run = 0;
                    prev = y;
                    continue;
                }

                run = prev == y - 1 ? run + 1 : 1;
                if (run > bestRun) bestRun = run;
                prev = y;
            }

            if (bestRun >= 3 && kv.Key < best) best = kv.Key;
        }

        return best == int.MaxValue ? float.MaxValue : best;
    }

    static int CurrentLevelIndex()
    {
        var boot = Object.FindFirstObjectByType<LevelBootstrap>();
        if (boot != null) return boot.levelIndex;

        string scene = SceneManager.GetActiveScene().name;
        if (scene.StartsWith("Level") && int.TryParse(scene.Substring(5), out int n))
            return n - 1;
        return -1;
    }

    static Hint? FindHint(int level)
    {
        for (int i = 0; i < Hints.Length; i++)
            if (Hints[i].level == level) return Hints[i];
        return null;
    }

    public void SetMessage(string text)
    {
        message = text;
        useDefaults = false;
        keyCaps = null;   // an explicit message is text, never key caps
    }

    void OnGUI()
    {
        // Art first: when the hint has key caps they replace the text panel entirely.
        if (DrawKeyCaps()) return;

        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                alignment = TextAnchor.UpperLeft,
                wordWrap = false
            };
        }

        int lines = 1;
        for (int i = 0; i < message.Length; i++)
            if (message[i] == '\n') lines++;

        float width = Mathf.Min(940f, Screen.width - 40f);
        float height = 24f * lines + 10f;
        float x = Mathf.Clamp(anchor.x * Screen.width, 12f, Mathf.Max(12f, Screen.width - width - 12f));
        float y = Mathf.Clamp(anchor.y * Screen.height, 12f, Mathf.Max(12f, Screen.height - height - 12f));
        var rect = new Rect(x, y, width, height);

        Color old = GUI.color;
        GUI.color = new Color(0.03f, 0.05f, 0.09f, 0.62f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = old;

        style.normal.textColor = new Color(0.88f, 0.93f, 1f, 0.96f);
        GUI.Label(new Rect(rect.x + 10f, rect.y + 5f, rect.width - 20f, rect.height - 10f), message, style);
    }

    /// <summary>
    /// Draws the hint's key caps above the player's head, gently bobbing. Returns false
    /// when the hint has no key caps (or the art is missing), so the text panel can take over.
    /// </summary>
    bool DrawKeyCaps()
    {
        if (keyCaps == null || keyCaps.Length == 0) return false;

        // Nothing is drawn unless every cap resolves - a half-drawn row looks broken.
        for (int i = 0; i < keyCaps.Length; i++)
            if (KeyTexture(keyCaps[i]) == null) return false;

        float size = Mathf.Clamp(Screen.height * keyCapScreenFraction, 28f, 110f);
        float gap = size * 0.22f;
        float total = keyCaps.Length * size + (keyCaps.Length - 1) * gap;

        // Same anchor and the same screen clamping the text panel used, so the prompt
        // appears in exactly the place and covers the same visible range as before.
        float x = Mathf.Clamp(anchor.x * Screen.width, 12f, Mathf.Max(12f, Screen.width - total - 12f));
        float y = Mathf.Clamp(anchor.y * Screen.height, 12f, Mathf.Max(12f, Screen.height - size - 12f));

        for (int i = 0; i < keyCaps.Length; i++)
        {
            GUI.DrawTexture(new Rect(x, y, size, size), KeyTexture(keyCaps[i]), ScaleMode.ScaleToFit, true);
            x += size + gap;
        }
        return true;
    }

    static Texture2D KeyTexture(string name)
    {
        Texture2D tex;
        if (s_keyTextures.TryGetValue(name, out tex) && tex != null) return tex;

        var sprite = Resources.Load<Sprite>("Keys/" + name);
        tex = sprite != null ? sprite.texture : Resources.Load<Texture2D>("Keys/" + name);
        s_keyTextures[name] = tex;
        return tex;
    }
}
