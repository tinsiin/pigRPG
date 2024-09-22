using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayersStates //�Z�[�u�ŃZ�[�u�����悤�Ȏ����Ƃ����C�����[�v�ő��삷�邽�߂̃X�e�[�^�X����
{

    /*public BassJackStates geino;
    public SateliteProcessStates sites;
    public StairStates noramlia;*/
    int _nowProgress;
    int _nowStageID;
    int _nowAreaID;

    /// <summary>
    /// ���ݐi�s�x
    /// </summary>
    public int NowProgress => _nowProgress;
    /// <summary>
    /// ���݂̃X�e�[�W
    /// </summary>
    public int NowStageID => _nowStageID;
    /// <summary>
    /// ���݂̃X�e�[�W���̃G���A
    /// </summary>
    public int NowAreaID => _nowAreaID;

    public PlayersStates()//�R���X�g���N�^�[ 
    { 
        _nowProgress = 0;
        _nowStageID = 0;
        _nowAreaID = 0;
        //geino = new BassJackStates(3,4,5,6,7,8);
        //sites = new SateliteProcessStates(3,4,5,6,7,8);
        //noramlia = new StairStates(3, 4, 5, 6, 7, 8);

        //�Z�[�u�f�[�^����Ȃ炱�̌�ɏ���
    }

    /// <summary>
    /// �i�s�x�𑝂₷  
    /// </summary>
    /// <param name="addPoint"></param>
    public void AddProgress(int addPoint)
    {
        _nowProgress += addPoint;
    }
    /// <summary>
    /// ���ݐi�s�x���[���ɂ���
    /// </summary>
    public void ProgressReset()
    {
        _nowProgress = 0;
    }

    /// <summary>
    /// �G���A���Z�b�g����B
    /// </summary>
    public void SetArea(int id)
    {
        _nowAreaID = id;
        Debug.Log(id + "��PlayerStates�ɋL�^");
    }


}
/*
public class BassJackStates : BaseStates//���ʃX�e�[�^�X�Ƀv���X�ł��ꂼ��̃L�����̓Ǝ��X�e�[�^�X�Ƃ����̏���
{
    public BassJackStates(int c_hp, int c_maxhp, int c_p, int c_maxP, int c_DEF, int c_ATK, int c_HIT, int c_AGI)
         : base(c_hp, c_maxhp, c_p, c_maxP, c_DEF, c_ATK, c_HIT, c_AGI)
    {//��l���̃R���X�g���N�^
       
    }
}
public class SateliteProcessStates : BaseStates//���ʃX�e�[�^�X�Ƀv���X�ł��ꂼ��̃L�����̓Ǝ��X�e�[�^�X�Ƃ����̏���
{
    public SateliteProcessStates(int c_hp, int c_maxhp, int c_p, int c_maxP, int c_DEF, int c_ATK, int c_HIT, int c_AGI)
         : base(c_hp, c_maxhp, c_p, c_maxP, c_DEF, c_ATK, c_HIT, c_AGI)
    {//�����̃R���X�g���N�^

    }
}
public class StairStates : BaseStates//���ʃX�e�[�^�X�Ƀv���X�ł��ꂼ��̃L�����̓Ǝ��X�e�[�^�X�Ƃ����̏���
{//��y�̃R���X�g���N�^
    public StairStates(int c_hp, int c_maxhp, int c_p, int c_maxP, int c_DEF, int c_ATK, int c_HIT, int c_AGI)
        :base(c_hp, c_maxhp, c_p, c_maxP, c_DEF, c_ATK, c_HIT,c_AGI)
    {//�����̃R���X�g���N�^
        
    }
}
*/