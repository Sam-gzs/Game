using UnityEngine;

/// <summary>
/// The little cat that wanders around the player when no ball exists.
/// On throw it runs to the player's front, curls into a ball (morph frames),
/// then the real BoxProjectile is spawned and the cat disappears until the
/// ball is gone again. Purely visual: no colliders and no physics.
/// </summary>
public class CatCompanion : MonoBehaviour
{
    public PlayerController player;

    [Tooltip("How tall the cat should be, in world units.")]
    public float catHeight = 1.0f;
    public float wanderRadius = 1.3f;
    public float moveSpeed = 1.5f;
    public int sortingOrder = 7;

    Sprite[] walk;

    Transform catRoot;
    SpriteRenderer sr;

    Vector3 wanderTarget;
    float wanderTimer;
    float frameTimer;
    int frame;

    bool visible = true;

    public bool IsBusy => false;
    public bool IsVisible => visible;

    /// <summary>The cat belonging to the player of the current level, if any.</summary>
    public static CatCompanion Current { get; private set; }

    /// <summary>True while the cat is still waiting out of sight, not yet let into the level.</summary>
    public bool IsDormant => dormant;

    bool dormant;

    /// <summary>Seconds left of the "sprint after the player" burst right after being let in.</summary>
    float catchUp;

    void Awake()
    {
        Current = this;
        if (player == null) player = GetComponentInParent<PlayerController>();

        // The following cat uses the grey-white cat from the art drop; the fork's
        // CatWalk sequence remains the fallback.
        walk = CatArt.Walk(false) ?? LoadSequence("CatWalk", 12);

        var rootGo = new GameObject("CatRoot");
        rootGo.transform.SetParent(transform, false);
        catRoot = rootGo.transform;

        var visual = new GameObject("CatSprite");
        visual.transform.SetParent(catRoot, false);
        sr = visual.AddComponent<SpriteRenderer>();
        sr.sortingOrder = sortingOrder;

        if (walk.Length > 0)
        {
            sr.sprite = walk[0];
            float h = walk[0].rect.height / Mathf.Max(1f, walk[0].pixelsPerUnit);
            float s = catHeight / Mathf.Max(0.001f, h);
            visual.transform.localScale = new Vector3(s, s, 1f);
        }

        if (player != null)
        {
            float feet = player.transform.position.y - 0.95f;
            catRoot.position = new Vector3(player.transform.position.x, feet, 0f);
            wanderTarget = catRoot.position;
        }
        wanderTimer = 0.4f;
    }

    static Sprite[] LoadSequence(string prefix, int max)
    {
        var list = new System.Collections.Generic.List<Sprite>();
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
        if (player == null) return;

        // Still waiting behind the wall: no wandering, nothing to see.
        if (dormant)
        {
            SetVisible(false);
            return;
        }

        float dt = Time.deltaTime;

        if (catchUp > 0f) catchUp = Mathf.Max(0f, catchUp - dt);

        if (BoxProjectile.Count > 0)
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);
        if (walk.Length == 0) return;

        Vector3 feet = new Vector3(player.transform.position.x, player.transform.position.y - 0.95f, 0f);

        wanderTimer -= dt;
        if (wanderTimer <= 0f)
        {
            wanderTimer = Random.Range(1.1f, 2.6f);
            wanderTarget = new Vector3(feet.x + Random.Range(-wanderRadius, wanderRadius), feet.y, 0f);
        }

        // If the player walks away, the cat gives up its old spot and catches up.
        if (Mathf.Abs(catRoot.position.x - feet.x) > wanderRadius * 1.7f)
            wanderTarget = new Vector3(feet.x + Random.Range(-0.45f, 0.45f), feet.y, 0f);

        float dx = wanderTarget.x - catRoot.position.x;
        if (Mathf.Abs(dx) > 0.03f)
        {
            // Right after being let in it sprints, so it visibly runs out of the gap to the player.
            float speed = catchUp > 0f ? moveSpeed * 2.5f : moveSpeed;
            float nx = Mathf.MoveTowards(catRoot.position.x, wanderTarget.x, speed * dt);
            catRoot.position = new Vector3(nx, feet.y, 0f);

            sr.flipX = dx > 0f;
            frameTimer += dt;
            if (frameTimer >= 0.2f)
            {
                frameTimer = 0f;
                frame = (frame + 1) % walk.Length;
            }
            sr.sprite = walk[frame];
            sr.transform.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(Time.time * 14f)) * 0.015f, 0f);
        }
        else
        {
            sr.sprite = walk[0];
            sr.transform.localPosition = Vector3.zero;
        }

        catRoot.position = new Vector3(catRoot.position.x, feet.y, 0f);
    }

    /// <summary>
    /// The cat snaps to the player's hand and the ball leaves on the same frame.
    /// The old dash + morph wind-up is intentionally gone.
    /// </summary>
    public void BeginThrow(Vector2Int targetCell, Vector2Int direction, int power)
    {
        // A cat that has not been let into the level yet must not pop up at the player's hand.
        if (!dormant && player != null)
        {
            float feet = player.transform.position.y - 0.95f;
            catRoot.position = new Vector3(player.transform.position.x + player.Facing * 0.7f, feet, 0f);
        }

        SetVisible(false);
        BoxProjectile.Spawn(targetCell, direction, null, null, power);
    }

    /// <summary>
    /// Parks the cat out of sight until the level lets it in (a CatTrigger does that).
    /// Used for the intro where it runs out of a gap in the wall.
    /// </summary>
    public void WaitAt(Vector3 worldPos)
    {
        dormant = true;
        if (catRoot != null) catRoot.position = new Vector3(worldPos.x, worldPos.y, 0f);
        wanderTarget = new Vector3(worldPos.x, worldPos.y, 0f);
        catchUp = 0f;
        SetVisible(false);
    }

    /// <summary>Let the cat in: it steps out where it was waiting and runs after the player.</summary>
    public static void Summon()
    {
        var cat = Current;
        if (cat == null || !cat.dormant) return;

        cat.dormant = false;
        cat.catchUp = 1.6f;
        cat.wanderTimer = 0f;
        cat.SetVisible(true);
    }

    void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    void SetVisible(bool v)
    {
        visible = v;
        if (sr != null) sr.enabled = v;
    }
}
