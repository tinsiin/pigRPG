using System.Collections.Generic;
using UnityEngine;

public sealed class PartyBuilder
{
    private readonly PlayersRoster roster;
    private readonly IPlayersUIControl uiControl;

    public PartyBuilder(PlayersRoster roster, IPlayersUIControl uiControl)
    {
        this.roster = roster;
        this.uiControl = uiControl;
    }

    private StairStates Geino => roster.GetAllyByIndex((int)PlayersStates.AllyId.Geino) as StairStates;
    private BassJackStates Noramlia => roster.GetAllyByIndex((int)PlayersStates.AllyId.Noramlia) as BassJackStates;
    private SateliteProcessStates Sites => roster.GetAllyByIndex((int)PlayersStates.AllyId.Sites) as SateliteProcessStates;

    public BattleGroup BuildParty()
    {
        var playerGroup = new List<BaseStates> { Geino };
        var nowOurImpression = GetPartyImpression();
        var compatibilityData = new Dictionary<(BaseStates, BaseStates), int>();

        if (playerGroup.Count >= 2)
        {
            // story dependent
        }

        uiControl.AllyAlliesUISetActive(false);
        foreach (var chara in playerGroup)
        {
            if (chara == null)
            {
                Debug.LogWarning("GetParty: playerGroup に null キャラが含まれています。");
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
                    Debug.LogWarning($"GetParty: {chara.CharacterName} の UI.HPBar が未割り当てです。");
                }
            }
            else
            {
                Debug.LogWarning($"GetParty: {chara.CharacterName} の UI が未割り当てです。");
            }
        }

        return new BattleGroup(playerGroup, nowOurImpression, allyOrEnemy.alliy, compatibilityData);
    }

    private PartyProperty GetPartyImpression()
    {
        float toleranceStair = Geino.MaxHP * 0.05f;
        float toleranceSateliteProcess = Sites.MaxHP * 0.05f;
        float toleranceBassJack = Noramlia.MaxHP * 0.05f;
        if (Mathf.Abs(Geino.HP - Sites.HP) <= toleranceStair &&
            Mathf.Abs(Sites.HP - Noramlia.HP) <= toleranceSateliteProcess &&
            Mathf.Abs(Noramlia.HP - Geino.HP) <= toleranceBassJack)
        {
            return PartyProperty.MelaneGroup;
        }

        if (Geino.HP >= Sites.HP && Sites.HP >= Noramlia.HP)
        {
            return PartyProperty.MelaneGroup;
        }
        else if (Geino.HP >= Noramlia.HP && Noramlia.HP >= Sites.HP)
        {
            return PartyProperty.Odradeks;
        }
        else if (Sites.HP >= Geino.HP && Geino.HP >= Noramlia.HP)
        {
            return PartyProperty.MelaneGroup;
        }
        else if (Sites.HP >= Noramlia.HP && Noramlia.HP >= Geino.HP)
        {
            return PartyProperty.HolyGroup;
        }
        else if (Noramlia.HP >= Geino.HP && Geino.HP >= Sites.HP)
        {
            return PartyProperty.TrashGroup;
        }
        else if (Noramlia.HP >= Sites.HP && Sites.HP >= Geino.HP)
        {
            return PartyProperty.Flowerees;
        }

        return PartyProperty.MelaneGroup;
    }
}
