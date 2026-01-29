using System;
using R3;
using UnityEngine;

public class ToggleButtons : MonoBehaviour //カスタマイズしやすいTabContentsChangerを継承とジェネリックを使った改造をしての使用するクラス。
{
    public enum TabContentsKind //tabContentsChangerに指定する列挙体
    {
        Players,
        CharactorConfig,
        Config
    }

    [SerializeField] private SampleTabContentsChanger _tabContentsChanger; //その継承したクラスを作る。

    private static readonly Subject<Unit> _onCharaConfigSelectSubject = new();
    /// <summary>
    /// キャラコンフィグのタブに選択される際のコールバック
    /// </summary>
    public static  Observable<Unit> OnCharaConfigSelectAsObservable => _onCharaConfigSelectSubject;

    private void Start()
    {
        _tabContentsChanger.Initialize();//これは切り替え時の動作だからなぁ
        _tabContentsChanger.OnChangeStateAsObservable
            .Subscribe(
                value =>
                {
                     // CharaConfigに切り替わる前の処理
                    if (value.content.Kind.Equals(TabContentsKind.CharactorConfig) && value.active)
                    {
                        //CharaConfigに切り替わる際にイベントを発行する。
                        _onCharaConfigSelectSubject.OnNext(Unit.Default);
                    }
                    value.content.View.SetActive(value.active); //切り替え時のクラスの動作をここで登録する。
                }).AddTo(this);
        //_tabContentsChanger.Select(0);//選んだ状態に予めする奴だから消しとく。

        // SelectedCharacterIdを購読（新キャラクターにも対応）
        var selectedCharacterId = UIStateHub.SelectedCharacterId;
        if (selectedCharacterId != null)
        {
            selectedCharacterId.Subscribe(
                id =>
                {
                    // CharacterIdでUI切り替え（新キャラにも対応）
                    _tabContentsChanger.GetViewFromKind(TabContentsKind.Players).SwitchCharacter(id);
                }).AddTo(this);
            selectedCharacterId.Value = CharacterId.Geino; // とりあえず親父が選ばれてる
        }
        else
        {
            Debug.LogError("ToggleButtons.Start: UIStateHub.SelectedCharacterId が null です");
        }

        var userState = UIStateHub.UserState;
        if (userState != null)
        {
            userState.Subscribe(
                state =>
                {
                    //mainとキャラコンフィグのタブだけ、USERUIの状態によって変化する。
                    _tabContentsChanger.GetViewFromKind(TabContentsKind.Players).SwitchContent(state);
                    _tabContentsChanger.GetViewFromKind(TabContentsKind.CharactorConfig).SwitchContent(state);
                }).AddTo(this);
        }
        else
        {
            Debug.LogError("ToggleButtons.Start: UIStateHub.UserState が null です");
        }
    }

    [Serializable]
    public class SampleTabContentsChanger : TabContentsChanger<TabContents, TabContentsKind>
    {
        //対応する二つのクラスを指定して継承

    }
}
