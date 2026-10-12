using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerChangeEquipment : MonoBehaviour
{
    public PlayerCombat combat;
    public PlayerBow bow;
    private void Update()
    {
        if (Input.GetButtonDown("ChangeEquipment"))
        {
            combat.enabled = !combat.enabled;
            bow.enabled = !bow.enabled;

        }
    }
}
