using System.Collections.Generic;

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
    public BattleGroup(List<BaseStates> ours, PartyProperty ourImpression)
    {
        _ours = ours;
        OurImpression = ourImpression;
    }

    /// <summary>
    ///     集団の人員リスト
    /// </summary>
    public IReadOnlyList<BaseStates> Ours => _ours;
}