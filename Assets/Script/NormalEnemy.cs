using System;
using System.Collections.Generic;

/// <summary>
///     通常の敵
/// </summary>
[Serializable]
public class NormalEnemy : BaseStates
{
    /// <summary>
    ///     この敵キャラクターの復活する歩数
    ///     手動だが基本的に生命キャラクターのみにこの歩数は設定する?
    /// </summary>
    public int recovelySteps;

    /// <summary>
    ///     このキャラクターが再生するかどうか
    ///     Falseにすると一度倒すと二度と出てきません。例えば機械キャラなら、基本Falseにする。
    /// </summary>
    public bool reborn;

    public NormalEnemy(int p, int maxp, string characterName, int recoveryTurn, List<BasePassive> passiveList,
        List<BaseSkill> skillList, int bDef, int bAgi, int bHit, int bAtk, int hp, int maxhp, CharacterType myType,
        SpiritualProperty myImpression, int maxRecoveryTurn, int recovelySteps, bool reborn) : base(p, maxp,
        characterName, recoveryTurn, passiveList, skillList, bDef, bAgi, bHit, bAtk, hp, maxhp, myType, myImpression,
        maxRecoveryTurn)
    {
        this.recovelySteps = recovelySteps;
        this.reborn = reborn;
    }
}