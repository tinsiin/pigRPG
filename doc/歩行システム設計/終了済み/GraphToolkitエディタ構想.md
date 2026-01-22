# Graph Toolkitエディタ構想

本書はFlowGraphSOをGraph Toolkitで視覚的に編集するエディタの構想をまとめたもの。

相性悪いから断念した
→GraphToolkitと歩行システムの相性評価.md
今後は　可視化、診断ノード繋がりツールとして歩行システムに
既存のsoだけの形式で続けることにする。
しかも可視化ツールだけなら　むしろgraphtoolkitはオーバースペックなので、完全に使わないことに(歩行システム管理としては、ただまぁ局所的な所で今後向いてるかもしれないところが出るかもね)
---

## 背景

### 現状のSOベース設計

```
FlowGraphSO（ステージ全体）
├── nodes[] : NodeSO配列
└── startNodeId : 開始ノードID

NodeSO（各ノード）
└── exits[] : ExitCandidate配列（toNodeId, weight, conditions, uiLabel）
```

- ノード間の接続はNodeSO内のExitCandidate（toNodeId文字列）で管理
- Inspector上で全て設定可能
- シンプルで保守しやすい

### 課題

- ノード間の接続が文字列ID → タイポでバグる
- ステージ全体の構造が一目で分からない
- 複雑なステージ（10ノード以上、複雑な分岐）だと管理が困難
- フラグ条件による分岐が増えると把握しづらい

---

## Graph Toolkitとは

Unity公式のノードエディタフレームワーク（com.unity.graphtoolkit）。

| 特徴 | 説明 |
|------|------|
| パッケージ | com.unity.graphtoolkit 0.4.0-exp.2 |
| 状態 | 実験的（experimental） |
| UIElements | 自動生成（手動実装不要） |
| データ形式 | 独自ファイル形式 + Importer でランタイムSOに変換 |

### GraphView（旧）との比較

| 観点 | GraphView | Graph Toolkit |
|------|-----------|---------------|
| 抽象度 | 低い（UIを自前実装） | 高い（UI自動生成） |
| コード量 | 多い | 少ない |
| ポート定義 | 手動でInstantiatePort | `OnDefinePorts()`で宣言的に |
| ノード配置 | 手動実装 | 自動処理 |
| Undo/Redo | 手動実装 | 自動対応 |

---

## SOとGraph Toolkitの共存

### 基本方針

**Graph Toolkit独自ファイル（.flowgraph）をエディタで編集し、Importerで既存のFlowGraphSOに変換する。**

```
[エディタ層] Graph Toolkit
FlowEditorGraph : Graph
├── FlowEditorNode : Node（各ノード）
└── 接続情報（ポート間のワイヤー）
↓
[変換層] ScriptedImporter
FlowGraphImporter
↓
[ランタイム層] 既存SO（変更なし）
FlowGraphSO
└── NodeSO[]
    └── ExitCandidate[]（各ノード内）
```

### 連携の仕組み

| 操作 | Graph Toolkit上 | SO上の反映 |
|------|----------------|-----------|
| ノード配置 | ドラッグで移動 | Importer実行時にNodeSOに反映 |
| ノード追加 | 右クリックメニュー | Importer実行時にNodeSO生成 |
| ノード削除 | Deleteキー | Importer実行時にFlowGraphSOから除外 |
| 線で接続 | ポートをドラッグ | Importer実行時にNodeSOのExitCandidateに反映 |
| 線を削除 | 線を選択→Delete | Importer実行時にExitCandidateから除外 |
| ノード詳細編集 | ノード上のフィールド編集 | Importer実行時にNodeSOに反映 |

### Undo/Redo

Graph Toolkitが自動対応。追加実装不要。

---

## 実装設計

### ファイル構成

