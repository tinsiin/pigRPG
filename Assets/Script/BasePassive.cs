using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using R3;

/// <summary>
/// ��b��Ԃ̒��ۃN���X
/// </summary>
public�@abstract class BasePassive 
{
    /// <summary>
    /// PassivePower�̐ݒ�l
    /// </summary>
    public int MaxPassivePower;

    /// <summary>
    /// ���̒l�̓p�b�V�u��"�d�ˊ|��" ��&lt;�v����4&gt;
    /// </summary>
    public int PassivePower { get; private set; }

    /// <summary>
    /// �p�b�V�u���d�ˊ|������B
    /// </summary>
    /// <param name="addpoint"></param>
    public void AddPassivePower(int addpoint)
    {
        PassivePower += addpoint;
        if(PassivePower > MaxPassivePower)PassivePower = MaxPassivePower;//�ݒ�l�𒴂�����ݒ�l�ɂ���
    }

    /// <summary>
    /// �K�������ʂ̃��X�g�B�@��ʂ͈�l��Ȃ̂ŁA���f��͂��ꂾ����OK
    /// </summary>
    public List<CharacterType> TypeOkList;

    //�K������L��������(���_����)�̃��X�g�@
    public List<SpiritualProperty> CharaPropertyOKList;

    /// <summary>
    /// ���s�����ʁ@basestates��applypassive�ōw�ǂ���
    /// </summary>
    public abstract void WalkEffect();
    
    /// <summary>
    /// �퓬�����ʁ@basestates��applypassive�ōw�ǂ���
    /// </summary>
    public abstract void BattleEffect();
}
