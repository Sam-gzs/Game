# 项目结构详解 — `E:/unity/jam`

> Unity 6000.6.7f1 · 代码 5 171 行 · 场景 12 张 · 入库 949 个文件
> 本文覆盖**你自己写的每一个文件**；第三方包和 Unity 自动生成的目录只做归纳。

---

## 0. 先分清四类文件

| 类别 | 例子 | 要不要管 |
|---|---|---|
| **你写的**（Assets 下的 .cs / .unity / .prefab / 图） | 关卡、机关代码 | ✅ 全部要懂 |
| **Unity 自动生成、但要入库** | 所有 `.meta`、`ProjectSettings/*.asset` | ⚠️ 不能删、不能手改 |
| **Unity 自动生成、不入库** | `Library/`、`Temp/`、`Logs/`、`*.csproj` | ❌ 删了会自动重建 |
| **模板自带的**（2D URP 新建工程附赠） | `Assets/Welcome/`、`Assets/Settings/` | 🔸 与玩法无关 |

---

## 1. 顶层

```
E:/unity/jam/
├── Assets/                 ← 99% 的活儿都在这儿
├── ProjectSettings/        ← 工程全局设置（30 个 .asset）
├── Packages/               ← 包依赖清单 + 内嵌的 MCP 插件
├── Docs/                   ← 我写的评审与协作文档
├── .gitignore              ← 排除 Library/Temp/Logs/Builds 等生成物
├── .gitattributes          ← 让 .unity 走 UnityYAMLMerge，图片标 binary
├── .vsconfig               ← 告诉 VS 要装哪些 Unity 工作负载（自动生成）
│
├── Library/  Temp/  Logs/  obj/  UserSettings/     ← 生成物，不入库，可随便删
├── Builds/                                          ← 打包输出（暂不存在）
└── *.csproj  jam.slnx                               ← IDE 工程文件，自动生成
```

---

## 2. `Assets/` 全树