```
Assets/Editor/Walk/GraphToolkit/
├── Model/
│   ├── FlowEditorGraph.cs      // Graph継承（グラフ定義）
│   ├── FlowEditorNode.cs       // Node継承（基底ノード）
│   ├── FlowStartNode.cs        // 開始ノード
│   └── FlowAreaNode.cs         // エリアノード
├── Import/
│   └── FlowGraphImporter.cs    // ScriptedImporter（SO変換）
└── FlowGraph.Editor.asmdef     // アセンブリ定義
```

### 1. FlowEditorGraph（グラフ定義）

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEditor;

namespace Walk.Editor.GraphToolkit
{
    /// <summary>
    /// FlowGraph のエディタグラフ定義
    /// </summary>
    [Serializable]
    [Graph(AssetExtension)]
    internal class FlowEditorGraph : Graph
    {
        internal const string AssetExtension = "flowgraph";

        [MenuItem("Assets/Create/Walk/Flow Graph")]
        static void CreateAssetFile()
        {
            GraphDatabase.PromptInProjectBrowserToCreateNewAsset<FlowEditorGraph>("New FlowGraph");
        }

        /// <summary>
        /// グラフ変更時のリアルタイムバリデーション
        /// 既存GraphValidatorのロジックを移植
        /// </summary>
        public override void OnGraphChanged(GraphLogger infos)
        {
            base.OnGraphChanged(infos);
            ValidateStartNode(infos);
            ValidateNodeIds(infos);
            ValidateConnections(infos);
            ValidateReachability(infos);
        }

        // 開始ノードのチェック
        void ValidateStartNode(GraphLogger infos)
        {
            var startNodes = GetNodes().OfType<FlowStartNode>().ToList();
            if (startNodes.Count == 0)
            {
                infos.LogError("開始ノードが必要です", this);
            }
            else if (startNodes.Count > 1)
            {
                infos.LogWarning("開始ノードは1つのみ有効です", startNodes[1]);
            }
        }

        // NodeId重複・空チェック
        void ValidateNodeIds(GraphLogger infos)
        {
            var nodeIds = new HashSet<string>();
            foreach (var node in GetNodes().OfType<FlowAreaNode>())
            {
                if (string.IsNullOrEmpty(node.NodeId))
                {
                    infos.LogError("NodeIdが空です", node);
                }
                else if (!nodeIds.Add(node.NodeId))
                {
                    infos.LogError($"NodeId '{node.NodeId}' が重複しています", node);
                }
            }
        }

        // 出口先の存在確認
        void ValidateConnections(GraphLogger infos)
        {
            var validNodeIds = new HashSet<string>(
                GetNodes().OfType<FlowAreaNode>().Select(n => n.NodeId));

            foreach (var node in GetNodes().OfType<FlowAreaNode>())
            {
                // ポート接続先のNodeIdを確認
                foreach (var port in node.GetOutputPortEnumerable())
                {
                    foreach (var connected in port.connectedPorts)
                    {
                        if (connected.GetNode() is FlowAreaNode targetNode)
                        {
                            if (!validNodeIds.Contains(targetNode.NodeId))
                            {
                                infos.LogError($"出口先 '{targetNode.NodeId}' が存在しません", node);
                            }
                        }
                    }
                }

                // 出口がないノード（デッドエンド）の警告
                var hasExit = node.GetOutputPortEnumerable().Any(p => p.connectedPorts.Any());
                if (!hasExit)
                {
                    infos.LogWarning($"ノード '{node.NodeId}' に出口がありません（デッドエンド）", node);
                }
            }
        }

        // 到達不能ノードのチェック
        void ValidateReachability(GraphLogger infos)
        {
            var startNode = GetNodes().OfType<FlowStartNode>().FirstOrDefault();
            if (startNode == null) return;

            var reachable = new HashSet<INode>();
            var queue = new Queue<INode>();

            // 開始ノードからBFS
            queue.Enqueue(startNode);
            reachable.Add(startNode);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var port in current.GetOutputPortEnumerable())
                {
                    foreach (var connected in port.connectedPorts)
                    {
                        var targetNode = connected.GetNode();
                        if (targetNode != null && reachable.Add(targetNode))
                        {
                            queue.Enqueue(targetNode);
                        }
                    }
                }
            }

