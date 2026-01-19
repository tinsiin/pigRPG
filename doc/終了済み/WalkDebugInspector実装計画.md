# WalkDebugInspector 実装計画

**ステータス: ✅ 完了（2026-01-19）**

## 実装結果

### 新規ファイル
- `Assets/Script/Walk/Conditions/IKeyedCondition.cs`
- `Assets/Editor/Walk/WalkConditionCollector.cs`
- `Assets/Editor/Walk/WalkingSystemManagerEditor.cs`

### 変更ファイル
- `HasTagCondition.cs`, `HasFlagCondition.cs`, `HasCounterCondition.cs` - IKeyedCondition実装
- `AndCondition.cs`, `OrCondition.cs`, `NotCondition.cs` - アクセサ追加
- `WalkingSystemManager.cs` - GameContextゲッター追加

---

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

Assets/Script/Walk/Conditions/
└── IKeyedCondition.cs               # キー取得用インターフェース（新規）
```

### クラス設計

#### IKeyedCondition（新規インターフェース）

リフレクションに依存せず、安全にキーを取得するためのインターフェース:

```csharp
public enum ConditionKeyType
{
    Tag,
    Flag,
    Counter
}

public interface IKeyedCondition
{
    string ConditionKey { get; }
    ConditionKeyType KeyType { get; }
}
```

対象Conditionに実装:

```csharp
// HasTagCondition
public sealed class HasTagCondition : ConditionSO, IKeyedCondition
{
    [SerializeField] private string tag;

    public string ConditionKey => tag;
    public ConditionKeyType KeyType => ConditionKeyType.Tag;
    // ...
}

// HasFlagCondition
public sealed class HasFlagCondition : ConditionSO, IKeyedCondition
{
    [SerializeField] private string flagKey;

    public string ConditionKey => flagKey;
    public ConditionKeyType KeyType => ConditionKeyType.Flag;
    // ...
}

// HasCounterCondition
public sealed class HasCounterCondition : ConditionSO, IKeyedCondition
{
    [SerializeField] private string counterKey;

    public string ConditionKey => counterKey;
    public ConditionKeyType KeyType => ConditionKeyType.Counter;
    // ...
}
```

#### WalkingSystemManagerEditor

```csharp
[CustomEditor(typeof(WalkingSystemManager))]
public class WalkingSystemManagerEditor : Editor
{
    private WalkConditionCollector collector;
    private bool showDebugFoldout = true;

    public override void OnInspectorGUI()
    {
        // PlayMode中は通常フィールドを編集禁止
        if (Application.isPlaying)
        {
            GUI.enabled = false;
        }

        DrawDefaultInspector();

        GUI.enabled = true;

        if (Application.isPlaying)
        {
            EditorGUILayout.Space();
            DrawDebugSection();
        }
    }

    private GameContext GetGameContext()
    {
        // 選択中のManagerに紐づくGameContextを取得（Hub.Currentではない）
        var manager = (WalkingSystemManager)target;
        return manager.GameContext; // 初期化前はnullの可能性あり
    }

    private void DrawDebugSection()
    {
        var context = GetGameContext();
        if (context == null)
        {
            EditorGUILayout.HelpBox("GameContext未初期化（歩行システム起動前）", MessageType.Info);
            return;
        }
        // ... デバッグUI描画
    }
}
```

#### WalkConditionCollector

```csharp
public sealed class WalkConditionCollector
{
    // 収集結果（Dictionary: キー名 → 使用回数）
    public Dictionary<string, int> UsedTags { get; } = new();
    public Dictionary<string, int> UsedFlags { get; } = new();
    public Dictionary<string, int> UsedCounters { get; } = new();

    // FlowGraphからすべてのConditionSOを収集
    public void CollectFromFlowGraph(FlowGraphSO graph)
    {
        // Refresh時の累積を防ぐため、収集前にクリア
        UsedTags.Clear();
        UsedFlags.Clear();
        UsedCounters.Clear();

        // ... FlowGraph走査処理
    }

