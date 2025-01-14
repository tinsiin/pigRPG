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
    public float Regen = 0f;
    //���ۂ̃��W�F�l������BaseStates?

    //�ǉ�HP���̂̕��������ւ̑ϐ�
    public float HeavyResistance = 1.0f;
    public float voltenResistance = 1.0f;
    public float DishSmackRsistance = 1.0f;

    /// <summary>
    /// �o���A�̑ϐ����ǂ��������iA/B/C��؂�ւ��j
    /// </summary>
    public BarrierResistanceMode ResistMode ;

    /// <summary>
    /// �_���[�W���w��ʉ߂���  �^����ꂽ�_���[�W�͒ʉ߂��Čy������Ԃ�
    /// </summary>
    public float PenetrateLayer(float dmg, PhysicalProperty impactProperty)
    {
        // 1) ���������ɉ������ϐ������擾
        float resistRate = 1.0f;
        switch (impactProperty)
        {
            case PhysicalProperty.heavy:
                resistRate = HeavyResistance;
                break;
            case PhysicalProperty.volten:
                resistRate = voltenResistance;
                break;
            case PhysicalProperty.dishSmack:
                resistRate = DishSmackRsistance;
                break;
        }

        // 2) �y����̎��_���[�W
        float dmgAfter = dmg * resistRate;

        // 3) ���C���[HP�����
        float leftover = LayerHP - dmgAfter; // leftover "HP" => �����}�C�i�X�Ȃ�j��
        if (leftover <= 0f)
        {
            // �j�󂳂ꂽ
            float overkill = -(leftover); // -negative => positive
            var tmpHP = LayerHP;//�d�g��C�p�ɍ���󂯂鎞��LayerHP��ۑ��B
            LayerHP = 0f; // ������HP�̓[��

            // �d�g�݂̈Ⴂ
            switch (ResistMode)
            {
                case BarrierResistanceMode.A_SimpleNoReturn:
                    // A�͈�x�y���������͖߂��Ȃ�: overkill �����̂܂܎���
                    return overkill;

                case BarrierResistanceMode.B_RestoreWhenBreak:
                    // B�́u�y����_���[�W�v�������ɖ߂� => leftover �� "�� resistRate" �Ŋg��
                    // ������ overkill �� "dmgAfter - LayerHP" �̌���
                    // �� �d�g��B: leftoverDamage = overkill / resistRate
                    float restored = overkill / resistRate;
                    return restored;

                case BarrierResistanceMode.C_IgnoreWhenBreak:
                    // C�͌��U�� - ���݂�LayerHP
                    // leftover(= overkill)�𖳎����A
                    // "dmg - tmpHP(LayerHP)" �Ȃǂ̍Čv�Z
                    float cValue = dmg - tmpHP;
                    if (cValue < 0) cValue = 0;
                    return cValue;
            }
        }
        else
        {
            // �o���A�őς����i�j�󂳂�Ȃ������j
            LayerHP = leftover;
            return 0f; // �]��_���[�W�Ȃ�
        }

        // fallback
        return 0f;
    }

}
public enum BarrierResistanceMode
{
    /// <summary>�d�g��A: ��x�y���������͕��������Ȃ��i���̂܂܂̒ʉ߁j</summary>
    A_SimpleNoReturn,

    /// <summary>�d�g��B: �o���A�j�󎞂ɑϐ���"���ϐ���"�œP�񂵂āA�c�_���[�W�𕜊�</summary>
    B_RestoreWhenBreak,

    /// <summary>�d�g��C: �o���A�j�󎞂�"�ϐ����̂Ȃ�����"�Ƃ��Čv�Z(��: (���U��-HP) �Ȃ�)</summary>
    C_IgnoreWhenBreak,
}

