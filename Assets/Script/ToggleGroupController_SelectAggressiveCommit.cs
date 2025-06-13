using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections.Generic;

/// <summary>
/// 汎用的なトグルボタンのグループ
/// </summary>
public class ToggleGroupController : MonoBehaviour
{
    /// <summary>
    /// トグルグループ内のトグル達。
    /// </summary>
    [SerializeField] List<Toggle> toggles = new(); 
    
    // コールバック
    private UnityAction<int> onToggleSelected;
    
    // 有効/無効設定
    public bool interactable
    {
        get { return toggles[0].interactable;}//最低一個あるはずだしそれでラジオボタングループが有効/無効の判断すればいい
        set 
        {
            foreach(var toggle in toggles)
            {
                toggle.interactable = value;
            }
        }
    }
    
    private void Start()
    {
        
        // トグルが設定されていない場合は子から自動取得
         if (toggles.Count == 0)
            toggles.AddRange(GetComponentsInChildren<Toggle>());
    }
    
    // コールバックを設定
    public void AddListener(UnityAction<int> callback)
    {
        if (callback == null) return;
        
        onToggleSelected = callback;
        
        
        for (int i = 0; i < toggles.Count; i++)
        {
            int idx = i;                           // クロージャ用ローカル変数
            toggles[i].onValueChanged.RemoveAllListeners();// リスナー登録前にクリア
            toggles[i].onValueChanged.AddListener(isOn =>
            {
                if (isOn && onToggleSelected != null) onToggleSelected(idx);
            });
        }
    }

    /// <summary>
    /// 外部からトグルを更新する
    /// 数字を渡すとそのインデックスのトグルだけがオンになる。
    /// 全てオフ(未選択状態)にするには「-1」を渡す
    /// </summary>
    public void SetOnWithoutNotify(int index)      // 外部から初期状態を合わせる用
    {
        // -1 なら全て OFF、通常は指定 index だけ ON
        if (index == -1)
        {
            foreach (var t in toggles) t.SetIsOnWithoutNotify(false);
            return;
        }

        if (index < 0 || index >= toggles.Count) return;

        foreach (var t in toggles) t.SetIsOnWithoutNotify(false);
        toggles[index].SetIsOnWithoutNotify(true);
    }

    
    private void OnDestroy()
    {
        // リスナーをクリア
        foreach (var t in toggles) t.onValueChanged.RemoveAllListeners();
    }
}