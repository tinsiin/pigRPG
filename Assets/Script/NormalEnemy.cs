using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Unity.Burst.Intrinsics.X86.Avx;

/// <summary>
/// �ʏ�̓G
/// </summary>
[System.Serializable]
public class NormalEnemy : BaseStates
{
    /// <summary>
    /// ���̓G�L�����N�^�[�̕����������
    /// �蓮������{�I�ɐ����L�����N�^�[�݂̂ɂ��̕����͐ݒ肷��?
    /// </summary>
    public int RecovelySteps;

    /// <summary>
    /// ���̃L�����N�^�[���Đ����邩�ǂ���
    /// False�ɂ���ƈ�x�|���Ɠ�x�Əo�Ă��܂���B�Ⴆ�΋@�B�L�����Ȃ�A��{False�ɂ���B
    /// </summary>
    public bool Reborn;

    /// <summary>
    /// �G����l�̍ۂ̑��������̂܂܃p�[�e�B�[�����ɕϊ����鎫���f�[�^
    /// </summary>
    private static Dictionary<SpiritualProperty,PartyProperty> EnemyLonelyPartyImpression = new Dictionary<SpiritualProperty, PartyProperty>
    {
        {SpiritualProperty.doremis,PartyProperty.Flowerees},
        {SpiritualProperty.pillar,PartyProperty.Odradeks},
        {SpiritualProperty.kindergarden,PartyProperty.TrashGroup},
        {SpiritualProperty.liminalwhitetile,PartyProperty.MelaneGroup},
        {SpiritualProperty.sacrifaith,PartyProperty.HolyGroup},
        {SpiritualProperty.cquiest,PartyProperty.MelaneGroup},
        //{SpiritualProperty.pysco,PartyProperty.T},//chatgpt�̍ŐV�̃`���b�g���烉���_���Ȓl���o��悤�ɂ���B
        {SpiritualProperty.godtier,PartyProperty.Flowerees},
        {SpiritualProperty.baledrival,PartyProperty.TrashGroup},
        {SpiritualProperty.devil,PartyProperty.HolyGroup}
    };

    /// <summary>
    /// ��ʓ��m�̓G�W�܂�AI�̑����̎����f�[�^
    /// �����y�A�͋t���ł������I�ɑΉ������̂ŁA�킴�킴�ǉ����Ȃ���OK
    /// </summary>
    private static Dictionary<(CharacterType, CharacterType), int> TypeMatchupTable = new Dictionary<(CharacterType, CharacterType), int>
    {
        {(CharacterType.TLOA, CharacterType.Life), 80},
        {(CharacterType.TLOA, CharacterType.Machine), 30},
        {(CharacterType.Machine, CharacterType.Life), 60},
        
    };
    /// <summary>
    /// �L�����������m�̓G�W�܂�AI�̑����̎����f�[�^�@����������׏����̏����܂ށB
    /// </summary>
    private static Dictionary<(SpiritualProperty, SpiritualProperty), int> ImpressionMatchupTable = new Dictionary<(SpiritualProperty, SpiritualProperty), int>
    {
        {(SpiritualProperty.liminalwhitetile,SpiritualProperty.pillar),90 },//���[�~�i�����x��
        {(SpiritualProperty.liminalwhitetile,SpiritualProperty.baledrival),90 },//���[�~�i�����x�[��
        {(SpiritualProperty.kindergarden,SpiritualProperty.liminalwhitetile),20 },//�L���_�[�����[�~�i��
        {(SpiritualProperty.cquiest,SpiritualProperty.devil),30 },//�V�[�N�C�G�X�g���f�r��
        {(SpiritualProperty.sacrifaith,SpiritualProperty.cquiest),80 },//���ȋ]�����V�[�N�C�G�X�g
        {(SpiritualProperty.sacrifaith,SpiritualProperty.devil), 70},//���ȋ]�����f�r��
        {(SpiritualProperty.devil,SpiritualProperty.cquiest),60 },//�f�r�����V�[�N�C�G�X�g
        {(SpiritualProperty.devil,SpiritualProperty.kindergarden),80 },//�f�r�����L���_�[
        {(SpiritualProperty.kindergarden,SpiritualProperty.devil),70 },//�L���_���f�r��
        {(SpiritualProperty.baledrival,SpiritualProperty.devil),80 },//�x�[�����f�r��
        {(SpiritualProperty.baledrival,SpiritualProperty.doremis),40},//�x�[�����h���~�X
        {(SpiritualProperty.baledrival,SpiritualProperty.pysco),70 },//�x�[�����T�C�R
        {(SpiritualProperty.baledrival,SpiritualProperty.pillar),80},//�x�[�����x��
        {(SpiritualProperty.baledrival,SpiritualProperty.godtier),100},//�x�[�����S�b�h
        {(SpiritualProperty.pysco,SpiritualProperty.pillar),0 },//�T�C�R���x��
        {(SpiritualProperty.pillar,SpiritualProperty.sacrifaith),100 },//�x�������ȋ]��
    };

