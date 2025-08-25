using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SelectCancelPassiveButtons : MonoBehaviour
{
    [SerializeField] GameObject cancelPassiveButtonPrefab;
    [SerializeField] Transform bascketField;
    
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
    
    public static SelectCancelPassiveButtons Instance { get; private set; }
    
    private void Awake()
    {
        // シングルトンパターン実装
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            Debug.LogError("SelectCancelPassiveButtonsのインスタンスが複数存在します");
            return;
        }
        Instance = this;
        
         // ボタンと親のサイズを取得
        var rectTransform = cancelPassiveButtonPrefab.GetComponent<RectTransform>();
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
    /// 指定したキャラクターがキャンセル可能なパッシブを持っている場合、
    /// そのパッシブ用のボタンを生成する
    /// </summary>
    /// <param name="chara">キャンセル可能なパッシブを持つキャラクター</param>
    public void ShowPassiveButtons(BaseStates chara)
    {
        ClearButtons();
        
        // キャラクターのパッシブリストを取得
        // キャンセル可能なパッシブのみをフィルタリング
        foreach (var passive in chara.PassiveList)
        {
            //CantACTのパッシブのみならば
            if(chara.HasCanCancelCantACTPassive)
            {
                if (passive.IsCantACT)//CantACTのパッシブならば
                {
                    if (passive.CanCancel)
                    {
                        CreatePassiveButton(passive, chara);
                    }
                }
            }
            else//CantACTのパッシブ以外ならば,すべてのキャンセル可能なパッシブを表示する
            {
                if (passive.CanCancel)
                {
                    CreatePassiveButton(passive, chara);
                }
            }
        }
    }
    
    /// <summary>
    /// パッシブボタンの生成
    /// </summary>
    /// <param name="passive">キャンセル対象のパッシブ</param>
    /// <param name="owner">パッシブの所持者</param>
    void CreatePassiveButton(BasePassive passive, BaseStates owner)
    {
        var obj = Instantiate(cancelPassiveButtonPrefab, bascketField);
        buttons.Add(obj);
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
        
        // 残りの部分は変更なし
        var text = obj.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
        {
            text.text = passive.PassiveName;
        }
        
        var button = obj.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(() => OnPassiveButtonClick(passive, owner));
        }
    }    
    /// <summary>
    /// パッシブキャンセルボタンクリック時の処理
    /// </summary>
    /// <param name="passive">キャンセル対象のパッシブ</param>
    /// <param name="owner">パッシブの所持者</param>
    void OnPassiveButtonClick(BasePassive passive, BaseStates owner)
    {
        // パッシブのキャンセル処理
        owner.RemovePassive(passive);
        
        Walking.Instance.bm.PassiveCancel = true;//ACTBranchingでpassiveCancelするboolをtrueに。

        Walking.Instance.USERUI_state.Value = TabState.NextWait;//CharacterACTBranchingへ
        
        
        // ボタンをクリア
        ClearButtons();
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
            var obj = Instantiate(cancelPassiveButtonPrefab, bascketField);
            buttons.Add(obj);
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
}