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
/// 敵個体の永続化データ（コンビメンバーの状態追跡用）
/// </summary>
[Serializable]
public sealed class EnemyPersistenceData
{
    public string EnemyGuid;
    /// <summary>所属EncounterSOのId</summary>
    public string EncounterId;
    /// <summary>EncounterSO.EnemyList内のインデックス</summary>
    public int TemplateIndex;
    public bool IsBroken;
    public float HP;
    public float MentalHP;
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
