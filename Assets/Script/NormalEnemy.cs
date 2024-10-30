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

}