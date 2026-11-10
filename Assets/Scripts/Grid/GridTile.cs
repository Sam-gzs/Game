using UnityEngine;

public enum TileType
{
    Solid,
    Stone,
    Bounce,
    Spawn,
    Goal,
    Dirt,
    Door,
    Falling,
    RefractorDown,   // "\"  a box going right comes out going down
    RefractorUp,     // "/"  a box going right comes out going up
    Switch,          // 1x1 button: a box passes through it and presses it
    Gate,            // 2 cells tall: solid until its button is pressed
    Pickup,          // 1x1 pickup: walking into it grants one more box
    Mirror,          // motorized mirror: channel drives "\" <-> "/"
    Plate,           // 1x1 pressure plate: pressed while a body is on it
    Wood,            // breakable by a ball with speed >= 2
    Refract,         // ball enters the cell and leaves turned 90 degrees clockwise
    Sticky,          // ball sticks to this block and does not fall
    Jelly,           // passable by player and ball; slows the player, costs the ball 1 momentum
    Slider,          // solid to bodies, but the sticky slime cannot stick to / rest on it
    BallSwitch,      // channel switch that is ON only while a ball is inside its cell
    TriggerPlayer,   // invisible in play; fires a tutorial text when the player enters
    TriggerBall,     // invisible in play; fires a tutorial text when a ball is in the cell
    TextAnchor,      // invisible in play; marker that owns/displays the tutorial text
    CatTrigger,      // colourless trigger: touching it lets the cat in
    CatExit          // colourless marker: where the cat waits before it is let in
}

/// <summary>
/// Which corner the SOLID blue-grey triangle occupies.  That triangle is the BODY of
/// the refractor: the ball must always arrive on the empty side and never shoot into
/// the solid wedge.  The four positions give these trajectories (enter -> leave):
///   SolidTopLeft      "/"    up    -> right,  left -> down
///   SolidBottomLeft   "\"    left  -> up,     down -> right
///   SolidTopRight     "\"    right -> down,   up   -> left
///   SolidBottomRight  "/"    right -> up,     down -> left
/// Auto keeps the fork's original two-diagonal art (BlockRefractUp / Down).
/// </summary>
public enum RefractFacing
{
    Auto = 0,
    SolidTopLeft,
    SolidBottomLeft,
    SolidTopRight,
    SolidBottomRight
}

/// <summary>Who is allowed to set a CatTrigger off.</summary>
public enum CatTriggerActor
{
    Player,
    Cat,
    Both
}

/// <summary>
/// Which side of a spring (TileType.Bounce) faces the ball. The spring only gives along this
/// axis: a ball arriving against the face is thrown straight back, anything else hits a wall.
/// </summary>
public enum SpringFacing
{
    Up,
    Down,
    Left,
    Right
}

/// <summary>
/// One map cell. Drop the GridTile prefab into the scene and change its type,
/// or use Tools > Grid Painter to paint cells.
/// </summary>
[ExecuteAlways]
public class GridTile : MonoBehaviour
{
    public TileType type = TileType.Solid;
    public Vector2Int cell;
    public Sprite baseSprite;
    [SerializeField, HideInInspector] TileType lastType = TileType.Solid;

    [Tooltip("Switches, gates, mirrors and plates are bound by this number: same channel = same circuit.")]
    public int channel = 1;

    [Tooltip("Gate: open while the channel is off (inverting the gate itself).\n" +
             "Switch / BallSwitch: invert what the switch means - unlit (red/brown) counts as " +
             "pressed, so its gate stays open and pressing it closes the gate.")]
    public bool invert;

    [Tooltip("Mirror: true = '\\', false = '/'. A channel signal sets it to '/' when on.")]
    public bool mirrorDown = true;

    [Header("Refractor art")]
    [Tooltip("Auto = the fork's two-diagonal art. An explicit value uses the matching " +
             "four-way wedge sprite (Resources/Blocks/mirror_*).")]
    public RefractFacing refractFacing = RefractFacing.Auto;

    [Header("Spring (TileType.Bounce)")]
    [Tooltip("Which side of the spring faces the ball. The spring only gives along this axis: " +
             "a ball arriving against the face is thrown straight back, every other side is solid.")]
    public SpringFacing springFacing = SpringFacing.Right;

    /// <summary>Seconds left of the squashed spring frame after a ball bounced off it.</summary>
    float springHitTimer;
    const float SpringHitTime = 0.10f;

    /// <summary>Temporary: log why a sticky starts falling / glues / is released. Off by default.</summary>
    public const bool TraceSticky = false;

    [Header("Cat trigger (TileType.CatTrigger)")]
    [Tooltip("Who can set this trigger off. The cat is allowed too, for levels where it has to reach one.")]
    public CatTriggerActor catTriggerActor = CatTriggerActor.Player;

    bool catTriggered;

    [Header("Tutorial text (used by TextAnchor)")]
    [TextArea] public string tutorialText = "TEXT";
    public int tutorialFontSize = 28;
    public Font tutorialFont;
    public float tutorialDuration = 3f;
    public Color tutorialColor = Color.white;
    [Tooltip("World-space offset from this tile's centre.")]
    public Vector2 tutorialOffset;

    float tutorialTimer;
    GUIStyle tutorialStyle;
    bool ballTriggerInside;

    public bool SwitchOn { get; private set; }
    public bool GateOpen { get; private set; }
    public bool PlatePressed { get; private set; }
    float nextSwitchTime;

    public static readonly Color SolidColor = new Color(0.20f, 0.23f, 0.31f, 1f);
    public static readonly Color SolidEdge = new Color(0.36f, 0.43f, 0.58f, 1f);
    public static readonly Color StoneColor = new Color(0.55f, 0.37f, 0.31f, 1f);
    public static readonly Color BounceColor = new Color(0.11f, 0.80f, 0.74f, 1f);
    public static readonly Color GoalColor = new Color(1f, 0.84f, 0.32f, 1f);
    public static readonly Color WoodColor = new Color(0.78f, 0.52f, 0.24f, 1f);
    public static readonly Color DirtColor = new Color(0.52f, 0.36f, 0.22f, 1f);
    public static readonly Color RefractColor = new Color(0.45f, 0.85f, 1.00f, 1f);
    public static readonly Color StickyColor = new Color(0.60f, 0.90f, 0.40f, 1f);
    public static readonly Color DoorColor = new Color(0.95f, 0.78f, 0.35f, 1f);
    public static readonly Color FallingColor = new Color(0.78f, 0.66f, 0.42f, 1f);
    public static readonly Color SpawnColor = new Color(0.85f, 0.28f, 0.95f, 0.85f);

