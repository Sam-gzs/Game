using UnityEngine;
using UnityEngine.UI;

/// <summary>The "level cleared" panel shown on top of the level.</summary>
public class LevelCompleteUI : MonoBehaviour
{
    public static LevelCompleteUI Instance { get; private set; }

    public GameObject panel;
    public Text titleText;
    public Text hintText;
    public Button retryButton;
    public Button levelsButton;
    public Button nextButton;

    int levelIndex;
    bool shown;

    void Awake()
    {
        Instance = this;
        if (panel != null) panel.SetActive(false);

        // The panel is baked into every level scene with the built-in font: pull it onto the
        // pixel font and the pixel grid here instead of editing six scenes by hand.
        if (panel != null) PixelFont.ApplyTo(panel.transform, true);
        if (panel != null) PixelFont.OutlineBigText(panel.transform);

        SetButtonLabel(retryButton, "重试");
        SetButtonLabel(levelsButton, "选关");
        SetButtonLabel(nextButton, "下一关");
    }

    static void SetButtonLabel(Button button, string text)
    {
        if (button == null) return;
        var label = button.GetComponentInChildren<Text>(true);
        if (label != null) label.text = text;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        // make sure a leftover pause from a previous run cannot stick
        Time.timeScale = 1f;
    }

    public void Show(int index)
    {
        if (shown) return;
        shown = true;
        levelIndex = index;

        GameFlow.MarkCleared(index);

        if (titleText != null) titleText.text = "第 " + (index + 1) + " 关 完成";
        if (hintText != null) hintText.text = string.Empty;   // 不要副标题，只留关卡标题
        if (nextButton != null) nextButton.gameObject.SetActive(index + 1 < GameFlow.LevelCount);

        if (panel != null) panel.SetActive(true);
        Time.timeScale = 0f;
        AudioManager.Play(Sfx.LevelClear);

        var player = Object.FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            var rb = player.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero;
            player.enabled = false;
        }
    }

    public void Retry()
    {
        Time.timeScale = 1f;
        AudioManager.Play(Sfx.LevelRestart);
        GameFlow.LoadLevel(levelIndex);
    }

    public void Levels()
    {
        Time.timeScale = 1f;
        UiTone.Click();
        GameFlow.LoadMenu();
    }

    public void Next()
    {
        Time.timeScale = 1f;
        UiTone.Click();
        GameFlow.LoadNext(levelIndex);
    }
}
