# Event Kernel & 拡張可能イベント機構 設計ドキュメント v0.2

## このドキュメントで実装したいこと（かんたん概要）
本ドキュメントは、**イベント門／サイドオブジェクト／中立的敵オブジェクト／ランダムエンカウント**のどこにでも“同じ作り”で**イベントを宿せる**ようにするための、**共通コア（Event Kernel）**を定義します。狙いは「イベントの実装をばらけさせず、将来いくらでも種類を増やしても**壊れず**・**迷子にならず**・**テストしやすい**」状態を作ることです。

**やりたいこと**
- イベントの本体を **EventDefinitionSO + EventStep + EventRunner** に集約し、**結果は Effect（Flag/Tag/Counter/Jump など）**だけに落とす。
- 宿り先（門/サイド/遭遇/ランダム）は薄い **EventHost** で統一トリガを持ち、**EmitEvent** 一発で起動できる。
- 遭遇（戦闘系）は **Outcome Hooks（Win/Escape/Lose/Purchase）**を用意して、通常終了と“明示イベント”の両立を可能にする。
- **release/feature flags** でリリースごとの出し分けに対応（段階的に出して安全に拡張）。
- 将来は **Timeline 併走**や推理/RPGミニゲームも、**Step/Condition/Effect の追加**だけで入れられる。

**これで得られるもの**
- 追加しやすい（新しいイベントは **Step/Effect を足すだけ**）
- 壊れにくい（**Node/Edge/Resolver は不変**、共通の出力口＝Effect に統一）
- 再現性・検証性（seed固定／差分ログ）、運用性（一元管理・出し分け）

**具体例**
- ランダムエンカ敗北時は既定で**ジーノ搬送**（Jump+Heal）。ただし特定の敵では **Outcome Hook** が上書きされ、**全回復＆見逃しのノベル**を挿入。
- **イベント門**は“固定歩数で出現”を **Condition** で表現し、通過時に **UnlockEdge/SetFlag** を適用。
- **サイドオブジェクト**は選択で **EmitEvent** によるノベル／推理パートを起動し、最後に Flag/Tag/Counter だけを残す。

**使い方（最短）**
1. 宿り先に **EventHost** を付ける（どこでもOK）。
2. **EventDefinitionSO** を作り、必要な **EventStep**（Dialog/Puzzle/CombatHandoff 等）を並べる。
3. 終了結果は **Effect** に畳む（Flag/Tag/Counter/Jump など）。
4. 戦闘ベースの分岐は **Outcome Hooks**、グラフから直接呼ぶなら **EmitEvent** を使う。
5. 門の固定出現やリリース出し分けは **Condition / releaseFlags** で制御する。

**対象**: イベント門 / サイドオブジェクト / 中立的敵オブジェクト / ランダムエンカウント

**目的**: 「イベントの本体を統一の核（Event Kernel）に集約し、結果はフラグ/タグ/カウンタ等の**Effect**に畳んで他システムと疎結合で連携する」設計を定義する。

---

## 0. 要約（TL;DR）
- **EventDefinitionSO** にイベントの条件・ステップ・終端効果を集約。
- 各“宿り先”（門/サイド/遭遇/ランダム）には薄い **EventHost** を付与し、**EmitEvent** で起動。
- 実行は **EventRunner** が担い、**EventStep**（プラガブル）を順次実行→最終的に **EffectSO[]** へ畳む。
- **FactStore（GameState）** が Flags/Tags/Counters/Overlays を一元管理し、Change 通知を配信。
- 遭遇系には **Outcome Hooks（Win/Escape/Lose/Purchase）** を追加し、通常処理と“明示イベント”の両立を実現。
- ノード/エッジなど**中核のFlowGraph構造は不変**。拡張は **Condition/Effect/Step** の型追加で対応。

---

## 1. 設計原則
1) **出力は Effect に限定** … イベント結果は Flag/Tag/Counter/Jump/Unlock 等へ集約。
2) **宿り先は薄く** … Host はトリガーと参照だけ。ロジックは Kernel 側に集中。
3) **不変の容器** … Node/Edge/Resolver/Overlay は維持。DSL（Condition/Effect）と Step を足す。
4) **再現性と観測性** … seed・ログ・差分ビューでリプレイ可能。
5) **段階出荷** … feature/release flags でリリースごとに安全に出し分け。

---

## 2. 全体アーキテクチャ
```
[FlowGraph]
  └─ Node/Edge (不変) ──> (OnEnter/Choice/Resolver) ──> [EmitEvent]
                                                       └─> [Effect]

[EventHost] (門/サイド/遭遇/ランダム)
  └─ trigger(OnEnter/OnInteract/OnEncounterStart/OnEnd)
      └─ EventRunner.Run(EventDefinitionSO, Context)
              ├─ Step1(Dialog/Puzzle/CombatHandoff/外部MiniGame...)
              ├─ Step2(...)
              └─ Collect Effects → [EffectApplier]
                                      └─ [FactStore(GameState)]
                                           ├─ Flags/Tags/Counters/Overlays
                                           └─ Change Events (他システム購読)
```

