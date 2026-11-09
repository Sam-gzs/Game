using System;
using UnityEngine;

/// <summary>Every one-shot sound the game can ask for.</summary>
public enum Sfx
{
    Jump,
    Land,
    Throw,
    Recall,
    BreakDirt,
    BreakStone,
    BreakWood,          // 素材未到位：暂用 break_dirt 变调
    RefuseBlocked,
    RefuseAirThrow,
    SwitchPress,
    GateOpen,
    GateClose,
    FallingStart,
    FallingLand,
    StickyGlue,
    RefractorSpent,
    BallRefract,
    BallBlocked,
    BallPassSticky,
    BallStuck,
    BallRest,
    BallSpeed1,
    BallSpeed2,
    BallSpeed3,
    BallSpeed4,
    BallSpeed5,
    MirrorTurn,         // 素材未到位：暂用 ball_refract 变调
    Bounce,             // 素材未到位：暂用 ball_blocked 变调
    PlatePress,         // 素材未到位：暂用 switch_press 变调
    BallSwitchOn,       // 素材未到位：暂用 switch_press 变调
    BallSwitchOff,      // 素材未到位：暂用 switch_press 变调
    Pickup,             // 素材未到位：暂用 switch_press 变调
    LevelClear,
    LevelRestart,
}

/// <summary>
/// The only place that plays sound. Lives in DontDestroyOnLoad, is created on boot, and
/// needs no AudioSource in any scene - gameplay code just calls <see cref="Play"/>.
///
/// Volume: the master slider already drives <c>AudioListener.volume</c> (GameSettings),
/// which scales every voice here, so this class deliberately does NOT apply it again.
///
/// Clips are loaded from Resources/Audio/&lt;id&gt;.wav. A missing clip is silent, never an
/// error - that is how the not-yet-delivered sounds (wood break, mirror, bounce, plate,
/// ball switch, pickup) stay wired without breaking the build.
/// </summary>
public static class AudioManager
{
    const int VoiceCount = 14;
    const string Root = "Audio/";

    struct Def
    {
        public string path;
        public float volume;
        public float pitch;
        public float jitter;       // +/- random pitch, keeps repeated hits from sounding cloned
        public float minInterval;  // seconds before the same id may sound again
        public int maxVoices;      // simultaneous copies of the same clip
        public string fallback;    // stand-in while the real asset is missing
        public float fallbackVolume;
        public float fallbackPitch;
    }

    static Def[] defs;
    static AudioClip[] clips;
    static bool[] usingFallback;
    static bool[] resolved;
    static float[] lastPlay;
    static AudioSource[] voices;
    static AudioSource bgmSource;
    static GameObject root;
    static string bgmName;
    static bool booted;
    static bool loggedFailure;
    static int cursor;

    // ------------------------------------------------------------------
    // boot
    // ------------------------------------------------------------------

    /// <summary>
    /// The project runs with "Enter Play Mode Options" and domain reload OFF, so these statics
    /// survive from one play session to the next while the AudioSources they point at do not.
    /// Clearing them here is what keeps the first sound of a run from throwing
    /// "the object of type AudioSource has been destroyed" - and that exception used to abort
    /// the throw that called it, which looked like "the cat sometimes refuses to fire".
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        booted = false;
        loggedFailure = false;
        root = null;
        voices = null;
        bgmSource = null;
        bgmName = null;
        defs = null;
        clips = null;
        usingFallback = null;
        resolved = null;
        lastPlay = null;
        cursor = 0;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        booted = true;

        BuildDefs();

        int count = defs.Length;
        clips = new AudioClip[count];
        usingFallback = new bool[count];
        resolved = new bool[count];
        lastPlay = new float[count];
        for (int i = 0; i < count; i++) lastPlay[i] = -999f;

        if (root == null)
        {
            root = new GameObject("AudioManager");
            UnityEngine.Object.DontDestroyOnLoad(root);
        }

        voices = new AudioSource[VoiceCount];
        for (int i = 0; i < VoiceCount; i++)
        {
            var src = root.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 0f;   // 2D: the game is a side view, panning would fight the mix
            src.volume = 1f;
            voices[i] = src;
        }

