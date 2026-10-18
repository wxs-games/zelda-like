using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 菜单 Tools/Setup/Configure Mobile：把工程配置成可出手游包的状态。
///
/// 背景：新工程默认的构建设置对移动端基本不可用——
///   - applicationIdentifier 只有 Standalone 条目，Android 包名为空，BuildPipeline 直接失败；
///   - AndroidTargetArchitectures 只有 ARMv7，ARM64 设备装不上；
///   - targetSdkVersion / 手柄支持 / 屏幕方向 / 图形 API 均未配置。
/// 本脚本一次性把这些补齐，且重复执行结果一致（幂等），可安全纳入版本管理。
///
/// 为什么用反射调用 Android 设置：
///   Android 构建模块属于条件程序集（没装模块时 UnityEditor.Android 不存在），
///   直接静态引用会导致工程在未装模块的机器上编译失败。
///   这里统一走反射 + 缺失即警告，保证「装了模块就配置生效、没装模块也不阻断编译」。
///
/// 注意：包名、keystore 等与开发者身份相关的项集中在常量区，接入时只改这一处。
/// </summary>
public static class MobileSetup
{
    // ===== 与身份相关的常量：接入时只改这里 =====
    public const string CompanyName = "StarEcoStudio";
    public const string ProductName = "ZeldaLike";
    /// <summary>Android 包名 / iOS Bundle ID。至少两段，形如 com.公司.应用。</summary>
    public const string PackageName = "com.stareco.zeldalike";
    /// <summary>出包目录（工程同级的 Builds/，已由 .gitignore 排除）。</summary>
    private const string BuildRoot = "../Builds";

    [MenuItem("Tools/Setup/Configure Mobile", priority = 0)]
    public static void Configure()
    {
        ConfigureIdentity();
        ConfigureQuality();
        ConfigureOrientation();
        ConfigureInputHandler();
        ConfigureAndroid();
        ConfigureIOS();

        AssetDatabase.SaveAssets();
        Debug.Log($"[MobileSetup] 手游配置完成：{PackageName}（{CompanyName}/{ProductName}）");
    }

