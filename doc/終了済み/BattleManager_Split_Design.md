# BattleManager 分割設計案

## 目的
BattleManager の責務過多を解消し、戦闘ロジックのテスト容易性と拡張性を上げる。
UI/演出/入力とロジックを分離し、将来の歩行システムや他システムとの接続を簡単にする。

## 現状の痛点（要約）
- 戦闘ロジックと UI 操作、ログ表示が同居している
- グローバル参照（PlayersStates.Instance / Walking.Instance / SchizoLog.Instance など）が多く、BattleManager 単体で完結しない
- 先約リスト（ACTList）が並列リスト構造で、拡張時に壊れやすい
- IsRater / Acter == null など、特殊ケースで分岐が複雑化しやすい

## 分割方針（レイヤー構成）
1) **BattleCore**: 純粋な戦闘ロジック（入力→状態遷移）
2) **BattleServices**: ターン選出・対象選定・効果計算などの補助ロジック
3) **BattleUIBridge**: UI/演出/ログ表示の仲介
4) **BattleOrchestrator**: BattleCore を進め、UI/入力との橋渡し

## コンポーネント案

### 1) BattleCore（純ロジック）
- 役割: 戦闘状態、ターン進行、死亡判定、勝敗判定
- 依存: なし（Unity の MonoBehaviour 依存を避ける）
- 主な入出力:
  - 入力: ActionEntry（行動・特殊行動）
  - 出力: BattleEvent（ログや UI で使う情報）

### 2) TurnScheduler / ActionQueue
- 役割: 先約リスト、ランダムターン、再行動条件の管理
- 現在の ACTList を置き換える（並列リストではなく 1 エントリ構造）
- ActionEntry 例:
  - type: Act / Rather / Counter / Skip
  - actor: BaseStates?（Rather の場合は null 可）
  - faction
  - message
  - raterTargets / raterDamage
  - modifiers / freeze / singleTarget / exCounter

### 3) TargetingService
- 役割: 対象選定ロジック（前衛/後衛/ランダム/全体/事故など）
- BattleCore から独立し、今の分岐ロジックをこのクラスへ寄せる

### 4) EffectResolver
- 役割: ダメージ計算、状態変化適用、レイザー処理など
- BattleCore から「計算部分だけ」を切り出す

### 5) BattleUIBridge
- 役割: WatchUIUpdate / BattleSystemArrowManager / MessageDropper の操作
- BattleCore から直接 UI を触らないようにする
- BattleEvent を受け取り UI を更新

### 6) BattleOrchestrator
- 役割: BattleCore の進行と UI/入力をまとめる Facade
- BattleManager はこの Orchestrator に縮小するイメージ

## 依存関係イメージ
BattleOrchestrator
 ├─ BattleCore
 │   ├─ TurnScheduler
 │   ├─ TargetingService
 │   └─ EffectResolver
 └─ BattleUIBridge

## 既存コードからの分割単位
- `BattleManager.cs` のうち
  - **Core**: 状態変数、勝敗判定、NextTurn、RatherACT など
  - **TurnScheduler**: ACTList/RandomTurn/RemoveDeathCharacters
  - **TargetingService**: SelectGroup/UA の対象選定分岐
  - **UIBridge**: WatchUIUpdate / BattleSystemArrowManager / MessageDropper

## 既存連携の実態（変更対象）
分割時に必ず触るポイント。互換は不要だが「機能維持」のために洗い出し済み。

### 生成/開始フロー
- `Walking.cs` の Encount が `BattleManager` を直接生成し、`bm.ACTPop()` で初期状態を返す
- `BattleInitializer.cs` に同様の初期化があり、二重実装になっている
- `PlayersStates.Instance.OnBattleStart()` を戦闘開始で呼ぶ

### UI 状態遷移の軸
- `Walking.USERUI_state` が戦闘の画面遷移の主軸
- NextWait のクリックで `bm.CharacterActBranching()` が呼ばれ、戻り値で `USERUI_state` を更新
- `SelectTargetButtons` / `SelectRangeButtons` は `USERUI_state` 変化で生成される

### BattleManager への直参照（置換対象）
- `BaseSkill` / `BaseStates` / `BasePassive` が `Walking.Instance.bm` を参照
- `BattleAIBrain` は `Walking.Instance.bm` を取得し、`Acts.GetAtSingleTarget(0)` を参照
- `SelectTargetButtons` / `SelectRangeButtons` が `bm.Acter` / `bm.unders` / `bm.EnemyGroup` を参照
- `PlayersStates` が `bm.SkillStock` / `bm.DoNothing` / `bm.PassiveCancel` を立てる
- `PlayersStates` / `BattleAIBrain` が `bm.Acts.GetAtSingleTarget(0)` 依存

