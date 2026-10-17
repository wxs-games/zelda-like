# 需求规范：01-Framework（框架层五件套）

> 对应 TDD.md M1 里程碑 | 格式：EARS（Easy Approach to Requirements Syntax）
> 范围：MonoSingleton / EventCenter / ObjectPool+PoolManager / SaveSystem / AudioManager
> 非目标：场景管理器（M2 与关卡流程一起做）、资源加载框架（本项目资源量小，直接引用）

## R1 单例基类（MonoSingleton）

- WHEN 任意脚本首次访问 `MonoSingleton<T>.Instance`，THE SYSTEM SHALL 自动创建名为类型名的 GameObject 并挂载该组件，且标记 DontDestroyOnLoad。
- WHEN 场景中已存在该类型实例时再次访问，THE SYSTEM SHALL 返回同一实例，不创建重复对象。
- WHEN 手工在场景中放置了该类型组件且已有运行时实例，THE SYSTEM SHALL 销毁后到的重复对象（保留先到的）。
- WHEN 应用退出，THE SYSTEM SHALL 不再通过 Instance 属性创建新对象（防退出期复活）。
- IF 实例被显式销毁，THE SYSTEM SHALL 清空静态引用，使后续访问可重新创建。

## R2 事件中心（EventCenter）

- WHEN `EventCenter.Subscribe<T>(handler)` 被调用，THE SYSTEM SHALL 登记该事件类型的处理器，不影响其他事件类型。
- WHEN `EventCenter.Publish<T>(evt)` 被调用，THE SYSTEM SHALL 通知该事件类型的全部有效订阅者。
- WHEN 订阅者所属的 UnityEngine.Object 已被销毁后事件再触发，THE SYSTEM SHALL 自动清理该失效订阅且不抛出异常。
- WHEN `EventCenter.Unsubscribe<T>(handler)` 被调用，THE SYSTEM SHALL 仅移除该处理器。
- WHEN Publish 期间发生嵌套 Publish，THE SYSTEM SHALL 不因集合修改而崩溃（倒序遍历快照语义）。

## R3 对象池（ObjectPool / PoolManager）

- WHEN `ObjectPool<T>.Get` 被调用且空闲队列非空，THE SYSTEM SHALL 出队一个对象并激活，不实例化。
- WHEN `ObjectPool<T>.Get` 被调用且空闲队列为空，THE SYSTEM SHALL 实例化一份预制体副本返回。
- WHEN `Release(obj)` 被调用，THE SYSTEM SHALL 隐藏对象、挂回池根节点并入队，不销毁。
- WHEN PoolManager 以预制体为键请求池，THE SYSTEM SHALL 同一预制体复用同一个池；首次请求可指定预热数量。
- IF 同一对象被重复 Release，THE SYSTEM SHALL 忽略重复归还，不破坏队列。

## R4 存档系统（SaveSystem）

- WHEN `SaveSystem.Save(slot, data)` 被调用，THE SYSTEM SHALL 将 data 以 JSON（缩进美化）写入 `persistentDataPath/<slot>.json`。
- WHEN `Load<T>(slot)` 被调用且存档存在，THE SYSTEM SHALL 反序列化并返回数据对象。
- WHEN 存档文件不存在，THE SYSTEM SHALL 返回 `new T()` 默认值。
- IF 存档文件损坏或反序列化抛异常，THE SYSTEM SHALL 捕获异常并返回默认值，不崩溃。
- WHEN `Delete(slot)` 被调用，THE SYSTEM SHALL 删除对应文件（不存在时静默）。

## R5 音频管理器（AudioManager）

- WHEN `PlayBgm(clip)` 被调用且当前正在播放同一曲目，THE SYSTEM SHALL 不重启播放。
- WHEN `PlayBgm(clip)` 被调用且曲目变化，THE SYSTEM SHALL 平滑淡出旧曲并淡入新曲（约 0.8s）。
- WHEN `PlaySfx(clip)` 被调用，THE SYSTEM SHALL 通过 OneShot 播放，不打断已有音效。
- WHEN 音量接口（SetBgmVolume/SetSfxVolume）被调用，THE SYSTEM SHALL 立即应用并通过 SaveSystem 持久化到 "settings" 存档槽。
- WHEN 游戏启动后首次访问 AudioManager，THE SYSTEM SHALL 从 "settings" 槽加载音量并应用。

## 验收方式
每条 EARS 语句 = 一条可手动/脚本验证的检查项；M1 验收时逐条核对（见 tasks.md 验收任务）。
