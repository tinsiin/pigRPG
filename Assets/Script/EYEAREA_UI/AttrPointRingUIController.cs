using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// アイコン周囲のドーナツ領域で属性オーブを管理するUIコントローラ。
/// - BaseStates.OnAttrPChanged を購読し、生成/更新/破棄を行う
/// - ドーナツ漂遊は各オーブの DonutWanderUI2D が担当
/// </summary>
public class AttrPointRingUIController : MonoBehaviour
{
    // オーナー(BaseStates)毎にコントローラを引ける静的レジストリ。
    // StatesBanner 側から属性オーブの色を引く用途で使用する。
    private static readonly System.Collections.Generic.Dictionary<BaseStates, AttrPointRingUIController> s_registry = new();

    [Header("References")]
    [SerializeField] RectTransform m_RingContainer; // 未設定なら Initialize 時に自動生成（Icon の子）
    [SerializeField] Sprite m_DefaultOrbSprite;     // 未設定なら Builtin UISprite を使用
    [SerializeField] string m_FallbackSpriteResourcePath = "UI/OrbDefault"; // Resources からのフォールバック読み込み用

    [Header("Ring Radius (Icon幅基準)")]
    [SerializeField, Min(0f)] float m_InnerRadiusRatio = 0.7f;
    [SerializeField, Min(0f)] float m_OuterRadiusRatio = 1.25f;

    [Header("Orb Size (Icon幅基準)")]
    [SerializeField, Min(0f)] float m_MinSizeRatio = 0.18f;
    [SerializeField, Min(0f)] float m_MaxSizeRatio = 0.45f;

    [Header("Color Settings")]
    [SerializeField, Range(0f, 1f)] float m_MinColorDistance = 0.22f; // HSV距離のしきい値

    [Header("Wander Params")]
    [SerializeField] float m_MoveSpeed = 40f;           // px/sec 目安
    [SerializeField] float m_NoiseAmplitude = 10f;      // px
    [SerializeField] float m_NoiseFrequency = 0.35f;    // Hz相当（時間スケール）
    [SerializeField] float m_RepelRadius = 48f;         // px
    [SerializeField] float m_RepelStrength = 1600f;     // 力係数
    [SerializeField] float m_ConfinementStrength = 80f; // ドーナツ範囲への復帰強度

    [Header("Spawn/Despawn Animation (Scale)")]
    [SerializeField] float m_SpawnDuration = 0.25f;
    [SerializeField] float m_DespawnDuration = 0.18f;

    BaseStates _owner;
    RectTransform _iconRect;

    readonly Dictionary<SpiritualProperty, AttrOrbUI> _orbs = new();
    readonly List<DonutWanderUI2D> _wanders = new();

    public IReadOnlyList<DonutWanderUI2D> ActiveWanders => _wanders;

    void OnDisable()
    {
        if (_owner != null)
        {
            _owner.OnAttrPChanged -= HandleAttrPChanged;
        }
        // レジストリから除去（Disable 時点ではまだ再有効化の可能性があるが、
        // StatesBanner 側は Bind タイミング更新のため実害はない）
        if (_owner != null && s_registry.TryGetValue(_owner, out var cur) && cur == this)
        {
            s_registry.Remove(_owner);
        }
    }

    void OnDestroy()
    {
        if (_owner != null)
        {
            _owner.OnAttrPChanged -= HandleAttrPChanged;
        }
        if (_owner != null && s_registry.TryGetValue(_owner, out var cur) && cur == this)
        {
            s_registry.Remove(_owner);
        }
    }

