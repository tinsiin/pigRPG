using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// バトル中スキルUIの切り替えを管理するコンポーネント。
/// CharacterUIRegistry からSkillUIObjectを取得し、CharacterIdで切り替える。
/// </summary>
public class TabCharaStateContent : MonoBehaviour
{
    // 実行時のCharacterId→GameObjectマッピング
    private Dictionary<CharacterId, GameObject> _characterUIs;
    private CharacterId _currentCharacterId = CharacterId.None;
    private bool _initialized;

    /// <summary>現在表示中のCharacterId</summary>
    public CharacterId CurrentCharacterId => _currentCharacterId;

    /// <summary>CharacterUIRegistryへの参照</summary>
    private CharacterUIRegistry UIRegistry => CharacterUIRegistry.Instance;

    private void Start()
    {
        BuildSkillUIMap();
    }

    /// <summary>
    /// CharacterUIRegistry からSkillUIマップを構築する。
    /// </summary>
    private void BuildSkillUIMap()
    {
        _characterUIs = new Dictionary<CharacterId, GameObject>();

        var registry = UIRegistry;
        if (registry == null)
        {
            Debug.LogWarning("TabCharaStateContent: CharacterUIRegistry.Instance が null です");
            return;
        }

        foreach (var id in registry.AllCharacterIds)
        {
            var ui = registry.GetSkillUIObject(id);
            if (ui != null)
            {
                _characterUIs[id] = ui;
            }
        }

        _initialized = true;
        Debug.Log($"TabCharaStateContent: {_characterUIs.Count}キャラ分のSkillUIを登録しました");
    }

    /// <summary>
    /// UIマップを再構築する（シーン遷移なしで新キャラUI追加時に呼び出す）。
    /// </summary>
    public void RebuildSkillUIMap() => BuildSkillUIMap();

    public void SetActive(bool isActive)
    {
        gameObject.SetActive(isActive);
    }

    /// <summary>
    /// CharacterIdでコンテンツを切り替える。
    /// </summary>
    public void SwitchContent(CharacterId id)
    {
        EnsureInitialized();

        // 全UIを非表示
        foreach (var ui in _characterUIs.Values)
        {
            if (ui != null) ui.SetActive(false);
        }

        // 対象のUIを表示
        if (_characterUIs.TryGetValue(id, out var targetUI) && targetUI != null)
        {
            targetUI.SetActive(true);
            _currentCharacterId = id;
        }
        else
        {
            // UIが見つからない場合はエラー（フォールバックしない）
            Debug.LogError($"TabCharaStateContent.SwitchContent: {id} のUIが見つかりません。CharacterUIRegistryに登録されているか確認してください。");
            _currentCharacterId = CharacterId.None;
        }
    }

    /// <summary>
    /// CharacterIdのUIが登録されているか確認する。
    /// </summary>
    public bool HasCharacterUI(CharacterId id)
    {
        EnsureInitialized();
        return _characterUIs.ContainsKey(id);
    }

    /// <summary>
    /// 初期化を確認し、必要なら実行する。
    /// </summary>
    private void EnsureInitialized()
    {
        if (!_initialized || _characterUIs == null)
        {
            BuildSkillUIMap();
        }
    }
}
