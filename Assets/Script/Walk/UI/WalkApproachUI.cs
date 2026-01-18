using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum ApproachChoice
{
    None,
    Skip,
    Left,
    Right,
    Center,
    Gate
}

public sealed class WalkApproachUI : MonoBehaviour
{
    [SerializeField] private Button leftButton;
    [SerializeField] private Button rightButton;
    [SerializeField] private Button centerButton;
    [SerializeField] private Button gateButton;

    private TMP_Text leftLabel;
    private TMP_Text rightLabel;
    private TMP_Text centerLabel;
    private TMP_Text gateLabel;
    private UniTaskCompletionSource<ApproachChoice> pending;
    private bool isAwaiting;

    public bool IsAwaiting => isAwaiting;

    private void Awake()
    {
        ResolveButtons();
        HookButtons();
        SetActive(false, false, false);
        SetGateActive(false);
    }

    public UniTask<ApproachChoice> WaitForSelection(string leftText, string rightText, bool showCenter, string centerText)
    {
        ResolveButtons();
        SetLabels(leftText, rightText, centerText);
        var showLeft = !string.IsNullOrEmpty(leftText);
        var showRight = !string.IsNullOrEmpty(rightText);
        SetActive(showLeft, showRight, showCenter);

        pending = new UniTaskCompletionSource<ApproachChoice>();
        isAwaiting = true;
        return AwaitSelection();
    }

    public bool TrySkip()
    {
        if (!isAwaiting || pending == null) return false;
        Resolve(ApproachChoice.Skip);
        return true;
    }

    /// <summary>
    /// ゲート/出口用の選択待機
    /// </summary>
    public UniTask<ApproachChoice> WaitForGateSelection(string labelText)
    {
        ResolveButtons();
        SetGateLabel(labelText);
        SetGateActive(true);

        pending = new UniTaskCompletionSource<ApproachChoice>();
        isAwaiting = true;
        return AwaitGateSelection();
    }

    private async UniTask<ApproachChoice> AwaitGateSelection()
    {
        var result = await pending.Task;
        SetGateActive(false);
        isAwaiting = false;
        pending = null;
        return result;
    }

    private async UniTask<ApproachChoice> AwaitSelection()
    {
        var result = await pending.Task;
        SetActive(false, false, false);
        isAwaiting = false;
        pending = null;
        return result;
    }

    private void ResolveButtons()
    {
        if (leftButton == null) leftButton = FindButton("ApproachLeftButton");
        if (rightButton == null) rightButton = FindButton("ApproachRightButton");
        if (centerButton == null) centerButton = FindButton("ApproachCenterButton");
        if (gateButton == null) gateButton = FindButton("GateApproachButton");

        leftLabel = ResolveLabel(leftButton, leftLabel);
        rightLabel = ResolveLabel(rightButton, rightLabel);
        centerLabel = ResolveLabel(centerButton, centerLabel);
        gateLabel = ResolveLabel(gateButton, gateLabel);
    }

    private void HookButtons()
    {
        if (leftButton != null)
        {
            leftButton.onClick.RemoveAllListeners();
            leftButton.onClick.AddListener(() => Resolve(ApproachChoice.Left));
        }
        if (rightButton != null)
        {
            rightButton.onClick.RemoveAllListeners();
            rightButton.onClick.AddListener(() => Resolve(ApproachChoice.Right));
        }
        if (centerButton != null)
        {
            centerButton.onClick.RemoveAllListeners();
            centerButton.onClick.AddListener(() => Resolve(ApproachChoice.Center));
        }
        if (gateButton != null)
        {
            gateButton.onClick.RemoveAllListeners();
            gateButton.onClick.AddListener(() => Resolve(ApproachChoice.Gate));
        }
    }

    private void Resolve(ApproachChoice choice)
    {
        if (!isAwaiting || pending == null) return;
        pending.TrySetResult(choice);
    }

    private void SetLabels(string leftText, string rightText, string centerText)
    {
        if (leftLabel != null) leftLabel.text = string.IsNullOrEmpty(leftText) ? "<" : leftText;
        if (rightLabel != null) rightLabel.text = string.IsNullOrEmpty(rightText) ? ">" : rightText;
        if (centerLabel != null) centerLabel.text = string.IsNullOrEmpty(centerText) ? "中央" : centerText;
    }

    private void SetActive(bool left, bool right, bool center)
    {
        if (leftButton != null) leftButton.gameObject.SetActive(left);
        if (rightButton != null) rightButton.gameObject.SetActive(right);
        if (centerButton != null) centerButton.gameObject.SetActive(center);
    }

    private Button FindButton(string name)
    {
        var target = transform.Find(name);
        return target != null ? target.GetComponent<Button>() : null;
    }

    private TMP_Text ResolveLabel(Button button, TMP_Text current)
    {
        if (current != null || button == null) return current;
        return button.GetComponentInChildren<TMP_Text>(true);
    }

    private void SetGateLabel(string text)
    {
        if (gateLabel != null) gateLabel.text = string.IsNullOrEmpty(text) ? "アプローチ" : text;
    }

    private void SetGateActive(bool active)
    {
        if (gateButton != null) gateButton.gameObject.SetActive(active);
    }
}
