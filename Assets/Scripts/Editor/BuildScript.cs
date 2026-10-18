using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 菜单 Build/出包：一键出 Android APK / AAB，不再手点 Build Settings。
///
/// 解决的问题：手动出包最容易漏三件事——
///   1) 忘记切到 Android 平台（能在 Standalone 下点 Build Android 吗？不能，会报错）；
///   2) 忘记带场景列表（空列表出包 = 白屏，且 Unity 不一定报错）；
///   3) 版本号不递增导致无法区分包（覆盖安装时用户拿到的还是旧包）。
/// 本脚本把这三件事强制流程化。
///
/// 约定：产物输出到工程根下的 Builds/（已被 .gitignore 的 `[Bb]uilds/` 排除）。
/// </summary>
public static class BuildScript
{
    /// <summary>
    /// 产物目录。Application.dataPath 是「工程根/Assets」，所以这里取parent 得工程根。
    /// 放工程根而不是工程外，是为了让出包目录跟着工程走（换机器拷贝工程时一起带走）。
    /// </summary>
    private const string BuildRoot = "../Builds";

    [MenuItem("Build/出包/Android APK (内测)", priority = 0)]
    public static void BuildApk() { BuildAndroid(appBundle: false); }

    [MenuItem("Build/出包/Android AAB (上架)", priority = 1)]
    public static void BuildAab() { BuildAndroid(appBundle: true); }

    /// <summary>
    /// 命令行出包入口（batchmode）：
    ///   Unity.exe -quit -batchmode -projectPath &lt;工程根&gt; \
    ///            -executeMethod BuildScript.BuildApkBatch -logFile &lt;日志路径&gt;
    ///
    /// 为什么要单独留一个方法：MCP 的 execute_menu_item 会在 Unity 主线程上投递委托，
    /// 一旦此前有未完成的 MCP 命令把主循环卡住（实测：GameObjectSerializer 序列化
    /// Animator 抛异常后主线程再也不回来），菜单命令就永远不执行，且没有任何提示。
    /// 批处理模式不依赖编辑器主循环是否健康，是出包的可靠路径。
    /// 退出码：0 成功，1 失败。
    /// </summary>
    public static void BuildApkBatch()
    {
        try
        {
            if (BuildAndroid(appBundle: false))
            {
                Debug.Log("[BuildCli] 成功");
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError("[BuildCli] 失败");
                EditorApplication.Exit(1);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[BuildCli] 异常：{e}");
            EditorApplication.Exit(1);
        }
    }

    /// <summary>检查出包前置条件，返回错误列表；为空表示可以出包。</summary>
    [MenuItem("Build/检查出包配置", priority = 20)]
    public static void CheckConfiguration()
    {
        var problems = CollectProblems();
        if (problems.Count == 0)
        {
            Debug.Log("[Build] 出包配置检查通过。");
            return;
        }
        Debug.LogError("[Build] 出包配置存在以下问题：\n  - " + string.Join("\n  - ", problems));
    }

    private static List<string> CollectProblems()
    {
        var problems = new List<string>();

        var pkg = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
        if (string.IsNullOrEmpty(pkg) || !pkg.Contains("."))
            problems.Add("Android 包名为空或格式非法（当前：'" + pkg + "'）。执行菜单 Tools/Setup/Configure Mobile。");

        if (string.IsNullOrEmpty(PlayerSettings.productName) || PlayerSettings.productName == "111")
            problems.Add("产品名仍是默认占位值。执行菜单 Tools/Setup/Configure Mobile。");

        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).ToList();
        if (scenes.Count == 0)
            problems.Add("Build Settings 里没有任何启用的场景，出包会白屏。");

        // 存档系统依赖 GameDatabase 资产；缺失时读档会直接报错
        var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/Settings/GameDatabase.asset");
        if (db == null)
            problems.Add("缺少 Assets/Settings/GameDatabase.asset，执行菜单 Tools/Setup/Build Game Database。");
        else if (db.items.Count == 0 && db.quests.Count == 0)
            problems.Add("GameDatabase 是空的，执行菜单 Tools/Setup/Build Game Database 重新扫描。");

        // 图形 API 双份编译会顶满并行进程池并死锁，这里出包前明确要求单 API。
        if (PlayerSettings.GetGraphicsAPIs(BuildTarget.Android).Length > 1)
        {
            problems.Add(
                "Android 图形 API 配了多个（" +
                string.Join("+", PlayerSettings.GetGraphicsAPIs(BuildTarget.Android)) +
                "），会让每个 shader 编译多遍、多核机器上并行进程数顶满后死锁" +
                "（判据：UnityShaderCompiler 数量稳定且 20 秒 CPU 零增长）。" +
                "执行菜单 Tools/Setup/Configure Mobile 收窄为单 API。");
        }

        return problems;
    }

