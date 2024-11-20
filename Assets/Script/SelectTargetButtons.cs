using RandomExtensions;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UI;

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
        startX = -parentSize.x / 2 + buttonSize.x / 2;
        startY = parentSize.y / 2 - buttonSize.y;
    }
    // ボタンのサイズを取得
    Vector2 buttonSize;
    // 親オブジェクトのサイズを取得
    Vector2 parentSize;
    // 親オブジェクトの左上を基準とするためのオフセット
    float startX;
    float startY;

    BattleManager bm;
    int NeedSelectCountAlly;//このneedcountは基本的には対象選択のみ
    int NeedSelectCountEnemy;
    List<Button> AllybuttonList;
    List<Button> EnemybuttonList;
    /// <summary>
    /// 生成用コールバック
    /// </summary>
    public void OnCreated(BattleManager _bm)
    {
        bm = _bm;
        var acter = bm.Acter;
        var underActer = bm.UnderActer;
        var skill = acter.NowUseSkill;

        // 現在の位置を初期化
        float currentX = startX;
        float currentY = startY;


        //buttonPrefabをスキル性質に応じてbmのグループを特定の人数分だけ作って生成する
        bool EnemyTargeting = false;//敵の対象選択
        bool AllyTargeting = false;//味方の対象選択
        bool EnemyVanguardOrBackLine = false;//敵の前のめりor後衛

        if (skill.HasZoneTrait(SkillZoneTrait.CanPerfectSelectSingleTarget))//選択可能な単体対象
        {
            EnemyTargeting = true;
            NeedSelectCountEnemy = 1;

            if (skill.HasZoneTrait(SkillZoneTrait.CanSelectAlly))
            {
                AllyTargeting = true;//味方も選べたら味方も追加
                NeedSelectCountAlly = 1;
            }

        }
        if (skill.HasZoneTrait(SkillZoneTrait.CanSelectSingleTarget))//前のめりか後衛(ランダム単体)を狙うか
        {
            EnemyVanguardOrBackLine = true;
            if (skill.HasZoneTrait(SkillZoneTrait.CanSelectAlly))//味方も選べるなら
            {
                AllyTargeting = true;//味方は単体でしか選べない
                //一人または二人単位 
                NeedSelectCountAlly = Random.Range(1, 3);
            }
        }

        if (skill.HasZoneTrait(SkillZoneTrait.CanSelectSingleTarget))//前のめりか後衛(範囲)を狙うか
        {
            EnemyVanguardOrBackLine = true;
            if (skill.HasZoneTrait(SkillZoneTrait.CanSelectAlly))//味方も選べるなら
            {
                AllyTargeting = true;//味方は単体でしか選べない
                //二人範囲 
                NeedSelectCountAlly = 2;
            }
        }

        //ボタン作成フェーズ☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆☆


        if (EnemyVanguardOrBackLine) //前のめりか後衛かを敵から選ぶには
        {
            //前のめりが存在するかしないか、そもそも一人かどうかだと強制的にBackOrAnyにDirectedWillになって対象選択画面を飛ばす。
            //つまりボタンを作らずそのままNextWaitへ

            var enemyLives = bm.RemoveDeathCharacters(bm.EnemyGroup.Ours);//生きてる敵だけ
            if((bm.EnemyGroup.InstantVanguard == null || enemyLives.Count < 2)) //前のめりがいないか　敵の生きてる人数が二人未満
            {
                if (!AllyTargeting)//味方選択がないなら
                {
                    ReturnNextWaitView();//そのまま次の画面へ
                    bm.Acter.Target = DirectedWill.BacklineOrAny;//後衛または誰かの意思を入れとく。
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
                        currentY -= (buttonSize.y + verticalPadding);
                    }

                    // ボタンの位置を設定
                    rect.anchoredPosition = new Vector2(currentX, currentY);

                    // 次のボタンのX位置を更新
                    currentX += (buttonSize.x + horizontalPadding);

                    button.onClick.AddListener(() => OnClickSelectVanguardOrBacklines(button, DirectedWill.BacklineOrAny));
                    button.GetComponentInChildren<TextMeshProUGUI>().text = "敵";//ボタンのテキスト
                    EnemybuttonList.Add(button);//敵のボタンリストに入れる
                }
            }
            else//前のめりがいて二人以上いるなら
            {
                DirectedWill[] WillSet = new DirectedWill[] { DirectedWill.InstantVanguard, DirectedWill.BacklineOrAny };//for文で処理するため配列
                string[] BtnStringSet = new string[] { "前のめり", "それ以外" };

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
                        currentY -= (buttonSize.y + verticalPadding);
                    }

                    // ボタンの位置を設定
                    rect.anchoredPosition = new Vector2(currentX, currentY);

                    // 次のボタンのX位置を更新
                    currentX += (buttonSize.x + horizontalPadding);

                    button.onClick.AddListener(() => OnClickSelectVanguardOrBacklines(button, WillSet[i]));
                    button.GetComponentInChildren<TextMeshProUGUI>().text = BtnStringSet[i];//ボタンのテキストに前のめり等の記述
                    EnemybuttonList.Add(button);//敵のボタンリストに入れる

                }


            }
        }



        if (EnemyTargeting)//敵全員を入れる
        {
            var selects = bm.EnemyGroup.Ours;

            if (!skill.HasZoneTrait(SkillZoneTrait.CanSelectDeath))//死亡者選択不可能なら
            {
                selects = bm.RemoveDeathCharacters(selects);//省く
            }

            if (selects.Count < 2 && AllyTargeting)//敵の生きてる人数が二人未満で、味方の選択もなければ
            {
                ReturnNextWaitView();//そのまま次の画面へ
                bm.Acter.Target = DirectedWill.BacklineOrAny;//後衛または誰かの意思を入れとく。
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
                        currentY -= (buttonSize.y + verticalPadding);
                    }

                    // ボタンの位置を設定
                    rect.anchoredPosition = new Vector2(currentX, currentY);

                    // 次のボタンのX位置を更新
                    currentX += (buttonSize.x + horizontalPadding);

                    button.onClick.AddListener(() => OnClickSelectTarget(selects[i], button, WhichGroup.Enemyiy, DirectedWill.One));//関数を登録
                    button.GetComponentInChildren<TextMeshProUGUI>().text = selects[i].CharacterName;//ボタンのテキストにキャラ名
                    EnemybuttonList.Add(button);//敵のボタンリストを入れる

                }

            }
        }

        if (AllyTargeting)//味方全員を入れる
        {
            var selects = bm.AllyGroup.Ours;


            if(!skill.HasZoneTrait(SkillZoneTrait.CanSelectDeath))//死亡者選択不可能なら
            {
                selects = bm.RemoveDeathCharacters(selects);//省く
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
                    currentY -= (buttonSize.y + verticalPadding);
                }

                // ボタンの位置を設定
                rect.anchoredPosition = new Vector2(currentX, currentY);

                // 次のボタンのX位置を更新
                currentX += (buttonSize.x + horizontalPadding);

                button.onClick.AddListener(() => OnClickSelectTarget(selects[i], button, WhichGroup.alliy, DirectedWill.One));//関数を登録
                button.GetComponentInChildren<TextMeshProUGUI>().text = selects[i].CharacterName;//ボタンのテキストにキャラ名
                EnemybuttonList.Add(button);//敵のボタンリストを入れる

            }

        }

        //選択を途中で終えるボタン
        SelectEndBtn.gameObject.SetActive(false);//見えなくする。
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
        bm.UnderActer.Add(target);


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
