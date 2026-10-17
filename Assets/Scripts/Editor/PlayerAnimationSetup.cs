using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 一键搭建玩家动画管线：
/// 1. 把 Art/Player/Frames 下的 png 统一设为 Sprite 导入
/// 2. 按目录生成 AnimationClip（12fps）
/// 3. 生成 AnimatorController（状态机 + 转移条件）
/// 4. 生成 Player 预制体（挂 PlayerController）
/// 菜单：Tools → Setup → Player Animation（幂等，可重复运行）
///
/// 相对可复用模板（gongzuoliu/unity-art-pipeline）的适配说明：
/// - 模板用 `ProjectName` 作根命名空间占位；本工程脚本全部在全局命名空间，故删除该前缀。
/// - 模板引用的 PlayerConfig 与 PlayerAnimatorDriver 在本工程不存在
///   （玩家参数不走 SO 配置，动画由 Scripts/Player/States 状态机驱动），已裁剪相应调用。
/// - 帧目录随本工程美术布局改为 Assets/Art/Player/Frames。
/// </summary>
public static class PlayerAnimationSetup
{
    private const string FramesRoot = "Assets/Art/Player/Frames";
    private const string OutputRoot = "Assets/Animations/Player";
    private const string ControllerPath = "Assets/Animations/Player/Player.controller";
    private const string PrefabPath = "Assets/Prefabs/Player/Player.prefab";

    // 目录名 → (clip名, 是否循环, 采样率)。采样率取排期表 Samples 列（未声明则用默认 12）。
    private const float DefaultFrameRate = 12f;
    private static readonly Dictionary<string, (string clip, bool loop, float fps)> AnimMap = new Dictionary<string, (string, bool, float)>
    {
        { "Player_Idle",        ("Idle", true, 12) },
        { "Player_Run",         ("Run", true, 24) },
        { "Player_Jump",        ("Jump", false, 24) },
        { "Player_Fall",        ("Fall", true, 12) },
        { "Player_Land",        ("Land", false, 24) },
        { "Player_Hurt",        ("Hurt", false, 15) },
        { "Player_Die",         ("Death", false, 18) },
        { "Player_Attack01",    ("Attack", false, 24) },
        { "Player_Interact",    ("Interact", false, 18) },
        { "Player_Dash",        ("Dash", false, 30) },
        { "Player_Roll",        ("Roll", false, 24) },
        { "Player_Attack02",    ("Attack02", false, 24) },
        { "Player_Attack03",    ("Attack03", false, 24) },
        { "Player_AttackAir",   ("AttackAir", false, 24) },
        { "Player_Victory",     ("Victory", true, 15) },
        { "Player_Turn",        ("Turn", false, 24) },
        { "Player_WallSlide",   ("WallSlide", true, 12) },
        { "Player_WallJump",    ("WallJump", false, 24) },
        { "Player_LedgeGrab",   ("LedgeGrab", true, 12) },
        { "Player_Crouch",      ("Crouch", false, 15) },
        { "Player_Idle02",      ("Idle02", true, 10) },
        { "Player_HeavyAttack", ("HeavyAttack", false, 24) },
    };

    [MenuItem("Tools/Setup/Player Animation")]
    public static void Run()
    {
        EnsureFolder(OutputRoot);
        EnsureFolder("Assets/Prefabs/Player");
        CleanupLegacyClips();

        var clips = BuildAllClips();
        var controller = BuildController(clips);
        BuildPrefab(controller);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        // 保险：删除重建 controller 的窗口期会让场景内 Player 实例的 Animator 引用被清空
        // （guid 虽已恢复，但内存里的实例引用已断）。重建后统一回绑并保存场景。
        RebindSceneAnimators(controller);
        Debug.Log("[PlayerAnimationSetup] 玩家动画管线搭建完成 ✓");
    }

    /// <summary>把场景中 Player 根节点的 Animator 重新指向刚生成的 controller 并保存场景。</summary>
    private static void RebindSceneAnimators(RuntimeAnimatorController controller)
    {
        int bound = 0;
        foreach (var anim in UnityEngine.Object.FindObjectsOfType<Animator>(true))
        {
            if (anim.gameObject.name != "Player") continue;
            if (anim.runtimeAnimatorController == controller) continue;
            anim.runtimeAnimatorController = controller;
            EditorUtility.SetDirty(anim);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(anim.gameObject.scene);
            bound++;
        }
        if (bound > 0)
        {
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
            Debug.Log($"[PlayerAnimationSetup] 已回绑 {bound} 处场景 Animator 引用");
        }
    }

