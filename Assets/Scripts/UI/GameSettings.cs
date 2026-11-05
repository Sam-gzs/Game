using UnityEngine;

/// <summary>
/// Player-facing audio settings. The volume is stored in PlayerPrefs and applied to the
/// global AudioListener, so every AudioSource in every scene follows it without wiring.
/// </summary>
public static class GameSettings
{
    const string VolumeKey = "stickman_volume";
    const string DefaultVolumeKey = "stickman_volume_set";

    static float cached = -1f;

    /// <summary>0..1 master volume.</summary>
    public static float MasterVolume
    {
        get
        {
            if (cached < 0f)
            {
                // First launch: no saved value yet, start on a comfortable 80%.
                bool saved = PlayerPrefs.GetInt(DefaultVolumeKey, 0) == 1;
                cached = saved ? Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 0.8f)) : 0.8f;
            }
            return cached;
        }
        set
        {
            cached = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(VolumeKey, cached);
            PlayerPrefs.SetInt(DefaultVolumeKey, 1);
            PlayerPrefs.Save();
            Apply();
        }
    }

    public static void Apply()
    {
        AudioListener.volume = MasterVolume;
    }

    /// <summary>Runs before the first scene so a saved volume is in effect immediately.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ApplyOnBoot()
    {
        Apply();
    }
}
