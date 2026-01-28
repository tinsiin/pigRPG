# パーティー属性HP分岐マッピング

固定3人（Geino, Noramlia, Sites）のHP順序によるパーティー属性決定ロジック。

> **関連**: [パーティー管理システム拡張設計.md](./パーティー管理システム拡張設計.md)

---

## 1. 現在の3人ロジック

**ファイル**: `Assets/Script/Players/PartyBuilder.cs:63-101`

### 1.1. 許容誤差チェック（最優先）

3人のHPが「ほぼ同じ」場合、無条件で `MelaneGroup` を返す。

```csharp
float toleranceStair = Geino.MaxHP * 0.05f;
float toleranceSateliteProcess = Sites.MaxHP * 0.05f;
float toleranceBassJack = Noramlia.MaxHP * 0.05f;

if (Mathf.Abs(Geino.HP - Sites.HP) <= toleranceStair &&
    Mathf.Abs(Sites.HP - Noramlia.HP) <= toleranceSateliteProcess &&
    Mathf.Abs(Noramlia.HP - Geino.HP) <= toleranceBassJack)
{
    return PartyProperty.MelaneGroup;
}
```

**条件**: 各ペアのHP差が、高HP側の MaxHP × 5% 以内

### 1.2. HP順序による6通り分岐

HPが「ほぼ同じ」でない場合、3人のHP大小関係で決定。

| # | HP順序 | PartyProperty | 日本語名 |
|---|--------|---------------|----------|
| 1 | Geino ≥ Sites ≥ Noramlia | **MelaneGroup** | メレーンズ |
| 2 | Geino ≥ Noramlia ≥ Sites | **Odradeks** | オドラデクス |
| 3 | Sites ≥ Geino ≥ Noramlia | **MelaneGroup** | メレーンズ |
| 4 | Sites ≥ Noramlia ≥ Geino | **HolyGroup** | 聖戦 |
| 5 | Noramlia ≥ Geino ≥ Sites | **TrashGroup** | 馬鹿共 |
| 6 | Noramlia ≥ Sites ≥ Geino | **Flowerees** | 花樹 |

**フォールバック**: どの条件にも当てはまらない場合 → `MelaneGroup`

---

## 2. 視覚化: HP順序とパーティー属性

```
              Geino が最高HP
                   │
         ┌─────────┴─────────┐
         │                   │
    2番目: Sites        2番目: Noramlia
         │                   │
    MelaneGroup           Odradeks
    (G≥S≥N)               (G≥N≥S)


              Sites が最高HP
                   │
         ┌─────────┴─────────┐
         │                   │
    2番目: Geino        2番目: Noramlia
         │                   │
    MelaneGroup           HolyGroup
    (S≥G≥N)               (S≥N≥G)


              Noramlia が最高HP
                   │
         ┌─────────┴─────────┐
         │                   │
    2番目: Geino        2番目: Sites
         │                   │
    TrashGroup            Flowerees
    (N≥G≥S)               (N≥S≥G)
```

---

## 3. パーティー属性の分布

| PartyProperty | 出現パターン数 | HP順序パターン |
|---------------|----------------|----------------|
| MelaneGroup | 2 | G≥S≥N, S≥G≥N |
| Odradeks | 1 | G≥N≥S |
| HolyGroup | 1 | S≥N≥G |
| TrashGroup | 1 | N≥G≥S |
| Flowerees | 1 | N≥S≥G |

**傾向**:
- Geino または Sites が最高HPで、Noramlia が最低 → MelaneGroup
- Noramlia が最高HP → TrashGroup か Flowerees
- Sites が最高で Geino が最低 → HolyGroup
- Geino が最高で Sites が最低 → Odradeks

---

## 4. 2人用マッピング（要定義）

3人ロジックを参考に、2人の場合のマッピングを定義する必要がある。

### 4.1. Geino + Sites

