using UnityEngine;

/// <summary>
/// The crack/shatter animation of a destructible block.
///
/// When a ball breaks dirt / wood / stone the tile itself is destroyed the same frame the ball
/// steps into the cell, so the animation cannot live on the tile. Instead this spawns a short
/// lived sprite at the same spot that walks the material's frame sequence from
/// <see cref="BrickArt"/>, holds the last frame for a moment and fades out.
///
/// It has no collider: the ball flies through it, it is purely decoration.
/// </summary>
public class BrickBreakFx : MonoBehaviour
{
    /// <summary>Playback speed of the crack frames.</summary>
    public static float FramesPerSecond = 24f;

    /// <summary>How long the final (most broken) frame stays fully visible.</summary>
    public static float HoldTime = 0.07f;

    /// <summary>Fade-out time after the hold.</summary>
    public static float FadeTime = 0.12f;

    /// <summary>Between the tiles (1) and the ball (6) so the ball stays readable.</summary>
    public static int SortingOrder = 5;

    Sprite[] frames;
    SpriteRenderer sr;
    float age;
    float frameRate;
    int firstFrame;
    float holdTime;
    float fadeTime;

    /// <summary>
    /// Spawn the shatter animation. When <paramref name="duration"/> is greater than zero the
    /// whole animation (last frame plus fade) finishes exactly after that many seconds, which is
    /// how the effect is kept in sync with the ball flying through the cell it just broke.
    /// </summary>
    public static void Play(TileType type, Vector3 position, float duration = 0f)
    {
        if (!Application.isPlaying) return;

        // The artist's real GIF frames (200 ms each) win. Without the art drop we
        // keep the fork's crack sequence at its own frame rate, starting at frame 1
        // because frame 0 was already on screen as the intact block.
        var frames = BlockArt.CrateAnimation(type);
        float fps = 1f / BlockArt.GifFrameDuration;
        int first = 0;

        if (frames == null || frames.Length < 2)
        {
            frames = BrickArt.Frames(type);
            fps = FramesPerSecond;
            first = 1;
        }
        if (frames.Length < 2) return;

        var go = new GameObject("BrickBreak");
        go.transform.position = position;

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = SortingOrder;
        renderer.color = Color.white;

        var fx = go.AddComponent<BrickBreakFx>();
        fx.frames = frames;
        fx.sr = renderer;
        fx.firstFrame = first;
        renderer.sprite = frames[Mathf.Min(first, frames.Length - 1)];

        int shown = Mathf.Max(1, frames.Length - first);
        if (duration > 0f)
        {
            // Fit every remaining frame plus the fade into the given time.
            float fade = shown > 1 ? Mathf.Min(0.25f * duration, FadeTime) : duration;
            fx.holdTime = 0f;
            fx.fadeTime = fade;
            fx.frameRate = shown > 1
                ? (shown - 1) / Mathf.Max(0.0001f, duration - fade)
                : 1f;
        }
        else
        {
            fx.holdTime = HoldTime;
            fx.fadeTime = FadeTime;
            fx.frameRate = fps;
        }
    }

    void Update()
    {
        if (sr == null || frames == null || frames.Length == 0)
        {
            Destroy(gameObject);
            return;
        }

        age += Time.deltaTime;

        float step = 1f / Mathf.Max(1f, frameRate);
        int index = firstFrame + Mathf.FloorToInt(age / step);

        if (index >= frames.Length)
        {
            index = frames.Length - 1;

            // the last frame starts after (length - 1 - firstFrame) steps, then hold, then fade
            float sinceLast = age - (frames.Length - 1 - firstFrame) * step;
            if (sinceLast > holdTime)
            {
                float a = 1f - Mathf.Clamp01((sinceLast - holdTime) / Mathf.Max(0.01f, fadeTime));
                var c = sr.color;
                c.a = a;
                sr.color = c;
                if (a <= 0f)
                {
                    Destroy(gameObject);
                    return;
                }
            }
        }

        sr.sprite = frames[index];
    }
}
