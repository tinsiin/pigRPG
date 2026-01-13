using System.Collections.Generic;

public sealed class PlayersSaveService
{
    public PlayersSaveData Build(PlayersProgressTracker progress, PlayersRoster roster)
    {
        var data = new PlayersSaveData();

        if (progress != null)
        {
            data.Progress.NowProgress = progress.NowProgress;
            data.Progress.NowStageID = progress.NowStageID;
            data.Progress.NowAreaID = progress.NowAreaID;
        }

        var allies = roster?.Allies;
        if (allies == null) return data;

        for (int i = 0; i < allies.Length; i++)
        {
            var ally = allies[i];
            if (ally == null) continue;

            var allyData = new PlayersAllySaveData
            {
                AllyId = (AllyId)i,
                HP = ally.HP,
                MentalHP = ally.MentalHP,
                P = ally.P,
                NowPower = ally.NowPower,
                MyImpression = ally.MyImpression,
                DefaultImpression = ally.DefaultImpression,
                NowResonanceValue = ally.NowResonanceValue
            };

            if (ally.BaseTenDayValues != null)
            {
                foreach (var kv in ally.BaseTenDayValues)
                {
                    allyData.BaseTenDayValues.Add(new TenDayValueSaveData
                    {
                        Ability = kv.Key,
                        Value = kv.Value
                    });
                }
            }

            var attrState = ally.AttrPoints?.ExportState();
            if (attrState != null)
            {
                foreach (var kv in attrState.Map)
                {
                    allyData.AttrPoints.Map.Add(new AttrPointSaveEntry
                    {
                        Attr = kv.Key,
                        Amount = kv.Value
                    });
                }

                foreach (var kv in attrState.History)
                {
                    allyData.AttrPoints.History.Add(new AttrPointSaveEntry
                    {
                        Attr = kv.Key,
                        Amount = kv.Value
                    });
                }
            }

            var adapt = ally.ExportPersistentAdaptationMemories();
            if (adapt != null)
            {
                foreach (var kv in adapt)
                {
                    allyData.AdaptationMemories.Add(new AdaptationMemorySaveData
                    {
                        Impression = kv.Key,
                        Value = kv.Value
                    });
                }
            }

            if (ally.Passives != null)
            {
                foreach (var passive in ally.Passives)
                {
                    if (passive == null) continue;
                    allyData.Passives.Add(new PassiveSaveData
                    {
                        PassiveId = passive.ID,
                        PassivePower = passive.PassivePower,
                        DurationTurnCounter = passive.DurationTurnCounter,
                        DurationWalkCounter = passive.DurationWalkCounter
                    });
                }
            }

            var layers = ally.VitalLayers;
            if (layers != null)
            {
                foreach (var layer in layers)
                {
                    if (layer == null) continue;
                    allyData.VitalLayers.Add(new VitalLayerSaveData
                    {
                        VitalLayerId = layer.id,
                        LayerHP = layer.LayerHP
                    });
                }
            }

            if (ally is AllyClass allyClass)
            {
                allyData.EmotionalAttachmentSkillID = allyClass.EmotionalAttachmentSkillID;
                allyData.EmotionalAttachmentSkillQuantity = allyClass.EmotionalAttachmentSkillQuantity;
                allyData.ValidSkillIds = allyClass.ValidSkillIDList != null
                    ? new List<int>(allyClass.ValidSkillIDList)
                    : new List<int>();

                var skills = allyClass.AllSkills;
                if (skills != null)
                {
                    foreach (var skill in skills)
                    {
                        if (skill == null) continue;
                        allyData.Skills.Add(new PlayerSkillSaveData
                        {
                            SkillId = skill.ID,
                            RecordDoCount = skill.RecordDoCount,
                            Proficiency = skill.Proficiency
                        });
                    }
                }
            }

            data.Allies.Add(allyData);
        }

        return data;
    }

