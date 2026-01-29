using System;
using UnityEngine;

/// <summary>
/// キャラクター識別子。
/// Unity シリアライズ対応済み。デシリアライズ時に小文字正規化。
/// </summary>
[Serializable]
public struct CharacterId : IEquatable<CharacterId>, ISerializationCallbackReceiver
{
    [SerializeField] private string _value;

    public string Value => _value ?? string.Empty;

    public CharacterId(string value)
    {
        // 小文字に正規化して大文字小文字の不一致を防ぐ
        _value = value?.ToLowerInvariant() ?? string.Empty;
    }

    // === ISerializationCallbackReceiver ===

    public void OnAfterDeserialize()
    {
        // Inspector や JSON から読み込んだ値を小文字に正規化
        _value = _value?.ToLowerInvariant() ?? string.Empty;
    }

    public void OnBeforeSerialize()
    {
        // シリアライズ前は特に処理なし
    }

    // === 既存キャラの静的定義 ===

    public static readonly CharacterId Geino = new("geino");
    public static readonly CharacterId Noramlia = new("noramlia");
    public static readonly CharacterId Sites = new("sites");

    /// <summary>無効なID</summary>
    public static readonly CharacterId None = new(string.Empty);

    /// <summary>有効なIDかどうか</summary>
    public bool IsValid => !string.IsNullOrEmpty(_value);

    // === 比較・ハッシュ ===

    public bool Equals(CharacterId other) => Value == other.Value;
    public override bool Equals(object obj) => obj is CharacterId id && Equals(id);
    public override int GetHashCode() => Value?.GetHashCode() ?? 0;
    public override string ToString() => Value;

    public static bool operator ==(CharacterId left, CharacterId right) => left.Equals(right);
    public static bool operator !=(CharacterId left, CharacterId right) => !left.Equals(right);

    // === 暗黙変換（デバッグ用） ===

    public static implicit operator string(CharacterId id) => id.Value;

    /// <summary>固定メンバー（Geino, Noramlia, Sites）かどうか</summary>
    public bool IsOriginalMember =>
        this == Geino || this == Noramlia || this == Sites;
}