    // 単一のConditionを処理（And/Or/Not再帰対応）
    private void CollectFromCondition(ConditionSO condition)
    {
        if (condition == null) return;

        // IKeyedConditionインターフェースで安全にキー取得
        if (condition is IKeyedCondition keyed && !string.IsNullOrEmpty(keyed.ConditionKey))
        {
            var dict = keyed.KeyType switch
            {
                ConditionKeyType.Tag => UsedTags,
                ConditionKeyType.Flag => UsedFlags,
                ConditionKeyType.Counter => UsedCounters,
                _ => null
            };
            if (dict != null)
            {
                dict.TryGetValue(keyed.ConditionKey, out var count);
                dict[keyed.ConditionKey] = count + 1;
            }
        }

        // 複合条件を再帰的に辿る
        if (condition is AndCondition and)
        {
            foreach (var child in and.Conditions)
                CollectFromCondition(child);
        }
        else if (condition is OrCondition or)
        {
            foreach (var child in or.Conditions)
                CollectFromCondition(child);
        }
        else if (condition is NotCondition not)
        {
            CollectFromCondition(not.Condition);
        }
    }
}
```

**注意**: `AndCondition`, `OrCondition`, `NotCondition`に内部配列へのアクセサが必要:

```csharp
// AndCondition
public ConditionSO[] Conditions => conditions;

// OrCondition
public ConditionSO[] Conditions => conditions;

