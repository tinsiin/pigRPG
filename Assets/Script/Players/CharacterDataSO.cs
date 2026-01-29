using UnityEngine;

/// <summary>
/// キャラクターの初期データを定義するScriptableObject。
/// 新キャラクター追加時はこのアセットを作成し、CharacterDataRegistryに登録する。
///
/// 設計方針:
/// - _template（AllyClass）が全てのキャラクターデータを保持する
/// - CharacterDataSOは識別情報と初期パーティ設定のみ管理
/// - 元のInit_geinoと同じデータ構造を維持
/// </summary>
[CreateAssetMenu(menuName = "Character/Character Data", fileName = "NewCharacterData")]
public sealed class CharacterDataSO : ScriptableObject
{
    [Header("識別情報")]
    [Tooltip("キャラクターID（小文字英数字、例: geino, noramlia, sites）")]
    [SerializeField] private string _id;

    [Header("初期パーティ設定")]
    [Tooltip("ゲーム開始時にパーティに含めるかどうか")]
    [SerializeField] private bool _isInitialPartyMember;

    [Header("キャラクターデータ")]
    [Tooltip("AllyClassのテンプレート。元のInit_geinoと同じデータを入力する。")]
    [SerializeField] private AllyClass _template;

    // === プロパティ ===

    /// <summary>キャラクターID</summary>
    public CharacterId Id => new CharacterId(_id);

    /// <summary>テンプレートAllyClass</summary>
    public AllyClass Template => _template;

    /// <summary>テンプレートが設定されているか</summary>
    public bool HasTemplate => _template != null;

    /// <summary>固定メンバー（Geino/Noramlia/Sites）かどうか</summary>
    public bool IsOriginalMember => Id.IsOriginalMember;

    /// <summary>初期パーティメンバーかどうか</summary>
    public bool IsInitialPartyMember => _isInitialPartyMember;

    // === バリデーション ===

    private void OnValidate()
    {
        // IDを小文字に正規化
        if (!string.IsNullOrEmpty(_id))
        {
            var normalized = _id.ToLowerInvariant();
            if (_id != normalized)
            {
                _id = normalized;
            }
        }
    }

    /// <summary>
    /// テンプレートからAllyClassのディープコピーを作成する。
    /// テンプレートがない場合はnullを返す。
    /// </summary>
    public AllyClass CreateInstance()
    {
        if (_template == null)
        {
            Debug.LogWarning($"CharacterDataSO.CreateInstance: {_id} にテンプレートが設定されていません");
            return null;
        }

        var instance = _template.DeepCopy();
        instance.SetCharacterId(Id);

        return instance;
    }
}
