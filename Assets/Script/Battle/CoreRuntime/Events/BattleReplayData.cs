using System;
using System.Collections.Generic;

/// <summary>
/// Replay save data (seed + inputs + optional events).
/// JsonUtility-friendly: public fields only.
/// </summary>
[Serializable]
public sealed class BattleReplayData
{
    public const int CurrentVersion = 1;

    public int Version = CurrentVersion;
    public bool HasRandomSeed;
    public int RandomSeed;
    public List<BattleInputRecordData> Inputs = new();
    public List<BattleEventRecordData> Events = new();

    public int? GetRandomSeed()
    {
        return HasRandomSeed ? RandomSeed : null;
    }

    public IReadOnlyList<BattleInputRecord> ToInputRecords()
    {
        if (Inputs == null || Inputs.Count == 0) return Array.Empty<BattleInputRecord>();
        var list = new List<BattleInputRecord>(Inputs.Count);
        for (var i = 0; i < Inputs.Count; i++)
        {
            list.Add(Inputs[i].ToRecord());
        }
        return list;
    }

    public IReadOnlyList<BattleEvent> ToEvents()
    {
        if (Events == null || Events.Count == 0) return Array.Empty<BattleEvent>();
        var list = new List<BattleEvent>(Events.Count);
        for (var i = 0; i < Events.Count; i++)
        {
            list.Add(Events[i].ToEvent());
        }
        return list;
    }

    public static BattleReplayData FromRecorder(BattleEventRecorder recorder)
    {
        var data = new BattleReplayData();
        if (recorder == null)
        {
            return data;
        }

        if (recorder.RandomSeed.HasValue)
        {
            data.HasRandomSeed = true;
            data.RandomSeed = recorder.RandomSeed.Value;
        }

        if (recorder.Inputs != null)
        {
            for (var i = 0; i < recorder.Inputs.Count; i++)
            {
                data.Inputs.Add(BattleInputRecordData.FromRecord(recorder.Inputs[i]));
            }
        }

        if (recorder.Events != null)
        {
            for (var i = 0; i < recorder.Events.Count; i++)
            {
                data.Events.Add(BattleEventRecordData.FromEvent(recorder.Events[i]));
            }
        }

        return data;
    }
}

[Serializable]
public struct BattleInputRecordData
{
    public BattleInputType Type;
    public string ActorName;
    public string SkillName;
    public DirectedWill TargetWill;
    public SkillZoneTrait RangeWill;
    public bool IsOption;
    public string[] TargetNames;
    public int[] TargetIndices;
    public int TurnCount;

    public static BattleInputRecordData FromRecord(BattleInputRecord record)
    {
        return new BattleInputRecordData
        {
            Type = record.Type,
            ActorName = record.ActorName,
            SkillName = record.SkillName,
            TargetWill = record.TargetWill,
            RangeWill = record.RangeWill,
            IsOption = record.IsOption,
            TargetNames = record.TargetNames,
            TargetIndices = record.TargetIndices,
            TurnCount = record.TurnCount
        };
    }

    public BattleInputRecord ToRecord()
    {
        return new BattleInputRecord(
            Type,
            ActorName,
            SkillName,
            TargetWill,
            RangeWill,
            IsOption,
            TargetNames,
            TargetIndices,
            TurnCount);
    }
}

[Serializable]
public struct BattleEventRecordData
{
    public BattleEventType Type;
    public string Message;
    public bool Important;
    public int TurnCount;
    public string ActorName;
    public bool Immediate;
    public bool WaitForIntro;
    public bool HasSingleTargetReservation;

    public static BattleEventRecordData FromEvent(BattleEvent battleEvent)
    {
        return new BattleEventRecordData
        {
            Type = battleEvent.Type,
            Message = battleEvent.Message,
            Important = battleEvent.Important,
            TurnCount = battleEvent.TurnCount,
            ActorName = battleEvent.ActorName,
            Immediate = battleEvent.Immediate,
            WaitForIntro = battleEvent.WaitForIntro,
            HasSingleTargetReservation = battleEvent.HasSingleTargetReservation
        };
    }

    public BattleEvent ToEvent()
    {
        return new BattleEvent(
            Type,
            Message,
            Important,
            TurnCount,
            ActorName,
            actor: null,
            immediate: Immediate,
            waitForIntro: WaitForIntro,
            hasSingleTargetReservation: HasSingleTargetReservation);
    }
}
