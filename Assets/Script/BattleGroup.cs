using RandomExtensions;
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

    /// <summary>
    ///     パーティー属性
    /// </summary>
    public PartyProperty OurImpression;

    /// <summary>
    ///     コンストラクタ
    /// </summary>
    public BattleGroup(List<BaseStates> ours, PartyProperty ourImpression,WhichGroup _which)
    {
        Ours = ours;
        OurImpression = ourImpression;
        which = _which;
        InitializeRandomInstantVanGuardSelect();
    }

    /// <summary>
    /// 前のめりをランダムにグループ内で選別しInstantVanguardの初期化する
    /// </summary>
    private void InitializeRandomInstantVanGuardSelect()
    {
        InstantVanguard = RandomEx.Shared.GetItem(Ours.ToArray());
    }

    /// <summary>
    ///     集団の人員リスト
    /// </summary>
    public List<BaseStates> Ours {  get; private set; }

    public void SetCharactersList(List<BaseStates> list)
    {
        Ours = list;
    }

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
        return Ours.Any(one => impressions.Contains(one.MyImpression));
    }

    /// <summary>
    /// 指定された印象を持ったキャラクター達を返す関数
    /// </summary>
    public List<BaseStates> GetCharactersFromImpression(params SpiritualProperty[] impressions)
    {
        return Ours.Where(one => impressions.Contains(one.MyImpression)).ToList();
    }
}