            // 到達不能ノードを警告
            foreach (var node in GetNodes().OfType<FlowAreaNode>())
            {
                if (!reachable.Contains(node))
                {
                    infos.LogWarning($"ノード '{node.NodeId}' は開始ノードから到達できません", node);
                }
            }
        }
    }
}
```

### 2. FlowEditorNode（基底ノード）

```csharp
using System;
using Unity.GraphToolkit.Editor;

namespace Walk.Editor.GraphToolkit
{
    /// <summary>
    /// FlowGraph ノードの基底クラス
    /// </summary>
    [Serializable]
    internal abstract class FlowEditorNode : Node
    {
        public const string EXECUTION_PORT = "Execution";

        /// <summary>
        /// 標準の入出力ポートを追加
        /// </summary>
        protected void AddExecutionPorts(IPortDefinitionContext context)
        {
            context.AddInputPort(EXECUTION_PORT)
                .WithDisplayName("In")
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .Build();

            context.AddOutputPort(EXECUTION_PORT)
                .WithDisplayName("Out")
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .Build();
        }
    }
}
```

### 3. FlowAreaNode（エリアノード）

```csharp
using System;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Walk.Editor.GraphToolkit
{
    /// <summary>
    /// ステージ内の1エリア（NodeSOに相当）
    /// </summary>
    [Serializable]
    internal class FlowAreaNode : FlowEditorNode
    {
        // NodeSO相当のフィールド
        [SerializeField] private string nodeId;
        [SerializeField] private string displayName;
        [SerializeField] private int exitSteps = 100;

        // 門の数（詳細設定はInspector拡張で）
        [SerializeField] private int gateCount;

        // 遭遇テーブル参照
        [SerializeField] private EncountTableSO encountTable;

        public string NodeId => nodeId;
        public string DisplayName => displayName;
        public int ExitSteps => exitSteps;
        public int GateCount => gateCount;
        public EncountTableSO EncountTable => encountTable;

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            AddExecutionPorts(context);

            // 複数出口がある場合は追加ポートを定義可能
            // context.AddOutputPort("Exit_Castle").WithDisplayName("城へ").Build();
        }
    }
}
```

### 4. FlowStartNode（開始ノード）

```csharp
using System;
using Unity.GraphToolkit.Editor;