---

## 3. コアコンポーネント
### 3.1 EventDefinitionSO（イベント定義）
- `id, tags, releaseFlags[]`
- `conditions: ConditionSO[]` … 発火可否/再入場可否
- `steps: List<EventStep>` … UI/パズル/戦闘/外部ミニゲーム接続の**アダプタ**
- `terminalEffects: List<EffectSO>` … 終了時に必ず適用する確定出力

### 3.2 EventStep（拡張ポイント）
- 非同期実行（Coroutine/async）
- 返り値は **EffectSO（またはストリーム）** のみ
- 例: DialogStep / PuzzleStep / CombatHandoffStep / EmitEventStep / CustomMiniGameStep

### 3.3 EventRunner
- `Run(EventDefinitionSO, EventContext) -> EffectSO[]`
- Step を順次実行して Effects を収集 → まとめて適用
- ロギング/シード統制/キャンセル（中断再開フック）

### 3.4 EventHost（宿り先アダプタ）
- 取り付け先: ゲートノード、サイドオブジェクト、中立敵、ランダム遭遇エントリ
- `trigger: OnEnter/OnInteract/OnEncounterStart/OnSolved/OnDefeat` など
- 起動時: 条件判定→ EventRunner 実行→ Effect 適用

### 3.5 FactStore（GameState 一元ストア）
- `SetFlag/UnsetFlag/AddTag/IncCounter/PushOverlay/PopOverlay` 等
- 変更イベントを発行（FlowGraph 抽選/分岐が購読）
- セーブ/ロード、差分可視化

### 3.6 EventRegistrySO（総覧）
- 全 EventDefinition を束ねる
- 検索/参照関係チェック/未使用検出
- `featureFlags/releaseFlags` でビルド時フィルタ

---

## 4. ホスト別 はめ込みマトリクス
| 宿り先 | 器 | 起動条件/出し分け | イベント実行 | 出力（落とし先） |
|---|---|---|---|---|
| イベント門 | Gate相当ノード | 固定歩数/訪問履歴/フラグ | **OnEnter→EmitEvent** | UnlockEdge/Jump/Flag 等 |
| サイドオブジェクト | SideObjectSO | テーブル条件＋Overlay倍率 | 選択→`effects` or `EmitEvent` | Flag/Tag/Counter |
| 中立的敵 | EncounterSO | 遭遇率 = base×倍率（メモリレス） | resolver内分岐＋**OnEnd Hooks** | Outcomeに応じた Effect/EmitEvent |
| ランダムエンカ | EncounterSO | 同上 | デフォは戦闘、必要時に EmitEvent | Outcome Hooks で拡張 |

---

## 5. Outcome Hooks（遭遇結果フック）
ランダム/中立的遭遇に「通常終了」と「明示イベント」を共存させる仕掛け。

```csharp
public enum BattleOutcome { Win, Escape, Lose, Purchase }

[Serializable]
public struct OutcomeHook {
  public BattleOutcome outcome;      // 勝/逃/敗/買
  public ConditionSO[] conditions;   // 例: 敵ID==X, エリア==Y
  public EffectSO[] effects;         // 例: Jump("RecoveryNode"), Heal(Full)
  public string emitEventId;         // 例: 敗北後ノベル（全回復＋見逃し）
  public int priority;               // 高いほど優先
}

public class EncounterSO : ScriptableObject {
  public EffectSO[] onStart;         // 既存
  public EffectSO[] onEnd;           // 既存
  public OutcomeHook[] outcomeHooks; // 追加（後方互換）
}
```

**運用例**
- 共通: Lose → `Jump(回復地点) + Heal(基本値)` （“ジーノ搬送”）
- 特定敵: Loseかつ`EnemyTag==慈悲` → `emitEventId="MercyCutscene"`（全回復+見逃し）

---

## 6. イベント門の“固定”表現
- 門は**ノード**として扱い、**Condition** で出現を固定。
- 新 Condition 例: `VisitedCountSinceAreaEntry >= K`（内部歩行カウンタ）
- 通過時の効果は Effect で表現（UnlockEdge/Jump/Flag/evilsearch解放 など）

```csharp
[CreateAssetMenu(menuName="DSL/Condition/VisitedCountSinceAreaEntry")]
public class CondVisitedCount : ConditionSO {
  public int minSteps;
  public override bool IsMet(FactStore s) => s.StepsSinceAreaEntry >= minSteps;
}
```

---

## 7. Timeline/並行進行への布石
- **EventStep は非同期** … 戦闘進行中に背後で会話/演出を進める等。
- **GameState の Pub-Sub** … StepA が `PublishTag("X")`→ StepB が購読して遷移。
- **競合回避** … Effect は「加算/タグ付与/オーバレイ切替」のような衝突しにくい粒度を優先。
- **決定性** … `seed = hash(runId,nodeId,stepIndex,buildVersion)` に基づく RNG。

---

