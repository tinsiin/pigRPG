using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class PartyBuilder
{
    private readonly PlayersRoster roster;
    private readonly IPartyComposition composition;
    private readonly IPlayersUIControl uiControl;
    private readonly IPartyPropertyCalculator propertyCalculator;

    public PartyBuilder(
        PlayersRoster roster,
        IPartyComposition composition,
        IPlayersUIControl uiControl,
        IPartyPropertyCalculator propertyCalculator)
    {
        this.roster = roster;
        this.composition = composition;
        this.uiControl = uiControl;
        this.propertyCalculator = propertyCalculator;
    }

    /// <summary>互換性用コンストラクタ（propertyCalculatorなし）</summary>
    public PartyBuilder(PlayersRoster roster, IPlayersUIControl uiControl)
        : this(roster, null, uiControl, null)
    {
    }

    public BattleGroup BuildParty()
    {
        var members = new List<BaseStates>();

        // composition がある場合はそこから取得、ない場合は固定3人
        if (composition != null)
        {
            foreach (var id in composition.ActiveMemberIds)
            {
                var ally = roster.GetAlly(id);
                if (ally != null)
                {
                    members.Add(ally);
                }
            }
        }
        else
        {
            // 互換性: 旧ロジック（固定3人）
            var geino = roster.GetAlly(CharacterId.Geino);
            if (geino != null) members.Add(geino);
            // 現状は1人のみ（元のコードに合わせる）
        }

        if (members.Count == 0)
        {
            Debug.LogWarning("BuildParty: パーティーメンバーがいません");
        }

        var nowOurImpression = GetPartyImpression(members);
        var compatibilityData = new Dictionary<(BaseStates, BaseStates), int>();

        uiControl?.AllyAlliesUISetActive(false);
        foreach (var chara in members)
        {
            if (chara == null)
            {
                Debug.LogWarning("BuildParty: playerGroup に null キャラが含まれています。");
                continue;
            }

            if (chara.UI != null)
            {
                var bar = chara.UI.HPBar;
                if (bar != null)
                {
                    bar.SetBothBarsImmediate(
                        chara.HP / chara.MaxHP,
                        chara.MentalHP / chara.MaxHP,
                        chara.GetMentalDivergenceThreshold());
                }
                else
                {
                    Debug.LogWarning($"BuildParty: {chara.CharacterName} の UI.HPBar が未割り当てです。");
                }
            }
            else
            {
                Debug.LogWarning($"BuildParty: {chara.CharacterName} の UI が未割り当てです。");
            }
        }

        return new BattleGroup(members, nowOurImpression, allyOrEnemy.alliy, compatibilityData);
    }

    /// <summary>
    /// パーティー属性を決定
    /// - 固定メンバー3人 → 既存HP比較（決定論的）
    /// - 固定メンバー2人 → HP比較マッピング（決定論的）
    /// - 固定メンバー1人以下/新キャラのみ → 共通ロジック（ランダムあり）
    /// </summary>
    private PartyProperty GetPartyImpression(List<BaseStates> members)
    {
        if (members.Count == 0)
            return PartyProperty.MelaneGroup;

        // 固定メンバーを抽出
        var allyMembers = members.OfType<AllyClass>().ToList();
        int originalCount = CountOriginalMembers(allyMembers);

        // 固定メンバー3人全員揃っている場合 → 既存HP比較（決定論的）
        if (originalCount == 3 && IsOriginalThree(allyMembers))
        {
            return GetTrioPartyPropertyByHp();
        }

        // 固定メンバー2人の場合 → HP比較マッピング（決定論的）
        if (originalCount == 2 && allyMembers.Count == 2)
        {
            return GetDuoPartyPropertyByHp(allyMembers);
        }

        // 固定メンバー1人以下、または新キャラ含む → 共通ロジック（ランダムあり）
        if (propertyCalculator != null)
        {
            if (members.Count == 1)
            {
                return propertyCalculator.GetSoloPartyProperty(members[0].MyImpression);
            }

            var impressions = members.Select(m => m.MyImpression).ToList();
            return propertyCalculator.CalculateFromImpressions(impressions);
        }

        // propertyCalculator がない場合はデフォルト
        return PartyProperty.MelaneGroup;
    }

    /// <summary>
    /// 固定メンバー（Geino/Noramlia/Sites）の人数をカウント
    /// </summary>
    private int CountOriginalMembers(List<AllyClass> members)
    {
        return members.Count(a => a.CharacterId.IsOriginalMember);
    }

    /// <summary>
    /// オリジナル3人が全員いるか判定
    /// </summary>
    private bool IsOriginalThree(List<AllyClass> members)
    {
        if (members.Count != 3) return false;
        var ids = members.Select(a => a.CharacterId).ToHashSet();
        return ids.Contains(CharacterId.Geino)
            && ids.Contains(CharacterId.Noramlia)
            && ids.Contains(CharacterId.Sites);
    }

    /// <summary>
    /// 既存のHP比較ロジック（3人専用、決定論的）
    /// </summary>
    private PartyProperty GetTrioPartyPropertyByHp()
    {
        var geino = roster.GetAlly(CharacterId.Geino);
        var noramlia = roster.GetAlly(CharacterId.Noramlia);
        var sites = roster.GetAlly(CharacterId.Sites);

        if (geino == null || noramlia == null || sites == null)
            return PartyProperty.MelaneGroup;

        // 許容誤差チェック
        float toleranceStair = geino.MaxHP * 0.05f;
        float toleranceSateliteProcess = sites.MaxHP * 0.05f;
        float toleranceBassJack = noramlia.MaxHP * 0.05f;

        if (Mathf.Abs(geino.HP - sites.HP) <= toleranceStair &&
            Mathf.Abs(sites.HP - noramlia.HP) <= toleranceSateliteProcess &&
            Mathf.Abs(noramlia.HP - geino.HP) <= toleranceBassJack)
        {
            return PartyProperty.MelaneGroup;
        }

        // HP順序で6通り分岐
        if (geino.HP >= sites.HP && sites.HP >= noramlia.HP)
            return PartyProperty.MelaneGroup;
        if (geino.HP >= noramlia.HP && noramlia.HP >= sites.HP)
            return PartyProperty.Odradeks;
        if (sites.HP >= geino.HP && geino.HP >= noramlia.HP)
            return PartyProperty.MelaneGroup;
        if (sites.HP >= noramlia.HP && noramlia.HP >= geino.HP)
            return PartyProperty.HolyGroup;
        if (noramlia.HP >= geino.HP && geino.HP >= sites.HP)
            return PartyProperty.TrashGroup;
        if (noramlia.HP >= sites.HP && sites.HP >= geino.HP)
            return PartyProperty.Flowerees;

        return PartyProperty.MelaneGroup;
    }

    /// <summary>
    /// 2人の場合のパーティー属性決定（決定論的、HP比較マッピング）
    /// パーティー属性HP分岐マッピング.md に基づく
    /// </summary>
    private PartyProperty GetDuoPartyPropertyByHp(List<AllyClass> members)
    {
        if (members.Count != 2) return PartyProperty.MelaneGroup;

        var ids = members.Select(a => a.CharacterId).ToList();

        // オリジナルペアでない場合はデフォルト（またはpropertyCalculatorがあれば使う）
        if (!IsOriginalPair(ids))
        {
            if (propertyCalculator != null)
            {
                var impressions = members.Select(m => m.MyImpression).ToList();
                return propertyCalculator.CalculateFromImpressions(impressions);
            }
            return PartyProperty.MelaneGroup;
        }

        // HP順でソート
        var sorted = members.OrderByDescending(m => m.HP).ToList();
        var higherId = sorted[0].CharacterId;

        // Geino + Sites
        if (ids.Contains(CharacterId.Geino) && ids.Contains(CharacterId.Sites))
        {
            return higherId == CharacterId.Geino
                ? PartyProperty.MelaneGroup   // G≥S → メレーンズ
                : PartyProperty.Flowerees;    // S≥G → 花樹
        }

        // Geino + Noramlia
        if (ids.Contains(CharacterId.Geino) && ids.Contains(CharacterId.Noramlia))
        {
            return higherId == CharacterId.Geino
                ? PartyProperty.MelaneGroup   // G≥N → メレーンズ
                : PartyProperty.Odradeks;     // N≥G → オドラデクス
        }

        // Sites + Noramlia
        if (ids.Contains(CharacterId.Sites) && ids.Contains(CharacterId.Noramlia))
        {
            return higherId == CharacterId.Sites
                ? PartyProperty.HolyGroup     // S≥N → 聖戦
                : PartyProperty.TrashGroup;   // N≥S → 馬鹿共
        }

        return PartyProperty.MelaneGroup;
    }

    /// <summary>
    /// オリジナル3人のペアかどうか判定
    /// </summary>
    private bool IsOriginalPair(List<CharacterId> ids)
    {
        if (ids.Count != 2) return false;
        return ids.All(id => id.IsOriginalMember);
    }
}
