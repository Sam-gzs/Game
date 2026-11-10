using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Builds the game: MainMenu (start + level select) and grid levels.
/// A level is an ASCII map - every character becomes one GridTile prefab instance,
/// so the map can also be edited later with Tools > Grid Painter.
/// </summary>
public static class StickmanGameBuilder
{
    const string ArtFolder = "Assets/Art";
    const string ScenesFolder = "Assets/Scenes";
    const string SpritePath = ArtFolder + "/WhiteSquare.png";
    const string MaterialPath = ArtFolder + "/SpriteUnlit.mat";
    const string MenuScenePath = ScenesFolder + "/MainMenu.unity";

    static readonly Color Background = new Color(0.045f, 0.052f, 0.075f, 1f);
    static readonly Color Backdrop = new Color(0.045f, 0.052f, 0.075f, 1f);
    static readonly Color Accent = new Color(0.11f, 0.80f, 0.74f, 1f);
    static readonly Color Slate = new Color(0.20f, 0.23f, 0.32f, 1f);
    static readonly Color Card = new Color(0.12f, 0.14f, 0.20f, 1f);
    static readonly Color Muted = new Color(0.62f, 0.68f, 0.82f, 1f);
    static readonly Color DarkText = new Color(0.03f, 0.06f, 0.07f, 1f);

    // ---- maps: '#' solid, 'S' stone, 'B' bounce, 'P' player spawn, 'G' goal, '.' empty ----
    // rows go top (y = height-1) to bottom (y = 0)

    static readonly string[] MapTutorial =
    {
        "##################################",
        "#................................#",
        "#................................#",
        "#................................#",
        "#................................#",
        "#................................#",
        "#.................##...########..#",
        "#.................##.............#",
        "#.P........#.DD...##.....SS....E.#",
        "##################################"
    };

    static readonly string[] MapPractice =
    {
        "##################################",
        "#................................#",
        "#................................#",
        "#................................#",
        "#................................#",
        "#................................#",
        "#.................##...########..#",
        "#.................##.............#",
        "#.P...............##....SSS.....E.#",
        "##################################"
    };

    static readonly string[] MapChallenge =
    {
        "##################################",
        "#................................#",
        "#................................#",
        "#................................#",
        "#................................#",
        "#................................#",
        "#.................##...########..#",
        "#.................##.............#",
        "#.P......#...DDD..##...SSSS.....E.#",
        "##################################"
    };

    // ------------------------------------------------------------------
    // "Build All" 已移除（2026-11-05）
    // 它会把本文件里过时的 34 格 ASCII 地图重新铺到 Level1-3 上，覆盖现有 43~98 格
    // 的手工关卡，并把 EditorBuildSettings 重置成 4 个场景（菜单只剩 3 关）。
    // 需要重建单关请用下面的 "Rebuild Current Level From Map"，或直接用 Tools > Grid Painter。
    // ------------------------------------------------------------------

