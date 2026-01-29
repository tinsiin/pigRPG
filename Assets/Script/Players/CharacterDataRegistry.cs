using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// プロジェクト内の全CharacterDataSOを管理するレジストリ。
/// CharacterIdでのルックアップを提供する。
///
/// 使用方法:
/// 1. アセットを作成し、PlayersBootstrapperのInspectorから参照を設定
/// 2. PlayersRuntimeConfig経由で渡される
/// </summary>
[CreateAssetMenu(menuName = "Character/Character Data Registry", fileName = "CharacterDataRegistry")]
public sealed class CharacterDataRegistry : ScriptableObject
{

    [Header("登録キャラクター")]
    [Tooltip("プロジェクト内の全キャラクターデータ")]
    [SerializeField] private List<CharacterDataSO> _characters = new();

    // === ルックアップ用キャッシュ ===
    private Dictionary<CharacterId, CharacterDataSO> _lookupCache;
    private bool _cacheInitialized;

    /// <summary>登録キャラクター数</summary>
    public int Count => _characters?.Count ?? 0;

    /// <summary>全登録キャラクター</summary>
    public IReadOnlyList<CharacterDataSO> AllCharacters => _characters;

    /// <summary>
    /// CharacterIdでキャラクターデータを取得する。
    /// </summary>
    public CharacterDataSO GetCharacter(CharacterId id)
    {
        EnsureCacheInitialized();
        return _lookupCache.TryGetValue(id, out var data) ? data : null;
    }

    /// <summary>
    /// CharacterIdでキャラクターデータを取得する（string版）。
    /// </summary>
    public CharacterDataSO GetCharacter(string id)
    {
        return GetCharacter(new CharacterId(id));
    }

    /// <summary>
    /// キャラクターデータが存在するか確認する。
    /// </summary>
    public bool HasCharacter(CharacterId id)
    {
        EnsureCacheInitialized();
        return _lookupCache.ContainsKey(id);
    }

    /// <summary>
    /// キャラクターデータからAllyClassインスタンスを作成する。
    /// </summary>
    public AllyClass CreateInstance(CharacterId id)
    {
        var data = GetCharacter(id);
        if (data == null)
        {
            Debug.LogWarning($"CharacterDataRegistry.CreateInstance: {id} のデータが見つかりません");
            return null;
        }
        return data.CreateInstance();
    }

    /// <summary>
    /// 固定メンバー（Geino/Noramlia/Sites）のデータを取得する。
    /// </summary>
    public IEnumerable<CharacterDataSO> GetOriginalMembers()
    {
        EnsureCacheInitialized();
        foreach (var data in _characters)
        {
            if (data != null && data.IsOriginalMember)
            {
                yield return data;
            }
        }
    }

    /// <summary>
    /// 新キャラクター（固定メンバー以外）のデータを取得する。
    /// </summary>
    public IEnumerable<CharacterDataSO> GetNewCharacters()
    {
        EnsureCacheInitialized();
        foreach (var data in _characters)
        {
            if (data != null && !data.IsOriginalMember)
            {
                yield return data;
            }
        }
    }

    /// <summary>
    /// 初期パーティメンバーのデータを取得する。
    /// </summary>
    public IEnumerable<CharacterDataSO> GetInitialPartyMembers()
    {
        EnsureCacheInitialized();
        foreach (var data in _characters)
        {
            if (data != null && data.IsInitialPartyMember)
            {
                yield return data;
            }
        }
    }

    // === 内部メソッド ===

    private void EnsureCacheInitialized()
    {
        if (_cacheInitialized && _lookupCache != null) return;

        _lookupCache = new Dictionary<CharacterId, CharacterDataSO>();

        if (_characters == null) return;

        foreach (var data in _characters)
        {
            if (data == null) continue;

            var id = data.Id;
            if (!id.IsValid)
            {
                Debug.LogWarning($"CharacterDataRegistry: 無効なIDのキャラクターデータがあります: {data.name}");
                continue;
            }

            if (_lookupCache.ContainsKey(id))
            {
                Debug.LogWarning($"CharacterDataRegistry: 重複するID '{id}' があります。後のエントリを無視します。");
                continue;
            }

            _lookupCache[id] = data;
        }

        _cacheInitialized = true;
    }

    /// <summary>
    /// キャッシュを無効化する（エディタ用）。
    /// </summary>
    public void InvalidateCache()
    {
        _cacheInitialized = false;
        _lookupCache = null;
    }

    // === バリデーション ===

    private void OnValidate()
    {
        // エディタで変更があったらキャッシュを無効化
        InvalidateCache();

        // 重複チェック
        var seen = new HashSet<string>();
        for (int i = 0; i < _characters.Count; i++)
        {
            var data = _characters[i];
            if (data == null) continue;

            var idStr = data.Id.Value;
            if (string.IsNullOrEmpty(idStr)) continue;

            if (seen.Contains(idStr))
            {
                Debug.LogWarning($"CharacterDataRegistry: 重複するID '{idStr}' があります（インデックス {i}）");
            }
            else
            {
                seen.Add(idStr);
            }
        }
    }

    private void OnEnable()
    {
        // ロード時にキャッシュをクリア
        InvalidateCache();
    }

#if UNITY_EDITOR
    /// <summary>
    /// エディタ用: 全CharacterDataSOを自動収集する。
    /// </summary>
    [ContextMenu("Auto-collect Character Data")]
    private void AutoCollectCharacterData()
    {
        var guids = UnityEditor.AssetDatabase.FindAssets("t:CharacterDataSO");
        _characters.Clear();

        foreach (var guid in guids)
        {
            var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            var data = UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterDataSO>(path);
            if (data != null)
            {
                _characters.Add(data);
            }
        }

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"CharacterDataRegistry: {_characters.Count} 件のキャラクターデータを収集しました");
    }
#endif
}
