using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// 思い入れスキルを選ぶためのUI
/// </summary>
public class SelectEmotionalAttachmentSkillButtons : MonoBehaviour
{
    [SerializeField] GameObject ButtonPrefab;
    [SerializeField] Transform bascketField;
    [SerializeField] TextMeshProUGUI memo;
    [SerializeField] string defaultText;
    [SerializeField] string textOnChange;
    [SerializeField] TextMeshProUGUI NowEmotinalSkillText;

    // コールバック用のUnityEvent
    private UnityEvent<int> OnClicked = new();
    /// <summary>
    /// ボタンとスキルIDの紐づけ情報を保持するクラス
    /// </summary>
    private class SkillButtonData
    {
        public GameObject buttonObject;
        public int skillID;
        public string skillName;
        public Button button;
        
        public SkillButtonData(GameObject obj, int id, string name, Button btn)
        {
            buttonObject = obj;
            skillID = id;
            skillName = name;
            button = btn;
        }
    }
    /// <summary>
    /// 現在表示しているボタンのリスト
    /// </summary>
    private List<SkillButtonData> buttons = new();
    
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
    /// 現在の思い入れスキルID（ボタン生成時に設定）
    /// </summary>
    private int currentEmotionalAttachmentSkillID = -1;
    private AllySkill currentOldSkill;
    
    public static SelectEmotionalAttachmentSkillButtons Instance { get; private set; }
    
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
    /// <param name="skills">表示するスキルリスト</param>
    /// <param name="currentEmotionalSkillID">現在の思い入れスキルID</param>
    public void ShowSkillsButtons(List<AllySkill> skills, int currentEmotionalSkillID,UnityAction<int> OnClickeEvent)
    {
        ClearButtons();
        currentEmotionalAttachmentSkillID = currentEmotionalSkillID;//現在の思い入れスキルIDを保持
        // 現在の思い入れスキル（変更前のスキル）を特定して保持
        currentOldSkill = skills.FirstOrDefault(skill => skill.ID == currentEmotionalAttachmentSkillID);
        
        OnClicked.AddListener(OnClickeEvent);//結果を受け取るためのイベントを渡す

        // デバッグログ追加
        Debug.Log($"[EmotionalAttachment] Current Skill ID: {currentEmotionalAttachmentSkillID}");
        Debug.Log($"[EmotionalAttachment] Skills count: {skills.Count}");

        string currentInfo = "現在の思い入れスキル\n";
        if(currentOldSkill != null)
        {
            currentInfo += currentOldSkill.SkillName;
        }else
        {
            currentInfo += "なし";
        }
        
        if (NowEmotinalSkillText != null)
        {
            NowEmotinalSkillText.text = currentInfo;
        }
        
        // 作るべきスキルリストのボタンを取得
        foreach (var skill in skills)
        {
            Debug.Log($"[EmotionalAttachment] Skill ID: {skill.ID}, Name: {skill.SkillName}");
            CreateSkillButton(skill);
        }
    }
    
    /// <summary>
    /// スキルボタンの生成
    /// </summary>
    void CreateSkillButton(AllySkill skill)
    {
        var obj = Instantiate(ButtonPrefab, bascketField);
        var button = obj.GetComponent<Button>();
        buttons.Add(new SkillButtonData(obj, skill.ID, skill.SkillName, button));
        obj.SetActive(true);
        
        // ボタンの位置を調整して順番に並べる
        var rectTransform = obj.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            // ボタン数から最適な列数を計算
            int maxButtonsPerRow = Mathf.Max(1, Mathf.FloorToInt((parentSize.x - horizontalPadding * 2) / (buttonSize.x + horizontalPadding)));
            // 最低でも2列にする
            int buttonsPerRow = Mathf.Max(2, maxButtonsPerRow);
            
            int buttonIndex = buttons.Count - 1;
            int row = buttonIndex / buttonsPerRow;
            int col = buttonIndex % buttonsPerRow;
            
            float posX = startX + col * (buttonSize.x + horizontalPadding);
            float posY = startY - row * (buttonSize.y + verticalPadding);
            
            rectTransform.anchoredPosition = new Vector2(posX, posY);
        }
        
        // スキル名をボタンに表示
        var text = obj.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
        {
            text.text = skill.SkillName;
        }
        
        // ボタンの見た目を現在の思い入れスキルかどうかで変更
        //IDが一致しなければ有効化され、一致すればfalseが代入され無効化される。
        button.interactable = skill.ID != currentEmotionalAttachmentSkillID;

