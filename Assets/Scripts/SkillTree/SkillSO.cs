using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;

[CreateAssetMenu(fileName="NewSkill",menuName="skillTree/skill")]
public class SkillSO: ScriptableObject
{

    public string skillName;
    public int maxLevel;
    public Sprite skillIcon;
}
