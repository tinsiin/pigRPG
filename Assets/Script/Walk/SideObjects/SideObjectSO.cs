using UnityEngine;

[CreateAssetMenu(menuName = "Walk/SideObject")]
public sealed class SideObjectSO : ScriptableObject
{
    [SerializeField] private string id;
    [SerializeField] private string uiLabel;
    [SerializeField] private GameObject prefabLeft;
    [SerializeField] private GameObject prefabRight;
    [SerializeField] private EventDefinitionSO eventDefinition;

    public string Id => id;
    public string UILabel => string.IsNullOrEmpty(uiLabel) ? id : uiLabel;
    public GameObject PrefabLeft => prefabLeft;
    public GameObject PrefabRight => prefabRight;
    public EventDefinitionSO EventDefinition => eventDefinition;
}