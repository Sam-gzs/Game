# 协作与版本管理规程（程序 × 策划，2 人）

> 适用：`E:/unity/jam`（Unity 6000.6.7f1，git 仓库已初始化，分支 `main`）
> 目标：两个人在一周内并行推进代码与关卡设计，**不互相踩踏、不丢工作**。

---

## 0. 先看清核心矛盾

| 文件 | 行数 | 可合并性 |
|---|---:|---|
| `Level2.unity` | **21 399** | ❌ 基本不可合并 |
| `Level6.unity` | 20 936 | ❌ |
| `Level3.unity` | 17 493 | ❌ |
| `Level1/4/5.unity` | 14 900 ~ 15 900 | ❌ |
| `MainMenu.unity` | 2 943 | ⚠️ 勉强，别两个人同时动 |
| `Scripts/*.cs` | 单文件 50~1000 行 | ✅ 正常合并 |

**Unity 场景是 YAML，但内部全是 `fileID` / `guid` 引用。两个人各加一个方块，git 会把同一段文本改得面目全非，手工合并等于重做。**

所以真正的问题不是"怎么配 git"，而是：**同一时刻不能有两个人改同一个 `.unity`。**

有两条路，任选一条：

- **路线 A（本周就用）**：场景**单人独占**，靠约定和纪律避免冲突。零成本。
- **路线 B（要做长线才值）**：把关卡地图从场景里抽成文本文件。彻底解决，但要半天到一天的改造。

> **建议：本周走 A，同时把 B 记进下一轮迭代的待办。**

---

## 1. 所有权表（最重要的一张表）

先立规矩，比任何工具都管用。**默认只有所有者能改，别人要改先提需求。**

| 路径 | 所有者 | 说明 |
|---|---|---|
| `Assets/Scripts/**` | **程序** | 策划不碰代码 |
| `Assets/Editor/**` | **程序** | Grid Painter、关卡生成器 |
| `Assets/Scenes/Level1~10.unity` | **策划** | 程序只读；要调机关参数就口头提 |
| `Assets/Scenes/MainMenu.unity` | **程序** | 菜单是代码生成的（`MainMenu.cs` 运行时插按钮），策划改容易白改 |
| `ProjectSettings/**` | **程序** | 见下方"高频冲突文件" |
| `Assets/Resources/**`、`Assets/Art/**` | 美术 / 策划 | 二进制资源，谁加谁提 |

**"同一时刻只有一个人改某张场景"** —— 最简单的执行方式：

> 改之前在工作群发一句「**我改 Level3，预计 40 分钟**」，改完立刻提交。
> 谁看到这句话，就别在这段时间动 Level3。

这条约定能挡掉 90% 的场景冲突，成本是零。

---

## 2. 分支策略：trunk-based，短命分支

**不要 gitflow。** 一周的 jam 用不上 `develop` / `release` / `hotfix` 那一套，只会增加合并次数。

### 规矩

1. `main` **永远是能跑的版本**（随时可以打一个包发出去）。
2. 每项工作开一个短分支，**半天内合回**：
   ```bash
   git switch -c feat/level3-tutorial
   # ……改……
   git add Assets/Scenes/Level3.unity
   git commit -m "Level3：补折射镜与开关的教学提示"
   git switch main
   git pull --rebase
   git merge --no-ff feat/level3-tutorial
   git push
   git branch -d feat/level3-tutorial
   ```
3. 分支活过 24 小时就是在给自己挖坑——超过一天还没合，说明切得太大了。

### 为什么不是"直接都在 main 上"

也可以，但前提是**改动足够小、提交足够频繁**。两个人各改各的文件时，直接 main 反而更快。判断标准：

- 改的是**自己的文件** → 直接 main
- 改的是**共用文件**（场景、`GridTile.cs`、`ProjectSettings`）→ 开分支，改完立刻合

---

## 3. 每日节奏

| 时机 | 动作 |
|---|---|
| 开工 | `git pull --rebase` |
| 改完一个小功能 / 一张关卡的一小段 | `git commit`（小步，别攒一天） |
| 能跑通（进 Unity 按 Play 没报错） | `git push` |
| **收工前** | 必须 `git push`，不留未提交的活在自己机器上 |

**"能跑才推"**：push 之前进 Unity 按一次 Play，控制台看有没有红色报错。推一个跑不起来的版本，等于让另一个人卡住。

---

## 4. 提交纪律

1. **一次提交只做一件事**。别把"改了 Level3 + 修了球的碰撞 + 加了音效"塞进一个 commit。
2. **场景和脚本分开提交**：这样出问题时能只回退其中一半。
   ```bash
   git add Assets/Scripts/Grid/BoxProjectile.cs && git commit -m "球：折射后动量不足改为停下掉落，不再碎裂"
   git add Assets/Scenes/Level4.unity && git commit -m "Level4：调整折射镜旁的石块位置"
   ```
3. **提交信息写"改了什么 + 为什么"**，不写"update"、"fix"。
4. **`.meta` 必须跟着资源一起提交**。漏掉 meta，别人拉下来就是一堆丢失引用（粉红方块）。
   ```bash
   git status --short | grep -v "\.meta"   # 看看有没有资源没带 meta
   ```

---

## 5. 高频冲突文件（谁都不许乱动）

| 文件 | 冲突原因 | 规矩 |
|---|---|---|
| `ProjectSettings/EditorBuildSettings.asset` | 加减场景 | **只有程序改**；加新关卡先告诉程序 |
| `ProjectSettings/ProjectSettings.asset` | 谁都可能点开 Player Settings | 只有程序改 |
| `ProjectSettings/InputManager.asset` 与 `Assets/Settings/InputSystem_Actions.inputactions` | 改按键 | 只有程序改，改完 @所有人 |
| `Assets/Scripts/Grid/GridTile.cs` | 1010 行，所有机关都在这 | 程序内部也要**先说一声**再动 |
| `MainMenu.unity` | 菜单 UI | 程序独占（内容是代码生成的） |

