using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Walk/Encounter")]
public sealed class EncounterSO : ScriptableObject
{
    [SerializeField] private string id;
    [SerializeField] private string uiLabel;
    [SerializeReference, SelectableSerializeReference] private List<NormalEnemy> enemyList = new();
    [SerializeField] private int enemyCount = 2;
    [SerializeField] private float escapeRate = 50f;

    [Header("ストーリー敵フラグ")]
    [Tooltip("trueの場合、友情コンビ登録の対象外になる")]
    [SerializeField] private bool isStoryEncounter;

    [Header("勝利時イベントキュー")]
    [SerializeField] private EventQueueEntry[] onWinEvents;

    [Header("敗北時イベントキュー")]
    [SerializeField] private EventQueueEntry[] onLoseEvents;

    [Header("逃走時イベントキュー")]
    [SerializeField] private EventQueueEntry[] onEscapeEvents;

    public string Id => id;
    public string UILabel => string.IsNullOrEmpty(uiLabel) ? id : uiLabel;
    public IReadOnlyList<NormalEnemy> EnemyList => enemyList;
    public int EnemyCount => enemyCount;
    public float EscapeRate => escapeRate;
    public bool IsStoryEncounter => isStoryEncounter;
    public EventQueueEntry[] OnWinEvents => onWinEvents;
    public EventQueueEntry[] OnLoseEvents => onLoseEvents;
    public EventQueueEntry[] OnEscapeEvents => onEscapeEvents;

    /// <summary>
    /// 後方互換用: 最初のイベント定義を取得。
    /// </summary>
    public EventDefinitionSO OnWin =>
        onWinEvents != null && onWinEvents.Length > 0 ? onWinEvents[0].EventDefinition : null;

    public EventDefinitionSO OnLose =>
        onLoseEvents != null && onLoseEvents.Length > 0 ? onLoseEvents[0].EventDefinition : null;

    public EventDefinitionSO OnEscape =>
        onEscapeEvents != null && onEscapeEvents.Length > 0 ? onEscapeEvents[0].EventDefinition : null;
}
