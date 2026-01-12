using UnityEngine;
using UnityEngine.UI;

public class PlayersUIRefs : MonoBehaviour
{
    [Header(PlayersStates.AllyIndexHeader)]
    public AllyUISet[] AllyUISets = new AllyUISet[3];

    [Header("モーダルエリア")]
    public GameObject ModalArea;

    [Header("スキルパッシブ対象スキル選択ボタン管理エリア")]
    public SelectSkillPassiveTargetSkillButtons SelectSkillPassiveTargetHandle;

    [Header("思い入れスキル選択UI")]
    public SelectEmotionalAttachmentSkillButtons EmotionalAttachmentSkillSelectUIArea;

    [Header("EyeArea / ActionMark")]
    public GameObject EyeArea;
    public ActionMarkUI ActionMar;

    public void EnsureAllyUISets()
    {
        var count = AllyUISets != null ? AllyUISets.Length : 0;
        if (count <= 0) count = 3;

        if (AllyUISets == null || AllyUISets.Length != count)
        {
            var next = new AllyUISet[count];
            if (AllyUISets != null)
            {
                var copyCount = Mathf.Min(AllyUISets.Length, next.Length);
                for (int i = 0; i < copyCount; i++)
                {
                    next[i] = AllyUISets[i];
                }
            }
            AllyUISets = next;
        }

        for (int i = 0; i < AllyUISets.Length; i++)
        {
            if (AllyUISets[i] == null)
            {
                AllyUISets[i] = new AllyUISet();
            }

            var set = AllyUISets[i];
        }
    }
}
