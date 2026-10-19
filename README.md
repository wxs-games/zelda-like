# ZeldaLike · 2D 俯视角动作 RPG

一款基于 Unity 的类塞尔达 2D 动作 RPG：探索地图、近战/远程战斗、升级点天赋、拾取道具、与商人交易、接取任务。玩法逻辑全部手写 C#，未使用第三方游戏框架；现已支持 **Android 触屏操作**并出包。

## 下载

最新 Android 安装包见 [Releases](https://github.com/wxs-games/zelda-like/releases)：

| 版本 | 文件 | 大小 |
|---|---|---|
| v1.0.0（versionCode 20） | `ZeldaLike_v1.0.0_release.apk` | 40.3 MB |

> 小米 / MIUI 若提示 `INSTALL_FAILED_USER_RESTRICTED`，改用
> `adb push app.apk /data/local/tmp/app.apk` + `adb shell pm install -r -t /data/local/tmp/app.apk`。

## 技术栈

| 项 | 值 |
|---|---|
| 引擎 | Unity **2021.3.45f2c1**（中国版） |
| 渲染 | 2D（Sprite / Tilemap / Animator），ColorSpace Linear |
| 输入 | **Input System 1.4.4**（PC 键鼠 + 实体手柄 + 触屏三路统一到动作门面） |
| 相机 | Cinemachine 2.10.3 |
| UI | uGUI + TextMeshPro 3.0.6 |
| 脚本后端 | IL2CPP / Release（Android，ARMv7 + ARM64） |
| 图形 API | Vulkan |
| 版本控制 | Git + Git LFS（图片资源） |

## 快速开始

1. 安装 [Git LFS](https://git-lfs.com)，再 `git clone` 本仓库（直接下 ZIP 会拿到 LFS 指针而不是图片）；
2. 用 Unity Hub 以 **2021.3.45f2c1** 打开工程，等待 Library 首次导入；
3. 打开 `Assets/Scenes/SampleScene.unity`，点 **▶ Play**。

> 仓库只跟踪 `Assets/`、`Packages/`、`ProjectSettings/` 与根目录文件。
> `Docs/`（设计文档、规格）与 `Tools/`（美术/触屏/出包工具包）属于本地资产，**不随仓库分发**，
> 克隆后若需要出包工具，需另行获取。

## 操作

### PC

| 按键 | 功能 |
|---|---|
| `WASD` / 方向键 | 移动（八方向，自动左右翻转） |
| `Left Shift`（按住） | 奔跑 |
| `K` | 近战挥砍（冷却 1s，命中击退 + 硬直） |
| `J` | 弓箭射击（以最后移动方向瞄准，八方向） |
| `Q` | 剑 / 弓切换 |
| `F` | 交互（对话、商店） |
| `1` / `2` / `L` | 属性面板 / 天赋树 / 任务日志 |
| `Esc` | 取消（关闭商店等） |
| 鼠标左键 / 右键（背包格） | 使用 / 丢弃（商店打开时左键为半价出售） |

### Android 触屏

| 控件 | 功能 |
|---|---|
| 虚拟摇杆 | 移动（持续值，松开即归零） |
| 攻击键 | 按当前武器分派：剑 → 挥砍，弓 → 射击 |
| 交互键 | 对话 / 商店 / 拾取交互 |
| 菜单中枢 | 属性、天赋、任务日志、存档 |

触屏按钮走 `Button.onClick` 直连业务，**不经过虚拟手柄设备**——`OnScreenStick` 与 `OnScreenButton` 共用同一个虚拟 Gamepad，摇杆每帧写 `leftStick` 会冲刷掉其它按钮的边沿，表现为「推着摇杆时按钮全部失灵」。

## 已实现系统

- **战斗**：近战圆形范围判定（动画事件触发）、八方向箭矢、击退与硬直、双武器切换
- **敌人 AI**：Idle → Chase → Attack 三态，侦测圈、攻击冷却、受击硬直
- **经验 / 等级 / 技能树**：击杀得经验、升级得点数、前置技能满级才解锁后续节点
- **背包 / 道具 / 商店**：SO 数据资产、堆叠、消耗品与临时 Buff、三页签商店、半价回收
- **任务系统**：四态流转（未接 / 进行中 / 待交付 / 已交付）、击杀与收集目标、任务日志、接取/完成/交付横幅
- **NPC 对话**：条件/动作 ID 注册表，对话选项可门控任务接取与交付
- **存档**：单 JSON 存档（属性、背包、金币、经验、技能、任务、玩家位置与场景），手动存/读 + 退出与切后台自动存
- **音频**：程序化合成 12 个音效 + 1 首无缝循环 BGM
- **2.5D 地形高差**：进入山体切换碰撞器与 `sortingOrder`，实现遮挡与穿行
- **移动端**：虚拟摇杆、安全区适配、切后台暂停与自动存档、60fps 上限、禁止熄屏

## 技术架构

### 1. 泛型状态机
`StateMachine<T> / IState<T>` 同时驱动玩家与敌人两套状态集，切换时无堆分配。
- [StateMachine.cs](Assets/Scripts/StateMachine/StateMachine.cs)
- 玩家状态：`Assets/Scripts/Player/States/`
- 敌人状态：`Assets/Scripts/Enemy/States/`

### 2. 输入动作门面
游戏逻辑只读「动作」（Move / AttackPressed / InteractPressed…），不直接读物理按键；PC、手柄、触屏各自绑定到同一套动作，加端不改玩法代码。
- [GameInput.cs](Assets/Scripts/Core/GameInput.cs)、[TouchInputBridge.cs](Assets/Scripts/UI/TouchInputBridge.cs)

### 3. 数据驱动（ScriptableObject）
道具、技能、任务、对话节点均为资产；新增内容不改业务代码。
- [ItemSO.cs](Assets/Scripts/Inventory%20%26%20Shop/ItemSO.cs)、[SkillSO.cs](Assets/Scripts/SkillTree/SkillSO.cs)、[QuestSO.cs](Assets/Scripts/Quest/QuestSO.cs)

### 4. 存档与资产数据库
存档只写字符串 ID，读档靠 `GameDatabase` 反查回 SO；跨场景读档等 `sceneLoaded` 再落地。
- [SaveSystem.cs](Assets/Scripts/Core/SaveSystem.cs)、[GameDatabase.cs](Assets/Scripts/Core/GameDatabase.cs)

### 5. 事件解耦
击杀→经验、升级→技能点、拾取→背包、任务状态→UI，各系统间零直接引用，订阅在 `OnEnable/OnDisable` 成对注销。

### 6. 动画事件驱动战斗帧
伤害判定、箭矢生成、动作结束回 Idle 均挂在动画帧上，程序节奏与美术同步。

## 目录结构

```
Assets/
├── Scripts/
│   ├── Core/            # 输入门面、存档、资产数据库、存档数据
│   ├── StateMachine/    # 泛型状态机框架
│   ├── Player/          # 控制器、战斗、弓、生命、经验、各状态
│   ├── Enemy/           # AI、生命、移动、侦测、各状态
│   ├── Inventory & Shop/# 道具 SO、掉落、背包、商店
│   ├── SkillTree/       # 技能 SO、槽位、天赋树、效果分发
│   ├── Quest/           # 任务 SO、管理器、日志与横幅 UI
│   ├── NPC/             # 对话 SO、管理器、交互
│   ├── Audio/           # 音效与 BGM 播放
│   ├── UI/              # 触屏摇杆、按钮、菜单中枢、安全区
│   ├── Editor/          # 出包与工程配置（仅编辑器）
│   ├── MobileRuntime.cs # 切后台/帧率/休眠
│   └── StatsManager.cs  # 全局数值
├── Input/               # UI 触摸动作资产
├── Scenes/              # SampleScene / ForestArea
├── Art/ Audio/ Prefabs/ ScriptableObjects/ Settings/
└── ...
```

## 构建 Android 包

1. 安装 Unity 2021.3.45f2c1 的 Android Build Support（SDK / NDK / JDK）；
2. 菜单 **Tools → Setup → Configure Mobile** 固化包名、架构、方向、图形 API 与输入处理（`activeInputHandler` 必须是 `1`，改完需重启编辑器）；
3. 菜单 **Build → 出包 → Android APK**（自动递增 versionCode，产物在工程根 `Builds/`）。

> 出包脚本与踩坑手册在 `Tools/android-ship-kit/`，不随仓库分发。
> 关键硬性卡点：`Active Input Handling = Both` 会被 Android 直接拒绝；运行时代码引用 `UnityEditor` 会 CS0234；
> 双图形 API 会让 ShaderCompiler 死锁。

## 已知限制

- `ForestArea` 场景已存在，但 Build Settings 目前只启用了 `SampleScene`
- 仅单存档位，无多周目 / 云存档
- 上架前需配置正式签名（当前构建未接入发布流程）
- 无多语言支持（文本为中文硬编码）
