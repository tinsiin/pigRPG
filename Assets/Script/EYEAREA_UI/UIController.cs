using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif
/// <summary>
/// 全てのキャラで同じ個人UIを共有するためのコントローラー
/// 例えば特定のキャラならちょっと違うエフェクトの振る舞いをしてほしいなら、継承とかすればいい。
/// </summary>
public class UIController : MonoBehaviour, IPointerClickHandler
{
    //GameObject UIObject=> this.gameObject;
    
    /// <summary>
    /// HPバーUI　　敵はスクリプト経由で生成する
    /// </summary>
    public CombinedStatesBar HPBar;

    public Image Icon;
    public UIVerticalBob verticalBob;
    public ArrowGrowAndVanish arrowGrowAndVanish;
    public IconButtonLinkNumberEffect numberEffect;

    private BaseStates _user;

    public void BindUser(BaseStates user)
    {
        _user = user;
    }


    // Kモード用: Icon以外の子を一時的に非表示にする際の元状態を保存
    private List<(GameObject go, bool wasActive)> _kSavedChildActives;

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
    /// 指標用数字エフェクトのsetactiveと数字指定。
    /// </summary>
    /// <param name="isActive"></param>
    /// <param name="number"></param>
    public void SetActiveSetNumber_NumberEffect(bool isActive,int number = 0)
    {
        numberEffect.SetActive(isActive);
        numberEffect.Number = number;
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

    /// <summary>
    /// UIの初期化処理
    /// サイズ自動調整とか
    /// </summary>
    public void Init()
    {
        arrowGrowAndVanish.InitializeArrowByIcon();//矢印画像の初期化
        numberEffect.InitializeByIcon();//数字エフェクトの初期化
        SetActiveSetNumber_NumberEffect(false);//数字エフェクトを非表示
    }

    /// <summary>
    /// アイコン（このUI全体）クリック時: Kモード起動
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log($"[K/UI] OnPointerClick received: go={name}, button={eventData.button}, pos={eventData.position}", this);
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            Debug.Log($"[K/UI] Ignore non-left click on {name}", this);
            return;
        }
        TriggerKMode();
    }


    /// <summary>
    /// Kモード専用: Iconを含む枝以外の直下子を全て非表示にし、復元時に元のActiveへ戻す。
    /// </summary>
    public void SetExclusiveIconMode(bool enable)
    {
        var iconTr = Icon != null ? Icon.transform : null;
        if (iconTr == null)
        {
            Debug.LogWarning("[K/UI] SetExclusiveIconMode called but Icon is null.", this);
            return;
        }

        if (enable)
        {
            if (_kSavedChildActives != null)
            {
                // すでに有効化済み
                Debug.Log("[K/UI] SetExclusiveIconMode already enabled, skip.", this);
                return;
            }

            _kSavedChildActives = new List<(GameObject go, bool wasActive)>();

            // Icon までの祖先チェーン + Icon の全子孫を保持対象として収集
            var keep = new HashSet<Transform>();
            // 祖先チェーン（root=this.transform まで）
            Transform p = iconTr;
            while (p != null)
            {
                keep.Add(p);
                if (p == transform) break;
                p = p.parent;
            }
            // Icon の全子孫
            var iconStack = new Stack<Transform>();
            iconStack.Push(iconTr);
            while (iconStack.Count > 0)
            {
                var cur = iconStack.Pop();
                foreach (Transform ch in cur)
                {
                    keep.Add(ch);
                    iconStack.Push(ch);
                }
            }

            int hiddenCount = 0;
            // ルート直下から走査し、保持対象以外の枝をすべて非表示
            var stack = new Stack<Transform>();
            stack.Push(transform);
            while (stack.Count > 0)
            {
                var cur = stack.Pop();
                foreach (Transform child in cur)
                {
                    if (keep.Contains(child))
                    {
                        // Iconへの経路上のノードは残し、さらに下を検査
                        stack.Push(child);
                    }
                    else
                    {
                        var go = child.gameObject;
                        _kSavedChildActives.Add((go, go.activeSelf));
                        go.SetActive(false);
                        hiddenCount++;
                    }
                }
            }
            Debug.Log($"[K/UI] ExclusiveIconMode ENABLED on {name}. keptPathTop={transform.name}, icon={iconTr.name}, hidden={hiddenCount}", this);
        }
        else
        {
            if (_kSavedChildActives == null)
                return; // 何も保存していない

            foreach (var pair in _kSavedChildActives)
            {
                if (pair.go != null)
                {
                    pair.go.SetActive(pair.wasActive);
                }
            }
            _kSavedChildActives = null;
            Debug.Log($"[K/UI] ExclusiveIconMode DISABLED on {name}. restored children.", this);
        }
    }

    /// <summary>
    /// Kモード起動の公開メソッド（Button.onClick からも呼べる）
    /// </summary>
    public void TriggerKMode()
    {
        Debug.Log($"[K/UI] TriggerKMode called on {name}", this);
        var iconRT = Icon != null ? Icon.transform as RectTransform : null;
        var wui = WatchUIUpdate.Instance;

        if (Icon == null)
        {
            Debug.LogWarning($"[K/UI] Icon is NULL on UIController: {name}", this);
        }

        if (iconRT == null)
        {
            Debug.LogWarning($"[K/UI] iconRT is NULL (Icon not assigned or not a RectTransform) on {name}", this);
            return;
        }
        if (wui == null)
        {
            Debug.LogError("[K/UI] WatchUIUpdate.Instance is NULL. Scene missing WatchUIUpdate?", this);
            return;
        }

        // K中の再タップ動作（ユーザー要望）
        if (wui.IsKActive)
        {
            if (wui.IsCurrentKTarget(this))
            {
                if (!wui.IsKAnimating)
                {
                    Debug.Log("[K/UI] Re-tap on current K target -> ExitK", this);
                    wui.ExitK().Forget();
                }
                else
                {
                    Debug.Log("[K/UI] Ignore tap during K animating on current target", this);
                }
            }
            else
            {
                // 質問1: A（K中に他アイコンをタップした時は無視）
                Debug.Log("[K/UI] Ignore tap on other icon while K is active", this);
            }
            return;
        }

        Debug.Log($"[K/UI] CanEnterK={wui.CanEnterK} on {name}", this);
        if (wui.CanEnterK)
        {
            // タイトルは暫定でゲームオブジェクト名を使用（将来: BaseStatesの名前に置換）
            var title = "名前: " + _user.CharacterName;
            Debug.Log($"[K/UI] EnterK invoked with iconRT={iconRT.name}, title={title}", this);
            // クリック発火時のみ、Icon以外を即時OFF（アニメなし）
            SetExclusiveIconMode(true);
            wui.EnterK(iconRT, title).Forget();
        }
        else
        {
            Debug.LogWarning("[K/UI] CanEnterK=false. Kモードは起動しませんでした。既存ズーム/スライド中やK中の可能性。", this);
        }
    }
    public void TestOnClickLog()
    {
        Debug.Log($"[K/UI] TestOnClickLog called on {name}", this);
    }


}