    /// <summary>
    /// ���̑����̓G�L�����N�^�[���ŏ��̈�l�ɑI�΂ꂽ�Ƃ��A���̂܂܈�l�ŏI���m���B
    /// </summary>
    private static Dictionary<SpiritualProperty, int> LonelyMatchImpression = new Dictionary<SpiritualProperty, int>
{
    {SpiritualProperty.doremis, 30},
    {SpiritualProperty.pillar, 30},
    {SpiritualProperty.kindergarden, 20},
    {SpiritualProperty.liminalwhitetile, 30},
    {SpiritualProperty.sacrifaith, 70},
    {SpiritualProperty.cquiest, 30},
    {SpiritualProperty.pysco, 30},
    {SpiritualProperty.godtier, 30},
    {SpiritualProperty.baledrival, 30},
    {SpiritualProperty.devil, 30}
};
    /// <summary>
    /// ��l�ŏI���m����Ԃ��A���s������-1��Ԃ��B
    /// </summary>
    /// <param name="I"></param>
    /// <returns></returns>
    public static int LonelyMatchUp(SpiritualProperty I)
    {
        var matchPer = LonelyMatchImpression[I];
        if (Random.Range(1,101)<=matchPer)//�������m���ȉ��Ȃ�A�m����Ԃ��đ���������`����B
        {
            return matchPer;
        }

        return -1;//��l�ŏI���Ȃ������ꍇ���s��Ԃ��B
    }



    /// <summary>
    /// ��ʓ��m�̓G�W�܂�̑��� ��������Ɛ��������ۂ̃p�[�Z���g�������
    /// </summary>
    /// <param name="I">���Ƀp�[�e�B�ɂ��āA�W���b�W�����</param>
    /// <param name="You">�W���b�W����鑤�A�������������</param>
    public static int TypeMatchUp(CharacterType I, CharacterType You)
    {
        // �����Ɋ֌W�Ȃ���ɓ������ʂ𓾂邽�߁AI��You����ёւ���
        var sortedPair = I < You ? (I, You) : (You, I);
        //1<2 �Ȃ�true�Ȃ̂�(1,2)���Ԃ�A�@2<1�Ȃ�false�Ȃ̂ŋt�ɂ���(1,2)���Ԃ�B��ɓ����L�[�𐶐����郍�W�b�N�B
        //���̃��W�b�N�̂��A�ŏ������C�ɂ��Ȃ��Ă����B�@characteType��enum�͈ÖٓI�ɐ��l�ň����邱�ƑO��B

        var matchPer = TypeMatchupTable[sortedPair];//�W���b�W�m��

        if(Random.Range(1,101)<=matchPer)return matchPer;//�������m���ȉ��Ȃ�A�m����Ԃ��đ���������`����B


         return -1;//����������Ȃ������ꍇ���s��Ԃ��B
    }

    /// <summary>
    /// �������m�̓G�W�܂�̑��� ��������Ɛ��������ۂ̃p�[�Z���g�������
    /// </summary>
    /// <param name="I">���Ƀp�[�e�B�ɂ��āA�W���b�W�����</param>
    /// <param name="You">�W���b�W����鑤�A�������������</param>
    public static int ImpressionMatchUp(SpiritualProperty I, SpiritualProperty You)
    {

        // �������d�v�Ȃ̂ŁA���������̂܂܂ɂ��ă}�b�`���m�F
        var key = (I, You);

        // �W���b�W�m���̃f�t�H���g
        var matchPer = 50; 

        if (ImpressionMatchupTable.ContainsKey(key))//�f�t�H���g�̊m���ł͂Ȃ����̂����������ɑ��݂���̂ŁA�����Ŕ���B
        {
           matchPer=ImpressionMatchupTable[key];//�W���b�W�m��
        }
            

        if (Random.Range(1, 101) <= matchPer)return matchPer; // �������m���ȉ��Ȃ�A�m����Ԃ��đ���������`����
        

        return -1;//����������Ȃ������ꍇ���s��Ԃ��B
    }
}
