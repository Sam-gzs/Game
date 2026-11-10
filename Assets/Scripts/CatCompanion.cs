using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The cats that wander around the player when no ball exists.
///
/// One cat is owned from the start; every extra-box pickup adds another. Every owned cat
/// that has NOT been turned into a ball follows the player, so after the second pickup the
/// grey cat and the orange cat are both on screen. Throwing takes the next cat in line
/// (index == balls already in the world) and turns it into that ball; when the ball is gone
/// the cat comes back. Purely visual: no colliders and no physics.
/// </summary>
public class CatCompanion : MonoBehaviour
{
    public PlayerController player;

    [Tooltip("How tall the cat should be, in world units.")]
    public float catHeight = 1.0f;
    public float wanderRadius = 1.3f;
    public float moveSpeed = 1.5f;
    public int sortingOrder = 7;

    class CatUnit
    {
        public Transform root;
        public SpriteRenderer sr;
        public Sprite[] walk;
        public Sprite idle;
        public Vector3 wanderTarget;
        public float wanderTimer;
        public float frameTimer;
        public int frame;
    }

    readonly List<CatUnit> units = new List<CatUnit>();

    bool visible = true;

    public bool IsBusy => false;
    public bool IsVisible => visible;

    /// <summary>The cat group belonging to the player of the current level, if any.</summary>
    public static CatCompanion Current { get; private set; }

    /// <summary>True while the cats are still waiting out of sight, not yet let into the level.</summary>
    public bool IsDormant => dormant;

    bool dormant;

    /// <summary>Seconds left of the "sprint after the player" burst right after being let in.</summary>
    float catchUp;

    void Awake()
    {
        Current = this;
        if (player == null) player = GetComponentInParent<PlayerController>();

        int count = player != null ? Mathf.Clamp(player.MaxBoxCapacity, 1, 4) : 2;
        for (int i = 0; i < count; i++) units.Add(BuildCat(i));
    }

    CatUnit BuildCat(int index)
    {
        bool orange = index > 0;   // first cat is the grey-white one, every extra is orange
        var unit = new CatUnit();

        // Art drop first (tight-cropped, same framing as the fork sprites); fork fallback.
        var walk = CatArt.Walk(orange);
        if (walk == null || walk.Length == 0) walk = LoadSequence("CatWalk", 12);
        unit.walk = walk;
        unit.idle = CatArt.Idle(orange);

        var rootGo = new GameObject("CatRoot" + (index + 1));
        rootGo.transform.SetParent(transform, false);
        unit.root = rootGo.transform;

        var visual = new GameObject("CatSprite");
        visual.transform.SetParent(unit.root, false);
        unit.sr = visual.AddComponent<SpriteRenderer>();
        unit.sr.sortingOrder = sortingOrder + index;
        unit.sr.enabled = false;

        Sprite first = (walk != null && walk.Length > 0) ? walk[0] : unit.idle;
        if (first != null)
        {
            unit.sr.sprite = first;
            float h = first.rect.height / Mathf.Max(1f, first.pixelsPerUnit);
            float s = catHeight / Mathf.Max(0.001f, h);
            visual.transform.localScale = new Vector3(s, s, 1f);
        }

        if (player != null)
        {
            // Pivot is centred, so lift the root by half a cat to stand it ON the feet line.
            float feet = player.transform.position.y - 0.95f + catHeight * 0.5f;
            unit.root.position = new Vector3(player.transform.position.x + OffsetX(index), feet, 0f);
        }
        unit.wanderTarget = unit.root.position;
        unit.wanderTimer = 0.35f + index * 0.4f;
        return unit;
    }

    /// <summary>Keeps the group spread out instead of stacking on one spot.</summary>
    static float OffsetX(int index)
    {
        if (index == 0) return -0.55f;
        if (index == 1) return 0.55f;
        return (index % 2 == 0 ? -1f : 1f) * (0.55f + 0.5f * ((index + 1) / 2));
    }

    static Sprite[] LoadSequence(string prefix, int max)
    {
        var list = new List<Sprite>();
        for (int i = 0; i < max; i++)
        {
            var s = Resources.Load<Sprite>(prefix + "_" + i);
            if (s == null) break;
            list.Add(s);
        }
        return list.ToArray();
    }