### UI/演出の結合
- `BattleManager` 内で `WatchUIUpdate` / `BattleSystemArrowManager` / `SchizoLog` を直接操作
- `WatchUIUpdate` が `Walking.Instance.bm.EnemyGroup` を参照して敵 UI を配置

### 進行度・復活関連の依存
- `BattleManager.OnBattleEnd()` が `PlayersStates.Instance.NowProgress` を使用

## 機能維持のための不変条件
- バトル開始/終了のタイミングは変えない（ズーム演出や UI 表示の順序は維持）
- 行動予約（先約/カウンター/レイザー）の発火順序は変えない
- 「NextWait を押すたびに 1 つ進む」という操作感は維持する
- 対象選択/範囲選択の UI が出る条件は変えない
- 既存 AI の意思決定結果が反映される（単体先約時の制約を維持）

## 設計妥当性の再確認（現状結論）
- 現在の結合点と UI 遷移軸を踏まえても、この分割計画は「動作を維持したまま責務を整理する」形になっている
- 先約/レイザー/カウンター/SingleTarget/Freeze の順序を維持する限り、仕様崩れのリスクは低い
- 段階的に進めれば、各フェーズでの差分検証が可能

## 分割後の戦闘フロー（状態機械）
1) StartBattle: 初期化→BattleEvent を生成（UI はこのイベントを描画）
2) BuildNextAction: Scheduler が次の ActionEntry を返す（先約 > ランダム）
3) If ActionEntry.Type == Rather: EffectResolver を実行 → NextTurn
4) If Acter が操作必要:
   - Orchestrator が ChoiceRequest を発行（Skill/Range/Target）
   - UI が選択結果を返す（ActionInput）
5) EffectResolver が実行され BattleEvent を出力
6) NextTurn → BuildNextAction へ戻る
7) 勝敗条件成立で EndBattle

## 分割後の構成（詳細）
### BattleCore
- 状態を保持し、ActionEntry と ActionInput を処理して BattleEvent を返す
- 「進行度」「戦闘ターン数」「勝敗判定」「死亡判定」などをここに集約

### ActionQueue / TurnScheduler
- `List<ActionEntry>` を 1 本持つ
- 先約・レイザー・カウンター・スキップを同じエントリで表現
- ランダムターン選出はここに限定

### TargetingService
- `Acter` と `BattleState` を受けて対象リストを返す
- 「前のめり/後衛/事故/全体」などの分岐をここに集約

### EffectResolver
- Damage/Passive/StateModifier を適用し BattleEvent を生成
- レイザー処理もここに集約（Acter を必要としない）

### BattleUIBridge
- BattleEvent を UI に変換するだけの層
- `WatchUIUpdate` / `MessageDropper` / `BattleSystemArrowManager` の操作を集約

### BattleOrchestrator
- UI からの入力を受け、BattleCore に ActionInput を渡す
- `TabState` は Orchestrator で決定し、UI に通知

## 具体的な移行ステップ（細分化）
1) **ActionEntry の導入**
   - ACTList を置換し、並列リストを廃止
   - 先約/レイザー/カウンターを `ActionEntry.Type` で統一
2) **BattleState の導入**
   - BattleManager の状態変数を `BattleState` に移動
   - `BattleTurnCount`、`Wipeout`、`AlliesRunOut` などを集約
3) **TurnScheduler の抽出**
   - RandomTurn/RemoveDeathCharacters/先約消化を移動
4) **TargetingService の抽出**
   - SelectGroup/UA の分岐ロジックを移動
5) **EffectResolver の抽出**
   - ダメージ計算、レイザー処理、状態変化を集約
6) **BattleUIBridge の抽出**
   - WatchUIUpdate/SchizoLog/MessageDropper の呼び出しを集約
7) **Orchestrator で UI と接続**
   - `Walking` から BattleManager 直呼びを削除
   - UI は ChoiceRequest に従って入力を返す
8) **依存の削除**
   - `Walking.Instance.bm` 参照を廃止
   - `BaseSkill`/`BaseStates`/`BasePassive`/`BattleAIBrain` は `IBattleContext` を参照
