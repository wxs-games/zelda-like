using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 命令行出包入口（batchmode 用）。
/// 存在的理由：切换 activeInputHandler 会让编辑器触发全量重编译，
/// GUI 模式下容易在编译中被中断导致 Unity 进程退出、构建请求丢失。
/// 批处理模式没有这个干扰，一条命令走到底。
///
/// 用法：
///   Unity.exe -quit -batchmode -nographics -projectPath <工程根>
///            -executeMethod BuildEntry.BuildAndroid
///            -buildOutput <apk 完整路径>
///            -logFile <日志路径>
/// </summary>
public static class BuildEntry
{
    private const string Scenes =
        "Assets/Scenes/SampleScene.unity,Assets/Scenes/ForestArea.unity";

    /// <summary>从命令行读取 -buildOutput，缺省写到工程同级 Builds 目录。</summary>
    private static string ResolveOutputPath()
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-buildOutput") return args[i + 1];
        }

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string workspace = Directory.GetParent(projectRoot).FullName;
        string dir = Path.Combine(workspace, "Builds");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "ZeldaLike_batch.apk");
    }

    public static void BuildAndroid()
    {
        string output = ResolveOutputPath();
        var options = new BuildPlayerOptions
        {
            scenes = Scenes.Split(','),
            locationPathName = output,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(options);
        var s = report.summary;

        Debug.Log($"[BuildEntry] result={s.result} output={s.outputPath} " +
                  $"size={s.totalSize / 1048576}MB errors={s.totalErrors} warnings={s.totalWarnings}");

        // batchmode 下必须用明确的退出码，否则外层脚本分不清成功失败
        EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
    }
}