    /// <summary>出 Android 包。返回 true=成功，false=失败（供命令行入口判断退出码）。</summary>
    private static bool BuildAndroid(bool appBundle)
    {
        var problems = CollectProblems();
        if (problems.Count > 0)
        {
            Debug.LogError("[Build] 已中止出包，请先修复：\n  - " + string.Join("\n  - ", problems));
            return false;
        }

        // 0) 检查僵死的 ShaderCompiler 残留。
        //    切换构建平台时上一次的 UnityShaderCompiler 可能残留为僵尸进程，
        //    此时 BuildPlayer 会一直等它们的 IPC 而永不返回（Unity 的 Responding 仍显示 True，
        //    但主线程 CPU 与子进程 CPU 均不再增长）。这里提前拦一次并给出明确处置指引。
        if (HasZombieShaderCompilers())
        {
            Debug.LogError(
                "[Build] 检测到多个 UnityShaderCompiler 残留进程，出包很可能死锁。\n" +
                "  请先在任务管理器结束全部 UnityShaderCompiler.exe，然后重新执行本菜单。\n" +
                "  （PowerShell：Get-Process UnityShaderCompiler | Stop-Process -Force）");
            return false;
        }

        // 1) 强制切到 Android 平台
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            Debug.Log("[Build] 正在切换到 Android 平台，请稍候…");
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            Debug.LogWarning("[Build] 平台切换后请再次执行本菜单以完成出包。");
            return false;
        }

        // 2) 版本号递增，避免出包互相覆盖
        var versionCode = ReadAndroidVersionCode() + 1;
        WriteAndroidVersionCode(versionCode);

        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
        var suffix = appBundle ? "aab" : "apk";
        var fileName = $"{PlayerSettings.productName}_v{PlayerSettings.bundleVersion}_c{versionCode}_{stamp}.{suffix}";
        var outputPath = ResolveBuildPath(fileName);

        EditorUserBuildSettings.buildAppBundle = appBundle;
        EditorUserBuildSettings.development = false;
        SetCreateSymbols();

        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled)
            .Select(s => new EditorBuildSettingsScene(s.path, true)).ToArray();

        Debug.Log($"[Build] 开始出包：{fileName}\n" +
                  $"  平台 Android / 版本 {PlayerSettings.bundleVersion} (code {versionCode})\n" +
                  $"  场景 {scenes.Length} 个 / 类型 {(appBundle ? "AAB" : "APK")}\n" +
                  $"  输出 {outputPath}");

        var options = BuildOptions.None;
        var report = BuildPipeline.BuildPlayer(scenes, outputPath, BuildTarget.Android, options);
        Summarize(report, outputPath);
        return report.summary.result == BuildResult.Succeeded;
    }

    private static void Summarize(BuildReport report, string outputPath)
    {
        var s = report.summary;
        if (s.result == BuildResult.Succeeded)
        {
            var sizeMb = s.totalSize / (1024f * 1024f);
            Debug.Log($"[Build] 出包成功：{outputPath}\n" +
                      $"  耗时 {s.totalTime.TotalSeconds:F1}s / 大小 {sizeMb:F1} MB / 错误 {s.totalErrors}");
        }
        else
        {
            Debug.LogError($"[Build] 出包失败（{s.result}）：错误 {s.totalErrors} / 警告 {s.totalWarnings}");
            foreach (var step in report.steps)
            {
                foreach (var msg in step.messages)
                {
                    if (msg.type == LogType.Error || msg.type == LogType.Exception)
                        Debug.LogError($"  [{step.name}] {msg.content}");
                }
            }
        }
    }

    /// <summary>
    /// 是否存在疑似僵死的 UnityShaderCompiler。
    /// 正常出包时编译进程数量有限且 CPU 持续增长；切平台后残留的一批会长期闲置，
    /// 导致 BuildPlayer 死等。这里用「数量」做粗筛（阈值取 32，远高于正常峰值）。
    /// </summary>
    private static bool HasZombieShaderCompilers()
    {
        try
        {
            var procs = System.Diagnostics.Process.GetProcessesByName("UnityShaderCompiler");
            int count = procs.Length;
            foreach (var p in procs) p.Dispose();
            return count > 32;
        }
        catch
        {
            return false; // 取不到就放行，别因为探测失败拦住出包
        }
    }

    private static string ResolveBuildPath(string fileName)
    {
        var dir = Path.GetFullPath(Path.Combine(Application.dataPath, BuildRoot));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, fileName);
    }

    // ===== versionCode 读写：走反射，Android 模块未加载时也不报错 =====

    /// <summary>
    /// 出 Release 包时保留符号表，否则线上崩溃拿不到可解析的堆栈。
    /// AndroidCreateSymbols 的枚举值各 Unity 版本不一致，统一走反射 + 名称匹配。
    /// </summary>
    private static void SetCreateSymbols()
    {
        var prop = typeof(EditorUserBuildSettings).GetProperty(
            "androidCreateSymbols", BindingFlags.Public | BindingFlags.Static);
        if (prop == null || !prop.PropertyType.IsEnum) return;

        foreach (var candidate in new[] { "Public", "DebugInfo", "Full" })
        {
            if (!Enum.IsDefined(prop.PropertyType, candidate)) continue;
            try { prop.SetValue(null, Enum.Parse(prop.PropertyType, candidate)); return; }
            catch { /* 换下一个候选 */ }
        }
    }

    private static int ReadAndroidVersionCode()
    {
        var t = FindType("UnityEditor.AndroidPlayerSettings");
        if (t == null) return 0;
        var p = t.GetProperty("bundleVersionCode", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (p == null) return 0;
        try { return Convert.ToInt32(p.GetValue(null)); } catch { return 0; }
    }

    private static void WriteAndroidVersionCode(int value)
    {
        var t = FindType("UnityEditor.AndroidPlayerSettings");
        if (t == null) return;
        var p = t.GetProperty("bundleVersionCode", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (p == null) return;
        try { p.SetValue(null, value); } catch { }
    }

    private static Type FindType(string name)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType(name);
            if (t != null) return t;
        }
        return null;
    }
}