9) **整理**
   - `BattleInitializer` と `Walking.Encount` を統合
   - `BattleTimeLine` の役割を見直し（不要なら削除）

## 実装計画（さらに具体化）
この順で進めると、毎ステップで動作確認できる。

### Phase 0: 受け皿の作成（動作不変）
- 追加: `Assets/Script/Battle/Core/` に空のクラスを作成
  - `BattleState` / `ActionEntry` / `ActionType`
  - `BattleEvent` / `ChoiceRequest` / `ActionInput`
- 目的: 後続の差し替え先を準備
- 影響: 動作は一切変えない

#### Phase 0 具体案: ActionEntry スキーマ（最小）
旧 ACTList の各リストを 1 つの ActionEntry に集約する。

- `Actor`           = CharactorACTList
- `Faction`         = FactionList
- `Message`         = TopMessage
- `Modifiers`       = reservationStatesModifies
- `Freeze`          = IsFreezeList
- `SingleTarget`    = SingleTargetList
- `ExCounterDEFATK` = ExCounterDEFATKList
- `RatherTargets`   = RaterTargetList
- `RatherDamage`    = RaterDamageList

ActionType 例:
- `Act` / `Rather` / `Counter` / `Skip`

ActionEntry 最小形:
```csharp
public sealed class ActionEntry
{
    public ActionType Type;
    public BaseStates Actor;
    public allyOrEnemy Faction;
    public string Message;
    public List<ModifierPart> Modifiers;
    public bool Freeze;
    public BaseStates SingleTarget;
    public float ExCounterDEFATK;
    public List<BaseStates> RatherTargets;
    public float RatherDamage;
}
```

### Phase 1: ACTList の置換（最優先）
- 変更: `BattleManager.cs` の ACTList を `List<ActionEntry>` に差し替え
- 対応: RatherAdd/Add/RemoveDeathCharacters/RemoveAt を ActionEntry に移す
- 動作条件:
  - 先約/カウンター/レイザーの順序が変わらない
  - `GetAtSingleTarget(0)` に相当する情報が参照できる
- ここで止めて動作確認する

#### Phase 1 詳細: ActionQueue 置換手順
1) `ActionQueue` クラスを新設（旧 ACTList API を一旦模倣）
   - `Count` / `Peek()` / `Dequeue()` / `RemoveAt(int)`
   - `EnqueueAct(...)` / `EnqueueRather(...)`
   - `RemoveDeadActors()`（Actor が死んでいる entry を除外）
   - `PeekSingleTarget()`（旧 `GetAtSingleTarget(0)` 相当）
2) 旧 ACTList の参照を `ActionQueue` に置換
   - `BattleManager` の `Acts` 型を差し替え
3) 旧メソッド名は一時的に残し、呼び出し側の変更を最小化
4) `Add/RatherAdd/RemoveDeathCharacters/RemoveAt` の実装を ActionEntry ベースに置換
5) `CharacterAddFromListOrRandom()` での参照を `ActionEntry` から取得する形に切替

#### Phase 1 変更対象ファイル（最小）
- `Assets/Script/BattleManager.cs`（ACTList 廃止、ActionQueue 置換）
- `Assets/Script/PlayersStates.cs`（`Acts.GetAtSingleTarget(0)` を `PeekSingleTarget()` へ）
- `Assets/Script/BattleAIBrains/BattleAIBrain.cs`（同上）

#### Phase 1 完了条件
- 先約/カウンター/レイザーの発火順が変わらない
- `SingleTarget` 予約のある時、対象選択 UI をスキップする挙動が維持される
- レイザーのみの entry でも ACTPop -> NextWait -> RatherACT の流れが成立

### Phase 2: BattleState の切り出し
- 移動: `BattleTurnCount` / `Wipeout` / `AlliesRunOut` / `EnemyGroupEmpty` など
- BattleManager は BattleState を保持し参照するだけに縮小
- ここで止めて動作確認する

#### Phase 2 詳細: BattleState 導入手順
1) `BattleState` に「戦闘の進行状態」を集約
   - 例: `TurnCount`, `Wipeout`, `AlliesRunOut`, `EnemyGroupEmpty`, `VoluntaryRunOutEnemy`, `DominoRunOutEnemies`
2) `BattleManager` 内の状態フラグを `BattleState` 経由に置換
3) 既存のロジックはそのまま移動し、式は変えない
4) `Reset` 系の処理は `BattleState.ResetTurnFlags()` などに集約

