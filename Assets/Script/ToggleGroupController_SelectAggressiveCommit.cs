using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

/// <summary>
/// 前のめりを選択するトグルグループのコントローラー
/// </summary>
public class ToggleGroupController_SelectAggressiveCommit : MonoBehaviour
{
    // トグルグループ内のトグル
    [SerializeField] private Toggle toggleAggressiveYes; // 前のめり「する」側のトグル
    [SerializeField] private Toggle toggleAggressiveNo;  // 前のめり「しない」側のトグル
    
    // コールバック
    private UnityAction<int> onToggleSelected;
    
    // 有効/無効設定
    public bool interactable
    {
        get { return toggleAggressiveYes != null && toggleAggressiveYes.interactable; }
        set 
        {
            if (toggleAggressiveYes != null)
                toggleAggressiveYes.interactable = value;
                
            if (toggleAggressiveNo != null)
                toggleAggressiveNo.interactable = value;
        }
    }
    
    private void Start()
    {
        
        // トグルが設定されていない場合は子から自動取得
        if (toggleAggressiveYes == null || toggleAggressiveNo == null)
        {
            Toggle[] toggles = GetComponentsInChildren<Toggle>();
            if (toggles.Length >= 2)
            {
                toggleAggressiveYes = toggles[0];
                toggleAggressiveNo = toggles[1];
            }
            else
            {
                Debug.LogError($"ToggleGroupController: トグルが不足しています（見つかった数: {toggles.Length}）");
                return;
            }
        }
    }
    
    // コールバックを設定
    public void AddListener(UnityAction<int> callback)
    {
        if (callback == null) return;
        
        onToggleSelected = callback;
        
        // リスナー登録前にクリア
        if (toggleAggressiveYes != null)
        {
            toggleAggressiveYes.onValueChanged.RemoveAllListeners();
            toggleAggressiveYes.onValueChanged.AddListener((isOn) => {
                if (isOn && onToggleSelected != null)
                    onToggleSelected(0); // 0 = 前のめりする
            });
        }
        
        if (toggleAggressiveNo != null)
        {
            toggleAggressiveNo.onValueChanged.RemoveAllListeners();
            toggleAggressiveNo.onValueChanged.AddListener((isOn) => {
                if (isOn && onToggleSelected != null)
                    onToggleSelected(1); // 1 = 前のめりしない
            });
        }
    }

    /// <summary>
    /// 現在のスキルのIsAggressiveCommitに合わせてトグルを更新（リスナーを一時的に無効化）
    /// </summary>
    public void UpdateToggleState(bool isAggressiveCommit)
    {
        // 一時的にリスナーを無効化
        if (toggleAggressiveYes != null && toggleAggressiveNo != null)
        {
            toggleAggressiveYes.onValueChanged.RemoveAllListeners();
            toggleAggressiveNo.onValueChanged.RemoveAllListeners();
            
            // トグルの状態を設定
            toggleAggressiveYes.isOn = isAggressiveCommit;
            toggleAggressiveNo.isOn = !isAggressiveCommit;
            
            // リスナーを再設定
            AddListener(onToggleSelected);
        }
    }
    
    private void OnDestroy()
    {
        // リスナーをクリア
        if (toggleAggressiveYes != null)
            toggleAggressiveYes.onValueChanged.RemoveAllListeners();
            
        if (toggleAggressiveNo != null)
            toggleAggressiveNo.onValueChanged.RemoveAllListeners();
    }
}