namespace Walk.Editor.GraphToolkit
{
    /// <summary>
    /// グラフの開始点
    /// </summary>
    [Serializable]
    internal class FlowStartNode : FlowEditorNode
    {
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            // 開始ノードは出力のみ
            context.AddOutputPort(EXECUTION_PORT)
                .WithDisplayName("Start")
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .Build();
        }
    }
}
```

### 5. FlowGraphImporter（SO変換）

```csharp
using System.Collections.Generic;
using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Walk.Editor.GraphToolkit
{
    /// <summary>
    /// .flowgraph → FlowGraphSO 変換
    /// </summary>
    [ScriptedImporter(1, FlowEditorGraph.AssetExtension)]
    internal class FlowGraphImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var graph = GraphDatabase.LoadGraphForImporter<FlowEditorGraph>(ctx.assetPath);
            if (graph == null)
            {
                Debug.LogError($"Failed to load FlowGraph: {ctx.assetPath}");
                return;
            }

            // ランタイムアセット作成
            var runtimeGraph = ScriptableObject.CreateInstance<FlowGraphSO>();

            // 開始ノード取得
            var startNode = graph.GetNodes().OfType<FlowStartNode>().FirstOrDefault();
            if (startNode != null)
            {
                var firstArea = GetNextNode(startNode) as FlowAreaNode;
                if (firstArea != null)
                {
                    runtimeGraph.SetStartNodeId(firstArea.NodeId);
                }
            }

            // ノード変換（接続情報はExitCandidateとしてノード内に保持）
            var nodes = new List<NodeSO>();
            var nodeMap = new Dictionary<string, (FlowAreaNode editorNode, NodeSO runtimeNode)>();

            foreach (var areaNode in graph.GetNodes().OfType<FlowAreaNode>())
            {
                // NodeSO生成
                var nodeSO = ConvertToNodeSO(areaNode);
                nodes.Add(nodeSO);
                nodeMap[areaNode.NodeId] = (areaNode, nodeSO);
                ctx.AddObjectToAsset(areaNode.NodeId, nodeSO);
            }

            // 接続をExitCandidateに変換
            foreach (var (nodeId, pair) in nodeMap)
            {
                var exits = new List<ExitCandidate>();
                var outputPort = pair.editorNode.GetOutputPortByName(FlowEditorNode.EXECUTION_PORT);
                foreach (var connectedPort in outputPort.connectedPorts)
                {
                    var targetNode = connectedPort.GetNode() as FlowAreaNode;
                    if (targetNode != null)
                    {
                        exits.Add(CreateExitCandidate(targetNode.NodeId));
                    }
                }
                pair.runtimeNode.SetExits(exits.ToArray());
            }

            runtimeGraph.SetNodes(nodes.ToArray());

            ctx.AddObjectToAsset("MainAsset", runtimeGraph);
            ctx.SetMainObject(runtimeGraph);
        }

        static NodeSO ConvertToNodeSO(FlowAreaNode areaNode)
        {
            var nodeSO = ScriptableObject.CreateInstance<NodeSO>();
            // フィールドをコピー
            // nodeSO.nodeId = areaNode.NodeId;
            // nodeSO.displayName = areaNode.DisplayName;
            // ...
            return nodeSO;
        }

        static ExitCandidate CreateExitCandidate(string toNodeId)
        {
            return new ExitCandidate
            {
                Id = System.Guid.NewGuid().ToString(),
                ToNodeId = toNodeId,
                Weight = 1,
                UILabel = null // ノードエディタで個別設定
            };
        }

        static INode GetNextNode(INode currentNode)
        {
            var outputPort = currentNode.GetOutputPortByName(FlowEditorNode.EXECUTION_PORT);
            return outputPort?.firstConnectedPort?.GetNode();
        }
    }
}
```

---

## ノード上に表示する情報

### グラフビュー上

| 情報 | 表示方法 |
|------|---------|
| ノード名 | タイトル |
| ノードID | サブタイトル |
| 門の数 | バッジまたはラベル |
| 出口歩数 | フィールド |
| 開始ノード | 特別なアイコン |

### ノード選択時

- Graph Toolkitのインスペクタで詳細フィールドを編集
- 門、遭遇テーブル、条件等の複雑な設定

### 出口（ExitCandidate）上

- 条件がある場合はアイコン表示（将来対応）
- 重み（WeightedRandom用）
- UIラベル（プレイヤーに表示する選択肢名）

**注**: Graph Toolkitのワイヤー（線）自体にはメタデータを載せられないため、条件・重み・UIラベル等はノード側のフィールドで管理する。

---

## 実装優先度

### Phase 1: 最小限の可視化

1. FlowEditorGraph作成（.flowgraph拡張子）
2. FlowStartNode作成
3. FlowAreaNode作成（基本フィールドのみ）
4. FlowGraphImporter作成（FlowGraphSOへの変換）

**目標: .flowgraphファイルを作成・編集し、FlowGraphSOとして使用できる**

### Phase 2: 詳細設定とバリデーション

5. FlowAreaNodeに詳細フィールド追加（門、遭遇テーブル等）
6. ExitCandidateの詳細設定（条件、重み、UIラベル）
7. Node Optionsで動的出口ポート数を設定（下記「Node Options」参照）
8. GraphLoggerバリデーション実装（上記FlowEditorGraphに設計済み）
   - 既存GraphValidator.csのロジックを移植
   - リアルタイムでエラー/警告を表示

**目標: 既存のNodeSO相当の設定が可能 + エディタ上で即時フィードバック**

### Phase 3: 高度な機能（必要に応じて）

8. Subgraph対応（複雑なステージ構造のグループ化）
9. ContextNode + BlockNode 導入（門/出口の視覚化）
    - 通常ノードで運用してみて必要性を判断
    - 導入する場合、FlowAreaNode → FlowAreaContextNode に移行

**目標: 視覚的な門/出口管理**

※ 移行ツール（既存FlowGraphSO → .flowgraph変換）は作成しない。
  既存システムと並行運用し、使い勝手を比較してから判断する。

---

## 活用可能なGraph Toolkit機能

### Node Options（動的ポート数）

`OnDefineOptions`で出口数を動的に設定できる。

**重要: ポートID命名規約**

ポート名はIDとして使われるため、安定したIDを使用し、表示名はラベルで分離する:
- **ポートID**: `ExitCandidate.Id`（GUID等の安定ID）を使用
- **表示名**: `WithDisplayName()` で `UILabel` を設定
- **メリット**: 表示名を変更しても接続が壊れない

```csharp
[Serializable]
internal class FlowAreaNode : FlowEditorNode
{
    const string k_ExitCountName = "ExitCount";

