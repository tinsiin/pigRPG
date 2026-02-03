using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

/// <summary>
/// スキル効果を解決するサービス。
/// 攻撃実行後の派生効果をエフェクトチェーンで処理する。
/// </summary>
public sealed class EffectResolver
{
    private IBattleQueryService _queryService;
    private SkillEffectChain _effectChain;

    /// <summary>
    /// QueryServiceを設定する（BattleManager初期化時に呼ばれる）
    /// </summary>
    public void SetQueryService(IBattleQueryService queryService)
    {
        _queryService = queryService;

        // エフェクトチェーンを初期化
        _effectChain = new SkillEffectChain(new ISkillEffect[]
        {
            new FlatRozeEffect(),
            new HelpRecoveryEffect(),
            new RevengeBonusEffect(),
        });
    }

    public void ApplyRatherDamage(List<BaseStates> targets, float damageAmount)
    {
        var damage = new StatesPowerBreakdown(new TenDayAbilityDictionary(), damageAmount);
        foreach (var target in targets)
        {
            target.RatherDamage(damage, false, 1);
        }
    }

    public async UniTask ResolveSkillEffectsAsync(
        BaseStates acter,
        allyOrEnemy acterFaction,
        UnderActersEntryList targets,
        BattleGroup allyGroup,
        BattleGroup enemyGroup,
        ActionQueue acts,
        int battleTurnCount,
        Action<string> messageCallback)
    {
        if (acter == null || targets == null) return;

        var skill = acter.NowUseSkill;
        if (skill == null) return;

        skill.SetDeltaTurn(battleTurnCount);
        var message = await acter.AttackChara(targets);
        messageCallback?.Invoke(message);

        // エフェクトチェーンで派生効果を実行
        if (_effectChain != null)
        {
            var context = new SkillEffectContext(
                acter,
                acterFaction,
                targets,
                allyGroup,
                enemyGroup,
                acts,
                battleTurnCount,
                _queryService);

            await _effectChain.ExecuteAll(context);
        }
    }
}
