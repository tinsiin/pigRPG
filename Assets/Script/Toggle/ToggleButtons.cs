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

        Walking.Instance.SKILLUI_state.Subscribe(
            state =>
            {
                //今の所メイン画面のみがキャラ状態によって変わる
                _tabContentsChanger.GetViewFromKind(TabContentsKind.Players).CharaStateSwitch(state);


            }).AddTo(this);
        Walking.Instance.SKILLUI_state.Value = SkillUICharaState.geino;//とりあえず親父が選ばれてる

        Walking.Instance.USERUI_state.Subscribe(
            state =>
            {
                //mainとキャラコンフィグのタブだけ、USERUIの状態によって変化する。
                _tabContentsChanger.GetViewFromKind(TabContentsKind.Players).SwitchContent(state);
                _tabContentsChanger.GetViewFromKind(TabContentsKind.CharactorConfig).SwitchContent(state);
            }).AddTo(this);
    }

    [Serializable]
    public class SampleTabContentsChanger : TabContentsChanger<TabContents, TabContentsKind>
    {
        //対応する二つのクラスを指定して継承

    }
}