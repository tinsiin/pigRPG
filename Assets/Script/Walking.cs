using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using RandomExtensions;

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
    
        /// <summary>
    /// 与えられた敵のリストを基に今回の敵を決める、
    /// 汎用的な相性で敵を集めてリストで返す静的関数
    /// </summary>
    public static BattleGroup EnemyCollectAI(List<NormalEnemy> targetList)
    {
        List<NormalEnemy> ResultList = new List<NormalEnemy>();//返す用のリスト
        PartyProperty ourImpression = PartyProperty.TrashGroup;//初期値は馬鹿共
        
        //最初の一人はランダムで選ぶ
        var rndIndex = Random.Range(0, targetList.Count - 1);//ランダムインデックス指定
        var referenceOne= targetList[rndIndex];//抽出
        targetList.RemoveAt(rndIndex);//削除
        ResultList.Add(referenceOne);//追加

        //数判定(一人判定)　
        if(EnemyCollectManager.Instance.LonelyMatchUp(referenceOne.MyImpression)){

            //パーティー属性を決める　一人なのでその一人の属性をそのままパーティー属性にする
            ourImpression = EnemyCollectManager.Instance.EnemyLonelyPartyImpression[referenceOne.MyImpression];//()ではなく[]でアクセスすることに注意

            return new BattleGroup(ResultList.Cast<BaseStates>().ToList(),ourImpression) ;//while文に入らずに返す  
        }

        while (true)
        {
            //まず吟味する加入対象をランダムに選ぶ
            var targetIndex = Random.Range(0, targetList.Count - 1);//ランダムでインデックス指定
            int okCount = 0;//適合数 これがResultList.Countと同じになったら加入させる

            for (int i = 0; i < ResultList.Count; i++)//既に選ばれた敵全員との相性を見る
            {//for文で判断しないと現在の配列のインデックスを相性値用の配列のインデックス指定に使えない
                //種別同士の判定 if文内で変数に代入できる
                if ((EnemyCollectManager.Instance.TypeMatchUp(ResultList[i].MyType, targetList[targetIndex].MyType)) )
                {
                    //属性同士の判定　上クリアしたら
                    if ((EnemyCollectManager.Instance.ImpressionMatchUp(ResultList[i].MyImpression, targetList[targetIndex].MyImpression)) )
                    {
                        okCount++;//適合数を増やす
                    }
                }
            }
            //foreachで全員との相性を見たら、加入させる。
            if (okCount == ResultList.Count)//全員との相性が合致したら
            {
                ResultList.Add(targetList[targetIndex]);//結果のリストに追加
                targetList.RemoveAt(targetIndex);//候補リストから削除
            }

            //数判定
            if (ResultList.Count == 1)//一人だったら(まだ一人も見つけれてない場合)
            {
                if (RandomEx.Shared.NextInt(100) < 88)//88%の確率で一人で終わる計算に入る。
                {
                    //数判定(一人判定)　
                    if(EnemyCollectManager.Instance.LonelyMatchUp(referenceOne.MyImpression)){

                        //パーティー属性を決める　一人なのでその一人の属性をそのままパーティー属性にする
                        ourImpression = EnemyCollectManager.Instance.EnemyLonelyPartyImpression[referenceOne.MyImpression];//()ではなく[]でアクセスすることに注意

                        break;
                    }
                }
            }

            if (ResultList.Count == 2)//二人だったら三人目の加入を決める
            {
                if (RandomEx.Shared.NextInt(100) < 65)//この確率で終わる。
                {
                    //パーティー属性を決める
                    ourImpression = EnemyCollectManager.Instance.calculatePartyProperty(ResultList);
                    break;
                }
            }
            
            if(ResultList.Count>=3)
            {
                //パーティー属性を決める
                ourImpression = EnemyCollectManager.Instance.calculatePartyProperty(ResultList);
                break;//三人になったら強制終了
            } 
        }

        

        return new BattleGroup(ResultList.Cast<BaseStates>().ToList(), ourImpression);//バトルグループを制作 
        }    

}