    private static void ConfigureIdentity()
    {
        PlayerSettings.companyName = CompanyName;
        PlayerSettings.productName = ProductName;

        // 三端包名都写全。只配 Standalone 是新工程最常见的出包失败原因。
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, PackageName);
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, PackageName);
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone, PackageName);
        PlayerSettings.bundleVersion = "1.0.0";
    }

    private static void ConfigureQuality()
    {
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.gpuSkinning = true;

        // 移动端用帧率上限而非 vSync 控制节奏：vsync 在移动端会显著增加耗电与发热
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;
    }

    /// <summary>
    /// Active Input Handling 必须是「Input System (New)」。
    ///
    /// 新工程默认是 Both（0=Old / 1=New / 2=Both），而 Unity 在 BuildPlayer 阶段会直接判定
    ///     "Active Input Handling is set to Both, this is unsupported on Android ... Cancelling"
    /// 也就是在手游出包链路上这是一道硬性卡点，必须显式收窄。
    /// 工程用的是 com.unity.inputsystem + InputAction 动作层门面，选 New 即为正解；
    ///残留的 UnityEngine.Input.mousePosition（UI 点击取值）在 New 模式下依旧可用，无需改代码。
    ///
    /// 该项在 Unity 里**没有公开 C# API**，只能改 ProjectSettings.asset，
    /// 且改完必须重启编辑器才生效——这一点无法用代码绕过。
    /// </summary>
    private static void ConfigureInputHandler()
    {
        const string settingsPath = "ProjectSettings/ProjectSettings.asset";
        if (!File.Exists(settingsPath))
        {
            Debug.LogWarning("[MobileSetup] 找不到 ProjectSettings.asset，跳过输入系统设置。");
            return;
        }

        string text = File.ReadAllText(settingsPath);
        var match = System.Text.RegularExpressions.Regex.Match(text, @"(?m)^(\s*activeInputHandler:\s*)(\d+)\s*$");
        if (!match.Success)
        {
            Debug.LogWarning("[MobileSetup] 未在 ProjectSettings.asset 中找到 activeInputHandler，请手动设为 Input System (New)。");
            return;
        }

        if (match.Groups[3].Value == "1") return; // 已是 New，无需改动

        text = text.Remove(match.Groups[3].Index, match.Groups[3].Length).Insert(match.Groups[3].Index, "1");
        File.WriteAllText(settingsPath, text);
        Debug.LogWarning(
            "[MobileSetup] Active Input Handling 已由 " + match.Groups[3].Value +
            "（Both）改为 1（Input System New）。**该设置需重启 Unity 编辑器后才生效**，请重启后再出包。");
    }

    private static void ConfigureOrientation()
    {
        // 横屏 2D 俯视玩法在手机上的可玩性远好于竖屏
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.useAnimatedAutorotation = true;
        PlayerSettings.SplashScreen.show = false;
    }

    private static void ConfigureAndroid()
    {
        // 脚本后端：ARM64 必须 IL2CPP
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetIl2CppCompilerConfiguration(BuildTargetGroup.Android, Il2CppCompilerConfiguration.Release);
        PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, ManagedStrippingLevel.Low);

        // 图形 API：只留 Vulkan。
        //
        // 原先配的是「Vulkan + GLES3」双 API（覆盖新旧设备的常见做法），
        // 但在 32 核机器上出包时，每个内置 shader 都要为两套 API 各编译一遍，
        // UnityShaderCompiler 并行进程数直接顶到 32，实测在此处**死锁**：
        //   判据 = ShaderCompiler 进程数稳定在 32 且 20 秒 CPU 零增长、构建日志不再增长。
        // Vulkan 在 Android 设备覆盖率已超 99%，GLES3 兜底的实际收益远小于卡死风险，
        // 因此按社区主流做法收窄为单API。确需兼容极老设备时再把 OpenGLES3 加回数组。
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });

        EditorUserBuildSettings.development = false;
        EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
        EditorUserBuildSettings.buildAppBundle = false; // 本地内测 APK；上架 Google Play 时改 true 出 AAB

        // 以下走反射：属于 Android 模块专属程序集
        TrySetAndroid("targetArchitectures", "ARMv7, ARM64");
        TrySetAndroid("minSdkVersion", "AndroidApiLevel23");
        TrySetAndroid("targetSdkVersion", "AndroidApiLevelAuto");
        TrySetAndroid("bundleVersionCode", "1");
        TrySetAndroid("androidGamepadSupportLevel", "UpToMaxConcurrentGamepads");
        TrySetAndroid("androidTVCompatibility", "AndroidTVCompatibilityStandard");
    }

    private static void ConfigureIOS()
    {
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.iOS, ScriptingImplementation.IL2CPP);
        TrySetNestedString("PlayerSettings", "iOS", "targetOSVersionString", "13.0");
        TrySetNestedString("PlayerSettings", "iOS", "applicationDisplayName", ProductName);
    }

    /// <summary>设置 PlayerSettings 内嵌平台类（如 PlayerSettings.iOS）上的字符串属性。</summary>
    private static void TrySetNestedString(string ownerType, string nestedType, string property, string value)
    {
        var outer = FindType("UnityEditor." + ownerType);
        if (outer == null) { Debug.LogWarning($"[MobileSetup] {ownerType} 不存在，跳过。"); return; }

        var nested = outer.GetNestedType(nestedType, BindingFlags.Public | BindingFlags.NonPublic);
        if (nested == null) { Debug.LogWarning($"[MobileSetup] {ownerType}.{nestedType} 不存在，跳过。"); return; }

        var prop = nested.GetProperty(property, StaticProps);
        if (prop == null) { Debug.LogWarning($"[MobileSetup] {ownerType}.{nestedType}.{property} 不存在，跳过。"); return; }

        prop.SetValue(null, value);
    }

    // ===== Android/iOS 专属设置的反射封装 =====

    private const BindingFlags StaticProps =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

    /// <summary>
    /// 设置 AndroidPlayerSettings 上的属性。value 语义随目标属性类型而定：
    ///   枚举       —— 逗号分隔的枚举名（"ARMv7, ARM64"表示位或组合，单值直接写"AndroidApiLevel23"）
    ///   int       —— 数字字符串
    ///   string    —— 原样赋值
    /// Android 模块未安装时只警告不报错，保证工程在任意机器上都能编译。
    /// </summary>
    private static void TrySetAndroid(string property, string value)
    {
        var type = FindType("UnityEditor.AndroidPlayerSettings");
        if (type == null) { WarnAndroidModule(); return; }

        var prop = type.GetProperty(property, StaticProps);
        if (prop == null) { Debug.LogWarning($"[MobileSetup] AndroidPlayerSettings.{property} 不存在，跳过。"); return; }

        var t = prop.PropertyType;

        if (t.IsEnum)
        {
            prop.SetValue(null, ParseEnumFlag(t, value));
            return;
        }
        if (t == typeof(int))
        {
            prop.SetValue(null, int.Parse(value));
            return;
        }
        prop.SetValue(null, value);
    }

    /// <summary>解析逗号分隔的枚举名并按位或组合；单值时直接返回该枚举值。</summary>
    private static object ParseEnumFlag(Type enumType, string csv)
    {
        var parts = csv.Split(',');
        long acc = 0;
        foreach (var raw in parts)
        {
            var name = raw.Trim().Replace(" ", "");
            if (name.Length == 0) continue;
            if (!System.Enum.IsDefined(enumType, name))
            {
                Debug.LogWarning($"[MobileSetup] {enumType.Name} 无枚举值 '{name}'，可选：{string.Join("/", System.Enum.GetNames(enumType))}");
                continue;
            }
            acc |= System.Convert.ToInt64(System.Enum.Parse(enumType, name));
        }
        return System.Enum.ToObject(enumType, acc);
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

    private static void WarnAndroidModule()
    {
        Debug.LogWarning("[MobileSetup] 未找到 Android 构建模块，跳过 Android 专属设置。" +
                         "请在 Unity Hub 中为当前版本安装 Android Build Support。");
    }

    /// <summary>取本次构建的输出路径（工程同级的 Builds/ 目录，已 gitignore）。</summary>
    public static string GetBuildOutputPath(string fileName)
    {
        var dir = Path.GetFullPath(Path.Combine(Application.dataPath, BuildRoot));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, fileName);
    }
}