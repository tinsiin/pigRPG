using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CharacterIdとUIセットのマッピングを管理するレジストリ。
/// シーンに配置してInspectorで全キャラクターのUI参照を設定する。
///
/// 役割:
/// - CharacterId → AllyUISet のルックアップ（スキル選択UI）
/// - CharacterId → SkillUIObject のルックアップ
/// - 全キャラクター（固定3人 + 新キャラ）を統一的に管理
///
/// 注: BattleIconUIはPartyUISlotManager（PlayersRuntime内）で管理
/// </summary>
public sealed class CharacterUIRegistry : MonoBehaviour
{
    public static CharacterUIRegistry Instance { get; private set; }

    [Serializable]
    public class UIEntry
    {
        [Tooltip("キャラクターID（小文字英数字）例: geino, newchar1")]
        public string CharacterId;

        [Tooltip("スキルUI用のGameObject（TabCharaStateContent用）")]
        public GameObject SkillUIObject;

        [Tooltip("キャラクター用のAllyUISet（スキル選択ボタン等）")]
        public AllyUISet AllyUISet;

        // BattleIconUIはPartyUISlotManager（PlayersRuntime内）で管理するため削除

        public bool IsValid => !string.IsNullOrEmpty(CharacterId);
    }

    [Header("キャラクターUI設定")]
    [Tooltip("全キャラクターのUI参照を設定。固定3人（geino, noramlia, sites）も新キャラも同じリストで管理。")]
    [SerializeField] private List<UIEntry> entries = new();

    private Dictionary<CharacterId, UIEntry> _map;
    private bool _initialized;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("CharacterUIRegistry: 複数のインスタンスが存在します。古いインスタンスを上書きします。");
        }
        Instance = this;
        BuildMap();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void BuildMap()
    {
        _map = new Dictionary<CharacterId, UIEntry>();

        foreach (var entry in entries)
        {
            if (entry == null || !entry.IsValid) continue;

            var id = new CharacterId(entry.CharacterId);
            if (!id.IsValid)
            {
                Debug.LogWarning($"CharacterUIRegistry: 無効なCharacterId '{entry.CharacterId}'");
                continue;
            }

            if (_map.ContainsKey(id))
            {
                Debug.LogWarning($"CharacterUIRegistry: ID重複 '{id}'。スキップします。");
                continue;
            }

            _map[id] = entry;
        }

        _initialized = true;
        Debug.Log($"CharacterUIRegistry: {_map.Count}キャラクター分のUIを登録しました");
    }

    /// <summary>
    /// 初期化を確認し、必要なら実行する。
    /// </summary>
    private void EnsureInitialized()
    {
        if (!_initialized)
        {
            BuildMap();
        }
    }

    /// <summary>
    /// CharacterIdでUIセットを取得する。
    /// </summary>
    public AllyUISet GetUISet(CharacterId id)
    {
        EnsureInitialized();
        return _map.TryGetValue(id, out var entry) ? entry.AllyUISet : null;
    }

    /// <summary>
    /// CharacterIdでスキルUIオブジェクトを取得する。
    /// </summary>
    public GameObject GetSkillUIObject(CharacterId id)
    {
        EnsureInitialized();
        return _map.TryGetValue(id, out var entry) ? entry.SkillUIObject : null;
    }

    // GetBattleIconUI は削除 - BattleIconUIはPartyUISlotManager（PlayersRuntime内）で管理

    /// <summary>
    /// CharacterIdでUIセットを取得する（存在確認付き）。
    /// </summary>
    public bool TryGetUISet(CharacterId id, out AllyUISet uiSet)
    {
        EnsureInitialized();
        uiSet = null;
        if (_map.TryGetValue(id, out var entry))
        {
            uiSet = entry.AllyUISet;
            return uiSet != null;
        }
        return false;
    }

    /// <summary>
    /// CharacterIdが登録されているか確認する。
    /// </summary>
    public bool HasUISet(CharacterId id)
    {
        EnsureInitialized();
        return _map.ContainsKey(id);
    }

    /// <summary>
    /// 実行時にキャラクターUIを登録する（動的追加用）。
    /// </summary>
    public void Register(CharacterId id, AllyUISet uiSet, GameObject skillUIObject = null)
    {
        EnsureInitialized();

        if (!id.IsValid || uiSet == null)
        {
            Debug.LogWarning($"CharacterUIRegistry.Register: 無効なパラメータ id={id}, uiSet={uiSet}");
            return;
        }

        if (_map.ContainsKey(id))
        {
            Debug.LogWarning($"CharacterUIRegistry.Register: '{id}' は既に登録済みです。上書きします。");
        }

        _map[id] = new UIEntry
        {
            CharacterId = id.Value,
            AllyUISet = uiSet,
            SkillUIObject = skillUIObject
        };

        Debug.Log($"CharacterUIRegistry: '{id}' を動的に登録しました");
    }

    /// <summary>
    /// 登録されている全CharacterIdを取得する。
    /// </summary>
    public IEnumerable<CharacterId> AllCharacterIds
    {
        get
        {
            EnsureInitialized();
            return _map.Keys;
        }
    }

    /// <summary>
    /// 登録されているUIセット数。
    /// </summary>
    public int Count
    {
        get
        {
            EnsureInitialized();
            return _map.Count;
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// エディタ用: 空ID/重複を検証
    /// </summary>
    [ContextMenu("Validate Entries")]
    private void ValidateEntries()
    {
        var seen = new HashSet<string>();
        var hasError = false;

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry == null)
            {
                Debug.LogError($"CharacterUIRegistry: entries[{i}] が null です");
                hasError = true;
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.CharacterId))
            {
                Debug.LogError($"CharacterUIRegistry: entries[{i}] のCharacterIdが空です");
                hasError = true;
                continue;
            }

            var normalized = entry.CharacterId.ToLowerInvariant();
            if (!seen.Add(normalized))
            {
                Debug.LogError($"CharacterUIRegistry: ID重複 '{entry.CharacterId}' (entries[{i}])");
                hasError = true;
            }

            if (entry.AllyUISet == null)
            {
                Debug.LogWarning($"CharacterUIRegistry: entries[{i}] ({entry.CharacterId}) のAllyUISetが未設定です");
            }
        }

        if (!hasError)
        {
            Debug.Log($"CharacterUIRegistry: 検証OK ({entries.Count}エントリ)");
        }
    }

    /// <summary>
    /// エディタ用: 固定3人のエントリを自動生成
    /// </summary>
    [ContextMenu("Add Default Entries (Geino, Noramlia, Sites)")]
    private void AddDefaultEntries()
    {
        var defaults = new[] { "geino", "noramlia", "sites" };
        foreach (var id in defaults)
        {
            if (entries.Exists(e => e?.CharacterId?.ToLowerInvariant() == id))
            {
                Debug.Log($"CharacterUIRegistry: '{id}' は既に存在します");
                continue;
            }

            entries.Add(new UIEntry { CharacterId = id });
            Debug.Log($"CharacterUIRegistry: '{id}' を追加しました");
        }
    }
#endif
}