    /// <summary>
    /// 外部から初期化される（BaseStates.BindUIController から呼ばれる想定）。
    /// </summary>
    public void Initialize(BaseStates owner, RectTransform iconRect)
    {
        _owner = owner;
        _iconRect = iconRect;

        if (_owner == null || _iconRect == null) return;

        // レジストリ登録（同一オーナーに対しては上書き）
        s_registry[_owner] = this;

        // リングコンテナ未設定なら自動生成（Icon の"親"= UIController 配下、中心は Icon を基準に初期化時のみ合わせる）
        if (m_RingContainer == null)
        {
            var go = new GameObject("AttrPointRing", typeof(RectTransform));
            m_RingContainer = go.GetComponent<RectTransform>();
            // 親は Icon の親（= UIController の RectTransform を想定）
            var parentRect = _iconRect.transform.parent as RectTransform;
            if (parentRect == null) parentRect = _iconRect; // フォールバック
            m_RingContainer.SetParent(parentRect, false);

            // アンカー/ピボット/座標を Icon と一致させる（初期位置のみ参照。以降は追従しない）
            m_RingContainer.anchorMin = _iconRect.anchorMin;
            m_RingContainer.anchorMax = _iconRect.anchorMax;
            m_RingContainer.pivot = _iconRect.pivot;
            m_RingContainer.anchoredPosition = _iconRect.anchoredPosition;
            m_RingContainer.sizeDelta = Vector2.zero; // サイズ不要（中心だけ合っていれば良い）
            m_RingContainer.name = "AttrPointRing";
        }

        // 再購読（重複防止）
        _owner.OnAttrPChanged -= HandleAttrPChanged;
        _owner.OnAttrPChanged += HandleAttrPChanged;

        // 既存量から再構築
        RebuildFromSnapshot();
    }

    void RebuildFromSnapshot()
    {
        if (_owner == null) return;
        var snap = _owner.GetAttrPSnapshot();
        var existing = new HashSet<SpiritualProperty>(_orbs.Keys);
        // 更新/生成
        foreach (var entry in snap)
        {
            if (entry.Amount > 0)
            {
                EnsureOrb(entry.Attr, entry.Amount);
            }
        }
        // 消滅（スナップにない/0になっている）
        foreach (var attr in existing)
        {
            var now = _owner.GetAttrP(attr);
            if (now <= 0) RemoveOrb(attr).Forget();
        }
    }

    void HandleAttrPChanged(SpiritualProperty attr, int amount)
    {
        if (amount > 0)
        {
            EnsureOrb(attr, amount);
        }
        else
        {
            RemoveOrb(attr).Forget();
        }
    }

    void EnsureOrb(SpiritualProperty attr, int amount)
    {
        if (!_orbs.TryGetValue(attr, out var orb))
        {
            orb = CreateOrb(attr);
            _orbs[attr] = orb;
        }
        // サイズ更新
        var px = CalcOrbSizePx(amount);
        orb.SetSize(px);
    }

    async UniTaskVoid RemoveOrb(SpiritualProperty attr)
    {
        if (!_orbs.TryGetValue(attr, out var orb)) return;
        _orbs.Remove(attr);
        var wander = orb.GetComponent<DonutWanderUI2D>();
        if (wander != null) _wanders.Remove(wander);
        await orb.PlayDespawnAndDestroyAsync(m_DespawnDuration);
    }

