# TenDayValues 参照リファクタリング計画

## 問題

`BaseStates.TenDayValues(bool IsSkillEffect)` は `true`/`false` のbool引数で武器ボーナスの適用範囲を制御しているが、以下の問題がある:

### 1. 可読性の欠如
```csharp
Atker.TenDayValues(true)   // ← 何が true？ 読めない
TenDayValues(false)         // ← 何が false？ 読めない
```
100箇所以上の呼び出しが全てマジックブールで、コードレビュー時に意図が読み取れない。

### 2. 隠れた状態依存
```csharp
// NowUseSkill が変わると TenDayValues(true) の結果も変わる
character.NowUseSkill = bladeSkill;
character.TenDayValues(true);  // → BladeData が乗る

character.NowUseSkill = magicSkill;
character.TenDayValues(true);  // → MagicData が乗る（同じ true なのに結果が違う）
```
`TenDayValues(true)` は内部で `NowUseSkill` を暗黙参照しており、呼び出しタイミングによって結果が変わる。

### 3. 引数の意味が名前と一致しない
パラメータ名は `IsSkillEffect` だが、実際には「武器のスキル種別ボーナスを適用するか」を制御している。「スキルエフェクト」という名前は誤解を招く。

---

## 現在の仕組み

### メソッド定義（`BaseStates.StatesNumber.cs:682`）

```csharp
public ReadOnlyIndexTenDayAbilityDictionary TenDayValues(bool IsSkillEffect)
{
    var IsBladeSkill = false;
    var IsMagicSkill = false;
    var IsTLOASkill = false;
    if (NowUseSkill != null && IsSkillEffect)
    {
        IsBladeSkill = NowUseSkill.IsBlade;
        IsMagicSkill = NowUseSkill.IsMagic;
        IsTLOASkill = NowUseSkill.IsTLOA;
    }
    var weaponBonus = (NowUseWeapon != null)
        ? NowUseWeapon.TenDayBonusData.GetTenDayAbilityDictionary(IsBladeSkill, IsMagicSkill, IsTLOASkill)
        : new TenDayAbilityDictionary();
    var result = _baseTenDayValues + weaponBonus;
    return new ReadOnlyIndexTenDayAbilityDictionary(result);
}
```

### 武器ボーナスの構造（`BaseWeapon.TenDayBonusData`）

| 辞書 | 適用条件 |
|------|----------|
| `NormalData` | **常に**加算（`true` でも `false` でも） |
| `BladeData` | `IsSkillEffect=true` かつスキルが刃物の時 |
| `MagicData` | `IsSkillEffect=true` かつスキルが魔法の時 |
| `TLOAData` | `IsSkillEffect=true` かつスキルがTLOAの時 |

### 実質的な意味

| 引数 | 返す値 | 意味 |
|------|--------|------|
| `true` | `_baseTenDayValues + NormalData + スキル種別ボーナス` | スキル行使中の自分 |
| `false` | `_baseTenDayValues + NormalData` | 素の自分（武器基本ボーナスのみ） |

---

## 全参照箇所

### TenDayValues(true) — 15箇所「スキル行使中の自分」

主に **攻撃者側のダメージ計算・判定** で使用。

| ファイル | 行 | 対象 | 用途 |
|----------|-----|------|------|
| `BaseStates.StatesNumber.cs` | 717 | self | ランダム十日能力選択 |
| `BaseStates.StatesNumber.cs` | 827,834 | self | トップ十日能力の検索 |
| `BaseStates.StatesNumber.cs` | 886 | self | 自信ブースト計算（馬鹿） |
| `BaseStates.StatesNumber.cs` | 1211 | Atker | ダメージ計算のマッチ合計 |
| `BaseStates.VitalLayerManager.cs` | 107 | atker | 重撃ダメージ（ケレケレ） |
| `BaseStates.Damage.cs` | 43 | Atker | 即死クリティカル閾値（刃物） |
| `BaseStates.Damage.cs` | 472 | Atker | 精神ダメージ計算（余裕） |
| `BaseStates.Damage.cs` | 653 | Atker | 精神ダメージ計算（余裕・別経路） |
| `BaseStates.BattleEvent.cs` | 578 | Atker | 刃物カウンター発動（刃物） |
| `BaseStates.BattleEvent.cs` | 686-726 | self | フロー最大値算出（多数の十日能力） |
| `BaseStates.BattleEvent.cs` | 821,828-830 | attacker | 割り込みカウンター（ヴォンド等） |
| `BaseStates.ReactionSkill.cs` | 216 | Attacker | 命中補正（ケレケレ） |
| `BaseStates.ReactionSkill.cs` | 224 | Defender | 命中補正（ケレケレ） |
| `Passive/Upper.cs` | 11 | _owner | パッシブ効果（烈火） |
| `Battle/Effects/FlatRozeEffect.cs` | 66 | acter | エフェクト効果（泉水） |

