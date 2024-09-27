using System;
using R3;
using UnityEngine;


    public class ToggleButtons : MonoBehaviour//カスタマイズしやすいTabContentsChangerを継承とジェネリックを使った改造をしての使用するクラス。
    {
        public enum TabContentsKind//tabContentsChangerに指定する列挙体
        {
            Players,
            CharactorConfig,
            Config
        }

        [Serializable]
        public class SampleTabContentsChanger : TabContentsChanger<TabContents, TabContentsKind>
        {//対応する二つのクラスを指定して継承
        }

        [SerializeField]
        private SampleTabContentsChanger _tabContentsChanger;//その継承したクラスを作る。

        void Start()
        {
            _tabContentsChanger.Initialize();
            _tabContentsChanger.OnChangeStateAsObservable
                               .Subscribe(
                                   value =>
                                   {
                                       value.content.View.SetActive(value.active);//切り替え時のクラスの動作をここで登録する。
                                   }).AddTo(this);
            //_tabContentsChanger.Select(0);//選んだ状態に予めする奴だから消しとく。
        }
    }