    AttrOrbUI CreateOrb(SpiritualProperty attr)
    {
        var go = new GameObject($"AttrOrb_{attr}");
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(m_RingContainer, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;

        var img = go.AddComponent<Image>();
        Sprite sprite = m_DefaultOrbSprite;
        if (sprite == null && !string.IsNullOrEmpty(m_FallbackSpriteResourcePath))
        {
            sprite = Resources.Load<Sprite>(m_FallbackSpriteResourcePath);
            if (sprite == null)
            {
                Debug.LogWarning($"AttrPointRingUIController: Resources.Load failed for '{m_FallbackSpriteResourcePath}'. Using procedural white sprite.");
            }
        }
        if (sprite == null)
        {
            var tex = Texture2D.whiteTexture;
            sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }
        img.sprite = sprite;
        img.raycastTarget = false;
        img.type = Image.Type.Simple;
        img.preserveAspect = true;

        var orb = go.AddComponent<AttrOrbUI>();
        var color = GenerateDistinctColor();
        orb.SetImage(img);
        orb.SetColor(color);
        orb.SetSize(CalcOrbSizePx(_owner.GetAttrP(attr)));
        orb.PlaySpawn(m_SpawnDuration);

        var wander = go.AddComponent<DonutWanderUI2D>();
        wander.Initialize(this);
        // パラメータ適用
        wander.SetParams(new DonutWanderUI2D.Params
        {
            innerRadius = GetInnerRadiusPx(),
            outerRadius = GetOuterRadiusPx(),
            moveSpeed = m_MoveSpeed,
            noiseAmplitude = m_NoiseAmplitude,
            noiseFrequency = m_NoiseFrequency,
            repelRadius = m_RepelRadius,
            repelStrength = m_RepelStrength,
            confinementStrength = m_ConfinementStrength,
        });
        _wanders.Add(wander);

        // 初期位置をランダムに
        var angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        var rad = UnityEngine.Random.Range(GetInnerRadiusPx(), GetOuterRadiusPx());
        rt.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * rad;

        return orb;
    }

    // ========= 追加: 色取得用API =========
    /// <summary>
    /// 指定オーナーのリングコントローラを取得（なければ null）。
    /// </summary>
    public static AttrPointRingUIController TryGet(BaseStates owner)
    {
        if (owner == null) return null;
        s_registry.TryGetValue(owner, out var c);
        return c;
    }

    /// <summary>
    /// このコントローラが保持するオーブ色のスナップショットを返す。
    /// </summary>
    public System.Collections.Generic.Dictionary<SpiritualProperty, Color> GetColorMapSnapshot()
    {
        var dict = new System.Collections.Generic.Dictionary<SpiritualProperty, Color>();
        foreach (var kv in _orbs)
        {
            dict[kv.Key] = kv.Value != null ? kv.Value.Color : Color.white;
        }
        return dict;
    }

    /// <summary>
    /// 単一属性の色を取得。
    /// </summary>
    public bool TryGetColor(SpiritualProperty attr, out Color color)
    {
        if (_orbs.TryGetValue(attr, out var orb) && orb != null)
        {
            color = orb.Color;
            return true;
        }
        color = default;
        return false;
    }

    /// <summary>
    /// 静的経由で色取得（StatesBanner などから利用）。
    /// </summary>
    public static bool TryGetOrbColor(BaseStates owner, SpiritualProperty attr, out Color color)
    {
        color = default;
        var c = TryGet(owner);
        if (c == null) return false;
        return c.TryGetColor(attr, out color);
    }

    float GetIconWidth()
    {
        if (_iconRect == null) return 100f;
        var w = _iconRect.rect.width;
        if (w <= 0f) w = _iconRect.sizeDelta.x;
        if (w <= 0f) w = 100f;
        return w;
    }

    float GetInnerRadiusPx() => GetIconWidth() * m_InnerRadiusRatio;
    float GetOuterRadiusPx() => GetIconWidth() * m_OuterRadiusRatio;

    float CalcOrbSizePx(int amount)
    {
        var iconW = GetIconWidth();
        var max = Mathf.Max(1, _owner?.CombinedAttrPMax ?? 1);
        var ratio = Mathf.Clamp01(amount / (float)max);
        var sizeRatio = Mathf.Lerp(m_MinSizeRatio, m_MaxSizeRatio, ratio);
        return Mathf.Max(1f, iconW * sizeRatio);
    }

    Color GenerateDistinctColor()
    {
        // 既存色との距離を確保する
        var existing = _orbs.Values.Select(o => o.Color).ToList();
        if (existing.Count == 0)
        {
            Color.RGBToHSV(UnityEngine.Random.ColorHSV(), out var h0, out var s0, out var v0);
            return Color.HSVToRGB(h0, Mathf.Lerp(0.7f, 0.95f, 1f), Mathf.Lerp(0.9f, 1f, 1f));
        }

        float golden = 0.6180339887498948f; // 1/phi
        float seed = UnityEngine.Random.value;
        Color best = Color.white;
        float bestScore = -1f;
        for (int k = 0; k < 16; k++)
        {
            float h = (seed + k * golden) % 1f;
            float s = UnityEngine.Random.Range(0.65f, 0.95f);
            float v = UnityEngine.Random.Range(0.88f, 1.0f);
            var cand = Color.HSVToRGB(h, s, v);
            float d = MinColorDistanceHSV(cand, existing);
            if (d >= m_MinColorDistance)
            {
                return cand; // しきい値を満たしたら即採用
            }
            if (d > bestScore)
            {
                bestScore = d; best = cand;
            }
        }
        return best; // 最良候補
    }

    float MinColorDistanceHSV(Color c, List<Color> set)
    {
        Color.RGBToHSV(c, out var h, out var s, out var v);
        float mind = float.MaxValue;
        foreach (var e in set)
        {
            Color.RGBToHSV(e, out var he, out var se, out var ve);
            float dh = Mathf.Min(Mathf.Abs(h - he), 1f - Mathf.Abs(h - he)); // 環状距離
            float ds = Mathf.Abs(s - se);
            float dv = Mathf.Abs(v - ve);
            float d = dh * 2.0f + ds * 1.0f + dv * 1.0f; // 重み付き
            if (d < mind) mind = d;
        }
        return mind;
    }
}
