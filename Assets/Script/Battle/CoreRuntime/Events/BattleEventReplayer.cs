using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

public sealed class BattleEventReplayer : IBattleEventSink
{
    private readonly IBattleSession _session;
    private readonly IReadOnlyList<BattleInputRecord> _inputs;
    private bool _battleEnded;

    public BattleEventReplayer(IBattleSession session, IReadOnlyList<BattleInputRecord> inputs)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _inputs = inputs ?? Array.Empty<BattleInputRecord>();
    }

    public async UniTask ReplayAsync()
    {
        _battleEnded = false;
        var bus = _session.EventBus;
        bus?.Register(this);

        _session.Start();
        _session.Advance();

        for (var i = 0; i < _inputs.Count; i++)
        {
            if (_battleEnded) break;
            var input = BuildInput(_inputs[i]);
            await _session.ApplyInputAsync(input);
        }

        bus?.Unregister(this);
    }

    public void OnBattleEvent(BattleEvent battleEvent)
    {
        if (battleEvent.Type == BattleEventType.BattleEnded)
        {
            _battleEnded = true;
        }
    }

    private BattleInput BuildInput(BattleInputRecord record)
    {
        var actor = ResolveActor(record.ActorName);
        switch (record.Type)
        {
            case BattleInputType.SelectSkill:
                return BattleInput.SelectSkill(actor, ResolveSkill(actor, record.SkillName));
            case BattleInputType.StockSkill:
                return BattleInput.StockSkill(actor, ResolveSkill(actor, record.SkillName));
            case BattleInputType.SelectRange:
                return BattleInput.SelectRange(actor, record.RangeWill, record.IsOption);
            case BattleInputType.SelectTarget:
                return BattleInput.SelectTarget(actor, record.TargetWill, ResolveTargets(record.TargetIndices, record.TargetNames));
            case BattleInputType.Escape:
                return BattleInput.Escape(actor);
            case BattleInputType.DoNothing:
                return BattleInput.DoNothing(actor);
            case BattleInputType.Cancel:
                return BattleInput.Cancel(actor);
            case BattleInputType.Next:
                return BattleInput.Next();
            default:
                return BattleInput.Next();
        }
    }

    private BaseStates ResolveActor(string actorName)
    {
        var context = _session.Context;
        if (context == null) return null;
        if (string.IsNullOrEmpty(actorName)) return context.Acter;

        var candidates = context.AllCharacters;
        if (candidates == null) return context.Acter;

        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            if (candidate != null && candidate.CharacterName == actorName)
            {
                return candidate;
            }
        }

        return context.Acter;
    }

    private BaseSkill ResolveSkill(BaseStates actor, string skillName)
    {
        if (actor == null || string.IsNullOrEmpty(skillName)) return null;
        var list = actor.SkillList;
        if (list == null) return null;

        for (var i = 0; i < list.Count; i++)
        {
            var skill = list[i];
            if (skill != null && skill.SkillName == skillName)
            {
                return skill;
            }
        }

        return null;
    }

    private IReadOnlyList<BaseStates> ResolveTargets(int[] targetIndices, string[] targetNames)
    {
        var context = _session.Context;
        if (context == null || context.AllCharacters == null) return null;
        var candidates = context.AllCharacters;

        // インデックスベースの解決を優先（同名キャラクターの曖昧性を回避）
        if (targetIndices != null && targetIndices.Length > 0)
        {
            var result = new List<BaseStates>(targetIndices.Length);
            for (var i = 0; i < targetIndices.Length; i++)
            {
                var index = targetIndices[i];
                if (index >= 0 && index < candidates.Count)
                {
                    var candidate = candidates[index];
                    if (candidate != null)
                    {
                        result.Add(candidate);
                    }
                }
            }
            if (result.Count > 0)
            {
                return result;
            }
        }

        // フォールバック: 名前ベースの解決（後方互換性のため）
        if (targetNames == null || targetNames.Length == 0) return null;

        var fallbackResult = new List<BaseStates>(targetNames.Length);
        for (var i = 0; i < targetNames.Length; i++)
        {
            var name = targetNames[i];
            if (string.IsNullOrEmpty(name)) continue;
            for (var j = 0; j < candidates.Count; j++)
            {
                var candidate = candidates[j];
                if (candidate != null && candidate.CharacterName == name)
                {
                    fallbackResult.Add(candidate);
                    break;
                }
            }
        }

        return fallbackResult.Count > 0 ? fallbackResult : null;
    }
}