// NotCondition
public ConditionSO Condition => condition;
```

## UI設計

```
┌─────────────────────────────────────────────────────┐
│ Walking System Manager                              │
├─────────────────────────────────────────────────────┤
│ Flow Graph: [FlowGraph_Stages]      (編集禁止)       │
│ Start Node: [エントランスホール]      (編集禁止)       │
│ ...（通常のフィールド）                (編集禁止)       │
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
│ │   [✓] vip          (2箇所で使用)                │ │
│ │   [ ] healed       (1箇所で使用)                │ │
│ │   [✓] custom_tag   (手動追加)                   │ │
│ └─────────────────────────────────────────────────┘ │
│ ┌─────────────────────────────────────────────────┐ │
│ │ Flags                             [+ Add]       │ │
│ │   [✓] gate_passed  (1箇所で使用)                │ │
│ │   [ ] boss_defeated (手動追加)                  │ │
│ └─────────────────────────────────────────────────┘ │
│ ┌─────────────────────────────────────────────────┐ │
│ │ Counters                          [+ Add]       │ │
│ │   kills: [___5___]                              │ │
│ │   items: [___12__]                              │ │
│ └─────────────────────────────────────────────────┘ │
│ ┌─────────────────────────────────────────────────┐ │
│ │ Encounter Overlays                [+ Add]       │ │
│ │   double_encounter: x2.0 (残り 5歩)    [Remove] │ │
│ └─────────────────────────────────────────────────┘ │
│                                                     │
│ [Refresh Conditions] [Clear Debug State]            │
└─────────────────────────────────────────────────────┘
```

## 収集対象のConditionSO

以下のConditionSOから使用キーを自動収集（IKeyedCondition実装クラス）:

| ConditionSO | キー取得 | 収集先 |
|-------------|----------|--------|
| HasTagCondition | `ConditionKey` (tag) | UsedTags |
| HasFlagCondition | `ConditionKey` (flagKey) | UsedFlags |
| HasCounterCondition | `ConditionKey` (counterKey) | UsedCounters |

### 複合条件の再帰走査

以下の複合条件は内部のConditionを再帰的に辿る:

| 複合Condition | 走査対象 |
|---------------|----------|
| AndCondition | `Conditions[]` |
| OrCondition | `Conditions[]` |
| NotCondition | `Condition` |

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

**対象外**: EventDefinitionSO（現状はConditionフィールドを持たないため）

## Clear Debug State の対象範囲

「Clear Debug State」ボタンは対象4種を**全消去**する。ゲーム進行で設定された値も含めて消去されるため、実行前に確認ダイアログを表示する。

| 対象 | クリア | 備考 |
|------|--------|------|
| タグ | ✓ | GameContext.tags を全消去 |
| フラグ | ✓ | GameContext.flags を全消去 |
| カウンター | ✓ | GameContext.counters を全消去 |
| オーバーレイ | ✓ | EncounterOverlayStack を全消去 |
| 歩数 | ✗ | 進行状態は維持 |
| アンカー | ✗ | 進行状態は維持 |
| ゲート状態 | ✗ | 進行状態は維持 |

**確認ダイアログ**: 「タグ/フラグ/カウンター/オーバーレイをすべてクリアします。ゲーム中に設定された値も消去されますが、よろしいですか？」

## 実装フェーズ

### Phase 0: 前提準備

0. `IKeyedCondition.cs` インターフェース作成
1. `HasTagCondition`, `HasFlagCondition`, `HasCounterCondition` に `IKeyedCondition` 実装
2. `AndCondition`, `OrCondition`, `NotCondition` に内部アクセサ追加
3. `WalkingSystemManager` に `GameContext` publicゲッター追加

### Phase 1: 基盤（必須）

4. `WalkingSystemManagerEditor.cs` 作成
   - デフォルトインスペクター描画（PlayMode中は編集禁止）
   - PlayMode判定
   - デバッグセクションのFoldout

5. `WalkConditionCollector.cs` 作成
   - FlowGraph走査ロジック
   - IKeyedCondition対応
   - And/Or/Not再帰走査

6. タグ操作UI
   - 収集したタグの一覧表示（使用回数付き）
   - チェックボックスでトグル

### Phase 2: 拡張

7. フラグ操作UI
8. カウンター操作UI
9. オーバーレイ表示UI

### Phase 3: 利便性向上

10. カスタム値の手動追加UI
11. 状態サマリー表示
12. Refresh/Clear Debug Stateボタン（確認ダイアログ付き）

## 実装上の注意点

### GameContextへのアクセス

```csharp
// 選択中のManagerに紐付くGameContextを取得
// ※ GameContextHub.Current ではなく、target経由で取得
var manager = (WalkingSystemManager)target;
var context = manager.GameContext; // publicプロパティが必要
```

**理由**: 将来的に複数のWalkingSystemManagerが存在する場合、`GameContextHub.Current`は最後にアクティブになったものを指すため、Inspector選択と一致しない可能性がある。

**必要な変更**: WalkingSystemManagerにGameContextのpublicゲッターを追加

```csharp
// WalkingSystemManager.cs
public GameContext GameContext => gameContext;
```

### Condition型の判定（IKeyedCondition使用）

```csharp
// リフレクション不要、インターフェースで安全に取得
if (condition is IKeyedCondition keyed)
{
    var key = keyed.ConditionKey;
    var type = keyed.KeyType;
    // ...
}
```

### パフォーマンス考慮

- Condition収集はFlowGraph変更時またはRefreshボタン押下時のみ実行
- 収集結果はキャッシュし、毎フレーム再計算しない

### PlayMode中の安全性

```csharp
// 通常フィールドは編集禁止にして誤操作を防止
if (Application.isPlaying)
{
    GUI.enabled = false;
}
DrawDefaultInspector();
GUI.enabled = true;
```

## テスト方法

1. PlayModeでWalkingSystemManagerを選択
2. Debugセクションが表示されることを確認
3. 通常フィールドが編集禁止になっていることを確認
4. タグのチェックボックスをONにし、コンソールで確認
5. ゲート通過後、自動的にタグが付与されチェックが入ることを確認
6. Clear Debug Stateで確認ダイアログが表示されることを確認

## 成果物

### 新規ファイル
- `Assets/Script/Walk/Conditions/IKeyedCondition.cs`
- `Assets/Editor/Walk/WalkingSystemManagerEditor.cs`
- `Assets/Editor/Walk/WalkConditionCollector.cs`

### 変更ファイル
- `Assets/Script/Walk/Conditions/HasTagCondition.cs` (IKeyedCondition実装)
- `Assets/Script/Walk/Conditions/HasFlagCondition.cs` (IKeyedCondition実装)
- `Assets/Script/Walk/Conditions/HasCounterCondition.cs` (IKeyedCondition実装)
- `Assets/Script/Walk/Conditions/AndCondition.cs` (Conditionsアクセサ追加)
- `Assets/Script/Walk/Conditions/OrCondition.cs` (Conditionsアクセサ追加)
- `Assets/Script/Walk/Conditions/NotCondition.cs` (Conditionアクセサ追加)
- `Assets/Script/Walk/WalkingSystemManager.cs` (GameContextゲッター追加)

## FAQ

### Q: EventDefinition内の条件も収集対象ですか？

A: **現状は対象外**。`EventDefinitionSO`は`ConditionSO`フィールドを持たず、`EffectSO`のみを持つため収集不要。将来的にEventStepに条件フィールドを追加する場合は収集対象に加える。

### Q: 複数のWalkingSystemManagerがある場合はどうなりますか？

A: **Inspectorは選択中のManagerに紐付ける**方針。`GameContextHub.Current`は最後にアクティブになったManagerを指すため、複数Manager時に意図しないContextを参照する可能性がある。`target`経由で確実に選択中のManagerのContextを取得する。

（ただし現状はManager1つのため、どちらでも同じ動作になる）

## 将来の拡張

- EditorWindow版（独立ウィンドウとして常時表示）
- 複数GameContextの比較表示
- 状態のスナップショット保存/復元
- PlayModeテストとの統合
- EventDefinitionSOに条件フィールド追加時の収集対応
