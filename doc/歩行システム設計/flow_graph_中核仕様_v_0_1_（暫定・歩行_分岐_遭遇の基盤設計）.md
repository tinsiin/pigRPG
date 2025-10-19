# FlowGraph 中核仕様 v0.1（暫定）

> **目的**: 「ステージ→エリア」という階層を前提にせず、**分岐（進行）**と**寄り道イベント（サイドオブジェクト）**、**敵/遭遇**をすべて**同じ器＝グラフ**で扱うための中核仕様。歩数や内部インデックスに依存しない“シームレス”挙動を最小コアで成立させ、将来の機能は DSL 拡張で吸収する。

---

## 0. スコープ / 非スコープ
- 本仕様は **歩行画面の進行・分岐・遭遇**の基盤を定める。戦闘の中身やUI詳細は非スコープ。
- **再現性（デバッグ用シード）**、**シームレス分岐**、**歩数非依存の遭遇**をコア要件とする。

---

## 1. コア概念
- **FlowGraph**: ノード（Node）とエッジ（Edge）からなる有向グラフ。世界の「場面」をノードで表す。
- **Node**: 1サイクル（歩行1回相当）で処理される“場面”。
  - **SideObjects**: その場面で選べる寄り道イベント（左右ペア提示を想定）。
  - **Encounters**: その場面での敵/中立者との“遭遇”候補テーブル。
- **Edge**: 遷移規則。`Auto / Conditional / Weighted / RandomSwitch / Choice` の5種。
- **Overlay**: 天候・警戒度・分岐モードなど **状況レイヤ**。SO/遭遇の重みや確率を乗算/抑制する。
- **DSL（Condition / Effect）**: 条件・副作用を小粒モジュールで合成。将来拡張は DSL の型を足すだけで吸収する。
- **GameState**: フラグ/タグ/カウンタ/訪問履歴/オーバーレイ/乱数シードを持つ軽量状態。

> 原則: **“場所＝ノード”“進行＝エッジ”**。グルーピングが必要ならタグ/Overlayで与える（物理階層に縛られない）。

---

## 2. データモデル（ScriptableObject）

### 2.1 FlowGraphSO
- `entryNodeId: string` … グラフ開始ノード
- `nodes: List<NodeSO>` … すべてのノード
- `edges: List<EdgeSO>` … 遷移一覧（from→to）
- `featureFlags: string[]` … リリース/ビルド切り替え用

### 2.2 NodeSO（抽象）
- `nodeId: string`
- `tags: string[]`
- `onEnter: EffectSO[]`, `onExit: EffectSO[]`
- `sideObjects: SideObjectTableSO`
- `encounters: EncounterTableSO`
- `uiHints: UIHint` … 背景/SE/演出ヒント

> ノードのサブタイプは任意（例: Hub/Gate/Terminal）。必須ではない。汎用Node + Edgeで表現できるのが理想。

### 2.3 EdgeSO
- `fromNodeId: string`, `toNodeId: string`
- `policy: Auto | Conditional | Weighted | RandomSwitch | Choice`
- `weight?: float`（Weighted/RandomSwitch）
- `uiLabel?: string`（Choice）
- `conditions?: ConditionSO[]`（Conditional/共通フィルタ）
- `priority?: int`（候補競合時の解決順）

> **既定直進**: `from`から出るエッジが1本で`Auto`なら UIなしで自動遷移。複数本を混在可能。

### 2.4 SideObjectTableSO / SideObjectSO
- **SideObjectTableSO**
  - `entries: List<(so: SideObjectSO, weight: float, conditions: ConditionSO[], fixedOnEnter?: bool, cooldownSteps?: int)>`
  - `varietyBias: VarietyBiasConfig`（同カテゴリ連発抑止/履歴長/減衰）
- **SideObjectSO**
  - `id: string, category: string, uiLabel: string`
  - `effects: EffectSO[]`
  - `exclusiveKey?: string, oneShot?: bool, cooldownSteps?: int`

### 2.5 EncounterTableSO / EncounterSO
- **EncounterTableSO**
  - `baseRate: float`（0〜1、**メモリレス遭遇率**）
  - `entries: List<(enc: EncounterSO, weight: float, conditions: ConditionSO[])>`
  - `varietyBias: VarietyBiasConfig`
  - `suppressionRules?: ConditionSO[]`（遭遇全体を抑止する条件）
- **EncounterSO**
  - `id: string, tags: string[]`
  - `resolver: EncounterResolver`（戦闘/会話/取引/分岐/複合を解決）
  - `onStart: EffectSO[]`, `onEnd: EffectSO[]`

> “中立的人間”は EncounterSO で表し、`resolver` によって戦闘に入らず会話・取引・分岐に落とせる。

### 2.6 OverlaySO
- `id: string, tags: string[]`
- `sideObjectMods: List<WeightMod>`（カテゴリ/ID単位で×倍率/禁止）
- `encounterMods: List<RateMod|WeightMod>`（遭遇率×倍率、個別重み補正）
- `onEnter: EffectSO[]`, `onExit: EffectSO[]`

> 例: `Night` で遭遇率×1.3、`Alert` で対人系重み+50%、`Branch-X` で特定SOのみ解放、など。

### 2.7 DSL（Condition / Effect）
- **Condition**: `HasFlag/HasItem/HasTag/Visited(node)/Chance(p)/OverlayActive(name)/TimeOfDay(...)` + 合成 `Not/And/Or`
- **Effect**: `SetFlag/UnsetFlag/AddTag/RemoveTag/PushOverlay/PopOverlay/UnlockEdge/LockEdge/Jump(node)/EmitEvent/IncCounter/DecCounter`