#### 精神状態遷移（`BaseStates.StatesState.cs`）— 大量

自分の精神状態遷移の確率計算で `TenDayValues(true)` を多用。
該当行: 1379, 1380, 1404, 1464, 1465, 1510, 1564, 1577, 1600, 1612, 1680, 1700, 1712, 1724, 1735, 1746, 1747, 1759, 1814, 1816, 1829, 1840, 1853, 1866, 1927, 1961, 1973, 1988, 1991, 1994, 2005, 2011, 2023, 2025, 2034, 2045, 2074, 2077, 2083, 2115, 2119, 2122, 2136, 2238, 2269, 2394

**パターン**: 自分の値は `TenDayValues(true)`、敵の値は `ene.TenDayValues(false)`

---

### TenDayValues(false) — 76箇所「素の自分」

主に **防御者・比較対象・パッシブ条件・UI表示** で使用。

#### 戦闘ロジック

| ファイル | 行 | 対象 | 用途 |
|----------|-----|------|------|
| `BattleGroup.cs` | 168 | target | サイレント練度（パッシブ条件） |
| `BattleGroup.cs` | 169 | attacker | ピルマグレイトフル（パッシブ条件） |
| `BattleGroup.cs` | 170 | target | 星テルシ（パッシブ条件） |
| `BattleGroup.cs` | 171 | attacker | 星テルシ（パッシブ条件） |
| `BattleGroup.cs` | 172 | target | 天と終戦（パッシブ条件） |
| `BattleGroup.cs` | 281 | Doer | ピルマグレイトフル（AGI減算補正） |
| `BattleManager.cs` | 369-385 | nowVanguard/newVanguard | 前衛交代時の防御/攻撃力比較（9箇所） |
| `BaseStates.Damage.cs` | 44-45 | self | 被クリティカル閾値（防御側） |
| `BaseStates.Damage.cs` | 472,653 | self | 精神ダメージの防御側余裕 |
| `BaseStates.ReactionSkill.cs` | 980 | self | 成長記録用の総量 |
| `BaseStates.ReactionSkill.cs` | 1117 | self | 強さ比率計算 |
| `TargetingService.cs` | 445-447 | vanguard/attacker | 前衛プレッシャー判定 |

#### 精神状態遷移（敵の値を参照）

`BaseStates.StatesState.cs` 内で `ene.TenDayValues(false)` として使用。
該当行: 1377, 1378, 1403, 1462, 1463, 1511, 1680, 1746, 1747, 1813, 1815, 1972, 2023, 2269

#### パッシブ

| ファイル | 行数 | 対象 | 用途 |
|----------|------|------|------|
| `Passive/Upper.cs` | 16,17,22,23 | _owner | ATK/AGI/DEF修正 |
| `Passive/Slaim.cs` | 16,35,36,52-59 | user/_owner/各キャラ | サイレント練度関連（11箇所） |
| `Passive/Slaim2.cs` | 52,53,64,69-73 | Attacker/Slaimer | シャッフル判定（7箇所） |
| `Passive/Raitistinian_MetaDodge.cs` | 12,13 | _owner | DEF修正（スマイラー+ドクマムシ） |
| `Passive/ForcedDowner.cs` | 10-14 | _owner | AGIペナルティ（4箇所） |

#### UI表示

