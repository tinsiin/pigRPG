using RandomExtensions;
using RandomExtensions.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static CommonCalc;

public class SelectTargetButtons : MonoBehaviour
{
    public static SelectTargetButtons Instance { get; private set; }

    [SerializeField]
    Button buttonPrefab;
    [SerializeField]
    Button SelectEndBtn;
    [SerializeField]
    RectTransform parentRect;

    [Header("Layout Settings")]
    [SerializeField] float horizontalPadding = 10f; // ボタン間の横余白
    [SerializeField] float verticalPadding = 10f;   // ボタン間の縦余白

    public void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        buttonSize = buttonPrefab.GetComponent<RectTransform>().sizeDelta;
        parentSize = parentRect.rect.size;

        // 親オブジェクトの左上を基準とするためのオフセット
        startX = -parentSize.x / 2 + buttonSize.x / 2 + horizontalPadding;
        startY = parentSize.y / 2 - buttonSize.y + horizontalPadding;
    }
    // ボタンのサイズを取得
    Vector2 buttonSize;
    // 親オブジェクトのサイズを取得
    Vector2 parentSize;
    // 親オブジェクトの左上を基準とするためのオフセット
    float startX;
    float startY;

    BattleUIBridge uiBridge => BattleUIBridge.Active;
    IBattleContext battle => uiBridge?.BattleContext;
    private BattleOrchestrator _orchestrator;
    private BattleOrchestrator Orchestrator => _orchestrator ?? BattleOrchestratorHub.Current;
    public void Initialize(BattleOrchestrator orchestrator) => _orchestrator = orchestrator;
    int NeedSelectCountAlly;//このneedcountは基本的には対象選択のみ
    int NeedSelectCountEnemy;
    List<Button> AllybuttonList = new List<Button>();
    List<Button> EnemybuttonList = new List<Button>();

    List<BaseStates> CashUnders;//分散値に対するランダム性を担保するための対象者キャッシュ
    DirectedWill selectedTargetWill = DirectedWill.One;

    /// <summary>
    /// 全ボタンを削除してリストをクリア
    /// </summary>
    private void ClearAllTargetButtons()
    {
        foreach (var button in AllybuttonList)
            if (button != null) Destroy(button.gameObject);
        foreach (var button in EnemybuttonList)
            if (button != null) Destroy(button.gameObject);
        AllybuttonList.Clear();
        EnemybuttonList.Clear();
    }

    /// <summary>
    /// 対象選択ボタンを生成するヘルパー（個別キャラ選択用）
    /// </summary>
    private Button CreateTargetButton(
        BaseStates target,
        string text,
        DirectedWill will,
        Faction faction,
        ref float currentX,
        ref float currentY,
        List<Button> targetButtonList)
    {
        var button = Instantiate(buttonPrefab, transform);
        var rect = button.GetComponent<RectTransform>();

        // 親オブジェクトの右端を超える場合は次の行に移動
        if (currentX + buttonSize.x / 2 > parentSize.x / 2)
        {
            currentX = startX;
            currentY -= buttonSize.y + verticalPadding;
        }

        rect.anchoredPosition = new Vector2(currentX, currentY);
        currentX += buttonSize.x + horizontalPadding;

        button.onClick.AddListener(() => OnClickSelectTarget(target, button, faction, will));
        button.GetComponentInChildren<TextMeshProUGUI>().text = text;
        targetButtonList.Add(button);
        return button;
    }

    /// <summary>
    /// 前のめり/後衛選択ボタンを生成するヘルパー
    /// </summary>
    private Button CreateVanguardButton(
        string text,
        DirectedWill will,
        ref float currentX,
        ref float currentY)
    {
        var button = Instantiate(buttonPrefab, transform);
        var rect = button.GetComponent<RectTransform>();

        if (currentX + buttonSize.x / 2 > parentSize.x / 2)
        {
            currentX = startX;
            currentY -= buttonSize.y + verticalPadding;
        }

        rect.anchoredPosition = new Vector2(currentX, currentY);
        currentX += buttonSize.x + horizontalPadding;

        button.onClick.AddListener(() => OnClickSelectVanguardOrBacklines(button, will));
        button.GetComponentInChildren<TextMeshProUGUI>().text = text;
        EnemybuttonList.Add(button);
        return button;
    }

    private void OnDestroy()
    {
        ClearAllTargetButtons();
    }

    /// <summary>
    /// 生成用コールバック
    /// </summary>
    public void OnCreated()
    {
        var battleContext = battle;
        if (battleContext == null)
        {
            Debug.LogError("SelectTargetButtons.OnCreated: BattleContext が null です");
            return;
        }
        var acter = battleContext.Acter;
        var skill = acter.NowUseSkill;
        CashUnders = new List<BaseStates>();
        selectedTargetWill = acter.Target;
        // 前回の参照が残ると誤判定や二重Destroyの原因になるため、生成前に必ずクリア
        AllybuttonList.Clear();
        EnemybuttonList.Clear();

        // RangeWillの初期化はBattleOrchestrator.ApplySkillSelect()で行われる
        // CanSelectRangeがないスキルは、そこで正規化済みの値が設定される

        // 現在の位置を初期化
        float currentX = startX;
        float currentY = startY;


        //buttonPrefabをスキル性質に応じてbmのグループを特定の人数分だけ作って生成する
        NeedSelectCountAlly = 0;
        NeedSelectCountEnemy = 0;
        bool EnemyTargeting = false;//敵の対象選択
        bool AllyTargeting = false;//味方の対象選択
        bool MySelfTargeting = acter.HasRangeWill(SkillZoneTrait.CanSelectMyself);//自分自身の対象選択
        bool EnemyVanguardOrBackLine = false;//敵の前のめりor後衛

        if(skill.HasZoneTrait(SkillZoneTrait.SelectOnlyAlly))//味方のみを対象を前提とした性質ならば、
        {
            if (acter.HasRangeWill(SkillZoneTrait.CanPerfectSelectSingleTarget))//選択可能な単体対象
            {
                AllyTargeting = true;
                NeedSelectCountAlly = 1;
            }

        }
        else//敵を主軸とした標準的な範囲性質なら
        {
            if (acter.HasRangeWill(SkillZoneTrait.CanPerfectSelectSingleTarget))//選択可能な単体対象
            {
                EnemyTargeting = true;
                NeedSelectCountEnemy = 1;

                if (acter.HasRangeWill(SkillZoneTrait.CanSelectAlly))
                {
                    AllyTargeting = true;//味方も選べたら味方も追加
                    NeedSelectCountAlly = 1;
                }else if (MySelfTargeting)
                {
                    NeedSelectCountAlly = 1;
                }

            }
            if (acter.HasRangeWill(SkillZoneTrait.CanSelectSingleTarget))//前のめりか後衛(ランダム単体)を狙うか
            {
                EnemyVanguardOrBackLine = true;
                if (acter.HasRangeWill(SkillZoneTrait.CanSelectAlly))//味方も選べるなら
                {
                    AllyTargeting = true;//味方は単体でしか選べない
                    //一人または二人単位 
                    NeedSelectCountAlly = Random.Range(1, 3);
                }else if (MySelfTargeting)
                {
                    NeedSelectCountAlly = 1;
                }
            }

            if (acter.HasRangeWill(SkillZoneTrait.CanSelectMultiTarget))//前のめりか後衛(範囲)を狙うか
            {
                EnemyVanguardOrBackLine = true;
                if (acter.HasRangeWill(SkillZoneTrait.CanSelectAlly))//味方も選べるなら
                {
                    AllyTargeting = true;//味方は単体でしか選べない
                    //二人範囲 
                    NeedSelectCountAlly = 2;
                }else if (MySelfTargeting)
                {
                    NeedSelectCountAlly = 1;
                }
            }

        }

        //ボタン作成フェーズ☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆


        if (EnemyVanguardOrBackLine) //前のめりか後衛かを敵から選ぶには
        {
            //前のめりが存在するかしないか、そもそも一人かどうかなら　味方がいるなら選択ボタンとして強制的にBackOrAnyにDirectedWillになる
            //選べるオプションとしての味方がいないのならボタンを作らずそのままNextWaitへ　対象選択画面を飛ばす。

            var enemyLives = RemoveDeathCharacters(battleContext.EnemyGroup.Ours);//生きてる敵だけ
            if(battleContext.EnemyGroup.InstantVanguard == null || enemyLives.Count < 2) //前のめりがいないか　敵の生きてる人数が二人未満
            {
                if (!AllyTargeting && !MySelfTargeting)//味方選択がないなら
                {
                    ReturnNextWaitView();//そのまま次の画面へ
                    //bmに処理を任せる  selecttargetsで自動で一人の敵として選ばれるからdirectedwillを入れる必要なし
                    //oneは必ず分岐に使われるが　backorany,instantvanguardは明示的に必要な場所でしか使われないから
                    // この場合の自動選択に影響なし
                }
                else//味方も選択できるなら
                {
                    //敵一人を選択可能なボタンとして配置する
                    var txt = "敵";

                    //対象者ボーナスの発動範囲なのでテキストに記す
                    if (acter.HasRangeWill(SkillZoneTrait.CanSelectSingleTarget) && enemyLives.Count == 1)
                    {
                        var data = acter.TargetBonusDatas;
                        var singleEne = enemyLives[0];
                        if(data.DoIHaveTargetBonus(singleEne))
                        {
                            var percentage = data.GetAtPowerBonusPercentage(data.GetTargetIndex(singleEne));
                            txt += "\n " + percentage + "倍";
                        }
                    }

                    CreateVanguardButton(txt, DirectedWill.BacklineOrAny, ref currentX, ref currentY);
                }
            }
            else//前のめりがいて二人以上いるなら
            {
                //前のめりのキャラクターが対象者ボーナスに含まれているか調査
                var vanguard = battleContext.EnemyGroup.InstantVanguard;
                var data = acter.TargetBonusDatas;
                var vanguardTxt = "前のめり-1";
                if(data.DoIHaveTargetBonus(vanguard))
                {
                    var percentage = data.GetAtPowerBonusPercentage(data.GetTargetIndex(vanguard));
                    vanguardTxt += "\n " + percentage + "倍";
                }

                vanguard.UI.SetActiveSetNumber_NumberEffect(true, 1);
                CreateVanguardButton(vanguardTxt, DirectedWill.InstantVanguard, ref currentX, ref currentY);
                CreateVanguardButton("それ以外", DirectedWill.BacklineOrAny, ref currentX, ref currentY);
            }
        }



        if (EnemyTargeting)//敵全員を入れる
        {
            var selects = battleContext.EnemyGroup.Ours;

            if (!acter.HasRangeWill(SkillZoneTrait.CanSelectDeath))//死亡者選択不可能なら
            {
                selects = RemoveDeathCharacters(selects);//省く
            }

            if (selects.Count < 2 && !AllyTargeting && !MySelfTargeting)//敵の生きてる人数が二人未満で、味方の選択もなければ
            {
                // 対象が1人でもUI選択と同様の結果になるよう、リストに追加してから終了
                // これにより CanPerfectSelectSingleTarget + RandomRange の特権（選択済み対象が残る）が維持される
                if (selects.Count == 1)
                {
                    CashUnders.Add(selects[0]);
                    selectedTargetWill = DirectedWill.One;
                }
                ReturnNextWaitView();//そのまま次の画面へ
            }
            else
            {
                var allyActer = acter as AllyClass;
                for (var i = 0; i < selects.Count; i++)
                {
                    var ene = selects[i];
                    var txt = $"「{i+1}」";
                    var data = acter.TargetBonusDatas;
                    if(data.DoIHaveTargetBonus(ene))
                    {
                        var percentage = data.GetAtPowerBonusPercentage(data.GetTargetIndex(ene));
                        txt += "\n " + percentage + "倍";
                    }

                    var ExposureModifier = allyActer.GetExposureAccuracyPercentageBonus(ene.PassivesTargetProbability());
                    if(ExposureModifier > 0)
                    {
                        txt += "\n 隙だらけ命中補正 " + ExposureModifier + "倍";
                    }

                    ene.UI.SetActiveSetNumber_NumberEffect(true, i+1);
                    CreateTargetButton(ene, txt, DirectedWill.One, Faction.Enemy, ref currentX, ref currentY, EnemybuttonList);
                }
            }
        }

        if (AllyTargeting || MySelfTargeting)//味方全員を入れる
        {
            List<BaseStates> selects;
            if(AllyTargeting)//味方選択可能なら
            {
                selects = battleContext.AllyGroup.Ours;
                if(!MySelfTargeting)//自分自身が選ばれないのなら　自分自身を省く
                {
                    selects.Remove(acter);
                }
            }
            else//味方選択不可能だが自分自身は選択可能なら
            {
                selects = new List<BaseStates>{acter};//自分自身だけ
            }


            if(!acter.HasRangeWill(SkillZoneTrait.CanSelectDeath))//死亡者選択不可能なら
            {
                selects = RemoveDeathCharacters(selects);//省く
            }
            


            for (var i = 0; i < selects.Count; i++)
            {
                var chara = selects[i];
                chara.UI.SetActiveSetNumber_NumberEffect(true, i+11);
                var txt = chara.CharacterName + $"「{i+11}」";
                // 味方ボタンはAllybuttonListに追加（OnClickSelectTargetの終了判定と整合性を保つ）
                CreateTargetButton(chara, txt, DirectedWill.One, Faction.Ally, ref currentX, ref currentY, AllybuttonList);
            }

        }

        //選択を途中で終えるボタン
        SelectEndBtn.gameObject.SetActive(false);//見えなくする。
    }
    /// <summary>
    /// ボタンの並び方テスト用ボタン
    /// </summary>
    public void OnClickTestButton()
    {
        // 現在の位置を初期化
        float currentX = startX;
        float currentY = startY;

        const int count = 10;

        for (var i = 0; i < count; i++)
        {
            var button = Instantiate(buttonPrefab, transform);
            var rect = button.GetComponent<RectTransform>();

            // 親オブジェクトの右端を超える場合は次の行に移動
            if (currentX + buttonSize.x / 2 > parentSize.x / 2)
            {
                // 左端にリセット
                currentX = startX;

                // 次の行に移動
                currentY -= (buttonSize.y + verticalPadding);
            }

            // ボタンの位置を設定
            rect.anchoredPosition = new Vector2(currentX, currentY);

            // 次のボタンのX位置を更新
            currentX += (buttonSize.x + horizontalPadding);

        }


    }
    /// <summary>
    /// 途中で複数選択を止めるボタン
    /// </summary>
    public void OnClickSelectEndBtn()
    {
        ReturnNextWaitView();
    }
    /// <summary>
    /// 前のめりか後衛かを選択するボタン。
    /// </summary>
    void OnClickSelectVanguardOrBacklines(Button thisBtn,DirectedWill will)
    {
        selectedTargetWill = will;
        ReturnNextWaitView();
    }

    /// <summary>
    /// "人物を対象として選ぶクリック関数"
    /// </summary>
    void OnClickSelectTarget(BaseStates target, Button thisBtn, Faction faction,DirectedWill will)
    {
        CashUnders.Add(target);
        selectedTargetWill = will;

        if (AllybuttonList.Count > 0 && faction == Faction.Enemy)///敵のボタンで主人公達のボタンが一つ以上あったら
        {
            foreach (var button in AllybuttonList)
            {
                Destroy(button);//主人公達のボタンを全部消す
            }
        }

        if (EnemybuttonList.Count > 0 && faction == Faction.Ally)///主人公達のボタンで敵のボタンが一つ以上あったら
        {
            foreach (var button in EnemybuttonList)
            {
                Destroy(button);//敵のボタンを全部消す
            }
        }

        //人物セレクトカウントをデクリメント
        if (faction == Faction.Ally)
            NeedSelectCountAlly--;
        if (faction == Faction.Enemy)
            NeedSelectCountEnemy--;

        // ボタンをリストから削除してDestroy（リストのCountを正確に保つ、同一キャラの重複選択を防ぐ）
        if (faction == Faction.Ally)
        {
            AllybuttonList.Remove(thisBtn);
        }
        else if (faction == Faction.Enemy)
        {
            EnemybuttonList.Remove(thisBtn);
        }
        Destroy(thisBtn);

        // 終了判定：
        // - ボタンが二つ以上残っていないなら選択の余地がないので終了
        // - 必要選択数に達した（NeedSelectCount <= 0）なら終了
        // ※対象が最初から1人しかいない場合は、OnCreated()でボタンを作らずReturnNextWaitView()している
        if (faction == Faction.Ally)
        {//味方ボタンなら
            if (AllybuttonList.Count < 1 || NeedSelectCountAlly <= 0)
            {
                ReturnNextWaitView();
            }
            else
            {
                //まだ選べるのなら、途中で選択を止められるボタンを表示する。
                SelectEndBtn.gameObject.SetActive(true);
            }
        }
        else if (faction == Faction.Enemy)
        {//敵ボタンなら
            if (EnemybuttonList.Count < 1 || NeedSelectCountEnemy <= 0)
            {
                ReturnNextWaitView();
            }
            else
            {
                //まだ選べるのなら、途中で選択を止められるボタンを表示する。
                SelectEndBtn.gameObject.SetActive(true);
            }
        }
    }
    /// <summary>
    /// 各対象者ボタンに渡すNextWaitにtabStateを戻す処理
    /// </summary>
    private void ReturnNextWaitView()
    {
        var orchestrator = Orchestrator;
        if (orchestrator == null)
        {
            Debug.LogError("[CRITICAL] SelectTargetButtons.ReturnNextWaitView: BattleOrchestrator is not initialized");
            return;
        }

        var input = new ActionInput
        {
            Kind = ActionInputKind.TargetSelect,
            RequestId = orchestrator.CurrentChoiceRequest.RequestId,
            Actor = battle?.Acter,
            TargetWill = selectedTargetWill,
            Targets = new List<BaseStates>(CashUnders)
        };
        var state = orchestrator.ApplyInput(input);
        if (uiBridge != null)
        {
            uiBridge.SetUserUiState(state, false);
        }
        else
        {
            Debug.LogError("SelectTargetButtons.ReturnNextWaitView: BattleUIBridge が null です");
        }

        foreach (var button in AllybuttonList)
        {
            Destroy(button);//ボタン全部削除
        }
        foreach (var button in EnemybuttonList)
        {
            Destroy(button);//ボタン全部削除
        }

        if (battle != null)
        {
            foreach(var one in battle.AllCharacters)
            {
                one.UI.SetActiveSetNumber_NumberEffect(false);//全キャラの数字を非表示
            }
        }
    }
}