> 将来の機能は **DSLの型を追加**して注入する。**Node/Edge構造は変えない**のが重要。

---

## 3. ランタイム実行フロー（1サイクル）
1) **Enter(node)**: `onEnter`適用（Overlayのenterも含む）
2) **SideObjects**: テーブルから有効候補をフィルタ→重み付→**左右ペア抽選**→UI提示→選択→`effects`適用
3) **Encounters**: `p = Clamp01(baseRate × overlayMultipliers × flagMultipliers)` をロール。
   - 成立ならテーブルから重み抽選→`resolver`実行（戦闘/会話/他）。
   - **歩数/内部インデックス非依存**（メモリレス）。再現性はシードで保証。
4) **Transitions**: `edges[from==node]` を集め、下記優先で解決：
   - `Auto` → `Conditional`（成立したもの） → `Weighted` → `RandomSwitch` → `Choice`（UI）
   - 遷移不可ならエラー（またはフォールバック Jump）
5) **Exit(node)**: `onExit` 適用 → 次ノードへ

> **シームレス性**: 分岐がないノードは `Auto`一本で“直進”。分岐があるノードだけ UI/抽選が出現。

---

## 4. 設計原則 / 不変条件
- **歩数や座標の非表示**: ゲーム内で距離/歩数は露出しない（内部でカウンタを用いてもプレイヤーには見せない）。
- **再現性**: `seed = hash(runId, nodeId, stepIndex, buildVersion)` を基本にRNGを生成。
- **観測性**: デバッグモードで「候補→除外理由→最終重み→選出結果→遭遇率」を逐次ログ。
- **後方互換**: 未知のDSL型/フィールドは無視（データの前方互換を確保）。

---

## 5. オーサリング指針
- **分岐が1つだけ**: 分岐ノードに `Choice` を生やし、終端側に別のノード列を繋ぐ。
- **複数分岐/再分岐**: `Hub` 的ノードを挟むか、`Conditional`と`Weighted`を併用。
- **終端で“次章”分岐**: Terminal風ノードの `onExit` で `Jump(nextNode)` または外部フローへ接続。
- **中立的人間**: EncounterSOの `resolver = TalkOrCombat(prob)` などのプリセットで定義。
- **寄り道の強制**: SideObjectエントリ `fixedOnEnter=true` を使用（拠点/演出）。
- **反復抑止**: VarietyBias（履歴長=4, 同カテゴリ連続2回まで、等）を基本プリセットに。

---

## 6. テスト / 検証
- **GraphValidator**
  - 未接続ノード/孤立ノード検知
  - ループ検査（許容/禁止フラグ）
  - `Choice`のUIラベル欠落/重複警告
  - `Conditional`が全不成立になりうる分岐の警告
- **SimRunner**（エディタツール）
  - 任意ノード/シードで N ステップ実行し、候補/重み/遭遇率/遷移を可視化。

---

## 7. 例（ミニフロー）
```
A0 --Auto--> A1 --Conditional(keyA)--> B1 --Auto--> T1
                    └──（不成立）--> C1 --Auto--> T2

A0,A1: 直線
B1,C1: 分岐先。B1は城下、C1は裏路地。
T1,T2: 終端。onExitで次章の開始ノードへJump。
遭遇: baseRate=0.25、Night Overlayで×1.3
```

---

## 8. 最小 SO スケルトン（抜粋）
```csharp
public abstract class NodeSO : ScriptableObject {
  public string nodeId; public string[] tags;
  public List<EffectSO> onEnter, onExit;
  public SideObjectTableSO sideObjects;
  public EncounterTableSO encounters;
}

public enum EdgePolicy { Auto, Conditional, Weighted, RandomSwitch, Choice }

public class EdgeSO : ScriptableObject {
  public string fromNodeId, toNodeId; public EdgePolicy policy;
  public float weight; public string uiLabel; public List<ConditionSO> conditions;
  public int priority;
}
```

---

## 9. 移行/拡張
- 旧「ステージ→エリア」配列は、**各エリアをノード化して直列に接続**すれば等価に移行可能。分岐箇所だけ `Choice` を挿す。
- 将来の機能（天候、時間帯、名声、危険度、イベント門など）は **Overlay/DSL** を追加すれば注入可能。

---

## 10. リリース運用（例）
- `featureFlags` と `releaseTags` をノード/SideObject/Encounterに付与。ビルド時にフィルタリング。
- データは前方互換（未知フィールド無視）。古いバージョンでも落ちない。

---

### 付録A: サイドオブジェクト提示アルゴリズム（概要）
1. テーブル entries を条件/Overlayでフィルタ
2. 排他/oneShot/クールダウンを除外
3. VarietyBiasで重み再計算
4. Left = WeightedPick(pool) / Right = WeightedPick(pool \ Left)

### 付録B: 遭遇率（メモリレス）
`p = Clamp01( baseRate × Π overlayRates × Π flagRates )`
- 成立→ EncounterTable から WeightedPick
- 不成立→ Encounter無しで遷移処理へ

---

> **結語**: FlowGraph は「場所＝ノード」「進行＝エッジ」「状況＝オーバーレイ」「ロジック＝DSL」の四分割で、最小コアのまま長期拡張できる基盤である。これを“暫定中核”として実装を開始し、必要に応じて DSL 型を足していく。

