using Cysharp.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using R3;
using System;
using UnityEngine.UI;
using UnityEngine.Rendering;

public class ToggleButtonGroup : MonoBehaviour
//https://qiita.com/mikuri8/items/cb8318622e5e67607e23
{
    [SerializeField] private List<ToggleButton> _toggleButtons = new();

    [SerializeField] List<Sprite> ButtonsSprite;//�{�^���̃X�v���C�g�̃��X�g�B
    [SerializeField] Image ButtonImage;//�\������C���[�W�B

    private ToggleButton _selectedButton;//�O�ɑI�������{�^����ێ����邽�߂̃o�b�t�@������I

    public Observable<Unit> OnClickAsObservable(int index)//button��r3�g���C�x���g���O���[�v���琧�䂵�ĕԂ����]�b�g
    {
        if(index >= _toggleButtons.Count)//index���͈͊O����������Ԃ��H
        {
            return Observable.Empty<Unit>();
        }
        return _toggleButtons[index].OnClickAsObservable();
        
    }

    private void Awake()
    {
       Initialize();
    }

    private void Initialize()//��x�I���������̂��ēx�I������Ȃ��悤�ȃ��W�b�N������B
    {
        foreach (var one in _toggleButtons)
        {
            one.OnStateChangedAsObservable().Subscribe(
                state =>//���ꂼ��̃{�^���̃X�e�[�^�X���ύX����邽�тɓ����֐��Ƃ��ēo�^����
                {
                    if (state == ToggleButton.State.Selected)//�{�^�����I������Ă��ԂɂȂ����Ȃ�B(������selected�̓z�͂����������̃C�x���g�͔������Ȃ�)
                    {//(����Ɏd�g�ݏ�selected��default�ɖ߂邱�Ƃ��Ȃ��B)
                        if (_selectedButton != null)//�O�ɑI�������{�^������������B
                        {
                            _selectedButton.IsManaged = false;//�O�ɑI�������{�^����ismanaged���������āB
                            _selectedButton.SwitchToggleState();//����ɃX�C�b�`���đI������ĂȂ���Ԃɖ߂��B
                        }

                        one.IsManaged = true;//���̑I�������{�^����Ismanaged��true�ɁB�@ismanaged��true���ƃ{�^�����Ő؂�ւ��Ȃ��悤�ɂȂ��Ă�B
                        _selectedButton = one;//�o�b�t�@�ɓo�^����
                        SetTabButtonImage(one.MyButtonRole);//�C���[�W�ύX�B
                    }
                }).AddTo(this);//���̊Ǘ��I�u�W�F�N�g�ƌ��т���
        }

        SetTabButtonImage(ButtonRole.Default);//�����摜���Z�b�g���Ƃ�
    }
    /// <summary>
    /// �^�u�{�^���̈ꗗ�摜���Z�b�g����B
    /// </summary>
    public void SetTabButtonImage(ButtonRole br)
    {
        ButtonImage.sprite=ButtonsSprite[(int)br];
    }

    /// <summary>
    /// �N������g�O�����O���[�v����I�ԁB�@(�����I������Ă�����̂Ȃ������ismanaged�ɂ���ĕω����Ȃ��B)
    /// </summary>
    /// <param name="index"></param>
    public void SelectToggleIndex(int index)
    {
        if(0<= index && _toggleButtons.Count > index)//�͈̓`�F�b�N
        {
            _toggleButtons[index].SwitchToggleState();
        }
    }

    //���I������Ă�{�^���̃C���f�b�N�X��Ԃ��B
    public int GetSelectedIndex()
    {
        int index = 0;
        for (var i = 0; i < _toggleButtons.Count; i++)
        {
            if (_toggleButtons[i] == _selectedButton)
            {
                index = i;
                break;
            }
        }

        return index;
    }
}
