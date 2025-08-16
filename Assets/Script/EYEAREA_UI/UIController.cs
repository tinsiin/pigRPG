using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif
/// <summary>
/// 全てのキャラで同じ個人UIを共有するためのコントローラー
/// 例えば特定のキャラならちょっと違うエフェクトの振る舞いをしてほしいなら、継承とかすればいい。
/// </summary>
public class UIController : MonoBehaviour
{
    //GameObject UIObject=> this.gameObject;
    
    /// <summary>
    /// HPバーUI　　敵はスクリプト経由で生成する
    /// </summary>
    public CombinedStatesBar HPBar;

    public Image Icon;
    public UIVerticalBob verticalBob;
    public ArrowGrowAndVanish arrowGrowAndVanish;

    private void Awake()
    {
        Debug.Log($"[UIController.Awake] {name} activeSelf={gameObject.activeSelf}, inHierarchy={gameObject.activeInHierarchy}", this);
    }

    private void OnEnable()
    {
        Debug.Log($"[UIController.OnEnable] {name} activeSelf={gameObject.activeSelf}, inHierarchy={gameObject.activeInHierarchy}", this);
    }

    private void OnDisable()
    {
        Debug.Log($"[UIController.OnDisable] {name} activeSelf={gameObject.activeSelf}, inHierarchy={gameObject.activeInHierarchy}", this);
    }

    public void SetActive(bool isActive)
    {
        Debug.Log($"[UIController.SetActive] {name} -> {isActive}\n{new System.Diagnostics.StackTrace(true)}", this);
#if UNITY_EDITOR
        // Prefabアセットに対して呼び出されていないか検出（アセット本体を無効化してしまう事故を防止）
        if (PrefabUtility.IsPartOfPrefabAsset(gameObject))
        {
            Debug.LogError($"[UIController.SetActive] PREFAB ASSET に対して SetActive が呼ばれました。処理を中断します。 name={name} -> {isActive}\n{new System.Diagnostics.StackTrace(true)}", this);
            return;
        }
#endif
        this.gameObject.SetActive(isActive);
    }

    
    /// <summary>
    /// 前のめり時のアイコンのエフェクト
    /// </summary>
    public void BeVanguardEffect()
    {
        verticalBob.Enabled = true;//大文字で動く　小文字だとMonoBehavior用のプロパティなので注意
        arrowGrowAndVanish.PlayOnceAsync().Forget();
    }
    /// <summary>
    /// 前のめりを失ったとき。
    /// </summary>
    public void LostVanguardEffect()
    {   
        verticalBob.Enabled = false;
    }

}