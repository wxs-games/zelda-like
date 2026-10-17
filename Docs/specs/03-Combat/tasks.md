# 任务清单：03-Combat

- [ ] T1 DamageInfo + IHittable 接口（Assets/Scripts/Combat/，M4 复用基座）
- [ ] T2 CombatConfig.cs（ScriptableObject：HP/伤害/连段窗/硬直/无敌/击退/开窗帧号）
- [ ] T3 PlayerCombat.cs（实现 IHittable、HP、连段输入接收、受击/无敌判定入口）
- [ ] T4 PlayerAttackState.cs（发起/锁移动/连段 buffer/AttackAir 分支/结束回落）
- [ ] T5 改造 PlayerHurtState：吃 DamageInfo（击退方向来自伤害源），接 PlayerCombat 的 HP 扣减
- [ ] T6 AttackHitbox 子物体 + 开窗判定（OnTriggerEnter2D + 本窗命中集合）
- [ ] T7 为 Attack01/02/03/AttackAir 四条 clip 补 Animation Event（帧号严格对齐排期表「动画事件帧」列）
- [ ] T8 PlayerAnimatorDriver 补消费 4 个攻击 Trigger（Attack/Attack2/Attack3/AttackAir）
- [ ] T9 测试靶：场景内占位 IHittable 对象（验证命中回调与击退）
- [ ] T10 Unity 编译零报错 + 控制台检查
- [ ] T11 运行验证：三段连击衔接、连段窗回落、空中攻击、开窗期外不命中、受击击退、无敌闪烁、HP 归零进 Die（需用户配合按 Play 目检手感）
- [ ] T12 git commit（M3: combat system — combo/HP/hit window/invincibility）
