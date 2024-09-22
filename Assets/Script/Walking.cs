using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;

public class Walking : MonoBehaviour
{
    [SerializeField] PlayersStates ps;
    [SerializeField] WatchUIUpdate wui;
    [SerializeField] Stages stages;
    [SerializeField] TextMeshProUGUI tmp;
    [SerializeField] Button walkbtn;
    [SerializeField] SelectButton SelectButtonPrefab;
    [SerializeField] int SelectBtnSize;
    /// <summary>
    /// �I�����{�^��������e�I�u�W�F�N�g�擾
    /// </summary>
    [SerializeField] RectTransform SelectButtonArea;

    //���݂̃X�e�[�W�ƃG���A�̃f�[�^��ۑ�����֐�
    StageData NowStageData;
    StageCut NowStageCut;
    AreaDate NowAreaData;
    /// <summary>
    /// �G���A�I���{�^�����������ƕԂ��Ă���B-1��push�҂�
    /// </summary>
    int AreaResponse;

    /// <summary>
    /// �I�����{�^���̃��X�g
    /// </summary>
    List<SelectButton> buttons;
    async void Start()
    {
        ps = new PlayersStates();

        await walk(0);//�œK���̂��ߍŏI�J���̒i�K�ŏ���UI�̍X�V����������悤�ɂ���B
    }

    /// <summary>
    /// ���s����{�^��
    /// </summary>
    public async void OnWalkBtn()
    {
        if (stages && ps != null)
        {
             await walk(1);
        }
    }

    /// <summary>
    /// �G���J�E���g����
    /// </summary>
    public async void EnemyEncount()
    {
        
    }
    async UniTask�@walk(int footnumber)//���X�g�̓��e�𔽉f
    {
        
        ps.AddProgress(footnumber);//�i�s�x�𑝂₷�B
        StageDataUpdate();
       
        if (NowAreaData.Rest)//�x�e�n�_�Ȃ�
        {
           Debug.Log("�����͋x�e�n�_");
        }

        if (!string.IsNullOrEmpty(NowAreaData.NextID))//���̃G���A�I����
        {
            string[] arr = NowAreaData.NextIDString.Split(",");//�I�������͂�������
            string[] arr2 = NowAreaData.NextID.Split(",");//�I������ID��������
            
            ps.SetArea(await CreateAreaButton(arr,arr2));
            ps.ProgressReset();
        }

        if (string.IsNullOrEmpty(NowAreaData.NextStageID))//���̃X�e�[�W��(�I�����Ȃ�)
        {
        }

        StageDataUpdate();
        TestProgressUIUpdate();//�e�X�g�p�i�s�xui�X�V
    }
    /// <summary>
    /// �X�e�[�W�f�[�^�̍X�V
    /// </summary>
    void StageDataUpdate()
    {
        NowStageData = stages.StageDates[ps.NowStageID];//���݂̃X�e�[�W�f�[�^
        NowStageCut = NowStageData.CutArea[ps.NowAreaID];//���݂̃G���A�f�[�^
        NowAreaData = NowStageCut.AreaDates[ps.NowProgress];//���ݒn�_

        wui.UIUpdate(NowStageData, NowStageCut, ps);//ui�X�V
    }

    /// <summary>
    /// ���̃G���A�I�����̃{�^���𐶐��B
    /// </summary>
    /// <param name="selectparams"></param>
    public async UniTask<int> CreateAreaButton(string[] stringParams, string[] idParams)
    {
        walkbtn.enabled = false;
        AreaResponse = -1;//�{�^���𓚂��i�܂Ȃ��悤������
        int index = 0;
        //var tasks = new List<UniTask>();

        buttons = new();
        foreach (string s in stringParams)
        {
            var button = Instantiate(SelectButtonPrefab, SelectButtonArea);
            buttons.Add(button);
            //tasks.Add(button.OnCreateButton(index, s, OnAnyClickSelectButton));
            button.OnCreateButton(index, s, OnAnyClickSelectButton, int.Parse(idParams[index]), SelectBtnSize);
            index++;
        }

        //��������{�^����������ĕԂ����܂ő҂@:cancellationToken�͕�������I�v�V�����̂������I�ԍ\��
        await UniTask.WaitUntil(() => AreaResponse != -1, cancellationToken: this.GetCancellationTokenOnDestroy());

        AreaButtonClose();
        walkbtn.enabled = true;

        var res = AreaResponse;

        return res;
    }
    /// <summary>
    /// �G���A�I�����{�^�������
    /// </summary>
    void AreaButtonClose()
    {
        foreach (var button in buttons) {
            button.Close();
        }
    }

    /// <summary>
    /// �I�����{�^���ɓn���I�����̌��ʂ��L�^���邽�߂̊֐�
    /// </summary>
    void OnAnyClickSelectButton(int returnid)
    {
        Debug.Log(returnid + "�̃G���AID���L�^");
        AreaResponse = returnid;//������0�`�̐�����n����邱�ƂŃ{�^���I�������̔񓯊��҂����i�s
    }


    //�ŏI�I��eyearea���ň�C��eyearea��UI����������̂�����āA�������Ƀf�[�^��n���悤�ɂ���B
    void TestProgressUIUpdate()//�e�X�g�p
    {
        tmp.text = "" + ps.NowProgress;
    }
}
