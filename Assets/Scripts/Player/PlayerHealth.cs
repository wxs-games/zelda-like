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
        healthText.text = "HP: " + StatsManager.Instance.playerCurrentHealth + "/" + StatsManager.Instance.playerMaxHealth;
    }
    public void ChangeHealth(float amount)
    {


        StatsManager.Instance.playerCurrentHealth += amount;
        StatsManager.Instance.playerCurrentHealth = Mathf.Clamp(StatsManager.Instance.playerCurrentHealth, 0, StatsManager.Instance.playerMaxHealth);
        healthTextAnim.Play("TextUpdate");
        healthText.text = "HP: " + StatsManager.Instance.playerCurrentHealth + "/" + StatsManager.Instance.playerMaxHealth;
        if (StatsManager.Instance.playerCurrentHealth <= 0)
        {
            gameObject.SetActive(false);
        }
    }
}
