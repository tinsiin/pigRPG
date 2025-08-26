using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// UI(RectTransform.anchoredPosition) を中心からのドーナツ範囲に拘束しつつ、
/// ふわふわ漂う移動と相互リペルを行う。
/// 単体では動作せず、AttrPointRingUIController から Initialize/SetParams される想定。
/// </summary>
public class DonutWanderUI2D : MonoBehaviour
{
    public struct Params
    {
        public float innerRadius;          // px
        public float outerRadius;          // px
        public float moveSpeed;            // px/sec
        public float noiseAmplitude;       // ノイズ寄与の強度（速度への寄与）
        public float noiseFrequency;       // ノイズ周波数（Hz相当）
        public float repelRadius;          // 相互反発の有効半径（px）
        public float repelStrength;        // 反発の強度（加速度係数）
        public float confinementStrength;  // 外縁/内縁への復帰強度
    }

    AttrPointRingUIController _owner;
    RectTransform _rt;
    Params _p;

    Vector2 _vel;
    float _seedX;
    float _seedY;

    public void Initialize(AttrPointRingUIController owner)
    {
        _owner = owner;
        _rt = GetComponent<RectTransform>();
        _seedX = Random.value * 1000f;
        _seedY = Random.value * 1000f;
        // 初期速度は接線方向へ少し与えておく
        var pos = _rt.anchoredPosition;
        var r = pos.magnitude;
        Vector2 t = r > 0.001f ? new Vector2(-pos.y, pos.x).normalized : Random.insideUnitCircle.normalized;
        _vel = t * Mathf.Max(10f, _p.moveSpeed * 0.25f);
    }

    public void SetParams(Params p)
    {
        _p = p;
    }

    void Update()
    {
        if (_rt == null) return;
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        var pos = _rt.anchoredPosition;
        float r = pos.magnitude;
        Vector2 radial = r > 0.0001f ? (pos / r) : Vector2.right;
        Vector2 tangent = new Vector2(-radial.y, radial.x);

        // 1) 滑らかなノイズベースのドリフト方向
        float t = Time.time;
        float n1 = Mathf.PerlinNoise(_seedX + t * _p.noiseFrequency, _seedY) * 2f - 1f; // [-1,1]
        float n2 = Mathf.PerlinNoise(_seedY, _seedX + t * _p.noiseFrequency) * 2f - 1f; // [-1,1]
        Vector2 noiseDir = new Vector2(n1, n2);
        if (noiseDir.sqrMagnitude < 1e-4f) noiseDir = Random.insideUnitCircle;
        noiseDir.Normalize();

        // 2) 接線方向へやや流す + ノイズ
        Vector2 desiredDir = (tangent * 0.6f + noiseDir * 0.4f).normalized;
        Vector2 desiredVel = desiredDir * Mathf.Max(0.001f, _p.moveSpeed);

        // 3) ステアリング
        float steer = Mathf.Clamp01(2.0f * dt);
        _vel = Vector2.Lerp(_vel, desiredVel, steer);

        // 4) 相互リペル
        if (_owner != null)
        {
            IReadOnlyList<DonutWanderUI2D> peers = _owner.ActiveWanders;
            for (int i = 0; i < peers.Count; i++)
            {
                var w = peers[i];
                if (w == null || w == this) continue;
                var rt2 = w.GetComponent<RectTransform>();
                if (rt2 == null) continue;
                Vector2 dp = pos - rt2.anchoredPosition;
                float d = dp.magnitude;
                if (d <= 0.0001f) continue;
                if (d < _p.repelRadius)
                {
                    float s = (_p.repelStrength / Mathf.Max(12f, d)) * dt; // 近いほど強い
                    _vel += dp.normalized * s;
                }
            }
        }

        // 5) ドーナツ拘束（内側に入りすぎたら外へ、外側に出たら内へ）
        if (r < _p.innerRadius)
        {
            float k = Mathf.InverseLerp(_p.innerRadius, 0f, r); // r=inner->0 で 0->1
            _vel += radial * (_p.confinementStrength * (1f - k)) * dt;
        }
        else if (r > _p.outerRadius)
        {
            float k = Mathf.InverseLerp(_p.outerRadius, _p.outerRadius * 1.6f, r); // r=outer->外 で 0->1
            _vel -= radial * (_p.confinementStrength * (1f - k)) * dt;
        }

        // 6) 速度クランプ
        float maxSpeed = Mathf.Max(10f, _p.moveSpeed + _p.noiseAmplitude);
        float sp = _vel.magnitude;
        if (sp > maxSpeed) _vel = _vel * (maxSpeed / sp);

        // 7) 移動
        pos += _vel * dt;

        // 8) 万一中心付近に吸い込まれたら少し弾く
        if (pos.magnitude < Mathf.Max(2f, _p.innerRadius * 0.25f))
        {
            pos = radial * Mathf.Max(_p.innerRadius * 0.75f, 8f);
            _vel += tangent * 10f;
        }

        _rt.anchoredPosition = pos;
    }
}
