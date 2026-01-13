# PlayersStates 完全リファクタリング計画（最終案）

## 目的
- PlayersStates の責務を最小化し、保守性・変更耐性・テスト容易性を最大化する。
- UI参照・UI操作・進行/戦闘連携・セーブ対象データを完全に分離する。
- シングルトン/Hub依存を段階的に撤廃し、依存関係を明示化する。

## 最終ゴール（ここまで到達したら「余地なし」）
- PlayersStates は「状態の箱」ではなく **アプリケーションレベルの組み立て役**のみ。
- UIは UIRefs + UIPresenter/UIService に閉じ、ロジックから直接触らない。
- 静的アクセス（PlayersStates.Instance/PlayersStatesHub）を削除。
- 外部は Interfaces 経由で PlayersContext を受け取り、Unityシーン側で注入する。
- 進行/戦闘/歩行連携は Application 層の Service だけが扱う。

## 最終構成（想定）
### Core / Domain
- PlayersRuntime（純C#）
  - PlayersRoster / PlayersProgress / PlayersTuning / PlayersParty などを保持
- PlayersServices（純C#）
  - PartyService / WalkLoopService / BattleCallbacks など

### Application
- PlayersContext（純C#）
  - PlayersRuntime + PlayersServices をまとめる
- PlayersBootstrapper（MonoBehaviour）
  - Unity側で参照を受け、PlayersContext を生成

### UI
- PlayersUIRefs（MonoBehaviour）
  - UI参照だけを保持
- PlayersUIService（純C#）
  - UI操作の実装
- PlayersUIFacade（純C#）
  - ロジック側が呼ぶ入口
- PlayersUIEventRouter（純C#）
  - Facadeイベント → Service の中継

### Data / Save
- PlayersSaveData（Serializable）
- PlayersSaveService（純C#）

## 依存ルール（必須）
- Domain は UnityEngine を参照しない。
- UI層は Domain を参照してよいが、Domain は UI を参照しない。
- PlayersContext は依存を「注入」されるだけで、静的取得しない。

## 実装フェーズ（完全分離まで）

### Phase 1: PlayersStates の Mono 化の縮小
- PlayersStates を「Bootstrapper 的な Mono」に限定する。
- Runtime と Services は純C#クラスへ完全に移す。
- UnityのAwake/Start内ロジックは PlayersBootstrapper に集約。

### Phase 2: PlayersStatesHub / Instance の撤廃
- PlayersStatesHub を削除し、必要な場所に明示的に注入。
- `BattleInitializer` / `Walking` などで依存を受け取る設計に変更。
- 互換が必要なら、一時的に Adapter を用意して段階移行。

### Phase 3: UIイベント完全分離
- UI操作は PlayersUIFacade への命令のみ。
- UIService は EventRouter 経由でのみ動く。
- PlayersRuntime/Services から UI参照は完全排除。

### Phase 4: Save/Load 層を切り出し
- PlayersRuntime の保持データを SaveData に集約。
- Save/Load は SaveService が扱う。
- 進行状況の保存は Runtime → SaveData の変換で行う。

### Phase 5: アプリケーション層の明確化
- PlayersContext を中心に、他のシステム（Battle/Walk/Stage）へ注入。
- シーン切り替え時の寿命管理も PlayersBootstrapper で統一。

### Phase 6: インデックス依存の弱体化
- AllyId を中心にアクセスし、配列インデックスの直接利用を減らす。
- UIも AllyId マップで引けるようにする（必要なら Dictionary 化）。

## Unity上の操作（想定）
- Managers に PlayersBootstrapper を配置。
- PlayersUIRefs は引き続き Managers に持たせ、参照入口は一つに維持。
- 旧PlayersStatesコンポーネントは削除または最小化。

## 完全分離後の操作イメージ
- Unityシーン側は Bootstrapper で参照を渡すだけ。
- ロジック側は PlayersContext 経由で必要な操作をするだけ。
- UI側は EventRouter で通知を受けるだけ。

## 完了判定（この時点で「余地なし」）
- PlayersStatesHub が存在しない。
- PlayersStates.Instance が存在しない。
- UI参照が PlayersUIRefs 以外に存在しない。
- 進行/戦闘/歩行は PlayersContext からしか触れない。
- UI操作は PlayersUIFacade 経由のみ。

## 次のアクション
- この計画に沿って、Phase 1 から順に実施する。
- Phase 2 で外部依存の注入方式を決める（Battle/Walking/Stage へどう渡すか）。
## 依存監査（Hub/Instanceの置換を確実にするための明文化）
- **目的**: `PlayersStatesHub` / `PlayersStates.Instance` の依存箇所を全て洗い出し、置換方針を決める。
- **検索ルール**:
  - `rg "PlayersStatesHub" Assets/Script`
  - `rg "PlayersStates\.Instance" Assets/Script`
- **出力物**: 依存一覧表（ファイル/行/用途/置換先）。

例（表）:
```
| 依存箇所 | 理由 | 置換先 |
| Walking.cs | UI制御 | IPlayersUIControl注入 |
| BattleInitializer.cs | パーティ取得 | IPlayersParty注入 |
```

- **完了条件**: 依存一覧に載った全箇所の置換先が決まり、実装タスク化できている。
## クローズ更新 (2026-01-13)
- Phase 1-6 は実施済み（Bootstrapper/Runtime分離、Hub/Instance撤廃、UI分離、Context注入、AllyId完全化）。
- Phase 4 の Save/Load も実装済み（プレイヤー状態のみ。戦闘内の一時情報は除外）。
- 注入方式は PlayersContextRegistry による明示登録/解除に統一。
- UnityEvent は PlayersBootstrapper 経由の明示メソッドに更新済み。
- 以降は必須のリファクタリングは無し。必要なら任意改善で対応する。
