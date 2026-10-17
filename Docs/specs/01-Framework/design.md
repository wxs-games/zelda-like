# 技术方案：01-Framework

> 对应 requirements.md 的 R1~R5 | 命名空间统一为 `StarEco.Framework`

## 1. 总体设计

框架层全部无场景依赖：管理器为懒加载单例（首次访问自动建 GameObject），因此 **Boot 场景可以推迟到 M2** 再建，M1 任何脚本都能直接用 `XxxManager.Instance`。

```
MonoSingleton<T>（抽象基类）
   ↑ 继承
PoolManager / AudioManager          SaveSystem / EventCenter（静态类，无需单例）
```

## 2. 模块设计

### 2.1 MonoSingleton<T>（R1）
- 泛型约束 `where T : MonoSingleton<T>`（CRTP）
- 双重检查：`_instance == null` 时创建；`lock` 防.Return 竞态（单线程引擎下是保险）
- `_quitting` 标志位在 `OnApplicationQuit` 置位，堵"退出期 Instance 复活"漏洞
- `OnDestroy` 时若 `_instance == this` 则置空，支持域重载（Domain Reload）后重建

### 2.2 EventCenter（R2）
- 静态类 + `Dictionary<Type, List<Delegate>>`
- 事件类型不加约束（struct/class 均可），发布时 `is` 转型为 `Action<T>`
- 失效清理：调用目标的 `UnityEngine.Object` 若已销毁（Unity 伪 null），倒序遍历时移除——天然满足"销毁不抛异常"
- 倒序遍历同时规避嵌套 Publish 的集合修改问题

### 2.3 ObjectPool<T> + PoolManager（R3）
- `ObjectPool<T>`：非 Mono 纯 C# 类，`Stack<T>` 空闲队列；自带池根节点 GameObject（挂在 PoolManager 下）
- `PoolManager : MonoSingleton<PoolManager>`：`Dictionary<GameObject, object>` 按预制体缓存池（值装箱为非泛型池包装，外部 API 泛型化：`Get<T>(prefab)` / `Release<T>(prefab, obj)`）
- 重复 Release 防御：池内维护 `HashSet<T>` 已借出检查（用 contains 判断）

### 2.4 SaveSystem（R4）
- 静态类；`JsonUtility` 序列化（无需第三方库，答辩友好）
- 路径：`Application.persistentDataPath/<slot>.json`
- 损坏容错：try/catch 包裹读档，异常时返回 `new T()`
- 约束：数据类必须可被 JsonUtility 处理（[Serializable] + 基本类型/List<T>）

### 2.5 AudioManager（R5）
- `MonoSingleton<AudioManager>`；Awake 里创建两路子 AudioSource：
  - `_bgmSource`：loop=true，播放 BGM
  - `_sfxSource`：OneShot 播放 SFX（`PlayOneShot`，支持叠加）
- BGM 切歌协程：`AudioSource.volume` 从当前值线性降到 0 → 换 clip → 升到目标音量（0.8s，`yield return null` 逐帧插值）
- 音量持久化：`GameSettingsData { float bgmVolume; float sfxVolume; }` 存 "settings" 槽

## 3. 数据定义

```csharp
[Serializable] public class GameSettingsData { public float bgmVolume = 0.8f; public float sfxVolume = 1f; }
```

## 4. 测试策略
- M1 无场景对象，验收以"编译零报错 + 一个一次性验证脚本"进行：Boot 时通过 `RuntimeInitializeOnLoadMethod` 触发自检（事件订阅/触发、池取还、存档读写、BGM 音量），结果打到 Console
- 自检脚本归属 `Assets/Scripts/Framework/FrameworkSelfTest.cs`（EDITOR 条件编译保留，供后续回归）

## 5. 兼容性说明
- 目标 Unity 2021.3：仅使用 JsonUtility / Coroutine / AudioSource 等稳定 API，不使用 Unity 6 特性
- C# 8/9 特性仅用目标运行时支持的子集（switch 表达式、using 声明可用；不使用 records/init）
