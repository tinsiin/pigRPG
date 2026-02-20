using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static CommonCalc;

/// <summary>
/// 戦闘中のキャラクター/グループ情報を照会するサービスの実装。
/// </summary>
public sealed class BattleQueryService : IBattleQueryService
{
    private readonly BattleGroup _allyGroup;
    private readonly BattleGroup _enemyGroup;

    public BattleQueryService(BattleGroup allyGroup, BattleGroup enemyGroup)
    {
        _allyGroup = allyGroup;
        _enemyGroup = enemyGroup;
    }

    public bool IsVanguard(BaseStates chara)
    {
        if (_allyGroup != null && chara == _allyGroup.InstantVanguard) return true;
        if (_enemyGroup != null && chara == _enemyGroup.InstantVanguard) return true;
        return false;
    }

    public BattleGroup GetGroupForCharacter(BaseStates chara)
    {
        if (_allyGroup != null && _allyGroup.Ours.Contains(chara)) return _allyGroup;
        if (_enemyGroup != null && _enemyGroup.Ours.Contains(chara)) return _enemyGroup;
        return null;
    }

    public Faction GetCharacterFaction(BaseStates chara)
    {
        if (_allyGroup != null)
        {
            foreach (var one in _allyGroup.Ours)
            {
                if (one == chara) return Faction.Ally;
            }
        }
        if (_enemyGroup != null)
        {
            foreach (var one in _enemyGroup.Ours)
            {
                if (one == chara) return Faction.Enemy;
            }
        }
        // キャラクターがどのグループにも属していない場合（一時的なリスト外や不正な呼び出し）
        Debug.LogWarning($"[BattleQueryService] Character {chara?.CharacterName ?? "null"} not found in any group");
        return Faction.Ally; // フォールバック値
    }

    public List<BaseStates> GetOtherAlliesAlive(BaseStates chara)
    {
        var group = GetGroupForCharacter(chara);
        if (group == null) return new List<BaseStates>();
        return RemoveDeathCharacters(group.Ours).Where(x => x != chara).ToList();
    }

    public bool IsFriend(BaseStates chara1, BaseStates chara2)
    {
        bool chara1InAlly = _allyGroup != null && _allyGroup.Ours.Contains(chara1);
        bool chara2InAlly = _allyGroup != null && _allyGroup.Ours.Contains(chara2);
        return chara1InAlly == chara2InAlly;
    }

    public BattleGroup FactionToGroup(Faction faction)
    {
        return faction switch
        {
            Faction.Ally => _allyGroup,
            Faction.Enemy => _enemyGroup,
            _ => null
        };
    }
}
