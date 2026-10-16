using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一份完整对话的集合。多个 NPC 可以共享同一份（比如村民 A 和 B 说一样的话）。
/// </summary>
[CreateAssetMenu(fileName = "Dialog", menuName = "NPC/Dialog")]
public class DialogSO : ScriptableObject
{
    [Tooltip("入口节点 ID，玩家按 F 触发对话时从此节点开始。")]
    public string entryNodeId = "start";

    [Tooltip("所有节点。必须包含 entryNodeId 对应的节点。")]
    public List<DialogNodeSO> nodes = new List<DialogNodeSO>();

    /// <summary>按 ID 查节点，找不到返回 null。</summary>
    public DialogNodeSO GetNode(string id)
    {
        foreach (var n in nodes)
            if (n != null && n.nodeId == id) return n;
        return null;
    }
}
