# スキル ↔ ビジュアルエフェクト 統合（実装完了）

## 1. 目的

バトル中のスキル発動時に、既存のエフェクトシステム（`EffectManager`）のエフェクトを自動再生する。
スキルにエフェクトが未設定でもエラーにならない（null安全）。

---

## 2. 設計: 3スロット方式

スキル1つにつき、最大3つのエフェクトスロットを持つ。

| スロット | フィールド名 | 再生先 | 用途例 |
|---------|-------------|--------|--------|
| 術者エフェクト | `CasterEffectName` | 術者の `BattleIconUI` | 詠唱光、集中の波動 |
| 対象エフェクト | `TargetEffectName` | 各ターゲットの `BattleIconUI` | 斬撃、爆発、回復光 |
| フィールドエフェクト | `FieldEffectName` | `ViewportArea` 全体 | 地震、全体魔法の背景 |

- 全て `string` 型（エフェクトJSON名、拡張子なし）
- null / 空文字 → そのスロットはスキップ（エラーなし）
- 3スロット全て同時に指定可能（同時再生）
- 対象エフェクトは **全ターゲットに対して** それぞれ再生される

### エフェクトJSONとの対応

| スロット | エフェクトJSON の target | 再生API |
|---------|------------------------|---------|
| `CasterEffectName` | `"icon"` | `EffectManager.Play(name, acter.BattleIcon)` |
| `TargetEffectName` | `"icon"` | `EffectManager.Play(name, target.BattleIcon)` ×各対象 |
| `FieldEffectName` | `"field"` | `EffectManager.PlayField(name)` |

---

## 3. 変更ファイル一覧

### 3.1 `SkillLevelData` — フィールド追加

**ファイル**: `Assets/Script/BaseSkill/BaseSkill.SkillLevel.cs`

```csharp
// ─── ⑧ ビジュアルエフェクト ───
[EffectName] public string CasterEffectName;
[EffectName] public string TargetEffectName;
[EffectName] public string FieldEffectName;
```

- `Clone()` にもコピー処理を追加済み
- `[EffectName]` 属性によりInspectorでドロップダウン選択UI表示

### 3.2 `BaseSkill.Core.cs` — プロパティアクセサ追加

```csharp
public string CasterEffectName => FixedSkillLevelData[_levelIndex].CasterEffectName;
public string TargetEffectName => FixedSkillLevelData[_levelIndex].TargetEffectName;
public string FieldEffectName  => FixedSkillLevelData[_levelIndex].FieldEffectName;
```

### 3.3 `SkillExecutor.cs` — エフェクト再生

**ファイル**: `Assets/Script/Battle/CoreRuntime/SkillExecutor.cs`

`SkillACT()` 内、`ResolveSkillEffectsAsync` 呼び出しの直前で `PlaySkillVisualEffects(skill)` を呼び出し。
fire-and-forget（awaitしない）。

### 3.4 Inspector — ドロップダウン選択UI

| ファイル | 役割 |
|---------|------|
| `Assets/Script/BaseSkill/EffectNameAttribute.cs` | `[EffectName]` PropertyAttribute定義 |
| `Assets/Editor/Effects/EffectNameDrawer.cs` | `Assets/Resources/Effects/*.json` を走査してドロップダウン表示 |
| `Assets/Editor/SkillLevelDataDrawer.cs` | セクション⑧「ビジュアルエフェクト」を追加（3フィールド表示） |

### 3.5 循環参照の修正

Phase 2リファクタリングで `SpecialFlags` が `_levelIndex` 経由になったことで、
`_levelIndex → _nowSkillLevel → IsTLOA → SpecialFlags → _levelIndex → ∞` の循環参照が発生していた。

**修正箇所**:
- `BaseSkill.SkillLevel.cs` の `_nowSkillLevel` — `IsTLOA` の代わりに `FixedSkillLevelData[0].SpecialFlags` を直接参照
- `NormalEnemy.cs` の `_nowSkillLevel` override — 同様の修正

---

## 4. null安全の保証

| ケース | 挙動 |
|--------|------|
| 3スロット全て未設定（null/空） | 何も再生されない |
| 一部のスロットのみ設定 | 設定されたスロットのみ再生 |
| BattleIconUI が null | スキップ |
| 存在しないエフェクト名を指定 | `EffectManager` がエラーログを出すが例外は投げない |
| FieldEffectLayer がシーンにない | `EffectManager` がエラーログを出すが例外は投げない |

---

## 5. 変更しないもの

| 対象 | 理由 |
|------|------|
| `EffectManager` | 既存APIで十分。変更不要 |
| `EffectDefinition` / `KfxCompiler` | エフェクト側の仕様は変更なし |
| `EffectResolver` / `SkillEffectPipeline` | メカニカル効果のパイプライン。ビジュアルとは独立 |
| `BattleIconUI` | 既に `EffectLayer` を自動生成する仕組みがある |
| エフェクトJSON | 既存エフェクトはそのまま使える |

---

## 6. 将来の拡張余地

- **タイミング制御**: 現在は「ダメージ計算と同時」固定だが、将来的にはディレイや「ダメージ後」のタイミングを持たせることも可能
- **連撃ごとのエフェクト切替**: `MoveSet` にエフェクト名を持たせれば、連撃の各ヒットで異なるエフェクトを出せる
- **エフェクト完了待ち**: 演出重視の場面では `EffectPlayer.OnComplete` を使って完了を待つことも可能
