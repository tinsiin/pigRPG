using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BattleSystemArrowManager : MonoBehaviour
{
    [Header("描画コンテナ")]
    [Tooltip("矢印を描画する親の RectTransform（同一 Canvas 配下を推奨）。未設定時は自分の RectTransform を使用します。")]
    [SerializeField] private RectTransform _container; // 矢印を描画する親（同一Canvas配下推奨）

    [Header("プール / 履歴キュー")]
    [Tooltip("履歴として保持・表示するグループ数の上限。超えた分は古い順に破棄されます。")]
    [SerializeField] private int _capacity = 4; // 同時表示/保持するグループ数（古いものは自動で破棄）
    [Tooltip("必須。矢印のPrefab（BattleSystemArrow を含む）を指定してください。未設定だとエラーになり、矢印は生成されません。")]
    [SerializeField] private BattleSystemArrow _arrowPrefab;

    private readonly List<BattleSystemArrow> _pool = new List<BattleSystemArrow>();

    // 矢印要求（オプションの太さパーセントを含む）
    private struct ArrowRequest
    {
        public BaseStates actor;
        public BaseStates target;
        public float? thicknessPercent01; // null の場合はPrefabの太さを維持
        public ArrowRequest(BaseStates a, BaseStates t, float? p)
        {
            actor = a;
            target = t;
            thicknessPercent01 = p;
        }
    }

    private readonly List<List<ArrowRequest>> _groups = new List<List<ArrowRequest>>();
    private List<ArrowRequest> _currentGroup;

    [Header("透明度（履歴フェード）")]
    [Tooltip("最新グループの不透明度（前面）")]
    [Range(0f, 1f)] [SerializeField] private float _alphaFront = 1f;
    [Tooltip("最古グループの不透明度（背面）")]
    [Range(0f, 1f)] [SerializeField] private float _alphaBack = 0.35f;
    [Tooltip("α逓減のカーブ。1=線形、>1で前面寄りに詰まり、<1で背面寄りに詰まる")]
    [SerializeField] private float _alphaExponent = 1f;

    [Header("Prefab運用ガイド: 矢印の見た目（太さ・矢じり長さ/角度・ノイズ等）はPrefabの BattleSystemArrow で設定してください。Manager側では設定しません。")]

    // 色は外部（ステージテーマ）から適用する想定。インスペクタからは設定しません。
    [System.NonSerialized] private Color _colorMain = Color.magenta;
    [System.NonSerialized] private Color _colorSub = Color.magenta;
    
    [Header("デバッグ")]
    [Tooltip("内部状態や配置結果などのログを出力します。")]
    [SerializeField] private bool _debugLog = true;
    public static BattleSystemArrowManager Instance;

    private void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(this);
        }

        EnsureContainer();
        EnsurePool();
        ApplyDefaultVisualToPool();
        // 初回はグループ未開始。Next() で「次のグループへ」→ 次の Enqueue で開始。
        RefreshAll();
    }

    private void Reset()
    {
        EnsureContainer();
    }

    public void SetContainer(RectTransform container)
    {
        _container = container;
        EnsurePoolParents();
        if (_debugLog)
        {
            var cv = _container != null ? _container.GetComponentInParent<Canvas>() : null;
            Debug.Log($"[ArrowMgr] SetContainer -> {_container?.name ?? "null"}, active={_container?.gameObject.activeInHierarchy.ToString() ?? "-"}, canvas={cv?.name ?? "null"}");
        }
        RefreshAll();
    }

    public void Enqueue(BaseStates actor, BaseStates target)
    {
        if (actor == null || target == null) return;
        if (_currentGroup == null)
        {
            StartNewGroupInternal();
        }
        _currentGroup.Add(new ArrowRequest(actor, target, null));
        TrimOldGroups();
        if (_debugLog)
        {
            Debug.Log($"[ArrowMgr] Enqueue: groups={_groups.Count}, currentGroupCount={_currentGroup.Count}");
        }
        RefreshAll();
    }

    /// <summary>
    /// 太さパーセント(0〜1)を同時に指定してキューします。既存APIと互換。
    /// </summary>
    public void Enqueue(BaseStates actor, BaseStates target, float thicknessPercent01)
    {
        if (actor == null || target == null) return;
        if (_currentGroup == null)
        {
            StartNewGroupInternal();
        }
        _currentGroup.Add(new ArrowRequest(actor, target, thicknessPercent01));
        TrimOldGroups();
        if (_debugLog)
        {
            Debug.Log($"[ArrowMgr] Enqueue(p): groups={_groups.Count}, currentGroupCount={_currentGroup.Count}, p={thicknessPercent01}");
        }
        RefreshAll();
    }

    public void Next()
    {
        // 次に登録される最初の Enqueue から新しいグループとして開始する
        _currentGroup = null;
        if (_debugLog)
        {
            Debug.Log("[ArrowMgr] Next(): will start new group on next Enqueue");
        }
        RefreshAll();
    }

    public void ClearQueue()
    {
        _groups.Clear();
        _currentGroup = null;
        if (_debugLog)
        {
            Debug.Log("[ArrowMgr] ClearQueue()");
        }
        RefreshAll();
    }
    

    public void SetColorsForAll(Color? colorMain = null, Color? colorSub = null)
    {
        if (colorMain.HasValue) _colorMain = colorMain.Value;
        if (colorSub.HasValue) _colorSub = colorSub.Value;

        foreach (var a in _pool)
        {
            if (a == null) continue;
            a.SetVisual(null, colorMain, colorSub);
        }
        RefreshAll();
    }

    /// <summary>
    /// ステージテーマ側からの色適用用ショートカット。
    /// 例）起動時やステージ切替時に呼び出して、全プールに反映します。
    /// </summary>
    public void ApplyStageThemeColors(Color main, Color sub)
    {
        SetColorsForAll(main, sub);
    }

    public void RebuildAll()
    {
        RefreshAll();
    }

    private void OnValidate()
    {
        // 値の妥当化
        _alphaFront = Mathf.Clamp01(_alphaFront);
        _alphaBack = Mathf.Clamp01(_alphaBack);
        _alphaExponent = Mathf.Max(0.0001f, _alphaExponent);
        _capacity = Mathf.Max(1, _capacity);

#if UNITY_EDITOR
        if (_arrowPrefab == null)
        {
            Debug.LogWarning("[ArrowMgr] _arrowPrefab が未設定です。Prefab必須です。BattleSystemArrow を含む Prefab を割り当ててください。");
        }
        else
        {
            var go = _arrowPrefab.gameObject;
            var assetType = UnityEditor.PrefabUtility.GetPrefabAssetType(go);
            if (assetType == UnityEditor.PrefabAssetType.NotAPrefab)
            {
                Debug.LogWarning("[ArrowMgr] _arrowPrefab はシーン内オブジェクトが割り当てられています。Prefabアセットを割り当ててください。");
            }
        }
#endif
    }

