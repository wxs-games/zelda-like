using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 玩家装备外观动画管线（幂等，可重复运行）。
///
/// 做什么：
///   1. 从 Assets/Sprites/Player/ 下 4 张派生图集切出帧，生成 12 段 clip
///      （每个外观各一套 Idle / Move / Attack），外加 2 段举盾 Idle；
///   2. 用 AnimatorOverrideController 把这三段动画替换成对应外观的 clip，
///      状态机图与参数全部继承 Play.controller，不复制、不改图；
///   3. 把 PlayerShield / PlayerEquipmentVisual 挂到 GameRoot 预制体的 Player 上并接线。
///
/// 菜单：Tools → Setup → Player Equipment Art
///
/// 【为什么要 Override 而不是新建 controller】
///   Play.controller 里还有第二层 ArcherLayer（弓箭瞄准用），复制一份会导致
///   两套 controller 各自维护、参数漂移。Override 只换 clip，其它全部继承。
/// </summary>
public static class PlayerEquipmentArtSetup
{
    private const string BaseControllerPath = "Assets/Animation/Play.controller";
    private const string IdleClipPath = "Assets/Animation/Idle.anim";
    private const string MoveClipPath = "Assets/Animation/Waking.anim";
    private const string AttackClipPath = "Assets/Sprites/Animations/Slash_player.anim";

    private const string SheetsDir = "Assets/Sprites/Player";
    private const string OutputDir = "Assets/Animations/PlayerEquip";
    private const string PrefabPath = "Assets/Prefabs/GameRoot.prefab";

    /// <summary>各段动画在图集里的帧序号（行/列换算后）。取自原始 clip 的实际帧序列。</summary>
    private static readonly int[] IdleFrames = { 5, 5, 2, 3, 4, 5, 5 };
    private static readonly int[] MoveFrames = { 6, 7, 8, 9, 10, 11, 6 };
    private static readonly int[] AttackFrames = { 12, 13, 14, 15, 16, 17, 17 };

    /// <summary>外观名 → 图集文件名前缀。与 gen_assets.py 的产物一一对应。</summary>
    private static readonly (string key, string sheet)[] Variants =
    {
        ("SwordShield", null),                   // 原图集，不派生
        ("Sword", "Warrior_Blue_Sword"),
        ("Shield", "Warrior_Blue_Shield"),
        ("Bare", "Warrior_Blue_NoWeapon"),
    };

    [MenuItem("Tools/Setup/Player Equipment Art")]
    public static void Run()
    {
        EnsureFolder(OutputDir);

        var baseCtrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(BaseControllerPath);
        var idleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(IdleClipPath);
        var moveClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(MoveClipPath);
        var attackClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AttackClipPath);
        if (baseCtrl == null || idleClip == null || moveClip == null || attackClip == null)
        {
            Debug.LogError("[PlayerEquipmentArtSetup] 缺少基础 controller 或原始 clip，中止。");
            return;
        }

        var result = new Dictionary<string, RuntimeAnimatorController>();

        // ---- 各装备外观 ----
        foreach (var (key, sheet) in Variants)
        {
            if (sheet == null)
            {
                // 剑+盾 = 原图集，直接用基础 controller（无需 override）
                result[key] = baseCtrl;
                continue;
            }

            var table = LoadSprites($"{SheetsDir}/{sheet}.png");
            if (table == null) continue;

            var idle = BuildClip($"{key}_Idle", table, IdleFrames, true);
            var move = BuildClip($"{key}_Move", table, MoveFrames, true);
            var attack = BuildClip($"{key}_Attack", table, AttackFrames, false);

            result[key] = BuildOverride(baseCtrl, $"{OutputDir}/PlayerEquip_{key}.overrideController",
                idleClip, idle, moveClip, move, attackClip, attack);
        }

