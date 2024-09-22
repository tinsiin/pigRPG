using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;

public class WatchUIUpdate : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI StagesString;//�X�e�[�W�ƃG���A���̃e�L�X�g
    [SerializeField] TenmetuNowImage MapImg;//���ڂŌ��݈ʒu�\������ȈՃ}�b�v
    void Start()
    {
        
    }

    void Update()
    {
        
    }
    /// <summary>
    /// �S�̓I��EYEAREA��UI�X�V
    /// </summary>
    public void UIUpdate(StageData sd,StageCut sc,PlayersStates pla)
    {
        StagesString.text = sd.StageName + "�E\n" + sc.AreaName;
        NowImageCalc(sc, pla);
    }
    /// <summary>
    /// �ȈՃ}�b�v���ݒn��UI�X�V�Ƃ��̏���
    /// </summary>
    void NowImageCalc(StageCut sc,PlayersStates player)
    {
        //�i�s�x���̂̊������v�Z
        float Ratio = (float)player.NowProgress / (sc.AreaDates.Count - 1);
        //�i�s�x���G���A��(count������-1) �Е��L���X�g���Ȃ��Ɛ������m�Ƃ��ď����_�ȉ��؂�̂Ă���B
        //Debug.Log("���ݐi�s�x�̃G���A���ɑ΂��銄��"+Ratio);

        //lerp���x�N�g����ݒ肵�Ă����A�������ꂽ�ʒu��n��
        MapImg.LocationSet(Vector2.Lerp(sc.MapLineS,sc.MapLineE, Ratio));

    }
}
