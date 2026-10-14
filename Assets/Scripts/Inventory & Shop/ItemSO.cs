using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 道具可解锁的战斗能力。遵循塞尔达"能力即钥匙"设计：
/// 战斗能力不是开局默认拥有，而是通过对应道具永久解锁。
/// </summary>
public enum Ability
{
    None,
    Sword,
    Bow
}

[CreateAssetMenu(fileName ="New Item")]
public class ItemSO : ScriptableObject
{
    [Tooltip("内部标识：全小写英文，用于代码查找与场景物体命名（如 Loot_sword），不展示给玩家")]
    public string id;

    [Tooltip("显示名：给玩家看的文本，可使用中文")]
    public string itemName;

    [TextArea] public string itemDescription;
    public Sprite icon;
    public bool isGold;
    public int stackSize = 3;

    [Header("Stats")]
    public int currentHealth;
    public int maxHealth;
    public int speed;
    public int damage;

    [Header("For Temporary Items")]
    public float duration;

    [Header("Ability Unlock")]
    [Tooltip("使用该道具后永久解锁的战斗能力，与临时 buff 的 duration 机制相互独立")]
    public Ability unlocksAbility = Ability.None;

    [Header("World Presentation")]
    [Tooltip("地面拾取物的逐帧动画序列（精灵图集切割而来）；留空时地面直接显示 icon 单图")]
    public Sprite[] worldFrames;
    [Tooltip("地面帧动画播放速率（帧/秒）")]
    public float worldFrameRate = 8f;

}
