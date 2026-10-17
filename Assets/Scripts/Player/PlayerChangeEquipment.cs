using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerChangeEquipment : MonoBehaviour
{
    public PlayerCombat combat;
    public PlayerBow bow;
    private void Update()
    {
        if (GameInput.ChangeEquipmentPressed)
        {
            // 只在已解锁的能力之间切换：没解锁弓时按键无效；
            // 只解锁弓而未解锁剑时，弓保持装备，不会误启用剑
            if (!StatsManager.Instance.hasBow) return;

            if (combat.enabled && StatsManager.Instance.hasSword)
            {
                combat.enabled = false;
                bow.enabled = true;
            }
            else if (bow.enabled)
            {
                bow.enabled = false;
                if (StatsManager.Instance.hasSword) combat.enabled = true;
            }
        }
    }
}