    // 各出口のID/ラベル情報を保持
    [SerializeField] private List<ExitPortInfo> exitPorts = new();

    [Serializable]
    internal class ExitPortInfo
    {
        public string id;       // GUID（安定ID）
        public string uiLabel;  // 表示名
        public int weight = 1;
        // conditions等は別途
    }

    protected override void OnDefineOptions(IOptionDefinitionContext context)
    {
        context.AddOption<int>(k_ExitCountName)
            .WithDisplayName("出口数")
            .WithDefaultValue(1)
            .Delayed();
    }

    protected override void OnDefinePorts(IPortDefinitionContext context)
    {
        // 入力ポート
        context.AddInputPort(EXECUTION_PORT)
            .WithDisplayName("In")
            .WithConnectorUI(PortConnectorUI.Arrowhead)
            .Build();

        // 動的な出口ポート
        var exitCountOption = GetNodeOptionByName(k_ExitCountName);
        exitCountOption.TryGetValue<int>(out var exitCount);

        // exitPorts配列を出口数に合わせる
        EnsureExitPortCount(exitCount);

        for (int i = 0; i < exitCount; i++)
        {
            var exitPort = exitPorts[i];
            context.AddOutputPort(exitPort.id)           // GUID = 安定ID
                .WithDisplayName(exitPort.uiLabel ?? $"出口{i + 1}")  // 表示名
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .Build();
        }
    }

    void EnsureExitPortCount(int count)
    {
        // 不足分を追加
        while (exitPorts.Count < count)
        {
            exitPorts.Add(new ExitPortInfo
            {
                id = System.Guid.NewGuid().ToString(),
                uiLabel = null
            });
        }
        // 超過分は削除しない（接続情報を保持）
    }
}
```

### ContextNode + BlockNode（門/出口の視覚化）

門や出口が複雑化した場合、ノード内にブロックとして視覚的に配置できる。

**通常ノード（現在の設計）**:
```
┌─────────────────────────────┐
│ FlowAreaNode                │
│  nodeId: "forest_01"        │
│  gates: [Inspector配列]     │  ← 配列をInspectorで編集
│  exits: [Inspector配列]     │
│  [In] ──────────── [Out]    │
└─────────────────────────────┘
※ 門の詳細はInspectorを開かないと見えない
```

**ContextNode + BlockNode（将来の拡張案）**:
```
┌─────────────────────────────────────────────┐
│ FlowAreaNode (ContextNode)                  │
│  nodeId: "forest_01"                        │
│                                             │
│  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐
│  │ GateBlock   │ │ GateBlock   │ │ GateBlock   │
│  │ "ボス門"    │ │ "鍵門"      │ │ "謎解き門"  │
│  │ pos: 50歩  │ │ pos: 80歩  │ │ pos: 100歩 │
│  └─────────────┘ └─────────────┘ └─────────────┘
│         ↑ ドラッグで順序変更可能 ↑
│                                             │
│  ┌─────────────┐ ┌─────────────┐             │
│  │ ExitBlock   │ │ ExitBlock   │             │
│  │ "城へ"      │→│ "村へ"      │→ [接続先]   │
│  └─────────────┘ └─────────────┘             │
└─────────────────────────────────────────────┘
※ グラフ上で全て見える、ドラッグで並べ替え可能
```

**実装コード例**:
```csharp
// コンテキストノード（親）
[Serializable]
internal class FlowAreaContextNode : ContextNode
{
    [SerializeField] private string nodeId;
    [SerializeField] private string displayName;

