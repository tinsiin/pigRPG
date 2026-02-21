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
/// バトル中のキャラクターアイコンUI。
/// HPバー、アイコン画像、前衛エフェクト、数字表示などを管理する。
/// 味方・敵問わず全キャラクターで共通のバトル用個人UIコンポーネント。
/// </summary>
public class BattleIconUI : MonoBehaviour, IPointerClickHandler
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
    private IKZoomController _kZoom;

    public void BindUser(BaseStates user)
    {
        _user = user;
    }

    /// <summary>
    /// アイコン画像を設定する
    /// </summary>
    public void SetIconSprite(Sprite sprite)
    {
        if (Icon != null && sprite != null)
        {
            Icon.sprite = sprite;
        }
    }

    /// <summary>
    /// Phase 1: IKZoomControllerを注入
    /// </summary>
    public void BindKZoom(IKZoomController kZoom)
    {
        _kZoom = kZoom;
    }


    // Kモード用: Icon以外の子を一時的に非表示にする際の元状態を保存
    private List<(GameObject go, bool wasActive)> _kSavedChildActives;

    private void Awake()
    {
        Debug.Log($"[BattleIconUI.Awake] {name} activeSelf={gameObject.activeSelf}, inHierarchy={gameObject.activeInHierarchy}", this);
    }

    private void OnEnable()
    {
        Debug.Log($"[BattleIconUI.OnEnable] {name} activeSelf={gameObject.activeSelf}, inHierarchy={gameObject.activeInHierarchy}", this);
    }

    private void OnDisable()
    {
        Debug.Log($"[BattleIconUI.OnDisable] {name} activeSelf={gameObject.activeSelf}, inHierarchy={gameObject.activeInHierarchy}", this);
    }

    public void SetActive(bool isActive)
    {
        Debug.Log($"[BattleIconUI.SetActive] {name} -> {isActive}\n{new System.Diagnostics.StackTrace(true)}", this);
#if UNITY_EDITOR
        // Prefabアセットに対して呼び出されていないか検出（アセット本体を無効化してしまう事故を防止）
        if (PrefabUtility.IsPartOfPrefabAsset(gameObject))
        {
            Debug.LogError($"[BattleIconUI.SetActive] PREFAB ASSET に対して SetActive が呼ばれました。処理を中断します。 name={name} -> {isActive}\n{new System.Diagnostics.StackTrace(true)}", this);
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

        // Phase 1: 注入されたコントローラーを優先、フォールバックでWatchUIUpdate.Instance
        var kZoom = _kZoom ?? WatchUIUpdate.Instance?.KZoomCtrl;

        if (Icon == null)
        {
            Debug.LogWarning($"[K/UI] Icon is NULL on BattleIconUI: {name}", this);
        }

        if (iconRT == null)
        {
            Debug.LogWarning($"[K/UI] iconRT is NULL (Icon not assigned or not a RectTransform) on {name}", this);
            return;
        }
        if (kZoom == null)
        {
            Debug.LogError("[K/UI] IKZoomController is NULL. Scene missing WatchUIUpdate?", this);
            return;
        }

        // K中の再タップ動作（ユーザー要望）
        if (kZoom.IsKActive)
        {
            if (kZoom.IsCurrentKTarget(this))
            {
                if (!kZoom.IsKAnimating)
                {
                    Debug.Log("[K/UI] Re-tap on current K target -> ExitK", this);
                    kZoom.ExitK().Forget();
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

        Debug.Log($"[K/UI] CanEnterK={kZoom.CanEnterK} on {name}", this);
        if (kZoom.CanEnterK)
        {
            // タイトルは暫定でゲームオブジェクト名を使用（将来: BaseStatesの名前に置換）
            var title = "名前: " + _user.CharacterName;
            Debug.Log($"[K/UI] EnterK invoked with iconRT={iconRT.name}, title={title}", this);
            // クリック発火時のみ、Icon以外を即時OFF（アニメなし）
            SetExclusiveIconMode(true);
            kZoom.EnterK(iconRT, title).Forget();
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

    /// <summary>
    /// ダメージフロー数字を生成する。プレハブを受け取り、自身の子としてインスタンス化する。
    /// </summary>
    public void SpawnDamageFlow(int damage, HitResult hitResult, DamageFlowNumber prefab, bool isDisturbed = false)
    {
        if (prefab == null) return;
        var instance = Instantiate(prefab, transform);
        instance.Play(damage, hitResult, isDisturbed);
    }

    /// <summary>
    /// レイザーダメージのフロー数字を生成する。
    /// </summary>
    public void SpawnRatherDamageFlow(int damage, DamageFlowNumber prefab)
    {
        if (prefab == null) return;
        var instance = Instantiate(prefab, transform);
        instance.Play(damage, category: DamageFlowNumber.Category.RatherDamage);
    }

    /// <summary>
    /// 回復のフロー数字を生成する。
    /// </summary>
    public void SpawnHealFlow(int amount, DamageFlowNumber prefab)
    {
        if (prefab == null) return;
        var instance = Instantiate(prefab, transform);
        instance.Play(amount, category: DamageFlowNumber.Category.Heal);
    }

    /// <summary>
    /// 被弾時の点滅エフェクト（fire-and-forget）。
    /// Icon の alpha を 0↔1 にトグルして点滅させる。
    /// </summary>
    public async UniTaskVoid PlayDamageBlink(float duration = 0.5f, int count = 4)
    {
        if (Icon == null) return;

        float halfCycle = duration / (count * 2f);
        int halfCycleMs = Mathf.Max(1, (int)(halfCycle * 1000f));
        var originalColor = Icon.color;

        try
        {
            for (int i = 0; i < count; i++)
            {
                if (Icon == null) return;
                Icon.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0f);
                await UniTask.Delay(halfCycleMs);

                if (Icon == null) return;
                Icon.color = new Color(originalColor.r, originalColor.g, originalColor.b, 1f);
                await UniTask.Delay(halfCycleMs);
            }
        }
        finally
        {
            if (Icon != null)
                Icon.color = new Color(originalColor.r, originalColor.g, originalColor.b, 1f);
        }
    }

}