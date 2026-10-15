using UnityEngine;

/// <summary>
/// 轻量相机跟随：ForestArea 没有搭 Cinemachine，用硬跟随保持玩家居中。
/// </summary>
public class CameraFollow : MonoBehaviour
{
    public Transform target;

    void Start()
    {
        // 无条件绑定到当前场景中"活跃"的 Player：切场景回来时序列化 target 指向的
        // 重复 Player 此时只是停用、帧末才销毁，判空不可靠；FindGameObjectWithTag
        // 只返回活跃对象，天然指向持久存活的那个
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) target = player.transform;
    }

    void LateUpdate()
    {
        if (target == null) return;
        transform.position = new Vector3(target.position.x, target.position.y, transform.position.z);
    }
}