    /// <summary>The only kind of block that falls; every other kind stays where it is.</summary>
    public float fallSpeed = 9f;

    SpriteRenderer sr;
    BoxCollider2D col;
    Transform pipsRoot;
    SpriteRenderer[] pips;

    bool falling;
    float fallT;
    Vector3 fallFrom;
    Vector3 fallTo;
    bool justLanded;

    // Sticky block: floats when free, can be pushed/carried by the ball.
    public BoxProjectile followBall;
    public GridTile followFalling;
    bool stickyFalling;
    float stickyFallT;
    Vector3 stickyFrom;
    Vector3 stickyTo;
    // Starts true: a sticky that is already glued when the level loads must not click.
    bool gluedToWall = true;

    public bool IsFalling => falling;

    void OnEnable()
    {
        // Same one-time safety as OnValidate, but for an already-saved scene:
        // an old BallSwitch instance with no lastType starts unwired.
        // Only the default channel (1) is treated as "unwired": a channel of 2/3/...
        // is an explicit designer choice and must survive, otherwise the gate it
        // points at can never be opened.
        if (type == TileType.BallSwitch && lastType != TileType.BallSwitch && channel <= 1)
        {
            channel = 0;
            lastType = type;
        }
        Apply();
    }

    void OnValidate()
    {
        // A newly created ball-hold switch must not silently join channel 1 and
        // open/close some existing gate. Start unwired (0); the designer assigns it.
        // An explicitly wired channel (>= 2) is kept as-is.
        if (type == TileType.BallSwitch && lastType != TileType.BallSwitch && channel <= 1) channel = 0;
        lastType = type;

        Apply();
#if UNITY_EDITOR
        if (!Application.isPlaying) EnforceGrid();
#endif
    }

    void Update()
    {
        // no per-frame work in edit mode: snapping happens when the tile is selected
        // (GridSnapKeeper) or when the inspector changes (OnValidate)
        if (!Application.isPlaying) return;

        if (type == TileType.Falling) FallTick();
        else if (type == TileType.Sticky) StickyTick();
        else if (type == TileType.TriggerBall) TriggerBallTick();
        else if (type == TileType.Bounce) SpringTick();
        else if (type == TileType.CatTrigger) CatTriggerTick();
        else if (type == TileType.TextAnchor && tutorialTimer > 0f)
            tutorialTimer = Mathf.Max(0f, tutorialTimer - Time.deltaTime);
    }

    /// <summary>
    /// Sensors are settled only after every moving thing has finished its step, so a switch can
    /// never bend the movement that is being resolved in the same frame: during movement the
    /// switch simply does not exist, and once everything has landed the channel is evaluated.
    /// </summary>
    void LateUpdate()
    {
        if (!Application.isPlaying) return;

        if (type == TileType.BallSwitch) BallSwitchTick();
        else if (type == TileType.Plate) PlateTick();
    }

    /// <summary>Fires once when a ball's logical cell enters this tile.</summary>
    void TriggerBallTick()
    {
        bool inside = BoxProjectile.IsBoxAt(cell);
        if (inside && !ballTriggerInside) FireTutorial(channel);
        ballTriggerInside = inside;
    }

    /// <summary>Show the TextAnchor text(s) on the same channel.</summary>
    void FireTutorial(int ch)
    {
        var tiles = Object.FindObjectsByType<GridTile>(FindObjectsSortMode.None);
        for (int i = 0; i < tiles.Length; i++)
        {
            var t = tiles[i];
            if (t == null || t.type != TileType.TextAnchor || t.channel != ch) continue;
            t.ShowTutorial();
        }
    }

    public void ShowTutorial()
    {
        tutorialTimer = Mathf.Max(0.1f, tutorialDuration);
    }

    // ---- cat trigger / cat exit ------------------------------------------

    /// <summary>
    /// The cat has no collider, so "the cat walked over it" is a cell comparison instead of a
    /// physics callback. The player side is handled by the trigger collider in OnTriggerEnter2D.
    /// </summary>
    void CatTriggerTick()
    {
        if (catTriggered) return;
        if (catTriggerActor != CatTriggerActor.Cat && catTriggerActor != CatTriggerActor.Both) return;

        var cat = CatCompanion.Current;
        if (cat == null || cat.IsDormant) return;
        if (GridUtil.WorldToCell(cat.transform.position) != cell) return;

        TriggerCat();
    }

    void TriggerCat()
    {
        if (catTriggered) return;
        catTriggered = true;
        CatCompanion.Summon();
    }

    /// <summary>The level's "the cat is waiting here" marker, if it has one.</summary>
    public static GridTile FindCatExit()
    {
        var tiles = Object.FindObjectsByType<GridTile>(FindObjectsSortMode.None);
        for (int i = 0; i < tiles.Length; i++)
        {
            if (tiles[i] != null && tiles[i].type == TileType.CatExit) return tiles[i];
        }
        return null;
    }

