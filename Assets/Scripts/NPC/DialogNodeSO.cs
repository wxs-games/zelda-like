using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 对话节点：一句台词 + 说话者 + 选项。
/// 一个完整对话由多个 DialogNodeSO 通过 DialogOption 串联。
/// </summary>
[CreateAssetMenu(fileName = "DialogNode", menuName = "NPC/Dialog Node")]
public class DialogNodeSO : ScriptableObject
{
    [Tooltip("节点唯一 ID，DialogSO 内不能重复。入口节点留空由 DialogSO 指定。")]
    public string nodeId;

    [Tooltip("说话者名字，显示在对话框顶部。空则隐藏名字条。")]
    public string speakerName;

    [Tooltip("说话者头像，显示在对话框左侧。空则隐藏头像。")]
    public Sprite speakerPortrait;

    [TextArea(3, 6)]
    [Tooltip("台词正文，支持 \\n 换行。")]
    public string text;

    [Tooltip("选项。空列表则对话到此结束（Exit=true）。")]
    public List<DialogOption> options = new List<DialogOption>();
}

[Serializable]
public class DialogOption
{
    [Tooltip("按钮上显示的文字，例如 \"好的\" / \"算了\" / \"买东西\"。")]
    public string label;

    [Tooltip("点选后跳到的下一个节点 ID。空则对话结束。")]
    public string nextNodeId;

    [Tooltip("条件 ID，留空表示无条件。DialogManager 会去条件注册表里查 ID 对应的 Func<bool>。")]
    public string conditionId;

    [Tooltip("点选此选项时触发的动作（通过 DialogManager.RegisterAction 注册）。常用于\"开商店\"这类副作用。")]
    public string actionId;
}
