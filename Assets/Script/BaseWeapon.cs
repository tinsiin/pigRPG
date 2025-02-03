using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BaseWeapon
{
    public int id;
    public string name;
    /// <summary>
    /// この武器で決まる戦闘規格
    /// </summary>
    public BattleProtocol protocol;
}