    /// <summary>Falling blocks drop cell by cell until something is under them.</summary>
    void FallTick()
    {
        if (falling)
        {
            fallT += Time.deltaTime * fallSpeed;
            transform.position = Vector3.Lerp(fallFrom, fallTo, Mathf.Clamp01(fallT));
            if (fallT >= 1f)
            {
                falling = false;
                fallT = 0f;
                transform.position = fallTo;
                justLanded = true;   // it may still keep falling: resolved on the next tick
            }
            return;
        }

        var below = new Vector2Int(cell.x, cell.y - 1);

        // A cell can hold a button/plate plus the block that dropped into it, so inspect
        // every tile in the cell. A gate occupies two cells and open gates are passable.
        GridTile button = null;
        bool supported = false;
        var tiles = Object.FindObjectsByType<GridTile>(FindObjectsSortMode.None);
        for (int i = 0; i < tiles.Length; i++)
        {
            var t = tiles[i];
            if (t == null || t == this || !t.OccupiesCell(below)) continue;

            // buttons/plates are not a floor: the block drops through and presses them
            // jelly is soft: everything passes through it
            if (t.type == TileType.Switch || t.type == TileType.Plate ||
                t.type == TileType.Jelly || t.type == TileType.BallSwitch ||
                t.type == TileType.TriggerPlayer || t.type == TileType.TriggerBall ||
                t.type == TileType.TextAnchor || t.type == TileType.CatTrigger ||
                t.type == TileType.CatExit)
            {
                if (t.type == TileType.Switch) button = t;
                continue;
            }

            if (t.type == TileType.Gate && t.GateOpen) continue; // open gate = hole
            supported = true; // sticky or a normal tile: either way it holds the block
            break;
        }

        // A thrown box (BoxProjectile) is not a GridTile: the block lands on top of it.
        if (!supported && BoxProjectile.IsBoxAt(below)) supported = true;

        if (supported)
        {
            if (justLanded)
            {
                justLanded = false;
                AudioManager.Play(Sfx.FallingLand);
            }
            return;
        }

        falling = true;
        justLanded = false;
        AudioManager.Play(Sfx.FallingStart);
        fallT = 0f;
        fallFrom = transform.position;
        cell = below;
        fallTo = GridUtil.CellCenter(cell);

        if (button != null) button.PressButton();
    }

    public static GridTile FindAt(Vector2Int c)
    {
        var tiles = Object.FindObjectsByType<GridTile>(FindObjectsSortMode.None);
        for (int i = 0; i < tiles.Length; i++)
        {
            if (tiles[i].cell == c) return tiles[i];
        }
        return null;
    }

    /// <summary>Normal bricks anchor a sticky block. Jelly and specials do not.</summary>
    public static bool IsNormalBrick(TileType t)
    {
        return t == TileType.Solid || t == TileType.Stone || t == TileType.Wood || t == TileType.Dirt;
    }

    /// <summary>Sticky touches a normal brick on any 4-neighbour side: it is glued and cannot be pushed.</summary>
    public bool IsStickyAnchored()
    {
        if (type != TileType.Sticky) return false;

        var dirs = new[]
        {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
        };
        var tiles = Object.FindObjectsByType<GridTile>(FindObjectsSortMode.None);
        for (int d = 0; d < dirs.Length; d++)
        {
            Vector2Int c = cell + dirs[d];
            for (int i = 0; i < tiles.Length; i++)
            {
                var t = tiles[i];
                if (t == null || t == this || !t.OccupiesCell(c)) continue;
                if (IsNormalBrick(t.type)) return true;
            }
        }
        return false;
    }

    /// <summary>Buttons / plates / ball switches: no body at all, anything may pass through.</summary>
    static bool IsButtonLike(TileType t)
    {
        return t == TileType.Switch || t == TileType.Plate || t == TileType.BallSwitch;
    }

    static bool IsStickyPassable(TileType t)
    {
        return t == TileType.Jelly || IsButtonLike(t) ||
               t == TileType.TriggerPlayer || t == TileType.TriggerBall ||
               t == TileType.TextAnchor || t == TileType.CatTrigger ||
               t == TileType.CatExit;
    }

    /// <summary>
    /// Is there a solid block directly below the sticky? Jelly/buttons/plates/open gates do not count.
    ///
    /// A switch cell is empty space for slime, full stop: the tile has no body, and a ball that
    /// happens to be parked in that cell is only there to hold the switch - it must not quietly
    /// become a floor either, otherwise the switch bends the slime's fall after all.
    /// </summary>
    public bool HasBlockBelow()
    {
        Vector2Int belowCell = cell + Vector2Int.down;
        var tiles = Object.FindObjectsByType<GridTile>(FindObjectsSortMode.None);
        bool buttonCell = false;
        for (int i = 0; i < tiles.Length; i++)
        {
            var t = tiles[i];
            if (t == null || t == this || !t.OccupiesCell(belowCell)) continue;

            if (t.type == TileType.Falling) continue; // a moving falling block is not a support
            if (t.type == TileType.Gate && t.GateOpen) continue;
            if (IsStickyPassable(t.type))
            {
                if (IsButtonLike(t.type)) buttonCell = true;
                continue;
            }
            return true;
        }

        if (buttonCell) return false;

        return BoxProjectile.IsBoxAt(belowCell);
    }

    GridTile FindAdjacentFalling()
    {
        var dirs = new[]
        {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
        };
        var tiles = Object.FindObjectsByType<GridTile>(FindObjectsSortMode.None);
        for (int d = 0; d < dirs.Length; d++)
        {
            Vector2Int c = cell + dirs[d];
            for (int i = 0; i < tiles.Length; i++)
            {
                var t = tiles[i];
                if (t == null || t == this || !t.OccupiesCell(c)) continue;
                if (t.type == TileType.Falling) return t;
            }
        }
        return null;
    }

    /// <summary>Can this sticky be dragged down by a falling block? No block below and not carried.</summary>
    public bool CanStickyFall()
    {
        if (type != TileType.Sticky || followBall != null) return false;
        if (HasBlockBelow()) return false;
        return true;
    }

