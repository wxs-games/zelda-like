using UnityEngine;

/// <summary>
/// 通用 NPC 交互基类：触发器检测玩家靠近 + Interact 按键 → 启动对话。
/// ShopKeeper、村民、任务 NPC 全部用这一个，不再各写一份 Update+Trigger。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class NPCInteractable : MonoBehaviour
{
    [Header("对话")]
    [Tooltip("NPC 持有的对话 SO。")]
    public DialogSO dialog;

    [Tooltip("可选：对话启动时注入的额外条件（比如 NPC 自己的状态）。key=conditionId, value=判断逻辑。")]
    public NPCCondition[] extraConditions;

    private bool playerInRange;

    void Reset()
    {
        // 确保 Collider2D 是触发器
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    void Update()
    {
        if (playerInRange && Input.GetButtonDown("Interact"))
        {
            Interact();
        }
    }

    /// <summary>
    /// 按 F 时调用，启动对话。DialogManager 会处理 timeScale、UI 渲染、选项分支、动作触发。
    /// </summary>
    public void Interact()
    {
        if (dialog == null)
        {
            Debug.LogWarning($"[NPC] {name} 没有配置 DialogSO");
            return;
        }
        // 把自己注册的条件注入 DialogManager（每次对话开始时刷新一次）
        if (extraConditions != null)
        {
            foreach (var c in extraConditions)
                DialogManager.RegisterCondition(c.conditionId, c.predicate);
        }
        DialogManager.Instance.StartDialog(dialog, this);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player")) playerInRange = true;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player")) playerInRange = false;
    }
}

/// <summary>给 NPCInteractable 用的条件对。predicate 直接在 Inspector 里通过代码赋值太麻烦——
/// 实际上条件判断统一写在 DialogManager 里注册，这个类主要是让以后扩展有地方挂。</summary>
[System.Serializable]
public class NPCCondition
{
    public string conditionId;
    [Tooltip("运行时由代码赋值，不在 Inspector 里配")]
    public System.Func<bool> predicate;
}
