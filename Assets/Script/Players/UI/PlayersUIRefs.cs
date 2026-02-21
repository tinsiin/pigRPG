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

    [Header("武器スラッシュ")]
    [Tooltip("スラッシュエフェクトの表示倍率（1.0 = 等倍）")]
    [Range(0.5f, 3f)]
    public float WeaponSlashScale = 1f;

    [Tooltip("スラッシュの速度（1.0 = 最速＝現在速度、0.05 = 非常にゆっくり）")]
    [Range(0.05f, 1f)]
    public float WeaponSlashSpeed = 1f;

    [Tooltip("スラッシュ発動時に再生する共通効果音（未設定なら無音）")]
    public AudioClip WeaponSlashSE;

    private AudioSource _audioSource;

    public void PlayWeaponSlashSE()
    {
        if (WeaponSlashSE == null) return;
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
        }
        _audioSource.PlayOneShot(WeaponSlashSE);
    }

    [Header("ダメージフロー数字")]
    [Tooltip("ダメージ数字ポップアップのプレハブ")]
    public DamageFlowNumber DamageFlowPrefab;

    [Header("被弾点滅")]
    [Tooltip("点滅の合計時間（秒）")]
    [Range(0.2f, 1.5f)]
    public float DamageBlinkDuration = 0.5f;

    [Tooltip("点滅回数")]
    [Range(2, 8)]
    public int DamageBlinkCount = 4;
}