| ファイル | 行 | 対象 | 用途 |
|----------|-----|------|------|
| `TenDaysMordaleAreaController.cs` | 507 | actor | 十日能力一覧表示 |
| `TenDaysMordaleAreaController.cs` | 690 | actor | 共通DEF計算 |
| `TenDayAbilityHorizontalBarsView.cs` | 151 | actor | バーグラフ表示 |

---

### TenDayValuesSum() — 14箇所

| ファイル | 行 | 対象 | 引数 | 用途 |
|----------|-----|------|------|------|
| `BattleGroup.cs` | 56 | chara | **変数** | パーティ平均パワー |
| `BattleGroup.cs` | 68 | chara | **変数** | パーティ合計パワー |
| `BaseStates.StatesState.cs` | 666 | self | false | 戦闘開始時条件 |
| `BaseStates.StatesNumber.cs` | 936 | target | false | 自信ブースト（相手強さ比） |
| `BaseStates.StatesNumber.cs` | 936 | self | true | 自信ブースト（自分強さ） |
| `BaseStates.StatesNumber.cs` | 1246 | self | false | レゾナンス基礎（56%） |
| `BaseStates.BattleEvent.cs` | 325 | atker/self | true/false | 死亡イベント強さ比較 |
| `BaseStates.Damage.cs` | 913 | Atker/self | false/false | レゾナンスダメージ比率 |
| `BaseStates.ReactionSkill.cs` | 980 | self | false | 成長記録 |
| `BaseStates.ReactionSkill.cs` | 1117 | self | true | 強さ比率計算 |
| `Passive/Slaim2.cs` | 64 | Attacker | true | Slaim2パッシブ計算 |

---

## リファクタリング方針

### 案: メソッド名分離 + スキル明示渡し

```csharp
// 現在
character.TenDayValues(true)
character.TenDayValues(false)

// リファクタリング後
character.TenDayValuesForSkill(skill)   // スキル種別ボーナス込み（スキルを明示渡し）
character.TenDayValuesBase()            // 武器NormalDataのみ（素の能力）
```

#### メリット
- **可読性**: 呼び出し箇所で意図が自明
- **状態依存の除去**: `NowUseSkill` の暗黙参照がなくなり、スキルを明示的に渡す
- **安全性**: 引数の取り違えが構造的に不可能

#### 移行手順
1. 新メソッド `TenDayValuesForSkill(BaseSkill skill)` と `TenDayValuesBase()` を追加
2. 旧メソッドに `[Obsolete]` を付与
3. `TenDayValues(false)` → `TenDayValuesBase()` に置換（76箇所 — 機械的に可能）
4. `TenDayValues(true)` → `TenDayValuesForSkill(skill)` に置換（15箇所 + StatesState大量 — スキル参照元の確認が必要）
5. `TenDayValuesSum` も同様に分離
6. テスト実行・動作確認
7. 旧メソッド削除

### 注意点

- **StatesState.cs の精神状態遷移**: `TenDayValues(true)` が大量（46行）にあり、全てで `NowUseSkill` の暗黙参照を使っている。リファクタリング時にスキル参照元を明確にする必要がある
- **パッシブ**: 全て `false` なので機械的置換で済む
- **BattleGroup の変数引数**: `IsSkillEffect` パラメータを受け取って透過しているため、呼び出し元まで遡って分岐する必要がある
- **UI表示**: 全て `false` なので機械的置換で済む

### 影響範囲サマリ

| カテゴリ | 箇所数 | 難易度 | 備考 |
|----------|--------|--------|------|
| `false` → `TenDayValuesBase()` | 76 | 低 | 機械的置換 |
| `true` → `TenDayValuesForSkill(skill)` (戦闘ロジック) | 15 | 中 | スキル参照元の確認必要 |
| `true` (精神状態遷移) | 46 | 中 | 大量だが全て同じパターン |
| `TenDayValuesSum` | 14 | 低〜中 | 同じ方針で分離 |
| 変数引数（BattleGroup） | 2 | 中 | 呼び出し元の分岐検討 |
| **合計** | **153** | | |
