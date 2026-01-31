using UnityEngine;

[CreateAssetMenu(menuName = "Walk/SideObject")]
public sealed class SideObjectSO : ScriptableObject
{
    [SerializeField] private string id;
    [SerializeField] private string uiLabel;
    [SerializeField] private GameObject prefabLeft;
    [SerializeField] private GameObject prefabRight;

    [Header("イベントキュー")]
    [SerializeField] private EventQueueEntry[] events;

    public string Id => id;
    public string UILabel => string.IsNullOrEmpty(uiLabel) ? id : uiLabel;
    public GameObject PrefabLeft => prefabLeft;
    public GameObject PrefabRight => prefabRight;
    public EventQueueEntry[] Events => events;

    /// <summary>
    /// 後方互換用: 最初のイベント定義を取得。
    /// </summary>
    public EventDefinitionSO EventDefinition =>
        events != null && events.Length > 0 ? events[0].EventDefinition : null;
}