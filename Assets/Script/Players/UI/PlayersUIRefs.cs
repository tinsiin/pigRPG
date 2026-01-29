using UnityEngine;
using UnityEngine.UI;

public class PlayersUIRefs : MonoBehaviour
{
    [Header("モーダルエリア")]
    public GameObject ModalArea;

    [Header("スキルパッシブ対象スキル選択ボタン管理エリア")]
    public SelectSkillPassiveTargetSkillButtons SelectSkillPassiveTargetHandle;

    [Header("思い入れスキル選択UI")]
    public SelectEmotionalAttachmentSkillButtons EmotionalAttachmentSkillSelectUIArea;

    [Header("EyeArea / ActionMark")]
    public GameObject EyeArea;
    public ActionMarkUI ActionMar;
}