| HP関係 | 3人の場合の可能性 | 提案 |
|--------|-------------------|------|
| Geino ≥ Sites | G≥S≥N → MelaneGroup, G≥N≥S → Odradeks | **[要定義]** |
| Sites ≥ Geino | S≥G≥N → MelaneGroup, S≥N≥G → HolyGroup | **[要定義]** |

### 4.2. Geino + Noramlia

| HP関係 | 3人の場合の可能性 | 提案 |
|--------|-------------------|------|
| Geino ≥ Noramlia | G≥S≥N → MelaneGroup, G≥N≥S → Odradeks, S≥G≥N → MelaneGroup | **[要定義]** |
| Noramlia ≥ Geino | S≥N≥G → HolyGroup, N≥G≥S → TrashGroup, N≥S≥G → Flowerees | **[要定義]** |

### 4.3. Sites + Noramlia

| HP関係 | 3人の場合の可能性 | 提案 |
|--------|-------------------|------|
| Sites ≥ Noramlia | G≥S≥N → MelaneGroup, S≥G≥N → MelaneGroup, S≥N≥G → HolyGroup | **[要定義]** |
| Noramlia ≥ Sites | G≥N≥S → Odradeks, N≥G≥S → TrashGroup, N≥S≥G → Flowerees | **[要定義]** |

---

## 5. 2人マッピング記入欄

以下を埋めてください:

```
Geino + Sites:
  Geino ≥ Sites  → メレーンズ
  Sites ≥ Geino  → 花樹

Geino + Noramlia:
  Geino ≥ Noramlia  → メレーンズ
  Noramlia ≥ Geino  → オドラデクス

Sites + Noramlia:
  Sites ≥ Noramlia  → 聖戦
  Noramlia ≥ Sites  → 馬鹿
```

---

## 6. キャラクター特性とHP分岐の考察

### 6.1. キャラクター象徴

各キャラクターのクラス名から読み取れる象徴的意味:

| キャラクター | クラス名 | 象徴 |
|-------------|----------|------|
| **Geino** | StairStates (階段) | 段階的進行、構造、秩序、安定性 |
| **Noramlia** | BassJackStates | 低周波・ベース音、カオス、予測不能さ、本能的 |
| **Sites** | SateliteProcessStates (衛星) | 外部視点、俯瞰、技術的観点、距離感 |

### 6.2. パーティー属性の意味

```
TrashGroup   = 馬鹿共（愚か、目的なき混沌）
HolyGroup    = 聖戦（必死、目的使命、外的使命に駆られる）
MelaneGroup  = メレーンズ（王道的、バランスが取れている）
Odradeks     = オドラデクス（秩序から離れてる、内に閉じた捻れ）
Flowerees    = 花樹（オサレ、美意識のあるカオス）
```

### 6.3. 各HP順序の考察

#### MelaneGroup（G≥S≥N / S≥G≥N）
**共通パターン**: Noramlia（カオス）が常に最低HP

- Geinoの秩序とSitesの外部視点が健在で、Noramliaのカオス要素が抑制されている
- 構造と俯瞰のバランスが取れた「王道的」なパーティー状態
- **要約**: カオスが弱いと正統派になる

#### Odradeks（G≥N≥S）
**パターン**: Geino最高、Sites最低

- Geino（秩序）がリーダーだが、Sites（外部視点）が最も弱い
- Noramlia（カオス）がSitesより強いため、秩序はあるが**外への目が閉じている**
- 結果として「秩序から離れてコロコロと転がる」内向きの捻れた状態
- **要約**: 秩序はあるが俯瞰がないと内に閉じる

#### HolyGroup（S≥N≥G）
**パターン**: Sites最高、Geino最低

- Sites（外部視点/使命）がリードし、Geino（構造/安定）が最も苦しんでいる
- 外的目標に駆り立てられるが、足元の構造が脆弱な「必死」の状態
- 目的使命が先行し、自己の安定を犠牲にする「聖戦」モード
- **要約**: 外への使命があり内の安定がないと必死になる