---

## 6. 冲突了怎么办（决策树）

```
git pull 报冲突
├─ 冲突文件是 .cs
│   └─ 正常手工解决（打开文件改 <<<<<<< 段），改完 build 通过再提交
└─ 冲突文件是 .unity / .prefab
    ├─ 冲突很小（两边改的不是同一块）
    │   └─ 【可选】用 Unity SmartMerge 试一次（见第 7 节），合完必须进 Unity 验证
    └─ 否则 → 不要手工合并
        └─ 选一边，另一边重做那一小段：
           git checkout --ours  Assets/Scenes/Level3.unity   # 保自己的
           git checkout --theirs Assets/Scenes/Level3.unity   # 保对方的
           git add Assets/Scenes/Level3.unity
           git commit
```

**核心心态**：场景冲突的正确处理不是"合"，而是"**谁的改动小，谁重做**"。重做 10 分钟的关卡调整，比修一个合并坏的场景快得多。

---

## 7. （可选）Unity SmartMerge

Unity 自带一个能理解 YAML 结构的合并工具，对**小范围、不重叠**的场景改动有时能正确合并。

本机路径（已确认存在）：

```
E:/unity/unity-version/6000.6.7f1/Editor/Data/Tools/UnityYAMLMerge.exe
```

**每台机器各配一次**（路径按自己安装位置改）：

```bash
cd E:/unity/jam

# 换行符策略必须一致，否则每次拉取都会显示"整个文件被修改"
git config core.autocrlf true

git config merge.unityyamlmerge.name "Unity SmartMerge"
git config merge.unityyamlmerge.driver "'E:/unity/unity-version/6000.6.7f1/Editor/Data/Tools/UnityYAMLMerge.exe' merge -p %O %A %B %L %P"
```

仓库里已经有 `.gitattributes` 把 `*.unity` / `*.prefab` / `*.asset` 指到这个驱动。

> ⚠️ **SmartMerge 不是万能**，它只是"比你手改强一点"。合并完**必须**打开 Unity：
> 1. 看 Hierarchy 有没有重复对象、丢脚本（Missing Script）
> 2. 进 Play 跑一遍
> 3. 不对就 `git checkout` 回滚重做
>
> 宁可重做，也不要相信一个你看不懂的自动合并结果。

---

## 8. 里程碑与回滚

```bash
# 每天收工打一个标记，出问题能回到"昨天还能跑"的状态
git tag d1-可玩
git tag d2-有音效
git push --tags

# 回滚到某个标签
git switch -c hotfix/rollback d1-可玩
```

**发布包另外存**：`Builds/` 已在 `.gitignore` 里（构建产物不入库），出包后把 zip 单独传到网盘/itch，不要塞进 git。

---

## 9. 路线 B：把地图外置成文本（下一轮迭代再做）

### 现状：基础设施已经有了，只是没用

项目里 `Assets/Scripts/Grid/RuntimeLevel.cs` 已经能**从一行 ASCII 地图在运行时生成整关**：

```
"####################|
 #..................#|
 #.P.......D....E...#|
 ####################"
```

字符表：`#` 墙、`S` 石头、`D` 土、`P` 出生点、`E` 门、`F` 落块、`B` 弹块、`X` 加球、`R`/`T` 折射镜、`K/L/N` 开关(ch1/2/3)、`H/G/J` 闸门(ch1/2/3)、`M/Z/m/z` 反射镜、`O/o/Q` 压力板。

**但目前 10 张关卡没有任何一张挂这个组件**——全是手工摆的 Prefab 实例。

### 这么做的好处

- 关卡变成 **`Assets/Levels/Level3.map`，几十行纯文本**，`git diff` 一目了然，行级合并几乎不会冲突
- 策划改地图完全不碰场景 → 程序随便改代码，互不干扰
- 关卡可以 code review、可以在聊天里贴出来讨论

### 需要补的东西

1. 扩展字符表：现在缺 `Wood` / `Sticky` / `Jelly` / `Slider` / `BallSwitch` / 教学触发器（`TriggerPlayer` / `TextAnchor`）——这几样只能靠 Inspector 配，得设计一套语法（例如 `Y`+通道号 表示第几号通道的黏块）。
2. 一个编辑器工具：**把现有场景导出成 map 文本**（读所有 GridTile 的 cell/type/channel 反序列化），否则 6 张老关卡要重画。
3. 每关场景只留 `RuntimeLevel` + 相机 + UI + 玩家，地图在 `Awake` 时生成。

**成本估计：导出工具 3~4 小时，字符表扩展 2 小时，6 关迁移与逐关验证 4 小时。** 一周的 jam 里塞不进，但如果是"jam 之后继续做"，这是第一优先的重构。

---

## 10. 一页速查

```bash
# 开工
git pull --rebase

# 干活（改自己的文件直接 main；改共用文件先开分支）
git switch -c feat/xxx

# 提交（小步、分开场景与脚本）
git add <具体文件>
git commit -m "改了什么 + 为什么"

# 合并前先对齐
git switch main && git pull --rebase
git merge --no-ff feat/xxx

# 收工
git push
git tag dN-描述 && git push --tags
```

**四条铁律**

1. **同一时刻只有一个人改某张场景**，改前群里说一句。
2. **场景冲突不要手merge**，谁的改动小谁重做。
3. **`main` 永远能跑**，推之前进 Unity 按一次 Play。
4. **资源和它的 `.meta` 一起提交**，构建产物一律不进库。
