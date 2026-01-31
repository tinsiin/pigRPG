using UnityEngine;

[CreateAssetMenu(menuName = "Walk/CentralObject")]
public sealed class CentralObjectSO : ScriptableObject
{
    [SerializeField] private string id;
    [SerializeField] private string uiLabel;
    [SerializeField] private CentralObjectVisual visual;

    [Header("イベントキュー")]
    [SerializeField] private EventQueueEntry[] events;

    public string Id => id;
    public string UILabel => string.IsNullOrEmpty(uiLabel) ? id : uiLabel;
    public CentralObjectVisual Visual => visual;
    public EventQueueEntry[] Events => events;

    /// <summary>
    /// 後方互換用: 最初のイベント定義を取得。
    /// </summary>
    public EventDefinitionSO EventDefinition =>
        events != null && events.Length > 0 ? events[0].EventDefinition : null;
}
