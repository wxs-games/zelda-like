using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class ToggleSkillTree : MonoBehaviour
{
    public CanvasGroup statsCanvas;
    private bool skillTreeOpen = false;
    void Update()
    {
        if (Input.GetButtonDown("ToggleSkillTree"))
        {
            if (skillTreeOpen)
            {
                Time.timeScale = 1;
                statsCanvas.alpha = 0;
                statsCanvas.blocksRaycasts = false;
                skillTreeOpen = false;
            }
            else
            {
                Time.timeScale = 0;
                statsCanvas.alpha = 1;
                statsCanvas.blocksRaycasts = true;
                skillTreeOpen = true;
            }
        }
    }

}
