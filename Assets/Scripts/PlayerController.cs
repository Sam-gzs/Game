using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Move")]
    public float moveSpeed = 6f;
    public float groundAccel = 70f;
    public float airAccel = 40f;
    public float jumpSpeed = 12.5f;
    public float riseGravity = 3.1f;
    public float fallGravity = 5.4f;
    public float maxFallSpeed = 24f;
    public float coyoteTime = 0.10f;
    public float jumpBufferTime = 0.12f;

    [Header("Box")]
    public float throwCooldown = 0.16f;
    public Sprite blockSprite;
    public Material blockMaterial;

    [Header("Charge")]
    public int maxCharge = 3;
    public float chargeStep = 0.22f;

    [Header("Aim")]
    // edge length of one dash block, in cells (0.2 cell = 0.05 corner radius at 25%)
    public float dashBlockSize = 0.2f;
    // opacity of the aim dashes
    [Range(0.1f, 1f)] public float dashAlpha = 0.9f;

    [Header("Refs")]
    public Transform visual;
    public LayerMask groundMask = ~0;

    Rigidbody2D rb;
    BoxCollider2D col;
    float coyote;
    float jumpBuffer;
    float nextThrow;
    int facing = 1;
    int charge;
    float chargeTimer;
    bool charging;
    int jellyContacts;
    public float jellySlow = 0.35f;
    Transform dashRoot;              // container for the pooled aim blocks
    SpriteRenderer[] dashes;         // one block per cell the ball would pass through
    bool wasGrounded = true;   // spawn already counts as grounded: no landing thud on level start

    public bool Grounded { get; private set; }
    public float SpeedX => rb != null ? rb.linearVelocity.x : 0f;
    public float SpeedY => rb != null ? rb.linearVelocity.y : 0f;
    /// <summary>Raw A/D input this frame, even when a wall prevents actual movement.</summary>
    public float MoveInput { get; private set; }
    public int Facing => facing;
    public int ThrowCount { get; private set; }
    public int ChargeLevel => charge;
    public bool IsCharging => charging;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<BoxCollider2D>();
        rb.gravityScale = riseGravity;

        // always start the level with the designed capacity, whatever value got serialized
        boxCapacity = Mathf.Clamp(startBoxCapacity, 1, MaxBoxCapacity);
        if (maxCharge < 1) maxCharge = 3;
        if (chargeStep <= 0.01f) chargeStep = 0.22f;
        if (jellySlow <= 0.01f) jellySlow = 0.35f;
        if (dashBlockSize <= 0.02f) dashBlockSize = 0.2f;
        CreateAimDashes();
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        // Rounded feet/shoulders: a square box catches on the seam between two floor tiles.
        // NOTE: BoxCollider2D.edgeRadius grows the shape OUTWARDS, so the inner size has to be
        // shrunk by twice the radius - otherwise the stickman becomes 2.14 cells tall and can
        // no longer fit through a 2 cell high gap.
        const float bodyWidth = 0.9f;
        const float bodyHeight = 1.9f;
        const float radius = 0.12f;
        if (col != null)
        {
            if (col.edgeRadius < radius) col.edgeRadius = radius;
            col.size = new Vector2(bodyWidth - 2f * col.edgeRadius, bodyHeight - 2f * col.edgeRadius);
        }
    }

    /// <summary>Bottom of the real (rounded) collider shape.</summary>
    float FeetY()
    {
        if (col == null) return transform.position.y - 0.95f;
        return col.bounds.min.y - col.edgeRadius;
    }

    void Update()
    {
        // Paused (settings overlay / level-complete): never read gameplay input, otherwise
        // a click on the UI would start a charge and throw a box on release.
        if (Time.timeScale <= 0.0001f)
        {
            charging = false;
            charge = 0;
            MoveInput = 0f;
            HideAimPreview();
            return;
        }

        var kb = Keyboard.current;
        var mouse = Mouse.current;

        float ax = 0f;
        if (kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) ax -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) ax += 1f;

            if (kb.eKey.wasPressedThisFrame) RecallBox();
            if (kb.rKey.wasPressedThisFrame) RestartLevel();

            if (kb.spaceKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame)
                jumpBuffer = jumpBufferTime;

            if ((kb.spaceKey.wasReleasedThisFrame || kb.wKey.wasReleasedThisFrame) && rb.linearVelocity.y > 0f)
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * 0.45f);
        }

        MoveInput = ax;

        jumpBuffer = Mathf.Max(0f, jumpBuffer - Time.deltaTime);
        coyote = Grounded ? coyoteTime : Mathf.Max(0f, coyote - Time.deltaTime);

        float speedFactor = jellyContacts > 0 ? jellySlow : 1f;
        float target = ax * moveSpeed * speedFactor;
        float vx = rb.linearVelocity.x;
        if (Grounded) vx = Mathf.MoveTowards(vx, target, groundAccel * Time.deltaTime);
        else if (Mathf.Abs(ax) > 0.01f) vx = Mathf.MoveTowards(vx, target, airAccel * Time.deltaTime);
        rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);

        if (Mathf.Abs(ax) > 0.01f) facing = ax > 0f ? 1 : -1;

        if (jumpBuffer > 0f && coyote > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpSpeed);
            jumpBuffer = 0f;
            coyote = 0f;
            Grounded = false;
            AudioManager.Play(Sfx.Jump);
        }

        if (mouse != null)
        {
            if (mouse.leftButton.wasPressedThisFrame && Time.time >= nextThrow)
            {
                if (!Grounded)
                {
                    AudioManager.Play(Sfx.RefuseAirThrow);
                    ToastUI.Show("LAND FIRST  -  no throwing in the air");
                }
                else
                {
                    charging = true;
                    chargeTimer = 0f;
                    charge = 1;
                }
            }

            if (charging)
            {
                if (mouse.leftButton.isPressed)
                {
                    chargeTimer += Time.deltaTime;
                    charge = Mathf.Clamp(1 + Mathf.FloorToInt(chargeTimer / Mathf.Max(0.05f, chargeStep)), 1, maxCharge);
                }
                else
                {
                    charging = false;
                    nextThrow = Time.time + throwCooldown;
                    int power = Mathf.Clamp(charge, 1, maxCharge);
                    charge = 0;
                    Throw(Camera.main != null ? ScreenToWorld(mouse) : (Vector2)transform.position + Vector2.right, power);
                }
            }
        }

        if (charging) UpdateAimPreview();
        else HideAimPreview();

        if (visual != null)
        {
            Vector3 s = visual.localScale;
            s.x = Mathf.Abs(s.x) * facing;
            visual.localScale = s;
        }
    }

    void FixedUpdate()
    {
        Grounded = CheckGround();
        if (Grounded && !wasGrounded) AudioManager.Play(Sfx.Land);
        wasGrounded = Grounded;
        rb.gravityScale = rb.linearVelocity.y > 0.1f ? riseGravity : fallGravity;
        if (rb.linearVelocity.y < -maxFallSpeed)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -maxFallSpeed);
    }

    Vector2 ScreenToWorld(Mouse mouse)
    {
        Vector3 p = mouse.position.ReadValue();
        p.z = -Camera.main.transform.position.z;
        return Camera.main.ScreenToWorldPoint(p);
    }

    bool CheckGround()
    {
        // cast from just above the soles so a 1px overlap / tile seam still counts as ground
        float bottom = FeetY();
        Vector2 origin = new Vector2(transform.position.x + col.offset.x, bottom + 0.12f);
        Vector2 size = new Vector2(col.size.x + col.edgeRadius * 2f, 0.10f);
        size.x *= 0.75f;
        RaycastHit2D[] hits = Physics2D.BoxCastAll(origin, size, 0f, Vector2.down, 0.26f, groundMask);
        for (int i = 0; i < hits.Length; i++)
        {
            var h = hits[i];
            if (h.collider == null || h.collider == col) continue;
            if (h.collider.isTrigger) continue;
            if (h.normal.y > 0.25f) return true;
        }
        return false;
    }

    /// <summary>Jelly slows the player while he is inside it; multiple overlapping jelly cells stack the flag.</summary>
    public void EnterJelly()
    {
        jellyContacts++;
    }

    public void ExitJelly()
    {
        jellyContacts = Mathf.Max(0, jellyContacts - 1);
    }

    /// <summary>How many boxes this player may have in the world at once (pickups raise it).</summary>
    public int boxCapacity = 1;

    /// <summary>Capacity every level starts with. boxCapacity is always reset to this on Awake.</summary>
    public int startBoxCapacity = 1;

    public const int MaxBoxCapacity = 3;

    /// <summary>Pickups add to the current capacity, they never set an absolute value.</summary>
    public void GrantExtraBox()
    {
        if (boxCapacity >= MaxBoxCapacity) return;
        boxCapacity++;
        ToastUI.Show("EXTRA BOX  -  you can throw " + boxCapacity);
    }

    /// <summary>Press E: only a box that has come to rest can be taken back / destroyed.</summary>
    public void RecallBox()
    {
        var box = BoxProjectile.LatestIdle;
        if (box == null)
        {
            if (BoxProjectile.Count > 0) ToastUI.Show("BOX IS STILL MOVING  -  wait until it stops");
            return;
        }

        Destroy(box.gameObject);
        AudioManager.Play(Sfx.Recall);
        ToastUI.Show("BOX RECALLED");
    }

    /// <summary>Press R: reload the level (irreversible blocks/stone make this necessary).</summary>
    public void RestartLevel()
    {
        AudioManager.Play(Sfx.LevelRestart);
        var boot = Object.FindFirstObjectByType<LevelBootstrap>();
        GameFlow.LoadLevel(boot != null ? boot.levelIndex : 0);
    }

    static readonly Vector2Int[] AimDirs =
    {
        new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(0, 1), new Vector2Int(-1, 1),
        new Vector2Int(-1, 0), new Vector2Int(-1, -1), new Vector2Int(0, -1), new Vector2Int(1, -1)
    };

    Vector2Int AimDirection(Vector2 aim)
    {
        if (aim.sqrMagnitude < 0.0001f) return new Vector2Int(facing, 0);
        float ang = Mathf.Atan2(aim.y, aim.x);
        int sector = Mathf.RoundToInt(ang / (Mathf.PI / 4f));
        sector %= 8;
        if (sector < 0) sector += 8;
        return AimDirs[sector];
    }

    static Vector2Int TargetCellFor(Vector2Int lowerCell, Vector2Int dir)
    {
        if (dir.y > 0 && dir.x == 0) return new Vector2Int(lowerCell.x, lowerCell.y + 2);
        if (dir.y > 0) return new Vector2Int(lowerCell.x + dir.x, lowerCell.y + 1);
        if (dir.y < 0 && dir.x == 0) return new Vector2Int(lowerCell.x, lowerCell.y - 1);
        if (dir.y < 0) return new Vector2Int(lowerCell.x + dir.x, lowerCell.y - 1);
        return new Vector2Int(lowerCell.x + dir.x, lowerCell.y);
    }

    /// <summary>Throw the ball in one of 8 directions with a 1..3 charge.</summary>
    public BoxProjectile Throw(Vector2 worldPoint, int power)
    {
        if (!Grounded)
        {
            ToastUI.Show("LAND FIRST  -  no throwing in the air");
            return null;
        }

        // capacity is 1 by default; an extra-box pickup raises it
        if (BoxProjectile.Count >= boxCapacity) return null;

        Vector2 bodyCenter = transform.position;
        Vector2Int lowerCell = FeetCell();

        Vector2Int dir = AimDirection(worldPoint - bodyCenter);
        if (dir.x != 0) facing = dir.x;

        Vector2Int targetCell = TargetCellFor(lowerCell, dir);

        // The ball may only be released into a cell it can actually occupy.
        // Buttons/plates/open gates are pass-through; solids, breakables, mirrors,
        // refractors, sticky blocks and falling blocks are not.
        if (!BoxProjectile.CanSpawnAt(targetCell))
        {
            AudioManager.Play(Sfx.RefuseBlocked);
            ToastUI.Show(BoxProjectile.IsBoxAt(targetCell)
                ? "NO ROOM HERE  -  a ball is already there"
                : "NO ROOM HERE  -  the ball would get stuck");
            return null;
        }

        ThrowCount++;
        AudioManager.Play(Sfx.Throw);
        return BoxProjectile.Spawn(targetCell, dir, blockSprite, blockMaterial, Mathf.Clamp(power, 1, maxCharge));
    }

    // ---------------------------------------------------------------------
    // Aim preview - a dashed trail of pixel blocks, one per cell centre
    // ---------------------------------------------------------------------

    /// <summary>Same sorting layer/order the old solid aim line used.</summary>
    const int AimSortingOrder = 30;

    /// <summary>Hard cap on dashes: the preview path never walks more than 160 steps.</summary>
    const int MaxDashBlocks = 192;

    /// <summary>
    /// Texels along one dash edge. A 0.2 cell block is ~19 px tall at 1080p with the level
    /// camera (orthographic size 5.6), so 20 texels lands almost exactly 1 texel = 1 pixel.
    /// </summary>
    const int DashTexels = 20;

    /// <summary>Corner radius as a fraction of the edge: 25% of a 0.2 cell block = 0.05.</summary>
    const float DashCornerRatio = 0.25f;

    static Sprite dashSprite;

    /// <summary>
    /// Hard-edged rounded square. Every texel is either fully opaque or fully clear, so the
    /// corner comes out as a stepped pixel-art cut instead of a smooth curve. The sprite
    /// spans one world unit and is scaled down to dashBlockSize.
    /// </summary>
    static Sprite DashSprite()
    {
        if (dashSprite != null) return dashSprite;

        const int n = DashTexels;
        int r = Mathf.Clamp(Mathf.RoundToInt(n * DashCornerRatio), 1, n / 2); // 5 texels = 0.05 cell

        // Exact inset of the arc for each row inside the corner box...
        var inset = new int[n];
        for (int y = 0; y < n; y++)
        {
            float dy = r - (y + 0.5f);
            if (dy <= 0f) continue;
            float dx = r - Mathf.Sqrt(Mathf.Max(0f, r * r - dy * dy));
            inset[y] = Mathf.Clamp(Mathf.RoundToInt(dx), 0, r);
        }

        // ...then force a one-texel-per-row staircase. At 20 texels the raw arc drops 3 -> 1
        // between two rows, which reads as a notch rather than a rounded corner.
        for (int y = 1; y < r; y++)
            inset[y] = Mathf.Max(inset[y], inset[y - 1] - 1);

        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
        {
            name = "AimDashRounded",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        var px = new Color32[n * n];
        var solid = new Color32(255, 255, 255, 255);
        var clear = new Color32(255, 255, 255, 0);

        for (int y = 0; y < n; y++)
        {
            int row = Mathf.Min(y, n - 1 - y);      // mirror: same cut top and bottom
            int cut = inset[row];
            for (int x = 0; x < n; x++)
            {
                int col = Mathf.Min(x, n - 1 - x);  // and left and right
                px[y * n + x] = col >= cut ? solid : clear;
            }
        }

        tex.SetPixels32(px);
        tex.Apply(false, false);

        // FullRect keeps the mesh exactly the block square; a Tight mesh would trim the
        // transparent corner texels and make the block wobble by a pixel.
        dashSprite = Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), n,
            0u, SpriteMeshType.FullRect);
        dashSprite.name = "AimDashRounded";
        return dashSprite;
    }

    void CreateAimDashes()
    {
        if (dashRoot != null) return;

        var go = new GameObject("AimDashes");
        go.transform.SetParent(transform, false);
        dashRoot = go.transform;

        var sprite = DashSprite();
        dashes = new SpriteRenderer[MaxDashBlocks];
        for (int i = 0; i < dashes.Length; i++)
        {
            var block = new GameObject("Dash" + i);
            block.transform.SetParent(dashRoot, false);

            var sr = block.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = AimSortingOrder;
            sr.enabled = false;
            dashes[i] = sr;
        }
    }

    void HideAimPreview()
    {
        HideDashesFrom(0);
    }

    void HideDashesFrom(int first)
    {
        if (dashes == null) return;
        for (int i = Mathf.Max(0, first); i < dashes.Length; i++)
        {
            if (dashes[i] != null && dashes[i].enabled) dashes[i].enabled = false;
        }
    }

    /// <summary>Place one block in every cell of the preview path.</summary>
    void ShowDashes(List<Vector3> cells, Color color)
    {
        CreateAimDashes();

        int count = Mathf.Min(cells.Count, dashes.Length);
        Vector3 size = Vector3.one * dashBlockSize;

        for (int i = 0; i < count; i++)
        {
            var d = dashes[i];
            d.transform.position = cells[i];
            d.transform.localScale = size;
            d.color = color;
            if (!d.enabled) d.enabled = true;
        }

        HideDashesFrom(count);
    }

    void ShowSingleBlock(Vector3 cell, Color color)
    {
        CreateAimDashes();

        var d = dashes[0];
        d.transform.position = cell;
        d.transform.localScale = Vector3.one * dashBlockSize;
        d.color = color;
        d.enabled = true;

        HideDashesFrom(1);
    }

    void UpdateAimPreview()
    {
        var mouse = Mouse.current;
        if (mouse == null || Camera.main == null || !Grounded)
        {
            HideAimPreview();
            return;
        }

        Vector2 world = ScreenToWorld(mouse);
        Vector2Int dir = AimDirection(world - (Vector2)transform.position);
        Vector2Int target = TargetCellFor(FeetCell(), dir);
        int level = Mathf.Clamp(charge, 1, maxCharge);

        if (!BoxProjectile.CanSpawnAt(target))
        {
            // Nothing leaves the hand here: one ghosted block marks the refused cell.
            ShowSingleBlock(Center(target), ChargePalette.Denied);
            return;
        }

        var cells = BuildPreview(target, dir, level);
        if (cells.Count == 0)
        {
            HideAimPreview();
            return;
        }

        ShowDashes(cells, ChargePalette.WithAlpha(ChargePalette.ForLevel(level), dashAlpha));
    }

    /// <summary>
    /// The cells the ball would visit, starting at the release cell. No point for the
    /// player's own position: the trail must not draw a diagonal from the hand to the ball.
    /// </summary>
    List<Vector3> BuildPreview(Vector2Int start, Vector2Int dir, int speed)
    {
        var points = new List<Vector3>(64);
        points.Add(Center(start));

        var startTile = TileAt(start);
        if (startTile != null && startTile.type == TileType.Jelly)
            speed = Mathf.Max(0, speed - 1);

        // The ball can be caught immediately if it is released beside an anchored sticky.
        if (PreviewContactStop(start, speed)) return points;

        Vector2Int cell = start;
        Vector2Int d = dir;
        int bounces = 0;

        for (int step = 0; step < 160; step++)
        {
            Vector2Int next = cell + d;

            if (BoxProjectile.IsBoxAt(next))
            {
                points.Add(Center(next));
                break;
            }

            var tile = TileAt(next);
            if (tile == null)
            {
                points.Add(Center(next));
                cell = next;
                if (PreviewContactStop(cell, speed)) return points;
                continue;
            }

            switch (tile.type)
            {
                case TileType.Jelly:
                    points.Add(Center(next));
                    // Contact is checked on entering the jelly, before its -1 is applied.
                    if (PreviewContactStop(next, speed)) return points;
                    speed = Mathf.Max(0, speed - 1);
                    cell = next;
                    continue;

                case TileType.Switch:
                case TileType.Plate:
                case TileType.BallSwitch:
                case TileType.TriggerPlayer:
                case TileType.TriggerBall:
                case TileType.TextAnchor:
                    points.Add(Center(next));
                    cell = next;
                    if (PreviewContactStop(cell, speed)) return points;
                    continue;

                case TileType.Gate:
                    points.Add(Center(next));
                    if (tile.GateOpen)
                    {
                        cell = next;
                        if (PreviewContactStop(cell, speed)) return points;
                        continue;
                    }
                    return points;

                case TileType.Mirror:
                    points.Add(Center(next));
                    d = tile.RefractDirection(d);
                    speed = Mathf.Min(BoxProjectile.MaxMomentum, speed + 1);
                    bounces++;
                    if (bounces > 6) return points;
                    continue;

                case TileType.RefractorDown:
                case TileType.RefractorUp:
                {
                    points.Add(Center(next));
                    if (speed <= 1) return points; // 1 momentum: does not enter

                    int after = Mathf.Max(0, speed - 1);
                    Vector2Int outDir = tile.RefractDirection(d);
                    var exitTile = TileAt(next + outDir);
                    if (exitTile != null)
                    {
                        int need = GridTile.BallBreakThreshold(exitTile.type);
                        // Too slow to leave the refractor into that block: the ball loses its
                        // momentum and drops here. Nothing is destroyed, so the preview just stops.
                        if (need > 0 && after < need) return points;
                    }

                    speed = after;
                    d = outDir;
                    cell = next;
                    if (PreviewContactStop(cell, speed)) return points;
                    continue;
                }

                case TileType.Refract:
                    points.Add(Center(next));
                    if (speed <= 1) return points;
                    speed = Mathf.Max(0, speed - 1);
                    d = tile.Refract90Direction(d);
                    cell = next;
                    if (PreviewContactStop(cell, speed)) return points;
                    continue;

                case TileType.Bounce:
                    points.Add(Center(next));
                    d = -d;
                    bounces++;
                    if (bounces > 6) return points;
                    continue;

                case TileType.Sticky:
                    points.Add(Center(next));
                    bool stuckSticky = tile.followBall == null &&
                                       (tile.IsStickyAnchored() || tile.followFalling != null);
                    if (stuckSticky && speed < 4) return points;
                    cell = next;
                    if (PreviewContactStop(cell, speed)) return points;
                    continue;

                default:
                    points.Add(Center(next));
                    int required = GridTile.BallBreakThreshold(tile.type);
                    if (required > 0 && speed >= required)
                    {
                        speed -= 1;
                        if (speed <= 0) return points;
                        cell = next;
                        if (PreviewContactStop(cell, speed)) return points;
                        continue;
                    }
                    return points;
            }
        }

        return points;
    }

    static bool PreviewContactStop(Vector2Int cell, int speed)
    {
        if (speed >= 4) return false;
        return IsAdjacentToAnchoredSticky(cell);
    }

    static bool IsAdjacentToAnchoredSticky(Vector2Int cell)
    {
        var dirs = new[]
        {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
        };
        for (int i = 0; i < dirs.Length; i++)
        {
            var t = TileAt(cell + dirs[i]);
            if (t == null) continue;
            if (t.type != TileType.Sticky) continue;
            if (t.followBall != null) continue;
            if (t.IsStickyAnchored()) return true;
        }
        return false;
    }

    static GridTile TileAt(Vector2Int cell)
    {
        var tiles = Object.FindObjectsByType<GridTile>(FindObjectsSortMode.None);
        for (int i = 0; i < tiles.Length; i++)
        {
            var t = tiles[i];
            if (t != null && t.OccupiesCell(cell)) return t;
        }
        return null;
    }

    static Vector3 Center(Vector2Int cell)
    {
        Vector2 p = GridUtil.CellCenter(cell);
        return new Vector3(p.x, p.y, 0f);
    }

    void OnDisable()
    {
        HideAimPreview();
    }

    void OnGUI()
    {
        if (!Application.isPlaying) return;

        GUIStyle label = new GUIStyle(GUI.skin.label) { fontSize = 14 };
        label.normal.textColor = new Color(0.85f, 0.89f, 0.98f, 0.9f);

        GUI.Label(new Rect(18f, 44f, 80f, 22f), "POWER", label);

        for (int i = 0; i < maxCharge; i++)
        {
            var r = new Rect(88f + i * 28f, 48f, 22f, 14f);
            // level 1 yellow -> 2 orange -> 3 red, same ramp as the ball and the aim dashes
            GUI.color = i < charge
                ? ChargePalette.ForLevel(i + 1)
                : new Color(1f, 1f, 1f, 0.14f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
        }
        GUI.color = Color.white;

        if (charging)
        {
            label.normal.textColor = ChargePalette.ForLevel(Mathf.Clamp(charge, 1, maxCharge));
            GUI.Label(new Rect(18f, 64f, 300f, 22f), "release to throw  (power " + charge + ")", label);
        }
    }

    /// <summary>Width of the free strip needed beside the feet cell to release a box.</summary>
    public float releaseReach = 1.1f;

    bool SideSpaceFree(Vector2Int feetCell, int dirX)
    {
        float reach = Mathf.Max(0.5f, releaseReach);
        float x0 = dirX > 0 ? feetCell.x + 1f : feetCell.x - reach;
        Vector2 center = new Vector2(x0 + reach * 0.5f, feetCell.y + 0.5f);
        Vector2 size = new Vector2(reach - 0.02f, 0.9f);

        var hits = Physics2D.OverlapBoxAll(center, size, 0f);
        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if (hit == null || hit.isTrigger) continue;
            if (hit.GetComponent<PlayerController>() != null) continue; // the thrower
            if (hit.GetComponent<BoxProjectile>() != null) return false;
            if (hit.GetComponent<GridTile>() != null) return false;
        }

        return true;
    }

    /// <summary>
    /// The cell the player is standing in, derived from the tile under his feet rather than
    /// from his exact x - so standing on the edge of a block cannot shift the throw target.
    /// </summary>
    public Vector2Int FeetCell()
    {
        float bottom = FeetY();
        var hit = Physics2D.Raycast(new Vector2(transform.position.x, bottom - 0.01f), Vector2.down, 0.12f, groundMask);
        if (hit.collider != null && !hit.collider.isTrigger)
        {
            var tile = hit.collider.GetComponent<GridTile>();
            if (tile != null) return tile.CellAboveTop;
        }

        return GridUtil.WorldToCell(new Vector2(transform.position.x, bottom + 0.1f));
    }
}
