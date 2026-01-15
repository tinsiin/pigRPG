using System;
using System.Collections.Generic;
using System.Linq;
using RandomExtensions;
using UnityEngine;


public class Stages : MonoBehaviour
{
    //ステージにまつわる物を処理したり呼び出したり(ステージデータベース??
    public List<StageData> StageDates; //ステージのデータベースのリスト     
    public List<StageData> RunTimeStageDates; //ステージのデータベースのリスト(ランタイム用)
    [SerializeField][TextArea(1, 30)] private string memo;

    public static Stages Instance;

    private void Awake()
    {
        if (Instance == null)

        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        // Awake 完了（全シングルトンの準備完了）後に初期化を実行
        DeepCopyToRunTime();//ランタイム用にディープコピー
        OnInitializeAllStageCharas();//全て初期化
    }
    /// <summary>
    /// 全てのステージのキャラクターの初期化をする
    /// </summary>
    private void OnInitializeAllStageCharas()
    {
        Debug.Log("ステージのキャラクターの初期化を実行");
        foreach (var stage in RunTimeStageDates)
        {
            stage.OnInitializeAllAreaCharas();
        }

    }
    /// <summary>
    /// ランタイム用にディープコピー
    /// </summary>
    void DeepCopyToRunTime()
    {
        RunTimeStageDates = new List<StageData>();
        foreach (var stage in StageDates)
        {
            RunTimeStageDates.Add(stage.DeepCopy());
        }
    }
}

    /// <summary>
    ///     ステータスボーナスのクラス ステージごとに登録する。
    /// </summary>
    [Serializable]
    public class StatesBonus
    {
        public int ATKBpunus;
        public int DEFBonus;
        public int AGIBonus;
        public int HITBonus;
        public int HPBonus;
        public int PBonus;
        public int RecovelyTurnMinusBonus;
        public StatesBonus DeepCopy()
        {
            var copy = new StatesBonus();
            copy.ATKBpunus = this.ATKBpunus;
            copy.DEFBonus = this.DEFBonus;
            copy.AGIBonus = this.AGIBonus;
            copy.HITBonus = this.HITBonus;
            copy.HPBonus = this.HPBonus;
            copy.PBonus = this.PBonus;
            copy.RecovelyTurnMinusBonus = this.RecovelyTurnMinusBonus;
            return copy;
        }
    }

    /// <summary>
    ///UIごとに対応したステージのテーマカラー
    /// </summary>
    [Serializable]
    public class StageThemeColorUI
    {
        [Header("ActionMarkの色 マゼンタとこの色を行き来する。 ")]
        /// <summary>
        /// 行動者のアイコン強調表示色
        /// </summary>
        public Color ActionMarkColor;
        [Header("フレーム描画用のテーマ色のメイン部分 これ自体がテーマ色のメイン")]
        /// <summary>
        /// 歩行時のサイドオブジェクトと、EYEAREAでの矢印を描画する色
        /// フレーム描画用のテーマ色のメイン部分
        /// </summary>
        public Color FrameArtColor;
        [Header("基本同じAlphaの離れ具合で指定するといいと思う。")]
        public Color TwoColor;
        public StageThemeColorUI DeepCopy()
        {
            var copy = new StageThemeColorUI();
            copy.ActionMarkColor = this.ActionMarkColor;
            copy.FrameArtColor = this.FrameArtColor;
            copy.TwoColor = this.TwoColor;
            return copy;
        }

    }
    [Serializable]
    public class StageData //ステージデータのクラス
    {
        [SerializeField] private string _stageName;
        public StageThemeColorUI StageThemeColorUI;
        [SerializeField] private List<StageCut> _cutArea;
        /// <summary>
        ///     ステージごとに設定される主人公陣営たちのボーナス。
        /// </summary>
        public StatesBonus Satelite_StageBonus;

        /// <summary>
        ///     ステージごとに設定される主人公陣営たちのボーナス。
        /// </summary>
        public StatesBonus Bass_StageBonus;

        /// <summary>
        ///     ステージごとに設定される主人公陣営たちのボーナス。
        /// </summary>
        public StatesBonus Stair_StageBonus;


        /// <summary>
        ///     ステージの名前
        /// </summary>
        public string StageName => _stageName; //ラムダ式で読み取り専用

        /// <summary>
        ///     ステージを小分けにしたリスト
        /// </summary>
        public IReadOnlyList<StageCut> CutArea => _cutArea;

        /// <summary>
        /// 全てのエリアのキャラの初期化コールバック
        /// </summary>
        public void OnInitializeAllAreaCharas()
        {
            foreach (var cut in _cutArea)
            {
                cut.OnInitializeAllCharas();
            }
        }
        /// <summary>
        /// このクラスの内容を別インスタンスとして返す
        /// </summary>
        public StageData DeepCopy()
        {
            var newData = new StageData();
            newData._stageName = _stageName;
            newData._cutArea = new List<StageCut>();
            foreach (var cut in _cutArea)
            {
                newData._cutArea.Add(cut.DeepCopy());
            }
            newData.Satelite_StageBonus = Satelite_StageBonus.DeepCopy();//ステージボーナスはフィールドあるのでディープコピーメゾット
            newData.Bass_StageBonus = Bass_StageBonus.DeepCopy();
            newData.Stair_StageBonus = Stair_StageBonus.DeepCopy();
            newData.StageThemeColorUI = StageThemeColorUI.DeepCopy();

            return newData;
        }

    }

    /// <summary>
    ///     分岐に対応するため、ステージを小分けにしたもの。
    /// </summary>
    [Serializable]
    public class StageCut
    {
        [SerializeField] private string _areaName;
        [SerializeField] private int _id;
        [SerializeField] private List<AreaDate> _areaDates; //並ぶエリア
        [SerializeField] private Vector2 _mapLineS;
        [SerializeField] private Vector2 _mapLineE;
        [SerializeField] private string _mapsrc;
        [SerializeReference,SelectableSerializeReference] private List<NormalEnemy> _enemyList; //敵のリスト
        //[SerializeReference,SelectableSerializeReference] private List<BaseStates> _enyList; //敵のリスト
        [SerializeField] private GameObject[] _sideObject_Lefts;//左側に出現するオブジェクト
        [SerializeField] private GameObject[] _sideObject_Rights;//右側に出現するオブジェクト


        /// <summary>
        ///     エンカウント率
        /// </summary>
        [SerializeField] private int EncounterRate;

        /// <summary>
        ///     逃走率
        /// </summary>
        public float EscapeRate;

        /// <summary>
        ///     小分けしたエリアの名前
        /// </summary>
        public string AreaName => _areaName;


        /// <summary>
        ///     マップ画像に定義する直線の始点　nowimgのanchoredPositionを直接入力
        /// </summary>
        public Vector2 MapLineS => _mapLineS;

        /// <summary>
        ///     マップ画像に定義する直線の終点　nowimgのanchoredPositionを直接入力
        /// </summary>
        public Vector2 MapLineE => _mapLineE;

        /// <summary>
        ///     小分けにしたエリアのID
        /// </summary>
        public int Id => _id;

        /// <summary>
        ///     エリアごとの簡易マップの画像。
        /// </summary>
        public string MapSrc => _mapsrc;

        /// <summary>
        ///     並べるエリアのデータ
        /// </summary>
        public IReadOnlyList<AreaDate> AreaDates => _areaDates;

        /// <summary>
        ///     敵のリスト
        /// </summary>
        public IReadOnlyList<NormalEnemy> EnemyList => _enemyList;
        public int EncounterRateValue => EncounterRate;
        public GameObject[] SideObjectLefts => _sideObject_Lefts;
        public GameObject[] SideObjectRights => _sideObject_Rights;

        public StageCut DeepCopy()
        {
            var newData = new StageCut();
            newData._areaName = _areaName;
            newData._id = _id;
            newData._areaDates = new List<AreaDate>();
            foreach (var area in _areaDates)
            {
                newData._areaDates.Add(area.DeepCopy());
            }
            newData._mapLineS = _mapLineS;
            newData._mapLineE = _mapLineE;
            newData._mapsrc = _mapsrc;
            newData._enemyList = new List<NormalEnemy>();
            foreach (var enemy in _enemyList)
            {
                newData._enemyList.Add(enemy.DeepCopy());
            }
            newData.EncounterRate = EncounterRate;
            newData.EscapeRate = EscapeRate;
            newData._sideObject_Lefts = _sideObject_Lefts;
            newData._sideObject_Rights = _sideObject_Rights;
            return newData;
        }

        /// <summary>
        /// エリアの全ての敵キャラの初期化コールバック
        /// </summary>
        public void OnInitializeAllCharas()
        {
            foreach (var chara in _enemyList)
            {
                if(chara != null)
                chara.OnInitializeSkillsAndChara();
                else
                Debug.LogWarning("enemyList 内に null 要素が存在します。");

                //chara.OnInitializeSkillsAndChara();
            }
        }

        
        private bool EncountCheck()
        {
            Debug.Log("エリアの逃走率は" + EscapeRate + "%");
        return RandomEx.Shared.NextInt(100) < EncounterRate;
        }
        public bool EscapeCheck()
        {
            Debug.Log("エリアの逃走率は" + EscapeRate + "%");
            return RandomEx.Shared.NextInt(100) < EscapeRate;
        }

        SchizoLog schizoLog = SchizoLog.Instance;
        /// <summary>
        ///     EnemyCollectManagerを使って敵を選ぶAI　キャラクター属性や種別などを考慮して選ぶ。
        ///     エンカウント失敗したら、nullを返す　「上限人数3人」
        ///     数を指定すると指定された分だけ返るが、自動モード同様に三人まで。
        /// </summary>
        /// <param name="number">敵の数を指定する。-1を指定すると指定されない</param>
        public BattleGroup EnemyCollectAI(int nowProgress, int number = -1)
        {
            var CompatibilityData = new Dictionary<(BaseStates,BaseStates),int>();//相性値のデータ保存用
            
            if (!EncountCheck()) return null; //エンカウント判定に失敗したら、nullを返す

            var ResultList = new List<NormalEnemy>(); //返す用のリスト
            PartyProperty ourImpression;
            var targetList = new List<NormalEnemy>(_enemyList); //引数のリストをコピー newを使ってディープコピー
            var manualCount = number >= 1;
            var targetCount = manualCount ? Mathf.Clamp(number, 1, 3) : -1;

            //生きてる敵や死んでても今回で復活予定の敵を有効リストに抽出
            var validEnemies = new List<NormalEnemy>(); //有効な敵のリスト
            foreach(var ene in targetList)
            {
                if(!ene.Death())
                {
                    Debug.Log($"敵キャラが生きているから追加(enemyColllecAI){ene.CharacterName}");
                    validEnemies.Add(ene);//生きてたら追加
                    continue;
                }
                if(ene.broken) continue;//死んでてなおかつ壊れてもいたら復活不可能なのでスキップ

                if(ene.Reborn)//敵が復活可能タイプなら
                if(ene.CanRebornWhatHeWill(nowProgress))//復活判定をして復活可能なら
                {
                    Debug.Log($"敵キャラが復活可能だから追加(enemyColllecAI){ene.CharacterName}");
                    validEnemies.Add(ene);//追加
                }
            }

            //有効な敵のリスト全員に再遭遇時コールバックを実施
            foreach(var ene in validEnemies)
            {
                ene.ReEncountCallback(nowProgress);
            }
            //有効リストが空だった場合
            if (!validEnemies.Any())
            {
                Debug.Log("EnemyCollectAI: エリアに有効な敵が存在しません。");
                return null; // または適切なデフォルト値を返す
            }else
            {
                Debug.Log($"EnemyCollectAI: エリアに有効な敵が存在します。{validEnemies.Count}個");
            }

            //最初の一人はランダムで選ぶ
            var rndIndex = RandomEx.Shared.NextInt(0, validEnemies.Count - 1); //ランダムインデックス指定
            var referenceOne = validEnemies[rndIndex]; //抽出

            referenceOne.InitializeMyImpression();//精神属性のランダム生成
            validEnemies.RemoveAt(rndIndex); //削除
            ResultList.Add(referenceOne); //追加

            // 手動人数指定（1人）ならここで確定して返す
            if (manualCount && targetCount == 1)
            {
                //パーティー属性を決める　一人なのでその一人の属性をそのままパーティー属性にする
                ourImpression =
                    EnemyCollectManager.Instance.EnemyLonelyPartyImpression
                        [ResultList[0].MyImpression]; //()ではなく[]でアクセスすることに注意

                return new BattleGroup(ResultList.Cast<BaseStates>().ToList(), ourImpression, allyOrEnemy.Enemyiy); //while文に入らずに返す  
            }

            //数判定(一人判定)　または　もう待機リストに誰もいなかった場合
            if(ResultList[0].MyImpression == 0)
            {
                Debug.LogError("EnemyCollectAI: 最初の一人の精神属性が0です。これはキャラの精神属性に何もセットされていないです。" 
                + ResultList[0].MyImpression);
            }
            if (!manualCount && (EnemyCollectManager.Instance.LonelyMatchUp(ResultList[0].MyImpression) || validEnemies.Count <= 0))
            {
                //パーティー属性を決める　一人なのでその一人の属性をそのままパーティー属性にする
                ourImpression =
                    EnemyCollectManager.Instance.EnemyLonelyPartyImpression
                        [ResultList[0].MyImpression]; //()ではなく[]でアクセスすることに注意

                return new BattleGroup(ResultList.Cast<BaseStates>().ToList(), ourImpression, allyOrEnemy.Enemyiy); //while文に入らずに返す  
            }

            //複数人加入するループ
            while (true)
            {
                // 指定人数モード: 目標人数に達したら確定して抜ける
                if (manualCount && ResultList.Count >= targetCount)
                {
                    ourImpression = (ResultList.Count == 1)
                        ? EnemyCollectManager.Instance.EnemyLonelyPartyImpression[ResultList[0].MyImpression]
                        : EnemyCollectManager.Instance.calculatePartyProperty(ResultList);
                    break;
                }
                // 指定人数モード: 候補が尽きた場合は現在のメンバーで確定
                if (manualCount && validEnemies.Count < 1)
                {
                    ourImpression = (ResultList.Count == 1)
                        ? EnemyCollectManager.Instance.EnemyLonelyPartyImpression[ResultList[0].MyImpression]
                        : EnemyCollectManager.Instance.calculatePartyProperty(ResultList);
                    break;
                }
                //まず吟味する加入対象をランダムに選ぶ
                var targetIndex = RandomEx.Shared.NextInt(0, validEnemies.Count - 1); //ランダムでインデックス指定
                var target = validEnemies[targetIndex];
                 //適合数 これがResultList.Countと同じになったら加入させる
                var okCount = 0;
                //同情
                var sympathy = false;

                //もし、既にいる敵や加入対象のHPが半分以下の場合、精神属性の相性判定が二倍になる。
                foreach(var ene in ResultList)
                {
                    if(ene.HP <= ene.MaxHP / 2) sympathy = true;
                }
                if(target.HP <= target.MaxHP / 2) sympathy = true;

                for (var i = 0; i < ResultList.Count; i++)
                { //既に選ばれた敵全員との相性を見る
                    //for文で判断しないと現在の配列のインデックスを相性値用の配列のインデックス指定に使えない
                    //種別同士の判定 if文内で変数に代入できる
                    if (EnemyCollectManager.Instance.TypeMatchUp(ResultList[i].MyType, target.MyType))
                    {
                        //属性同士の判定
                        if (EnemyCollectManager.Instance.ImpressionMatchUp(ResultList[i].MyImpression,target.MyImpression, sympathy))
                        {
                            okCount++; //適合数を増やす
                        }
                    }
                }
                //foreachで全員との相性を見たら、加入させる。
                if (okCount == ResultList.Count) //全員との相性が合致したら
                {
                    target.InitializeMyImpression();//精神属性のランダム生成
                    ResultList.Add(target); //結果のリストに追加
                    validEnemies.Remove(target); //候補リストから削除
                }

                //数判定
                if (!manualCount)
                    if (ResultList.Count == 1) //一人だったら(まだ一人も見つけれてない場合)
                        if (RandomEx.Shared.NextInt(100) < 88) //88%の確率で一人で終わる計算に入る。
                                                               //数判定(一人判定)　
                            if (EnemyCollectManager.Instance.LonelyMatchUp(ResultList[0].MyImpression))
                            {
                                //パーティー属性を決める　一人なのでその一人の属性をそのままパーティー属性にする
                                ourImpression =
                                    EnemyCollectManager.Instance.EnemyLonelyPartyImpression
                                        [ResultList[0].MyImpression]; //()ではなく[]でアクセスすることに注意

                                break;
                            }

                if (!manualCount)
                {
                    if (ResultList.Count == 2) //二人だったら三人目の加入を決める
                    {
                        if (RandomEx.Shared.NextInt(100) < 65) //この確率で終わる。
                        {
                            //パーティー属性を決める
                            ourImpression = EnemyCollectManager.Instance.calculatePartyProperty(ResultList);
                            break;
                        }
                    }
                }

                if (ResultList.Count >= 3)
                {
                    //パーティー属性を決める
                    ourImpression = EnemyCollectManager.Instance.calculatePartyProperty(ResultList);
                    break; //三人になったら強制終了
                }

                if (validEnemies.Count < 1)//人数チェックして待機リストに一人もいなかったら終わり
                {
                    //パーティー属性を決める
                    ourImpression = EnemyCollectManager.Instance.calculatePartyProperty(ResultList);
                    break;
                }
            }

            // 複数キャラがいる場合のみ、各キャラペアの相性値を計算して格納する
            if (ResultList.Count >= 2)
            {
                for (int i = 0; i < ResultList.Count; i++)
                {
                    for (int j = i + 1; j < ResultList.Count; j++)
                    {
                        // ここで、EnemyCollectManager などに用意されている相性計算メソッドを使う
                        var compatibilityValue = EnemyCollectManager.Instance.GetImpressionMatchPercent(ResultList[i].MyImpression, ResultList[j].MyImpression);
                        CompatibilityData[(ResultList[i], ResultList[j])] = compatibilityValue;
                        //逆にどう思われてるか　
                        compatibilityValue = EnemyCollectManager.Instance.GetImpressionMatchPercent(ResultList[j].MyImpression, ResultList[i].MyImpression);
                        CompatibilityData[(ResultList[j], ResultList[i])] = compatibilityValue;
                    }
                }
            }



            return new BattleGroup(ResultList.Cast<BaseStates>().ToList(), ourImpression, allyOrEnemy.Enemyiy,CompatibilityData); //バトルグループを制作 
        }

        /// <summary>
        /// leftとRightのオブジェクトを返す。
        /// </summary>
        /// <returns>GameObjectの入った配列</returns>
        public GameObject[] GetRandomSideObject()
        {
            if (_sideObject_Lefts.Length < 0)
            {
                Debug.LogError("_sideObject_LeftsのPrefabのリストが空です");
                return null;
            }
            if (_sideObject_Rights.Length < 0)
            {
                Debug.LogError("_sideObject_RightsのPrefabのリストが空です");
                return null;
            }

            // ランダムなオブジェクトを選択
            var leftItem = RandomEx.Shared.GetItem<GameObject>(_sideObject_Lefts);
            var rightItem = RandomEx.Shared.GetItem<GameObject>(_sideObject_Rights);

            return new GameObject[] { leftItem, rightItem };
        }

    }

    /// <summary>
    ///     ステージに並ぶエリアデータ
    /// </summary>
    [Serializable]
    public class AreaDate
    {
        [SerializeField] private bool _rest;
        [SerializeField] private string _backsrc;
        [SerializeField] private string _nextID;
        [SerializeField] private string _nextIDString;
        [SerializeField] private string _nextStageID;

        /// <summary>
        ///     次のステージのid、入力されてないならスルー
        ///     string.splitでstring[]に格納して分岐できる。
        ///     「,」で区切って入力。
        /// </summary>
        public string NextStageID => _nextStageID;

        /// <summary>
        ///     休憩地点かどうか
        /// </summary>
        public bool Rest => _rest;

        /// <summary>
        ///     背景画像のファイル名
        /// </summary>
        public string BackSrc => _backsrc;

        /// <summary>
        ///     次のエリアID、入力されてないならスルー
        ///     string.splitでstring[]に格納して分岐できる。
        ///     「,」で区切って入力。
        /// </summary>
        public string NextID => _nextID;

        /// <summary>
        ///     次のエリア選択肢のボタン文章、入力されてないならスルー
        ///     string.splitでstring[]に格納して分岐できる。
        ///     「,」で区切って入力。
        /// </summary>
        public string NextIDString => _nextIDString;

        public AreaDate DeepCopy()
        {
            var newData = new AreaDate();
            newData._rest = _rest;
            newData._backsrc = _backsrc;
            newData._nextID = _nextID;
            newData._nextIDString = _nextIDString;
            newData._nextStageID = _nextStageID;
            return newData;
        }
    }
