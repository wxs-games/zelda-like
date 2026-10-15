using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class PlayerHealth : MonoBehaviour
{
    public TMP_Text healthText;
    public Animator healthTextAnim;
    void Start()
    {
        // ForestArea 没有 UI 树，healthText 引用会为空，判空避免 NRE
        if (healthText != null)
            healthText.text = "HP: " + StatsManager.Instance.playerCurrentHealth + "/" + StatsManager.Instance.playerMaxHealth;
    }
    public void ChangeHealth(float amount)
    {


        StatsManager.Instance.playerCurrentHealth += amount;
        StatsManager.Instance.playerCurrentHealth = Mathf.Clamp(StatsManager.Instance.playerCurrentHealth, 0, StatsManager.Instance.playerMaxHealth);
        if (healthTextAnim != null) healthTextAnim.Play("TextUpdate");
        if (healthText != null)
            healthText.text = "HP: " + StatsManager.Instance.playerCurrentHealth + "/" + StatsManager.Instance.playerMaxHealth;
        if (StatsManager.Instance.playerCurrentHealth <= 0)
        {
            gameObject.SetActive(false);
        }
    }
}
