using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// TMPの実際に描画されている文字領域(textBounds)にだけ、背面に画像を敷くコンポーネント。
/// - このコンポーネントは「親コンテナ」にアタッチしてください。
/// - 対象のTMP(TextMeshProUGUI)は親コンテナの直下の子として配置します（必須）。
/// - OnEnableで一度だけ位置・サイズを更新します（要求仕様）。
/// - 背景は常に「親コンテナ直下」に生成し、子TMPの直前（背面）に配置します。
/// 注意: TMP側のRectTransformの回転・スケールがデフォルト(回転0, スケール1)前提での配置を想定しています。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class TMPTextBackgroundImage : MonoBehaviour
{
    [Header("見た目")]
    [SerializeField] private Sprite m_Sprite;
    [SerializeField] private Color m_Color = new Color(0f, 0f, 0f, 0.25f);
    [SerializeField] private Vector2 m_Padding = new Vector2(8f, 4f);

    [Header("挙動")]
    [Tooltip("テキストが空のときは背景を非表示にします")]
    [SerializeField] private bool m_AutoHideWhenEmpty = true;

    [Tooltip("生成する背景オブジェクト名の接頭辞")]
    [SerializeField] private string m_BackgroundNamePrefix = "_TMP_BG_Image";

    [Tooltip("ランタイム時のみ背景オブジェクトを新規生成します（推奨）。エディタでは既存があれば更新のみ行います。")]
    [SerializeField] private bool m_GenerateAtRuntimeOnly = true;

    [Header("ターゲット設定（親直下の子TMP）")]
    [Tooltip("背景を敷く対象のTMP。未指定時は、このコンテナ直下の子から唯一のTMP_Textを自動検出します。複数ある場合は明示的に指定してください。")]
    [SerializeField] private TMP_Text m_TargetTMP;

    private TMP_Text _tmp;
    private RectTransform _tmpRect;
    private Image _bgImage;
    private RectTransform _bgRect;

    // キャッシュと更新制御
    private Vector3 _cachedLocalCenter;   // テキストローカル空間でのbounds.center
    private Vector2 _cachedBgSize;        // 背景サイズ（bounds.size + padding）
    private bool _boundsDirty = true;     // 文字内容やPadding変更時にのみtrue
    private bool _pendingInitialRefresh;  // 初回LateUpdateでフル反映するためのフラグ

    /// <summary>
    /// 外部からテキストを変更するapi
    /// </summary>
    public string text
    {
        get
        {
            if (_tmp != null) return _tmp.text;
            if (m_TargetTMP != null) return m_TargetTMP.text;
            var auto = FindDirectChildTMPOrNull(out _);
            return auto != null ? auto.text : string.Empty;
        }
        set
        {
            if (_tmp == null || _tmpRect == null)
            {
                EnsureReferences();
            }
            if (_tmp != null)
            {
                _tmp.text = value;
                RefreshBackground();
            }
            else
            {
                Debug.LogWarning($"{nameof(TMPTextBackgroundImage)}: text setter called but TMP is not ready. Assign m_TargetTMP or ensure a TMP child exists.", this);
            }
        }
    }
    public RectTransform rectTransform => _tmpRect;

    private void OnEnable()
    {
        EnsureReferences();
        SubscribeTMPEvent();
        _pendingInitialRefresh = true; // 初回はレイアウト後のLateUpdateで更新
    }

    private void OnDisable()
    {
        UnsubscribeTMPEvent();
    }

    private void Reset()
    {
        // インスペクタにデフォルトで分かりやすい色を入れておく
        m_Color = new Color(0f, 0f, 0f, 0.25f);
        m_Padding = new Vector2(8f, 4f);
    }

    private void EnsureReferences()
    {
        // 対象TMPの決定（未指定時は直下の子から唯一のTMP_Textを自動検出）
        if (_tmp == null)
        {
            if (m_TargetTMP != null)
            {
                _tmp = m_TargetTMP;
            }
            else
            {
                _tmp = FindDirectChildTMPOrNull(out int count);
                if (_tmp == null)
                {
                    if (count == 0)
                        Debug.LogWarning($"{nameof(TMPTextBackgroundImage)}: No TMP_Text found as a direct child of this container. Assign m_TargetTMP or add a TMP child under '{name}'.", this);
                    else
                        Debug.LogWarning($"{nameof(TMPTextBackgroundImage)}: Multiple TMP_Text found as direct children. Please assign m_TargetTMP explicitly.", this);
                    return;
                }
            }
        }
        if (_tmpRect == null && _tmp != null)
        {
            _tmpRect = _tmp.GetComponent<RectTransform>();
        }

        if (_tmp == null || _tmpRect == null)
        {
            Debug.LogWarning($"{nameof(TMPTextBackgroundImage)}: Target TMP_Text is invalid. Assign m_TargetTMP or ensure a valid TMP child exists.", this);
            return;
        }

        // 親コンテナ直下の子であることを要求
        var container = transform as RectTransform;
        if (_tmpRect.parent != container)
        {
            Debug.LogWarning($"{nameof(TMPTextBackgroundImage)}: Target TMP must be a direct child of this container. Please reparent the TMP under '{name}'.", this);
            return;
        }

        var desiredParent = container;
        if (desiredParent == null) return;

        // 既存の背景を探索
        if (_bgRect == null || _bgImage == null)
        {
            string targetName = BuildBackgroundObjectName();
            var existing = desiredParent.Find(targetName) as RectTransform;
            if (existing != null)
            {
                _bgRect = existing;
                _bgImage = existing.GetComponent<Image>();
                // レイアウトグループから除外
                var le0 = _bgRect.GetComponent<LayoutElement>();
                if (le0 == null) le0 = _bgRect.gameObject.AddComponent<LayoutElement>();
                le0.ignoreLayout = true;
            }
        }

        // 生成タイミングの制御（ランタイム限定）
        if ((_bgRect == null || _bgImage == null))
        {
            if (m_GenerateAtRuntimeOnly && !Application.isPlaying)
            {
                // エディタでは新規生成せず、以降の更新も安全にスキップできるよう参照はnullのまま
                return;
            }

            // 背景を新規作成
            string targetName = BuildBackgroundObjectName();
            var go = new GameObject(targetName, typeof(RectTransform), typeof(Image));
            _bgRect = go.GetComponent<RectTransform>();
            _bgImage = go.GetComponent<Image>();
            _bgRect.SetParent(desiredParent, false);

            // レイアウトグループから除外
            var le = _bgRect.GetComponent<LayoutElement>();
            if (le == null) le = _bgRect.gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
        }

        // 背景Imageの基本設定
        if (_bgImage != null)
        {
            _bgImage.sprite = m_Sprite;
            _bgImage.color = m_Color;
            _bgImage.raycastTarget = false;
        }

        if (_bgRect != null)
        {
            // アンカー・ピボットは中央基準に固定（配置計算を簡易化）
            _bgRect.anchorMin = new Vector2(0.5f, 0.5f);
            _bgRect.anchorMax = new Vector2(0.5f, 0.5f);
            _bgRect.pivot = new Vector2(0.5f, 0.5f);
            _bgRect.localRotation = Quaternion.identity;
            _bgRect.localScale = Vector3.one;
        }
    }

    [ContextMenu("Refresh Background")]
    public void RefreshBackground()
    {
        if (_tmp == null || _tmpRect == null)
        {
            EnsureReferences();
            if (_tmp == null || _tmpRect == null) return;
        }
        // フル更新要求
        _boundsDirty = true;
        _pendingInitialRefresh = true;
        // 即時に一度だけ反映（エディタ/手動用）
        LateUpdate();
    }

    private void LateUpdate()
    {
        if (_tmp == null || _tmpRect == null)
        {
            return;
        }
        if (_bgRect == null || _bgImage == null)
        {
            EnsureReferences();
            if (_bgRect == null || _bgImage == null) return;
        }

        // 文字内容が変わったときのみboundsを更新（重いForceMeshUpdateはここでだけ）
        if (_boundsDirty)
        {
            _tmp.ForceMeshUpdate();
            var b = _tmp.textBounds;
            _cachedLocalCenter = b.center;
            var s = (Vector2)b.size + m_Padding;
            s.x = Mathf.Max(0f, s.x);
            s.y = Mathf.Max(0f, s.y);
            _cachedBgSize = s;

            if (m_AutoHideWhenEmpty)
            {
                bool hasText = _tmp.textInfo != null && _tmp.textInfo.characterCount > 0 && (s.sqrMagnitude > 1e-6f);
                _bgImage.enabled = hasText;
            }
            else
            {
                _bgImage.enabled = true;
            }

            _boundsDirty = false;
        }

        // 親コンテナ基準の座標にスナップ（テキストや親の移動に追従）
        var container = (RectTransform)transform;
        Vector3 worldCenter = _tmpRect.TransformPoint(_cachedLocalCenter);
        Vector2 localInContainer = container.InverseTransformPoint(worldCenter);
        Vector2 anchorRefOffset = new Vector2(
            (0.5f - container.pivot.x) * container.rect.width,
            (0.5f - container.pivot.y) * container.rect.height
        );
        _bgRect.anchoredPosition = localInContainer - anchorRefOffset;

        // サイズは文字内容が変わったときのみ更新
        if (_pendingInitialRefresh)
        {
            _bgRect.sizeDelta = _cachedBgSize;
        }

        // 兄弟順: 常に「TMPの直前 = index-1」に固定してPing-Pongを防止
        int desired = Mathf.Max(0, _tmpRect.GetSiblingIndex() - 1);
        if (_bgRect.GetSiblingIndex() != desired)
        {
            _bgRect.SetSiblingIndex(desired);
        }

        _pendingInitialRefresh = false;
    }

    // --------- 外部からの調整API（任意） ---------
    public void SetSprite(Sprite sprite)
    {
        m_Sprite = sprite;
        if (_bgImage != null) _bgImage.sprite = sprite;
    }

    public void SetColor(Color color)
    {
        m_Color = color;
        if (_bgImage != null) _bgImage.color = color;
    }

    public void SetPadding(Vector2 padding)
    {
        m_Padding = padding;
        _boundsDirty = true;
        if (isActiveAndEnabled) RefreshBackground();
    }

    public GameObject BackgroundGameObject => _bgRect != null ? _bgRect.gameObject : null;

    private string BuildBackgroundObjectName()
    {
        var targetName = _tmp != null ? _tmp.gameObject.name : gameObject.name;
        return m_BackgroundNamePrefix + "_" + targetName;
    }

    private TMP_Text FindDirectChildTMPOrNull(out int count)
    {
        count = 0;
        TMP_Text found = null;
        var container = transform;
        int childCount = container.childCount;
        for (int i = 0; i < childCount; i++)
        {
            var child = container.GetChild(i);
            var tmp = child.GetComponent<TMP_Text>();
            if (tmp != null)
            {
                count++;
                if (found == null) found = tmp;
            }
        }
        return count == 1 ? found : null;
    }

    // --- TMPイベント購読 ---
    private void SubscribeTMPEvent()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(HandleTMPTextChanged);
    }

    private void UnsubscribeTMPEvent()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(HandleTMPTextChanged);
    }

    private void HandleTMPTextChanged(UnityEngine.Object obj)
    {
        if (obj == _tmp)
        {
            _boundsDirty = true;
            _pendingInitialRefresh = true;
        }
    }
    // 親コンテナ方式に統一したため、背景の親は常に transform、兄弟順は常にTMP直前とする
}
