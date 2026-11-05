using UnityEngine;
using UnityEngine.UI;

/// <summary>Short message strip at the bottom of the screen ("no room here").</summary>
public class ToastUI : MonoBehaviour
{
    public static ToastUI Instance { get; private set; }

    public Text label;
    public CanvasGroup group;
    public float holdTime = 1.6f;

    float timer;

    void Awake()
    {
        Instance = this;
        if (group != null) group.alpha = 0f;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static void Show(string message)
    {
        if (Instance == null) return;
        Instance.ShowInternal(message);
    }

    void ShowInternal(string message)
    {
        if (label != null) label.text = message;
        timer = holdTime;
        if (group != null) group.alpha = 1f;
    }

    void Update()
    {
        if (timer <= 0f) return;
        timer -= Time.unscaledDeltaTime;
        if (group != null) group.alpha = Mathf.Clamp01(timer / 0.4f);
    }
}
