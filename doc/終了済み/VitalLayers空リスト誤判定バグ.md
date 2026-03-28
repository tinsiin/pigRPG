# VitalLayers空リスト誤判定バグ

## 発見日: 2026-03-28

## 概要

`BasePassive.UpdateTurnSurvival` の VitalLayer 生存条件チェックにおいて、VitalLayer バインディングを持たないパッシブが「生存条件のVitalLayerが全滅した」と誤判定され、次のターン遷移時に即座に除去されるバグ。

## 影響範囲

`PassiveManager.GetAtID()` → `DeepCopy()` で生成された全パッシブに潜在的に影響する。ただし、インスペクタ経由で直接キャラクターに登録されたパッシブ（Unity のシリアライズで `VitalLayers` が `null` になるもの）は影響を受けない。

スキル吸引パッシブ（`AttractionPassive`）の実装テスト中に発見。

## 根本原因

### フィールド初期化

```csharp
// BasePassive.cs L246
public List<PassiveVitalLayerBinding> VitalLayers = new();
```

フィールド初期化子 `= new()` により、`new` で生成されるインスタンスは常に**空リスト**（`null` ではない）を持つ。`DeepCopy()` 内でも `new` が呼ばれるため、コピー先も空リストになる。

### 誤判定のロジック

```csharp
// BasePassive.cs UpdateTurnSurvival 内（修正前）
if (VitalLayers != null)  // 空リストでも true
{
    if (!HasRemainingSurvivalVitalLayer(user))  // 空リスト → false
    {
        user.RemovePassive(this);  // 誤除去！
    }
}
```

`HasRemainingSurvivalVitalLayer` の内部:

1. `VitalLayers` から `IsSurvivalCondition == true` のバインディングを抽出
2. 0件の場合 → `null` を返す
3. 呼び出し元: `null != null && Count > 0` → `false`

結果: VitalLayer を一切使っていないパッシブが「生存条件が全滅した」と解釈される。

### なぜ今まで顕在化しなかったか

インスペクタ経由で `PassiveManager._masterList` に登録されたテンプレートは、Unity のシリアライズによって `VitalLayers` フィールドが実際の状態で保持される。VitalLayer を設定していないテンプレートでは、Unity のシリアライズが空リストとして保持するか `null` にするかはバージョンや設定による。

`ApplyPassiveByID` → `DeepCopy()` で生成されるパッシブのみ、`new` の初期化子が走って空リストが確実に生成されるため、この経路でのみ問題が顕在化する。従来の `ApplyPassiveByID` 経由のパッシブが影響を受けていなかったのは、それらのパッシブが実際に VitalLayer バインディングを持っていたか、あるいはテンプレート側の `VitalLayers` が `null` のまま DeepCopy されていた可能性がある。

## 修正内容

```csharp
// BasePassive.cs UpdateTurnSurvival 内（修正後）
if (VitalLayers != null && VitalLayers.Count > 0)
{
    if (!HasRemainingSurvivalVitalLayer(user))
    {
        user.RemovePassive(this);
    }
}
```

`Count > 0` の条件を追加し、空リスト（= VitalLayer を使用していないパッシブ）では生存条件チェック自体をスキップするようにした。

## 発見経緯

1. スキル吸引パッシブ（`AttractionPassive`）をスキルヒット時に `DeepCopy()` → `ApplyPassive()` で付与
2. パッシブは正常に付与されるが、次のターン遷移時に即座に除去される
3. `RemovePassive` にスタックトレースを仕込んで特定
4. `UpdateTurnSurvival` → VitalLayer 生存条件チェック → 空リスト誤判定が原因と判明

## 関連ファイル

| ファイル | 変更内容 |
|---------|---------|
| `Assets/Script/BasePassive.cs` L570 | `VitalLayers != null` → `VitalLayers != null && VitalLayers.Count > 0` |