#### Phase 2 追加する構造（最小）
```csharp
public sealed class BattleState
{
    public int TurnCount;
    public bool Wipeout;
    public bool EnemyGroupEmpty;
    public bool AlliesRunOut;
    public NormalEnemy VoluntaryRunOutEnemy;
    public List<NormalEnemy> DominoRunOutEnemies = new();

    public void ResetTurnFlags()
    {
        EnemyGroupEmpty = false;
        VoluntaryRunOutEnemy = null;
        DominoRunOutEnemies.Clear();
    }
}
```

#### Phase 2 変更対象ファイル（最小）
- `Assets/Script/BattleManager.cs`（フラグ参照の置換）

#### Phase 2 完了条件
- `BattleTurnCount` の更新タイミングが従来と同じ
- 勝敗判定のフラグが同じタイミングで立つ
- 逃走（単独/連鎖）の挙動が変わらない

### Phase 3: TurnScheduler の抽出
- 移動: RandomTurn / RemoveDeathCharacters / 先約消化ロジック
- BattleManager から「次の ActionEntry を取得する」だけにする
- ここで止めて動作確認する

#### Phase 3 詳細: TurnScheduler 抽出手順
1) `TurnScheduler` を作成し、戦闘内の「次の行動決定」を集約
   - 先約の消化（ActionQueue から先頭を使う）
   - 先約が無い場合はランダム選出（RandomTurn 相当）
2) `BattleManager` の `CharacterAddFromListOrRandom()` を分割
   - `TurnScheduler.SelectNext()` に集約
   - 戻り値を `ActionEntry` と `Acter` に分ける
3) `RemoveDeathCharacters` / `RetainActionableCharacters` を Scheduler 側に移動
4) Scheduler は BattleState を受け取り、`TurnCount` を参照

#### Phase 3 追加する構造（最小）
```csharp
public sealed class TurnScheduler
{
    private readonly BattleGroup _ally;
    private readonly BattleGroup _enemy;
    private readonly ActionQueue _queue;
    private readonly BattleState _state;

    public TurnScheduler(BattleGroup ally, BattleGroup enemy, ActionQueue queue, BattleState state)
    {
        _ally = ally;
        _enemy = enemy;
        _queue = queue;
        _state = state;
    }

    public ActionEntry SelectNext()
    {
        if (_queue.Count > 0)
        {
            return _queue.Peek();
        }

        var acter = RandomTurn();
        if (acter == null)
        {
            return new ActionEntry { Type = ActionType.Skip };
        }

        return new ActionEntry
        {
            Type = ActionType.Act,
            Actor = acter,
            Faction = GetFaction(acter),
            Message = string.Empty
        };
    }
}
```

#### Phase 3 変更対象ファイル（最小）
- `Assets/Script/BattleManager.cs`（RandomTurn / CharacterAddFromListOrRandom の移動）
- `Assets/Script/Battle/Core/TurnScheduler.cs`（新規）

#### Phase 3 完了条件
- 先約があるときは必ず先約が優先される
- 先約がない時、ランダムターンの挙動が従来と同じ
- 両陣営行動不可時は Skip 相当で進む（従来の挙動と一致）

### Phase 4: TargetingService の抽出
- 移動: SelectGroup / UA / 前のめり・後衛・事故の分岐
- 戻り値を「対象リスト」に統一
- ここで止めて動作確認する

#### Phase 4 詳細: TargetingService 抽出手順
1) `TargetingService.SelectTargets()` を作成
   - 入力: `ActionEntry` / `BattleState` / `BattleGroup` / `Acter`
   - 出力: `List<BaseStates>`（最終対象）
2) `SelectTargetFromWill()` / `SelectByPassiveAndRandom()` のロジックを移動
3) `UnderActersEntryList` は `TargetingService` 内部で完結させる
4) BattleManager からは「対象リストを受け取るだけ」にする

#### Phase 4 追加する構造（最小）
```csharp
public sealed class TargetingService
{
    public List<BaseStates> SelectTargets(BaseStates acter, BattleGroup ally, BattleGroup enemy)
    {
        // 旧 SelectTargetFromWill の分岐をここに移動
    }
}
```

#### Phase 4 変更対象ファイル（最小）
- `Assets/Script/BattleManager.cs`（対象選択分岐を除去）
- `Assets/Script/Battle/Services/TargetingService.cs`（新規）

#### Phase 4 完了条件
- 対象選択 UI が出る条件が従来と一致
- 前衛/後衛/事故の判定が従来と一致
- ランダム対象の分布が従来と一致

