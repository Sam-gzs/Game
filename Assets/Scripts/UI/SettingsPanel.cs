using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// One settings overlay for the whole game: opened from the main menu (设置 button),
/// or from inside a level with ESC. In a level it also offers "restart level" and
/// "back to main menu". While open the game is paused (Time.timeScale = 0) and the
/// previous time scale is restored on close, so it can also be opened on top of the
/// level-complete panel without unfreezing it.
/// </summary>
public class SettingsPanel : MonoBehaviour
{
    public static SettingsPanel Instance { get; private set; }

    const float CardWidth = 780f;
    const float MenuCardHeight = 430f;
    const float LevelCardHeight = 630f;
    const float ButtonWidth = 520f;
    const float ButtonHeight = 84f;

    // vertical rhythm, measured from the top edge of the card
    const float TitleFromTop = 66f;
    const float VolumeFromTop = 186f;
    const float ResumeFromTop = 300f;
    const float RestartFromTop = 405f;
    const float MenuFromTop = 510f;
    const float HintFromBottom = 34f;

    static readonly Color DimColor = new Color(0.02f, 0.03f, 0.05f, 0.88f);
    static readonly Color CardColor = new Color(0.09f, 0.11f, 0.16f, 1f);
    static readonly Color Accent = new Color(0.11f, 0.80f, 0.74f, 1f);
    static readonly Color ButtonColor = new Color(0.14f, 0.17f, 0.25f, 1f);
    static readonly Color QuitColor = new Color(0.26f, 0.13f, 0.17f, 1f);
    static readonly Color Muted = new Color(0.62f, 0.68f, 0.82f, 1f);

    Canvas canvas;
    RectTransform card;
    RectTransform titleRect;

    Slider volumeSlider;
    RectTransform sliderRect;
    Text volumeValue;
    RectTransform volumeValueRect;
    Text volumeCaption;

    Button resumeButton;
    Button restartButton;
    Button menuButton;
    RectTransform resumeRect;
    RectTransform restartRect;
    RectTransform menuRect;

    Text hintLabel;
    RectTransform hintRect;

    bool built;
    bool isOpen;
    float previousTimeScale = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;

