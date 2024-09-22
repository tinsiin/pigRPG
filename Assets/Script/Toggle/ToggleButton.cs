using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using R3;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public enum ButtonRole
{//�{�^��ROle�̕��я���ToggleButton��Group�ł̃��X�g�̕��я���ButtonSprite�̕��я��͈�v������K�v������B
    Default, Main, CharaConfy, Confy
}
public class ToggleButton : Button
//https://qiita.com/mikuri8/items/a546adb2451140167ce5
//https://qiita.com/mikuri8/items/cb8318622e5e67607e23
{
    public enum State
    {
        Default, Selected
    }
    
    [SerializeField] private GameObject _defaultObject;//�{�^���̉����͈�
    //[SerializeField] private GameObject _selectedObject;

    private ReactiveProperty<State> _state = new();
    public ButtonRole MyButtonRole;//�C���X�y�N�^�[����o�^
    private Subject<State> _onStateChanged = new();//�O���Ɍ��J����R�[���o�b�N�֐��̃o�b�L���O�t�B�[���h�݂�����

    public bool IsManaged { get; set; }

    public Observable<State> OnStateChangedAsObservable()//���J�t�B�[���h�݂�����
    {
        return _onStateChanged.AsObservable();
    }

    private void Awake()
    {
        Initialize();
    }
    private void Initialize()
    {
        //�{�^���̓����ŏ�ԕω����ɍs������
        _state.Subscribe(
            state =>
            {
                _defaultObject.SetActive(state == State.Default);
                //_selectedObject.SetActive(state == State.Selected);

                _onStateChanged.OnNext(state);//��ԕω����N�����Ƃ��ɊO���ł̓o�^�o����R�[���o�b�N���N����^�C�~���O���w��
            }).AddTo(this);

        _state.Value = State.Default;//�����l
    }
    public override void OnPointerClick(PointerEventData eventData)
    {
        base.OnPointerClick(eventData);

        SwitchToggleState();
    }
    public void SwitchToggleState()
    {
        if (interactable && !IsManaged)//ismanaged�̂��A�ł��������I�������{�^���͑��̉����Ȃ��Ɖ�������Ȃ��Ȃ�B
        {
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

}
