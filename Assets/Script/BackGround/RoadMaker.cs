using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

public class RoadMaker : MonoBehaviour
{
    // 描画対象のスプライン
    [SerializeField] private SplineContainer _splineContainer;

    // Line Renderer
    [SerializeField] private LineRenderer _lineRenderer;

    // セグメントの分割数
    [SerializeField] private int _segments = 30;

    private bool _isDirty;

    private void OnEnable()
    {
        // スプラインの更新時にLineRendererにパスを反映する設定
        _splineContainer.Spline.changed += Rebuild;

        // 初期化時は必ず反映
        Rebuild();
    }

    private void OnDisable()
    {
        _splineContainer.Spline.changed -= Rebuild;
    }

    private void Update()
    {
        // ワールド空間での描画の場合、Transformの更新もチェックしておく
        if (_lineRenderer.useWorldSpace && !_isDirty)
        {
            var splineTransform = _splineContainer.transform;
            _isDirty = splineTransform.hasChanged;
            splineTransform.hasChanged = false;
        }

        if (_isDirty)
            Rebuild();
    }

    // スプラインからLineRendererにパスを反映する
    public void Rebuild()
    {
        // テンポラリバッファを確保
        var points = new NativeArray<Vector3>(_segments, Allocator.Temp);

        // スプラインの読み取り専用情報
        using var spline = new NativeSpline(
            _splineContainer.Spline,
            _lineRenderer.useWorldSpace
                ? _splineContainer.transform.localToWorldMatrix
                : float4x4.identity
        );

        float total = _segments - 1;

        // セグメント数だけ線分を作成
        for (var i = 0; i < _segments; ++i)
        {
            points[i] = spline.EvaluatePosition(i / total);
        }

        // LineRendererに点情報を反映
        _lineRenderer.positionCount = _segments;
        _lineRenderer.SetPositions(points);

        // バッファを解放
        points.Dispose();

        _isDirty = false;
    }
}