        // ---- 举盾姿态（只换 Idle；举盾时玩家站桩，当前状态必为 Idle） ----
        result["BlockSwordShield"] = BuildBlockOverride(baseCtrl, idleClip,
            "Warrior_Blue_Block", "BlockSwordShield");
        result["BlockShield"] = BuildBlockOverride(baseCtrl, idleClip,
            "Warrior_Blue_BlockShield", "BlockShield");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        WirePrefab(result);
        Debug.Log("[PlayerEquipmentArtSetup] 装备外观管线完成 ✓");
    }

    // ===== clip 生成 =====

    private static AnimationClip BuildClip(string name, Sprite[] table, int[] frames, bool loop)
    {
        const float step = 0.1f;   // 与 Idle.anim 的 7 帧节奏一致
        var keys = new ObjectReferenceKeyframe[frames.Length];
        for (int i = 0; i < frames.Length; i++)
        {
            var sprite = table[frames[i]];
            if (sprite == null)
            {
                Debug.LogError($"[PlayerEquipmentArtSetup] {name} 缺少第 {frames[i]} 帧");
                return null;
            }
            keys[i] = new ObjectReferenceKeyframe { time = i * step, value = sprite };
        }

        var clip = new AnimationClip { frameRate = 10f, name = name };
        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        var path = $"{OutputDir}/{name}.anim";
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(clip, existing);
            Object.DestroyImmediate(clip);
            existing.name = name;
            return existing;
        }
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    // ===== override 生成 =====

    private static RuntimeAnimatorController BuildOverride(
        AnimatorController baseCtrl, string path,
        AnimationClip o1, AnimationClip n1,
        AnimationClip o2, AnimationClip n2,
        AnimationClip o3, AnimationClip n3)
    {
        if (n1 == null || n2 == null || n3 == null) return null;
        var ov = LoadOrCreateOverride(baseCtrl, path);
        var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        ov.GetOverrides(pairs);
        for (int i = 0; i < pairs.Count; i++)
        {
            var k = pairs[i].Key;
            if (k == o1) pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(k, n1);
            else if (k == o2) pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(k, n2);
            else if (k == o3) pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(k, n3);
        }
        ov.ApplyOverrides(pairs);
        EditorUtility.SetDirty(ov);
        return ov;
    }

    private static RuntimeAnimatorController BuildBlockOverride(
        AnimatorController baseCtrl, AnimationClip idleClip, string sheet, string key)
    {
        var table = LoadSprites($"{SheetsDir}/{sheet}.png");
        if (table == null) return null;
        var blockClip = BuildClip($"{key}_Block", table, IdleFrames, true);
        if (blockClip == null) return null;

        var ov = LoadOrCreateOverride(baseCtrl, $"{OutputDir}/PlayerEquip_{key}.overrideController");
        var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        ov.GetOverrides(pairs);
        for (int i = 0; i < pairs.Count; i++)
        {
            if (pairs[i].Key == idleClip)
                pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(idleClip, blockClip);
        }
        ov.ApplyOverrides(pairs);
        EditorUtility.SetDirty(ov);
        return ov;
    }

    private static AnimatorOverrideController LoadOrCreateOverride(AnimatorController baseCtrl, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
        if (existing != null)
        {
            // 复用资产：保留 guid，prefab/预制体上的引用不会断
            existing.runtimeAnimatorController = baseCtrl;
            return existing;
        }
        var created = new AnimatorOverrideController(baseCtrl);
        AssetDatabase.CreateAsset(created, path);
        return created;
    }

    // ===== 预制体接线 =====

    private static void WirePrefab(Dictionary<string, RuntimeAnimatorController> ctrls)
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform player = FindDeep(root.transform, "Player");
            if (player == null)
            {
                Debug.LogError("[PlayerEquipmentArtSetup] GameRoot 里找不到 Player 节点");
                return;
            }

            var shield = player.GetComponent<PlayerShield>();
            if (shield == null) shield = player.gameObject.AddComponent<PlayerShield>();

            var visual = player.GetComponent<PlayerEquipmentVisual>();
            if (visual == null) visual = player.gameObject.AddComponent<PlayerEquipmentVisual>();

            visual.withSwordAndShield = Get(ctrls, "SwordShield");
            visual.swordOnly = Get(ctrls, "Sword");
            visual.shieldOnly = Get(ctrls, "Shield");
            visual.bare = Get(ctrls, "Bare");
            visual.blockWithSword = Get(ctrls, "BlockSwordShield");
            visual.blockShieldOnly = Get(ctrls, "BlockShield");

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[PlayerEquipmentArtSetup] 已在 GameRoot/Player 上挂好盾牌与外观组件");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
    }

    private static RuntimeAnimatorController Get(Dictionary<string, RuntimeAnimatorController> d, string key)
    {
        return d.TryGetValue(key, out var v) ? v : null;
    }

    private static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t)
        {
            var r = FindDeep(c, name);
            if (r != null) return r;
        }
        return null;
    }

    // ===== 工具 =====

    /// <summary>按名字里的序号取整张图集的 48 个 Sprite，返回下标=帧序号的数组。</summary>
    private static Sprite[] LoadSprites(string path)
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath(path);
        var table = new Sprite[48];
        int count = 0;
        foreach (var a in assets)
        {
            if (a is Sprite sp)
            {
                int idx = ParseIndex(sp.name);
                if (idx >= 0 && idx < table.Length && table[idx] == null)
                {
                    table[idx] = sp;
                    count++;
                }
            }
        }
        if (count < 48)
        {
            Debug.LogWarning($"[PlayerEquipmentArtSetup] {path} 只解析到 {count}/48 个 Sprite，检查切片设置");
            return null;
        }
        return table;
    }

    private static int ParseIndex(string name)
    {
        int i = name.LastIndexOf('_');
        if (i < 0) return -1;
        return int.TryParse(name.Substring(i + 1), out int v) ? v : -1;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parts = path.Split('/');
        var cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            var next = $"{cur}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }
}