    public void Apply(PlayersSaveData data, PlayersProgressTracker progress, PlayersRoster roster)
    {
        if (data == null) return;

        if (progress != null && data.Progress != null)
        {
            progress.SetProgress(data.Progress.NowProgress);
            progress.SetStage(data.Progress.NowStageID);
            progress.SetArea(data.Progress.NowAreaID);
        }

        var allies = roster?.Allies;
        if (allies == null || allies.Length == 0) return;

        var lookup = new Dictionary<AllyId, PlayersAllySaveData>();
        if (data.Allies != null)
        {
            foreach (var allyData in data.Allies)
            {
                lookup[allyData.AllyId] = allyData;
            }
        }

        for (int i = 0; i < allies.Length; i++)
        {
            var ally = allies[i];
            if (ally == null) continue;

            var allyId = (AllyId)i;
            if (!lookup.TryGetValue(allyId, out var allyData) || allyData == null) continue;

            ally.NowPower = allyData.NowPower;
            ally.SetImpressions(allyData.MyImpression, allyData.DefaultImpression);

            var tenDay = new TenDayAbilityDictionary();
            if (allyData.BaseTenDayValues != null)
            {
                foreach (var entry in allyData.BaseTenDayValues)
                {
                    tenDay[entry.Ability] = entry.Value;
                }
            }
            ally.ImportBaseTenDayValues(tenDay);

            var attrState = BuildAttrPointState(allyData.AttrPoints);
            if (attrState != null)
            {
                ally.AttrPoints.ImportState(attrState, suppressNotify: false);
                ally.ReclampAttrPToCapDropNew();
            }

            var adapt = BuildAdaptationDictionary(allyData.AdaptationMemories);
            ally.ImportPersistentAdaptationMemories(adapt);

            ally.P = allyData.P;
            ally.HP = allyData.HP;
            ally.MentalHP = allyData.MentalHP;
            ally.NowResonanceValue = allyData.NowResonanceValue;

            if (ally is AllyClass allyClass)
            {
                allyClass.EmotionalAttachmentSkillID = allyData.EmotionalAttachmentSkillID;
                allyClass.EmotionalAttachmentSkillQuantity = allyData.EmotionalAttachmentSkillQuantity;
                allyClass.ValidSkillIDList = allyData.ValidSkillIds != null
                    ? new List<int>(allyData.ValidSkillIds)
                    : new List<int>();

                ApplySkillState(allyClass, allyData.Skills);
            }

            ReplacePassives(ally, allyData.Passives);
            ReplaceVitalLayers(ally, allyData.VitalLayers);
        }
    }

    private static AttrPointModule.AttrPointModuleState BuildAttrPointState(AttrPointSaveState saved)
    {
        if (saved == null) return null;
        var state = new AttrPointModule.AttrPointModuleState();
        if (saved.Map != null)
        {
            foreach (var entry in saved.Map)
            {
                state.Map[entry.Attr] = entry.Amount;
            }
        }
        if (saved.History != null)
        {
            foreach (var entry in saved.History)
            {
                state.History.Add(new KeyValuePair<SpiritualProperty, int>(entry.Attr, entry.Amount));
            }
        }
        return state;
    }

    private static Dictionary<SkillImpression, float> BuildAdaptationDictionary(List<AdaptationMemorySaveData> entries)
    {
        var result = new Dictionary<SkillImpression, float>();
        if (entries == null) return result;
        foreach (var entry in entries)
        {
            result[entry.Impression] = entry.Value;
        }
        return result;
    }

    private static void ApplySkillState(AllyClass ally, List<PlayerSkillSaveData> skills)
    {
        if (ally == null) return;
        if (skills == null || skills.Count == 0) return;

        var lookup = new Dictionary<int, PlayerSkillSaveData>();
        foreach (var entry in skills)
        {
            lookup[entry.SkillId] = entry;
        }

        var allSkills = ally.AllSkills;
        if (allSkills == null) return;
        foreach (var skill in allSkills)
        {
            if (skill == null) continue;
            if (!lookup.TryGetValue(skill.ID, out var entry)) continue;
            skill.RecordDoCount = entry.RecordDoCount;
            skill.Proficiency = entry.Proficiency;
        }
    }

    private static void ReplacePassives(BaseStates ally, List<PassiveSaveData> passives)
    {
        if (ally == null) return;

        if (ally.Passives != null && ally.Passives.Count > 0)
        {
            var existing = new List<BasePassive>(ally.Passives);
            foreach (var passive in existing)
            {
                if (passive == null) continue;
                ally.RemovePassive(passive);
            }
        }

        if (passives == null) return;
        foreach (var entry in passives)
        {
            ally.ApplyPassiveByID(entry.PassiveId);
            var passive = ally.GetPassiveByID(entry.PassiveId);
            if (passive == null) continue;
            passive.SetPassivePower(entry.PassivePower);
            passive.DurationTurnCounter = entry.DurationTurnCounter;
            passive.DurationWalkCounter = entry.DurationWalkCounter;
        }
    }

    private static void ReplaceVitalLayers(BaseStates ally, List<VitalLayerSaveData> layers)
    {
        if (ally == null) return;

        var existing = ally.VitalLayers;
        if (existing != null && existing.Count > 0)
        {
            var cache = new List<BaseVitalLayer>(existing);
            foreach (var layer in cache)
            {
                if (layer == null) continue;
                ally.RemoveVitalLayerByID(layer.id);
            }
        }

        if (layers == null) return;
        foreach (var entry in layers)
        {
            ally.ApplyVitalLayer(entry.VitalLayerId);
            var assigned = FindVitalLayer(ally, entry.VitalLayerId);
            if (assigned != null)
            {
                assigned.LayerHP = entry.LayerHP;
            }
        }
    }

    private static BaseVitalLayer FindVitalLayer(BaseStates ally, int id)
    {
        var layers = ally?.VitalLayers;
        if (layers == null) return null;
        for (int i = 0; i < layers.Count; i++)
        {
            var layer = layers[i];
            if (layer != null && layer.id == id) return layer;
        }
        return null;
    }
}
