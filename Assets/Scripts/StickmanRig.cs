using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Character sprite presentation. Idle uses the front image; walking plays the
/// frames imported from the walk video; jumping reuses walk poses as a small
/// two-pose jump. Push animation is intentionally not implemented yet.
/// </summary>
public class StickmanRig : MonoBehaviour
{
    public PlayerController player;

    [Header("Sprites (leave empty to load Resources)")]
    public Sprite idleSprite;
    public Sprite[] walkSprites;

    [Header("Layout")]
    public float visualHeight = 1.9f;
    public float feetY = -0.95f;
    public int sortingOrder = 5;

    [Header("Animation")]
    public float walkFps = 8f;

    // Kept so the old editor-level StickmanGameBuilder still compiles. The sprite
    // presentation ignores these line-rig proportions.
    [Header("Legacy builder fields (unused)")]
    public Color bodyColor = new Color(0.92f, 0.94f, 0.98f, 1f);
    public float lineWidth = 0.068f;
    public float hipY = -0.22f;
    public float shoulderY = 0.34f;
    public float headY = 0.66f;
    public float headRadius = 0.19f;

    public Color flashColor = new Color(1f, 0.94f, 0.68f, 1f);

    Transform root;
    Transform spriteT;
    SpriteRenderer sr;
    Sprite idle;
    Sprite[] walk = new Sprite[0];
    float idleScale;
    float walkScale;
    float walkTimer;
    float breathe;
    float flash;

    void Awake()
    {
        if (player == null) player = GetComponentInParent<PlayerController>();

        idle = idleSprite != null ? idleSprite : LoadSprite("PlayerIdle");
        if (walkSprites != null && walkSprites.Length > 0)
            walk = walkSprites;
        else
            walk = LoadWalkFrames();

        if (idle == null && walk.Length > 0) idle = walk[0];
        if ((walk == null || walk.Length == 0) && idle != null) walk = new[] { idle };

        BuildVisual();
        if (player != null) player.visual = root;
    }

    static Sprite LoadSprite(string name)
    {
        var s = Resources.Load<Sprite>(name);
        if (s != null) return s;
        var all = Resources.LoadAll<Sprite>(name);
        return all != null && all.Length > 0 ? all[0] : null;
    }

    static Sprite[] LoadWalkFrames()
    {
        var list = new List<Sprite>();
        for (int i = 0; i < 32; i++)
        {
            var s = Resources.Load<Sprite>("Walk_" + i);
            if (s == null) break;
            list.Add(s);
        }
        return list.ToArray();
    }

    void BuildVisual()
    {
        var rootGo = new GameObject("CharacterVisual");
        rootGo.transform.SetParent(transform, false);
        root = rootGo.transform;

        if (idle == null || walk.Length == 0)
        {
            // Last-resort fallback so the player is never invisible.
            sr = rootGo.AddComponent<SpriteRenderer>();
            sr.sprite = Resources.Load<Sprite>("Ball");
            sr.color = new Color(0.95f, 0.8f, 0.9f, 1f);
            sr.sortingOrder = sortingOrder;
            spriteT = root;
            idleScale = walkScale = 1f;
            return;
        }

        var go = new GameObject("CharacterSprite");
        go.transform.SetParent(root, false);
        spriteT = go.transform;
        sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = idle;
        sr.sortingOrder = sortingOrder;
        sr.color = Color.white;

        idleScale = visualHeight / Mathf.Max(0.001f, idle.rect.height / Mathf.Max(1f, idle.pixelsPerUnit));
        walkScale = visualHeight / Mathf.Max(0.001f, walk[0].rect.height / Mathf.Max(1f, walk[0].pixelsPerUnit));
        spriteT.localScale = new Vector3(idleScale, idleScale, 1f);
        spriteT.localPosition = new Vector3(0f, feetY, 0f);
    }

    void LateUpdate()
    {
        if (player == null || root == null || sr == null || idle == null || walk.Length == 0)
        {
            ApplyFlash();
            return;
        }

        float dt = Time.deltaTime;
        float speed01 = Mathf.Clamp01(Mathf.Abs(player.SpeedX) / Mathf.Max(0.01f, player.moveSpeed));
        // Use held input as well as actual velocity. Pushing into a wall zeroes SpeedX
        // but the character should keep walking instead of flickering to idle.
        bool wantsMove = Mathf.Abs(player.MoveInput) > 0.1f;
        bool moving = wantsMove || speed01 > 0.15f;
        float animSpeed = Mathf.Max(speed01, wantsMove ? 0.3f : 0f);
        bool grounded = player.Grounded;
        bool sidePose = moving || !grounded;

        Sprite target;
        float scale;
        float bob = 0f;
        float lean = 0f;
        float stretch = 1f;

        if (!grounded)
        {
            // Jump: a walk pose reused, with a very small stretch/lean.
            int up = Mathf.Clamp(1, 0, walk.Length - 1);
            int down = Mathf.Clamp(4, 0, walk.Length - 1);
            target = walk[player.SpeedY > 0.1f ? up : down];
            scale = walkScale;
            lean = Mathf.Clamp(player.SpeedY / 12f, -1f, 1f) * 2.5f;
            stretch = 1f + Mathf.Clamp(player.SpeedY / 120f, -0.012f, 0.012f);
        }
        else if (moving)
        {
            walkTimer += dt * Mathf.Lerp(6.5f, walkFps, animSpeed);
            int frame = Mathf.FloorToInt(walkTimer) % walk.Length;
            target = walk[frame];
            scale = walkScale;
            bob = Mathf.Abs(Mathf.Sin(walkTimer * Mathf.PI)) * 0.006f;
            lean = Mathf.Sin(walkTimer * Mathf.PI * 2f) * 0.8f;
        }
        else
        {
            breathe += dt;
            target = idle;
            scale = idleScale;
            float s = Mathf.Sin(breathe * 2f);
            bob = s * 0.008f;
            stretch = 1f + s * 0.006f;
        }

        if (sr.sprite != target) sr.sprite = target;

        float xs = sidePose ? -scale : scale;
        spriteT.localScale = new Vector3(xs, scale * stretch, 1f);
        spriteT.localPosition = new Vector3(0f, feetY + bob, 0f);
        spriteT.localRotation = Quaternion.Euler(0f, 0f, lean);

        ApplyFlash();
    }

    /// <summary>Briefly warm-tint the character (kept for compatibility).</summary>
    public void Flash()
    {
        flash = 1f;
    }

    void ApplyFlash()
    {
        if (sr == null) return;
        if (flash <= 0f)
        {
            sr.color = Color.white;
            return;
        }

        flash = Mathf.Max(0f, flash - Time.deltaTime * 4f);
        sr.color = Color.Lerp(Color.white, flashColor, flash);
    }
}
