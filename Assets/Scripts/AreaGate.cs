using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 区域门禁：玩家踩上触发器后，按条件决定放行切场景还是提示。
/// 每个门禁配置目标场景与到达后的玩家出生点，出生点经静态字段跨场景传递。
/// </summary>
public class AreaGate : MonoBehaviour
{
    public enum GateCondition { None, HasSword }

    [Header("Gate Settings")]
    [Tooltip("通过门禁需要满足的条件；None 表示自由通行（用于反向传送点）")]
    public GateCondition condition = GateCondition.None;

    [Tooltip("通过门禁后加载的目标场景名")]
    public string targetSceneName;

    [Tooltip("进入目标场景后玩家的出生位置")]
    public Vector2 targetSpawnPosition;

    [Header("Locked Hint")]
    [Tooltip("未满足条件时显示的提示文本（TextMesh，中文用内置 Arial 动态字体），留空则无提示")]
    public TextMesh lockedHint;

    [Tooltip("提示文本显示时长（秒）")]
    public float hintDuration = 2.5f;

    // 跨场景传递出生点：目标场景里的 PlayerSpawn 在 Start 时消费
    static bool _hasPendingSpawn;
    static Vector3 _pendingSpawnPosition;

    public static bool HasPendingSpawn => _hasPendingSpawn;

    public static Vector3 ConsumePendingSpawn()
    {
        _hasPendingSpawn = false;
        return _pendingSpawnPosition;
    }

    Coroutine _hintRoutine;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        if (condition == GateCondition.HasSword && !StatsManager.Instance.hasSword)
        {
            ShowLockedHint();
            return;
        }

        _pendingSpawnPosition = targetSpawnPosition;
        _hasPendingSpawn = true;
        SceneManager.LoadScene(targetSceneName);
    }

    void ShowLockedHint()
    {
        if (lockedHint == null) return;
        lockedHint.gameObject.SetActive(true);
        if (_hintRoutine != null) StopCoroutine(_hintRoutine);
        _hintRoutine = StartCoroutine(HideHintRoutine());
    }

    IEnumerator HideHintRoutine()
    {
        yield return new WaitForSeconds(hintDuration);
        lockedHint.gameObject.SetActive(false);
    }
}
