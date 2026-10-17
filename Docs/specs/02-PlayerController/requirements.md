# 需求规范：02-PlayerController（玩家控制器）

> 对应 TDD.md M2 里程碑 | 格式：EARS
> 范围：移动/跳跃手感、玩家状态机、动画参数驱动、PlayerConfig 数据配置、一键动画搭建工具
> 非目标：攻击/净化（M3）、与敌人交互、输入自定义（键盘硬编码，手柄后置）

## R1 移动

- WHEN 玩家按住 A/D（或左右方向键），THE SYSTEM SHALL 以 PlayerConfig.MoveSpeed 沿水平方向移动，且松开即停（无惯性漂移）。
- WHEN 玩家同时按住左右键，THE SYSTEM SHALL 以后按下的方向为准。
- WHEN 玩家在空中，THE SYSTEM SHALL 保持水平控制（空中机动），控制力可与地面不同（AirControlRatio）。
- WHEN 玩家朝向改变，THE SYSTEM SHALL 通过 SpriteRenderer.flipX 翻转且不缩放 Transform（避免碰撞体镜像问题）。
- WHEN 角色面朝右/左播放 Run 动画，THE SYSTEM SHALL 使用面向右的原始美术帧 + flipX（美术只提供了单侧帧）。

## R2 跳跃与手感

- WHEN 玩家在地面上按跳跃键，THE SYSTEM SHALL 施加 JumpVelocity 并进入空中上升状态。
- WHEN 玩家松开跳跃键且仍在上升，THE SYSTEM SHALL 将垂直速度乘以 JumpCutMultiplier（可变跳高）。
- WHEN 玩家离开地面但仍在 CoyoteTime（默认 0.1s）内按跳跃，THE SYSTEM SHALL 仍允许起跳一次。
- WHEN 玩家在落地前 JumpBufferTime（默认 0.1s）内按了跳跃，THE SYSTEM SHALL 在触地瞬间自动起跳。
- WHEN 玩家在空中下落，THE SYSTEM SHALL 施加 FallGravityMultiplier（下落比上升快，手感更利落）。
- WHEN 玩家最大下落速度超过 MaxFallSpeed，THE SYSTEM SHALL 钳制（防止穿透与摔落过快）。
- IF 玩家已处于跳跃上升中再次按跳跃，THE SYSTEM SHALL 忽略（单段跳，本作无二段跳）。

## R3 状态机

- WHEN 玩家在地面且水平输入为零，THE SYSTEM SHALL 处于 Idle 状态（播放 Player_Idle）。
- WHEN 玩家在地面且水平输入非零，THE SYSTEM SHALL 处于 Run 状态（播放 Player_Run）。
- WHEN 玩家垂直速度 > 0.1 且离地，THE SYSTEM SHALL 处于 Jump 状态（播放 Player_Jump）。
- WHEN 玩家垂直速度 < -0.1 且离地，THE SYSTEM SHALL 处于 Fall 状态（播放 Player_Fall）。
- WHEN 玩家由空中触地，THE SYSTEM SHALL 进入 Land 状态短暂锁定移动（LandLockTime，默认 0.06s，播放 Player_Land）。
- WHEN 玩家受到伤害，THE SYSTEM SHALL 进入 Hurt 状态：击退 + 无敌 1s（无敌期闪烁），结束后回到 Idle/Fall。
- WHEN 玩家 HP 归零，THE SYSTEM SHALL 进入 Die 状态：停用输入与碰撞，播放 Player_Die。
- IF 状态切换发生，THE SYSTEM SHALL 通过 PlayerAnimator 同步对应的动画参数/触发器。

## R4 动画驱动（与美术 9 套动画对接）

- THE SYSTEM SHALL 使用美术帧目录：Idle/Run/Jump/Fall/Land/Hurt/Die/Attack01/Interact（Attack01 与 Interact 在 M3 接入，本里程碑只搭好状态）。
- WHEN 驱动动画，THE SYSTEM SHALL 仅通过 Animator 参数（Speed/IsGrounded/VelocityY/Trigger）控制，状态机逻辑不直接操作 Animation 组件。
- WHEN 一键搭建工具运行，THE SYSTEM SHALL 从 Art/Art/Player/Frames/ 各子目录生成 AnimationClip（12fps，按文件名排序），并生成 AnimatorController（含全部状态与转移条件）存至 Assets/Animations/Player/。

## R5 数据配置（PlayerConfig ScriptableObject）

- THE SYSTEM SHALL 所有手感参数（MoveSpeed/JumpVelocity/GravityScale/AirControlRatio/JumpCutMultiplier/CoyoteTime/JumpBufferTime/FallGravityMultiplier/MaxFallSpeed/LandLockTime）均可编辑于 PlayerConfig 资产，代码不硬编码。
- WHEN PlayerConfig 缺失，THE SYSTEM SHALL 使用代码内默认值并输出警告。

## 验收方式
- 编译零报错；按 Play 后在地面上跑跳：土狼时间/缓冲/可变跳高逐条人工验证
- 一键搭建工具生成的 Animator 状态机可在 Animator 窗口目检，Enter Play 模式跑动时动画正确切换
