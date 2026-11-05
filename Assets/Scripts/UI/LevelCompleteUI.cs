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

        if (titleText != null) titleText.text = "LEVEL " + (index + 1) + " CLEARED";
        if (hintText != null) hintText.text = "you launched the stickman to the top";
        if (nextButton != null) nextButton.gameObject.SetActive(index + 1 < GameFlow.LevelCount);

        if (panel != null) panel.SetActive(true);
        Time.timeScale = 0f;

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
        GameFlow.LoadLevel(levelIndex);
    }

    public void Levels()
    {
        Time.timeScale = 1f;
        GameFlow.LoadMenu();
    }

    public void Next()
    {
        Time.timeScale = 1f;
        GameFlow.LoadNext(levelIndex);
    }
}
