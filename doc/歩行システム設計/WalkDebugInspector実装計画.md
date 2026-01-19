# WalkDebugInspector 実装計画

## 概要

WalkingSystemManagerのカスタムインスペクターを作成し、PlayMode中にGameContextの状態（タグ/フラグ/カウンター/オーバーレイ）を可視化・操作できるデバッグUIを提供する。

## 目的

- 歩行システムのタグ/条件ベース機能をゲーム内イベント実装前にテスト可能にする
- FlowGraph内で使用されているタグ/フラグを自動収集し、ワンクリックで付与/削除できるようにする
- 開発効率とデバッグ体験の向上

## 要件

### 機能要件

1. **タグ操作**
   - 現在のGameContextに設定されているタグを一覧表示
   - FlowGraph内のConditionSOから使用タグを自動収集・リストアップ
   - チェックボックスでタグの付与/削除をトグル
   - カスタムタグの手動追加

2. **フラグ操作**
   - 現在設定されているフラグを一覧表示
   - FlowGraph内のConditionSOから使用フラグを自動収集
   - チェックボックスでフラグのON/OFFをトグル
   - カスタムフラグの手動追加

3. **カウンター操作**
   - 現在設定されているカウンターを一覧表示
   - 値の直接編集（IntField）
   - カスタムカウンターの手動追加

4. **オーバーレイ表示**
   - 現在アクティブなEncounterOverlayを一覧表示
   - 各オーバーレイのID、倍率、残り歩数を表示
   - 手動でオーバーレイを追加/削除

5. **状態サマリー**
   - 現在のノードID
   - GlobalSteps / NodeSteps / TrackProgress
   - 現在の遭遇倍率（合計）

### 非機能要件

- PlayMode中のみ操作可能（EditModeでは読み取り専用または非表示）
- パフォーマンスへの影響を最小限に（毎フレーム更新ではなくRepaint時のみ）
- 既存のWalkingSystemManagerインスペクターを破壊しない（継承またはデコレータパターン）

## アーキテクチャ

```
Assets/Editor/Walk/
├── WalkingSystemManagerEditor.cs    # カスタムインスペクター本体
├── WalkDebugDrawer.cs               # デバッグUI描画ヘルパー
└── WalkConditionCollector.cs        # Condition自動収集ユーティリティ
```

### クラス設計

#### WalkingSystemManagerEditor

```csharp
[CustomEditor(typeof(WalkingSystemManager))]
public class WalkingSystemManagerEditor : Editor
{
    private WalkConditionCollector collector;
    private bool showDebugFoldout = true;

    // デフォルトインスペクター + デバッグセクション
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (Application.isPlaying)
        {
            DrawDebugSection();
        }
    }
}
```

#### WalkConditionCollector

```csharp
public sealed class WalkConditionCollector
{
    // FlowGraphからすべてのConditionSOを収集
    public void CollectFromFlowGraph(FlowGraphSO graph);

    // 収集結果
    public HashSet<string> UsedTags { get; }
    public HashSet<string> UsedFlags { get; }
    public HashSet<string> UsedCounters { get; }
}
```

## UI設計