```
Assets/
├── Scripts/                        ← 全部游戏逻辑（23 个 .cs，5 171 行含 Editor）
│   ├── PlayerController.cs          665 行  玩家：移动/跳跃/蓄力投球/瞄准线/暂停保护
│   ├── CameraFollow.cs               57 行  侧滚相机，按住 Q 跟随球
│   ├── GameFlow.cs                   64 行  静态类：关卡数量、进度存档、场景跳转
│   ├── HudText.cs                   115 行  IMGUI 左上角操作提示（按关卡显示不同文案）
│   ├── StickmanRig.cs               198 行  角色表现：idle + 6 帧走路，跳跃复用走路帧
│   │
│   ├── Grid/                       ← 网格与机关（核心玩法）
│   │   ├── GridTile.cs            1 014 行  ★ 所有机关都在这里，TileType 共 23 种
│   │   ├── BoxProjectile.cs         651 行  ★ 球：格子步进、动量、折射、黏附、破碎
│   │   ├── GateSystem.cs             72 行  通道信号：开关奇偶 / 压力板 OR / 闸门镜面响应
│   │   ├── LevelBootstrap.cs         52 行  关卡开场：放角色到出生点、设相机边界、销毁出生标记
│   │   ├── RuntimeLevel.cs           96 行  ASCII 地图运行时生成关卡 —— ⚠️ 目前没有任何关卡使用
│   │   └── GridUtil.cs               22 行  格子↔世界坐标换算
│   │
│   └── UI/
│       ├── UIFactory.cs             130 行  代码生成 uGUI 的工具（按钮/文本/画布）
│       ├── MainMenu.cs              143 行  主菜单：选关按钮、设置按钮都在运行时生成
│       ├── SettingsPanel.cs         447 行  ★ 全局设置浮层：音量、ESC 暂停、重开、返回菜单
│       ├── GameSettings.cs           48 行  音量读写（PlayerPrefs）→ AudioListener.volume
│       ├── LevelCompleteUI.cs        77 行  "LEVEL N CLEARED" 面板：重试/选关/下一关
│       └── ToastUI.cs                45 行  底部一句话提示（"NO ROOM HERE" 之类）
│
├── Scenes/                         ← 12 张场景
│   ├── MainMenu.unity              82 KB  主菜单（构建列表 index 0）
│   ├── Level1.unity               579 KB  15 130 行 ─┐
│   ├── Level2.unity               836 KB  21 399 行  │
│   ├── Level3.unity               676 KB  17 493 行  ├ 六张手工长关，43~98 格宽横向滚动
│   ├── Level4.unity               572 KB  14 949 行  │
│   ├── Level5.unity               602 KB  15 872 行  │
│   ├── Level6.unity               812 KB  20 936 行 ─┘
│   ├── Level7~10.unity        179~198 KB  单屏 19×10 房间（另一套范式，疑似占位）
│   └── SampleScene.unity           9 KB  模板空场景，只有相机+灯，**不是游戏内容**
│
├── Prefabs/                        ← 9 个预制体，实际只有 2 个在用
│   ├── GridTile.prefab             4 KB  ★ 唯一真正在用：所有方块都是它的实例 + type 覆盖
│   ├── Switch.prefab               3 KB  仅 Level3 残留一处引用
│   └── BreakableDirt / ExtraBoxPickup / FallingBlock / Gate /
│       LevelDoor / RefractDown / RefractUp         ← 7 个孤儿，0 张场景引用
│
├── Resources/                      ← 运行时按名字 Resources.Load 的素材（42 个文件）
│   ├── PlayerIdle.png             143 KB  角色站立图 360×600（唯一的正式角色美术）
│   ├── Walk_0~5.png           53~62 KB×6  走路 6 帧 242×490（从 mp4 抽帧）
│   ├── Ball.png                     0 KB  球：16×16 浅黄圆点（占位级）
│   ├── Door / Gate / Jelly / Pickup / Refract90 / RefractDown / RefractUp /
│   │   Slider / Sticky / SwitchOn / SwitchOff.png
│   │                                 0 KB  11 张 16×16 机关图，全是几百字节的占位图
│   ├── BillingMode.json             0 KB  IAP 模板残留，未使用
│   ├── a57c7b19….mp4               14 KB  抽走路帧用的原始视频，可删
│   └── 微信图片_2026110219xxxx.jpg ×3   0 KB  参考图/截图，未参与游戏
│
├── Editor/                         ← 只在编辑器里跑的工具（打包时不进游戏）
│   ├── StickmanGameBuilder.cs     630 行  ★ 一键建关（ASCII map→场景）——⚠️ 会覆盖 Level1~3
│   ├── GridPainterWindow.cs       287 行  ★ Tools > Grid Painter 刷格子窗口
│   ├── GridSceneUtil.cs           138 行  预制体/材质/精灵的创建与查找
│   ├── GridSnapKeeper.cs           58 行  拖动方块自动吸附到格心
│   ├── GameBuild.cs               127 行  Build 菜单：出 Windows / WebGL zip
│   └── AutoBuildLevels1to6.cs      35 行  自动打包触发器，路径还指向 D:\unity_project，已失效
│
├── Settings/                       ← URP 2D 渲染管线配置（模板自带，基本不用动）
│   ├── UniversalRP.asset            4 KB  管线资产
│   ├── Renderer2D.asset             3 KB  2D 渲染器
│   ├── DefaultVolumeProfile.asset  18 KB  后期处理默认值（当前没启用任何效果）
│   ├── InputSystem_Actions.inputactions  40 KB  默认输入配置
│   ├── Lit2DSceneTemplate.scenetemplate  3.8 MB  新建场景模板（最大的文件，可忽略）
│   └── Scenes/URP2DSceneTemplate.unity    9 KB  同上
│
├── Art/
│   ├── WhiteSquare.png              0 KB  ★ 8×8 纯白方块——所有墙/砖/土/石都是它染色的
│   └── SpriteUnlit.mat              1 KB  Sprite-Unlit 材质，方块共用
│
├── Welcome/                        ← 模板的欢迎页，与游戏无关（可整目录删）
├── _Recovery/0.unity               ← Unity 崩溃恢复的临时场景，已 gitignore
└── Screenshots/                    ← MCP 截图输出，已 gitignore（可删）
```

---

## 3. `ProjectSettings/` — 只讲你可能会碰的

| 文件 | 作用 | 谁动 |
|---|---|---|
| `ProjectSettings.asset` | 20 KB，产品名 / 分辨率 / 图标 / 打包设置（**现在还是 "My project"**） | 程序 |
| `EditorBuildSettings.asset` | **11 个场景的构建列表**，决定菜单显示几关（`GameFlow` 按它算关卡数） | 程序 |
| `InputManager.asset` | 9 KB，传统输入轴（本项目用新 Input System，基本不用） | 别动 |
| `TagManager.asset` | Tag 与 Layer | 程序 |
| `Physics2DSettings.asset` | 2D 物理：重力、层碰撞矩阵 | 程序 |
| `QualitySettings.asset` | 画质档位、垂直同步 | 程序 |
| `TimeManager.asset` | 固定时间步长 | 别动 |
| `AudioManager.asset` | 全局音频设置（DSP 缓冲等），**不是音量面板** | 别动 |
| `ProjectVersion.txt` | 记录 Unity 版本 `6000.6.7f1` —— 换版本会改它，队友必须同版本 | 别手改 |
| `Packages/*/Settings.json` | AI Assistant / 教程框架的局部设置 | 别动 |
| 其余（XRSettings、NavMeshAreas、VFXManager…） | 模板默认值 | 别动 |