        var go = new GameObject("SettingsPanel");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<SettingsPanel>();
    }

    /// <summary>Safe entry point: works even before the panel object exists.</summary>
    public static void OpenSettings()
    {
        if (Instance == null) Bootstrap();
        if (Instance != null) Instance.Open();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        Build();
        SetVisible(false);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this) Instance = null;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Never carry the overlay into a freshly loaded scene.
        if (isOpen)
        {
            isOpen = false;
            SetVisible(false);
        }
        previousTimeScale = 1f;
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.escapeKey.wasPressedThisFrame)
        {
            if (isOpen) Close();
            else Open();
        }
    }

    // ------------------------------------------------------------------
    // open / close
    // ------------------------------------------------------------------

    public void Open()
    {
        if (isOpen) return;
        if (!built) Build();

        isOpen = true;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        bool inLevel = InLevel();
        ApplyLayout(inLevel);
        SyncVolumeControls();
        SetVisible(true);

        // Keep keyboard focus off the slider so ESC keeps working predictably.
        if (UnityEngine.EventSystems.EventSystem.current != null)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        SetVisible(false);
        Time.timeScale = previousTimeScale;
        previousTimeScale = 1f;
    }

    void SetVisible(bool visible)
    {
        if (canvas != null) canvas.gameObject.SetActive(visible);
    }

    static bool InLevel()
    {
        if (Object.FindFirstObjectByType<LevelBootstrap>() != null) return true;
        var scene = SceneManager.GetActiveScene();
        return scene.IsValid() && scene.name.StartsWith("Level");
    }

    /// <summary>The menu only needs one button, a level needs three - so the card grows.</summary>
    void ApplyLayout(bool inLevel)
    {
        float height = inLevel ? LevelCardHeight : MenuCardHeight;
        card.sizeDelta = new Vector2(CardWidth, height);
        float half = height * 0.5f;

        restartButton.gameObject.SetActive(inLevel);
        menuButton.gameObject.SetActive(inLevel);

        Place(resumeRect, half - ResumeFromTop);
        Place(titleRect, half - TitleFromTop);
        Place(volumeCaption.rectTransform, half - VolumeFromTop);
        Place(sliderRect, half - VolumeFromTop);
        Place(volumeValueRect, half - VolumeFromTop);
        Place(hintRect, -half + HintFromBottom);

        if (inLevel)
        {
            Place(restartRect, half - RestartFromTop);
            Place(menuRect, half - MenuFromTop);
        }
    }

    static void Place(RectTransform rt, float y)
    {
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y);
    }

    // ------------------------------------------------------------------
    // actions
    // ------------------------------------------------------------------

    void OnRestart()
    {
        UiTone.Click();
        var boot = Object.FindFirstObjectByType<LevelBootstrap>();
        int index = boot != null ? boot.levelIndex : 0;
        Close();
        GameFlow.LoadLevel(index);
    }

    void OnBackToMenu()
    {
        UiTone.Click();
        Close();
        GameFlow.LoadMenu();
    }

    void OnResume()
    {
        UiTone.Click();
        Close();
    }

    // ------------------------------------------------------------------
    // build
    // ------------------------------------------------------------------

    void Build()
    {
        if (built) return;
        built = true;

        var canvasGo = new GameObject("SettingsCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);

        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();
        UIFactory.EnsureEventSystem();

        var root = canvasGo.transform;

        // full-screen dim that also swallows clicks meant for the game
        var dim = UIFactory.FullScreen(root, "Dim", DimColor);
        var dimButton = dim.gameObject.AddComponent<Button>();
        dimButton.transition = Selectable.Transition.None;
        dimButton.onClick.AddListener(OnResume);

        var cardImage = UIFactory.Box(root, "Card", new Vector2(0.5f, 0.5f),
            new Vector2(CardWidth, MenuCardHeight), CardColor);
        card = (RectTransform)cardImage.transform;

        // top accent strip
        var accent = UIFactory.Box(card, "Accent", new Vector2(0.5f, 1f),
            new Vector2(CardWidth, 6f), Accent);
        ((RectTransform)accent.transform).anchoredPosition = new Vector2(0f, -3f);

        var title = UIFactory.Label(card, "Title", "设置", 48, Color.white, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(600f, 64f), Vector2.zero);
        titleRect = title.rectTransform;
        Place(titleRect, MenuCardHeight * 0.5f - TitleFromTop);

        volumeCaption = UIFactory.Label(card, "VolumeCaption", "音量", 32, Color.white,
            TextAnchor.MiddleLeft, new Vector2(0.5f, 0.5f), new Vector2(140f, 44f),
            new Vector2(-300f, 0f));

        volumeSlider = BuildSlider(card, new Vector2(0.5f, 0.5f), new Vector2(10f, 0f),
            new Vector2(420f, 44f));
        sliderRect = (RectTransform)volumeSlider.transform;

        volumeValue = UIFactory.Label(card, "VolumeValue", "80%", 32, Muted, TextAnchor.MiddleRight,
            new Vector2(0.5f, 0.5f), new Vector2(100f, 44f), new Vector2(300f, 0f));
        volumeValueRect = volumeValue.rectTransform;

        resumeButton = UIFactory.MakeButton(card, "ResumeButton", "继续游戏",
            new Vector2(0.5f, 0.5f), new Vector2(ButtonWidth, ButtonHeight), ButtonColor, 32, Color.white);
        resumeRect = (RectTransform)resumeButton.transform;
        resumeButton.onClick.AddListener(OnResume);

        restartButton = UIFactory.MakeButton(card, "RestartButton", "重开本关",
            new Vector2(0.5f, 0.5f), new Vector2(ButtonWidth, ButtonHeight), ButtonColor, 32, Color.white);
        restartRect = (RectTransform)restartButton.transform;
        restartButton.onClick.AddListener(OnRestart);

        menuButton = UIFactory.MakeButton(card, "MenuButton", "返回主菜单",
            new Vector2(0.5f, 0.5f), new Vector2(ButtonWidth, ButtonHeight), QuitColor, 32, Color.white);
        menuRect = (RectTransform)menuButton.transform;
        menuButton.onClick.AddListener(OnBackToMenu);

        hintLabel = UIFactory.Label(card, "Hint", "按 ESC 关闭", 16,
            new Color(0.55f, 0.62f, 0.75f, 0.85f), TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.5f), new Vector2(600f, 34f), Vector2.zero);
        hintRect = hintLabel.rectTransform;

        volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        SyncVolumeControls();
        ApplyLayout(false);
    }

    Slider BuildSlider(Transform parent, Vector2 anchor, Vector2 offset, Vector2 size)
    {
        var rt = UIFactory.Rect(parent, "VolumeSlider");
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = offset;

        var slider = rt.gameObject.AddComponent<Slider>();
        slider.transition = Selectable.Transition.None;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.direction = Slider.Direction.LeftToRight;

        // track
        var background = UIFactory.Box(rt, "Background", new Vector2(0.5f, 0.5f),
            new Vector2(0f, 14f), new Color(1f, 1f, 1f, 0.14f));
        var backgroundRect = (RectTransform)background.transform;
        backgroundRect.anchorMin = new Vector2(0f, 0.5f);
        backgroundRect.anchorMax = new Vector2(1f, 0.5f);
        backgroundRect.sizeDelta = new Vector2(0f, 14f);
        backgroundRect.anchoredPosition = Vector2.zero;

        // fill
        var fillArea = UIFactory.Rect(rt, "Fill Area");
        fillArea.anchorMin = new Vector2(0f, 0.5f);
        fillArea.anchorMax = new Vector2(1f, 0.5f);
        fillArea.offsetMin = new Vector2(9f, -7f);
        fillArea.offsetMax = new Vector2(-9f, 7f);

        var fill = UIFactory.Box(fillArea, "Fill", new Vector2(0f, 0.5f),
            new Vector2(10f, 14f), Accent);
        var fillRect = (RectTransform)fill.transform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        // handle
        var handleArea = UIFactory.Rect(rt, "Handle Slide Area");
        handleArea.anchorMin = Vector2.zero;
        handleArea.anchorMax = Vector2.one;
        handleArea.offsetMin = new Vector2(16f, 0f);
        handleArea.offsetMax = new Vector2(-16f, 0f);

        var handle = UIFactory.Box(handleArea, "Handle", new Vector2(0.5f, 0.5f),
            new Vector2(34f, 34f), Color.white);
        var handleRect = (RectTransform)handle.transform;
        handleRect.anchorMin = new Vector2(0f, 0.5f);
        handleRect.anchorMax = new Vector2(0f, 0.5f);
        handleRect.sizeDelta = new Vector2(34f, 34f);

        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handle;
        slider.value = GameSettings.MasterVolume;

        return slider;
    }

    void SyncVolumeControls()
    {
        float v = GameSettings.MasterVolume;
        if (volumeSlider != null) volumeSlider.SetValueWithoutNotify(v);
        if (volumeValue != null) volumeValue.text = Mathf.RoundToInt(v * 100f) + "%";
    }

    void OnVolumeChanged(float value)
    {
        GameSettings.MasterVolume = value;
        if (volumeValue != null) volumeValue.text = Mathf.RoundToInt(value * 100f) + "%";
        UiTone.Tick();
    }
}

