# 技术方案：03-Combat

> 对应 requirements.md R1~R8 | 命名空间 `ProjectName.Player` / `ProjectName.Combat`
> 前置：M2 状态机、PlayerAnimatorDriver、PlayerAnimationSetup 生成的 20 参数/23 状态控制器（含 Attack/Attack2/Attack3/AttackAir 全部状态与转移）已就位

## 1. 结构

```
Player（Prefab，M2 已创建）
 ├─ PlayerController        ← 输入与运动（M2，不改核心，仅加攻击输入分支与攻击锁）
 ├─ PlayerStateMachine      ← 通用 FSM（M2，不改）
 ├─ PlayerAnimatorDriver    ← 参数写入（M2，补 Trigger 消费）
 ├─ PlayerCombat（新）      ← HP/受击/无敌/连段推进，战斗唯一入口
 ├─ SpriteRenderer / Animator
 └─ AttackHitbox（子物体，新）← 攻击开窗期启用的 BoxCollider2D（Trigger）
```

代码文件（Assets/Scripts/Combat/）：
| 文件 | 职责 |
|---|---|
| DamageInfo.cs | 伤害数据结构：Amount / KnockbackDir / Source（统一 R7） |
| IHittable.cs | 接口 `void TakeDamage(DamageInfo info)`（M4 敌人复用） |
| CombatConfig.cs | ScriptableObject：HP 5、伤害 1/段、连段窗口 0.2s、硬直 0.6s、无敌 1s、击退力、开窗帧号 |
| PlayerCombat.cs | 实现 IHittable；持有 HP；接收攻击输入推进连段；受击进入 Hurt 的业务判定（HP、无敌检查） |
| PlayerAttackState.cs | 状态：发起攻击、锁移动、按连段窗口响应下一段输入、发 Trigger |
| 状态改动 | PlayerHurtState / PlayerDeathState 从 M2 的占位改为接收 DamageInfo（击退方向来自伤害源） |

## 2. 三段连击状态机（PlayerAttackState 内部推进）

- 发起：按下 Attack → SwitchState(PlayerAttackState, seg=1) → Animator.SetTrigger(Attack)
- 连段：开窗后 `_comboBuffer` 记录下次输入；本段 clip 在 `comboWindow` 内收到输入 → seg+1、SetTrigger(Attack2/Attack3)；Animator 转移链（Attack→Attack2→Attack3，已由 M2 生成器接好）负责无退出时间衔接
- 回落：动画结束事件 `OnAttackAnimEnd` → 清 buffer → 按 IsGrounded 回 Idle 或 Air
- 第三段命中附击退：Attack03 开窗命中时对目标 DamageInfo.KnockbackDir 置攻击者朝向 × 击退力
- 空中：seg 固定 0（AttackAir 单发），不进链

## 3. 攻击开窗与命中

- Animation Event：`AttackWindowOpen(seg)` / `AttackWindowClose(seg)`（M2 生成的 clip 补加事件帧，帧号来自排期表「动画事件帧」列；任务清单里有补事件步骤）
- 开窗时 `AttackHitbox.enabled = true`，记录本窗已命中集合 `HashSet<IHittable>`；关窗清空并 disable
- 判定：OnTriggerEnter2D 取 IHittable → 不在集合 → 调 TakeDamage(DamageInfo{1, 朝向, player}) → 入集合
- 逻辑帧与时序：开窗状态以事件为准，不靠时间轮询（动画帧即权威）

## 4. 受击与无敌（PlayerCombat 实现 IHittable）

- TakeDamage：无敌期未过 → 忽略（R5）；否则 HP -= Amount → HP≤0 转 Death（复用 M2 Die）否则转 Hurt
- Hurt 击退：velocity 由伤害源反方向 × 硬直击退，锁输入 0.6s（沿用 M2 Hurt 状态骨架，改吃 DamageInfo）
- 无敌闪烁：Hurt 时协程按 0.08s 切 alpha，1s 后复位
- 优先级：Hurt > Attack > Air/地面常规（状态机 SwitchState 直接覆盖，与 M2 约定一致）

## 5. 数据流（一次命中）

```
输入 J → PlayerController 报给 PlayerCombat → SwitchState(Attack)
→ SetTrigger(Attack) → clip 播到开窗帧 → AttackWindowOpen → hitbox 开
→ OnTriggerEnter2D(IHittable) → TakeDamage(DamageInfo) → 敌人扣血 / 玩家 Hurt
→ 关窗帧 → 清集合 → 结束帧 → 回 Idle/Air
```

## 6. 设计取舍

- **HP 放 PlayerCombat 而非 PlayerConfig**：Config 是手感参数（静态），HP 是运行时状态（会被存档读写，M6 SaveSystem 对接）
- **攻击不位移（原地攻击）**：横版轻量战斗，位移斩击后续若需要再由事件帧施加小推力；减少空中攻击与平台边缘的组合 bug 面
- **开窗用事件而非定时器**：动画帧率即攻击节奏权威，改帧只需改 clip 事件，代码零改动
- **IHittable 提前定义**：M4 敌人接入零返工，命中流水线与 M5 特殊攻击共用同一接口（特殊攻击 = 另一种 DamageInfo）
- **HeavyAttack/Dash/Roll 排除**：控制器已有状态但玩法依赖未到（资源经济、闪避无敌设计），接线点已预留（Trigger 名已存在）

## 7. 风险

- 连段输入窗与动画衔接时序（玩家按晚了半帧）——连段窗参数化，手感校准期调整
- 玩家攻击与 Hurt 竞争：Hurt 从 Trigger 改为 SwitchState 覆盖后，攻击中受击必须走 PlayerCombat 统一入口，避免两处各写一半
- 攻击开窗帧号写死在 clip 里，若重生成动画帧数变化需同步重打事件——任务里安排一次校验
