using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// セーブでセーブされるような事柄とかメインループで操作するためのステータス太刀
/// </summary>
[DefaultExecutionOrder(-900)]
public class PlayersBootstrapper : MonoBehaviour
{
    [Header("キャラクターデータ")]
    public CharacterDataRegistry CharacterRegistry;

    [Header("思い入れスキル弱体化用スキルパッシブ")]
    public BaseSkillPassive EmotionalAttachmentSkillWeakeningPassive;

    public int HP_TO_MaxP_CONVERSION_FACTOR = 80;
    public int MentalHP_TO_P_Recovely_CONVERSION_FACTOR = 120;

    private PlayersRuntime runtime;
    private PlayersContext context;

    public PlayersContext Context => context;

    public AllyClass geino => runtime?.Roster?.GetAlly(CharacterId.Geino);
    public AllyClass noramlia => runtime?.Roster?.GetAlly(CharacterId.Noramlia);
    public AllyClass sites => runtime?.Roster?.GetAlly(CharacterId.Sites);

    public int AllyCount => runtime?.Roster?.AllyCount ?? 0;

    private void Awake()
    {
        var existing = FindObjectsOfType<PlayersBootstrapper>(true);
        if (existing.Length > 1)
        {
            Destroy(gameObject);
            return;
        }

        var refs = GetComponent<PlayersUIRefs>();
        if (refs == null)
        {
            Debug.LogError("PlayersUIRefs not found on PlayersBootstrapper.");
            Destroy(gameObject);
            return;
        }

        if (CharacterRegistry == null)
        {
            Debug.LogError("CharacterRegistry not assigned on PlayersBootstrapper.");
            Destroy(gameObject);
            return;
        }

        runtime = new PlayersRuntime();
        var config = new PlayersRuntimeConfig
        {
            CharacterRegistry = CharacterRegistry,
            EmotionalAttachmentSkillWeakeningPassive = EmotionalAttachmentSkillWeakeningPassive,
            HpToMaxPConversionFactor = HP_TO_MaxP_CONVERSION_FACTOR,
            MentalHpToPRecoveryConversionFactor = MentalHP_TO_P_Recovely_CONVERSION_FACTOR
        };
        runtime.Initialize(refs, config);
        context = runtime.Context;

        PlayersContextRegistry.SetContext(context);
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        runtime?.Init();
    }

    private void OnDestroy()
    {
        runtime?.Shutdown();
        PlayersContextRegistry.ClearContext(context);
    }

    public void Init()
    {
        runtime?.Init();
    }

    public void OnSkillSelectionScreenTransition(CharacterId id)
    {
        runtime?.SkillUI?.OnSkillSelectionScreenTransition(id);
    }

    public void OnlySelectActs(SkillZoneTrait trait, SkillType type, CharacterId id)
    {
        runtime?.SkillUI?.OnlySelectActs(trait, type, id);
    }

    public void RequestStopFreezeConsecutive(CharacterId id)
    {
        runtime?.Party?.RequestStopFreezeConsecutive(id);
    }

    public void GoToCancelPassiveField(CharacterId id)
    {
        runtime?.UIFacade?.GoToCancelPassiveField(id);
    }

    public void ReturnCancelPassiveToDefaultArea(CharacterId id)
    {
        runtime?.UIFacade?.ReturnCancelPassiveToDefaultArea(id);
    }

    public int GlobalSteps => GameContextHub.Current?.Counters?.GlobalSteps ?? 0;

    public void AllyAlliesUISetActive(bool isActive)
    {
        runtime?.UIControl?.AllyAlliesUISetActive(isActive);
    }

    public BattleGroup GetParty()
    {
        return runtime?.Party?.GetParty();
    }

    public void PlayersOnWin()
    {
        runtime?.Party?.PlayersOnWin();
    }

    public void PlayersOnLost()
    {
        runtime?.Party?.PlayersOnLost();
    }

    public void PlayersOnRunOut()
    {
        runtime?.Party?.PlayersOnRunOut();
    }

    public void PlayersOnWalks(int walkCount)
    {
        runtime?.Party?.PlayersOnWalks(walkCount);
    }

    public UniTask<List<BaseSkill>> GoToSelectSkillPassiveTargetSkillButtonsArea(List<BaseSkill> skills, int selectCount)
    {
        if (runtime?.SkillUI == null) return UniTask.FromResult(new List<BaseSkill>());
        return runtime.SkillUI.GoToSelectSkillPassiveTargetSkillButtonsArea(skills, selectCount);
    }

    public void ReturnSelectSkillPassiveTargetSkillButtonsArea()
    {
        runtime?.SkillUI?.ReturnSelectSkillPassiveTargetSkillButtonsArea();
    }

    public void OpenEmotionalAttachmentSkillSelectUIArea(CharacterId id)
    {
        runtime?.SkillUI?.OpenEmotionalAttachmentSkillSelectUIArea(id);
    }

    public void OnBattleStart()
    {
        runtime?.SkillUI?.OnBattleStart();
    }

    public PlayersSaveData CreateSaveData()
    {
        return runtime?.CreateSaveData();
    }

    public void ApplySaveData(PlayersSaveData data)
    {
        runtime?.ApplySaveData(data);
    }

    public bool SaveToJsonDefault(string fileName = PlayersSaveIO.DefaultFileName)
    {
        if (runtime == null)
        {
            Debug.LogError("PlayersBootstrapper.SaveToJsonDefault: runtime is null.");
            return false;
        }

        var data = runtime.CreateSaveData();
        return PlayersSaveIO.SaveDefault(data, fileName);
    }

    public bool LoadFromJsonDefault(string fileName = PlayersSaveIO.DefaultFileName)
    {
        if (runtime == null)
        {
            Debug.LogError("PlayersBootstrapper.LoadFromJsonDefault: runtime is null.");
            return false;
        }

        if (!PlayersSaveIO.TryLoadDefault(out var data, fileName)) return false;
        runtime.ApplySaveData(data);
        return true;
    }

    public float ExplosionVoidValue => runtime?.Tuning?.ExplosionVoidValue ?? 0f;
    public int HpToMaxPConversionFactor => runtime?.Tuning?.HpToMaxPConversionFactor ?? 0;
    public int MentalHpToPRecoveryConversionFactor => runtime?.Tuning?.MentalHpToPRecoveryConversionFactor ?? 0;
    public BaseSkillPassive EmotionalAttachmentSkillWeakeningPassiveRef => runtime?.Tuning?.EmotionalAttachmentSkillWeakeningPassiveRef;

    public void GoToCancelPassiveField_geino()
    {
        GoToCancelPassiveField(CharacterId.Geino);
    }

    public void ReturnCancelPassiveToDefaultArea_geino()
    {
        ReturnCancelPassiveToDefaultArea(CharacterId.Geino);
    }

    public void Geino_OpenEmotionalAttachmentSkillSelectUIArea()
    {
        OpenEmotionalAttachmentSkillSelectUIArea(CharacterId.Geino);
    }

    public void CheckGeinoTenDayValuesCallBack()
    {
        var controller = CharaconfigController.Instance ?? FindObjectOfType<CharaconfigController>(true);
        if (controller == null)
        {
            Debug.LogError("PlayersBootstrapper: CharaconfigController not found.");
            return;
        }

        controller.SetSelectedByCharacterId(CharacterId.Geino);
        controller.OnClickOpenTenDayAbility();
    }
}