/// <summary>
/// Tiny procedural UI sounds. The project ships no audio clips yet, so the slider needs
/// its own feedback to be audible at all - and because it plays through the same
/// AudioListener, it proves the volume setting actually works.
/// </summary>
static class UiTone
{
    static AudioSource source;
    static AudioClip tickClip;
    static AudioClip clickClip;
    static float nextTickTime;

    static void Ensure()
    {
        if (source != null) return;

        var go = new GameObject("UiTone");
        Object.DontDestroyOnLoad(go);
        source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 1f;

        tickClip = MakeTone("tick", 880f, 0.05f, 0.32f);
        clickClip = MakeTone("click", 520f, 0.07f, 0.36f);
    }

    static AudioClip MakeTone(string name, float frequency, float duration, float amplitude)
    {
        const int sampleRate = 44100;
        int count = Mathf.Max(1, Mathf.CeilToInt(sampleRate * duration));
        var data = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / sampleRate;
            // soft attack + exponential decay so it reads as a "blip", not a beep
            float attack = Mathf.Clamp01(t / 0.004f);
            float decay = Mathf.Exp(-t * 42f);
            // a touch of the fifth makes it sound softer than a pure sine
            float wave = Mathf.Sin(2f * Mathf.PI * frequency * t) * 0.85f
                       + Mathf.Sin(2f * Mathf.PI * frequency * 1.5f * t) * 0.15f;
            data[i] = wave * amplitude * attack * decay;
        }

        var clip = AudioClip.Create(name, count, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>Short pitch blip while dragging the volume slider (throttled).</summary>
    public static void Tick()
    {
        Ensure();
        if (Time.unscaledTime < nextTickTime) return;
        nextTickTime = Time.unscaledTime + 0.07f;
        source.PlayOneShot(tickClip);
    }

    public static void Click()
    {
        Ensure();
        source.PlayOneShot(clickClip);
    }
}
