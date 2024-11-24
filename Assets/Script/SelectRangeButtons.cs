using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UI;


public class SelectRangeButtons : MonoBehaviour
{
    public static SelectRangeButtons Instance { get; private set; }

    [SerializeField]
    Button buttonPrefab;
    [SerializeField]
    RectTransform parentRect;
    [Header("Layout Settings")]
    [SerializeField] float horizontalPadding = 10f; // ボタン間の横余白
    [SerializeField] float verticalPadding = 10f;   // ボタン間の縦余白

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(this);
        }
        buttonSize = buttonPrefab.GetComponent<RectTransform>().sizeDelta;
        parentSize = parentRect.rect.size;

        // 親オブジェクトの左上を基準とするためのオフセット
        startX = -parentSize.x / 2 + buttonSize.x / 2 + horizontalPadding;
        startY = parentSize.y / 2 - buttonSize.y + horizontalPadding;
        //親オブジェクトの左下に固定する為のオプション用オフセット
        optionStartX = -parentSize.x / 2 + buttonSize.x / 2 + horizontalPadding;
        optionStartY = -parentSize.y / 2 + buttonSize.y / 2 + horizontalPadding;
    }
    // ボタンのサイズを取得
    Vector2 buttonSize;
    // 親オブジェクトのサイズを取得
    Vector2 parentSize;
    // 親オブジェクトの左上を基準とするためのオフセット
    float startX;
    float startY;
    float optionStartX;
    float optionStartY;

    BattleManager bm;
    List<Button> buttonList;
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


        //random以外の範囲の性質がスキル性質に含まれてる分だけ、その範囲の性質を選ぶ


        if (skill.HasZoneTrait(SkillZoneTrait.CanSelectSingleTarget))//前のめりか後衛内ランダムかで選べるなら
        {
            //.CanSelectSingleTargetを今回の範囲とするボタンを作成する。
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

            button.onClick.AddListener(() => OnClickRangeBtn(button, SkillZoneTrait.CanSelectSingleTarget));
            button.GetComponentInChildren<TextMeshProUGUI>().text =
                "前のめりまたはそれ以外のどちらかを狙う" + AddPercentageTextOnButton(SkillZoneTrait.CanSelectSingleTarget);//ボタンのテキスト
            buttonList.Add(button);//ボタンリストに入れる

        }

        if (skill.HasZoneTrait(SkillZoneTrait.CanPerfectSelectSingleTarget))//個々で選べるなら
        {
            //.CanSelectSingleTargetを今回の範囲とするボタンを作成する。
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

            button.onClick.AddListener(() => OnClickRangeBtn(button, SkillZoneTrait.CanPerfectSelectSingleTarget));
            button.GetComponentInChildren<TextMeshProUGUI>().text =
                "個々を狙う" + AddPercentageTextOnButton(SkillZoneTrait.CanPerfectSelectSingleTarget);//ボタンのテキスト
            buttonList.Add(button);//ボタンリストに入れる

        }
        if (skill.HasZoneTrait(SkillZoneTrait.CanSelectMultiTarget))//前のめりまたは後衛の団体かで選べるなら
        {
            //.CanSelectSingleTargetを今回の範囲とするボタンを作成する。
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

            button.onClick.AddListener(() => OnClickRangeBtn(button, SkillZoneTrait.CanSelectMultiTarget));
            button.GetComponentInChildren<TextMeshProUGUI>().text =
                "前のめりかそれ以外二人を狙う" + AddPercentageTextOnButton(SkillZoneTrait.CanSelectMultiTarget);//ボタンのテキスト
            buttonList.Add(button);//ボタンリストに入れる

        }


        if (skill.HasZoneTrait(SkillZoneTrait.AllTarget))//全範囲なら
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

            button.onClick.AddListener(() => OnClickRangeBtn(button, SkillZoneTrait.AllTarget));
            button.GetComponentInChildren<TextMeshProUGUI>().text =
                "敵の全範囲を狙う" + AddPercentageTextOnButton(SkillZoneTrait.AllTarget);//ボタンのテキスト
            buttonList.Add(button);//ボタンリストに入れる

        }


        //ここからオプションのボタン

        currentX = optionStartX;
        currentY = optionStartY;

        if (skill.HasZoneTrait(SkillZoneTrait.CanSelectAlly))//味方を選ぶオプションを追加するなら
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

            button.onClick.AddListener(() => OnClickRangeBtn(button, SkillZoneTrait.CanSelectAlly));
            button.GetComponentInChildren<TextMeshProUGUI>().text =
                "味方" + AddPercentageTextOnButton(SkillZoneTrait.CanSelectAlly);//ボタンのテキスト
            buttonList.Add(button);//ボタンリストに入れる

        }

        if (skill.HasZoneTrait(SkillZoneTrait.CanSelectDeath))//死者を選ぶオプションを追加するなら
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

            button.onClick.AddListener(() => OnClickRangeBtn(button, SkillZoneTrait.CanSelectDeath));
            button.GetComponentInChildren<TextMeshProUGUI>().text =
                "死者" + AddPercentageTextOnButton(SkillZoneTrait.CanSelectDeath);//ボタンのテキスト
            buttonList.Add(button);//ボタンリストに入れる

        }






    }
    /// <summary>
    /// テスト用ボタン
    /// </summary>
    public void OnClickTestButton()
    {
        // 現在の位置を初期化
        float currentX = startX;
        float currentY = startY;

        const int Optioncount = 2;
        const int count = 3;

        currentX = startX;//通常ボタン
        currentY = startY;

        for (int i = 0; i < count; i++)
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



        currentX = optionStartX;//オプションボタン
        currentY = optionStartY;

        for (int i = 0; i < Optioncount; i++)
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
    /// オプションの範囲選択ボタンに渡すコールバック
    /// </summary>
    public void OnClickOptionRangeBtn(Button thisbtn, SkillZoneTrait option)
    {
        bm.Acter.RangeWill |= option;
        Destroy(thisbtn);//ボタンは消える

        //オプションなのでこれ選んだだけでは次へ進まない。
    }

    public void OnClickRangeBtn(Button thisbtn, SkillZoneTrait range)
    {
        bm.Acter.RangeWill |= range;
        foreach (var button in buttonList)
        {
            Destroy(button);//ボタン全部削除
        }
        NextTab();//次へ行く

    }
    /// <summary>
    /// ボタンに威力の範囲による割合差分のテキストを追加する
    /// 引数に範囲性質を渡すと、それに応じた割合差分があるならば、数字のテキストを返す。
    /// </summary>
    private string AddPercentageTextOnButton(SkillZoneTrait zone)
    {
        var skill = bm.Acter.NowUseSkill;
        var txt = "";//何もなければ空文字が返るのみ
        if (skill.PowerRangePercentageDictionary.ContainsKey(zone))//その範囲性質の割合差分があるならば
        {
            txt = "\n割合差分:" + (skill.PowerRangePercentageDictionary[zone] * 100).ToString() + "%";//テキストに入れる。
        }

        return txt;
    }

    /// <summary>
    /// 次のタブへ行く
    /// </summary>
    private void NextTab()
    {
        //全範囲ならそのままnextWait　　対象を選ぶ必要がないからね
        if (bm.Acter.HasRangeWill(SkillZoneTrait.AllTarget))
        {
            Walking.USERUI_state.Value = TabState.NextWait;
        }
        else
        {
            Walking.USERUI_state.Value = TabState.SelectTarget;//そうでないなら選択画面へ。

        }

    }





}
