using System;
using System.Collections.Generic;

/// <summary>
/// 友情コンビ1件分のセーブデータ
/// </summary>
[Serializable]
public sealed class FriendshipComboSaveData
{
    public string ComboId;
    public List<string> MemberGuids = new();
    public bool IsKusareEnPath;
    public int ReEncountCount;

    /// <summary>
    /// 友情コンビの成熟に必要な再会回数。この回数でlowerRatioが1.0に達する。
    /// HP自然回復ボーナス(§2.1)とパッシブ蓄積(§2.4)の共通定数。
    /// </summary>
    public const int BondMatureCount = 11;

    /// <summary>
    /// 再会回数に基づく下限率。0〜1.0。付き合いが長いほど恩恵が確実になる。
    /// </summary>
    public float LowerRatio => Math.Min((float)ReEncountCount / BondMatureCount, 1f);
}

/// <summary>
/// 敵個体の永続化データ（全敵の状態追跡用）
/// </summary>
[Serializable]
public sealed class EnemyPersistenceData
{
    // ── 識別 ──
    public string EnemyGuid;
    /// <summary>所属EncounterSOのId</summary>
    public string EncounterId;
    /// <summary>EncounterSO.EnemyList内のインデックス</summary>
    public int TemplateIndex;

    // ── 戦闘状態 ──
    public bool IsBroken;
    public float HP;
    public float MentalHP;

    // ── エンカウント進行 ──
    /// <summary>最後のエンカウント時歩数（-1=初回未到達）</summary>
    public int LastEncounterProgress = -1;

    // ── 武器適応 ──
    /// <summary>排他ATK係数に掛かる適応率（0.77〜1.0）</summary>
    public float AdaptationRate = 1.0f;
    /// <summary>規格切替時の初期適応率</summary>
    public float AdaptationStartRate = 1.0f;
    /// <summary>規格切替後の経過戦闘数</summary>
    public int BattlesSinceProtocolSwitch;

    // ── 復活 ──
    /// <summary>復活残り歩数（-1=非対象）</summary>
    public int RebornRemainingSteps = -1;
    /// <summary>復活カウンター最終更新時の歩数</summary>
    public int RebornLastProgress = -1;
    /// <summary>EnemyRebornState のint値（0=Idle, 1=Counting, 2=Ready, 3=Reborned）</summary>
    public int RebornState;

    // ── 相性値変動 ──
    /// <summary>相手GUIDごとの累積変動量</summary>
    public List<BondDeltaEntry> BondDeltas;
}

/// <summary>
/// 相性値変動のシリアライズ用エントリ
/// </summary>
[Serializable]
public sealed class BondDeltaEntry
{
    public string Guid;
    public int Delta;
}

/// <summary>
/// FriendshipComboRegistryのExport/Import用データ
/// </summary>
[Serializable]
public sealed class FriendshipComboRegistrySaveData
{
    public List<FriendshipComboSaveData> Combos = new();
    public List<EnemyPersistenceData> EnemyStates = new();
}