    [MenuItem("Tools/Stickman Jump/Rebuild Current Level From Map")]
    public static void RebuildLevelsOnly()
    {
        // only the level you have open - other levels are never touched
        string open = EditorSceneManager.GetActiveScene().name;
        string[][] maps = { MapTutorial, MapPractice, MapChallenge };
        int index = -1;
        for (int i = 0; i < maps.Length; i++)
        {
            if (open == "Level" + (i + 1)) index = i;
        }

        if (index < 0)
        {
            EditorUtility.DisplayDialog("No level open",
                "Open Level1, Level2 or Level3 first, then rebuild it from its ASCII map.",
                "OK");
            return;
        }

        bool ok = EditorUtility.DisplayDialog(
            "Rebuild " + open + "?",
            "This regenerates only " + open + " from its ASCII map.\n\n" +
            "Hand edits in " + open + " will be LOST. The other levels are left alone.",
            "Rebuild " + open, "Cancel");
        if (!ok) return;

        EnsureFolders();
        Sprite sprite = EnsureWhiteSprite();
        Material material = EnsureMaterial();
        GameObject tilePrefab = GridSceneUtil.EnsurePrefab(sprite, material);
        BuildLevel(index, maps[index], sprite, material, tilePrefab);
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/Stickman Jump/Generate Levels 4-9 (copy Level3)")]
    public static void GenerateLevelsFromLevel3()
    {
        string src = ScenesFolder + "/Level3.unity";
        if (!File.Exists(src))
        {
            Debug.LogError("[StickmanGameBuilder] Level3.unity not found.");
            return;
        }

        for (int i = 4; i <= GameFlow.LevelCount; i++)
        {
            string dst = ScenesFolder + "/Level" + i + ".unity";
            if (File.Exists(dst)) continue;
            AssetDatabase.CopyAsset(src, dst);
        }

        AssetDatabase.Refresh();

        for (int i = 4; i <= GameFlow.LevelCount; i++)
        {
            string path = ScenesFolder + "/Level" + i + ".unity";
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            foreach (var root in scene.GetRootGameObjects())
            {
                var boot = root.GetComponentInChildren<LevelBootstrap>(true);
                if (boot != null) boot.levelIndex = i - 1;
            }
            EditorSceneManager.SaveScene(scene);
            EditorSceneManager.CloseScene(scene, true);
        }

        UpdateBuildSettings();
        AssetDatabase.SaveAssets();
        Debug.Log("[StickmanGameBuilder] Levels 4-" + GameFlow.LevelCount + " created from Level3.");
    }

    [MenuItem("Tools/Stickman Jump/Rebuild Menu Only (9 levels)")]
    public static void RebuildMenuOnly()
    {
        if (EditorSceneManager.GetActiveScene().isDirty)
        {
            Debug.LogWarning("[StickmanGameBuilder] Save the open scene first - the menu rebuild opens a new scene.");
            return;
        }

        EnsureFolders();
        EnsureWhiteSprite();
        EnsureMaterial();
        BuildMenuScene();
        UpdateBuildSettings();
        AssetDatabase.SaveAssets();
        Debug.Log("[StickmanGameBuilder] Main menu rebuilt with " + GameFlow.LevelCount + " level buttons.");
    }

    static void UpdateBuildSettings()
    {
        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>();
        list.Add(new EditorBuildSettingsScene(MenuScenePath, true));
        for (int i = 1; i <= GameFlow.LevelCount; i++)
        {
            string p = ScenesFolder + "/Level" + i + ".unity";
            if (File.Exists(p)) list.Add(new EditorBuildSettingsScene(p, true));
        }
        EditorBuildSettings.scenes = list.ToArray();
    }

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder(ScenesFolder)) AssetDatabase.CreateFolder("Assets", "Scenes");
        if (!AssetDatabase.IsValidFolder(ArtFolder)) AssetDatabase.CreateFolder("Assets", "Art");
        if (!AssetDatabase.IsValidFolder("Assets/Scripts")) AssetDatabase.CreateFolder("Assets", "Scripts");
    }

    static Sprite EnsureWhiteSprite()
    {
        if (!File.Exists(SpritePath))
        {
            const int size = 8;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(SpritePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 8f;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
    }

    [MenuItem("Tools/Stickman Jump/Create Door Sprite")]
    public static void CreateDoorSprite()
    {
        const string dir = "Assets/Resources";
        if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets", "Resources");
        const string path = dir + "/Door.png";

        const int w = 16, h = 32;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var px = new Color32[w * h];
        Color32 gold = new Color32(242, 199, 89, 255);
        Color32 dark = new Color32(26, 89, 71, 255);
        Color32 panel = new Color32(41, 140, 102, 255);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool border = x < 2 || x > w - 3 || y < 2 || y > h - 3;
                Color32 c = border ? gold : ((x < 4 || x > w - 5) ? dark : panel);
                px[y * w + x] = c;
            }
        }

        tex.SetPixels32(px);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = 16f;
        imp.filterMode = FilterMode.Point;
        imp.mipmapEnabled = false;
        imp.textureCompression = TextureImporterCompression.Uncompressed;

        var settings = new TextureImporterSettings();
        imp.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = new Vector2(0.5f, 0.25f); // bottom sits on the tile's cell
        imp.SetTextureSettings(settings);
        imp.SaveAndReimport();

        AssetDatabase.SaveAssets();
        Debug.Log("[StickmanGameBuilder] Door sprite created at " + path);
    }

    [MenuItem("Tools/Stickman Jump/Create Extra Box Pickup Prefab")]
    public static void CreatePickupPrefab()
    {
        EnsureFolders();
        Sprite sprite = EnsureWhiteSprite();
        Material material = EnsureMaterial();
        GameObject tilePrefab = GridSceneUtil.EnsurePrefab(sprite, material);
        EnsureVariantPrefab(tilePrefab, TileType.Pickup, GridSceneUtil.PrefabFolder + "/ExtraBoxPickup.prefab");
        AssetDatabase.SaveAssets();
        Debug.Log("[StickmanGameBuilder] Extra box pickup prefab created.");
    }

    [MenuItem("Tools/Stickman Jump/Create Switch + Gate Prefabs")]
    public static void CreateSwitchGatePrefabs()
    {
        EnsureFolders();
        Sprite sprite = EnsureWhiteSprite();
        Material material = EnsureMaterial();
        GameObject tilePrefab = GridSceneUtil.EnsurePrefab(sprite, material);
        EnsureVariantPrefab(tilePrefab, TileType.Switch, GridSceneUtil.PrefabFolder + "/Switch.prefab");
        EnsureVariantPrefab(tilePrefab, TileType.Gate, GridSceneUtil.PrefabFolder + "/Gate.prefab");
        AssetDatabase.SaveAssets();
        Debug.Log("[StickmanGameBuilder] Switch and Gate prefabs created.");
    }

    [MenuItem("Tools/Stickman Jump/Create Refractor Prefabs")]
    public static void CreateRefractorPrefabs()
    {
        EnsureFolders();
        Sprite sprite = EnsureWhiteSprite();
        Material material = EnsureMaterial();
        GameObject tilePrefab = GridSceneUtil.EnsurePrefab(sprite, material);
        EnsureVariantPrefab(tilePrefab, TileType.RefractorDown, GridSceneUtil.PrefabFolder + "/RefractDown.prefab");
        EnsureVariantPrefab(tilePrefab, TileType.RefractorUp, GridSceneUtil.PrefabFolder + "/RefractUp.prefab");
        AssetDatabase.SaveAssets();
        Debug.Log("[StickmanGameBuilder] Refractor prefabs created.");
    }

    [MenuItem("Tools/Stickman Jump/Create Falling Block Prefab")]
    public static void CreateFallingPrefab()
    {
        EnsureFolders();
        Sprite sprite = EnsureWhiteSprite();
        Material material = EnsureMaterial();
        GameObject tilePrefab = GridSceneUtil.EnsurePrefab(sprite, material);
        EnsureVariantPrefab(tilePrefab, TileType.Falling, GridSceneUtil.PrefabFolder + "/FallingBlock.prefab");
        AssetDatabase.SaveAssets();
        Debug.Log("[StickmanGameBuilder] FallingBlock prefab created.");
    }

    /// <summary>Convenience prefabs: the same grid tile pre-set to a tile kind.</summary>
    static void EnsureVariantPrefab(GameObject basePrefab, TileType type, string path)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
        var tile = go.GetComponent<GridTile>();
        tile.cell = Vector2Int.zero;
        tile.SetType(type);
        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets();
    }

    static Material EnsureMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat != null) return mat;

        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");

        mat = new Material(shader) { name = "SpriteUnlit" };
        AssetDatabase.CreateAsset(mat, MaterialPath);
        AssetDatabase.SaveAssets();
        return mat;
    }

    // ------------------------------------------------------------------ level

    static void BuildLevel(int index, string[] rows, Sprite sprite, Material material, GameObject tilePrefab)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        BuildLevelCamera();
        BuildGroundTiles(rows, tilePrefab);

        var bootGo = new GameObject("Level");
        var boot = bootGo.AddComponent<LevelBootstrap>();
        boot.levelIndex = index;

        Vector2 spawn = FindSpawn(rows);
        BuildPlayer(sprite, material, spawn);

        BuildCompleteUI(index);

        string path = ScenesFolder + "/Level" + (index + 1) + ".unity";
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, path);
    }

    static void BuildGroundTiles(string[] rows, GameObject tilePrefab)
    {
        int height = rows.Length;
        for (int r = 0; r < height; r++)
        {
            string row = rows[r];
            int y = height - 1 - r;
            for (int x = 0; x < row.Length; x++)
            {
                char c = row[x];
                if (c == '.') continue;

                TileType type;
                switch (c)
                {
                    case '#': type = TileType.Solid; break;
                    case 'S': type = TileType.Stone; break;
                    case 'B': type = TileType.Bounce; break;
                    case 'G': type = TileType.Goal; break;
                    case 'D': type = TileType.Dirt; break;
                    case 'F': type = TileType.Falling; break;
                    case 'E': type = TileType.Door; break;
                    case 'R': type = TileType.RefractorDown; break;
                    case 'T': type = TileType.RefractorUp; break;
                    case 'K': type = TileType.Switch; break;
                    case 'H': type = TileType.Gate; break;
                    case 'X': type = TileType.Pickup; break;
                    case 'P': type = TileType.Spawn; break;
                    default: continue;
                }

                GridSceneUtil.Place(new Vector2Int(x, y), type, tilePrefab);
            }
        }
    }

    static Vector2 FindSpawn(string[] rows)
    {
        int height = rows.Length;
        for (int r = 0; r < height; r++)
        {
            int idx = rows[r].IndexOf('P');
            if (idx >= 0) return new Vector2(idx + 0.5f, height - 1 - r + 1f);
        }
        return new Vector2(2.5f, 2f);
    }

    static void BuildLevelCamera()
    {
        var go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        go.transform.position = new Vector3(10f, 5f, -10f);

        var cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 5.6f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Background;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 60f;

        go.AddComponent<AudioListener>();
        var follow = go.AddComponent<CameraFollow>();
        follow.minY = 5f;
        follow.maxY = 5f;
        follow.verticalOffset = 0f;
        follow.lookAhead = 0f;
        follow.minX = 9.5f;
        follow.maxX = 23.5f;

        go.AddComponent<HudText>();
    }

    // ------------------------------------------------------------------- menu

    static void BuildMenuScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.transform.position = new Vector3(0f, 0f, -10f);
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Backdrop;
        camGo.AddComponent<AudioListener>();

        Canvas canvas = UIFactory.CreateCanvas("MenuCanvas");
        Transform root = canvas.transform;

        var mainBg = UIFactory.FullScreen(root, "MainPanel", Backdrop);
        UIFactory.Label(mainBg.transform, "Title", "这是一个标题", 128, Color.white, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.70f), new Vector2(1700f, 200f), Vector2.zero);

        var startBtn = UIFactory.MakeButton(mainBg.transform, "StartButton", "开始游戏", new Vector2(0.5f, 0.40f),
            new Vector2(420f, 96f), Accent, 48, DarkText);
        var quitBtn = UIFactory.MakeButton(mainBg.transform, "QuitButton", "退出", new Vector2(0.5f, 0.27f),
            new Vector2(420f, 72f), Slate, 32, Color.white);

        var levelBg = UIFactory.FullScreen(root, "LevelPanel", Backdrop);
        UIFactory.Label(levelBg.transform, "Title", "选择关卡", 96, Color.white, TextAnchor.MiddleCenter,
            new Vector2(0.5f, 0.73f), new Vector2(1200f, 120f), Vector2.zero);

        var levelButtons = new Button[GameFlow.LevelCount];
        var levelTexts = new Text[GameFlow.LevelCount];

        const int columns = 3;
        for (int i = 0; i < GameFlow.LevelCount; i++)
        {
            int col = i % columns;
            int row = i / columns;

            var btn = UIFactory.MakeButton(levelBg.transform, "LevelButton" + (i + 1), "第 " + (i + 1) + " 关",
                new Vector2(0.5f, 0.70f), new Vector2(300f, 120f), Card, 32, Color.white);

            float x = (col - (columns - 1) * 0.5f) * 340f;
            float y = -row * 140f;
            ((RectTransform)btn.transform).anchoredPosition = new Vector2(x, y);

            levelButtons[i] = btn;
            levelTexts[i] = btn.GetComponentInChildren<Text>();
        }

        var backBtn = UIFactory.MakeButton(levelBg.transform, "BackButton", "返回", new Vector2(0.5f, 0.10f),
            new Vector2(280f, 70f), Slate, 32, Color.white);
        levelBg.gameObject.SetActive(false);

        var menu = canvas.gameObject.AddComponent<MainMenu>();
        menu.mainPanel = mainBg.gameObject;
        menu.levelPanel = levelBg.gameObject;
        menu.levelLabels = levelTexts;

        UnityEventTools.AddPersistentListener(startBtn.onClick, menu.OpenLevelSelect);
        UnityEventTools.AddPersistentListener(quitBtn.onClick, menu.QuitGame);
        UnityEventTools.AddPersistentListener(backBtn.onClick, menu.BackToMain);
        for (int i = 0; i < levelButtons.Length; i++)
            UnityEventTools.AddIntPersistentListener(levelButtons[i].onClick, menu.PlayLevel, i);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, MenuScenePath);
    }

    // ---------------------------------------------------------------- level ui

    static void BuildCompleteUI(int index)
    {
        Canvas canvas = UIFactory.CreateCanvas("UICanvas");

        var overlay = UIFactory.FullScreen(canvas.transform, "CompletePanel", new Color(0f, 0f, 0f, 0.72f));
        var card = UIFactory.Box(overlay.transform, "Card", new Vector2(0.5f, 0.5f), new Vector2(900f, 520f), Card);

        var title = UIFactory.Label(card.transform, "Title", "第 " + (index + 1) + " 关 完成", 64, Color.white,
            TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(820f, 120f), new Vector2(0f, 130f));
        var hint = UIFactory.Label(card.transform, "Hint", "", 32, Muted,
            TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(820f, 60f), new Vector2(0f, 48f));

        var retryBtn = UIFactory.MakeButton(card.transform, "RetryButton", "重试", new Vector2(0.5f, 0.5f),
            new Vector2(240f, 84f), Slate, 32, Color.white);
        ((RectTransform)retryBtn.transform).anchoredPosition = new Vector2(-270f, -110f);

        var levelsBtn = UIFactory.MakeButton(card.transform, "LevelsButton", "选关", new Vector2(0.5f, 0.5f),
            new Vector2(240f, 84f), Slate, 32, Color.white);
        ((RectTransform)levelsBtn.transform).anchoredPosition = new Vector2(0f, -110f);

        var nextBtn = UIFactory.MakeButton(card.transform, "NextButton", "下一关", new Vector2(0.5f, 0.5f),
            new Vector2(240f, 84f), Accent, 32, DarkText);
        ((RectTransform)nextBtn.transform).anchoredPosition = new Vector2(270f, -110f);

        var complete = canvas.gameObject.AddComponent<LevelCompleteUI>();
        complete.panel = overlay.gameObject;
        complete.titleText = title;
        complete.hintText = hint;
        complete.retryButton = retryBtn;
        complete.levelsButton = levelsBtn;
        complete.nextButton = nextBtn;

        UnityEventTools.AddPersistentListener(retryBtn.onClick, complete.Retry);
        UnityEventTools.AddPersistentListener(levelsBtn.onClick, complete.Levels);
        UnityEventTools.AddPersistentListener(nextBtn.onClick, complete.Next);

        overlay.gameObject.SetActive(false);
    }

    // ---------------------------------------------------------------- player

    static void BuildPlayer(Sprite sprite, Material material, Vector2 position)
    {
        var go = new GameObject("Stickman");
        go.transform.position = new Vector3(position.x, position.y, 0f);

        var body = go.AddComponent<Rigidbody2D>();
        body.gravityScale = 3.1f;
        body.freezeRotation = true;
        body.mass = 1.2f;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // 1 cell wide, 2 cells tall
        var col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.9f, 1.9f);
        col.offset = Vector2.zero;

        var controller = go.AddComponent<PlayerController>();
        controller.blockSprite = sprite;
        controller.blockMaterial = material;

        var rig = go.AddComponent<StickmanRig>();
        rig.player = controller;
        rig.feetY = -0.95f;
        rig.hipY = -0.35f;
        rig.shoulderY = 0.45f;
        rig.headY = 0.75f;
        rig.headRadius = 0.19f;
        rig.lineWidth = 0.07f;
    }
}
