# Stickman Bounce

> Game Jam 作品 · 关键词：**碰撞**

## 操作

| 按键 | 作用 |
|---|---|
| `A` / `D` 或 `←` / `→` | 移动 |
| `W` / `空格` | 跳跃 |
| 按住鼠标左键 | 蓄力（1~3 级），松开朝光标方向投球 |
| `E` | 收回静止的球 |
| `Q` | 按住跟随球 |
| `R` | 重开本关 |
| `ESC` | 设置面板（音量 / 重开本关 / 返回主菜单） |

## 关卡

`Assets/Scenes/` 下为 `MainMenu` + `Level1~10`。前六关是横向滚动长关（43~98 格宽），
`Level7~10` 为单屏房间，属于另一套关卡范式。

关卡内容存在场景里：每个方块都是 `GridTile.prefab` 的实例，只覆盖 `type` / `cell` / `channel` 三个字段，
23 种机关的行为全部集中在 `Assets/Scripts/Grid/GridTile.cs`。

## 技术栈

- Unity **6000.6.7f1** · URP 2D · 新 Input System
- 网格坐标 1 单位 = 1 格，`GridUtil` 负责换算
- 编辑器工具：`Tools > Grid Painter`（刷格子）、`Tools > Stickman Jump`（建关/造预制体）

## 音频

音频统一走 `Assets/Scripts/AudioManager.cs`：它在启动时自建 `DontDestroyOnLoad` 对象，
场景里不需要挂任何 AudioSource。

- 音效放 `Assets/Resources/Audio/<id>.wav`，文件名与 `Sfx` 枚举一一对应；新增一条只丢文件，不改代码
- 主音量复用设置面板（`GameSettings` → `AudioListener.volume`），音频层不再二次衰减
- BGM：放 `Assets/Resources/Audio/bgm/menu.wav` 与 `bgm/level.wav` 即自动循环
- 尚缺 7 条素材（`break_wood` `mirror_turn` `bounce` `plate_press` `ball_switch_on` `ball_switch_off` `pickup`），
  当前用近义音效变调兜底，替换表见 `AudioManager.BuildDefs()`
- 导入由 `Assets/Editor/AudioImportSettings.cs` 统一设为 PCM / 单声道 / 预加载

## 文档

| 文档 | 内容 |
|---|---|
| `Docs/project-structure.md` | 逐文件作用、文件引用链、可删清单 |
