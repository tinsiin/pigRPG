# GraphViewエディタ構想

本書はFlowGraphSOをGraphViewで視覚的に編集するエディタの構想をまとめたもの。

---

## 背景

### 現状のSOベース設計

```
FlowGraphSO（ステージ全体）
├── nodes[] : NodeSO配列
├── edges[] : EdgeSO配列
└── startNodeId : 開始ノードID
```

- ノード間の接続は文字列ID（fromNodeId, toNodeId）で管理
- Inspector上で全て設定可能
- シンプルで保守しやすい

### 課題

- ノード間の接続が文字列ID → タイポでバグる
- ステージ全体の構造が一目で分からない
- 複雑なステージ（10ノード以上、複雑な分岐）だと管理が困難
- フラグ条件による分岐が増えると把握しづらい

---

## GraphViewとは

Unity UIElements ベースのノードエディタフレームワーク。

- Shader Graph、VFX Graph等で使用実績あり
- ノードをドラッグ配置、線で接続
- ズーム、パン操作
- Unity 2018頃から利用可能、情報が豊富

---

## SOとGraphViewの共存

### 基本方針

**データはSOのまま、GraphViewは「エディタUI」として追加。**

```
[データ層] 変更なし
FlowGraphSO
├── NodeSO[]
└── EdgeSO[]

[エディタ層] 新規追加
FlowGraphEditorWindow
└── FlowGraphView（GraphView継承）
    ├── NodeSOを「ノード」として描画
    └── EdgeSOを「線」として描画
```

### 連携の仕組み

| 操作 | GraphView上 | SO上の反映 |
|------|------------|-----------|
| ノード配置 | ドラッグで移動 | NodeSOに位置情報を保存（UIHints等） |
| ノード追加 | 右クリックメニュー | NodeSO新規作成、FlowGraphSO.nodesに追加 |
| ノード削除 | Deleteキー | FlowGraphSO.nodesから削除 |
| 線で接続 | ポートをドラッグ | EdgeSO新規作成、FlowGraphSO.edgesに追加 |
| 線を削除 | 線を選択→Delete | FlowGraphSO.edgesから削除 |
| ノード詳細編集 | ノードをクリック | Inspectorで編集 |

### Undo/Redo

SerializedObject経由で編集すればUnityのUndo/Redoが自動対応。

---

## 必要な実装

### ファイル構成

```
Assets/Editor/Walk/GraphView/
├── FlowGraphEditorWindow.cs   // EditorWindow
├── FlowGraphView.cs           // GraphView継承
├── FlowNodeView.cs            // Node継承（NodeSO表示用）
├── FlowEdgeView.cs            // Edge表示（必要なら）
└── FlowGraphSearchProvider.cs // ノード追加メニュー
```

### 1. FlowGraphEditorWindow

```csharp
public class FlowGraphEditorWindow : EditorWindow
{
    private FlowGraphSO targetGraph;
    private FlowGraphView graphView;

    [MenuItem("Window/Walk/Flow Graph Editor")]
    public static void Open() { ... }

    // FlowGraphSOをダブルクリックで開く
    [OnOpenAsset]
    public static bool OnOpenAsset(int instanceId, int line) { ... }

    private void OnEnable()
    {
        // GraphView作成、ツールバー追加
    }
}
```

### 2. FlowGraphView

```csharp
public class FlowGraphView : GraphView
{
    private FlowGraphSO graphData;

    public FlowGraphView()
    {
        // 基本設定
        this.AddManipulator(new ContentDragger());
        this.AddManipulator(new SelectionDragger());
        this.AddManipulator(new RectangleSelector());
        SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);

        // スタイルシート
        var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("...");
        styleSheets.Add(styleSheet);
    }

    public void LoadGraph(FlowGraphSO graph)
    {
        graphData = graph;
        // 既存ノードを描画
        foreach (var node in graph.Nodes)
        {
            CreateNodeView(node);
        }
        // 既存エッジを描画
        foreach (var edge in graph.Edges)
        {
            CreateEdgeView(edge);
        }
    }

    public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
    {
        // 接続可能なポートを返す
    }
}
```

### 3. FlowNodeView

```csharp
public class FlowNodeView : Node
{
    public NodeSO NodeData { get; private set; }
    public Port InputPort { get; private set; }
    public Port OutputPort { get; private set; }

    public FlowNodeView(NodeSO nodeData)
    {
        NodeData = nodeData;
        title = nodeData.DisplayName;

        // 入力ポート（このノードへの入口）
        InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
        InputPort.portName = "In";
        inputContainer.Add(InputPort);

        // 出力ポート（このノードからの出口）
        OutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
        OutputPort.portName = "Out";
        outputContainer.Add(OutputPort);

        // ノード概要表示（門の数、出口の数など）
        RefreshExpandedState();
        RefreshPorts();
    }
}
```

---

## 表示する情報

### ノード上に表示

- ノード名（displayName）
- ノードID（nodeId）
- 門の数
- 出口の数
- 開始ノードマーク

### ノード選択時（Inspector）

- NodeSOの全設定項目
- 門、出口、遭遇テーブル等の詳細

### エッジ上に表示（オプション）

- 条件がある場合はアイコン表示
- 重み

---

## 実装優先度

### Phase 1: 最小限の可視化

1. FlowGraphEditorWindow作成
2. FlowGraphView作成
3. FlowNodeView作成（ノード表示のみ）
4. 既存エッジの線描画

**目標: 既存FlowGraphSOを開いて構造を確認できる**

### Phase 2: 編集機能

5. ノードのドラッグ移動 → 位置保存
6. エッジの作成・削除
7. Undo/Redo対応

**目標: GraphView上でグラフ構造を編集できる**

### Phase 3: 高度な機能

8. ノード追加メニュー（SearchWindow）
9. ノード削除
10. ミニマップ
11. 条件付きエッジの視覚化

**目標: フル機能のエディタ**

---

## 位置情報の保存

NodeSOに位置情報を追加する必要がある。

### 案1: NodeUIHintsに追加

```csharp
[System.Serializable]
public struct NodeUIHints
{
    // 既存
    public bool useThemeColors;
    public Color frameArtColor;
    // ...

    // 追加
    public Vector2 graphPosition; // GraphView上の位置
}
```

### 案2: FlowGraphSOに別途保持

```csharp
[CreateAssetMenu(menuName = "Walk/FlowGraph")]
public sealed class FlowGraphSO : ScriptableObject
{
    [SerializeField] private NodeSO[] nodes;
    [SerializeField] private EdgeSO[] edges;
    [SerializeField] private string startNodeId;

    // 追加: ノード位置情報（nodeId → position）
    [SerializeField] private List<NodePositionEntry> nodePositions;
}
```

**案1を推奨。** NodeSO自体に位置情報を持たせた方が管理しやすい。

---

## 参考資料

- Unity公式: [GraphView](https://docs.unity3d.com/ScriptReference/Experimental.GraphView.GraphView.html)
- Shader Graphのソースコード（参考実装）
- Unity Forum: GraphView関連スレッド

---

## 注意事項

### Graph Tools Foundation (GTF)

Unity 2021以降で新しいグラフフレームワーク（GTF）が開発中だが:

- ドキュメントが少ない
- 実験的な部分がある
- 情報が少なく学習コストが高い

**現時点ではGraphViewを推奨。** 将来的にGTFが安定したら移行を検討。

---

## まとめ

- 現在のSOベースのデータ構造は変更不要
- GraphViewは「可視化・編集UI」として後付け可能
- ステージが複雑化したら実装価値あり
- 最小限の可視化から段階的に機能追加可能
