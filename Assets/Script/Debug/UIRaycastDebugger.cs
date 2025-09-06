using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

/// <summary>
/// クリック位置に対して、どのUIがレイキャストにヒットしているかを一覧表示するデバッガ。
/// Canvas(GraphicRaycaster付き) と EventSystem を参照に設定して使います。
/// </summary>
public class UIRaycastDebugger : MonoBehaviour
{
    [SerializeField] private GraphicRaycaster raycaster;
    [SerializeField] private EventSystem eventSystem;
    [SerializeField] private bool logOnLeftClick = true;

    private PointerEventData _ped;
    private readonly List<RaycastResult> _results = new();

    private void Reset()
    {
        if (raycaster == null)
        {
            raycaster = GetComponentInParent<GraphicRaycaster>();
        }
        if (eventSystem == null)
        {
            eventSystem = EventSystem.current;
        }
    }

    private void Awake()
    {
        if (eventSystem == null) eventSystem = EventSystem.current;
        if (raycaster == null) raycaster = GetComponentInParent<GraphicRaycaster>();
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        if (logOnLeftClick && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            LogUnderPointer(Mouse.current.position.ReadValue());
        }
#else
        if (logOnLeftClick && Input.GetMouseButtonDown(0))
        {
            LogUnderPointer(Input.mousePosition);
        }
#endif
    }

    /// <summary>
    /// 指定スクリーン座標でのUIヒット一覧をログ出力します。
    /// </summary>
    public void LogUnderPointer(Vector2 screenPos)
    {
        if (eventSystem == null || raycaster == null)
        {
            Debug.LogWarning("[UIRaycastDebugger] eventSystem or raycaster is null. Please assign references.", this);
            return;
        }

        if (_ped == null)
        {
            _ped = new PointerEventData(eventSystem);
        }
        _ped.position = screenPos;
        _results.Clear();
        raycaster.Raycast(_ped, _results);

        if (_results.Count == 0)
        {
            Debug.Log($"[UIRaycastDebugger] No UI hit at {screenPos}", this);
            return;
        }

        Debug.Log($"[UIRaycastDebugger] UI hits at {screenPos}: count={_results.Count}", this);
        for (int i = 0; i < _results.Count; i++)
        {
            var r = _results[i];
            var go = r.gameObject;
            Debug.Log($" #{i} go={go.name}, module={r.module?.ToString()}, dist={r.distance:F3}, index={r.index}, sortingLayer={r.sortingLayer}, sortingOrder={r.sortingOrder}");
        }
    }
}
