using System.Collections.Generic;
using R3;
using UnityEngine;
using UnityEngine.UI;

public class ToggleButtonGroup : MonoBehaviour
//https://qiita.com/mikuri8/items/cb8318622e5e67607e23
{
    [SerializeField] private List<ToggleButton> _toggleButtons = new();

    [SerializeField] private List<Sprite> ButtonsSprite; //ボタンのスプライトのリスト。
    [SerializeField] private Image ButtonImage; //表示するイメージ。

    private ToggleButton _selectedButton; //前に選択したボタンを保持するためのバッファかこれ！

    private void Awake()
    {
        Initialize();
    }

    public Observable<Unit> OnClickAsObservable(int index) //buttonのr3拡張イベントをグループから制御して返すメゾット
    {
        if (index >= _toggleButtons.Count) //indexが範囲外だったら空を返す？
            return Observable.Empty<Unit>();
        return _toggleButtons[index].OnClickAsObservable();
    }

    private void Initialize() //一度選択したものが再度選択されないようなロジックがある。
    {
        foreach (var one in _toggleButtons)
            one.OnStateChangedAsObservable().Subscribe(
                state => //それぞれのボタンのステータスが変更されるたびに動く関数として登録する
                {
                    if (state == ToggleButton.State.Selected) //ボタンが選択されてる状態になったなら。(元からselectedの奴はそもそもこのイベントは発生しない)
                    {
                        //(それに仕組み上selectedがdefaultに戻ることもない。)
                        if (_selectedButton != null) //前に選択したボタンがあったら。
                        {
                            _selectedButton.IsManaged = false; //前に選択したボタンのismanagedを解除して。
                            _selectedButton.SwitchToggleState(); //さらにスイッチして選択されてない状態に戻す。
                        }

                        one.IsManaged = true; //その選択したボタンのIsmanagedをtrueに。　ismanagedがtrueだとボタン側で切り替わらないようになってる。
                        _selectedButton = one; //バッファに登録する
                        SetTabButtonImage(one.MyButtonRole); //イメージ変更。
                    }
                }).AddTo(this); //この管理オブジェクトと結びつける

        SetTabButtonImage(ButtonRole.Default); //初期画像をセットしとく
    }

    /// <summary>
    ///     タブボタンの一覧画像をセットする。
    /// </summary>
    public void SetTabButtonImage(ButtonRole br)
    {
        ButtonImage.sprite = ButtonsSprite[(int)br];
    }

    /// <summary>
    ///     起動するトグルをグループから選ぶ。　(もし選択されているものなら内部のismanagedによって変化しない。)
    /// </summary>
    /// <param name="index"></param>
    public void SelectToggleIndex(int index)
    {
        if (0 <= index && _toggleButtons.Count > index) //範囲チェック
            _toggleButtons[index].SwitchToggleState();
    }

    //今選択されてるボタンのインデックスを返す。
    public int GetSelectedIndex()
    {
        var index = 0;
        for (var i = 0; i < _toggleButtons.Count; i++)
            if (_toggleButtons[i] == _selectedButton)
            {
                index = i;
                break;
            }

        return index;
    }
}