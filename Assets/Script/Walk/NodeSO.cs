using UnityEngine;

[CreateAssetMenu(menuName = "Walk/Node")]
public sealed class NodeSO : ScriptableObject
{
    [SerializeField] private string nodeId;
    [SerializeField] private string displayName;
    [SerializeField] private NodeUIHints uiHints;
    [SerializeField] private SideObjectTableSO sideObjectTable;
    [SerializeField] private EncounterTableSO encounterTable;
    [SerializeField] private EventDefinitionSO onEnterEvent;
    [SerializeField] private EventDefinitionSO onExitEvent;
    [SerializeField] private EventDefinitionSO centralEvent;
    [SerializeField] private CentralObjectVisual centralVisual;
    [SerializeField] private ExitSpawnRule exitSpawn;
    [SerializeField] private ExitCandidate[] exits;

    public string NodeId => nodeId;
    public string DisplayName => displayName;
    public NodeUIHints UiHints => uiHints;
    public SideObjectTableSO SideObjectTable => sideObjectTable;
    public EncounterTableSO EncounterTable => encounterTable;
    public EventDefinitionSO OnEnterEvent => onEnterEvent;
    public EventDefinitionSO OnExitEvent => onExitEvent;
    public EventDefinitionSO CentralEvent => centralEvent;
    public CentralObjectVisual CentralVisual => centralVisual;
    public ExitSpawnRule ExitSpawn => exitSpawn;
    public ExitCandidate[] Exits => exits;
}

[System.Serializable]
public struct CentralObjectVisual
{
    [SerializeField] private Sprite sprite;
    [SerializeField] private Vector2 size;
    [SerializeField] private Vector2 offset;
    [SerializeField] private Color tint;

    public Sprite Sprite => sprite;
    public Vector2 Size => size;
    public Vector2 Offset => offset;
    public Color Tint => tint;
    public bool HasSprite => sprite != null;
    public bool HasVisual => HasSprite || (size.x > 0f && size.y > 0f);
}

[System.Serializable]
public struct NodeUIHints
{
    [SerializeField] private bool useThemeColors;
    [SerializeField] private Color frameArtColor;
    [SerializeField] private Color twoColor;
    [SerializeField] private bool useActionMarkColor;
    [SerializeField] private Color actionMarkColor;
    [SerializeField] private string backgroundId;

    public bool UseThemeColors => useThemeColors;
    public Color FrameArtColor => frameArtColor;
    public Color TwoColor => twoColor;
    public bool UseActionMarkColor => useActionMarkColor;
    public Color ActionMarkColor => actionMarkColor;
    public string BackgroundId => backgroundId;
    public bool HasBackgroundId => !string.IsNullOrEmpty(backgroundId);
}
