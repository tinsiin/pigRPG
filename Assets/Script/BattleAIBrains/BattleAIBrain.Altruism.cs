using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// BattleAIBrain partial: 戦闘後利他行動プロファイル・ターゲット生成
/// </summary>
public abstract partial class BattleAIBrain
{
    // =============================================================================================================================
    // 戦闘後・利他行動プロファイル
    //  - 相性値しきい値/有効フラグのみをBrain側で保持
    //  - 各精神属性ごとの確率は世界固定（BaseStates.AltruismHelpProfiles）から取得
    // =============================================================================================================================
    [Header("戦闘後 利他行動プロファイル（確率は世界固定）")]
    [SerializeField, Range(0,100)] private int AltruismAffinityThreshold = 60;
    [SerializeField] private bool UseAltruismComponent = true;

    [Serializable]
    protected struct HelpBehaviorProfile
    {
        [Range(0f,1f)] public float P_ExtraOthers;   // 追加で更に他人を助ける確率
        [Range(0f,1f)] public float P_OnlyOthers;    // 自分は入れず、味方のみを助ける確率
        [Range(0f,1f)] public float P_GroupHelp;     // 一度に複数人を助ける確率（失敗時は最大1人）
        [Range(0f,1f)] public float P_FriendFirst;   // 初手で味方を先に入れる確率（自分を一度入れた後は影響しない）
        [Range(0f,1f)] public float FavorAffinityRate; // 相性値贔屓率（相性降順 vs ランダムの切替確率）
        [Min(0)] public int MaxOthers;               // 助ける味方人数の上限
    }

    private static readonly Dictionary<SpiritualProperty, HelpBehaviorProfile> s_AltruismHelpProfiles = new()
    {
        { SpiritualProperty.Doremis,           new HelpBehaviorProfile{ P_ExtraOthers=0.6f, P_OnlyOthers=0.10f, P_GroupHelp=0.5f, P_FriendFirst=0.7f, FavorAffinityRate=0.7f, MaxOthers=3 } },
        { SpiritualProperty.Pillar,            new HelpBehaviorProfile{ P_ExtraOthers=0.4f, P_OnlyOthers=0.05f, P_GroupHelp=0.35f, P_FriendFirst=0.5f, FavorAffinityRate=0.6f, MaxOthers=2 } },
        { SpiritualProperty.Kindergarten,      new HelpBehaviorProfile{ P_ExtraOthers=0.5f, P_OnlyOthers=0.10f, P_GroupHelp=0.45f, P_FriendFirst=0.6f, FavorAffinityRate=0.6f, MaxOthers=2 } },
        { SpiritualProperty.LiminalWhiteTile,  new HelpBehaviorProfile{ P_ExtraOthers=0.3f, P_OnlyOthers=0.05f, P_GroupHelp=0.25f, P_FriendFirst=0.4f, FavorAffinityRate=0.5f, MaxOthers=1 } },
        { SpiritualProperty.Sacrifaith,        new HelpBehaviorProfile{ P_ExtraOthers=0.7f, P_OnlyOthers=0.20f, P_GroupHelp=0.6f,  P_FriendFirst=0.8f, FavorAffinityRate=0.8f, MaxOthers=3 } },
        { SpiritualProperty.Cquiest,           new HelpBehaviorProfile{ P_ExtraOthers=0.4f, P_OnlyOthers=0.05f, P_GroupHelp=0.35f, P_FriendFirst=0.5f, FavorAffinityRate=0.5f, MaxOthers=2 } },
        { SpiritualProperty.Psycho,             new HelpBehaviorProfile{ P_ExtraOthers=0.2f, P_OnlyOthers=0.05f, P_GroupHelp=0.2f,  P_FriendFirst=0.3f, FavorAffinityRate=0.3f, MaxOthers=1 } },
        { SpiritualProperty.GodTier,           new HelpBehaviorProfile{ P_ExtraOthers=0.6f, P_OnlyOthers=0.10f, P_GroupHelp=0.5f,  P_FriendFirst=0.7f, FavorAffinityRate=0.7f, MaxOthers=3 } },
        { SpiritualProperty.BaleDrival,        new HelpBehaviorProfile{ P_ExtraOthers=0.3f, P_OnlyOthers=0.05f, P_GroupHelp=0.25f, P_FriendFirst=0.4f, FavorAffinityRate=0.5f, MaxOthers=1 } },
        { SpiritualProperty.Devil,             new HelpBehaviorProfile{ P_ExtraOthers=0.2f, P_OnlyOthers=0.05f, P_GroupHelp=0.15f, P_FriendFirst=0.3f, FavorAffinityRate=0.3f, MaxOthers=1 } },
    };

