using System;
using System.Collections.Generic;

// Save data for PlayersRuntime (battle-temporary data excluded).
[Serializable]
public sealed class PlayersSaveData
{
    public PlayersProgressSaveData Progress = new PlayersProgressSaveData();
    public List<PlayersAllySaveData> Allies = new List<PlayersAllySaveData>();
    public WalkProgressData WalkProgress;
}

[Serializable]
public sealed class PlayersProgressSaveData
{
    public int NowProgress;
    public int NowStageID;
    public int NowAreaID;
}

[Serializable]
public sealed class PlayersAllySaveData
{
    public AllyId AllyId;
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
