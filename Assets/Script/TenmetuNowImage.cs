using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// �}�b�v�摜��ɓ�_���`���āA���̏�Ɍ��ݐi�s�x�ƃG���A�̏I���̊��������̓�_��Ƀx�N�g���|�W�V�����Ƃ��ĕ\���A
/// ���ݒn��\������B(���̃N���X���͈̂ʒu�ɓ_�ł����ĕ\�����邾���B)
/// </summary>
public class TenmetuNowImage : MonoBehaviour
{
    [SerializeField]RectTransform nowimg;


    /// <summary>
    /// �ʒu�����X�V���閽��
    /// </summary>
    public void LocationSet(Vector2 loc)
    {
        nowimg.anchoredPosition=loc;
    }

    // Update is called once per frame
    void Update()
    {
        //�_�ł�������@���̊J���̎��ɂ��̐F�Ɍ��点�悤����???
    }
}