        bgmSource = root.AddComponent<AudioSource>();
        bgmSource.playOnAwake = false;
        bgmSource.loop = true;
        bgmSource.spatialBlend = 0f;
        bgmSource.volume = 0.45f;

        // One-shots are short; letting them ring out through a scene load sounds better
        // than a hard cut (the restart jingle in particular). Only BGM is scene-bound.
    }

    static void BuildDefs()
    {
        defs = new Def[Enum.GetValues(typeof(Sfx)).Length];

        void Set(Sfx id, string path, float volume,
                 float pitch = 1f, float jitter = 0.04f, float minInterval = 0.05f,
                 int maxVoices = 2, string fallback = null,
                 float fallbackVolume = 1f, float fallbackPitch = 1f)
        {
            defs[(int)id] = new Def
            {
                path = path,
                volume = volume,
                pitch = pitch,
                jitter = jitter,
                minInterval = minInterval,
                maxVoices = maxVoices,
                fallback = fallback,
                fallbackVolume = fallbackVolume,
                fallbackPitch = fallbackPitch,
            };
        }

        Set(Sfx.Jump, "jump", 0.85f, jitter: 0.05f, minInterval: 0.06f);
        Set(Sfx.Land, "land", 0.80f, jitter: 0.06f, minInterval: 0.06f);
        Set(Sfx.Throw, "throw", 0.90f, jitter: 0.05f, minInterval: 0.06f);
        Set(Sfx.Recall, "recall", 0.85f, jitter: 0.03f, minInterval: 0.12f);

        Set(Sfx.BreakDirt, "break_dirt", 0.80f, jitter: 0.07f, minInterval: 0.04f, maxVoices: 3);
        Set(Sfx.BreakStone, "break_stone", 0.80f, jitter: 0.06f, minInterval: 0.04f, maxVoices: 3);
        Set(Sfx.BreakWood, "break_wood", 0.80f, jitter: 0.06f, minInterval: 0.04f, maxVoices: 3,
            fallback: "break_dirt", fallbackPitch: 0.92f);

        Set(Sfx.RefuseBlocked, "refuse_blocked", 0.70f, jitter: 0.03f, minInterval: 0.25f);
        Set(Sfx.RefuseAirThrow, "refuse_air_throw", 0.70f, jitter: 0.03f, minInterval: 0.30f);

        Set(Sfx.SwitchPress, "switch_press", 0.75f, jitter: 0.05f, minInterval: 0.05f);
        Set(Sfx.GateOpen, "gate_open", 0.75f, minInterval: 0.08f);
        Set(Sfx.GateClose, "gate_close", 0.75f, minInterval: 0.08f);
        Set(Sfx.FallingStart, "falling_start", 0.80f, jitter: 0.04f, minInterval: 0.15f);
        Set(Sfx.FallingLand, "falling_land", 0.80f, jitter: 0.05f, minInterval: 0.06f);
        Set(Sfx.StickyGlue, "sticky_glue", 0.70f, jitter: 0.04f, minInterval: 0.20f);
        Set(Sfx.RefractorSpent, "refractor_spent", 0.75f, jitter: 0.03f, minInterval: 0.20f);

        Set(Sfx.BallRefract, "ball_refract", 0.85f, jitter: 0.04f, minInterval: 0.05f, maxVoices: 3);
        Set(Sfx.BallBlocked, "ball_blocked", 0.75f, jitter: 0.05f, minInterval: 0.12f);
        Set(Sfx.BallPassSticky, "ball_pass_sticky", 0.55f, minInterval: 0.30f, maxVoices: 1);
        Set(Sfx.BallStuck, "ball_stuck", 0.85f, jitter: 0.04f, minInterval: 0.10f);
        Set(Sfx.BallRest, "ball_rest", 0.65f, jitter: 0.06f, minInterval: 0.15f);

        Set(Sfx.BallSpeed1, "ball_speed_1", 0.55f, minInterval: 0.18f, maxVoices: 1);
        Set(Sfx.BallSpeed2, "ball_speed_2", 0.58f, minInterval: 0.18f, maxVoices: 1);
        Set(Sfx.BallSpeed3, "ball_speed_3", 0.62f, minInterval: 0.18f, maxVoices: 1);
        Set(Sfx.BallSpeed4, "ball_speed_4", 0.66f, minInterval: 0.18f, maxVoices: 1);
        Set(Sfx.BallSpeed5, "ball_speed_5", 0.70f, minInterval: 0.18f, maxVoices: 1);

        Set(Sfx.MirrorTurn, "mirror_turn", 0.80f, jitter: 0.03f, minInterval: 0.06f,
            fallback: "ball_refract", fallbackPitch: 0.82f);
        Set(Sfx.Bounce, "bounce", 0.80f, jitter: 0.05f, minInterval: 0.06f,
            fallback: "ball_blocked", fallbackPitch: 1.35f);
        Set(Sfx.PlatePress, "plate_press", 0.70f, jitter: 0.04f, minInterval: 0.12f,
            fallback: "switch_press", fallbackPitch: 1.10f);
        Set(Sfx.BallSwitchOn, "ball_switch_on", 0.70f, jitter: 0.03f, minInterval: 0.10f,
            fallback: "switch_press", fallbackPitch: 1.28f);
        Set(Sfx.BallSwitchOff, "ball_switch_off", 0.70f, jitter: 0.03f, minInterval: 0.10f,
            fallback: "switch_press", fallbackPitch: 0.86f);
        Set(Sfx.Pickup, "pickup", 0.85f, jitter: 0.02f, minInterval: 0.20f,
            fallback: "switch_press", fallbackVolume: 1.1f, fallbackPitch: 1.50f);

        Set(Sfx.LevelClear, "level_clear", 0.90f, jitter: 0f, minInterval: 1.0f, maxVoices: 1);
        Set(Sfx.LevelRestart, "level_restart", 0.80f, jitter: 0f, minInterval: 0.5f, maxVoices: 1);
    }

    // ------------------------------------------------------------------
    // one-shots
    // ------------------------------------------------------------------

    public static void Play(Sfx id, float volume = 1f, float pitch = 1f)
    {
        // Sound is decoration: if the mixer ever misbehaves it must not take gameplay down with
        // it. This used to be the difference between "the cat fired" and "nothing happened".
        try
        {
            PlayNow(id, volume, pitch);
        }
        catch (Exception e)
        {
            if (!loggedFailure)
            {
                loggedFailure = true;
                Debug.LogWarning("AudioManager.Play failed (sound skipped): " + e.Message);
            }
        }
    }

    static void PlayNow(Sfx id, float volume, float pitch)
    {
        EnsureBoot();
        int i = (int)id;
        Def d = defs[i];

        float now = Time.unscaledTime;
        if (now - lastPlay[i] < d.minInterval) return;

        AudioClip clip = Resolve(i, d, out float volMul, out float pitchMul);
        if (clip == null) return;
        if (CountPlaying(clip) >= d.maxVoices) return;

        lastPlay[i] = now;

        AudioSource src = NextVoice();
        if (src == null) return;   // nothing usable left in the pool

        src.Stop();
        src.clip = clip;
        src.volume = Mathf.Clamp01(d.volume * volMul * volume);
        src.pitch = Mathf.Clamp(
            d.pitch * pitchMul * pitch * (1f + UnityEngine.Random.Range(-d.jitter, d.jitter)),
            0.25f, 3f);
        src.Play();
    }

    /// <summary>
    /// The ball gained a momentum level. Called on every falling cell, so the throttle here
    /// is what stops a long drop from turning into a machine gun of whooshes.
    /// </summary>
    public static void PlayBallSpeed(int level)
    {
        try
        {
            EnsureBoot();
            level = Mathf.Clamp(level, 1, 5);
            int i = (int)Sfx.BallSpeed1 + (level - 1);
            if (Time.unscaledTime - lastPlay[i] < defs[i].minInterval) return;

            // Also gate against the neighbouring levels: 1 -> 2 should not stack.
            for (int k = 0; k < 5; k++)
            {
                if (Time.unscaledTime - lastPlay[(int)Sfx.BallSpeed1 + k] < 0.18f) return;
            }

            Play((Sfx)i);
        }
        catch (Exception e)
        {
            if (!loggedFailure)
            {
                loggedFailure = true;
                Debug.LogWarning("AudioManager.PlayBallSpeed failed (sound skipped): " + e.Message);
            }
        }
    }

    static AudioClip Resolve(int i, Def d, out float volMul, out float pitchMul)
    {
        volMul = 1f;
        pitchMul = 1f;

        if (!resolved[i])
        {
            AudioClip c = Resources.Load<AudioClip>(Root + d.path);
            if (c != null) clips[i] = c;
            else if (!string.IsNullOrEmpty(d.fallback))
            {
                c = Resources.Load<AudioClip>(Root + d.fallback);
                if (c != null)
                {
                    clips[i] = c;
                    usingFallback[i] = true;
                }
            }
            resolved[i] = true;
        }

        if (usingFallback[i])
        {
            volMul = d.fallbackVolume;
            pitchMul = d.fallbackPitch;
        }

        return clips[i];
    }

    static int CountPlaying(AudioClip clip)
    {
        if (voices == null) return 0;
        int n = 0;
        for (int i = 0; i < voices.Length; i++)
        {
            var v = voices[i];
            // A destroyed AudioSource compares equal to null, so dead voices are never touched.
            if (v != null && v.isPlaying && v.clip == clip) n++;
        }
        return n;
    }

    static AudioSource NextVoice()
    {
        if (voices == null || voices.Length == 0) return null;

        int n = voices.Length;

        // Free voice first, skipping any source that a previous scene (or play session) destroyed.
        for (int i = 0; i < n; i++)
        {
            int k = (cursor + i) % n;
            var v = voices[k];
            if (v == null) continue;
            if (!v.isPlaying)
            {
                cursor = (k + 1) % n;
                return v;
            }
        }

        // All busy: steal in round-robin order, again skipping dead entries.
        for (int i = 0; i < n; i++)
        {
            int k = (cursor + i) % n;
            var v = voices[k];
            if (v == null) continue;
            cursor = (k + 1) % n;
            return v;
        }

        return null;
    }

    static void EnsureBoot()
    {
        if (!booted || root == null) Boot();
    }

    // ------------------------------------------------------------------
    // music (no assets yet - calling these is harmless until the files exist)
    // ------------------------------------------------------------------

    /// <summary>Play a looping track from Resources/Audio/bgm/&lt;name&gt;. Same name = keeps playing.</summary>
    public static void PlayBgm(string name, float volume = 0.45f)
    {
        try
        {
            EnsureBoot();
            if (string.IsNullOrEmpty(name) || bgmName == name) return;

            var clip = Resources.Load<AudioClip>(Root + "bgm/" + name);
            bgmName = name;

            if (clip == null)
            {
                StopBgm();
                bgmName = name;   // remember the request so a later scene does not retry every frame
                return;
            }

            var src = EnsureBgmSource();
            if (src == null) return;

            src.Stop();
            src.clip = clip;
            src.volume = Mathf.Clamp01(volume);
            src.Play();
        }
        catch (Exception e)
        {
            if (!loggedFailure)
            {
                loggedFailure = true;
                Debug.LogWarning("AudioManager.PlayBgm failed (no music): " + e.Message);
            }
        }
    }

    public static void StopBgm()
    {
        EnsureBoot();
        bgmName = null;
        if (bgmSource != null)
        {
            bgmSource.Stop();
            bgmSource.clip = null;
        }
    }

    /// <summary>Fade-free duck used by the pause / level-complete overlay.</summary>
    public static void SetBgmVolume(float volume)
    {
        EnsureBoot();
        if (bgmSource != null) bgmSource.volume = Mathf.Clamp01(volume);
    }

    static AudioSource EnsureBgmSource()
    {
        if (bgmSource != null) return bgmSource;
        if (root == null) return null;

        bgmSource = root.AddComponent<AudioSource>();
        bgmSource.playOnAwake = false;
        bgmSource.loop = true;
        bgmSource.spatialBlend = 0f;
        bgmSource.volume = 0.45f;
        return bgmSource;
    }
}
