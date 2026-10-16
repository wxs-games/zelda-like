using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 对话流程驱动器：单例。负责 timeScale 暂停、节点推进、条件判断、动作触发、对话结束清理。
/// UI 渲染（说话者/头像/文字/选项按钮）由 DialogBox 组件订阅本类事件负责。
/// </summary>
public class DialogManager : MonoBehaviour
{
    public static DialogManager Instance { get; private set; }

    [Header("引用")]
    [Tooltip("底部对话框 UI 预制体或场景上的实例。留空则运行时自动从场景 Canvas 找 DialogBox。")]
    public DialogBox dialogBox;

    [Header("条件注册表")]
    [Tooltip("条件 ID → 判断函数。通过 RegisterCondition 静态注册，游戏启动时集中注册一次即可。")]
    private static readonly Dictionary<string, Func<bool>> _conditions = new Dictionary<string, Func<bool>>();

    [Header("动作注册表")]
    [Tooltip("动作 ID → 回调。通过 RegisterAction 静态注册。")]
    private static readonly Dictionary<string, Action> _actions = new Dictionary<string, Action>();

    // 运行期状态
    private DialogSO _currentDialog;
    private DialogNodeSO _currentNode;
    private NPCInteractable _speaker;
    private bool _wasPaused; // 记录进入对话前 timeScale 状态，退出时恢复
    private List<DialogOption> _visibleOptions = new List<DialogOption>(); // 当前节点条件过滤后的选项，与按钮下标一一对应

