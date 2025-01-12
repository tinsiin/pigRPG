using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// �ǉ������HP�̑w�@������Ə�����B
/// ���̃N���X���̂̃C���[�W�Ƃ��ẮA�p�b�V�u�Ɛ����������̈Ӗ������ŋ������ڂ��Ă��邪�A
/// �����܂Ńp�b�V�u�̒��̈�̋@�\�̌`�ɉ߂����A�܂��A�p�b�V�u���L�����ɗ^����e���͂��̒ǉ�HP�w�ɂ͗^�����Ȃ��Ƃ����C���[�W�B
/// </summary>
public class BaseVitalLayer
{
    /// <summary>
    /// �}�X�^�[���X�g���甲���o�����߂̔��ʗpID
    /// </summary>
    public int id;

    /// <summary>
    /// �ςݏd�Ȃ�ۂ̗D�揇��
    /// �����Ⴂ������@�����Ȃ�撅���ɐςݏd�Ȃ�B
    /// </summary>
    public int Priority;

    [SerializeField]
    private float _layhp;
    /// <summary>
    /// ���C���[HP
    /// </summary>
    public float LayerHP
    {
        get { return _layhp; }
        set
        {
            if (value > MaxLayerHP)//�ő�l�𒴂��Ȃ��悤�ɂ���
            {
                _layhp = MaxLayerHP;
            }
            else _layhp = value;
        }
    }
    [SerializeField]
    private float _maxLayhp;
    public float MaxLayerHP => _maxLayhp;

    /// <summary>
    /// HP���ő�l�܂ōĕ�[
    /// </summary>
    public void ReplenishHP()
    {
        LayerHP = 999999;
    }


    /// <summary>
    /// �퓬�̒��f�ɂ���ď����邩�ǂ���
    /// </summary>
    public bool IsBattleEndRemove;

    /// <summary>
    /// ���̒ǉ�HP������Ƀ��W�F�l���邩�ǂ����B
    /// �ǉ�HP�̓L�����N�^�[�̃p�b�V�u�Ƃ͓Ɨ��������ł���B
    /// ������A��{HP�ɉe���͗^���Ȃ����L�������̂Ɋ|�����Ă���p�b�V�u���ǉ�HP�ɉe���͗^���Ȃ��B
    /// </summary>
    public float Regen=0f;
    //���ۂ̃��W�F�l������BaseStates?

    //�ǉ�HP���̂̕��������ւ̑ϐ�
    public float HeavyResistance;
    public float voltenResistance;
    public float DishSmackRsistance;

}
