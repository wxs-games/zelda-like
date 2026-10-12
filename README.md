# 游戏使用说明

> 一款 Unity 2D 俯视角动作 RPG（类塞尔达玩法）Demo。

---

## 一、项目简介

- **引擎版本**：Unity 2021.3.45f2c1（URP/2D，内置渲染管线亦可运行）
- **场景**：仅一个场景 `Assets/Scenes/SampleScene.unity`，打开即玩，无需额外构建
- **玩法循环**：探索地图 → 近战/远程击杀怪物获取经验 → 升级获得技能点 → 点天赋强化角色 → 拾取道具、与商人交易 → 挑战更多怪物


---

## 二、快速运行

1. 用 Unity Hub 以 **2021.3.45f2** 版本打开本工程；
2. 在 Project 窗口双击打开 `Assets/Scenes/SampleScene.unity`；
3. 点击编辑器顶部的 **▶ Play** 按钮即可开始，无需打包。

> 死亡后角色会被隐藏，目前没有复活流程，重新点击 Play 即可再来一局。

---

## 三、3 分钟演示路线（推荐）

| 顺序 | 操作 | 对应展示的系统 |
| --- | --- | --- |
| 1 | `WASD` 移动，按住 `Left Shift` 奔跑 | 玩家移动状态机 / 朝向翻转 |
| 2 | 按 `K` 挥剑攻击火把怪，击杀后观察经验条上涨、升级提示 | 战斗、击退、经验与升级 |
| 3 | 按 `2` 打开天赋树，点击图标消耗技能点加点（游戏会暂停） | 技能树、前置依赖解锁 |
| 4 | 按 `1` 打开属性面板，查看伤害/速度数值变化 | 属性系统、数据与 UI 同步 |
| 5 | 按 `Q` 切换到弓箭，先按方向键瞄准，再按 `J` 射箭 | 双武器切换、8 方向投射物 |
| 6 | 走过地上的蘑菇/牛排自动拾取，鼠标左键点击背包物品使用 | 拾取、背包堆叠、消耗品/临时 Buff |
| 7 | 走到商人身边按 `F` 打开商店，尝试买入与半价卖出 | 商店、金币经济、暂停式 UI |
| 8 | 走到山体入口处，观察角色被山体遮挡、可穿行到高处 | 2D 地形高差（动态碰撞器 + 排序层） |

---

## 四、键盘操作一览

| 按键 | 功能 | 说明 |
| --- | --- | --- |
| `W A S D` / 方向键 | 移动 | 八方向移动，角色自动左右翻转 |
| `Left Shift`（按住） | 奔跑 | 移动速度临时 +3 |
| `K` | 近战挥砍 | 冷却 1 秒；攻击点圆形范围内的首个敌人受伤并被击退、硬直 |
| `J` | 弓箭射击 | 冷却 0.5 秒；以**最后一次移动方向**为瞄准方向（八方向），箭矢命中后插在目标上，2 秒后销毁 |
| `Q` | 切换武器 | 剑 / 弓 互换，通过启用/禁用组件并切换 Animator 动画分层实现 |
| `F` | 交互 | 靠近商人时打开/关闭商店（游戏暂停） |
| `1` | 属性面板 | 查看伤害、速度；打开时暂停角色与敌人的活动 |
| `2` | 天赋树 | 打开/关闭技能树（`Time.timeScale = 0` 全局暂停） |
| 鼠标左键（背包格） | 使用物品 | 商店打开时左键变为**出售**（半价） |
| 鼠标右键（背包格） | 丢弃物品 | 掉落在脚下，移出后可重新拾取 |

---

## 五、游戏系统说明

### 1. 战斗系统
- **近战**：动画进行到特定帧时由动画事件触发伤害判定（`Physics2D.OverlapCircleAll`），命中造成伤害 + 击退 + 眩晕。
- **远程**：箭矢为独立 Prefab，发射时赋予方向与旋转；命中敌人或障碍物会"插"在目标上成为子物体。
- 攻击/射击期间角色速度归零，动作结束通过动画事件回到空闲状态，保证动作手感与状态一致性。

### 2. 敌人 AI
- 三态状态机：**Idle（待机）→ Chasing（追击）→ Attacking（攻击）**。
- 玩家进入侦测圈触发追击并自动转向；进入攻击距离（2 格）后出手，造成 25 点伤害与击退，随后进入 2 秒攻击冷却；玩家离开侦测范围则回到 Idle。
- 被玩家攻击时会被击退且暂停移动。

### 3. 经验 / 等级 / 技能树
- 击杀一只怪物奖励 3 点经验；升级所需经验按 1.2 倍递增（初始 10 点）。
- 每次升级获得 1 点技能点，在天赋树中点击图标消耗。
- 已实现的技能节点：
  - **Max Health Boost**：最大生命 +1，可升 5 级；
  - **Sword Slash**：解锁/启用近战能力，1 级满级；
  - **电闪雷鸣**：3 级节点（节点数据、等级与前置解锁链路完整，战斗效果分发已预留扩展位）。
- 高级技能需要前置技能升满后才会解锁（灰色 Locked → 亮色可点）。

### 4. 背包与道具
- 物品全部为 `ScriptableObject` 数据资产，支持堆叠（默认每格 3 个），背包满时无法拾取。
- 金币为特殊物品，拾取后直接进入金币计数，不占格子。
- 已配置的消耗品：
  - **蘑菇（Mushroom）**：立即回复 5 点生命；
  - **牛排（NiuPai）**：立即回复 25 点生命；
  - **南瓜（Pumpkin）**：3 秒内移动速度 +5，持续时间结束后自动还原（临时 Buff）。

