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

    [Header("武器選択UI")]
    public WeaponSelectArea WeaponSelectArea;

    [Header("EyeArea / ActionMark")]
    public GameObject EyeArea;
    public ActionMarkUI ActionMar;

    [Header("バトルアイコンUIスロット")]
    [Tooltip("スロット順: Left(0), Center(1), Right(2)")]
    public BattleIconUI[] BattleIconSlots = new BattleIconUI[3];
}
