using UnityEngine;

/// <summary>
/// The single box the stickman can throw. It steps from cell to cell on one axis at constant
/// speed (no gravity while travelling - only a collision changes its momentum), and it can
/// never stay in the air: as soon as it stops it falls cell by cell until something holds it.
/// </summary>
public class BoxProjectile : MonoBehaviour
{
    static readonly System.Collections.Generic.List<BoxProjectile> All = new System.Collections.Generic.List<BoxProjectile>();

    /// <summary>How many boxes are currently in the world.</summary>
    public static int Count => All.Count;

    /// <summary>The most recently thrown box.</summary>
    public static BoxProjectile Latest => All.Count > 0 ? All[All.Count - 1] : null;

    /// <summary>The most recent box that is standing still (may be destroyed).</summary>
    public static BoxProjectile LatestIdle
    {
        get
        {
            for (int i = All.Count - 1; i >= 0; i--)
            {
                var b = All[i];
                if (b != null && b.IsIdle) return b;
            }
            return null;
        }
    }

    /// <summary>True only when the box is settled: not flying, not falling, not sliding.</summary>
    public bool IsIdle => Resting && !moving && !falling && !sliding;

    /// <summary>Kept for compatibility: same as Latest.</summary>
    public static BoxProjectile Active => Latest;

    public float moveSpeed = 9f;      // cells per second
    public float fallSpeed = 12f;     // cells per second
    public float bounceBoost = 1.5f;

    public Vector2Int Cell { get; private set; }
    public Vector2Int Direction { get; private set; }
    public bool Resting { get; private set; }

    /// <summary>1..3. Charge level chosen by the player; also the impact strength.</summary>
    public int speedLevel = 1;

    /// <summary>Momentum can be raised by reflections; cap keeps a mirror loop finite.</summary>
    public const int MaxMomentum = 5;

    /// <summary>Sticky block this ball is carrying, if any.</summary>
    public GridTile followSticky;

    /// <summary>True: carried sticky stays in front of the ball (low momentum push). False: behind (swap).</summary>
    bool followStickyAhead;

    SpriteRenderer sr;

    // The cat is the ball's skin: idle frames while it is parked, roll frames while it moves.
    Sprite[] catIdle;
    Sprite[] catRoll;
    float catAnim;
    float catIdleAnim;

    [Tooltip("The cat art's forward is 90 degrees off the movement vector; adjust if needed.")]
    public float rollAngleOffset = -90f;

    [Tooltip("World-space diameter of the cat ball. Keep it equal to CatCompanion.catHeight so the stopped cat and the cat block match.")]
    public float catBallSize = 1.0f;

    Vector3 fromPos;
    Vector3 toPos;
    float t;
    bool moving;
    bool falling;
    bool sliding;
    bool stuck;
    float slideT;
    bool wasResting;

    /// <summary>Refractor cells this box has already used (each one works once per box).</summary>
    readonly System.Collections.Generic.HashSet<Vector2Int> usedRefractors = new System.Collections.Generic.HashSet<Vector2Int>();

