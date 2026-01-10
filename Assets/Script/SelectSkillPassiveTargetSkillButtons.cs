using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;
using Cysharp.Threading.Tasks;   

public class SelectSkillPassiveTargetSkillButtons : MonoBehaviour
{
    [SerializeField] GameObject ButtonPrefab;
    [SerializeField] Transform bascketField;

    private UniTaskCompletionSource<List<BaseSkill>> tcs;
    private List<BaseSkill> selected = new();   // 選択結果を保持

    int SelectCount = 0;

    
    [Header("レイアウト設定")]
    [SerializeField] float horizontalPadding = 10f; // ボタン間の横余白
    [SerializeField] float verticalPadding = 10f;   // ボタン間の縦余白
    
    // ボタンのサイズを取得
    Vector2 buttonSize;
    // 親オブジェクトのサイズを取得
    Vector2 parentSize;
    // 親オブジェクトの左上を基準とするためのオフセット
    float startX;
    float startY;
    
    /// <summary>
    /// 現在表示しているボタンのリスト
    /// </summary>
    List<GameObject> buttons = new List<GameObject>();
    
    public static SelectSkillPassiveTargetSkillButtons Instance { get; private set; }
    
    private void Awake()
    {
        // シングルトンパターン実装
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
         // ボタンと親のサイズを取得
        var rectTransform = ButtonPrefab.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            buttonSize = rectTransform.sizeDelta;
            Debug.Log($"ボタンサイズ: {buttonSize}");
        }
        
        var parentRect = bascketField.GetComponent<RectTransform>();
        if (parentRect != null)
        {
            // 親のrectサイズを取得
            parentSize = parentRect.rect.size;
            Debug.Log($"親サイズ: {parentSize}");
            
            // 最初のボタンにも余白を加える
            startX = -parentSize.x / 2 + buttonSize.x / 2 + horizontalPadding;  // 水平パディングを追加
            startY = parentSize.y / 2 - buttonSize.y / 2 - verticalPadding;     // 垂直パディングを追加
        }
    }
    /// <summary>
    /// 渡された情報を元に複数のスキルボタンの生成
    /// </summary>
    public async UniTask<List<BaseSkill>> ShowSkillsButtons(List<BaseSkill> skills, int selectCount)
    {
        SelectCount = selectCount;//選択可能数を代入
        ClearButtons();
        selected.Clear();

        tcs = new UniTaskCompletionSource<List<BaseSkill>>();        
        // 作るべきスキルリストのボタンを取得
        foreach (var skill in skills)
        {
            CreateSkillButton(skill);
        }

        // 完了を待機して結果を返す
        return await tcs.Task;
    }
    
    /// <summary>
    /// スキルボタンの生成
    /// </summary>
    void CreateSkillButton(BaseSkill skill)
    {
        var obj = Instantiate(ButtonPrefab, bascketField);
        buttons.Add(obj);
        obj.SetActive(true);
        
        // ボタンの位置を調整して順番に並べる
        var rectTransform = obj.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            int index = buttons.Count - 1; // 0 始まりで上から数える
            float posX = startX; // X 位置は固定
            float posY = startY - index * (buttonSize.y + verticalPadding);
            rectTransform.anchoredPosition = new Vector2(posX, posY);
        }
        
        // 残りの部分は変更なし
        var text = obj.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
        {
            text.text = skill.SkillName;
        }
        
        var button = obj.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(() => OnSkillButtonClick(skill));
        }
    }    
    /// <summary>
    /// スキル選択ボタンクリック時の処理
    /// </summary>
    /// <param name="skill">選択されたスキル</param>
    void OnSkillButtonClick(BaseSkill skill)
    {
        selected.Add(skill);

        SelectCount--;//選択可能数をデクリメント
        if(SelectCount == 0)
        {
            ClearButtons();//ボタンを消す
            var skillUi = PlayersStatesHub.SkillUI;
            if (skillUi != null)
            {
                skillUi.ReturnSelectSkillPassiveTargetSkillButtonsArea();//ボタンモーダルエリアから退出
            }
            else
            {
                Debug.LogError("SelectSkillPassiveTargetSkillButtons: PlayersStatesHub.SkillUI が null です");
            }
            // 完了通知
            tcs.TrySetResult(selected);
        }
    }
    
    /// <summary>
    /// 表示中のボタンをすべて削除
    /// </summary>
    public void ClearButtons()
    {
        foreach (var obj in buttons)
        {
            Destroy(obj);
        }
        buttons.Clear();
    }

    /// <summary>
    /// ボタンの配置テスト用関数 - 指定した数のボタンを生成して表示
    /// </summary>
    /// <param name="count">表示するボタンの数</param>
    public void TestButtonLayout(int count)
    {
        ClearButtons();
        
        // 毎回サイズを再取得
        var parentRect = bascketField.GetComponent<RectTransform>();
        if (parentRect != null)
        {
            parentSize = parentRect.rect.size;
            // 最初のボタンにも余白を加える
            startX = -parentSize.x / 2 + buttonSize.x / 2 + horizontalPadding;  // 水平パディングを追加
            startY = parentSize.y / 2 - buttonSize.y / 2 - verticalPadding;     // 垂直パディングを追加
        }
        
        for (int i = 0; i < count; i++)
        {
            var obj = Instantiate(ButtonPrefab, bascketField);
            buttons.Add(obj);
            obj.SetActive(true);
            
            // ボタンの位置を調整
            var rectTransform = obj.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                int index = i; // 0 始まりで上から数える
                float posX = startX; // X 位置は固定
                float posY = startY - index * (buttonSize.y + verticalPadding);
                rectTransform.anchoredPosition = new Vector2(posX, posY);
                // ボタン生成時にデバッグ情報を表示
                Debug.Log($"ボタン {i+1} の位置 - 計算値: ({posX}, {posY}), 実際: {rectTransform.anchoredPosition}");
            }
            
            // ボタンに番号を表示
            var text = obj.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
            {
                text.text = "ボタン " + (i + 1);
            }
        }
    }
}
