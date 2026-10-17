# 任务清单：01-Framework

> 每个 T 是一个可独立验证的最小步；完成标准 = 代码写完 + Unity 编译通过 + 对应 EARS 条款不破

- [ ] T1 MonoSingleton.cs —— R1 全部条款
- [ ] T2 EventCenter.cs —— R2 全部条款（含销毁订阅者自动清理）
- [ ] T3 ObjectPool.cs + PoolManager.cs —— R3 全部条款（含重复 Release 防御）
- [ ] T4 SaveSystem.cs —— R4 全部条款（损坏容错）
- [ ] T5 AudioManager.cs —— R5 全部条款（切歌淡入淡出、音量持久化）
- [ ] T6 FrameworkSelfTest.cs —— RuntimeInitializeOnLoadMethod 自检：事件订阅/触发、池取还、存档往返、设置读写
- [ ] T7 Unity 刷新编译，Console 零报错；运行自检输出全 PASS
- [ ] T8 git commit（M1: framework five-piece）
