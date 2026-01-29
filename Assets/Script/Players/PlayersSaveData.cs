using System;
using System.Collections.Generic;

/// <summary>
/// PlayersRuntime のセーブデータ（バトル一時データ除外）
/// </summary>
[Serializable]
public sealed class PlayersSaveData
{
    /// <summary>解放済みキャラクターID一覧（Roster.AllIdsから生成）</summary>
    public List<string> UnlockedCharacterIds = new List<string>();

    /// <summary>全キャラクターのセーブデータ</summary>
    public List<PlayersAllySaveData> Allies = new List<PlayersAllySaveData>();

    /// <summary>現在のパーティー編成（CharacterId の文字列リスト）</summary>
    public List<string> ActivePartyIds = new List<string>();

    /// <summary>歩行進捗データ</summary>
    public WalkProgressData WalkProgress;
}

[Serializable]
public sealed class PlayersAllySaveData
{
    /// <summary>キャラクターID（文字列）</summary>
    public string CharacterId;

    public float HP;
    public float MentalHP;
    public int P;
    public ThePower NowPower;
    public SpiritualProperty MyImpression;
    public SpiritualProperty DefaultImpression;
    public float NowResonanceValue;
    public int EmotionalAttachmentSkillID;
    public float EmotionalAttachmentSkillQuantity;
    public List<int> ValidSkillIds = new List<int>();
    public List<TenDayValueSaveData> BaseTenDayValues = new List<TenDayValueSaveData>();
    public AttrPointSaveState AttrPoints = new AttrPointSaveState();
    public List<PlayerSkillSaveData> Skills = new List<PlayerSkillSaveData>();
    public List<PassiveSaveData> Passives = new List<PassiveSaveData>();
    public List<VitalLayerSaveData> VitalLayers = new List<VitalLayerSaveData>();
    public List<AdaptationMemorySaveData> AdaptationMemories = new List<AdaptationMemorySaveData>();
}

[Serializable]
public struct TenDayValueSaveData
{
    public TenDayAbility Ability;
    public float Value;
}

[Serializable]
public sealed class AttrPointSaveState
{
    public List<AttrPointSaveEntry> Map = new List<AttrPointSaveEntry>();
    public List<AttrPointSaveEntry> History = new List<AttrPointSaveEntry>();
}

[Serializable]
public struct AttrPointSaveEntry
{
    public SpiritualProperty Attr;
    public int Amount;
}

[Serializable]
public struct PlayerSkillSaveData
{
    public int SkillId;
    public int RecordDoCount;
    public float Proficiency;
}

[Serializable]
public struct PassiveSaveData
{
    public int PassiveId;
    public int PassivePower;
    public int DurationTurnCounter;
    public int DurationWalkCounter;
}

[Serializable]
public struct VitalLayerSaveData
{
    public int VitalLayerId;
    public float LayerHP;
}

[Serializable]
public struct AdaptationMemorySaveData
{
    public SkillImpression Impression;
    public float Value;
}