### 5. 商店系统
- 靠近商人按 `F` 打开，游戏暂停（`Time.timeScale = 0`），再次按 `F` 或离开范围关闭。
- 三个页签：**物品 / 武器 / 护甲**；金币不足或背包无空位时无法购买。
- 商店打开期间，左键点击背包中的物品可以半价卖出。

### 6. 地形高差
- 山体设置了入口/出口触发器：进入高处时关闭山体碰撞器、打开边界碰撞器，并抬高角色 `sortingOrder`，实现"走到山前被遮挡、上山后在山前显示"的 2.5D 层次效果，无需 3D 相机。

---

## 六、技术亮点

1. **泛型状态机**：一套 `StateMachine<T> / IState<T>` 同时驱动玩家与敌人两套完全不同的状态集；状态以单例对象存在，无切换时的堆分配。
   - 核心：[StateMachine.cs](file:///e:/desktop/UnityXiangMu/zeldaLike/Assets/Scripts/StateMachine/StateMachine.cs)
   - 玩家状态：[PlayerIdleState.cs](file:///e:/desktop/UnityXiangMu/zeldaLike/Assets/Scripts/Player/States/PlayerIdleState.cs)、[PlayerMoveState.cs](file:///e:/desktop/UnityXiangMu/zeldaLike/Assets/Scripts/Player/States/PlayerMoveState.cs)、[PlayerAttackState.cs](file:///e:/desktop/UnityXiangMu/zeldaLike/Assets/Scripts/Player/States/PlayerAttackState.cs)、[PlayerShootState.cs](file:///e:/desktop/UnityXiangMu/zeldaLike/Assets/Scripts/Player/States/PlayerShootState.cs)
   - 敌人状态：[IdleState.cs](file:///e:/desktop/UnityXiangMu/zeldaLike/Assets/Scripts/Enemy/States/IdleState.cs)、[ChaseState.cs](file:///e:/desktop/UnityXiangMu/zeldaLike/Assets/Scripts/Enemy/States/ChaseState.cs)、[AttackState.cs](file:///e:/desktop/UnityXiangMu/zeldaLike/Assets/Scripts/Enemy/States/AttackState.cs)

2. **数据驱动（ScriptableObject）**：物品属性与技能配置均为资产文件，新增道具/技能无需改业务代码。
   - [ItemSO.cs](file:///e:/desktop/UnityXiangMu/zeldaLike/Assets/Scripts/Inventory%20%26%20Shop/ItemSO.cs)、[SkillSO.cs](file:///e:/desktop/UnityXiangMu/zeldaLike/Assets/Scripts/SkillTree/SkillSO.cs)

3. **事件解耦（C# event / Action）**：击杀→经验、升级→技能点、掉落拾取→背包、商店开关→背包格行为切换，各系统之间零直接引用，订阅均在 `OnEnable/OnDisable` 中成对注销，避免内存泄漏。
   - [EnemyHealth.cs](file:///e:/desktop/UnityXiangMu/zeldaLike/Assets/Scripts/Enemy/EnemyHealth.cs)、[ExpManager.cs](file:///e:/desktop/UnityXiangMu/zeldaLike/Assets/Scripts/Player/ExpManager.cs)、[InventoryManager.cs](file:///e:/desktop/UnityXiangMu/zeldaLike/Assets/Scripts/InventoryManager.cs)

4. **动画事件驱动战斗帧**：伤害判定、箭矢生成、攻击结束回 Idle 都挂在动画帧上，程序与美术节奏天然同步。
   - [PlayerCombat.cs](file:///e:/desktop/UnityXiangMu/zeldaLike/Assets/Scripts/Player/PlayerCombat.cs)、[PlayerBow.cs](file:///e:/desktop/UnityXiangMu/zeldaLike/Assets/Scripts/Player/PlayerBow.cs)

5. **集中式数值管理**：`StatsManager` 单例统一持有伤害、速度、生命等数值，技能、道具、UI 都读写同一数据源，保证面板与实战数值一致。
   - [StatsManager.cs](file:///e:/desktop/UnityXiangMu/zeldaLike/Assets/Scripts/StatsManager.cs)、[StatsUI.cs](file:///e:/desktop/UnityXiangMu/zeldaLike/Assets/Scripts/StatsUI.cs)

6. **健壮性细节**：商店/天赋树暂停时处理了玩家离开范围导致的 `timeScale` 复位问题；致死当帧不再施加击退，避免对已销毁对象取组件。

---

## 七、目录结构速览

```
Assets/Scripts/
├── StateMachine/        # 泛型状态机框架（玩家/敌人共用）
├── Player/              # 玩家控制器、战斗、弓箭、生命、经验、击退、各状态
│   └── States/
├── Enemy/               # 敌人 AI、生命、移动、侦测、击退
│   └── States/
├── Inventory & Shop/    # 物品 SO、掉落拾取、背包、商店（含商人与页签）
├── SkillTree/           # 技能 SO、技能槽、天赋树管理、效果分发
├── StatsManager.cs      # 全局数值单例
├── StatsUI.cs           # 属性面板
└── ElevationEntry/Exit  # 山体高差区域进出处理
```

---

*如需调整按键，可在 Unity 编辑器 `Edit → Project Settings → Input Manager` 中修改 Slash / Shoot / Interact / ChangeEquipment / ToggleStats / ToggleSkillTree 六个自定义轴。*
