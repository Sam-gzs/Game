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

## 文档

| 文档 | 内容 |
|---|---|
| `Docs/project-structure.md` | 逐文件作用、文件引用链、可删清单 |
| `Docs/git-workflow.md` | 程序 × 策划的协作与版本管理规程 |
| `Docs/jam-review-2026-11-04.md` | 完成度评审、美术音效缺口、一周排期 |
