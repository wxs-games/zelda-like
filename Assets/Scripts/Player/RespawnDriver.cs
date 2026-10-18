using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// 复活计时驱动器：挂在一个**永不失活**的常驻对象上。
///
/// 【为什么需要它，重要】
/// 死亡时要把玩家 GameObject 设为 inactive，而 Unity 的
/// `SetActive(false)` 会**立即停掉该对象上的所有协程**。
/// 于是「先 StartCoroutine 再 SetActive(false)」也不行——协程还没真正跑起来
/// 就随对象一起被停掉，玩家永远复活不了（此坑实测踩过两次）。
///
/// 社区标准解法：把延时逻辑放到一个不会失活的对象上执行。
/// 本类挂在 GameRoot 上（Bootstrap 守卫保证全局只有一个，且不随玩家显隐）。
/// </summary>
public class RespawnDriver : MonoBehaviour
{
    private static RespawnDriver _instance;

    /// <summary>
    /// 取当前活动的驱动器；静态字段被域重载清零时会自动重新绑定。
    /// 见 <see cref="Resolve"/>。
    /// </summary>
    public static RespawnDriver Instance
    {
        get
        {
            if (_instance == null || !_instance.isActiveAndEnabled) Resolve();
            return _instance;
        }
    }

    private static void Resolve()
    {
        // FindObjectsOfTypeAll 含未激活对象，才能把被停用的那份也捞出来比较
        var all = Resources.FindObjectsOfTypeAll<RespawnDriver>();
        RespawnDriver best = null;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null || !all[i].isActiveAndEnabled) continue;
            if (!all[i].gameObject.scene.isLoaded) continue;
            best = all[i];
            break;
        }
        if (best != null) _instance = best;
    }

    private void Awake()
    {
        if (_instance == null) _instance = this;
    }

    // 【域重载坑，重要】编辑器里改脚本会触发域重载：静态字段被清零，但 Awake 不重跑，
    // 于是 Instance 变成 null，PlayerHealth 走进"立即复活"兜底分支，复活延迟静默失效。
    // OnEnable 在重载后一定会跑，这里重新绑定即可自愈。详见 StatsManager 同款注释。
    private void OnEnable()
    {
        if (_instance == null || !_instance.isActiveAndEnabled
            || _instance.gameObject.scene != gameObject.scene)
        {
            _instance = this;
        }
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    /// <summary>在常驻对象上跑延时回调（延时用 unscaledDeltaTime，不受 timeScale 影响）。</summary>
    public void Schedule(System.Collections.IEnumerator routine, System.Action onDone)
    {
        if (routine == null) return;
        StartCoroutine(Run(routine, onDone));
    }

    private IEnumerator Run(System.Collections.IEnumerator routine, System.Action onDone)
    {
        yield return routine;
        if (onDone != null) onDone();
    }
}
