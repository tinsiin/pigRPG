using RandomExtensions.Linq;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;

/// <summary>
///     パーティー属性
/// </summary>
public enum PartyProperty
{
    TrashGroup,
    HolyGroup,
    MelaneGroup,
    Odradeks,

    Flowerees
    //馬鹿共、聖戦(必死、目的使命)、メレーンズ(王道的)、オドラデクス(秩序から離れてる、目を開いて我々から離れるようにコロコロと)、花樹(オサレ)
}

/// <summary>
///     戦いをするパーティーのクラス
/// </summary>
public class BattleGroup
{
    private readonly List<BaseStates> _ours;

    /// <summary>
    ///     パーティー属性
    /// </summary>
    public PartyProperty OurImpression;

    /// <summary>
    ///     コンストラクタ
    /// </summary>
    public BattleGroup(List<BaseStates> ours, PartyProperty ourImpression,WhichGroup _which)
    {
        _ours = ours;
        OurImpression = ourImpression;
        which = _which;
    }


    /// <summary>
    ///     集団の人員リスト
    /// </summary>
    public IReadOnlyList<BaseStates> Ours => _ours;

    /// <summary>
    /// 現在のグループで前のめりしているcharacter
    /// </summary>
    public BaseStates InstantVanguard;

    /// <summary>
    /// 陣営
    /// </summary>
    public WhichGroup which;

    /// <summary>
    /// このグループには指定したどれかの精神印象を持った奴が"一人でも"いるかどうか　
    /// 複数の印象を一気に指定できます。
    /// </summary>
    public bool ContainAnyImpression(params SpiritualProperty[] impressions)
    {
        /*foreach (var impression in impressions)//判定する印象全てを判定する
        {
            foreach (var one in _ours)//グループ内全てに回す
            {
                if (one.MyImpression == impression) return true;//いっこでも　あったら終わりです
            }
        }

        return false;*/

        return _ours.Any(one => impressions.Contains(one.MyImpression));
    }

    /// <summary>
    /// 指定された印象を持ったキャラクター達を返す関数
    /// </summary>
    public BaseStates[] GetCharactersFromImpression(params SpiritualProperty[] impressions)
    {
        return _ours.Where(one => impressions.Contains(one.MyImpression)).ToArray();
    }
}