    void Update()
    {
        if (player == null || units.Count == 0) return;

        // Still waiting behind the wall: no wandering, nothing to see.
        if (dormant)
        {
            HideAll();
            return;
        }

        float dt = Time.deltaTime;
        if (catchUp > 0f) catchUp = Mathf.Max(0f, catchUp - dt);

        // Cats 0..capacity-1 are owned. The first Count of them are out as balls, so the
        // ones that stay on the ground and follow are exactly Count..capacity-1.
        int balls = BoxProjectile.Count;
        int capacity = Mathf.Clamp(player.boxCapacity, 0, units.Count);

        var feet = new Vector3(player.transform.position.x,
            player.transform.position.y - 0.95f + catHeight * 0.5f, 0f);

        for (int i = 0; i < units.Count; i++)
        {
            bool show = visible && i >= balls && i < capacity;
            var unit = units[i];
            if (unit.sr != null && unit.sr.enabled != show) unit.sr.enabled = show;
            if (!show) continue;
            Follow(unit, i, feet, dt);
        }
    }

    void Follow(CatUnit unit, int index, Vector3 feet, float dt)
    {
        if (unit.walk == null || unit.walk.Length == 0) return;

        unit.wanderTimer -= dt;
        if (unit.wanderTimer <= 0f)
        {
            unit.wanderTimer = Random.Range(1.1f, 2.6f);
            float cx = feet.x + OffsetX(index);
            unit.wanderTarget = new Vector3(cx + Random.Range(-wanderRadius, wanderRadius), feet.y, 0f);
        }

        // If the player walks away, the cat gives up its old spot and catches up.
        if (Mathf.Abs(unit.root.position.x - feet.x) > wanderRadius * 1.7f)
            unit.wanderTarget = new Vector3(feet.x + OffsetX(index), feet.y, 0f);

        float dx = unit.wanderTarget.x - unit.root.position.x;
        if (Mathf.Abs(dx) > 0.03f)
        {
            // Right after being let in it sprints, so it visibly runs out of the gap to the player.
            float speed = catchUp > 0f ? moveSpeed * 2.5f : moveSpeed;
            float nx = Mathf.MoveTowards(unit.root.position.x, unit.wanderTarget.x, speed * dt);
            unit.root.position = new Vector3(nx, feet.y, 0f);

            unit.sr.flipX = dx > 0f;
            unit.frameTimer += dt;
            if (unit.frameTimer >= 0.2f)
            {
                unit.frameTimer = 0f;
                unit.frame = (unit.frame + 1) % unit.walk.Length;
            }
            unit.sr.sprite = unit.walk[unit.frame];
            unit.sr.transform.localPosition =
                new Vector3(0f, Mathf.Abs(Mathf.Sin(Time.time * 14f + index)) * 0.015f, 0f);
        }
        else
        {
            // Standing still: show the dedicated idle pose instead of frame 0 of the walk.
            var still = unit.idle != null ? unit.idle : unit.walk[0];
            if (unit.sr.sprite != still) unit.sr.sprite = still;
            unit.sr.transform.localPosition = Vector3.zero;
        }

        unit.root.position = new Vector3(unit.root.position.x, feet.y, 0f);
    }

    void HideAll()
    {
        for (int i = 0; i < units.Count; i++)
            if (units[i].sr != null) units[i].sr.enabled = false;
    }

    /// <summary>
    /// The next cat in line snaps to the player's hand and the ball leaves on the same frame.
    /// </summary>
    public void BeginThrow(Vector2Int targetCell, Vector2Int direction, int power)
    {
        if (!dormant && player != null && units.Count > 0)
        {
            int index = Mathf.Clamp(BoxProjectile.Count, 0, units.Count - 1);
            var unit = units[index];
            float feet = player.transform.position.y - 0.95f + catHeight * 0.5f;
            unit.root.position = new Vector3(player.transform.position.x + player.Facing * 0.7f, feet, 0f);
            unit.sr.transform.localPosition = Vector3.zero;
            unit.sr.enabled = false;
        }

        BoxProjectile.Spawn(targetCell, direction, null, null, power);
    }

    /// <summary>
    /// Parks the cats out of sight until the level lets them in (a CatTrigger does that).
    /// </summary>
    public void WaitAt(Vector3 worldPos)
    {
        dormant = true;
        catchUp = 0f;
        for (int i = 0; i < units.Count; i++)
        {
            var unit = units[i];
            unit.root.position = new Vector3(worldPos.x + i * 0.35f, worldPos.y, 0f);
            unit.wanderTarget = unit.root.position;
        }
        HideAll();
    }

    /// <summary>Let the cats in: they step out where they were waiting and run after the player.</summary>
    public static void Summon()
    {
        var cat = Current;
        if (cat == null || !cat.dormant) return;

        cat.dormant = false;
        cat.catchUp = 1.6f;
        for (int i = 0; i < cat.units.Count; i++) cat.units[i].wanderTimer = 0f;
    }

    void OnDestroy()
    {
        if (Current == this) Current = null;
    }
}
