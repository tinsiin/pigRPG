using RandomExtensions;
using RandomExtensions.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.GraphicsBuffer;
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
            DontDestroyOnLoad(gameObject);
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

    BattleManager bm => Walking.bm;
    int NeedSelectCountAlly;//このneedcountは基本的には対象選択のみ
    int NeedSelectCountEnemy;
    List<Button> AllybuttonList;
    List<Button> EnemybuttonList;

    List<BaseStates> CashUnders;//分散値に対するランダム性を担保するための対象者キャッシュ
    /// <summary>
    /// 生成用コールバック
    /// </summary>
    public void OnCreated()
    {
        var acter = bm.Acter;
        var skill = acter.NowUseSkill;
        CashUnders = new List<BaseStates>();

        //もしスキルの範囲性質にcanSelectRangeがない場合 (=範囲選択の必要がないスキルなので範囲選択が発生せず代入されないのでここで入れる)
        //範囲選択されたこと前提でこの後分岐するので。
        if (!skill.HasZoneTrait(SkillZoneTrait.CanSelectRange))
        {
            acter.RangeWill |= skill.ZoneTrait;//実行者の範囲意志にそのままスキルの範囲性質を入れる。
        }

        // 現在の位置を初期化
        float currentX = startX;
        float currentY = startY;


        //buttonPrefabをスキル性質に応じてbmのグループを特定の人数分だけ作って生成する
        bool EnemyTargeting = false;//敵の対象選択
        bool AllyTargeting = false;//味方の対象選択
        bool MySelfTargeting = acter.HasRangeWill(SkillZoneTrait.CanSelectMyself);//自分自身の対象選択
        bool EnemyVanguardOrBackLine = false;//敵の前のめりor後衛


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

        //ボタン作成フェーズ☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆


        if (EnemyVanguardOrBackLine) //前のめりか後衛かを敵から選ぶには
        {
            //前のめりが存在するかしないか、そもそも一人かどうかだと強制的にBackOrAnyにDirectedWillになって対象選択画面を飛ばす。
            //つまりボタンを作らずそのままNextWaitへ

            var enemyLives = RemoveDeathCharacters(bm.EnemyGroup.Ours);//生きてる敵だけ
            if(bm.EnemyGroup.InstantVanguard == null || enemyLives.Count < 2) //前のめりがいないか　敵の生きてる人数が二人未満
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
                var vanguard = bm.EnemyGroup.InstantVanguard;
                var data = acter.TargetBonusDatas;
                var txt = "前のめり";
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

                    button.onClick.AddListener(() => OnClickSelectVanguardOrBacklines(button, WillSet[i]));
                    button.GetComponentInChildren<TextMeshProUGUI>().text = BtnStringSet[i];//ボタンのテキストに前のめり等の記述
                    EnemybuttonList.Add(button);//敵のボタンリストに入れる

                }


            }
        }



        if (EnemyTargeting)//敵全員を入れる
        {
            var selects = bm.EnemyGroup.Ours;

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
                    var txt = ene.CharacterName;//テキストにキャラ名
                    var data = acter.TargetBonusDatas;
                    if(data.DoIHaveTargetBonus(ene))//対象者ボーナスに含まれてるのなら
                    {
                        //その対象者のボーナス倍率を取得し、ボタンテキストに追加
                        var percentage = data.GetAtPowerBonusPercentage(data.GetTargetIndex(ene));//対象者ボーナス
                        txt += "\n " + percentage + "倍";
                    }


                    button.onClick.AddListener(() => OnClickSelectTarget(selects[i], button, WhichGroup.Enemyiy, DirectedWill.One));//関数を登録
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
                selects = bm.AllyGroup.Ours;
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

                button.onClick.AddListener(() => OnClickSelectTarget(selects[i], button, WhichGroup.alliy, DirectedWill.One));//関数を登録
                button.GetComponentInChildren<TextMeshProUGUI>().text = selects[i].CharacterName;//ボタンのテキストにキャラ名
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
        bm.Acter.Target = will;//渡された前のめりか後衛かの意思を入れる。

        ReturnNextWaitView();
    }

    /// <summary>
    /// "人物を対象として選ぶクリック関数"
    /// </summary>
    void OnClickSelectTarget(BaseStates target, Button thisBtn, WhichGroup faction,DirectedWill will)
    {
        CashUnders.Add(target);

        if (AllybuttonList.Count > 0 && faction == WhichGroup.Enemyiy)///敵のボタンで味方のボタンが一つ以上あったら
        {
            foreach (var button in AllybuttonList)
            {
                Destroy(button);//味方のボタンを全部消す
            }
        }

        if (EnemybuttonList.Count > 0 && faction == WhichGroup.alliy)///味方のボタンで敵のボタンが一つ以上あったら
        {
            foreach (var button in EnemybuttonList)
            {
                Destroy(button);//味方のボタンを全部消す
            }
        }

        //人物セレクトカウントをデクリメント
        if (faction == WhichGroup.alliy)
            NeedSelectCountAlly--;
        if (faction == WhichGroup.Enemyiy)
            NeedSelectCountEnemy--;

        //各陣営ごとに属したボタンが二つ以上なくても終わり (一つだけあってもそれは廃棄予定の"このオブジェクト"だから)
        //つまりもう選ぶボタンがないなら
        if (faction == WhichGroup.alliy)
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
        else if (faction == WhichGroup.Enemyiy)
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


            bm.Acter.Target = will;//選択意思を入れる
            Destroy(thisBtn);//このボタンは破棄
        }
    }
    /// <summary>
    /// 各対象者ボタンに渡すNextWaitにtabStateを戻す処理
    /// </summary>
    private void ReturnNextWaitView()
    {
        Walking.USERUI_state.Value = TabState.NextWait;

        //bmの対象者リストにキャッシュリストを入れる
        CashUnders.Shuffle();//分散値のランダム性のためシャッフル
        foreach(var cash in CashUnders)
        {
            bm.unders.CharaAdd(cash);
        }

        foreach (var button in AllybuttonList)
        {
            Destroy(button);//ボタン全部削除
        }
        foreach (var button in EnemybuttonList)
        {
            Destroy(button);//ボタン全部削除
        }
    }
}