---

## 4. `Packages/`

```
Packages/
├── manifest.json          2.6 KB  依赖清单（URP、Input System、2D 全家桶…37 项）
├── packages-lock.json    19 KB    锁定版本与实际来源，**必须入库**
└── com.coplaydev.unity-mcp/     736 个文件 5.1 MB  内嵌的 MCP 插件（让我能操作 Unity）
                                 第三方代码，不要改；删了 MCP 就断
```

---

## 5. 一关是怎么跑起来的（文件之间的引用链）

```
MainMenu.unity
   └─ MenuCanvas ─ MainMenu.cs ──点击──> GameFlow.LoadLevel(n)
                                          └─ SceneManager.LoadScene("Level5")

Level5.unity
   ├─ Main Camera      → CameraFollow.cs     跟玩家
   ├─ Level           → LevelBootstrap.cs    放玩家、算相机边界
   ├─ Stickman         → PlayerController.cs + StickmanRig.cs
   │                        └─ 左键 → BoxProjectile.cs（运行时 new 出来的对象）
   ├─ 200 多个方块实例 → 全部是 GridTile.prefab 的实例
   │                        └─ 每个实例只改 type / cell / channel 三个字段
   │                        └─ GridTile.cs 按 type 换 sprite、换碰撞体、换行为
   ├─ UICanvas         → LevelCompleteUI.cs / ToastUI.cs / HudText.cs
   └─ (第一次按 ESC)   → SettingsPanel.cs 自己 DontDestroyOnLoad 建出来
```

**关键理解**：关卡里那 200 多个"方块"不是 200 个不同的东西，而是**同一个预制体的 200 个实例**，区别只在 inspector 里的 `type` 数字（0=Solid、5=Dirt、17=Sticky…）。所以改 `GridTile.cs` 等于同时改所有关卡的所有方块——这也是为什么它是全项目最危险的文件。

---

## 6. "这个能删吗"清单

| 文件 | 结论 |
|---|---|
| `Assets/Scenes/SampleScene.unity` | ✅ 可删，模板空场景 |
| `Assets/Welcome/` 整个目录 | ✅ 可删，2D 模板欢迎页 |
| `Assets/Resources/*.mp4`、`微信图片*.jpg` | ✅ 可删，抽帧素材与参考图 |
| `Assets/Resources/BillingMode.json` | ✅ 可删，IAP 残留 |
| `Assets/Prefabs/` 里那 7 个孤儿预制体 | ⚠️ 可删，但先确认 `StickmanGameBuilder` 不会再生成 |
| `Assets/Editor/AutoBuildLevels1to6.cs` | ⚠️ 建议删，路径失效且会在启动时检查 D 盘文件 |
| `Assets/Settings/Lit2DSceneTemplate.scenetemplate` | ⚠️ 3.8 MB，删了新建场景会退回默认模板 |
| `Assets/_Recovery/` | ✅ 可删，崩溃恢复残留 |
| `Assets/Screenshots/` | ✅ 可删，MCP 截图 |
| **任何 `.meta` 文件** | ❌ **绝对不能删**，删了引用全断 |
| `ProjectSettings/ProjectVersion.txt` | ❌ 不能手改，队友必须同一 Unity 版本 |

---

## 7. 三个值得注意的结论

1. **`GridTile.prefab` 是唯一在用的预制体**。另外 8 个（Gate / Switch / LevelDoor / RefractUp/Down / FallingBlock / BreakableDirt / ExtraBoxPickup）是旧工作流遗留，0~1 张场景引用——想清理可以先删它们。
2. **`RuntimeLevel.cs` 是睡着的宝藏**。它能用几十行 ASCII 生成整关，但 12 张场景一张都没挂。如果哪天要把关卡从 `.unity` 里抽出来做成可合并的文本（见 `git-workflow.md` 第 9 节），这就是起点。
3. **`Assets/Settings/` 占了仓库 27% 的体积**，其中 `Lit2DSceneTemplate.scenetemplate` 一个文件就 3.8 MB，而它只是"新建场景时用哪个模板"，和游戏运行完全无关。