    /// <summary>
    /// A free sticky floats. It only moves when stuck to a falling block:
    /// with no block below it falls with the block; with a block below it stays
    /// and the falling block falls alone.
    /// </summary>
    void StickyTick()
    {
        if (followBall != null) return;

        // Moving into a cell that touches a normal brick: it glues there, mid-motion.
        // A switch cell never glues: the slime passes over it like it was empty.
        bool anchoredNow = !IsSwitchCellAt(cell) && IsStickyAnchored() &&
                           (followFalling != null || HasBlockBelow());
        if (anchoredNow)
        {
            if (!gluedToWall)
            {
                gluedToWall = true;
                AudioManager.Play(Sfx.StickyGlue);
            }
            followFalling = null;
            stickyFalling = false;
            stickyFallT = 0f;
            transform.position = GridUtil.CellCenter(cell);
            return;
        }

        gluedToWall = false;

        if (stickyFalling)
        {
            stickyFallT += Time.deltaTime * fallSpeed;
            transform.position = Vector3.Lerp(stickyFrom, stickyTo, Mathf.Clamp01(stickyFallT));
            if (stickyFallT >= 1f)
            {
                stickyFalling = false;
                stickyFallT = 0f;
                transform.position = stickyTo;
            }
            return;
        }

        bool supported = HasBlockBelow();
        if (supported) return; // a block below keeps even an anchored sticky in place; block falls alone

        // No block below: a falling block is the only thing that can pull a stuck sticky off.
        var adjacentFalling = FindAdjacentFalling();
        if (adjacentFalling != null) followFalling = adjacentFalling;

        if (followFalling == null || (!followFalling.IsFalling && followFalling.cell != cell + Vector2Int.up))
            return; // floating: free sticky does not fall by itself
        if (followFalling.cell == cell + Vector2Int.down) return;       // let it move first
        if (!CanStickyFall()) return;

        if (TraceSticky)
            Debug.Log($"[Sticky] FALL {cell} -> {cell + Vector2Int.down} | dragFrom={followFalling.cell} " +
                      $"dragIsFalling={followFalling.IsFalling} | followBall={(followBall != null ? "yes" : "no")} " +
                      $"anchored={IsStickyAnchored()} below={HasBlockBelow()}");

        stickyFalling = true;
        stickyFallT = 0f;
        stickyFrom = transform.position;
        cell += Vector2Int.down;
        stickyTo = GridUtil.CellCenter(cell);
    }

    /// <summary>True when that cell currently holds a button / plate / switch - something with no body.</summary>
    static bool IsSwitchCellAt(Vector2Int c)
    {
        var tiles = Object.FindObjectsByType<GridTile>(FindObjectsSortMode.None);
        for (int i = 0; i < tiles.Length; i++)
        {
            var t = tiles[i];
            if (t == null || !t.OccupiesCell(c)) continue;
            if (IsButtonLike(t.type)) return true;
        }
        return false;
    }

    /// <summary>The carrying ball moves one cell; the sticky follows into the cell it just left.</summary>
    public void FollowTo(Vector2Int newCell)
    {
        stickyFalling = false;
        stickyFallT = 0f;
        cell = newCell;
        transform.position = new Vector3(newCell.x + 0.5f, newCell.y + 0.5f, 0f);

        // While being carried, touching a normal brick glues it mid-motion - but a switch is not
        // a brick: passing over one must never stop the slime, it just keeps moving as it would
        // with the switch taken out of the level.
        if (IsSwitchCellAt(newCell)) return;

        if (IsStickyAnchored())
        {
            if (TraceSticky)
                Debug.Log($"[Sticky] DETACH at {newCell} (touches a brick) followBall={(followBall != null ? "yes" : "no")}");
            followFalling = null;
            if (followBall != null)
            {
                followBall.followSticky = null;
                followBall = null;
            }
        }
    }

    public void AttachTo(BoxProjectile ball)
    {
        if (followBall != null && followBall != ball) followBall.followSticky = null;
        if (ball != null && ball.followSticky != null && ball.followSticky != this)
            ball.followSticky.DetachFromBall();

        followBall = ball;
        if (ball != null) ball.followSticky = this;
        stickyFalling = false;
    }

    public void DetachFromBall()
    {
        followBall = null;
        stickyFalling = false;
    }

    void OnDestroy()
    {
        if (followBall != null) followBall.followSticky = null;
    }

    /// <summary>Does this tile occupy that cell? A gate fills two cells.</summary>
    public bool OccupiesCell(Vector2Int c)
    {
        if (c == cell) return true;
        return type == TileType.Gate && c == cell + Vector2Int.up;
    }

    /// <summary>The cell a body standing on top of this tile occupies.</summary>
    public Vector2Int CellAboveTop
    {
        get
        {
            int height = type == TileType.Gate ? 2 : 1;
            return new Vector2Int(cell.x, cell.y + height);
        }
    }

    /// <summary>A button is pressed by a box, a falling block or the player.</summary>
    public void PressButton()
    {
        if (type != TileType.Switch) return;
        if (Time.time < nextSwitchTime) return;

        nextSwitchTime = Time.time + 0.4f;
        AudioManager.Play(Sfx.SwitchPress);
        GateSystem.Toggle(channel, this);
    }

    /// <summary>Channel signal: gates open/close, mirrors turn. Other tiles ignore it.</summary>
    public void OnSignal(bool open)
    {
        if (type == TileType.Gate) SetGateOpen(invert ? !open : open);
        else if (type == TileType.Mirror) SetMirrorOpen(open);
    }