## 8. DSL（Condition/Effect）の最小セット
- **Condition**: `HasFlag`, `VisitedCountSinceAreaEntry`, `OverlayIs`, `RandomRoll(p)` など
- **Effect**: `SetFlag/UnsetFlag`, `AddTag/RemoveTag`, `IncCounter`, `Jump(nodeId)`, `UnlockEdge(edgeId)`, `PushOverlay/PopOverlay`, `Heal(X or Full)`
- **EmitEventEffect**: FlowGraph/Encounter/SideObject から EventDefinition を直接呼び出すためのエフェクト

```csharp
public class EmitEventEffect : EffectSO {
  public string eventId;
  public override async UniTask Apply(FactStore s, EventContext ctx) {
    var def = EventRegistrySO.Instance.Find(eventId);
    var effects = await EventRunner.Instance.Run(def, ctx);
    await EffectApplier.ApplyAll(effects, s);
  }
}
```

---

## 9. データ構造（C# 雛形）
```csharp
[CreateAssetMenu(menuName="Game/EventDefinition")]
public class EventDefinitionSO : ScriptableObject {
  public string id;
  public string[] tags;
  public string[] releaseFlags;
  public List<ConditionSO> conditions;
  public List<EventStep> steps;
  public List<EffectSO> terminalEffects;
}

public abstract class EventStep : ScriptableObject {
  public abstract IAsyncEnumerable<EffectSO> Execute(EventContext ctx);
}

public class EventHost : MonoBehaviour {
  public EventTriggerKind trigger;
  public EventDefinitionSO eventDef;
  public async UniTask Trigger(EventContext ctx) {
    if (!ConditionSO.And(eventDef.conditions).IsMet(ctx.state)) return;
    var effects = await EventRunner.Instance.Run(eventDef, ctx);
    await EffectApplier.ApplyAll(effects, ctx.state);
  }
}
```

---

## 10. ランタイムフロー（1サイクル）
1. FlowGraph: Node 進入 → SideObject 提示 / Encounter 抽選（メモリレス）
2. 宿り先で **EventHost.Trigger** または **EmitEventEffect** 実行
3. **EventRunner** が Step を実行 → **Effect** を収集
4. **FactStore** へ一括適用（Flags/Tags/Counters/Overlays 更新）
5. 更新通知により次サイクルの抽選・分岐へ反映

---

## 11. 拡張性と一元管理
- **EventRegistrySO**: イベントの重複ID/未参照検知、参照グラフ可視化、releaseFlags フィルタ
- **featureFlags/releaseFlags**: バリアント出し分け、段階実装に対応
- **asmdef 分割**: `Game.Events.Core` / `Game.Events.Steps.*` / `Game.Content`
- **Addressables**: 大型ミニゲーム Step の遅延ロード

---

## 12. 実装ロードマップ（最短コース）
1) **FactStore**（Flags/Tags/Counters + 変更通知）
2) **ConditionSO / EffectSO**（最小セット）
3) **EventDefinitionSO / EventRunner / EventHost**
4) **EmitEventEffect**（FlowGraph/Encounter/SideObject から起動）
5) **Outcome Hooks**（EncounterSO にフィールド追加）
6) **Gate 固定用 Condition**（VisitedCountSinceAreaEntry）
7) **Editor 検証/単体実行ツール**（差分ビュー/ログ/seed固定）

---

## 13. 既存仕様との整合
- Node/Edge/Resolver/Overlay は**不変**。
- 追加は型/フィールドの**後方互換拡張**のみ（破壊的変更不要）。
- 既定の「敗北→ジーノ搬送」は共通 OutcomeHook として標準化。特殊敵は上書き可能。

---

## 14. 運用Tips
- **テスト**: EventDefinition を単体実行して Effect 差分をスナップショット比較。
- **ロギング**: `ctx.runId, nodeId, stepIndex, effectId` で時系列を固定。
- **翻訳/台詞**: Step がテキストIDを返し、UI 層でレンダリング（Kernel から文面は切り離す）。

---

## 15. 付録：定義サンプル
**YAML（概念例）**
```yaml
id: MercyCutscene
conditions:
  - HasFlag: ["Met_KindEnemy"]
steps:
  - type: DialogStep
    scriptId: dlg_mercy_intro
  - type: ApplyEffects
    effects:
      - Heal: Full
      - SetFlag: ForgivenByX
terminalEffects:
  - Tag: ["MercyEncountered"]
```

**Encounter OutcomeHook（概念例）**
```yaml
encounter:
  id: rnd_beast
  outcomeHooks:
    - outcome: Lose
      conditions: [ EnemyTag==Merciful ]
      emitEventId: MercyCutscene
      priority: 100
    - outcome: Lose
      effects: [ Jump:RecoveryNode, Heal:Base ]
      priority: 0
```

---

### 結論
現行の中核仕様（FlowGraph＋DSL）を維持したまま、**Event Kernel + Outcome Hooks + EmitEvent** を導入すれば、
- イベント門/サイド/中立敵/ランダムの全系統に一貫した“宿らせ方”が可能
- 将来の推理/RPG/Timeline 并行も「型追加」で吸収
- リリース段階での機能出し分け・検証・再現性を担保
できる。破壊的変更は不要で、段階的に導入可能。

