using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 戦闘を起動するEventStep。
/// EncounterSOを参照し、結果に応じたEffectを返す。
/// </summary>
[Serializable]
public sealed class BattleStep : IEventStep
{
    [Header("戦闘設定")]
    [SerializeField] private EncounterSO encounter;

    [Header("結果別Effect（EncounterSO側を上書きする場合）")]
    [Tooltip("trueの場合、Step側のEffect設定を使用。falseの場合、EncounterSO側のEventDefinitionを実行。")]
    [SerializeField] private bool overrideOutcomeEffects;
    [SerializeField] private EffectSO[] onWin;
    [SerializeField] private EffectSO[] onLose;
    [SerializeField] private EffectSO[] onEscape;

    public EncounterSO Encounter => encounter;
    public bool OverrideOutcomeEffects => overrideOutcomeEffects;

    public BattleStep() { }

    public BattleStep(EncounterSO encounter, bool overrideOutcomeEffects = false)
    {
        this.encounter = encounter;
        this.overrideOutcomeEffects = overrideOutcomeEffects;
    }

    public async UniTask<EffectSO[]> ExecuteAsync(EventContext context)
    {
        if (encounter == null)
        {
            Debug.LogWarning("BattleStep: encounter is null.");
            return Array.Empty<EffectSO>();
        }

        if (context.BattleRunner == null)
        {
            Debug.LogWarning("BattleStep: BattleRunner is null.");
            return Array.Empty<EffectSO>();
        }

        // 戦闘実行
        var battleContext = new EncounterContext(encounter, context.GameContext);
        var result = await context.BattleRunner.RunBattleAsync(battleContext);

        // 戦闘が発生しなかった場合
        if (!result.Encountered)
        {
            return Array.Empty<EffectSO>();
        }

        // 結果に応じたEffect決定
        if (overrideOutcomeEffects)
        {
            // Step側の設定を使用
            return result.Outcome switch
            {
                BattleOutcome.Victory => onWin ?? Array.Empty<EffectSO>(),
                BattleOutcome.Defeat => onLose ?? Array.Empty<EffectSO>(),
                BattleOutcome.Escape => onEscape ?? Array.Empty<EffectSO>(),
                _ => Array.Empty<EffectSO>()
            };
        }
        else
        {
            // EncounterSO側のEventDefinitionを実行
            if (context.EventRunner == null)
            {
                Debug.LogWarning("BattleStep: EventRunner is null, cannot run outcome event.");
                return Array.Empty<EffectSO>();
            }

            var outcomeEvent = result.Outcome switch
            {
                BattleOutcome.Victory => encounter.OnWin,
                BattleOutcome.Defeat => encounter.OnLose,
                BattleOutcome.Escape => encounter.OnEscape,
                _ => null
            };

            if (outcomeEvent != null)
            {
                // EventDefinitionSOを実行してEffectを収集
                return await context.EventRunner.RunAsync(outcomeEvent, context);
            }

            return Array.Empty<EffectSO>();
        }
    }
}
