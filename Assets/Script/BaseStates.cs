using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// �L�����N�^�[�B�̎��
/// </summary>
public enum CharacterType
{
    TLOA,Machine,Life//TLOA���̂��́A�@�B�A����
}
/// <summary>
/// ���������A�X�L���Ɉˑ����A�L�����N�^�[�B�̎�ʂ�l�Ƃ̑����ōU���̒ʂ肪�ς��
/// </summary>
public enum PhysicalProperty
{
    heavy,volten,dishSmack//������A�������]�A�\�f
}
�@
/// <summary>
/// ���_�����A�X�L���A�L�����N�^�[�Ɉˑ����A�L�����N�^�[�͒��O�Ɏg���������K�p�����
/// �����琸�_�������m�ōU���̒ʂ�͐ݒ肳���B
/// </summary>
public enum SpiritualProperty
{
    �@doremis,pillar,kindergarden,liminalwhitetile,sacrifaith,cquiest,pysco,godtier,baledrival,devil
}
/// <summary>
/// ��b�X�e�[�^�X�̃N���X�@�@�N���X���̂��͎̂g�p���Ȃ��̂Œ��ۃN���X
/// </summary>
public abstract class BaseStates
{
    //HP
    public int HP { get; private set; }
    public int MAXHP { get; private set; }

    //�|�C���g
    public int P;
    public int MAXP;

    /// <summary>
    /// ���̃L�����N�^�[�̖��O
    /// </summary>
    public string CharacterName;

    /// <summary>
    /// ���̃L�����N�^�[�̎��
    /// </summary>
    public CharacterType MyType { get; private set; }
    /// <summary>
    /// ���̃L�����N�^�[�̑��� ���_����������
    /// </summary>
    public SpiritualProperty MyImpression {  get; private set; }

    /// <summary>
    /// ���J�o���^�[��
    /// ���U��������ɁA���̃����_���G�I�����X�g�ɓ��荞�ނ܂ł̃^�[���J�E���^�[�B�O�̂߂��Ԃ���2�{�̑��x�ŃJ�E���g�����B
    /// </summary>
    public int recoveryTurn;
    /// <summary>
    /// ���J�o���^�[���̐ݒ�l�B
    /// </summary>
    public int maxRecoveryTurn{ get; private set; }

    [SerializeField]private List<BasePassive> _passiveList;
    //��Ԉُ�̃��X�g
    public IReadOnlyList<BasePassive>�@ PassiveList => _passiveList;

    [SerializeField]private List<BaseSkill> _skillList;
    //�X�L���̃��X�g
    public IReadOnlyList<BaseSkill>�@ SkillList => _skillList;

    //��b�U���h��@�@(�厖�Ȃ̂́A��{�I�ɂ��̕ӂ�͒��X�L���ˑ��Ȃ̂ŁA���Ȃ����ł����ݒ肵�Ȃ����ƁB)
    public int b_DEF;
    public int b_AGI;
    public int b_HIT;
    public int b_ATK;

    /// <summary>
    /// �h��͌v�Z
    /// </summary>
    /// <returns></returns>
    public�@virtual int DEF()
    {
        var def = b_DEF;//��b�h��͂���{�B
        

        return def;
    }
    /// <summary>
    /// �������_��������֐�(��{�͈�ۂ������Ă�X�L�����X�g����K���ɑI�яo��
    /// </summary>
    public virtual void InitializeMyImpression()
    {
        SpiritualProperty that;

        if (SkillList != null)
        {
            var rnd = Random.Range(0, SkillList.Count);
            that = SkillList[rnd].SkillSpiritual;//�X�L���̐��_�����𒊏o
            MyImpression = that;//��ۂɃZ�b�g
        }
        else
        {
            Debug.Log(CharacterName + " �̃X�L������ł��B");
        }
    }

    /// <summary>
    /// �I�[�o���C�h�\�ȃ_���[�W�֐�
    /// </summary>
    /// <param name="atkPoint"></param>
    public virtual void Damage(int atkPoint)
    {
        HP -= atkPoint - DEF();//HP����w�肳�ꂽ�U���͂��������B
    }

    /// <summary>
    /// ���𔻒肷��I�[�o���C�h�\�Ȋ֐�
    /// </summary>
    /// <returns></returns>
    public virtual bool Death()
    {
        if (HP <= 0) return true;
        return false;
    }

    /// <summary>
    /// �p�b�V�u��K�p
    /// </summary>
    public virtual void  ApplyPassive(BasePassive status)
    {
        bool typeMatch = false;
        bool propertyMatch = false;
        //�L�����N�^�[��ʂ̑�������
        foreach (var type in status.TypeOkList)
        {
            if (MyType == type)
            {
                typeMatch = true;
                break;
            }
            
        }
        //�L�����N�^�[������
        foreach (var property in status.CharaPropertyOKList)
        {
            if (MyImpression == property)
            {
                propertyMatch = true;
                break;
            }
        }

        //���������N���A������
        if(typeMatch && propertyMatch)
        {
            bool isactive=false;
            foreach(var passive in _passiveList)
            {
                if(passive == status)
                {
                    isactive = true;//���Ƀ��X�g�Ɋ܂܂�Ă���p�b�V�u�Ȃ�B
                    passive.AddPassivePower(1);//���Ɋ܂܂�Ă�p�b�V�u����������                   
                    break;
                }
            }
            if (!isactive)
            {
                _passiveList.Add(status);//��Ԉُ탊�X�g�ɒ��ڒǉ�
            }
        }



    }//remove������R3�ŏ�������B�@

}
