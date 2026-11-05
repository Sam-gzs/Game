using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Controls guide, split across the first few levels. Each hint's viewport
/// anchor is in 0..1 screen space with the origin at the top-left, so it is
/// easy to move later without touching the level scenes.
/// </summary>
public class HudText : MonoBehaviour
{
    public const string DefaultText = "A / D  move     W / SPACE  jump     HOLD LEFT MOUSE  charge 1-3, release to throw (8 directions, aim line)     HOLD Q  follow ball     E  recall ball     R  restart";

    public string message = DefaultText;

    /// <summary>New scenes and old ones alike show the current control hints.</summary>
    public bool useDefaults = true;

    GUIStyle style;
    Vector2 anchor = new Vector2(0.03f, 0.04f);

    struct Hint
    {
        public int level;
        public string text;
        public Vector2 anchor;

        public Hint(int level, string text, Vector2 anchor)
        {
            this.level = level;
            this.text = text;
            this.anchor = anchor;
        }
    }

    // Tutorial order: move/jump -> charge+throw -> retry -> Q camera.
    // Coordinates are viewport (0..1) with (0,0) at the top-left.
    static readonly Hint[] Hints =
    {
        new Hint(0, "A / D  移动\nW / SPACE  跳跃", new Vector2(0.03f, 0.13f)),
        new Hint(1, "按住左键蓄力 1~3\n松开朝鼠标 8 向发射", new Vector2(0.03f, 0.13f)),
        new Hint(2, "按 R 重试本关", new Vector2(0.03f, 0.82f)),
        new Hint(3, "按住 Q 跟随球\n松开回到玩家", new Vector2(0.66f, 0.13f)),
    };

    void Awake()
    {
        int level = CurrentLevelIndex();
        Hint? hint = FindHint(level);
        if (hint.HasValue)
        {
            message = hint.Value.text;
            anchor = hint.Value.anchor;
        }
        else if (useDefaults)
        {
            message = DefaultText;
            anchor = new Vector2(0.03f, 0.04f);
        }
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
    }

    void OnGUI()
    {
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
}
