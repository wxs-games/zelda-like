# 技术设计文档（TDD）

> 项目：基于Unity2D的"星际环保"动作冒险游戏设计与实现
> 版本：v0.1（初稿） | 最后更新：2026-10-15
> 参考模板：rikegames Unity TGDD + KelptheWriter/TechDesignDocument 结构
> 关联文档：GDD.md（设计愿景）、specs/（逐功能规范三件套）

---

## 1. 技术概览

| 项 | 决策 | 理由 |
|---|---|---|
| 引擎 | Unity 2D（URP 可选，内置管线亦可） | 毕设主题指定；2D 工具链成熟 |
| 语言 | C#（.NET Standard 2.1） | 引擎原生 |
| 版本管理 | Git + GitHub（已有 main 分支），美术大文件不进 LFS（已提交验证） | 现状可用 |
| 目标平台 | Windows Standalone，60 FPS | 毕设演示环境 |
| 外部依赖 | 仅 TextMeshPro（内置）；其余自研 | 减少答辩时"用了多少现成库"的质疑 |

## 2. 工程结构与规范

### 2.1 目录结构
```
Assets/
├─ Art/                    （已就位：Player/Enemies/Boss/Towers/FX）
├─ Scenes/                 Level01.unity / Level02.unity / Level03.unity / Boot.unity
├─ Prefabs/                Player/ Enemies/ Boss/ Towers/ Props/ FX/ UI/
├─ ScriptableObjects/      Enemies/ Items/ Levels/ Dialogue/
├─ Resources/              （仅存档与对话 JSON）
└─ Scripts/
   ├─ Framework/           单例、事件中心、对象池、场景管理、存档、音频
   ├─ Player/              控制器、状态机、净化喷雾
   ├─ Combat/              伤害、判定框、HP、受击反馈
   ├─ EnemyAI/             FSM 基类 + 三种敌人实现
   ├─ Boss/                Boss 阶段状态机
   ├─ Eco/                 垃圾堆、污染源、净化值、污染度
   ├─ Dialogue/            对话控制器、提示牌
   ├─ Quest/               关卡目标、限时挑战
   ├─ UI/                  HUD、菜单、结算、图鉴
   └─ LevelFlow/           关卡流程、检查点、门/通路控制
```

### 2.2 编码规范
- 命名：类/方法 PascalCase，私有字段 `_camelCase`，常量 PascalCase；文件名=类名
- 模式应用：管理器用泛型单例；系统间通信走事件中心（解耦，论文可写"观察者模式"）；子弹/FX/敌人用对象池；敌人与玩家用状态机；配置用 ScriptableObject 数据驱动
- 性能红线：Update 内禁止 `Find`/`GetComponent`（缓存于 Awake）；物理逻辑只在 FixedUpdate；频繁生成物全部入池；每帧零 GC 分配为目标
- 碰撞分层：Player / Enemy / Boss / Ground / Interactable / FX 六层 + 碰撞矩阵配置表

## 3. 核心架构（对应论文"系统设计"章）

### 3.1 系统关系图（文字 UML）
```
GameManager(Facade单例)
 ├─ EventCenter          ← 全系统解耦通信
 ├─ SceneManagerEx       ── Boot→菜单→关卡 流程
 ├─ SaveSystem           ── JSON 存档（进度/图鉴/设置）
 ├─ AudioManager         ── BGM/SFX 双通道
 ├─ ObjectPool           ── 弹体/FX/敌人复用
 └─ LevelFlowController  ── 每关目标链 + 检查点 + 结算
       ↓ 引用
 PlayerController ── FSM：Ground(Idle/Run)/Air(Jump/Fall/Land)/Combat(Attack/Spray)/Hurt/Die
       ↓ 输入伤害                ↑ 净化事件
 HealthSystem(共用于玩家/敌人/Boss)   EcoSystem(垃圾堆/污染源/净化值)
       ↓ 死亡事件                        ↓ 污染度变化
 EnemyFSM(基类) ── Enemy01/02/03 子类   AtmosphereController(色调/刷怪)
 BossFSM(阶段机：P1→P2→P3)
```

### 3.2 关键数据流
- 净化：SprayController 命中 `IPurifiable` → EcoSystem.AddPurity() → EventCenter 广播 → HUD/Atmosphere/LevelFlow 三方响应
- 敌人死亡：HealthSystem Die 事件 → ObjectPool 回收 → 掉落表(ScriptableObject) 生成补给
- 存档：检查点触发 → SaveSystem 序列化 LevelState（净化值/已收集品/检查点 ID）

### 3.3 ScriptableObject 数据模型
- `EnemyConfig`：HP/移速/攻击力/攻击间隔/掉落表/动画帧率
- `LevelConfig`：目标序列、净化值阈值、初始净化剂、污染度-色调曲线
- `DialogueAsset`：NPC 对话 JSON 引用 + 事件回调 ID
- `ItemConfig`：道具类型/效果/图标引用

## 4. 功能清单与风险
| 功能 | 风险 | 对策 |
|---|---|---|
| 横版手感（土狼时间/缓冲） | 手感调参耗时 | 参数全部入 SO，运行时可调 |
| Boss 三阶段状态机 | 复杂度最高 | 先做通用 FSM 基类，Boss 只写阶段脚本 |
| 污染度驱动视觉 | 需要后处理/色调插值 | 用简单材质属性插值，避免引入后处理栈 |
| 对话/图鉴系统 | 内容量大 | JSON 数据驱动，策划表可批量填 |
| 三个关卡搭建 | 场景工作量 | Tilemap + 预制件复用，MCP 工具辅助批量摆放 |

## 5. 实施计划（与 specs/ 对应）

| 阶段 | Spec 包 | 交付物 |
|---|---|---|
| M1 框架 | 01-Framework | 单例/事件/对象池/存档/音频 五件套 + Boot 场景 |
| M2 玩家 | 02-PlayerController | 完整移动跳跃手感 + 动画状态机 |
| M3 战斗 | 03-Combat | 三段攻击 + HP/受击/无敌帧 |
| M4 敌人 | 04-EnemyAI | FSM 基类 + 三种敌人 + Tower01 |
| M5 环保玩法 | 05-EcoSystem | 净化喷雾/垃圾堆/净化值/污染度 |
| M6 关卡一 | 06-Level01 | 第一关完整可玩（含检查点/结算） |
| M7 UI/对话 | 07-UIDialogue | HUD/菜单/对话/图鉴 |
| M8 Boss+关三 | 08-Boss | Boss 阶段机 + 第三关 |
| M9 关卡二三 | 09-Level02-03 | 解谜/解救玩法 + 全流程打通 |
| M10 打磨 | 10-Polish | 音效铺量/数值校准/Build |

## 6. 测试计划
- 单元级：伤害计算、净化值累加、存档序列化（EditMode Test）
- 集成级：每 M 完成后走查 GDD"玩法走读"路径
- 性能级：Profiler 检查关卡 GC=0、帧率稳定 60
- 验收级：对照对应 spec 的 requirements.md 逐条 EARS 验证

## 7. 变更记录
| 版本 | 日期 | 变化 |
|---|---|---|
| v0.1 | 2026-10-15 | 初稿：架构与 M1~M10 计划 |
