using UnityEngine;

/// <summary>
/// 场景相机绑定器：场景加载后把虚拟相机的 Follow/LookAt 绑定到持久存活的 Player。
/// 场景里的相机不放进 GameRoot（每个场景构图/Confiner 不同），切场景回来时
/// 新相机序列化的 Follow 可能指向已被 GameBootstrap 守卫销毁的重复 Player，
/// 这里检测到空引用就用当前存活的 Player 覆盖。
/// Cinemachine 程序集未被项目脚本直接引用，用反射设置其 Follow/LookAt 属性。
/// </summary>
public class CameraTargetBinder : MonoBehaviour
{
    void Start()
    {
        // 无条件绑定到活跃 Player：重复 GameRoot 在 Awake 被停用、帧末才销毁，
        // 此时旧序列化引用判空不可靠，而 FindGameObjectWithTag 只返回活跃对象，
        // 天然得到持久存活的那个
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        foreach (var c in GetComponents<Component>())
        {
            if (c == null || c.GetType().Name != "CinemachineVirtualCamera") continue;

            var followProp = c.GetType().GetProperty("Follow");
            if (followProp != null)
                followProp.SetValue(c, player.transform);
            // 不设 LookAt：2D 相机管线无 Aim 阶段（Framing Transposer 只用 Follow），
            // 旋转必须保持固定，否则 Composer 式瞄准会持续微转镜头产生画面摇摆
        }
    }
}