    public bool HasUsedRefractor(Vector2Int cell)
    {
        return usedRefractors.Contains(cell);
    }

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        All.Add(this);
    }

    void OnDestroy()
    {
        if (followSticky != null)
        {
            followSticky.DetachFromBall();
            followSticky = null;
        }
        All.Remove(this);
    }

    public static BoxProjectile Spawn(Vector2Int cell, Vector2Int direction, Sprite sprite, Material material, int power = 1)
    {
        if (IsBoxAt(cell)) return null;

        var go = new GameObject("Ball");
        var center = GridUtil.CellCenter(cell);
        go.transform.position = new Vector3(center.x, center.y, 0f);

        var ballSprite = Resources.Load<Sprite>("Ball");

        // The cat itself is the ball: the renderer lives on a child so the collider,
        // the rigidbody and the logical cell stay on the root object.
        var catGo = new GameObject("CatVisual");
        catGo.transform.SetParent(go.transform, false);
        var csr = catGo.AddComponent<SpriteRenderer>();
        csr.sortingOrder = 6;
        if (material != null) csr.sharedMaterial = material;

        var idle = LoadSequence("CatIdle", 6);
        var roll = LoadSequence("CatRoll", 20);
        Sprite first = idle.Length > 0 ? idle[0] : (roll.Length > 0 ? roll[0] : ballSprite);
        if (first == null) first = sprite;
        csr.sprite = first;
        csr.color = Color.white;
        catGo.transform.localScale = Vector3.one;

        var body = go.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.useFullKinematicContacts = true;

        var col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.92f, 0.92f);

        var box = go.AddComponent<BoxProjectile>();
        box.Cell = cell;
        box.Direction = direction;
        box.fromPos = box.toPos = go.transform.position;
        box.speedLevel = Mathf.Clamp(power, 1, 3);
        box.moving = true;
        box.sr = csr;
        box.catIdle = idle;
        box.catRoll = roll;
        box.SetCatScale(first);
        box.PressButtonAt(cell);
        box.ApplyJellyAt(cell);
        box.CheckStickyContact();

        return box;
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

    static Color SpeedColor(int s)
    {
        if (s <= 0) return new Color(0.55f, 0.55f, 0.62f, 1f); // spent
        if (s >= 5) return new Color(0.55f, 0.95f, 1.00f, 1f); // max momentum
        if (s == 4) return new Color(1f, 0.98f, 0.80f, 1f);
        if (s == 3) return new Color(1f, 0.88f, 0.35f, 1f);
        if (s == 2) return new Color(1f, 0.62f, 0.24f, 1f);
        return new Color(0.90f, 0.32f, 0.20f, 1f);
    }

    static void PlayBreak(TileType type)
    {
        if (type == TileType.Dirt) AudioManager.Play(Sfx.BreakDirt);
        else if (type == TileType.Wood) AudioManager.Play(Sfx.BreakWood);
        else AudioManager.Play(Sfx.BreakStone);
    }

    void SetCatScale(Sprite s)
    {
        if (sr == null || s == null) return;
        float h = s.rect.height / Mathf.Max(1f, s.pixelsPerUnit);
        float k = catBallSize / Mathf.Max(0.001f, h);
        sr.transform.localScale = new Vector3(k, k, 1f);
    }

    void UpdateSpeedVisual()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        // The cat art is already coloured: tinting it by momentum would just muddy it.
        if (sr != null) sr.color = Color.white;
    }

    void UpdateCatVisual()
    {
        if (sr == null) return;

        bool active = moving || falling || sliding;
        if (active && catRoll != null && catRoll.Length > 0)
        {
            float speed = Mathf.Lerp(9f, 18f, Mathf.Clamp01(speedLevel / 5f));
            catAnim += Time.deltaTime * speed;
            int f = Mathf.Abs(Mathf.FloorToInt(catAnim)) % catRoll.Length;
            sr.sprite = catRoll[f];
            SetCatScale(sr.sprite);

            float ax = Mathf.Abs(Direction.x);
            float angle = Mathf.Atan2(Direction.y, ax) * Mathf.Rad2Deg + rollAngleOffset;
            sr.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            sr.flipX = Direction.x < 0f;
        }
        else if (catIdle != null && catIdle.Length > 0)
        {
            catIdleAnim += Time.deltaTime;
            int f = Mathf.Abs(Mathf.FloorToInt(catIdleAnim * 2f)) % catIdle.Length;
            sr.sprite = catIdle[f];
            SetCatScale(sr.sprite);
            sr.transform.localRotation = Quaternion.identity;
        }
    }

    void Update()
    {
        UpdateCatVisual();

        if (Resting && !wasResting && !stuck) AudioManager.Play(Sfx.BallRest);
        wasResting = Resting;

        if (sliding)
        {
            slideT += Time.deltaTime * 18f;
            transform.position = Vector3.Lerp(fromPos, toPos, Mathf.Clamp01(slideT));
            if (slideT >= 1f)
            {
                sliding = false;
                transform.position = toPos;
                Resting = true;
            }
            return;
        }

        if (moving) Step(Direction);
        else if (falling) Step(Vector2Int.down);
        else if (Resting)
        {
            CheckStickyContact();
            CheckSupport();
        }
    }

    void Step(Vector2Int dir)
    {
        float stepSpeed = moving ? moveSpeed : fallSpeed;
        t += Time.deltaTime * stepSpeed;

        if (t >= 1f)
        {
            t -= 1f;
            fromPos = toPos;

            Vector2Int next = Cell + dir;
            if (!IsFree(next))
            {
                OnBlocked(dir, next);
                return;
            }

            // Gravity feeds momentum for the cell being entered. Do it before MoveToCell
            // so the anchored-sticky contact check sees the speed the ball actually has
            // in that cell: a fast falling ball passes, a slow one is caught.
            if (falling)
            {
                speedLevel = Mathf.Min(MaxMomentum, speedLevel + 1);
                UpdateSpeedVisual();
                AudioManager.PlayBallSpeed(speedLevel);
            }

            MoveToCell(next);
            toPos = GridUtil.CellCenter(Cell);
            PressButtonAt(Cell);
            ApplyJellyAt(Cell);
        }

        transform.position = Vector3.Lerp(fromPos, toPos, Mathf.Clamp01(t));
    }

    void OnBlocked(Vector2Int dir, Vector2Int blockedCell)
    {
        // another box in the way: shove a resting one forward, otherwise stop dead
        var other = FindBox(blockedCell);
        if (other != null)
        {
            Vector2Int beyond = blockedCell + dir;
            if (other.Resting && other.IsFree(beyond))
            {
                other.Push(dir);
                fromPos = transform.position;
                MoveToCell(blockedCell);
                toPos = GridUtil.CellCenter(Cell);
                t = 0f;
                return;
            }

            CeilingPenalty(dir);
            StopHere();
            return;
        }

        var tile = FindTile(blockedCell);

        if (tile != null)
        {
            // Dirt / wood / stone are broken only by a fast enough ball.
            // Breaking costs one speed level, so a full-power ball can chew through
            // several blocks in a row before it runs out.
            int required = GridTile.BallBreakThreshold(tile.type);
            if (required > 0)
            {
                if (speedLevel >= required)
                {
                    PlayBreak(tile.type);
                    BrickBreakFx.Play(tile.type, tile.transform.position);
                    Destroy(tile.gameObject);
                    speedLevel -= 1;
                    fromPos = transform.position;
                    MoveToCell(blockedCell);
                    toPos = GridUtil.CellCenter(Cell);
                    t = 0f;
                    UpdateSpeedVisual();
                    if (speedLevel <= 0 && !stuck) StopHere(false);
                    return;
                }

                CeilingPenalty(dir);
                StopHere();
                return;
            }

            // Sticky block: this must work for BOTH a ball flying into it and a ball
            // falling onto it. It used to sit inside the "if (moving)" block, so a
            // falling ball never reached it: it stopped on top, re-fell forever and
            // could not be recalled.
            if (tile.type == TileType.Sticky)
            {
                bool stuckSticky = tile.followBall == null &&
                                   (tile.IsStickyAnchored() || tile.followFalling != null);

                if (stuckSticky)
                {
                    if (speedLevel >= 4)
                    {
                        // The stuck sticky cannot move; a carried free sticky cannot follow
                        // through it, so it is left behind.
                        if (followSticky != null)
                        {
                            followSticky.DetachFromBall();
                            followSticky = null;
                        }

                        AudioManager.Play(Sfx.BallPassSticky);
                        fromPos = transform.position;
                        MoveToCell(blockedCell);
                        toPos = GridUtil.CellCenter(Cell);
                        t = 0f;
                        return;
                    }

                    AudioManager.Play(Sfx.BallStuck);
                    StopHere(false);
                    stuck = true;
                    Resting = true;
                    falling = false;
                    speedLevel = 0;
                    UpdateSpeedVisual();
                    return;
                }

                if (speedLevel <= 2)
                {
                    Vector2Int ahead = blockedCell + dir;
                    if (!IsFree(ahead))
                    {
                        StopHere();
                        return;
                    }

                    tile.AttachTo(this);
                    followStickyAhead = true;
                    if (GridTile.TraceSticky)
                        Debug.Log($"[Sticky] PUSH {tile.cell} -> {ahead} dir {dir} speed {speedLevel}");
                    fromPos = transform.position;
                    MoveToCell(blockedCell);
                    toPos = GridUtil.CellCenter(Cell);
                    t = 0f;
                    return;
                }

                tile.AttachTo(this);
                followStickyAhead = false;
                fromPos = transform.position;
                MoveToCell(blockedCell);
                toPos = GridUtil.CellCenter(Cell);
                t = 0f;
                return;
            }

            // Spring: a real spring only gives along the axis of its face. A ball arriving
            // against the face is thrown straight back with the same momentum (elastic);
            // every other side is a solid block. Checked for falling balls too, which is
            // exactly how a spring gets used as a trampoline.
            if (tile.type == TileType.Bounce)
            {
                if (tile.SpringCatches(dir))
                {
                    AudioManager.Play(Sfx.Bounce);
                    tile.SpringHit();

                    Direction = -dir;
                    moving = true;
                    falling = false;
                    sliding = false;
                    fromPos = toPos;
                    t = 0f;
                    return;
                }

                CeilingPenalty(dir);
                StopHere();
                return;
            }

            if (moving)
            {
                // Refractor mirrors (R/T): the ball enters the cell and then turns.
                // Each one works once per ball; after that it is a plain solid block.
                if ((tile.type == TileType.RefractorDown || tile.type == TileType.RefractorUp) &&
                    !usedRefractors.Contains(blockedCell))
                {
                    // A ball with only 1 momentum cannot enter/pay the refraction:
                    // it stops at the face and falls.
                    if (speedLevel <= 1)
                    {
                        StopHere();
                        return;
                    }

                    usedRefractors.Add(blockedCell);
                    AudioManager.Play(Sfx.BallRefract);
                    Direction = tile.RefractDirection(Direction);
                    speedLevel = Mathf.Max(0, speedLevel - 1); // refraction costs 1 momentum
                    UpdateSpeedVisual();
                    if (StopIfRefractExitTooHard(tile, blockedCell)) return;
                    fromPos = transform.position;
                    MoveToCell(blockedCell);
                    toPos = GridUtil.CellCenter(Cell);
                    t = 0f;
                    return;
                }

                // Reflect mirror (Mirror): the ball bounces off the face and never enters.
                // Reflection adds 1 momentum (capped so a loop cannot grow forever).
                if (tile.type == TileType.Mirror)
                {
                    AudioManager.Play(Sfx.MirrorTurn);
                    Direction = tile.RefractDirection(Direction);
                    speedLevel = Mathf.Min(MaxMomentum, speedLevel + 1);
                    UpdateSpeedVisual();
                    fromPos = toPos;
                    t = 0f;
                    return;
                }

                // A refractor that this ball already used is a plain solid block now.
                if ((tile.type == TileType.RefractorDown || tile.type == TileType.RefractorUp) &&
                    usedRefractors.Contains(blockedCell))
                {
                    AudioManager.Play(Sfx.RefractorSpent);
                }

                // Refract blocks: the ball enters and is turned 90 degrees clockwise.
                // Refraction costs 1 momentum; a 1-momentum ball is too slow to enter.
                if (tile.type == TileType.Refract)
                {
                    if (speedLevel <= 1)
                    {
                        StopHere();
                        return;
                    }

                    AudioManager.Play(Sfx.BallRefract);
                    Direction = tile.Refract90Direction(Direction);
                    speedLevel = Mathf.Max(0, speedLevel - 1);
                    UpdateSpeedVisual();
                    if (StopIfRefractExitTooHard(tile, blockedCell)) return;
                    fromPos = transform.position;
                    MoveToCell(blockedCell);
                    toPos = GridUtil.CellCenter(Cell);
                    t = 0f;
                    return;
                }

                // Sticky blocks and springs are handled above, for moving and falling balls alike.
            }
        }

        CeilingPenalty(dir);
        StopHere();
    }

    /// <summary>Hitting the ceiling while moving upward costs 1 momentum, then the ball falls.</summary>
    void CeilingPenalty(Vector2Int dir)
    {
        if (!moving || dir.y <= 0) return;
        speedLevel = Mathf.Max(0, speedLevel - 1);
        UpdateSpeedVisual();
    }

    /// <summary>
    /// A refractor whose outgoing direction runs straight into a destructible block the ball
    /// cannot break is a dead end. The turn already costs 1 momentum, so the ball simply keeps
    /// no momentum at all: it stalls at the refractor's face and then falls if there is nothing
    /// under it. Nothing is destroyed - the refractor, the block and the ball all survive.
    /// </summary>
    bool StopIfRefractExitTooHard(GridTile refractor, Vector2Int refractorCell)
    {
        if (refractor == null || !moving) return false;

        var next = FindTile(refractorCell + Direction);
        if (next == null) return false;

        int required = GridTile.BallBreakThreshold(next.type);
        if (required <= 0) return false;          // not a destructible block
        if (speedLevel >= required) return false; // it will be pierced normally

        // Lose the momentum the turn gave us, without destroying anything.
        speedLevel = 0;
        UpdateSpeedVisual();
        ToastUI.Show("NOT ENOUGH MOMENTUM  -  the ball drops");
        StopHere();
        return true;
    }

    /// <summary>
    /// Move the ball to a new cell and drag its carried sticky into the cell the ball just left.
    /// All cell changes while flying (step, break, refract, push) go through here.
    /// </summary>
    void MoveToCell(Vector2Int newCell)
    {
        Vector2Int old = Cell;
        Cell = newCell;
        if (followSticky != null)
        {
            Vector2Int dir = newCell - old;
            Vector2Int stickyCell = followStickyAhead ? newCell + dir : old;

            // A carried sticky follows one cell per step. When the ball turns around, "ahead"
            // ends up on the far side of the ball, and moving the sticky there would teleport it
            // straight through the ball (push it up into a switch, let the ball fall, and the
            // sticky snaps back down). In that case leave it where it is and drop the carry.
            Vector2Int from = followSticky.cell;
            int step = Mathf.Abs(stickyCell.x - from.x) + Mathf.Abs(stickyCell.y - from.y);

            if (step > 1)
            {
                if (GridTile.TraceSticky)
                    Debug.Log($"[Sticky] RELEASE {from} (ball {old}->{newCell}, target {stickyCell} was {step} cells away)");
                followSticky.DetachFromBall();
                followSticky = null;
            }
            else
            {
                followSticky.FollowTo(stickyCell);
            }
        }

        CheckStickyContact();
    }

    /// <summary>
    /// Touching an anchored sticky block from ANY side (even when moving past it)
    /// catches a ball with less than 4 momentum and zeroes that momentum.
    /// </summary>
    void CheckStickyContact()
    {
        if (stuck || speedLevel >= 4) return;

        var dirs = new[]
        {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
        };
        var tiles = Object.FindObjectsByType<GridTile>(FindObjectsSortMode.None);

        for (int d = 0; d < dirs.Length; d++)
        {
            Vector2Int c = Cell + dirs[d];
            for (int i = 0; i < tiles.Length; i++)
            {
                var t = tiles[i];
                if (t == null || !t.OccupiesCell(c)) continue;
                if (t.type != TileType.Sticky) continue;
                if (t.followBall != null) continue; // a sticky carried by a ball is not an anchor
                if (!t.IsStickyAnchored()) continue;

                AudioManager.Play(Sfx.BallStuck);
                StopHere(false);
                stuck = true;
                Resting = true;
                falling = false;
                speedLevel = 0;
                UpdateSpeedVisual();
                return;
            }
        }
    }

    /// <summary>Park the box in its current cell; it then falls if nothing holds it.</summary>
    void StopHere(bool blocked = true)
    {
        if (blocked) AudioManager.Play(Sfx.BallBlocked);

        moving = false;
        falling = false;
        Resting = false;
        sliding = false;
        stuck = false;
        t = 0f;
        fromPos = toPos = GridUtil.CellCenter(Cell);
        transform.position = fromPos;

        // never leave the box floating: drop it until something holds it
        if (CanFallInto(Cell + Vector2Int.down))
        {
            falling = true;
        }
        else
        {
            Resting = true;
        }
    }

    void CheckSupport()
    {
        if (stuck) return;
        if (CanFallInto(Cell + Vector2Int.down))
        {
            Resting = false;
            falling = true;
            fromPos = toPos;
            t = 0f;
        }
    }

    /// <summary>
    /// A fast ball (>=4 momentum) can drop THROUGH an anchored sticky below it.
    /// A slower ball treats the sticky as a floor and is caught by the contact check.
    /// </summary>
    bool CanFallInto(Vector2Int cell)
    {
        if (IsFree(cell)) return true;

        if (speedLevel >= 4)
        {
            var tile = FindTile(cell);
            if (tile != null && tile.type == TileType.Sticky &&
                tile.followBall == null && tile.IsStickyAnchored())
                return true;
        }

        return false;
    }

    /// <summary>
    /// A cell is free only if no solid tile AND no other box is in it.
    /// Buttons/plates and open gates are pass-through; a gate fills two cells.
    /// </summary>
    bool IsFree(Vector2Int cell)
    {
        var tiles = Object.FindObjectsByType<GridTile>(FindObjectsSortMode.None);
        for (int i = 0; i < tiles.Length; i++)
        {
            var t = tiles[i];
            if (t == null || !t.OccupiesCell(cell)) continue;

            if (t.type == TileType.Switch || t.type == TileType.Plate ||
                t.type == TileType.Jelly || t.type == TileType.BallSwitch ||
                t.type == TileType.TriggerPlayer || t.type == TileType.TriggerBall ||
                t.type == TileType.TextAnchor || t.type == TileType.CatTrigger ||
                t.type == TileType.CatExit) continue; // pass through
            if (t.type == TileType.Gate && t.GateOpen) continue;                // open gate: hole

            return false;
        }

        return FindBox(cell) == null;
    }

    BoxProjectile FindBox(Vector2Int cell)
    {
        for (int i = 0; i < All.Count; i++)
        {
            var b = All[i];
            if (b == null || b == this) continue;
            if (b.Cell == cell) return b;
        }
        return null;
    }

    public static bool IsBoxAt(Vector2Int cell)
    {
        for (int i = 0; i < All.Count; i++)
        {
            var b = All[i];
            if (b != null && b.Cell == cell) return true;
        }
        return false;
    }

    /// <summary>Can a box be released into this cell? Mirrors/refractors/falling blocks block it.</summary>
    public static bool CanSpawnAt(Vector2Int cell)
    {
        if (IsBoxAt(cell)) return false;

        var tiles = Object.FindObjectsByType<GridTile>(FindObjectsSortMode.None);
        for (int i = 0; i < tiles.Length; i++)
        {
            var t = tiles[i];
            if (t == null || !t.OccupiesCell(cell)) continue;

            if (t.type == TileType.Switch || t.type == TileType.Plate ||
                t.type == TileType.Jelly || t.type == TileType.BallSwitch ||
                t.type == TileType.TriggerPlayer || t.type == TileType.TriggerBall ||
                t.type == TileType.TextAnchor || t.type == TileType.CatTrigger ||
                t.type == TileType.CatExit) continue;
            if (t.type == TileType.Gate && t.GateOpen) continue;

            return false;
        }

        return true;
    }

    /// <summary>Shoved one cell by another box.</summary>
    public void Push(Vector2Int dir)
    {
        fromPos = transform.position;
        MoveToCell(Cell + dir);
        toPos = GridUtil.CellCenter(Cell);
        slideT = 0f;
        sliding = true;
        moving = false;
        falling = false;
        Resting = false;
        PressButtonAt(Cell);
        ApplyJellyAt(Cell);
    }

    /// <summary>
    /// A button does not block the box: entering its cell presses it instead.
    /// Called whenever the box's logical cell changes (step, push or spawn).
    /// </summary>
    void PressButtonAt(Vector2Int cell)
    {
        var tiles = Object.FindObjectsByType<GridTile>(FindObjectsSortMode.None);
        for (int i = 0; i < tiles.Length; i++)
        {
            var t = tiles[i];
            if (t != null && t.cell == cell && t.type == TileType.Switch) t.PressButton();
        }
    }

    /// <summary>Jelly is passable, but the ball loses one momentum on entering it.</summary>
    void ApplyJellyAt(Vector2Int cell)
    {
        var tile = FindTile(cell);
        if (tile == null || tile.type != TileType.Jelly) return;

        speedLevel = Mathf.Max(0, speedLevel - 1);
        UpdateSpeedVisual();
    }

    static GridTile FindTile(Vector2Int cell)
    {
        var tiles = Object.FindObjectsByType<GridTile>(FindObjectsSortMode.None);
        for (int i = 0; i < tiles.Length; i++)
        {
            if (tiles[i].cell == cell) return tiles[i];
        }
        return null;
    }
}
