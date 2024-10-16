using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

public class RoadMaker : MonoBehaviour
{
    // �`��Ώۂ̃X�v���C��
    [SerializeField] private SplineContainer _splineContainer;

    // Line Renderer
    [SerializeField] private LineRenderer _lineRenderer;

    // �Z�O�����g�̕�����
    [SerializeField] private int _segments = 30;

    private bool _isDirty;

    private void OnEnable()
    {
        // �X�v���C���̍X�V����LineRenderer�Ƀp�X�𔽉f����ݒ�
        _splineContainer.Spline.changed += Rebuild;

        // ���������͕K�����f
        Rebuild();
    }

    private void OnDisable()
    {
        _splineContainer.Spline.changed -= Rebuild;
    }

    private void Update()
    {
        // ���[���h��Ԃł̕`��̏ꍇ�ATransform�̍X�V���`�F�b�N���Ă���
        if (_lineRenderer.useWorldSpace && !_isDirty)
        {
            var splineTransform = _splineContainer.transform;
            _isDirty = splineTransform.hasChanged;
            splineTransform.hasChanged = false;
        }

        if (_isDirty)
            Rebuild();
    }

    // �X�v���C������LineRenderer�Ƀp�X�𔽉f����
    public void Rebuild()
    {
        // �e���|�����o�b�t�@���m��
        var points = new NativeArray<Vector3>(_segments, Allocator.Temp);

        // �X�v���C���̓ǂݎ���p���
        using var spline = new NativeSpline(
            _splineContainer.Spline,
            _lineRenderer.useWorldSpace
                ? _splineContainer.transform.localToWorldMatrix
                : float4x4.identity
        );

        float total = _segments - 1;

        // �Z�O�����g�������������쐬
        for (var i = 0; i < _segments; ++i)
        {
            points[i] = spline.EvaluatePosition(i / total);
        }

        // LineRenderer�ɓ_���𔽉f
        _lineRenderer.positionCount = _segments;
        _lineRenderer.SetPositions(points);

        // �o�b�t�@�����
        points.Dispose();

        _isDirty = false;
    }
}