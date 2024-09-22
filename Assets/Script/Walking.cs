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
    /// 選択肢ボタンを入れる親オブジェクト取得
    /// </summary>
    [SerializeField] RectTransform SelectButtonArea;

    //現在のステージとエリアのデータを保存する関数
    StageData NowStageData;
    StageCut NowStageCut;
    AreaDate NowAreaData;
    /// <summary>
    /// エリア選択ボタンが押されると返ってくる。-1はpush待ち
    /// </summary>
    int AreaResponse;

    /// <summary>
    /// 選択肢ボタンのリスト
    /// </summary>
    List<SelectButton> buttons;
    async void Start()
    {
        ps = new PlayersStates();

        await walk(0);//最適化のため最終開発の段階で初期UIの更新だけをするようにする。
    }

    /// <summary>
    /// 歩行するボタン
    /// </summary>
    public async void OnWalkBtn()
    {
        if (stages && ps != null)
        {
             await walk(1);
        }
    }

    /// <summary>
    /// エンカウント処理
    /// </summary>
    public async void EnemyEncount()
    {
        
    }
    async UniTask　walk(int footnumber)//リストの内容を反映
    {
        
        ps.AddProgress(footnumber);//進行度を増やす。
        StageDataUpdate();
       
        if (NowAreaData.Rest)//休憩地点なら
        {
           Debug.Log("ここは休憩地点");
        }

        if (!string.IsNullOrEmpty(NowAreaData.NextID))//次のエリア選択肢
        {
            string[] arr = NowAreaData.NextIDString.Split(",");//選択肢文章を小分け
            string[] arr2 = NowAreaData.NextID.Split(",");//選択肢のIDを小分け
            
            ps.SetArea(await CreateAreaButton(arr,arr2));
            ps.ProgressReset();
        }

        if (string.IsNullOrEmpty(NowAreaData.NextStageID))//次のステージへ(選択肢なし)
        {
        }

        StageDataUpdate();
        TestProgressUIUpdate();//テスト用進行度ui更新
    }
    /// <summary>
    /// ステージデータの更新
    /// </summary>
    void StageDataUpdate()
    {
        NowStageData = stages.StageDates[ps.NowStageID];//現在のステージデータ
        NowStageCut = NowStageData.CutArea[ps.NowAreaID];//現在のエリアデータ
        NowAreaData = NowStageCut.AreaDates[ps.NowProgress];//現在地点

        wui.UIUpdate(NowStageData, NowStageCut, ps);//ui更新
    }

    /// <summary>
    /// 次のエリア選択肢のボタンを生成。
    /// </summary>
    /// <param name="selectparams"></param>
    public async UniTask<int> CreateAreaButton(string[] stringParams, string[] idParams)
    {
        walkbtn.enabled = false;
        AreaResponse = -1;//ボタン解答が進まないよう無効化
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

        //何かしらボタンが押されて返されるまで待つ　:cancellationTokenは複数あるオプションのうち一つを選ぶ構文
        await UniTask.WaitUntil(() => AreaResponse != -1, cancellationToken: this.GetCancellationTokenOnDestroy());

        AreaButtonClose();
        walkbtn.enabled = true;

        var res = AreaResponse;

        return res;
    }
    /// <summary>
    /// エリア選択肢ボタンを閉じる
    /// </summary>
    void AreaButtonClose()
    {
        foreach (var button in buttons) {
            button.Close();
        }
    }

    /// <summary>
    /// 選択肢ボタンに渡し選択肢の結果を記録するための関数
    /// </summary>
    void OnAnyClickSelectButton(int returnid)
    {
        Debug.Log(returnid + "のエリアIDを記録");
        AreaResponse = returnid;//ここで0～の数字を渡されることでボタン選択処理の非同期待ちが進行
    }


    //最終的にeyearea側で一気にeyeareaのUIを処理するのを作って、そっちにデータを渡すようにする。
    void TestProgressUIUpdate()//テスト用
    {
        tmp.text = "" + ps.NowProgress;
    }
}
