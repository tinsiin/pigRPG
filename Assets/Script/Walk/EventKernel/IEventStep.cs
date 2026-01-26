using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// イベントステップの基底インターフェース。
/// EventDefinitionSOのsteps配列で多態的に扱う。
/// </summary>
public interface IEventStep
{
    /// <summary>
    /// ステップを実行し、収集したEffectを返す。
    /// </summary>
    UniTask<EffectSO[]> ExecuteAsync(EventContext context);
}

/// <summary>
/// イベント実行に必要なコンテキスト。
/// 各Stepに依存を注入するためのコンテナ。
/// </summary>
public sealed class EventContext
{
    /// <summary>
    /// ゲーム全体のコンテキスト。
    /// </summary>
    public GameContext GameContext { get; set; }

    /// <summary>
    /// 旧システム用イベントUI。
    /// </summary>
    public IEventUI EventUI { get; set; }

    /// <summary>
    /// ノベルパート用UI（IEventUIを継承）。
    /// </summary>
    public INovelEventUI NovelUI { get; set; }

    /// <summary>
    /// ダイアログランナー。
    /// </summary>
    public IDialogueRunner DialogueRunner { get; set; }

    /// <summary>
    /// バトルランナー。
    /// </summary>
    public IBattleRunner BattleRunner { get; set; }

    /// <summary>
    /// イベントランナー（EmitEventStep/BattleStep用）。
    /// </summary>
    public IEventRunner EventRunner { get; set; }

    /// <summary>
    /// 中央オブジェクトのRectTransform（ズーム用）。
    /// </summary>
    public RectTransform CentralObjectRT { get; set; }

    /// <summary>
    /// 実行中に収集されたEffect。
    /// </summary>
    public List<EffectSO> CollectedEffects { get; } = new();

    /// <summary>
    /// 必須依存のバリデーション。
    /// EventRunner起動前に呼び出して未設定を検出。
    /// </summary>
    public void ValidateRequired()
    {
        if (GameContext == null) throw new InvalidOperationException("EventContext: GameContext is required");
        if (EventRunner == null) throw new InvalidOperationException("EventContext: EventRunner is required");
        // DialogueRunner, BattleRunnerはStep使用時にnullチェック
    }
}

/// <summary>
/// イベントランナーのインターフェース。
/// EventContext経由で参照され、EmitEventStep等から使用される。
/// </summary>
public interface IEventRunner
{
    /// <summary>
    /// EventDefinitionSOを実行し、収集したEffectを返す。
    /// </summary>
    UniTask<EffectSO[]> RunAsync(EventDefinitionSO definition, EventContext context);
}
