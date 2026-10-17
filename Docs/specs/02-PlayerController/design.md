# 技术方案：02-PlayerController

> 对应 requirements.md R1~R5 | 命名空间 `ProjectName.Player`

## 1. 结构

```
Player（Prefab，M2 末或 M3 初创建）
 ├─ Rigidbody2D（Dynamic，冻结 Z 轴旋转）
 ├─ BoxCollider2D（脚部 + 主体）
 ├─ PlayerController      ← 物理与手感核心（唯一 Update/FixedUpdate 入口）
 ├─ PlayerStateMachine    ← 通用状态机（状态注册表 + 当前态 + SwitchState）
 ├─ PlayerAnimatorDriver  ← 每帧向 Animator 写参数
 ├─ SpriteRenderer / Animator
 └─ GroundChecker（子物体 + 可配置 LayerMask）
```

代码文件（Assets/Scripts/Player/）：
| 文件 | 职责 |
|---|---|
| PlayerConfig.cs | ScriptableObject 手感参数 + 缺省兜底 |
| PlayerController.cs | 输入读取、Rigidbody2D 运动、土狼/缓冲计时、朝向 |
| PlayerStateMachine.cs | 通用 FSM：字典注册 + SwitchState(old→new, Enter/Exit) |
| IPlayerState.cs | 状态接口（Enter/Exit/Tick/FixedTick） |
| PlayerStateBase.cs | 公共基类（持有 Controller 引用与常用上下文） |
| 状态们 | PlayerIdleState / PlayerRunState / PlayerAirState（Jump 与 Fall 合并为空中态，由 VelocityY 分支动画）/ PlayerLandState / PlayerHurtState / PlayerDieState |
| PlayerAnimatorDriver.cs | Speed/IsGrounded/VelocityY 参数写入 |

设计取舍：
- **Jump/Fall 合并为 AirState**：物理上是同一个自由落体过程，动画分支交给 Animator 参数（VelocityY 阈值），状态数量减半、代码更薄；论文可解释"状态内聚性"
- **Hurt 击退**：直接改 velocity + 短暂锁输入；无敌闪烁用协程切换 SpriteRenderer.alpha
- **输入**：用旧版 Input Manager（GetAxisRaw/GetButtonDown("Jump")），零依赖；M10 打磨期可视需要迁移 Input System

## 2. 手感实现细节

- 土狼时间：`_coyoteCounter`，OnGround 时刷新为 CoyoteTime，离地后逐帧递减；跳跃消耗置 0
- 跳跃缓冲：`_jumpBufferCounter`，按下时刷新为 JumpBufferTime；FixedTick 触地且缓冲>0 → 起跳并清零
- 可变跳高：Input JumpUp 事件时 `velocity.y *= JumpCutMultiplier`（仅当 velocity.y > 0）
- 重力：默认 GravityScale 挂 Rigidbody2D；下落时动态设为 `GravityScale * FallGravityMultiplier`（用 Mathf.MoveTowards 平滑切换）
- 全部物理写操作在 FixedUpdate；输入缓存在 Update 采集

## 3. 一键动画搭建工具（Editor）

`Assets/Scripts/Editor/PlayerAnimationSetup.cs`，菜单 `Tools/Setup/Player Animation`：
1. 遍历 `Art/Art/Player/Frames/<Anim>/` 下 png，设 importer.textureType = Sprite（单精灵，无需切片——美术是逐帧单文件）
2. 生成 AnimationClip：ObjectReferenceKeyframe 绑定 sprite，采样 12fps，按文件名自然排序，存 `Assets/Animations/Player/<Anim>.anim`
3. 生成 AnimatorController：
   - 参数：Speed(float)、IsGrounded(bool)、VelocityY(float)、Hurt/Die/Interact(Trigger)
   - 状态：Idle ⇄ Run（Speed 比较）、Idle/Run → Air（IsGrounded false）、Air → Land（IsGrounded true）、Land → Idle（动画结束退出时间）、任意 → Hurt（Trigger，Has Exit Time 退出回 Idle）、任意 → Die（Trigger，不退出）
4. 幂等：重复运行覆盖重建，不产生垃圾资产
- 帧率 12fps 的依据：美术帧序列风格为手绘 12 格/秒（社区标准 2D 手绘帧率），写入 design 备查

## 4. 风险
- Land 状态锁 0.06s 可能让高手觉得"粘地"——参数化，校准期可调 0
- Hurt 与 Air 状态竞争：Hurt 优先级最高（状态机 SwitchState 覆盖），结束回 Idle 时若在空中由 Idle→Air 的 IsGrounded 转移自然接管
- Animator 状态名与代码常量必须一致——用 PlayerAnimatorParams 静态常量类统一
