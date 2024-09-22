using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// �p�[�e�B�[����
/// </summary>
public enum PartyProperty
{
    TrashGroup,HolyGroup,MelaneGroup,Odradeks,Flowerees
        //�n�����A����(�K���A�ړI�g��)�A�����[���Y(�����I)�A�I�h���f�N�X(�������痣��Ă�A�ڂ��J���ĉ�X���痣���悤�ɃR���R����)�A�Ԏ�(�I�T��)
}
/// <summary>
/// �킢������p�[�e�B�[�̃N���X
/// </summary>
public  class BattleGroup
{
    /// <summary>
    /// �p�[�e�B�[����
    /// </summary>
    public PartyProperty OurImpression;

    private List<BaseStates> _ours;

    /// <summary>
    /// �R���X�g���N�^
    /// </summary>
    public BattleGroup(List<BaseStates> ours, PartyProperty ourImpression)
    {
        _ours = ours;
        OurImpression = ourImpression;
    }

    /// <summary>
    /// �W�c�̐l�����X�g
    /// </summary>
    public IReadOnlyList<BaseStates> Ours => _ours;

    /// <summary>
    /// �^����ꂽ�G�̃��X�g����ɍ���̓G�����߂�A
    /// �ėp�I�ȑ����œG���W�߂ă��X�g�ŕԂ��ÓI�֐�
    /// </summary>
    public static BattleGroup EnemyCollectAI(List<NormalEnemy> targetList)
    {
        List<BaseStates> ResultList = new List<BaseStates>();//�Ԃ��p�̃��X�g
        PartyProperty ourImpression = PartyProperty.TrashGroup;//�����l�͔n����

        //�ŏ��̈�l�̓����_���őI��
        var rndIndex = Random.Range(0, targetList.Count - 1);//�����_���C���f�b�N�X�w��
        var ReferenceOne= targetList[rndIndex];//���o
        targetList.RemoveAt(rndIndex);//�폜
        ResultList.Add(ReferenceOne);//�ǉ�

        //������(��l����)�@������if������while���܂�
        if(NormalEnemy.LonelyMatchUp(ReferenceOne.MyImpression)>0){

            //�p�[�e�B�[���������߂�@��l�Ȃ̂ł��̈�l�̑��������̂܂܃p�[�e�B�[�����ɂ���

            return new BattleGroup(ResultList,ourImpression) ;//��l�����̏ꍇ�͂��̂܂ܕԂ�      
        }

        while (true)
        {
            //�܂��ᖡ��������Ώۂ������_���ɑI��
            var targetIndex = Random.Range(0, targetList.Count - 1);//�����_���ŃC���f�b�N�X�w��
            int TypePer;//��ʂ̑����l
            int ImpPer;//��ۂ̑����l
            int okCount = 0;//�K����

            foreach(var one in ResultList)//���ɑI�΂ꂽ�G�S���Ƃ̑���������
            {
                //��ʓ��m�̔��� if�����ŕϐ��ɑ���ł���
                if ((TypePer = NormalEnemy.TypeMatchUp(one.MyType, targetList[targetIndex].MyType)) > 0)
                {
                    //�������m�̔���@��N���A������
                    if ((ImpPer = NormalEnemy.ImpressionMatchUp(one.MyImpression, targetList[targetIndex].MyImpression)) > 0)
                    {
                        okCount++;//�K�����𑝂₷
                    }
                }
            }
            //foreach�őS���Ƃ̑�����������A����������B
            if (okCount == ResultList.Count)//�S���Ƃ̑��������v������
            {
                ResultList.Add(targetList[targetIndex]);//�ǉ�
                targetList.RemoveAt(targetIndex);//�폜
            }

            //������

            if(ResultList.Count>=3) break;//�O�l�ɂȂ����狭���I��

        }

        return new BattleGroup(ResultList, ourImpression);
    }    
}
