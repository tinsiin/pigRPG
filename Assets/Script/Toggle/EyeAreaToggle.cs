using R3;
using UnityEngine;

/// <summary>
/// EyeArea側のContent切替を管理するコンポーネント。
/// UIStateHub.EyeStateを購読してWalk/Novel/Battleを切り替える。
/// 4つのズーム階層それぞれにEyeAreaContentsを持つ。
/// </summary>
public class EyeAreaToggle : MonoBehaviour
{
    [Header("4階層のContent（各階層にEyeAreaMainContentをアタッチ）")]
    [SerializeField] private EyeAreaContents zoomBackContent;
    [SerializeField] private EyeAreaContents middleFixedContent;
    [SerializeField] private EyeAreaContents zoomFrontContent;
    [SerializeField] private EyeAreaContents frontFixedContent;

    private ReactiveProperty<EyeAreaState> eyeState;

    public EyeAreaState CurrentState => eyeState?.Value ?? EyeAreaState.Walk;

    private void Awake()
    {
        eyeState = new ReactiveProperty<EyeAreaState>(EyeAreaState.Walk);
        UIStateHub.BindEyeArea(eyeState);
    }

    private void Start()
    {
        // EyeAreaState購読 → 全階層に適用
        eyeState.Subscribe(state =>
        {
            ApplyStateToAllLayers(state);
        }).AddTo(this);

        // TabState購読 → EyeAreaStateに変換
        var userState = UIStateHub.UserState;
        if (userState != null)
        {
            userState.Subscribe(tabState =>
            {
                eyeState.Value = TabStateToEyeAreaState(tabState);
            }).AddTo(this);
        }

        // 初期状態を適用
        ApplyStateToAllLayers(eyeState.Value);
    }

    private void OnDestroy()
    {
        UIStateHub.ClearEyeArea(eyeState);
    }

    private void ApplyStateToAllLayers(EyeAreaState state)
    {
        zoomBackContent?.SwitchContent(state);
        middleFixedContent?.SwitchContent(state);
        zoomFrontContent?.SwitchContent(state);
        frontFixedContent?.SwitchContent(state);
    }

    private static EyeAreaState TabStateToEyeAreaState(TabState tabState)
    {
        switch (tabState)
        {
            case TabState.FieldDialogue:
            case TabState.EventDialogue:
            case TabState.NovelChoice:
                return EyeAreaState.Novel;

            case TabState.Skill:
            case TabState.SelectTarget:
            case TabState.SelectRange:
            case TabState.TalkWindow:
            case TabState.NextWait:
                return EyeAreaState.Battle;

            case TabState.walk:
            default:
                return EyeAreaState.Walk;
        }
    }
}
