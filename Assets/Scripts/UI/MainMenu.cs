using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Start screen + level select. The level buttons are generated at runtime from GameFlow.LevelCount.</summary>
public class MainMenu : MonoBehaviour
{
    public GameObject mainPanel;
    public GameObject levelPanel;
    public Text[] levelLabels;

    void Awake()
    {
        Time.timeScale = 1f;
        AudioManager.PlayBgm("menu");

        // Title / subtitle / footer / level labels are saved with the built-in font inside
        // the scene; re-font them here so swapping PixelFont.ttf is the only step needed.
        // snapSizes: keep every size on the 16px pixel grid (128 / 96 / 48 / 32 / 16).
        PixelFont.ApplyTo(transform, true);
        PixelFont.OutlineBigText(transform);

        BuildLevelButtons();
        EnsureSettingsButton();
        Refresh();
    }

    /// <summary>
    /// Puts a "设置" button between START and QUIT. It is built at runtime so the scene
    /// keeps its original layout. All three buttons share START's anchor, and the gaps
    /// are plain pixel offsets, so the result does not depend on the canvas size
    /// (the CanvasScaler has not run yet when Awake is called).
    /// </summary>
    void EnsureSettingsButton()
    {
        if (mainPanel == null) return;

        var parent = mainPanel.transform;
        if (parent.Find("SettingsButton") != null) return;

        var start = parent.Find("StartButton") as RectTransform;
        var quit = parent.Find("QuitButton") as RectTransform;

        float anchorX = start != null ? start.anchorMin.x : 0.5f;
        float anchorY = start != null ? start.anchorMin.y : 0.4f;
        float startY = start != null ? start.anchoredPosition.y : 0f;
        float startHalf = start != null ? start.sizeDelta.y * 0.5f : 48f;
        var anchor = new Vector2(anchorX, anchorY);

        const float buttonHeight = 80f;
        const float gap = 18f;

        float settingsY = startY - startHalf - gap - buttonHeight * 0.5f;

        var button = UIFactory.MakeButton(parent, "SettingsButton", "设置",
            anchor, new Vector2(420f, buttonHeight),
            new Color(0.12f, 0.16f, 0.24f, 1f), 32, Color.white);

        var settingsRect = (RectTransform)button.transform;
        settingsRect.anchorMin = anchor;
        settingsRect.anchorMax = anchor;
        settingsRect.anchoredPosition = new Vector2(0f, settingsY);
        button.onClick.AddListener(SettingsPanel.OpenSettings);

        // QUIT follows the new button so the three stay evenly spaced.
        if (quit != null)
        {
            float quitHalf = quit.sizeDelta.y * 0.5f;
            quit.anchorMin = anchor;
            quit.anchorMax = anchor;
            quit.anchoredPosition = new Vector2(quit.anchoredPosition.x,
                settingsY - buttonHeight * 0.5f - gap - quitHalf);
        }
    }

    void OnEnable()
    {
        Refresh();
    }

    public void OpenLevelSelect()
    {
        UiTone.Click();
        if (mainPanel != null) mainPanel.SetActive(false);
        if (levelPanel != null) levelPanel.SetActive(true);
        Refresh();
    }

    public void BackToMain()
    {
        UiTone.Click();
        if (levelPanel != null) levelPanel.SetActive(false);
        if (mainPanel != null) mainPanel.SetActive(true);
        Refresh();
    }

    public void PlayLevel(int index)
    {
        UiTone.Click();
        GameFlow.LoadLevel(index);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>Rebuild the grid of level buttons so any number of levels fits.</summary>
    void BuildLevelButtons()
    {
        if (levelPanel == null) return;

        var old = new List<GameObject>();
        foreach (Transform child in levelPanel.transform)
        {
            if (child.name.StartsWith("LevelButton")) old.Add(child.gameObject);
        }
        for (int i = 0; i < old.Count; i++) Destroy(old[i]);

        const int columns = 3;
        var labels = new List<Text>();

        for (int i = 0; i < GameFlow.LevelCount; i++)
        {
            int col = i % columns;
            int row = i / columns;

            var btn = UIFactory.MakeButton(levelPanel.transform, "LevelButton" + (i + 1), "第 " + (i + 1) + " 关",
                new Vector2(0.5f, 0.70f), new Vector2(300f, 120f), new Color(0.12f, 0.14f, 0.20f), 32, Color.white);
            ((RectTransform)btn.transform).anchoredPosition = new Vector2((col - (columns - 1) * 0.5f) * 340f, -row * 140f);

            int index = i;
            btn.onClick.AddListener(() => GameFlow.LoadLevel(index));

            labels.Add(btn.GetComponentInChildren<Text>());
        }

        levelLabels = labels.ToArray();
    }

    void Refresh()
    {
        if (levelLabels == null) return;
        for (int i = 0; i < levelLabels.Length; i++)
        {
            if (levelLabels[i] == null) continue;
            levelLabels[i].text = "第 " + (i + 1) + " 关" +
                                  (GameFlow.IsCleared(i) ? "\n<size=" + PixelFont.PixelUnit + ">已通关</size>" : "");
        }
    }
}