### Phase 5: EffectResolver の抽出
- 移動: ダメージ計算 / レイザー処理 / 状態変化
- BattleManager は「解決結果(BattleEvent)を受けるだけ」にする
- ここで止めて動作確認する

#### Phase 5 詳細: EffectResolver 抽出手順
1) `EffectResolver.Resolve(ActionEntry, targets)` を作成
   - 入力: `ActionEntry` / `targets` / `BattleState`
   - 出力: `BattleEvent`（ログ・UI用の結果）
2) `RatherACT()` / `SkillACT()` / `TriggerACT()` の核心部分を移動
3) `BaseStates` への状態反映は EffectResolver 内に集約

#### Phase 5 追加する構造（最小）
```csharp
public sealed class EffectResolver
{
    public BattleEvent Resolve(ActionEntry entry, List<BaseStates> targets, BattleState state)
    {
        // 旧ダメージ計算・ステート更新をここに移動
    }
}
```

#### Phase 5 変更対象ファイル（最小）
- `Assets/Script/BattleManager.cs`（ダメージ計算分岐の削除）
- `Assets/Script/Battle/Services/EffectResolver.cs`（新規）

#### Phase 5 完了条件
- ダメージ計算結果が従来と一致
- レイザー処理のタイミングが従来と一致
- 状態変化（パッシブ付与/解除）が従来と一致

### Phase 6: UIBridge / Orchestrator に集約
- 目的: UI/演出/入力を分離
- `TabState` の遷移は Orchestrator が専任
- `Walking.cs` は Orchestrator の結果を UI に反映するだけにする

#### Phase 6 詳細: Orchestrator 導入
1) `BattleOrchestrator` を作成
   - `Step()` で次の `TabState` を返す
   - `ChoiceRequest` を発行し、UI から `ActionInput` を受け取る
2) `BattleUIBridge` が `BattleEvent` を UI に変換
3) `Walking.cs` は `Orchestrator.Step()` の戻り値で UI を遷移

#### Phase 6 変更対象ファイル（最小）
- `Assets/Script/Walking.cs`（bm直呼びを Orchestrator に変更）
- `Assets/Script/Battle/UI/BattleOrchestrator.cs`（新規）
- `Assets/Script/Battle/UI/BattleUIBridge.cs`（新規）

#### Phase 6 完了条件
- `TabState` の遷移順が従来と一致
- ズーム演出/ログ表示の順序が従来と一致

### Phase 7: 依存削除
- `Walking.Instance.bm` 参照を段階的に削除
- `IBattleContext` 経由に置換
- `BattleInitializer` と `Walking.Encount` を統合

#### Phase 7 詳細: 依存削除の順序
1) `IBattleContext` を定義し、`BattleCore` を実装
2) `BaseSkill` / `BaseStates` / `BasePassive` / `BattleAIBrain` を `IBattleContext` に切替
3) `Walking.Instance.bm` 参照を削除
4) `BattleInitializer` と `Walking.Encount` を統合
5) `BattleTimeLine` を整理/削除

#### Phase 7 完了条件
- `Walking.Instance.bm` の直接参照がプロジェクトから消える
- Battle の開始/終了が Orchestrator 経由に統一される

## ファイル配置案
```
Assets/Script/Battle/
  Core/
    BattleState.cs
    ActionEntry.cs
    BattleEvent.cs
    BattleCore.cs
  Services/
    TurnScheduler.cs
    TargetingService.cs
    EffectResolver.cs
  UI/
    BattleUIBridge.cs
    BattleOrchestrator.cs
```

## 進捗ごとの確認ポイント
- Phase 1: 先約/レイザー/カウンターが従来の順序で処理される
- Phase 2: 戦闘ターン数や勝敗判定が同じタイミングで更新される
- Phase 3: 片側行動不能でも戦闘が停止しない（RandomTurn の挙動維持）
- Phase 4: 対象選択UIが今までと同じ条件で出る
- Phase 5: ダメージ結果/ログ出力が一致する
- Phase 6: 画面遷移と演出のタイミングが変わらない

