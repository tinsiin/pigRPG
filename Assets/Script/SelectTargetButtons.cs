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
    int NeedSelectCountAlly;//このneedcountは基本的には対象選択のみ
    int NeedSelectCountEnemy;
    List<Button> AllybuttonList = new List<Button>();
    List<Button> EnemybuttonList = new List<Button>();

    List<BaseStates> CashUnders;//分散値に対するランダム性を担保するための対象者キャッシュ
    DirectedWill selectedTargetWill = DirectedWill.One;
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

        //もしスキルの範囲性質にcanSelectRangeがない場合 (=範囲選択の必要がないスキルなので範囲選択が発生せず代入されないのでここで入れる)
        //範囲選択されたこと前提でこの後分岐するので。
        if (BattleOrchestratorHub.Current == null && !skill.HasZoneTrait(SkillZoneTrait.CanSelectRange))
        {
            // 旧UI経路: 正規化を適用してRangeWillに追加（競合解消）
            var normalizedTrait = SkillZoneTraitNormalizer.NormalizeForInitial(skill.ZoneTrait);
            acter.RangeWill = acter.RangeWill.Add(normalizedTrait);
        }

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
            //前のめりが存在するかしないか、そもそも一人かどうかだと強制的にBackOrAnyにDirectedWillになって対象選択画面を飛ばす。
            //つまりボタンを作らずそのままNextWaitへ

            var enemyLives = RemoveDeathCharacters(battleContext.EnemyGroup.Ours);//生きてる敵だけ
            if(battleContext.EnemyGroup.InstantVanguard == null || enemyLives.Count < 2) //前のめりがいないか　敵の生きてる人数が二人未満
            {
                if (!AllyTargeting && !MySelfTargeting)//味方選択がないなら
                {
                    ReturnNextWaitView();//そのまま次の画面へ
                    //bmに処理を任せる
                }
                else//味方も選択できるなら
                {
                    //敵一人を選択可能なボタンとして配置する
                    var button = Instantiate(buttonPrefab, transform);
                    var rect = button.GetComponent<RectTransform>();

                    // 親オブジェクトの右端を超える場合は次の行に移動
                    if (currentX + buttonSize.x / 2 > parentSize.x / 2)
                    {
                        // 左端にリセット
                        currentX = startX;

                        // 次の行に移動
                        currentY -= buttonSize.y + verticalPadding;
                    }

                    // ボタンの位置を設定
                    rect.anchoredPosition = new Vector2(currentX, currentY);

                    // 次のボタンのX位置を更新
                    currentX += buttonSize.x + horizontalPadding;

                    //テキスト
                    var txt = "敵";

                    //対象者ボーナスの発動範囲なのでテキストに記す
                    if (acter.HasRangeWill(SkillZoneTrait.CanSelectSingleTarget) && enemyLives.Count == 1)
                    {
                        var data = acter.TargetBonusDatas;
                        var singleEne = enemyLives[0];
                        if(data.DoIHaveTargetBonus(singleEne))//対象者ボーナスに該当の敵キャラが含まれてるのなら
                        {
                            //その対象者のボーナス倍率を取得し、ボタンテキストに追加
                            var percentage = data.GetAtPowerBonusPercentage(data.GetTargetIndex(singleEne));//対象者ボーナス
                            txt += "\n " + percentage + "倍";
                        }
                        
                    }

                    button.onClick.AddListener(() => OnClickSelectVanguardOrBacklines(button, DirectedWill.BacklineOrAny));
                    button.GetComponentInChildren<TextMeshProUGUI>().text = txt;//ボタンのテキスト
                    EnemybuttonList.Add(button);//敵のボタンリストに入れる
                }
            }
            else//前のめりがいて二人以上いるなら
            {
                //前のめりのキャラクターが対象者ボーナスに含まれているか調査
                var vanguard = battleContext.EnemyGroup.InstantVanguard;
                var data = acter.TargetBonusDatas;
                var txt = "前のめり-1";
                if(data.DoIHaveTargetBonus(vanguard))//対象者ボーナスに含まれてるのなら
                {
                    //その対象者のボーナス倍率を取得し、ボタンテキストに追加
                    var percentage = data.GetAtPowerBonusPercentage(data.GetTargetIndex(vanguard));//対象者ボーナス
                    txt += "\n " + percentage + "倍";
                }

                DirectedWill[] WillSet = new DirectedWill[] { DirectedWill.InstantVanguard, DirectedWill.BacklineOrAny };//for文で処理するため配列
                string[] BtnStringSet = new string[] { txt, "それ以外" };

                for (var i = 0; i < 2; i++)
                {
                    var button = Instantiate(buttonPrefab, transform);
                    var rect = button.GetComponent<RectTransform>();

                    // 親オブジェクトの右端を超える場合は次の行に移動
                    if (currentX + buttonSize.x / 2 > parentSize.x / 2)
                    {
                        // 左端にリセット
                        currentX = startX;

                        // 次の行に移動
                        currentY -= buttonSize.y + verticalPadding;
                    }

                    // ボタンの位置を設定
                    rect.anchoredPosition = new Vector2(currentX, currentY);

                    // 次のボタンのX位置を更新
                    currentX += buttonSize.x + horizontalPadding;

                    var will = WillSet[i];
                    var str = BtnStringSet[i];
                    vanguard.UI.SetActiveSetNumber_NumberEffect(true, 1);
                    button.onClick.AddListener(() => OnClickSelectVanguardOrBacklines(button, will));
                    button.GetComponentInChildren<TextMeshProUGUI>().text = str;//ボタンのテキストに前のめり等の記述
                    EnemybuttonList.Add(button);//敵のボタンリストに入れる

                }


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
                ReturnNextWaitView();//そのまま次の画面へ
                                     //bmに処理を任せる
            }
            else
            {
                for (var i = 0; i < selects.Count; i++)
                {
                    var button = Instantiate(buttonPrefab, transform);
                    var rect = button.GetComponent<RectTransform>();

                    // 親オブジェクトの右端を超える場合は次の行に移動
                    if (currentX + buttonSize.x / 2 > parentSize.x / 2)
                    {
                        // 左端にリセット
                        currentX = startX;

                        // 次の行に移動
                        currentY -= buttonSize.y + verticalPadding;
                    }

                    // ボタンの位置を設定
                    rect.anchoredPosition = new Vector2(currentX, currentY);

                    // 次のボタンのX位置を更新
                    currentX += buttonSize.x + horizontalPadding;

                    var ene = selects[i];
                    var txt = $"「{i+1}」";//テキストにキャラ名
                    var data = acter.TargetBonusDatas;
                    if(data.DoIHaveTargetBonus(ene))//対象者ボーナスに含まれてるのなら
                    {
                        //その対象者のボーナス倍率を取得し、ボタンテキストに追加
                        var percentage = data.GetAtPowerBonusPercentage(data.GetTargetIndex(ene));//対象者ボーナス
                        txt += "\n " + percentage + "倍";
                    }

                    //隙だらけ補正の命中パーセンテージ補正0より大きいのなら
                    var allyActer = acter as AllyClass;
                    var ExposureModifier = allyActer.GetExposureAccuracyPercentageBonus(ene.PassivesTargetProbability());
                    if(ExposureModifier > 0)
                    {
                        //その補正をボタンテキストに追加 1.〇倍の形で表示
                        txt += "\n 隙だらけ命中補正 " + ExposureModifier + "倍";
                    }
                    
                    ene.UI.SetActiveSetNumber_NumberEffect(true, i+1);
                    var chara = selects[i];//ここでこのままこれを渡すと、ボタンをクリックする時には、iの値が変わってしまう
                    button.onClick.AddListener(() => OnClickSelectTarget(chara, button, allyOrEnemy.Enemyiy, DirectedWill.One));//関数を登録
                    button.GetComponentInChildren<TextMeshProUGUI>().text = txt;//ボタンのテキスト
                    EnemybuttonList.Add(button);//敵のボタンリストを入れる

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
                var button = Instantiate(buttonPrefab, transform);
                var rect = button.GetComponent<RectTransform>();

                // 親オブジェクトの右端を超える場合は次の行に移動
                if (currentX + buttonSize.x / 2 > parentSize.x / 2)
                {
                    // 左端にリセット
                    currentX = startX;

                    // 次の行に移動
                    currentY -= buttonSize.y + verticalPadding;
                }

                // ボタンの位置を設定
                rect.anchoredPosition = new Vector2(currentX, currentY);

                // 次のボタンのX位置を更新
                currentX += buttonSize.x + horizontalPadding;
                var chara = selects[i];
                chara.UI.SetActiveSetNumber_NumberEffect(true, i+11);
                button.onClick.AddListener(() => OnClickSelectTarget(chara, button, allyOrEnemy.alliy, DirectedWill.One));//関数を登録
                button.GetComponentInChildren<TextMeshProUGUI>().text = chara.CharacterName + $"「{i+11}」";//ボタンのテキストにキャラ名
                EnemybuttonList.Add(button);//敵のボタンリストを入れる

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
    void OnClickSelectTarget(BaseStates target, Button thisBtn, allyOrEnemy faction,DirectedWill will)
    {
        CashUnders.Add(target);
        selectedTargetWill = will;

        if (AllybuttonList.Count > 0 && faction == allyOrEnemy.Enemyiy)///敵のボタンで主人公達のボタンが一つ以上あったら
        {
            foreach (var button in AllybuttonList)
            {
                Destroy(button);//主人公達のボタンを全部消す
            }
        }

        if (EnemybuttonList.Count > 0 && faction == allyOrEnemy.alliy)///主人公達のボタンで敵のボタンが一つ以上あったら
        {
            foreach (var button in EnemybuttonList)
            {
                Destroy(button);//敵のボタンを全部消す
            }
        }

        //人物セレクトカウントをデクリメント
        if (faction == allyOrEnemy.alliy)
            NeedSelectCountAlly--;
        if (faction == allyOrEnemy.Enemyiy)
            NeedSelectCountEnemy--;

        //各陣営ごとに属したボタンが二つ以上なくても終わり (一つだけあってもそれは廃棄予定の"このオブジェクト"だから)
        //つまりもう選ぶボタンがないなら
        if (faction == allyOrEnemy.alliy)
        {//味方ボタンなら
            if (AllybuttonList.Count > 1 || NeedSelectCountAlly <= 0)//味方ボタンが二つ以上ないか、味方選択必要カウントダウンがゼロ以下なら次行く処理
            {
                ReturnNextWaitView();
            }
            else
            {
                //まだ選べるのなら、途中で選択を止められるボタンを表示する。
                SelectEndBtn.gameObject.SetActive(true);
            }
        }
        else if (faction == allyOrEnemy.Enemyiy)
        {//敵ボタンなら
            if (EnemybuttonList.Count > 1 || NeedSelectCountEnemy <= 0) //敵ボタンが二つ以上ないなら、敵選択必要カウントダウンがゼロ以下なら次行く処理
            {
                ReturnNextWaitView();
            }
            else
            {
                //まだ選べるのなら、途中で選択を止められるボタンを表示する。
                SelectEndBtn.gameObject.SetActive(true);
            }
            Destroy(thisBtn);//このボタンは破棄
        }
    }
    /// <summary>
    /// 各対象者ボタンに渡すNextWaitにtabStateを戻す処理
    /// </summary>
    private void ReturnNextWaitView()
    {
        var orchestrator = BattleOrchestratorHub.Current;
        if (orchestrator != null)
        {
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
        }
        else
        {
            if (uiBridge != null)
            {
                uiBridge.SetUserUiState(TabState.NextWait);
            }
            else
            {
                Debug.LogError("SelectTargetButtons.ReturnNextWaitView: BattleUIBridge が null です");
            }

            if (battle != null)
            {
                battle.Acter.Target = selectedTargetWill;

                //bmの対象者リストにキャッシュリストを入れる
                CashUnders.Shuffle();//分散値のランダム性のためシャッフル
                var allyActer = battle.Acter as AllyClass;
                foreach(var cash in CashUnders)
                {
                    if (allyActer != null && !battle.IsFriend(battle.Acter, cash))
                    {
                        var exposureModifier = allyActer.GetExposureAccuracyPercentageBonus(cash.PassivesTargetProbability());
                        if (exposureModifier > 0)
                        {
                            allyActer.SetCharaConditionalModifierList(cash, "隙だらけ", whatModify.eye, exposureModifier);
                        }
                    }
                    battle.unders.CharaAdd(cash);
                }
            }
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
