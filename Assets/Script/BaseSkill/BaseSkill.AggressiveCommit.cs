using R3;
using RandomExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
public partial class BaseSkill
{
    [Header("スキルの各所実行時、前のめりするかどうか")]
    /// <summary>
    /// このスキルを利用すると前のめり状態になるかどうか
    /// </summary>
    public bool IsAggressiveCommit = true;
    /// <summary>
    /// 発動カウント実行時に前のめりになるかどうか
    /// </summary>
    public bool IsReadyTriggerAgressiveCommit = false;
    /// <summary>
    /// スキルのストック時に前のめりになるかどうか
    /// </summary>
    public bool IsStockAgressiveCommit = false;
    /// <summary>
    /// スキルが前のめりになるからならないかを選べるかどうか
    /// </summary>
    public bool CanSelectAggressiveCommit = false;

}
