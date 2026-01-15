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
    [SerializeField] private EventDefinitionSO onWin;
    [SerializeField] private EventDefinitionSO onLose;
    [SerializeField] private EventDefinitionSO onEscape;

    public string Id => id;
    public string UILabel => string.IsNullOrEmpty(uiLabel) ? id : uiLabel;
    public IReadOnlyList<NormalEnemy> EnemyList => enemyList;
    public int EnemyCount => enemyCount;
    public float EscapeRate => escapeRate;
    public EventDefinitionSO OnWin => onWin;
    public EventDefinitionSO OnLose => onLose;
    public EventDefinitionSO OnEscape => onEscape;
}
