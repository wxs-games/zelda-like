using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Threading;
using System.Security.AccessControl;

public class StatsUI : MonoBehaviour
{
    public GameObject[] statsSlots;
    public CanvasGroup statsCanvas;
    private bool statsOpen = false;
    public Animator playerAnim;
    public Animator EnemyAnim;
    [UnityEngine.Serialization.FormerlySerializedAs("playerMovement")]
    public PlayerKnockbackable playerKnockbackable;
    public EnemyMovement enemyMovement;
    private void Start()
    {
      UpdateAllStats();
    }
    private void Update()
    {
        if (Input.GetButtonDown("ToggleStats"))
        {
            if (statsOpen)
            {
                
                statsCanvas.alpha = 0;
                statsCanvas.blocksRaycasts = false;
                statsOpen = false;
                playerAnim.enabled = !statsOpen;
                EnemyAnim.enabled = !statsOpen;
                playerKnockbackable .enabled = !statsOpen;
                enemyMovement .enabled = !statsOpen;

            }
            else
            {
              
                statsCanvas.alpha=1;
                statsCanvas.blocksRaycasts = true;
                statsOpen = true;
                playerAnim.enabled = !statsOpen;
                EnemyAnim.enabled = !statsOpen;
                playerKnockbackable.enabled = !statsOpen;
                enemyMovement.enabled = !statsOpen;
            }
          
        }
    }
    public void UpdateDamage()
    {
        if (statsSlots == null || statsSlots.Length < 1) return;
        statsSlots[0].GetComponentInChildren<TMP_Text>().text = "Damage: " + StatsManager.Instance.playerDamage.ToString();
    }
    public void UpdateSpeed()
    {
        if (statsSlots == null || statsSlots.Length < 2) return;
        statsSlots[1].GetComponentInChildren<TMP_Text>().text = "Speed: " + StatsManager.Instance.playerSpeed.ToString();
    }
    public void UpdateAllStats()
    {
        UpdateDamage();
        UpdateSpeed();
    }
}