    // ========== 生命周期 ==========

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // 只销毁自己（组件），不要销毁 GameObject（GameBootstrap 管它）
            Destroy(this);
            return;
        }
        Instance = this;
        // DialogManager 挂在 GameRoot 上，随持久根存活
        DontDestroyOnLoad(gameObject);
    }

    // ========== 静态注册入口 ==========

    /// <summary>注册一个条件判断。key=SO 里填的 conditionId，value=返回 true/false。</summary>
    public static void RegisterCondition(string conditionId, Func<bool> predicate)
    {
        if (string.IsNullOrEmpty(conditionId) || predicate == null) return;
        _conditions[conditionId] = predicate;
    }

    /// <summary>
    /// 外部系统（QuestManager 等）查询条件：conditionId 为空视为无条件 true；
    /// 未注册的 id 视为 false（防止误显示选项）。
    /// </summary>
    public static bool CheckCondition(string conditionId)
    {
        if (string.IsNullOrEmpty(conditionId)) return true;
        return _conditions.TryGetValue(conditionId, out var pred) && pred();
    }

    /// <summary>注销条件（条件变化时重注册即可覆盖，一般不需要注销）。</summary>
    public static void UnregisterCondition(string conditionId) => _conditions.Remove(conditionId);

    /// <summary>注册一个动作。key=SO 里填的 actionId，value=被触发的回调。</summary>
    public static void RegisterAction(string actionId, Action callback)
    {
        if (string.IsNullOrEmpty(actionId) || callback == null) return;
        _actions[actionId] = callback;
    }

    // ========== 对话驱动 ==========

    /// <summary>由 NPCInteractable 调用，启动对话流程。</summary>
    public void StartDialog(DialogSO dialog, NPCInteractable speaker)
    {
        if (dialog == null || IsInDialog) return;

        _currentDialog = dialog;
        _speaker = speaker;
        _wasPaused = Time.timeScale == 0;
        Time.timeScale = 0;

        EnsureDialogBox();
        dialogBox.ShowDialog();

        GoToNode(dialog.entryNodeId);
    }

    /// <summary>当前是否在对话中。UI 和其他系统可以查这个。</summary>
    public bool IsInDialog => _currentDialog != null;

    /// <summary>推进到下一个节点。选项按钮点击时调用，或无选项时按 Interact 自动进下一节点。</summary>
    public void Advance()
    {
        if (!IsInDialog || _currentNode == null) return;

        // 有选项的节点，Advance 不做任何事（等玩家点按钮）
        if (_currentNode.options != null && _currentNode.options.Count > 0) return;

        // 无选项 → 结束对话
        EndDialog();
    }

    /// <summary>点选某选项后的行为。index 是按钮列表（条件过滤后）的下标。</summary>
    public void SelectOption(int index)
    {
        if (!IsInDialog || _currentNode == null) return;
        // 用过滤后的可见选项列表，而不是原始 options——
        // 否则被条件隐藏的选项会占用下标，按钮点击会命中错误选项
        if (index < 0 || index >= _visibleOptions.Count) return;

        var option = _visibleOptions[index];
        bool shopOpened = false;

        // 执行副作用动作（如果有）
        if (!string.IsNullOrEmpty(option.actionId) && _actions.TryGetValue(option.actionId, out var action))
        {
            // 特殊处理 openShop：需要让 EndDialog 保持 timeScale=0
            shopOpened = option.actionId == "openShop";
            action?.Invoke();
        }

        // 跳节点或结束
        if (string.IsNullOrEmpty(option.nextNodeId))
        {
            // openShop 后，EndDialog 不应恢复 timeScale（保持商店暂停）
            if (shopOpened) _wasPaused = true;
            EndDialog();
        }
        else
        {
            GoToNode(option.nextNodeId);
        }
    }

    /// <summary>外部强制结束对话（比如 NPC 被消灭、场景切换）。</summary>
    public void ForceEndDialog()
    {
        if (IsInDialog) EndDialog();
    }

    // ========== 内部工具 ==========

    private void GoToNode(string nodeId)
    {
        var node = _currentDialog.GetNode(nodeId);
        if (node == null)
        {
            Debug.LogWarning($"[Dialog] 找不到节点 id={nodeId}，对话结束");
            EndDialog();
            return;
        }

        _currentNode = node;

        // 过滤不满足条件的选项，存到 _visibleOptions 供 SelectOption 按按钮下标取
        _visibleOptions.Clear();
        if (node.options != null)
        {
            foreach (var opt in node.options)
            {
                if (CheckCondition(opt.conditionId))
                {
                    _visibleOptions.Add(opt);
                }
                // 条件不满足的选项直接隐藏
            }
        }

        dialogBox.RenderNode(node, _visibleOptions);

        // 无选项 → 显示"按 F 继续"提示，玩家按 Interact 时 Advance 会结束对话
        if (_visibleOptions.Count == 0)
        {
            // 留对话框显示当前台词，等玩家按 Interact 结束
        }
    }

    private void EndDialog()
    {
        if (!IsInDialog) return;
        _currentDialog = null;
        _currentNode = null;
        _speaker = null;

        // 恢复 timeScale：恢复到对话前的值。
        // 特殊情况：如果 ShopKeeper.OpenShop() 在 action 里设了 timeScale=0，
        // 这里恢复到对话前的 1 会让商店开着但玩家能动——这种情况下由 OpenShop
        // 自己管 timeScale（它会在 action 里设 0），EndDialog 恢复的是 StartDialog
        // 之前的值。对 ShopKeeperDialog：StartDialog 前 timeScale=1，openShop action
        // 里设 timeScale=0，EndDialog 又设回 1 → 冲突。
        // 解决：让 openShop action 设完 timeScale=0 后，用 _wasPaused=true 欺骗
        // EndDialog 恢复到 0。见 SelectOption 里 openShop 的特殊处理。
        Time.timeScale = _wasPaused ? 0 : 1;

        if (dialogBox != null)
        {
            // 先显式清 optionContainer（最保险，绕过 HideDialog 可能的问题）
            if (dialogBox.optionContainer != null)
            {
                for (int i = dialogBox.optionContainer.childCount - 1; i >= 0; i--)
                    DestroyImmediate(dialogBox.optionContainer.GetChild(i).gameObject);
            }
            dialogBox.HideDialog();
        }
    }

    private void EnsureDialogBox()
    {
        if (dialogBox != null) return;

        // 直接找 DialogBox，不管在哪个 Canvas 下
        dialogBox = UnityEngine.Object.FindObjectOfType<DialogBox>();
    }
}
