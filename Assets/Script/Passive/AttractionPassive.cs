using UnityEngine;

/// <summary>
/// 吸引パッシブ。機械のスキル吸引によって対象に付与される。
/// このパッシブが付いている間、全スキル系統のターゲットが _grantor（吸引元）に強制される。
/// パッシブの存在自体が吸引状態を示すマーカー。
/// </summary>
public class AttractionPassive : BasePassive
{
    public override void OnNextTurn()
    {
        base.OnNextTurn();

        // 吸引元が死亡していたらパッシブを自己除去
        if (_grantor == null || _grantor.Death())
        {
            if (_owner != null)
                _owner.RemovePassive(this);
        }
    }
}