#### TrashGroup（N≥G≥S）
**パターン**: Noramlia最高、Sites最低

- Noramlia（カオス）が支配的、Sites（外部視点）が欠如
- Geino（秩序）がある程度残っているが、外からの視点なしにカオスが暴走
- 目的や方向性のない「馬鹿騒ぎ」状態
- **要約**: カオスがあり俯瞰がないと馬鹿になる

#### Flowerees（N≥S≥G）
**パターン**: Noramlia最高、Geino最低

- Noramlia（カオス）が支配的だが、Sites（外部視点）も健在
- Geino（構造/安定）が最も弱い
- カオスに外部意識（美意識）が加わり、構造に縛られない「オサレ」なスタイル
- **要約**: カオスがあり俯瞰もあるがルールがないとオサレになる

### 6.4. 法則のまとめ

```
┌────────────────────────────────────────────────────────────────┐
│                    HP順序とパーティー属性の法則                  │
├────────────────────────────────────────────────────────────────┤
│ ・Noramliaが最低 → 王道（MelaneGroup）                         │
│ ・Sitesが最低 + Geinoが最高 → 内向きの捻れ（Odradeks）          │
│ ・Sitesが最低 + Noramliaが最高 → 馬鹿騒ぎ（TrashGroup）        │
│ ・Geinoが最低 + Sitesが最高 → 必死の聖戦（HolyGroup）          │
│ ・Geinoが最低 + Noramliaが最高 → オサレ（Flowerees）           │
├────────────────────────────────────────────────────────────────┤
│ 【キー】                                                        │
│  - Noramlia（カオス）の強さ → 混沌度                           │
│  - Sites（外部視点）の有無 → 目的意識・美意識の有無             │
│  - Geino（秩序）の有無 → 構造・安定性の有無                     │
└────────────────────────────────────────────────────────────────┘
```

---

## 7. 実装コード（参考）

```csharp
// PartyBuilder.cs:63-101
private PartyProperty GetPartyImpression()
{
    float toleranceStair = Geino.MaxHP * 0.05f;
    float toleranceSateliteProcess = Sites.MaxHP * 0.05f;
    float toleranceBassJack = Noramlia.MaxHP * 0.05f;

    // 許容誤差チェック
    if (Mathf.Abs(Geino.HP - Sites.HP) <= toleranceStair &&
        Mathf.Abs(Sites.HP - Noramlia.HP) <= toleranceSateliteProcess &&
        Mathf.Abs(Noramlia.HP - Geino.HP) <= toleranceBassJack)
    {
        return PartyProperty.MelaneGroup;
    }

    // HP順序分岐
    if (Geino.HP >= Sites.HP && Sites.HP >= Noramlia.HP)
    {
        return PartyProperty.MelaneGroup;
    }
    else if (Geino.HP >= Noramlia.HP && Noramlia.HP >= Sites.HP)
    {
        return PartyProperty.Odradeks;
    }
    else if (Sites.HP >= Geino.HP && Geino.HP >= Noramlia.HP)
    {
        return PartyProperty.MelaneGroup;
    }
    else if (Sites.HP >= Noramlia.HP && Noramlia.HP >= Geino.HP)
    {
        return PartyProperty.HolyGroup;
    }
    else if (Noramlia.HP >= Geino.HP && Geino.HP >= Sites.HP)
    {
        return PartyProperty.TrashGroup;
    }
    else if (Noramlia.HP >= Sites.HP && Sites.HP >= Geino.HP)
    {
        return PartyProperty.Flowerees;
    }

    return PartyProperty.MelaneGroup;
}
```

---

## 更新履歴

| 日付 | 内容 |
|------|------|
| 2026-01-27 | 初版（3人HP分岐ロジック抽出、2人用マッピング記入欄追加） |
| 2026-01-27 | キャラクター特性とHP分岐の考察（セクション6）追加 |