    /// <summary>删除旧命名（Player_ 前缀）的 clip 资产，避免残留。</summary>
    private static void CleanupLegacyClips()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { OutputRoot }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var name = Path.GetFileNameWithoutExtension(path);
            if (name.StartsWith("Player_"))
            {
                AssetDatabase.DeleteAsset(path);
                Debug.Log($"[PlayerAnimationSetup] 清理旧命名 clip：{name}");
            }
        }
    }

    // ===== 1. Sprite 导入 =====

    private static void EnsureSpriteImport(string pngPath)
    {
        var importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
        if (importer == null || (importer.textureType == TextureImporterType.Sprite &&
                                 importer.spriteImportMode == SpriteImportMode.Single))
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
    }

    // ===== 2. AnimationClip 生成 =====

    private static Dictionary<string, AnimationClip> BuildAllClips()
    {
        var clips = new Dictionary<string, AnimationClip>();

        foreach (var pair in AnimMap)
        {
            var folder = $"{FramesRoot}/{pair.Key}";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                Debug.LogWarning($"[PlayerAnimationSetup] 缺少动画目录：{folder}，跳过。");
                continue;
            }

            var clip = BuildClip(pair.Key, pair.Value.clip, folder, pair.Value.loop, pair.Value.fps);
            if (clip != null)
            {
                clips[pair.Key] = clip;
            }
        }

        return clips;
    }

    private static AnimationClip BuildClip(string animName, string clipName, string folder, bool loop, float fps)
    {
        float frameRate = fps > 0f ? fps : DefaultFrameRate;
        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
        var pngPaths = guids
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.EndsWith(".png"))
            .OrderBy(NaturalOrder)
            .ToList();

        if (pngPaths.Count == 0)
        {
            Debug.LogWarning($"[PlayerAnimationSetup] 目录 {folder} 内没有 png 帧，跳过。");
            return null;
        }

        foreach (var path in pngPaths)
        {
            EnsureSpriteImport(path);
        }
        AssetDatabase.Refresh();

        var keyframes = new ObjectReferenceKeyframe[pngPaths.Count];
        for (int i = 0; i < pngPaths.Count; i++)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(pngPaths[i]);
            keyframes[i] = new ObjectReferenceKeyframe
            {
                time = i / frameRate,
                value = sprite
            };
        }

        var clip = new AnimationClip { frameRate = frameRate };
        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        var clipPath = $"{OutputRoot}/{clipName}.anim";
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (existing != null)
        {
            // 覆盖重建时保留 Animation Event（Attack 开窗帧等由战斗系统手工打入，不能被冲掉）
            var preservedEvents = AnimationUtility.GetAnimationEvents(existing);
            EditorUtility.CopySerialized(clip, existing);
            Object.DestroyImmediate(clip);
            clip = existing;
            clip.name = clipName;
            if (preservedEvents != null && preservedEvents.Length > 0)
            {
                AnimationUtility.SetAnimationEvents(clip, preservedEvents);
            }
        }
        else
        {
            AssetDatabase.CreateAsset(clip, clipPath);
            clip.name = clipName;
        }

        Debug.Log($"[PlayerAnimationSetup] 生成 {clipName}（{pngPaths.Count} 帧, {(loop ? "循环" : "单次")}）");
        return clip;
    }

    /// <summary>自然排序：Player_Run_02 排在 Player_Run_10 之前。</summary>
    private static string NaturalOrder(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return Regex.Replace(name, @"\d+", m => m.Value.PadLeft(6, '0'));
    }

    // ===== 3. AnimatorController 生成 =====

    private static AnimatorController BuildController(Dictionary<string, AnimationClip> clips)
    {
        // 删除重建：历史重跑会在 .controller 里累积孤儿状态/转移块（含引用已删参数的死转移），
        // 重建保证文件只含当前图；同时恢复原 guid，保 prefab/场景引用不断。
        var oldGuid = AssetDatabase.AssetPathToGUID(ControllerPath);
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
        {
            AssetDatabase.DeleteAsset(ControllerPath);
        }

        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        if (!string.IsNullOrEmpty(oldGuid) && oldGuid != AssetDatabase.AssetPathToGUID(ControllerPath))
        {
            var metaPath = ControllerPath + ".meta";
            var meta = File.ReadAllText(metaPath);
            meta = new Regex(@"guid: \w+").Replace(meta, "guid: " + oldGuid, 1);
            File.WriteAllText(metaPath, meta);
            AssetDatabase.Refresh();
            controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Debug.Log($"[PlayerAnimationSetup] controller 已重建并恢复 guid {oldGuid}");
        }

        // 参数（与 PlayerAnimParams 常量一一对应）
        controller.parameters = new[]
        {
            new AnimatorControllerParameter { name = "Speed", type = AnimatorControllerParameterType.Float, defaultFloat = 0f },
            new AnimatorControllerParameter { name = "IsGrounded", type = AnimatorControllerParameterType.Bool, defaultBool = true },
            new AnimatorControllerParameter { name = "VelocityY", type = AnimatorControllerParameterType.Float, defaultFloat = 0f },
            new AnimatorControllerParameter { name = "Hurt", type = AnimatorControllerParameterType.Trigger },
            new AnimatorControllerParameter { name = "Death", type = AnimatorControllerParameterType.Trigger },
            new AnimatorControllerParameter { name = "Attack", type = AnimatorControllerParameterType.Trigger },
            new AnimatorControllerParameter { name = "Interact", type = AnimatorControllerParameterType.Trigger },
            new AnimatorControllerParameter { name = "Dash", type = AnimatorControllerParameterType.Trigger },
            new AnimatorControllerParameter { name = "Roll", type = AnimatorControllerParameterType.Trigger },
            new AnimatorControllerParameter { name = "Attack2", type = AnimatorControllerParameterType.Trigger },
            new AnimatorControllerParameter { name = "Attack3", type = AnimatorControllerParameterType.Trigger },
            new AnimatorControllerParameter { name = "AttackAir", type = AnimatorControllerParameterType.Trigger },
            new AnimatorControllerParameter { name = "HeavyAttack", type = AnimatorControllerParameterType.Trigger },
            new AnimatorControllerParameter { name = "Victory", type = AnimatorControllerParameterType.Trigger },
            new AnimatorControllerParameter { name = "Turn", type = AnimatorControllerParameterType.Trigger },
            new AnimatorControllerParameter { name = "WallJump", type = AnimatorControllerParameterType.Trigger },
            new AnimatorControllerParameter { name = "IsWallSliding", type = AnimatorControllerParameterType.Bool, defaultBool = false },
            new AnimatorControllerParameter { name = "IsLedgeGrab", type = AnimatorControllerParameterType.Bool, defaultBool = false },
            new AnimatorControllerParameter { name = "IsCrouching", type = AnimatorControllerParameterType.Bool, defaultBool = false },
            new AnimatorControllerParameter { name = "IsLongIdle", type = AnimatorControllerParameterType.Bool, defaultBool = false },
        };

        var root = controller.layers[0].stateMachine;
        root.states = new ChildAnimatorState[0];
        root.anyStateTransitions = new AnimatorStateTransition[0];
        root.entryTransitions = new AnimatorTransition[0];

        var idle = AddState(root, "Idle", GetClip(clips, "Player_Idle"), new Vector2(200, 0));
        var run = AddState(root, "Run", GetClip(clips, "Player_Run"), new Vector2(400, 0));
        var jump = AddState(root, "Jump", GetClip(clips, "Player_Jump"), new Vector2(400, 160));
        var fall = AddState(root, "Fall", GetClip(clips, "Player_Fall"), new Vector2(600, 160));
        var land = AddState(root, "Land", GetClip(clips, "Player_Land"), new Vector2(400, 320));
        var hurt = AddState(root, "Hurt", GetClip(clips, "Player_Hurt"), new Vector2(200, 480));
        var death = AddState(root, "Death", GetClip(clips, "Player_Die"), new Vector2(400, 480));
        root.defaultState = idle;

        // ---- 收尾补齐：一次性动作（trigger），播完回地面/空中 ----
        var interact = AddState(root, "Interact", GetClip(clips, "Player_Interact"), new Vector2(600, 0));
        var attack = AddState(root, "Attack", GetClip(clips, "Player_Attack01"), new Vector2(800, 0));
        var attack2 = AddState(root, "Attack02", GetClip(clips, "Player_Attack02"), new Vector2(1000, 0));
        var attack3 = AddState(root, "Attack03", GetClip(clips, "Player_Attack03"), new Vector2(1200, 0));
        var attackAir = AddState(root, "AttackAir", GetClip(clips, "Player_AttackAir"), new Vector2(800, 160));
        var heavy = AddState(root, "HeavyAttack", GetClip(clips, "Player_HeavyAttack"), new Vector2(1400, 0));
        var dash = AddState(root, "Dash", GetClip(clips, "Player_Dash"), new Vector2(600, 320));
        var roll = AddState(root, "Roll", GetClip(clips, "Player_Roll"), new Vector2(800, 320));
        var turn = AddState(root, "Turn", GetClip(clips, "Player_Turn"), new Vector2(1000, 320));
        var victory = AddState(root, "Victory", GetClip(clips, "Player_Victory"), new Vector2(1200, 320));
        var wallJump = AddState(root, "WallJump", GetClip(clips, "Player_WallJump"), new Vector2(600, 480));

        // ---- 持续型状态（bool 进出）----
        var wallSlide = AddState(root, "WallSlide", GetClip(clips, "Player_WallSlide"), new Vector2(800, 480));
        var ledgeGrab = AddState(root, "LedgeGrab", GetClip(clips, "Player_LedgeGrab"), new Vector2(1000, 480));
        var crouch = AddState(root, "Crouch", GetClip(clips, "Player_Crouch"), new Vector2(1200, 480));
        var longIdle = AddState(root, "Idle02", GetClip(clips, "Player_Idle02"), new Vector2(1400, 480));

        // ---- 地面移动循环 ----
        Link(idle, run, t => t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed"));
        Link(run, idle, t => t.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed"));

        // ---- 起跳 / 离地 ----
        Link(idle, jump, t => t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsGrounded"));
        Link(run, jump, t => t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsGrounded"));
        Link(jump, fall, t => t.AddCondition(AnimatorConditionMode.Less, -0.1f, "VelocityY"));

        // ---- 落地 ----
        Link(jump, land, t => t.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded"));
        Link(fall, land, t => t.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded"));

        // ---- Land 动画播完回待机；中途离地回空中 ----
        Link(land, idle, t =>
        {
            t.hasExitTime = true;
            t.exitTime = 0.9f;
        });
        Link(land, jump, t => t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsGrounded"));

        // ---- 收尾补齐接线 ----
        // 持续型状态（bool 由 Driver/状态机每帧写入，进出场即时响应）
        foreach (var from in new[] { idle, run })
        {
            Link(from, wallSlide, t => t.AddCondition(AnimatorConditionMode.If, 0, "IsWallSliding"));
            Link(from, ledgeGrab, t => t.AddCondition(AnimatorConditionMode.If, 0, "IsLedgeGrab"));
            Link(from, crouch, t => t.AddCondition(AnimatorConditionMode.If, 0, "IsCrouching"));
            Link(from, longIdle, t => t.AddCondition(AnimatorConditionMode.If, 0, "IsLongIdle"));
        }
        // 持续型 -> 退出条件解除即回地面循环
        Link(wallSlide, idle, t => t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsWallSliding"));
        Link(ledgeGrab, idle, t => t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsLedgeGrab"));
        Link(crouch, idle, t => t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouching"));
        Link(longIdle, idle, t => t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsLongIdle"));
        // 长待机被任何移动打断
        Link(longIdle, run, t => t.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed"));
        Link(longIdle, jump, t => t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsGrounded"));

        // 墙跳：从贴墙下滑或空中触发
        Link(wallSlide, wallJump, t => t.AddCondition(AnimatorConditionMode.If, 0, "WallJump"));
        Link(fall, wallJump, t => t.AddCondition(AnimatorConditionMode.If, 0, "WallJump"));
        Link(wallJump, fall, t =>
        {
            t.hasExitTime = true;
            t.exitTime = 0.9f;
        });

        // 一次性动作：AnyState 进入（trigger），播完回地面/空中
        var oneShots = new[]
        {
            (state: interact, trigger: "Interact"), (state: attack, trigger: "Attack"),
            (state: attack2, trigger: "Attack2"), (state: attack3, trigger: "Attack3"),
            (state: attackAir, trigger: "AttackAir"), (state: heavy, trigger: "HeavyAttack"),
            (state: dash, trigger: "Dash"), (state: roll, trigger: "Roll"),
            (state: turn, trigger: "Turn"), (state: victory, trigger: "Victory"),
        };
        foreach (var (state, trigger) in oneShots)
        {
            var any = root.AddAnyStateTransition(state);
            any.hasExitTime = false;
            any.duration = 0.02f;
            any.AddCondition(AnimatorConditionMode.If, 0, trigger);

            // 播完回地面或空中（Victory 循环播，由过关流程直接切场景）
            if (state != victory)
            {
                Link(state, idle, t =>
                {
                    t.hasExitTime = true;
                    t.exitTime = 0.95f;
                });
                Link(state, jump, t =>
                {
                    t.hasExitTime = true;
                    t.exitTime = 0.95f;
                    t.AddCondition(AnimatorConditionMode.IfNot, 0, "IsGrounded");
                });
            }
        }

        // 连段链：Attack 结束窗口可无缝接 Attack02 -> Attack03（玩家按攻击键触发下一段）
        Link(attack, attack2, t => t.AddCondition(AnimatorConditionMode.If, 0, "Attack2"));
        Link(attack2, attack3, t => t.AddCondition(AnimatorConditionMode.If, 0, "Attack3"));

        // ---- Hurt：任意状态进入，播放完回 Idle ----
        foreach (var state in new[] { idle, run, jump, fall, land })
        {
            var t = root.AddAnyStateTransition(hurt);
            t.hasExitTime = false;
            t.duration = 0.02f;
            t.AddCondition(AnimatorConditionMode.If, 0, "Hurt");
        }

        Link(hurt, idle, t =>
        {
            t.hasExitTime = true;
            t.exitTime = 0.95f;
        });

        // ---- Death：任意状态进入，不退出（由重生逻辑复位） ----
        foreach (var state in new[] { idle, run, jump, fall, land, hurt })
        {
            var t = root.AddAnyStateTransition(death);
            t.hasExitTime = false;
            t.duration = 0.02f;
            t.AddCondition(AnimatorConditionMode.If, 0, "Death");
        }

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static AnimatorState AddState(AnimatorStateMachine root, string name, AnimationClip clip, Vector2 position)
    {
        var state = root.AddState(name, position);
        state.motion = clip;
        return state;
    }

    private static void Link(AnimatorState from, AnimatorState to, System.Action<AnimatorStateTransition> setup)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = 0.05f;
        t.interruptionSource = TransitionInterruptionSource.Source;
        setup?.Invoke(t);
    }

    private static AnimationClip GetClip(Dictionary<string, AnimationClip> clips, string key)
    {
        if (clips.TryGetValue(key, out var clip))
        {
            return clip;
        }

        Debug.LogError($"[PlayerAnimationSetup] 缺少 clip {key}，动画状态将为空。");
        return null;
    }

    // ===== 4. Player 预制体 =====

    private static void BuildPrefab(AnimatorController controller)
    {
        // 用 Idle 第一帧做占位显示（外观由美术帧决定，尺寸由碰撞体决定）
        var idleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{OutputRoot}/Idle.anim");
        Sprite displaySprite = null;
        if (idleClip != null)
        {
            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            var keys = AnimationUtility.GetObjectReferenceCurve(idleClip, binding);
            if (keys != null && keys.Length > 0)
            {
                displaySprite = keys[0].value as Sprite;
            }
        }

        var go = new GameObject("Player");
        var sprite = go.AddComponent<SpriteRenderer>();
        sprite.sprite = displaySprite;

        var body = go.AddComponent<Rigidbody2D>();
        body.freezeRotation = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var collider = go.AddComponent<BoxCollider2D>();
        if (displaySprite != null)
        {
            var size = displaySprite.bounds.size * 0.9f; // 碰撞体略小于显示，宽容手感
            collider.size = size;
            collider.offset = new Vector2(0f, -displaySprite.bounds.size.y * 0.05f);
        }

        go.AddComponent<Animator>().runtimeAnimatorController = controller;

        // 本工程的 PlayerController 只持有 Animator 引用，参数不走 SO 配置，
        // 因此模板中的 _config 注入与 PlayerAnimatorDriver 挂载在此一并裁剪。
        go.AddComponent<PlayerController>();

        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (existing != null)
        {
            // 覆盖：保留已有预制体资产身份
            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);
        }
        else
        {
            PrefabUtility.SaveAsPrefabAssetAndConnect(go, PrefabPath, InteractionMode.AutomatedAction);
        }

        Debug.Log($"[PlayerAnimationSetup] 预制体已生成 {PrefabPath}");
    }

    // ===== 工具 =====

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        var parts = path.Split('/');
        var current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            var next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }
}