    protected static HelpBehaviorProfile GetAltruismHelpProfile(SpiritualProperty sp)
    {
        if (s_AltruismHelpProfiles.TryGetValue(sp, out var p)) return p;
        return new HelpBehaviorProfile{ P_ExtraOthers=0.4f, P_OnlyOthers=0.05f, P_GroupHelp=0.35f, P_FriendFirst=0.5f, FavorAffinityRate=0.5f, MaxOthers=2 };
    }

    // =============================================================================
    // 戦闘後：利他ターゲット生成部品（人リストのみ返す）
    //  - allies は self を除く味方候補を想定（Plan 側で取得）。順序＝優先度。スキル割当は Plan 側で実施。
    // =============================================================================

    [Serializable]
    protected struct TargetCandidate
    {
        public BaseStates Target;
        public bool IsSelf;
        public int Compatibility; // 0-100
    }

    protected List<TargetCandidate> BuildAltruisticTargetList(BaseStates self, IEnumerable<BaseStates> allies)
    {
        var result = new List<TargetCandidate>();
        if (self == null)
        {
            Debug.LogError("BuildAltruisticTargetList: self が null です");
            return result;
        }

        void AddSelf()
        {
            result.Add(new TargetCandidate { Target = self, IsSelf = true, Compatibility = 100 });
        }

        if (!UseAltruismComponent)
        {
            AddSelf();
            return result;
        }

        var allyList = (allies ?? Enumerable.Empty<BaseStates>())
            .Where(a => a != null && a != self)
            .Distinct()
            .ToList();

        // manager からグループと相性辞書を取得（取れない場合はログを出して自分のみ）
        var group = manager?.MyGroup(self);
        if (group == null)
        {
            Debug.LogError("BuildAltruisticTargetList: manager.MyGroup(self) が null です");
            AddSelf();
            return result;
        }
        var compDict = group.CharaCompatibility;
        if (compDict == null)
        {
            Debug.LogError("BuildAltruisticTargetList: CharaCompatibility が null です");
            // プログラマ調整前提の致命エラー：処理停止（空リスト）
            return result;
        }

        // 味方ごとの相性値を収集（存在しないキーがあればエラーにして自分のみ）
        var pairs = new List<TargetCandidate>();
        foreach (var ally in allyList)
        {
            if (!compDict.TryGetValue((self, ally), out var compat))
            {
                Debug.LogError($"BuildAltruisticTargetList: 相性値が未設定です ({self.CharacterName} -> {ally.CharacterName})");
                // プログラマ調整前提の致命エラー：処理停止（空リスト）
                return result;
            }
            pairs.Add(new TargetCandidate { Target = ally, IsSelf = false, Compatibility = Mathf.Clamp(compat, 0, 100) });
        }

        // しきい値に達する相性が無ければ、従来通り自分のみ
        if (!pairs.Any(p => p.Compatibility >= AltruismAffinityThreshold))
        {
            AddSelf();
            return result;
        }

        // プロファイル取得（見つからなければバランス型デフォルト）
        var profile = GetAltruismHelpProfile(self.MyImpression);

        // 並び替え：相性値贔屓率で切替（相性降順 or ランダム）
        List<TargetCandidate> orderedAllies;
        if (Roll(profile.FavorAffinityRate))
        {
            orderedAllies = pairs.OrderByDescending(p => p.Compatibility).ToList();
        }
        else
        {
            orderedAllies = pairs.OrderBy(_ => RandomSource.NextFloat()).ToList();
        }

        bool onlyOthers = Roll(profile.P_OnlyOthers);
        bool groupHelp = Roll(profile.P_GroupHelp);
        int maxOthers = Mathf.Max(0, profile.MaxOthers);

        // 選抜人数を決定（MaxOthers==0 なら誰も選ばない → フォールバック規則に従う）
        int take = 0;
        if (maxOthers > 0)
        {
            if (!groupHelp)
            {
                take = 1;
            }
            else
            {
                take = 1;
                for (int i = 1; i < maxOthers && i < orderedAllies.Count; i++)
                {
                    if (Roll(profile.P_ExtraOthers)) take++;
                    else break;
                }
            }
            take = Mathf.Min(take, orderedAllies.Count);
        }

        var selectedAllies = orderedAllies.Take(take).ToList();

        // OnlyOthers の場合で選抜0ならフォールバック：自分のみ
        if (onlyOthers)
        {
            if (selectedAllies.Count == 0)
            {
                AddSelf();
                return result;
            }
            result.AddRange(selectedAllies);
            return result;
        }

        // 自分も入れる場合：P_FriendFirst で順序を切替（自分を一度入れた後は影響しない想定により、単純な先頭配置）
        bool friendFirst = Roll(profile.P_FriendFirst);
        if (friendFirst && selectedAllies.Count > 0)
        {
            result.AddRange(selectedAllies);
            AddSelf();
        }
        else
        {
            AddSelf();
            result.AddRange(selectedAllies);
        }

        return result;
    }
}
