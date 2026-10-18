using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 全局 UI 音效转发器。
///
/// 【为什么不逐个按钮去挂脚本】
///   工程里有十几处 UI 按钮（商店、技能树、任务日志、对话选项、触屏虚拟键），
///   逐个挂脚本 + 逐个填引用，既容易漏也会把 prefab 弄得很脏。
///   社区主流做法是挂一个总闸：运行时扫描场景内所有 Button，
///   在 onClick 回调链末尾追加一个播放音效的回调。新增按钮无需任何配置。
///
/// 【实现要点】
///   1. 去重靠自己维护的 HashSet。Unity 的 AddListener 一旦添加无法移除，
///      所以绝不能重复扫描同一个按钮——否则音效会叠加播放（越点越响）。
///      这里按按钮 instanceId 记录，场景切换后重扫也不会重复挂。
///   2. 触屏虚拟按键是 Input System 的 OnScreenButton，走的是指针事件，
///      不产生 onClick，本就扫不到，无需特殊处理。
///   3. 对话进行中禁音：对话推进同样触发按钮点击，
///      每次「下一句」都咔哒一声会非常吵。
/// </summary>
[DefaultExecutionOrder(-100)]
public class UiSoundForwarder : MonoBehaviour
{
    private static UiSoundForwarder _instance;

    /// <summary>已挂过音效回调的按钮。Unity 的回调无法移除，故必须靠这个杜绝重复挂载。</summary>
    private readonly HashSet<int> _hooked = new HashSet<int>();

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this);
            return;
        }
        _instance = this;
        // 子节点调用无效，统一对根 GameObject 调用
        DontDestroyOnLoad(transform.root.gameObject);
    }

    private void Start()
    {
        if (_instance != this) return;
        UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnSceneChanged;
        ScanAll();
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged -= OnSceneChanged;
            _instance = null;
        }
    }

    private void OnSceneChanged(UnityEngine.SceneManagement.Scene from,
                                UnityEngine.SceneManagement.Scene to)
    {
        // 旧场景按钮已随场景销毁，但 instanceId 可能被复用，故清空记录
        _hooked.Clear();
        ScanAll();
    }

    /// <summary>扫描当前场景所有 Button，为尚未挂上音效回调的补挂。</summary>
    public void ScanAll()
    {
        var buttons = FindObjectsOfType<Button>(true);
        int added = 0;
        foreach (var btn in buttons)
        {
            if (btn == null) continue;
            if (!Hook(btn)) continue;
            added++;
        }
        if (added > 0)
            Debug.Log($"[UISound] 已为 {added} 个按钮挂上点击音效");
    }

    /// <summary>挂载点击音效。返回 true 表示本次真的挂上了。</summary>
    private bool Hook(Button btn)
    {
        int id = btn.GetInstanceID();
        if (_hooked.Contains(id)) return false;
        _hooked.Add(id);

        // 闭包捕获 btn，避免逐个按钮各写一个字段；靠 _hooked 保证只挂一次
        btn.onClick.AddListener(() => HandleClick(btn.gameObject));
        return true;
    }

    /// <summary>被所有按钮的 onClick 调用。参数类型与 Button.onClick 的元素类型一致 (GameObject)。</summary>
    public void HandleClick(GameObject clicked)
    {
        // 当前正在对话时禁音
        if (DialogManager.Instance != null && DialogManager.Instance.IsInDialog) return;

        SoundManager.Instance?.PlayUiClick();
    }
}
