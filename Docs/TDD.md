# 技术设计文档（TDD）

> **这是通用模板**：章节骨架、目录结构、编码规范、架构写法可直接复用，
> 所有 `{{…}}` 与 `【填写：…】` 均为待填占位。装配到具体工程后逐项替换。
> 版本：v0.1（模板）
> 参考结构：rikegames Unity TGDD + KelptheWriter/TechDesignDocument
> 关联文档：GDD.md（设计愿景）、specs/（逐功能规范三件套）

**占位符约定**：`{{…}}` 为待填短语，`【填写：…】` 为需成段撰写的内容，`（示例：…）` 为写法参考、填完删除。

---

## 1. 技术概览

| 项 | 决策 | 理由 |
|---|---|---|
| 引擎 | Unity 2D（【填写：内置管线 / URP】） | 【填写：选择理由】 |
| 语言 | C#（.NET Standard 2.1） | 引擎原生 |
| 版本管理 | 【填写：Git + 远端；大文件是否走 LFS】 | 【填写】 |
| 目标平台 | 【填写：例 Windows Standalone，60 FPS】 | 【填写：演示/发布环境】 |
| 外部依赖 | 【填写：例 仅 TextMeshPro（内置）；其余自研】 | 【填写：依赖取舍理由】 |

## 2. 工程结构与规范

### 2.1 目录结构
```
Assets/
├─ Art/                    【填写：美术资源根目录。若沿用本模板示例美术：Player/Enemies/Boss/Towers/FX】
├─ Scenes/                 【填写：Boot.unity / 菜单 / 各关卡场景】
├─ Prefabs/                【填写：Player/ Enemies/ Boss/ Props/ FX/ UI/ …】
├─ ScriptableObjects/      【填写：按配置类型分子目录】
├─ Resources/              【填写：仅放运行时需 Resources 加载的内容，如存档与对话 JSON】
└─ Scripts/
   ├─ Framework/           【填写：单例、事件中心、对象池、场景管理、存档、音频】
   ├─ Player/              【填写：控制器、状态机、{{特色能力}}】
   ├─ Combat/              【填写：伤害、判定框、HP、受击反馈】
   ├─ EnemyAI/             【填写：FSM 基类 + 各敌人实现】
   ├─ Boss/                【填写：Boss 阶段状态机（若有）】
   ├─ {{特色系统}}/         【填写：本作独有系统，如任务/经济/环境度】
   ├─ Dialogue/            【填写：对话控制器、提示牌（若有）】
   ├─ Quest/               【填写：关卡目标、限时挑战（若有）】
   ├─ UI/                  【填写：HUD、菜单、结算、图鉴】
   └─ LevelFlow/           【填写：关卡流程、检查点、门/通路控制】
```

### 2.2 编码规范
- 命名：类/方法 PascalCase，私有字段 `_camelCase`，常量 PascalCase；文件名=类名
- 模式应用：管理器用泛型单例；系统间通信走事件中心（解耦，可写"观察者模式"）；子弹/FX/敌人用对象池；敌人与玩家用状态机；配置用 ScriptableObject 数据驱动
- 性能红线：Update 内禁止 `Find`/`GetComponent`（缓存于 Awake）；物理逻辑只在 FixedUpdate；频繁生成物全部入池；每帧零 GC 分配为目标
- 碰撞分层：【填写：本作的分层方案 + 碰撞矩阵配置表。示例：Player / Enemy / Boss / Ground / Interactable / FX 六层】
- 命名空间：【填写：根命名空间。若沿用本模板，`ProjectName` 是占位符，装配时全局替换为你的工程根命名空间；不使用命名空间的工程直接删除该前缀】

## 3. 核心架构（对应论文"系统设计"章）