## 進捗メモ
- 2025-03-08 時点
  - Phase 1〜6: 実装済み（ActionQueue/BattleState/TurnScheduler/TargetingService/EffectResolver/BattleUIBridge/Orchestrator）
  - Phase 7: 途中
    - 完了: `Walking.Instance.bm` 参照を削除し `IBattleContext` へ移行
    - 完了: BattleContextHub を導入し、`Walking.Instance.BattleContext` 依存を主要ロジックから撤去
    - 完了: UIStateHub を導入し、SKILLUI/USERUI の購読・更新を Hub 経由に統一
    - 完了: BattleTimeLine の生成/保持を削除
    - 完了: PlayersStates 依存を `IBattleMetaProvider` 注入へ置換
    - 進行中: `Walking.Instance` へのUI依存（ステージ色/個別UI）整理

## 具体的な移行ステップ（小さく進める）
1) **ActionEntry 化**
   - ACTList を `List<ActionEntry>` に置換
   - 先約/レイザー/カウンターを同じエントリ型で持つ
2) **UI 呼び出しの隔離**
   - WatchUIUpdate, SchizoLog, MessageDropper の呼び出しを BattleUIBridge へ移す
3) **TargetingService 抽出**
   - 既存の対象選定ロジックを移動し、BattleCore からは結果だけ受け取る
4) **BattleManager の軽量化**
   - BattleManager = Orchestrator へ縮小（Input/画面遷移の管理だけ）

## 追加計画: PlayersStates 依存の整理（グローバル依存の解消）
歩行システムと無関係に残るグローバル依存として `PlayersStates.Instance` があるため、
必要なら BattleManager からの直接参照を段階的に排除する。

### 方針（どれか1つを選ぶ）
1) **注入（推奨）**: 必要なデータ/操作だけを `IBattleMetaProvider` などの小さなインターフェースで渡す  
2) **DTO で受け渡し**: 戦闘開始時に必要な値を `BattleStartContext` に詰めて注入する  
3) **現状維持**: バトル単体テストをしない前提なら `PlayersStates.Instance` 依存を残す

### 影響範囲（現状）
- `BattleManager.OnBattleEnd()` の `PlayersStates.Instance.NowProgress` 参照（敵復帰の進行度）
- `PlayersStates.Instance.AllyAlliesUISetActive(false)`（戦闘終了時のUI制御）

### 進め方（最小）
1) `IBattleMetaProvider` を追加（進行度、味方UIのON/OFFなど最小機能）
2) `BattleOrchestrator` 生成時に実装を注入
3) `BattleManager` から `PlayersStates.Instance` の直接参照を削除
4) 動作確認（戦闘終了の復帰/UI非表示が同じ）

## インターフェース例（最小）
```csharp
public interface IBattleUIBridge
{
    void ShowActionMark(BaseStates actor);
    void HideActionMark();
    void PushLog(string message);
    void OnBattleStart();
    void OnBattleEnd();
}

public sealed class ActionEntry
{
    public ActionType Type;
    public BaseStates Actor;
    public allyOrEnemy Faction;
    public string Message;
    public List<BaseStates> RatherTargets;
    public float RatherDamage;
    public List<ModifierPart> Modifiers;
    public bool Freeze;
    public BaseStates SingleTarget;
    public float ExCounterDEFATK;
}
```

## 注意点
- `TabState` と UI の状態遷移は Orchestrator が一括管理する
- 戦闘終了時の `OnBattleEnd` 系コールバック順序は維持する
- AI の「単体先約時の制限」は TargetingService 側で保持する
- 歩行システム設計で後から変更予定の事項（例: ステージ遷移/歩行フロー/出口条件など）は本設計書では扱わない
- 本設計書は BattleManager 単独の責務分離に集中し、歩行システム側の仕様変更は別ドキュメントで追う
- ステージ配色（矢印色など）は歩行システム設計のノード側で設定する想定のため、BattleUIBridge からの参照整理は保留

## 実装済み責務分離一覧
- 状態管理: `BattleState`
- 行動エントリ/先約: `ActionEntry` + `ActionQueue`
- ターン選出: `TurnScheduler`
- 対象選定: `TargetingService`
- 効果解決: `EffectResolver`
- UI/演出仲介: `BattleUIBridge`
- 進行制御: `BattleOrchestrator`
- コンテキスト集約: `IBattleContext` + `BattleContextHub`
- メタ情報注入: `IBattleMetaProvider` + `PlayersStatesBattleMetaProvider`
- 初期化: `BattleInitializer`
- UI状態の購読/配信: `UIStateHub`

## クローズ
- Status: Completed
- Closed: 2026-01-11

## 期待効果
- バグ調査の範囲が狭まり、仕様変更に強くなる
- 歩行システムなどの上位システムと接続しやすくなる
- テスト（ロジック単体）を回しやすくなる
