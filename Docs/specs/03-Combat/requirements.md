# 需求规范：03-Combat（战斗系统）

> 对应 TDD.md M3 里程碑 | 格式：EARS
> 范围：三段近战连击（含空中攻击）、攻击开窗与命中判定、HP/受击硬直、无敌帧、玩家与敌人的伤害接口
> 非目标：HeavyAttack 蓄力攻击（M5，依赖净化剂经济）、Dash/Roll 闪避（后续里程碑评估）、敌人 AI（M4）、净化攻击（M5）、输入自定义（沿用 M2 硬编码）

## R1 三段连击

- WHEN 玩家在地面按下攻击键（默认 J / 鼠标左键），THE SYSTEM SHALL 进入攻击状态并播放 Attack01（三段链第一段）。
- WHEN 玩家处于第一段或第二段攻击的连段窗口内再次按下攻击键，THE SYSTEM SHALL 在当前段动画窗口结束后无缝衔接 Attack02 / Attack03。
- IF 连段窗口已关闭或玩家未再按攻击键，THE SYSTEM SHALL 在当前段动画播放完毕后回到 Idle（或空中状态）。
- WHEN 第三段（Attack03）命中，THE SYSTEM SHALL 附带击退效果（对目标施加击退向量，方向为攻击者面朝方向）。
- WHEN 玩家处于地面攻击中，THE SYSTEM SHALL 锁定水平移动输入（原地攻击），攻击结束立即恢复。
- WHEN 玩家在攻击期间朝向输入反向，THE SYSTEM SHALL 忽略翻转（攻击中不可转身，M3 简化）。

## R2 空中攻击

- WHEN 玩家离地按下攻击键，THE SYSTEM SHALL 播放 AttackAir（空中攻击一次，不参与三段链）。
- WHEN 空中攻击动画播放完毕且玩家仍离地，THE SYSTEM SHALL 回到 Air 状态。
- IF 玩家在空中攻击动画结束前触地，THE SYSTEM SHALL 按 M2 的 Land 流程处理（触地优先）。

## R3 攻击开窗与命中判定

- THE SYSTEM SHALL 通过 Animation Event（事件帧见排期表）在每段攻击的有效帧开启/关闭命中开窗，开窗期外的碰撞体不产生命中。
- WHEN 命中开窗期间检测到带 IHittable 组件的目标，THE SYSTEM SHALL 对其调用一次伤害回调（同一目标在单次开窗内不重复命中）。
- WHEN 攻击未命中任何目标，THE SYSTEM SHALL 正常走完动画（攻击不因落空取消）。

## R4 伤害与受击

- THE SYSTEM SHALL 玩家基础 HP = 5（CombatConfig 可配），受击时按 DamageInfo 扣减。
- WHEN 玩家在无敌期外受到伤害，THE SYSTEM SHALL 进入 Hurt 状态：沿伤害来源反方向击退，硬直锁输入 0.6s（CombatConfig 可配）。
- WHEN Hurt 硬直结束，THE SYSTEM SHALL 回到 Idle 或 Air（按 IsGrounded 判定）。
- IF 玩家攻击中受到伤害，THE SYSTEM SHALL 打断攻击并进入 Hurt（受击优先级高于攻击，同 M2 的 Hurt 最高优先级约定）。

## R5 无敌帧

- WHEN 玩家受击进入 Hurt，THE SYSTEM SHALL 同时开启 1s 无敌期（CombatConfig 可配），期间 SpriteRenderer 以固定频率闪烁。
- IF 玩家在无敌期内再次受到伤害，THE SYSTEM SHALL 忽略该伤害（不扣 HP、不打断动作）。
- WHEN 无敌期结束，THE SYSTEM SHALL 恢复正常受击与显示。

## R6 死亡

- WHEN 玩家 HP 降至 0，THE SYSTEM SHALL 进入 M2 已有的 Die 状态（停用输入与受击，播放 Player_Die），重生/回检查点由 LevelFlow（M6）接管。

## R7 伤害接口（为 M4 敌人预留）

- THE SYSTEM SHALL 定义统一伤害数据 DamageInfo（伤害值、来源方向、来源对象）与 IHittable 接口（TakeDamage(DamageInfo)）。
- THE SYSTEM SHALL 玩家侧实现 IHittable（走 R4/R5）；M4 敌人实现同接口后即接入同一条命中流水线。
- WHEN 攻击命中携带 IHittable 的目标，THE SYSTEM SHALL 仅通过接口回调，不直接引用任何具体目标类型。

## R8 数据配置与动画对接

- THE SYSTEM SHALL 战斗参数（HP、伤害值、连段窗口、硬直时长、无敌时长、击退力、命中开窗帧号）全部配置于 CombatConfig（ScriptableObject），代码不硬编码。
- WHEN 驱动攻击动画，THE SYSTEM SHALL 仅通过 Animator Trigger（Attack/Attack2/Attack3/AttackAir）切换，与 PlayerAnimationSetup 生成的控制器一致。
- WHEN 攻击开窗/结束事件触发，THE SYSTEM SHALL 由 Animation Event 调用脚本方法（事件帧与排期表「动画事件帧」列严格对应）。

## 验收方式
- 编译零报错；按 Play 后逐条验证：三段连击衔接、连段窗口关闭回落、空中攻击、开窗期外不命中
- 用测试靶（带 IHittable 的占位对象）验证命中回调、玩家受击击退、无敌期闪烁与伤害忽略、HP 归零进 Die
- Animator 窗口目检 Attack 状态转移（Attack→Attack2→Attack3 链与回落）