    public void SetMirrorOpen(bool open)
    {
        bool changed = mirrorDown != !open;
        mirrorDown = !open;
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr != null)
            sr.sprite = RefractorSprite(mirrorDown ? TileType.RefractorDown : TileType.RefractorUp);
        if (changed && Application.isPlaying) AudioManager.Play(Sfx.MirrorTurn);
    }

    /// <summary>Mirror/reflect mapping: a 45-degree mirror bounces the ball at its face.</summary>
    public Vector2Int RefractDirection(Vector2Int d)
    {
        bool down = type == TileType.RefractorDown || (type == TileType.Mirror && mirrorDown);
        if (down) return new Vector2Int(-d.y, -d.x);
        return new Vector2Int(d.y, d.x);
    }

    /// <summary>Refract: the ball enters the cell and leaves turned 90 degrees clockwise.</summary>
    public Vector2Int Refract90Direction(Vector2Int d)
    {
        return new Vector2Int(d.y, -d.x);
    }

    /// <summary>How much speed a ball needs to break this block. 0 means unbreakable.</summary>
    public static int BallBreakThreshold(TileType t)
    {
        if (t == TileType.Dirt) return 1;
        if (t == TileType.Wood) return 2;
        if (t == TileType.Stone) return 3;
        return 0;
    }

    /// <summary>Small white pips at the bottom of a block show the required ball speed.</summary>
    void SetThresholdPips(int count)
    {
        // OnValidate runs on every import / domain reload, on scene objects and on the prefab
        // asset. Creating children there dirties scenes and throws as soon as a serialized
        // reference has gone stale - so the pips are generated at runtime only.
        if (!Application.isPlaying) return;

        if (pipsRoot == null)
        {
            var existing = transform.Find("ThresholdPips");
            if (existing != null) pipsRoot = existing;
        }
        if (pipsRoot == null)
        {
            var go = new GameObject("ThresholdPips");
            go.transform.SetParent(transform, false);
            pipsRoot = go.transform;
        }

        bool stale = pips == null || pips.Length != 3;
        if (!stale)
        {
            for (int i = 0; i < pips.Length; i++)
            {
                if (pips[i] == null) { stale = true; break; }
            }
        }

        if (stale)
        {
            var list = new System.Collections.Generic.List<SpriteRenderer>();
            for (int i = 0; i < pipsRoot.childCount; i++)
            {
                var c = pipsRoot.GetChild(i);
                var s = c.GetComponent<SpriteRenderer>();
                if (s != null) list.Add(s);
            }
            while (list.Count < 3)
            {
                var go = new GameObject("pip");
                go.transform.SetParent(pipsRoot, false);
                var s = go.AddComponent<SpriteRenderer>();
                s.sprite = baseSprite;
                s.color = new Color(1f, 0.96f, 0.75f, 1f);
                s.sortingOrder = 2;
                list.Add(s);
            }
            pips = list.ToArray();
        }

        if (baseSprite != null)
        {
            for (int i = 0; i < pips.Length; i++) pips[i].sprite = baseSprite;
        }

        for (int i = 0; i < pips.Length; i++)
        {
            var p = pips[i];
            p.transform.localScale = Vector3.one * 0.12f;
            p.transform.localPosition = new Vector3((i - (count - 1) * 0.5f) * 0.18f, -0.33f, 0f);
            p.enabled = i < count;
        }
        pipsRoot.gameObject.SetActive(count > 0);
    }

    void PlateTick()
    {
        bool pressed = BoxProjectile.IsBoxAt(cell) || HasFallingAt(cell);
        if (pressed == PlatePressed) return;

        PlatePressed = pressed;
        if (pressed) AudioManager.Play(Sfx.PlatePress);
        RefreshPlateVisual();
        GateSystem.RefreshPlates(channel);
    }

    /// <summary>
    /// A ball-hold switch: ON only while a ball's logical cell is this cell.
    /// It uses the same channel/parity logic as a normal Switch.
    /// </summary>
    void BallSwitchTick()
    {
        bool pressed = BoxProjectile.IsBoxAt(cell);
        if (pressed == SwitchOn) return;

        SetSwitchState(pressed);
        GateSystem.RefreshButtons(channel);
        AudioManager.Play(pressed ? Sfx.BallSwitchOn : Sfx.BallSwitchOff);
        Debug.Log($"BallSwitch {cell} ch{channel} -> {(pressed ? "ON" : "OFF")}");
    }

    static bool HasFallingAt(Vector2Int c)
    {
        var tiles = Object.FindObjectsByType<GridTile>(FindObjectsSortMode.None);
        for (int i = 0; i < tiles.Length; i++)
        {
            var t = tiles[i];
            if (t != null && t.type == TileType.Falling && t.cell == c) return true;
        }
        return false;
    }

    void RefreshPlateVisual()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr == null) return;
        if (baseSprite != null) sr.sprite = baseSprite;
        sr.color = PlatePressed
            ? new Color(0.35f, 0.95f, 1.00f, 1f)
            : new Color(0.16f, 0.45f, 0.55f, 0.95f);
    }

    /// <summary>
    /// Edit-mode only: a tile can only ever sit in one cell. Dragging it in the scene
    /// snaps it to the centre of the nearest cell and locks its scale to 1.
    /// </summary>
