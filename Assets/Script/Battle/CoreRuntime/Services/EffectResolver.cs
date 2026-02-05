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
    private ISkillEffectPipeline _pipeline;
    private readonly IBattleLogger _logger;
    private readonly IBattleRandom _random;

    /// <summary>
    /// QueryServiceを設定する（BattleManager初期化時に呼ばれる）
    /// </summary>
    public EffectResolver(IBattleRandom random = null, IBattleLogger logger = null)
    {
        _random = random ?? new SystemBattleRandom();
        _logger = logger ?? new NoOpBattleLogger();
    }

    public void SetQueryService(IBattleQueryService queryService)
    {
        _queryService = queryService;
        EnsurePipeline();
    }

    public void SetEffectPipeline(ISkillEffectPipeline pipeline)
    {
        if (pipeline == null) return;
        _pipeline = pipeline;
    }

    public void RegisterEffect(ISkillEffect effect)
    {
        if (effect == null) return;
        EnsurePipeline();
        _pipeline.AddEffect(effect);
    }

    public void RegisterComboRule(ISkillComboRule rule)
    {
        if (rule == null) return;
        EnsurePipeline();
        _pipeline.AddComboRule(rule);
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

        // エフェクトパイプラインで派生効果を実行
        if (_pipeline == null)
        {
            _logger.LogWarning("[EffectResolver] Pipeline is null. SetQueryService was not called. Skill effects will be skipped.");
            return;
        }

        var context = new SkillEffectContext(
            acter,
            acterFaction,
            targets,
            allyGroup,
            enemyGroup,
            acts,
            battleTurnCount,
            _queryService,
            _random);

        await _pipeline.ExecuteAll(context);
    }

    private void EnsurePipeline()
    {
        if (_pipeline != null) return;
        _pipeline = SkillEffectPipeline.CreateDefault();
    }
}