```
┌─────────────────────────────────────────────────────┐
│ Walking System Manager                              │
├─────────────────────────────────────────────────────┤
│ Flow Graph: [FlowGraph_Stages]                      │
│ Start Node: [エントランスホール]                      │
│ ...（通常のフィールド）                               │
├─────────────────────────────────────────────────────┤
│ ▼ Debug (PlayMode)                                  │
│ ┌─────────────────────────────────────────────────┐ │
│ │ Status                                          │ │
│ │   Node: エントランスホール                        │ │
│ │   Steps: Global=42 Node=12 Track=12             │ │
│ │   Encounter Multiplier: x1.5                    │ │
│ └─────────────────────────────────────────────────┘ │
│ ┌─────────────────────────────────────────────────┐ │
│ │ Tags                              [+ Add]       │ │
│ │   [✓] vip          (used by: 2 conditions)     │ │
│ │   [ ] healed       (used by: 1 condition)      │ │
│ │   [✓] custom_tag   (manual)                    │ │
│ └─────────────────────────────────────────────────┘ │
│ ┌─────────────────────────────────────────────────┐ │
│ │ Flags                             [+ Add]       │ │
│ │   [✓] gate_passed  (used by: 1 condition)      │ │
│ │   [ ] boss_defeated                            │ │
│ └─────────────────────────────────────────────────┘ │
│ ┌─────────────────────────────────────────────────┐ │
│ │ Counters                          [+ Add]       │ │
│ │   kills: [___5___]                             │ │
│ │   items: [___12__]                             │ │
│ └─────────────────────────────────────────────────┘ │
│ ┌─────────────────────────────────────────────────┐ │
│ │ Encounter Overlays                [+ Add]       │ │
│ │   double_encounter: x2.0 (残り 5歩)             │ │
│ │   [Remove]                                     │ │
│ └─────────────────────────────────────────────────┘ │
│                                                     │
│ [Refresh Conditions] [Clear All State]              │
└─────────────────────────────────────────────────────┘
```

## 収集対象のConditionSO

以下のConditionSOから使用キーを自動収集：

| ConditionSO | 収集対象 |
|-------------|----------|
| HasTagCondition | tag フィールド → UsedTags |
| HasFlagCondition | flagKey フィールド → UsedFlags |
| CounterCondition | counterKey フィールド → UsedCounters |

### 収集範囲

FlowGraph内の以下の場所を走査：

1. **NodeSO**
   - gates[].passConditions
   - exits[].conditions

2. **SideObjectTableSO**
   - entries[].conditions

3. **EncounterTableSO**
   - entries[].conditions

4. **EdgeSO**
   - conditions

## 実装フェーズ

### Phase 1: 基盤（必須）

1. `WalkingSystemManagerEditor.cs` 作成
   - デフォルトインスペクター描画
   - PlayMode判定
   - デバッグセクションのFoldout

2. `WalkConditionCollector.cs` 作成
   - FlowGraph走査ロジック
   - HasTagCondition/HasFlagCondition対応

3. タグ操作UI
   - 収集したタグの一覧表示
   - チェックボックスでトグル

### Phase 2: 拡張

4. フラグ操作UI
5. カウンター操作UI
6. オーバーレイ表示UI

### Phase 3: 利便性向上

7. カスタム値の手動追加UI
8. 状態サマリー表示
9. Refresh/Clear Allボタン

## 実装上の注意点

### GameContextへのアクセス

```csharp
// WalkingSystemManagerからGameContextを取得
var manager = (WalkingSystemManager)target;
var context = manager.GameContext; // publicプロパティが必要
```

**必要な変更**: WalkingSystemManagerにGameContextのpublicゲッターを追加

### Condition型の判定

```csharp
if (condition is HasTagCondition tagCond)
{
    // リフレクションでtagフィールドを取得
    var tagField = typeof(HasTagCondition)
        .GetField("tag", BindingFlags.NonPublic | BindingFlags.Instance);
    var tag = (string)tagField.GetValue(tagCond);
    usedTags.Add(tag);
}
```

### パフォーマンス考慮

- Condition収集はFlowGraph変更時またはRefreshボタン押下時のみ実行
- 収集結果はキャッシュし、毎フレーム再計算しない

## テスト方法

1. PlayModeでWalkingSystemManagerを選択
2. Debugセクションが表示されることを確認
3. タグのチェックボックスをONにし、コンソールで確認
4. ゲート通過後、自動的にタグが付与されチェックが入ることを確認

## 成果物

- `Assets/Editor/Walk/WalkingSystemManagerEditor.cs`
- `Assets/Editor/Walk/WalkConditionCollector.cs`
- WalkingSystemManagerへの軽微な変更（GameContextゲッター追加）

## 将来の拡張

- EditorWindow版（独立ウィンドウとして常時表示）
- 複数GameContextの比較表示
- 状態のスナップショット保存/復元
- PlayModeテストとの統合