#if UNITY_EDITOR
    [ContextMenu("Arrow/Refresh All")]
    private void EditorRefreshAllMenu()
    {
        RefreshAll();
        Debug.Log("[ArrowMgr] RefreshAll via ContextMenu");
    }

    [ContextMenu("Arrow/Rebuild All")]
    private void EditorRebuildAllMenu()
    {
        RebuildAll();
        Debug.Log("[ArrowMgr] RebuildAll via ContextMenu");
    }

    [ContextMenu("Arrow/Validate Setup")]
    private void EditorValidateMenu()
    {
        OnValidate();
        Debug.Log("[ArrowMgr] Validate Setup via ContextMenu");
    }
#endif

    private void EnsureContainer()
    {
        if (_container == null)
        {
            _container = GetComponent<RectTransform>();
        }
    }

    private void EnsurePool()
    {
        // 親の整合性のみ確認（先行生成はしない）
        EnsurePoolParents();
    }

    private void EnsurePoolParents()
    {
        if (_container == null) return;
        foreach (var a in _pool)
        {
            if (a == null) continue;
            if (a.transform.parent != _container)
            {
                a.transform.SetParent(_container, false);
            }
            StretchToFill(a.GetComponent<RectTransform>());
        }
    }

    private void ApplyDefaultVisualToPool()
    {
        foreach (var a in _pool)
        {
            if (a == null) continue;
            // Prefab側の設定を尊重。ここではデバッグのみ適用。
            a.SetDebug(_debugLog);
        }
    }

    private void RefreshAll()
    {
        EnsureContainer();
        EnsurePool();

        // 空でないグループのみ可視対象とする（古い→新しい順）
        var nonEmpty = new List<List<ArrowRequest>>();
        foreach (var g in _groups)
        {
            if (g != null && g.Count > 0) nonEmpty.Add(g);
        }
        // 表示対象のグループ範囲（最新側_capacity件）
        int visibleGroups = Mathf.Min(nonEmpty.Count, _capacity);
        int startGroup = Mathf.Max(0, nonEmpty.Count - visibleGroups);

        if (_debugLog)
        {
            var cv = _container != null ? _container.GetComponentInParent<Canvas>() : null;
            Debug.Log($"[ArrowMgr] RefreshAll: groups(total={_groups.Count}, nonEmpty={nonEmpty.Count}), visibleGroups={visibleGroups}, startGroup={startGroup}, container={_container?.name ?? "null"}, active={_container?.gameObject.activeInHierarchy.ToString() ?? "-"}, canvas={cv?.name ?? "null"}");
        }

        // 必要な矢印本数
        int needed = 0;
        for (int gi = 0; gi < visibleGroups; gi++)
        {
            var g = nonEmpty[startGroup + gi];
            if (g != null) needed += g.Count;
        }

        if (_debugLog)
        {
            Debug.Log($"[ArrowMgr] needed={needed}, pool(before)={_pool.Count}");
        }
        EnsurePoolSize(needed);
        if (_debugLog)
        {
            Debug.Log($"[ArrowMgr] pool(after)={_pool.Count}");
        }

        // 配置
        int used = 0; // 使用した矢印本数=Sibling順
        for (int gi = 0; gi < visibleGroups; gi++)
        {
            var g = nonEmpty[startGroup + gi];
            if (g == null) continue;
            float alpha = EvalAlpha(gi, visibleGroups);

            for (int k = 0; k < g.Count; k++)
            {
                if (used >= _pool.Count) break;
                var req = g[k];
                var actor = req.actor;
                var target = req.target;
                if (actor == null || target == null) continue;

                var arrow = _pool[used];
                if (arrow == null) continue;
                arrow.gameObject.SetActive(true);
                arrow.BuildFromBaseStates(actor, target, _container);
                arrow.SetAlpha(alpha);
                if (req.thicknessPercent01.HasValue)
                {
                    arrow.SetThicknessFromPercent(req.thicknessPercent01.Value);
                }
                arrow.transform.SetSiblingIndex(used);
                used++;
            }
        }

        if (_debugLog)
        {
            Debug.Log($"[ArrowMgr] placed used={used}, hidden={_pool.Count - used}");
        }

        // 余剰は非表示
        for (int i = used; i < _pool.Count; i++)
        {
            var arrow = _pool[i];
            if (arrow == null) continue;
            arrow.Clear();
            arrow.SetAlpha(0f);
            arrow.gameObject.SetActive(false);
            arrow.transform.SetSiblingIndex(i);
        }
    }

    private float EvalAlpha(int index, int count)
    {
        if (count <= 1) return _alphaFront;
        float t = (float)index / (float)(count - 1); // 0:最古(背面) -> 1:最新(前面)
        float shaped = _alphaExponent <= 0f ? t : Mathf.Pow(t, _alphaExponent);
        return Mathf.Lerp(_alphaBack, _alphaFront, shaped);
    }

    private void StartNewGroupInternal()
    {
        _currentGroup = new List<ArrowRequest>();
        _groups.Add(_currentGroup);
        TrimOldGroups();
    }

    private void TrimOldGroups()
    {
        while (_groups.Count > _capacity)
        {
            _groups.RemoveAt(0);
        }
    }

    // 必要な本数までプールを拡張
    private void EnsurePoolSize(int needed)
    {
        if (needed <= _pool.Count) return;
        Transform parent = _container != null ? (Transform)_container : transform;
        if (_arrowPrefab == null)
        {
            Debug.LogError("[ArrowMgr] 矢印Prefab(_arrowPrefab)が未設定です。Prefabを指定してください。未設定のため矢印を生成できません。");
            return;
        }
        for (int i = _pool.Count; i < needed; i++)
        {
            BattleSystemArrow inst;
            // Prefab必須
            inst = Instantiate(_arrowPrefab, parent);

            var rt = inst.GetComponent<RectTransform>();
            StretchToFill(rt);

            // ここで現在のテーマ色を適用する
            inst.SetVisual(null, _colorMain, _colorSub);

            // Prefab設定を尊重。色は外部適用を想定。ここではデバッグのみ適用。
            inst.SetDebug(_debugLog);
            inst.SetAlpha(0f);
            inst.gameObject.SetActive(false);
            _pool.Add(inst);

            if (_debugLog)
            {
                Debug.Log($"[ArrowMgr] pooled new arrow index={i}, name={inst.name}, parent={(inst.transform.parent != null ? inst.transform.parent.name : "null")}");
            }
        }
        EnsurePoolParents();
    }

    private static void StretchToFill(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
    }

    private void D(string msg)
    {
        if (_debugLog) Debug.Log($"[ArrowMgr] {msg}");
    }

    public void SetDebug(bool on)
    {
        _debugLog = on;
        foreach (var a in _pool)
        {
            if (a != null) a.SetDebug(on);
        }
    }
}
