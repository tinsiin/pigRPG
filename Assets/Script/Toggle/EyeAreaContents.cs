using UnityEngine;

/// <summary>
/// EyeArea側のContent切替基底クラス。
/// TabContentsのEyeArea版。
/// </summary>
public abstract class EyeAreaContents : MonoBehaviour
{
    [Header("Contents")]
    [SerializeField] protected GameObject walkContent;
    [SerializeField] protected GameObject novelContent;
    [SerializeField] protected GameObject battleContent;

    public abstract void SwitchContent(EyeAreaState state);

    protected void SetAllInactive()
    {
        if (walkContent != null) walkContent.SetActive(false);
        if (novelContent != null) novelContent.SetActive(false);
        if (battleContent != null) battleContent.SetActive(false);
    }
}
