using System.Collections;
using System.Collections.Generic;
using TMPro;
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

    BattleUIBridge uiBridge => BattleUIBridge.Active;
    IBattleContext battle => uiBridge?.BattleContext;
    private BattleOrchestrator _orchestrator;
    private BattleOrchestrator Orchestrator => _orchestrator ?? BattleOrchestratorHub.Current;
    public void Initialize(BattleOrchestrator orchestrator) => _orchestrator = orchestrator;
    List<Button> buttonList;

    /// <summary>
    /// 全ボタンを削除してリストをクリア
    /// </summary>
    private void ClearAllButtons()
    {
        if (buttonList != null)
        {
            foreach (var button in buttonList)
            {
                if (button != null)
                    Destroy(button.gameObject);
            }
            buttonList.Clear();
        }
    }

    /// <summary>
    /// 範囲選択ボタンを生成するヘルパー
    /// </summary>
    /// <param name="trait">スキル範囲性質</param>
    /// <param name="text">ボタンテキスト</param>
    /// <param name="currentX">現在のX座標（参照渡し）</param>
    /// <param name="currentY">現在のY座標（参照渡し）</param>
    /// <param name="isOption">オプションボタンかどうか</param>
    /// <returns>生成されたボタン</returns>
    private Button CreateRangeButton(
        SkillZoneTrait trait,
        string text,
        ref float currentX,
        ref float currentY,
        bool isOption = false)
    {
        var button = Instantiate(buttonPrefab, transform);
        var rect = button.GetComponent<RectTransform>();

        // 親オブジェクトの右端を超える場合は次の行に移動
        if (currentX + buttonSize.x / 2 > parentSize.x / 2)
        {
            currentX = isOption ? optionStartX : startX;
            currentY -= (buttonSize.y + verticalPadding);
        }

        // ボタンの位置を設定
        rect.anchoredPosition = new Vector2(currentX, currentY);

        // 次のボタンのX位置を更新
        currentX += (buttonSize.x + horizontalPadding);

        // リスナー登録
        if (isOption)
            button.onClick.AddListener(() => OnClickOptionRangeBtn(button, trait));
        else
            button.onClick.AddListener(() => OnClickRangeBtn(button, trait));

        button.GetComponentInChildren<TextMeshProUGUI>().text = text + AddPercentageTextOnButton(trait);
        buttonList.Add(button);
        return button;
    }

    private void OnDestroy()
    {
        ClearAllButtons();
    }

    /// <summary>
    /// 生成用コールバック
    /// </summary>
    public void OnCreated()
    {
        // 既存ボタンをクリアして新規リスト作成
        ClearAllButtons();
        buttonList = new List<Button>();

        var battleContext = battle;
        if (battleContext == null)
        {
            Debug.LogError("SelectRangeButtons.OnCreated: BattleContext が null です");
            return;
        }
        var acter = battleContext.Acter;
        var skill = acter.NowUseSkill;

        // 現在の位置を初期化
        float currentX = startX;
        float currentY = startY;


        //random以外の範囲の性質がスキル性質に含まれてる分だけ、その範囲の性質を選ぶ
        //主流排他的範囲意志の選択。

        if(skill.HasZoneTrait(SkillZoneTrait.SelectOnlyAlly))//味方のみを対象を前提としたスキルならば、
        {
            if (skill.HasZoneTrait(SkillZoneTrait.CanPerfectSelectSingleTarget))
                CreateRangeButton(SkillZoneTrait.CanPerfectSelectSingleTarget, "個々を狙う", ref currentX, ref currentY);
            if (skill.HasZoneTrait(SkillZoneTrait.AllTarget))
                CreateRangeButton(SkillZoneTrait.AllTarget, "味方の全範囲を狙う", ref currentX, ref currentY);
        }
        else//味方オンリーでない、標準的な戦闘の範囲選択の場合
        {
            if (skill.HasZoneTrait(SkillZoneTrait.CanSelectSingleTarget))
                CreateRangeButton(SkillZoneTrait.CanSelectSingleTarget, "前のめりまたはそれ以外のどちらかを狙う", ref currentX, ref currentY);
            if (skill.HasZoneTrait(SkillZoneTrait.CanPerfectSelectSingleTarget))
                CreateRangeButton(SkillZoneTrait.CanPerfectSelectSingleTarget, "個々を狙う", ref currentX, ref currentY);
            if (skill.HasZoneTrait(SkillZoneTrait.CanSelectMultiTarget))
                CreateRangeButton(SkillZoneTrait.CanSelectMultiTarget, "前のめりかそれ以外二人を狙う", ref currentX, ref currentY);
            if (skill.HasZoneTrait(SkillZoneTrait.AllTarget))
                CreateRangeButton(SkillZoneTrait.AllTarget, "敵の全範囲を狙う", ref currentX, ref currentY);
        }

        //ここからオプションのボタン

        currentX = optionStartX;
        currentY = optionStartY;
        if(skill.HasZoneTrait(SkillZoneTrait.SelectOnlyAlly))//味方のみを対象を前提としたスキルならば、
        {
            if (skill.HasZoneTrait(SkillZoneTrait.CanSelectMyself))
                CreateRangeButton(SkillZoneTrait.CanSelectMyself, "自分自身", ref currentX, ref currentY, isOption: true);
            if (skill.HasZoneTrait(SkillZoneTrait.CanSelectDeath))
                CreateRangeButton(SkillZoneTrait.CanSelectDeath, "死者", ref currentX, ref currentY, isOption: true);
        }
        else //標準的な敵を主軸に置くスキルなら
        {
            if (skill.HasZoneTrait(SkillZoneTrait.CanSelectAlly))
                CreateRangeButton(SkillZoneTrait.CanSelectAlly, "味方", ref currentX, ref currentY, isOption: true);
            if (skill.HasZoneTrait(SkillZoneTrait.CanSelectMyself))
                CreateRangeButton(SkillZoneTrait.CanSelectMyself, "自分自身", ref currentX, ref currentY, isOption: true);
            if (skill.HasZoneTrait(SkillZoneTrait.CanSelectDeath))
                CreateRangeButton(SkillZoneTrait.CanSelectDeath, "死者", ref currentX, ref currentY, isOption: true);
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
        var orchestrator = Orchestrator;
        if (orchestrator == null)
        {
            Debug.LogError("[CRITICAL] SelectRangeButtons.OnClickOptionRangeBtn: BattleOrchestrator is not initialized");
            return;
        }

        var input = new ActionInput
        {
            Kind = ActionInputKind.RangeSelect,
            RequestId = orchestrator.CurrentChoiceRequest.RequestId,
            Actor = battle?.Acter,
            RangeWill = option,
            IsOption = true
        };
        var state = orchestrator.ApplyInput(input);
        Destroy(thisbtn);//ボタンは消える
        if (uiBridge != null)
        {
            uiBridge.SetUserUiState(state, false);
        }
        else
        {
            Debug.LogError("SelectRangeButtons.OnClickOptionRangeBtn: BattleUIBridge が null です");
        }
    }

    public void OnClickRangeBtn(Button thisbtn, SkillZoneTrait range)
    {
        var orchestrator = Orchestrator;
        if (orchestrator == null)
        {
            Debug.LogError("[CRITICAL] SelectRangeButtons.OnClickRangeBtn: BattleOrchestrator is not initialized");
            return;
        }

        var input = new ActionInput
        {
            Kind = ActionInputKind.RangeSelect,
            RequestId = orchestrator.CurrentChoiceRequest.RequestId,
            Actor = battle?.Acter,
            RangeWill = range
        };
        var state = orchestrator.ApplyInput(input);
        foreach (var button in buttonList)
        {
            Destroy(button);//ボタン全部削除
        }
        if (uiBridge != null)
        {
            uiBridge.SetUserUiState(state, false);
        }
        else
        {
            Debug.LogError("SelectRangeButtons.OnClickRangeBtn: BattleUIBridge が null です");
        }
    }
    /// <summary>
    /// ボタンに威力の範囲による割合差分のテキストを追加する
    /// 引数に範囲性質を渡すと、それに応じた割合差分があるならば、数字のテキストを返す。
    /// </summary>
    private string AddPercentageTextOnButton(SkillZoneTrait zone)
    {
        var skill = battle.Acter.NowUseSkill;
        var txt = "割合差分なし";//何もなければ空文字が返るのみ
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
        if (battle.Acter.HasRangeWill(SkillZoneTrait.AllTarget))
        {
            if (uiBridge != null)
            {
                uiBridge.SetUserUiState(TabState.NextWait);
            }
            else
            {
                Debug.LogError("SelectRangeButtons.OnClickRangeBtn: BattleUIBridge が null です");
            }
        }
        else
        {
            if (uiBridge != null)
            {
                uiBridge.SetUserUiState(TabState.SelectTarget);//そうでないなら選択画面へ。
            }
            else
            {
                Debug.LogError("SelectRangeButtons.OnClickRangeBtn: BattleUIBridge が null です");
            }

        }

    }





}