#if UNITY_EDITOR
    public void EnforceGrid()
    {
        Vector2Int target = GridUtil.WorldToCell(new Vector2(transform.position.x, transform.position.y));
        if (cell != target)
        {
            cell = target;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        if (transform.localScale != Vector3.one)
        {
            transform.localScale = Vector3.one;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        Vector2 center = GridUtil.CellCenter(target);
        Vector3 pos = new Vector3(center.x, center.y, 0f);
        if ((transform.position - pos).sqrMagnitude > 0.000001f)
        {
            transform.position = pos;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        // self-heal an old door (child sprites, half-cell offset) into the single-sprite door
        if (type == TileType.Door && col != null)
        {
            bool wrongCollider = !col.isTrigger
                || !Mathf.Approximately(col.size.x, 1f) || !Mathf.Approximately(col.size.y, 2f)
                || !Mathf.Approximately(col.offset.x, 0f) || !Mathf.Approximately(col.offset.y, 0.5f);
            bool hasOldChildren = transform.Find("DoorVisual") is Transform dv && dv.gameObject.activeSelf;
            bool wrongSprite = DoorSprite != null && sr != null && sr.sprite != DoorSprite;

            if (wrongCollider || hasOldChildren || wrongSprite) Apply();
        }
    }
#endif

    public void SetType(TileType t)
    {
        if (t == TileType.BallSwitch && type != TileType.BallSwitch) channel = 0;
        type = t;
        lastType = t;
        Apply();
    }

    public void Apply()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
        if (col == null) col = GetComponent<BoxCollider2D>();
        if (col == null) col = gameObject.AddComponent<BoxCollider2D>();

        col.size = Vector2.one;
        col.offset = Vector2.zero;
        transform.localScale = Vector3.one;

        if (baseSprite == null && type != TileType.Door) baseSprite = sr.sprite;
        RemoveDoorVisual();

        if (type != TileType.Door && baseSprite != null && sr.sprite != baseSprite) sr.sprite = baseSprite;

        switch (type)
        {
            case TileType.Solid:
                sr.color = SolidColor;
                sr.enabled = true;
                col.enabled = true;
                col.isTrigger = false;
                break;
            case TileType.Stone:
                sr.sprite = BrickArt.Still(TileType.Stone);
                sr.color = Color.white;   // the artwork is already coloured
                sr.enabled = true;
                col.enabled = true;
                col.isTrigger = false;
                break;
            case TileType.Wood:
                sr.sprite = BrickArt.Still(TileType.Wood);
                sr.color = Color.white;
                sr.enabled = true;
                col.enabled = true;
                col.isTrigger = false;
                break;
            case TileType.Refract:
                sr.enabled = true;
                sr.color = Color.white;
                sr.sprite = ResourceSprite("BlockRefract90");
                col.enabled = true;
                col.isTrigger = false;
                break;
            case TileType.Sticky:
                sr.enabled = true;
                sr.color = StickyColor;
                sr.sprite = ResourceSprite("Sticky");
                col.enabled = true;
                col.isTrigger = false;
                break;
            case TileType.Jelly:
                sr.enabled = true;
                sr.color = Color.white;
                sr.sprite = ResourceSprite("Jelly");
                col.enabled = true;
                col.isTrigger = true;
                break;
            case TileType.Slider:
                sr.enabled = true;
                sr.color = Color.white;
                sr.sprite = ResourceSprite("Slider");
                col.enabled = true;
                col.isTrigger = false;
                break;
            case TileType.Bounce:
                ApplySpringSprite(false);
                sr.color = Color.white;
                sr.enabled = true;
                col.enabled = true;
                col.isTrigger = false;
                break;
            case TileType.Dirt:
                sr.sprite = BrickArt.Still(TileType.Dirt);
                sr.color = Color.white;
                sr.enabled = true;
                col.enabled = true;
                col.isTrigger = false;
                break;
            case TileType.Falling:
                sr.color = FallingColor;
                sr.enabled = true;
                col.enabled = true;
                col.isTrigger = false;
                break;
            case TileType.Door:
                // one sprite, no child objects: the door must be clickable / draggable itself
                sr.enabled = true;
                sr.color = Color.white;
                if (DoorSprite != null) sr.sprite = DoorSprite;
                col.enabled = true;
                col.isTrigger = true;
                // exactly the two cells that start at this tile's cell
                col.size = new Vector2(1f, 2f);
                col.offset = new Vector2(0f, 0.5f);
                break;
            case TileType.Goal:
                sr.color = GoalColor;
                sr.enabled = true;
                col.enabled = true;
                col.isTrigger = true;
                break;
            case TileType.RefractorDown:
            case TileType.RefractorUp:
                sr.enabled = true;
                sr.color = Color.white;
                sr.sprite = RefractorSprite(type);
                col.enabled = true;
                col.isTrigger = false; // a solid block: the box turns at its face
                break;
            case TileType.Mirror:
                sr.enabled = true;
                sr.color = Color.white;
                sr.sprite = RefractorSprite(mirrorDown ? TileType.RefractorDown : TileType.RefractorUp);
                col.enabled = true;
                col.isTrigger = false;
                break;
            case TileType.Switch:
                sr.enabled = true;
                sr.color = Color.white;
                sr.sprite = ResourceSprite("SwitchOff");
                col.enabled = true;
                col.isTrigger = false;
                SwitchOn = false;
                break;
            case TileType.BallSwitch:
                sr.enabled = true;
                // cyan tint distinguishes it from the normal latching switch
                sr.color = new Color(0.55f, 0.95f, 1.00f, 1f);
                sr.sprite = ResourceSprite("SwitchOff");
                // no physical presence at all: the player, ball and falling blocks all pass
                col.enabled = false;
                col.isTrigger = true;
                SwitchOn = false;
                break;
            case TileType.Gate:
                sr.enabled = true;
                sr.color = Color.white;
                sr.sprite = ResourceSprite("Gate");
                col.enabled = true;
                col.isTrigger = false;
                col.size = new Vector2(1f, 2f);
                col.offset = new Vector2(0f, 0.5f);
                SetGateOpen(invert); // an inverted gate starts open
                break;
            case TileType.Plate:
                sr.enabled = true;
                if (baseSprite != null) sr.sprite = baseSprite;
                col.enabled = true;
                col.isTrigger = true;
                col.size = Vector2.one;
                col.offset = Vector2.zero;
                PlatePressed = false;
                RefreshPlateVisual();
                break;
            case TileType.Pickup:
            {
                sr.enabled = true;
                sr.color = Color.white;
                // The extra-box pickup is the second (orange) cat, walking in place.
                Sprite[] catWalk = CatArt.Walk(true);
                sr.sprite = (catWalk != null && catWalk.Length > 0)
                    ? catWalk[0] : ResourceSprite("Pickup");
                if (Application.isPlaying && GetComponent<CatPickup>() == null)
                    gameObject.AddComponent<CatPickup>();
                col.enabled = true;
                col.isTrigger = true;
                break;
            }
            case TileType.Spawn:
                // visible marker while editing, invisible while playing
                sr.enabled = !Application.isPlaying;
                sr.color = SpawnColor;
                col.enabled = false;
                break;
            case TileType.TriggerPlayer:
            case TileType.TriggerBall:
                // editor-only marker, trigger-only collider (no movement impact)
                sr.enabled = !Application.isPlaying;
                sr.sprite = baseSprite != null ? baseSprite : ResourceSprite("SwitchOff");
                sr.color = type == TileType.TriggerPlayer
                    ? new Color(1f, 0.32f, 0.88f, 1f)
                    : new Color(0.30f, 0.90f, 1f, 1f);
                col.enabled = true;
                col.isTrigger = true;
                break;
            case TileType.TextAnchor:
                // editor-only marker for where the tutorial text appears
                sr.enabled = !Application.isPlaying;
                sr.sprite = baseSprite != null ? baseSprite : ResourceSprite("SwitchOff");
                sr.color = new Color(1f, 0.90f, 0.30f, 1f);
                col.enabled = false;
                break;
            case TileType.CatTrigger:
            case TileType.CatExit:
                // colourless in play: a bare marker in the editor and nothing at all in the game.
                // The trigger keeps a trigger collider so the player can run into it; the exit
                // point has no collider so nobody bumps into it.
                sr.enabled = !Application.isPlaying;
                sr.sprite = baseSprite != null ? baseSprite : ResourceSprite("SwitchOff");
                sr.color = type == TileType.CatTrigger
                    ? new Color(1f, 0.62f, 0.15f, 1f)    // where the cat is called in
                    : new Color(0.35f, 1f, 0.55f, 1f);   // where the cat is waiting
                col.enabled = type == TileType.CatTrigger;
                col.isTrigger = true;
                break;
        }

        ApplyLocalArt();

        int speedPips = type == TileType.Stone ? 3 : type == TileType.Wood ? 2 : type == TileType.Dirt ? 1 : 0;
        SetThresholdPips(speedPips);

        // Everything static shares one layer, so whichever instance happened to be created last
        // drew on top. The slime has to be the one on top: passing over a switch/plate must show
        // the slime covering it for a moment, never the switch swallowing the slime.
        sr.sortingOrder = type == TileType.Sticky ? 2 : 1;
    }

    static Sprite s_door;

    static Sprite DoorSprite
    {
        get
        {
            if (s_door == null) s_door = Resources.Load<Sprite>("Door");
            return s_door;
        }
    }

    static Sprite s_refractDown;
    static Sprite s_refractUp;
    static Sprite s_wedgeLU, s_wedgeLD, s_wedgeRU, s_wedgeRD;

    /// <summary>
    /// Fork art (BlockRefractUp / BlockRefractDown) stays the default.  A tile that
    /// explicitly picks one of the four wedge rotations uses mirror_lu / mirror_ld /
    /// mirror_ru / mirror_rd instead; if that sprite is missing we fall back to the
    /// fork art so the level never renders a blank cell.
    /// </summary>
    Sprite RefractorSprite(TileType t)
    {
        switch (refractFacing)
        {
            // Solid corner -> sprite.  mirror_<xy> is named after the original art's
            // rotation, so this is not a naive name match.
            case RefractFacing.SolidTopLeft:      // solid top-left,    "/" hypotenuse
                if (s_wedgeLU == null) s_wedgeLU = Resources.Load<Sprite>("Blocks/mirror_rd");
                if (s_wedgeLU != null) return s_wedgeLU;
                break;
            case RefractFacing.SolidBottomLeft:   // solid bottom-left, "\" hypotenuse
                if (s_wedgeLD == null) s_wedgeLD = Resources.Load<Sprite>("Blocks/mirror_ru");
                if (s_wedgeLD != null) return s_wedgeLD;
                break;
            case RefractFacing.SolidTopRight:     // solid top-right,   "\" hypotenuse
                if (s_wedgeRU == null) s_wedgeRU = Resources.Load<Sprite>("Blocks/mirror_ld");
                if (s_wedgeRU != null) return s_wedgeRU;
                break;
            case RefractFacing.SolidBottomRight:  // solid bottom-right, "/" hypotenuse
                if (s_wedgeRD == null) s_wedgeRD = Resources.Load<Sprite>("Blocks/mirror_lu");
                if (s_wedgeRD != null) return s_wedgeRD;
                break;
        }

        if (t == TileType.RefractorDown)
        {
            if (s_refractDown == null) s_refractDown = Resources.Load<Sprite>("BlockRefractDown");
            return s_refractDown;
        }
        if (s_refractUp == null) s_refractUp = Resources.Load<Sprite>("BlockRefractUp");
        return s_refractUp;
    }

    // ---- spring (the bounce block) --------------------------------------

    static Sprite s_springUp, s_springUpHit, s_springSide, s_springSideHit;

    /// <summary>Direction the spring's face points at.</summary>
    public Vector2Int SpringFace
    {
        get
        {
            switch (springFacing)
            {
                case SpringFacing.Up: return Vector2Int.up;
                case SpringFacing.Down: return Vector2Int.down;
                case SpringFacing.Left: return Vector2Int.left;
                default: return Vector2Int.right;
            }
        }
    }

    /// <summary>
    /// True when a ball travelling along <paramref name="dir"/> runs into the spring's face.
    /// A real spring only gives along its own axis - every other side is just a wall.
    /// </summary>
    public bool SpringCatches(Vector2Int dir)
    {
        return type == TileType.Bounce && dir == -SpringFace;
    }

    /// <summary>Spring art: vertical or horizontal, resting or compressed.</summary>
    static Sprite SpringSprite(bool horizontal, bool squashed)
    {
        if (horizontal)
        {
            if (s_springSide == null) s_springSide = Resources.Load<Sprite>("Spring_Side");
            if (!squashed) return s_springSide;
            if (s_springSideHit == null) s_springSideHit = Resources.Load<Sprite>("Spring_Side_Hit");
            return s_springSideHit != null ? s_springSideHit : s_springSide;
        }

        if (s_springUp == null) s_springUp = Resources.Load<Sprite>("Spring");
        if (!squashed) return s_springUp;
        if (s_springUpHit == null) s_springUpHit = Resources.Load<Sprite>("Spring_Hit");
        return s_springUpHit != null ? s_springUpHit : s_springUp;
    }

    /// <summary>Picks the sprite and mirroring that match the spring's facing.</summary>
    void ApplySpringSprite(bool squashed)
    {
        if (sr == null) return;

        bool horizontal = springFacing == SpringFacing.Left || springFacing == SpringFacing.Right;
        sr.sprite = SpringSprite(horizontal, squashed);

        // The art is authored facing up / right; Left and Down are the mirrored copies.
        sr.flipX = springFacing == SpringFacing.Left;
        sr.flipY = springFacing == SpringFacing.Down;
    }

    /// <summary>A ball bounced off this spring: show the squashed frame for a moment.</summary>
    public void SpringHit()
    {
        if (type != TileType.Bounce) return;
        springHitTimer = SpringHitTime;
        ApplySpringSprite(true);
    }

    void SpringTick()
    {
        if (springHitTimer <= 0f) return;

        springHitTimer -= Time.deltaTime;
        if (springHitTimer > 0f) return;

        springHitTimer = 0f;
        ApplySpringSprite(false);
    }

    /// <summary>
    /// The old door was built from child sprites, which stole scene clicks (you could not
    /// select / drag the door). They are hidden so the single door sprite is what you hit.
    /// </summary>
    void RemoveDoorVisual()
    {
        var child = transform.Find("DoorVisual");
        if (child == null) return;
        if (child.gameObject.activeSelf) child.gameObject.SetActive(false);
    }

    static readonly System.Collections.Generic.Dictionary<string, Sprite> s_sprites =
        new System.Collections.Generic.Dictionary<string, Sprite>();

    // Fork resource name -> sprite in the local art drop (Resources/Blocks).
    // A missing drop silently falls back to the fork's own Resources sprite.
    static readonly System.Collections.Generic.Dictionary<string, string> s_localNames =
        new System.Collections.Generic.Dictionary<string, string>
    {
        { "Sticky", "sticky" },
        { "Slider", "slider" },
        { "Gate", "gate" },
        { "SwitchOff", "btn_off" },
        { "SwitchOn", "btn_on" },
    };

    static Sprite ResourceSprite(string name)
    {
        Sprite s;
        if (s_sprites.TryGetValue(name, out s) && s != null) return s;
        string local;
        if (s_localNames.TryGetValue(name, out local)) s = BlockArt.Get(local);
        if (s == null) s = Resources.Load<Sprite>(name);
        s_sprites[name] = s;
        return s;
    }

    /// <summary>
    /// After the fork's type switch has chosen a sprite/color, override it with the
    /// local art drop where one exists. The four-way refractor keeps its own logic
    /// (RefractorSprite), the ball switch keeps its cyan tint, and the gate keeps
    /// the open/closed alpha set by SetGateOpen.
    /// </summary>
    void ApplyLocalArt()
    {
        if (sr == null) return;
        if (type == TileType.RefractorDown || type == TileType.RefractorUp ||
            type == TileType.Mirror || type == TileType.BallSwitch ||
            type == TileType.Pickup) return;

        Sprite art = BlockArt.ForTile(type);
        if (art == null) return;

        sr.sprite = art;
        if (type != TileType.Gate) sr.color = Color.white;
        sr.enabled = true;
    }

    public void SetSwitchState(bool on)
    {
        SwitchOn = on;
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.sprite = ResourceSprite(on ? "SwitchOn" : "SwitchOff");
    }

    public void SetGateOpen(bool open)
    {
        bool changed = GateOpen != open;
        GateOpen = open;
        if (col == null) col = GetComponent<BoxCollider2D>();
        if (col != null) col.enabled = !open;
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = open ? new Color(1f, 1f, 1f, 0.25f) : Color.white;
        if (changed && Application.isPlaying) AudioManager.Play(open ? Sfx.GateOpen : Sfx.GateClose);
    }

    // The player deliberately cannot operate switches: buttons, ball switches and plates are
    // all driven by the thrown ball and by falling blocks only. There is no player-side
    // OnCollisionEnter2D / OnTriggerEnter2D path into PressButton() or PlateTick() any more.

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (type == TileType.TriggerPlayer)
        {
            UnityEditor.Handles.Label(transform.position + new Vector3(0f, 0.9f, 0f), "P ch " + channel);
            return;
        }
        if (type == TileType.TriggerBall)
        {
            UnityEditor.Handles.Label(transform.position + new Vector3(0f, 0.9f, 0f), "BALL ch " + channel);
            return;
        }
        if (type == TileType.TextAnchor)
        {
            UnityEditor.Handles.Label(transform.position + new Vector3(0f, 0.9f, 0f), "TXT ch " + channel);
            return;
        }
        if (type != TileType.Switch && type != TileType.Gate) return;
        float lift = type == TileType.Gate ? 2.2f : 1.2f;
        UnityEditor.Handles.Label(transform.position + new Vector3(0f, lift, 0f), "ch " + channel);
    }
#endif

    void OnGUI()
    {
        if (!Application.isPlaying || type != TileType.TextAnchor || tutorialTimer <= 0f) return;
        if (string.IsNullOrEmpty(tutorialText) || Camera.main == null) return;

        if (tutorialStyle == null)
        {
            tutorialStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
        }
        if (tutorialFont != null) tutorialStyle.font = tutorialFont;
        tutorialStyle.fontSize = tutorialFontSize;

        float a = Mathf.Clamp01(tutorialTimer / 0.35f) * tutorialColor.a;
        tutorialStyle.normal.textColor = new Color(tutorialColor.r, tutorialColor.g, tutorialColor.b, a);

        Vector3 world = transform.position + (Vector3)tutorialOffset;
        Vector3 sp = Camera.main.WorldToScreenPoint(world);
        if (sp.z < 0f) return;

        var content = new GUIContent(tutorialText);
        Vector2 size = tutorialStyle.CalcSize(content);
        var rect = new Rect(sp.x - size.x * 0.5f, (Screen.height - sp.y) - size.y * 0.5f, size.x, size.y);

        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.55f * a);
        GUI.DrawTexture(new Rect(rect.x - 9f, rect.y - 6f, rect.width + 18f, rect.height + 12f), Texture2D.whiteTexture);
        GUI.color = old;
        GUI.Label(rect, content, tutorialStyle);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        var player = other.GetComponent<PlayerController>();
        if (player == null) return;

        if (type == TileType.TriggerPlayer)
        {
            FireTutorial(channel);
            return;
        }

        if (type == TileType.CatTrigger)
        {
            if (catTriggerActor != CatTriggerActor.Cat) TriggerCat();
            return;
        }

        // jelly: passable, but the player is slowed while inside it
        if (type == TileType.Jelly)
        {
            player.EnterJelly();
            return;
        }

        // pressure plate: the player standing on it does nothing - only a ball or a falling
        // block presses it (see PlateTick). Kept as an early out so nothing below runs.
        if (type == TileType.Plate) return;

        // extra-box pickup
        if (type == TileType.Pickup)
        {
            AudioManager.Play(Sfx.Pickup);
            player.GrantExtraBox();
            Destroy(gameObject);
            return;
        }

        if (type != TileType.Goal && type != TileType.Door) return;

        int index = 0;
        var boot = Object.FindFirstObjectByType<LevelBootstrap>();
        if (boot != null) index = boot.levelIndex;
        if (LevelCompleteUI.Instance != null) LevelCompleteUI.Instance.Show(index);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        var player = other.GetComponent<PlayerController>();
        if (player == null) return;

        if (type == TileType.Jelly)
        {
            player.ExitJelly();
            return;
        }
    }
}