### 3.1 系统关系图（文字 UML）
```
【填写：按下面的形状画一棵依赖树，根节点是门面/管理器单例，向下挂各子系统，
再画出玩家、战斗、敌人、特色系统之间的事件与引用关系。
示例骨架：

GameManager(Facade单例)
 ├─ EventCenter          ← 全系统解耦通信
 ├─ SceneManagerEx       ── Boot→菜单→关卡 流程
 ├─ SaveSystem           ── JSON 存档
 ├─ AudioManager         ── BGM/SFX 双通道
 ├─ ObjectPool           ── 弹体/FX/敌人复用
 └─ LevelFlowController  ── 每关目标链 + 检查点 + 结算
       ↓ 引用
 PlayerController ── FSM：Ground(Idle/Run)/Air(Jump/Fall/Land)/Combat(Attack)/Hurt/Die
       ↓ 输入伤害                ↑ {{特色事件}}
 HealthSystem(共用于玩家/敌人/Boss)   {{特色系统}}
       ↓ 死亡事件                        ↓ {{全局量变化}}
 EnemyFSM(基类) ── 各敌人子类        AtmosphereController(色调/刷怪)
 BossFSM(阶段机：P1→P2→P3)
】
```

### 3.2 关键数据流
- 【填写：核心动作链路。示例：{{动作}}：XxxController 命中 `IXxxable` → XxxSystem.Add() → EventCenter 广播 → HUD/环境/关卡流程三方响应】
- 敌人死亡：HealthSystem Die 事件 → ObjectPool 回收 → 掉落表(ScriptableObject) 生成补给
- 存档：【填写：触发时机与序列化内容。示例：检查点触发 → SaveSystem 序列化 LevelState（进度/已收集品/检查点 ID）】

### 3.3 ScriptableObject 数据模型
- `EnemyConfig`：HP/移速/攻击力/攻击间隔/掉落表/动画帧率
- `LevelConfig`：目标序列、{{全局量}}阈值、初始{{资源}}、{{曲线类配置}}
- `DialogueAsset`：NPC 对话 JSON 引用 + 事件回调 ID
- `ItemConfig`：道具类型/效果/图标引用
- 【按需增删；只放策划可调的配置数据，固定行为逻辑仍用 C# 接口/泛型类】

## 4. 功能清单与风险
| 功能 | 风险 | 对策 |
|---|---|---|
| 【填写：功能名】 | 【填写：风险】 | 【填写：对策】 |
| 横版手感（土狼时间/缓冲） | 手感调参耗时 | 参数全部入 SO，运行时可调 |
| Boss 阶段状态机 | 复杂度最高 | 先做通用 FSM 基类，Boss 只写阶段脚本 |
| 关卡搭建 | 场景工作量大 | Tilemap + 预制件复用，编辑器工具辅助批量摆放 |

## 5. 实施计划（与 specs/ 对应）

| 阶段 | Spec 包 | 交付物 |
|---|---|---|
| M1 框架 | 01-Framework | 单例/事件/对象池/存档/音频 五件套 + Boot 场景 |
| M2 玩家 | 02-PlayerController | 完整移动跳跃手感 + 动画状态机 |
| M3 战斗 | 03-Combat | 攻击段数 + HP/受击/无敌帧 |
| M4 敌人 | 04-EnemyAI | FSM 基类 + 各敌人实现 |
| M5 {{特色玩法}} | 05-{{Xxx}}System | 【填写：本作独有系统的交付物】 |
| M6 关卡一 | 06-Level01 | 第一关完整可玩（含检查点/结算） |
| M7 UI/对话 | 07-UIDialogue | HUD/菜单/对话/图鉴 |
| M8 Boss+终关 | 08-Boss | Boss 阶段机 + 最终关 |
| M9 其余关卡 | 09-Level0X | 【填写：剩余玩法】+ 全流程打通 |
| M10 打磨 | 10-Polish | 音效铺量/数值校准/Build |

> 里程碑数量按实际增删；每阶段对应一个 `specs/NN-名称/` 三件套（requirements / design / tasks）。

## 6. 测试计划
- 单元级：【填写：伤害计算、{{核心数值}}累加、存档序列化（EditMode Test）】
- 集成级：每 M 完成后走查 GDD"玩法走读"路径
- 性能级：Profiler 检查关卡 GC=0、帧率稳定 {{目标帧率}}
- 验收级：对照对应 spec 的 requirements.md 逐条 EARS 验证

## 7. 变更记录
| 版本 | 日期 | 变化 |
|---|---|---|
| v0.1 | 【填写日期】 | 初稿：架构与里程碑计划 |
