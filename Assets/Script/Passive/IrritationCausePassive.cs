using System;
using UnityEngine;

/// <summary>
/// 持続付与ルートのイラつきパッシブ。
/// OnNextTurn()で毎ターン_ownerに対して_grantor由来のイラつきを加算する。
/// BasePassiveの既存プロパティ（ATK補正等）も併用可能。
/// </summary>
public class IrritationCausePassive : BasePassive
{
    /// <summary>毎ターンの付与量</summary>
    [SerializeField] private int _irritationAmountPerTurn = 1;

    public override void OnNextTurn()
    {
        base.OnNextTurn();

        // 付与元（_grantor=挑発スキルを使った者）が生存していればイラつき加算
        if (_grantor != null && !_grantor.Death() && _owner != null && !_owner.Death())
        {
            IrritationService.Add(_owner, _grantor, _irritationAmountPerTurn);
        }
    }
}
