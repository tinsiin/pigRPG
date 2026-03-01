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
