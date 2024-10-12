using R3;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum ButtonRole
{
    //ボタンROleの並び順とToggleButtonのGroupでのリストの並び順とButtonSpriteの並び順は一致させる必要がある。
    Default,
    Main,
    CharaConfy,
    Confy
}

public class ToggleButton : Button
//https://qiita.com/mikuri8/items/a546adb2451140167ce5
//https://qiita.com/mikuri8/items/cb8318622e5e67607e23
{
    public enum State
    {
        Default,
        Selected
    }

    [SerializeField] private GameObject _defaultObject; //ボタンの押す範囲
    public ButtonRole MyButtonRole; //インスペクターから登録

    private readonly Subject<State> _onStateChanged = new(); //外部に公開するコールバック関数のバッキングフィールドみたいな
    //[SerializeField] private GameObject _selectedObject;

    private readonly ReactiveProperty<State> _state = new();

    public bool IsManaged { get; set; }

    private new void Awake()
    {
        Initialize();
    }

    public Observable<State> OnStateChangedAsObservable() //公開フィールドみたいな
    {
        return _onStateChanged.AsObservable();
    }

    private void Initialize()
    {
        //ボタンの内部で状態変化時に行うこと
        _state.Subscribe(
            state =>
            {
                _defaultObject.SetActive(state == State.Default);
                //_selectedObject.SetActive(state == State.Selected);

                _onStateChanged.OnNext(state); //状態変化が起きたときに外部での登録出来るコールバックが起きるタイミングを指定
            }).AddTo(this);

        _state.Value = State.Default; //初期値
    }

    public override void OnPointerClick(PointerEventData eventData)
    {
        base.OnPointerClick(eventData);

        SwitchToggleState();
    }

    public void SwitchToggleState()
    {
        if (interactable && !IsManaged) //ismanagedのお陰でそもそも選択したボタンは他の押さないと解除されなくなる。
            switch (_state.Value)
            {
                case State.Default:
                    _state.Value = State.Selected;
                    break;
                case State.Selected:
                    _state.Value = State.Default;
                    break;
            }
    }
}