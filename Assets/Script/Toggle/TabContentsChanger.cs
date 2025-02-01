using System;
using System.Collections.Generic;
using R3;
using UnityEngine;

public class TabContentsChanger<TView, TKind>
    where TView : MonoBehaviour
    where TKind : Enum
//https://qiita.com/mikuri8/items/cc807a6c8de2ca7cb95e
//このジェネリッククラスは扱える列挙体にView(UI)を自由に指定出来るようにする為のもの？
{
    private readonly Subject<(ContentHolder, bool)> _onChangeStateSubject = new(); //バッキングフィールド??

    [SerializeField] private ToggleButtonGroup _toggleButtonGroup;

    public List<ContentHolder> Contents;

    /// <summary>
    ///     切り替え時のコールバック
    /// </summary>
    public Observable<(ContentHolder content, bool active)> OnChangeStateAsObservable =>
        _onChangeStateSubject; //公開フィールド?　ラムダでget専用プロパティ

    public void Initialize()
    {
        for (var i = 0; i < Contents.Count; i++)
        {
            //ボタンとコンテンツのインデックスは統一しているという前提　↓でコンテンツとボタンに同じインデックスを渡している。
            var content = Contents[i]; //第一ループの該当のコンテンツホルダー
            _toggleButtonGroup.OnClickAsObservable(i) //第一ループで回るボタンに登録するクリック時のコールバックを登録する。
                .Subscribe(
                    _ =>
                    {
                        //このforeachループは、あるボタンを押したときの全てのコンテンツ(ボタンを押すと変わる奴)
                        //の挙動を予めどう動くかここで登録してる、ってイメージ

                        foreach (var one in Contents) //コンテンツ全てに実行
                        {
                            var active = one.Kind.Equals(content.Kind); //選んだボタンに対応するコンテンツとループしてるコンテンツが一致してるか
                            _onChangeStateSubject.OnNext((one, active)); //変更コールバックを第二ループのコンテンツホルダーと上の比較結果を渡す。
                            //一つのボタンを押した際に全てのコンテンツに実行する

                            one.SetSelect(active); //第二ループのisSelectに比較結果boolを渡す
                            //選択されてる物だけにContentHolderのisselectがtrueになるようになってる
                        }
                    }).AddTo(content.View); //第一ループの該当コンテンツホルダーのViewに結びつける
        }
    }

    /// <summary>
    ///     指定インデックスの選択  
    /// </summary>
    public void Select(int index)
    {
        _toggleButtonGroup.SelectToggleIndex(index); //グループ管理クラスから選択する
        for (var i = 0; i < Contents.Count; i++)
        {
            Contents[i].SetSelect(i == index); //コンテンツの該当をセレクトする
            _onChangeStateSubject.OnNext((Contents[i], i == index)); //コンテンツとアクティブを返して実行する。
        }
    }

    /// <summary>
    /// kind列挙体でViewのmonobehaviorを入手する
    /// </summary>
    /// <param name="kind"></param>
    /// <returns></returns>
    public TView GetViewFromKind(TKind kind)
    {
        var holder = Contents.Find(content => content.Kind.Equals(kind));

        return holder.View;
    }

    /// <summary>
    ///     現在アクティブなContentHolderの取得
    /// </summary>
    /// <returns></returns>
    public ContentHolder GetActiveContent()
    {
        return Contents.Find(content => content.IsSelect); //listのfindでvar省略のラムダでそれが合致するなら、それを返す。
    }

    [Serializable]
    public class ContentHolder //リスト化して使用する為にクラスとして作成
    {
        public TView View; //MonoBehaviorなら何でも受け入れるので、MonoBehaviorをもったGameObjectを何でもSerializeFieldから登録できる
        public TKind Kind;

        public bool IsSelect { get; private set; } 

        public void SetSelect(bool select)
        {
            IsSelect = select;
        }
    }
}