    // 子ブロック（gates/exits）は自動管理される

    protected override void OnDefinePorts(IPortDefinitionContext context)
    {
        context.AddInputPort("In").Build();
        // 出口ポートは子のExitBlockが定義
    }
}

// 門ブロック（子）
[Serializable]
internal class GateBlockNode : BlockNode
{
    [SerializeField] private string gateId;
    [SerializeField] private int order;
    [SerializeField] private int positionSteps;
    [SerializeField] private ConditionSO[] passConditions;
}

// 出口ブロック（子）
[Serializable]
internal class ExitBlockNode : BlockNode
{
    [SerializeField] private string exitId;
    [SerializeField] private string uiLabel;
    [SerializeField] private int weight;

    protected override void OnDefinePorts(IPortDefinitionContext context)
    {
        context.AddOutputPort(exitId)
            .WithDisplayName(uiLabel ?? "Exit")
            .Build();
    }
}
```

**導入タイミング**: Phase 3以降。通常ノードで運用してみて、視覚化が必要と感じたら導入。

### Subgraph（サブグラフ）

複雑なステージ構造をグループ化し、再利用可能にする:

- **Local Subgraph**: 単一グラフ内でのグループ化
- **Asset Subgraph**: 複数グラフで再利用可能なサブグラフ

### Variables / Blackboard（将来検討）

グラフ全体で共有する変数。フラグ条件の可視化に活用可能。

---

## 運用方針: 並行運用（共存）

### 基本方針

**既存システムとGraph Toolkitを並行して使用する。**

移行ツールは作成しない。理由:
- 既存システムで本番データは作成していない
- どちらが便利か実際に使ってから判断する
- 必要になった時点で移行を検討すればよい

### 並行運用の構成

```
既存ステージ: FlowGraphSO.asset（Inspector編集）
新規ステージ: .flowgraph（Graph Toolkit編集）

両方とも FlowGraphSO としてランタイムで使用可能
```

### 互換性

- 両形式とも最終的に `FlowGraphSO` になる
- ランタイムコードは変更不要
- ステージ参照箇所で `.asset` でも `.flowgraph` でも同じように扱える

---

## 注意事項

### 実験的パッケージ

Graph Toolkit 0.4.0-exp.2 は実験的（experimental）パッケージ:

- APIが変更される可能性あり
- Unity 6以降で安定化予定
- 本番投入前にバージョン確認必須

### 制限事項

| 制限 | 対応 |
|------|------|
| 複雑なNodeSO設定 | カスタムInspector拡張で対応 |
| GateMarker等のSO参照 | インライン編集またはSO参照フィールド |
| 既存ワークフローとの共存 | 移行期間中は両方式をサポート |

---

## まとめ

- **データ形式**: .flowgraph（エディタ用）→ FlowGraphSO（ランタイム用）
- **編集方法**: Graph Toolkitのビジュアルエディタ
- **運用方針**: 既存Inspector編集と並行運用（移行不要）
- **互換性**: 既存のFlowGraphSOベースのランタイムは変更なし
- **メリット**: UIコード不要、Undo/Redo自動、ノード配置の視覚化
- **判断基準**: 実際に両方使ってみて、便利な方に後から寄せる
