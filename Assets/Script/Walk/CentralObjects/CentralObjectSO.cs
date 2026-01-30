using UnityEngine;

[CreateAssetMenu(menuName = "Walk/CentralObject")]
public sealed class CentralObjectSO : ScriptableObject
{
    [SerializeField] private string id;
    [SerializeField] private string uiLabel;
    [SerializeField] private CentralObjectVisual visual;
    [SerializeField] private EventDefinitionSO eventDefinition;

    public string Id => id;
    public string UILabel => string.IsNullOrEmpty(uiLabel) ? id : uiLabel;
    public CentralObjectVisual Visual => visual;
    public EventDefinitionSO EventDefinition => eventDefinition;
}