        // デバッグログ追加
        Debug.Log($"[EmotionalAttachment] Button for Skill {skill.ID} ({skill.SkillName}) - interactable: {button.interactable} (current: {currentEmotionalAttachmentSkillID})");
        
        button.onClick.AddListener(() => OnSkillButtonClick(skill.ID, skill));
    }
    
    /// <summary>
    /// スキル選択ボタンクリック時の処理
    /// </summary>
    /// <param name="skillID">選択されたスキルID</param>
    void OnSkillButtonClick(int skillID, AllySkill skill)
    {
        // 思い入れスキルの入れ替え処理をコールバックで通知
        OnClicked.Invoke(skillID);

        NowEmotinalSkillText.text = "現在の思い入れスキル\n" + skill.SkillName;
        
        // 変更前のスキル名を使ってメッセージを作成
        if (currentOldSkill != null)
        {
            // textOnChangeに{0}が含まれていればスキル名を挿入、なければそのまま表示
            if (textOnChange.Contains("{0}"))
            {
                memo.text = string.Format(textOnChange, currentOldSkill.SkillName);
            }
            else
            {
                memo.text = textOnChange; // プレースホルダーがない場合はそのまま
            }
        }
        else
        {
            memo.text = textOnChange; // フォールバック
        }
        
        // 現在の思い入れスキルIDを更新
        int previousSkillID = currentEmotionalAttachmentSkillID;
        currentEmotionalAttachmentSkillID = skillID;
        currentOldSkill = skill;//今回選んだスキルを、「次他のボタンを押したときの」変更前のスキルとして保持
        
        // ボタンの状態を更新（入れ替え表現）
        UpdateButtonStates(previousSkillID, skillID);
        
        Debug.Log($"思い入れスキルを変更: {previousSkillID} → {skillID}");
    }
    
    /// <summary>
    /// ボタンの状態を更新（思い入れスキル変更時の見た目更新）
    /// </summary>
    /// <param name="previousSkillID">前の思い入れスキルID</param>
    /// <param name="newSkillID">新しい思い入れスキルID</param>
    void UpdateButtonStates(int previousSkillID, int newSkillID)
    {
        foreach (var buttonObj in buttons)
        {
            var button = buttonObj.button;
            if (button != null)
            {
                button.interactable = true; // 一旦全て有効化
            }
        }
        
        // 新しい思い入れスキルのボタンのみ無効化
        foreach (var buttonObj in buttons)
        {
            if (buttonObj.skillID == newSkillID)
            {
                buttonObj.button.interactable = false;
            }
        }
    }
    
    /// <summary>
    /// 表示中のボタンをすべて削除
    /// </summary>
    public void ClearButtons()
    {
        foreach (var obj in buttons)
        {
            Destroy(obj.buttonObject);
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
            buttons.Add(new SkillButtonData(obj, i, "ボタン " + (i + 1), obj.GetComponent<Button>()));
            obj.SetActive(true);
            
            // ボタンの位置を調整
            var rectTransform = obj.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                // ボタン数から最適な列数を計算
                int maxButtonsPerRow = Mathf.Max(1, Mathf.FloorToInt((parentSize.x - horizontalPadding * 2) / (buttonSize.x + horizontalPadding)));
                // 最低でも2列にする
                int buttonsPerRow = Mathf.Max(2, maxButtonsPerRow);
                
                int buttonIndex = i;
                int row = buttonIndex / buttonsPerRow;
                int col = buttonIndex % buttonsPerRow;
                
                float posX = startX + col * (buttonSize.x + horizontalPadding);
                float posY = startY - row * (buttonSize.y + verticalPadding);
                
                rectTransform.anchoredPosition = new Vector2(posX, posY);
                // ボタン生成時にデバッグ情報を表示
                Debug.Log($"ボタン {i+1} の設定 - Anchor: {rectTransform.anchorMin}, {rectTransform.anchorMax}, Pivot: {rectTransform.pivot}");
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
        /// <summary>
    /// 思い入れスキル選択UIを閉じる
    /// </summary>
    public void CloseEmotionalAttachmentSkillSelectUIArea()
    {
        gameObject.SetActive(false);
    }
    /// <summary>
    /// 思い入れスキル選択UIを表示する
    /// </summary>
    public void OpenEmotionalAttachmentSkillSelectUIArea()
    {
        gameObject.SetActive(true);
        memo.text = defaultText;
    }

}
