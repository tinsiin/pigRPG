# MessageDropper 仕様書

## 概要

MessageDropperは、テキストメッセージを画面上にフローティング表示するUIコンポーネント。
メッセージブロック（MessageBlock）をプレハブから生成し、下から上へ自動スクロールさせて親境界を超えたら破棄する。

## 設計方針

### 用途の限定

**MessageDropperは歩行パート・ノベルパート専用のメッセージ表示システムとする。**

戦闘中のメッセージ表示には使用しない。戦闘中のテキスト表示は以下の専用システムで行う：

| 戦闘中のテキスト表示 | 担当システム |
|---|---|
| ターンログ（ダメージ・ミス等） | SchizoLog（タイプライター演出付き）。詳細は `doc/SchizoLog仕様書.md` 参照 |
| ステータス変化・デバフ等の通知 | BattleIcon上のエフェクト等 |

### 戦闘からの除去計画

現在、戦闘中に以下の経路でMessageDropperが呼ばれる可能性がある：

```
BattlePresentation.CreateBattleMessage(txt)
  → BattleEventBus.Publish(BattleEvent.MessageOnly(...))
    → BattleUiEventAdapter.OnBattleEvent(Message)
      → IBattleUiAdapter.PushMessage(message)
        → BattleUIBridge.PushMessage(message)
          → MessageDropper.CreateMessage(message)
```

この経路は**廃止予定**。`BattleEventType.Message` による MessageDropper への送出は今後削除する。

## コンポーネント構成

### MessageDropper（MonoBehaviour）

メッセージ生成と管理を行う親コンポーネント。

| フィールド | 型 | 説明 |
|---|---|---|
| MessageBlockPrefab | MessageBlock | 生成するメッセージのプレハブ |
| MessageUpspeed | float | メッセージの上昇速度 |
| MessageSpaceY | float | メッセージ間の最低スペース |

**ソースファイル**: `Assets/Script/MessageDropper.cs`

#### API

| メソッド | 説明 |
|---|---|
| `CreateMessage(string txt)` | メッセージブロックを生成し、既存メッセージとの重なりを回避して配置する |

#### 動作

1. MessageBlockPrefabをInstantiateして子として生成
2. `OnCreated()` で上昇速度・テキスト・親RectTransformを渡す
3. 破棄済みオブジェクトをリストからクリーンアップ
4. 直前のメッセージと近すぎる場合、既存メッセージ全体を `JumpUp()` で押し上げる
5. リストに追加して管理

### MessageBlock（MonoBehaviour）

個々のメッセージブロック。生成後は自律的に上昇し、親境界を超えたら自壊する。

| フィールド | 型 | 説明 |
|---|---|---|
| tmpText | TextMeshProUGUI | テキスト表示 |
| MessageRect | RectTransform | 自身のRectTransform |

**ソースファイル**: `Assets/Script/MessageBlock.cs`

#### ライフサイクル

1. `OnCreated()`: 速度・テキスト・親矩形を受け取り初期化
2. `Start()`: 背景サイズをテキストに合わせて自動調整（`AdjustBackgroundSize`）
3. `Update()`: 毎フレーム `_upSpeed` 分だけY座標を加算。子の下端が親の上端を超えたら `Destroy(gameObject)`
4. `JumpUp(float)`: 外部から呼ばれ、Y座標を指定分だけ即座に加算（重なり回避用）
5. `ContainBelow(RectTransform, float)`: 他のメッセージが自分の下方スペース内にあるか判定

## 現在の利用箇所

### 歩行パート（継続利用）

| 呼び出し元 | 経路 |
|---|---|
| `WalkingEventUI.ShowMessage(string)` | `messageDropper.CreateMessage(message)` |
| `WalkingSystemManager` | SerializeFieldで保持、`ResolveMessageDropper()` でフォールバック検索 |

### ノベルパート（継続利用）

| 呼び出し元 | 経路 |
|---|---|
| `NovelPartEventUI.ShowText(speaker, text)` | Dinoidモード時のみ `messageDropper.CreateMessage(text)` |

### 戦闘パート（廃止予定）

| 呼び出し元 | 経路 |
|---|---|
| `BattleUIBridge.PushMessage(string)` | `messageDropper?.CreateMessage(message)` |
| `BattleInitializer` | コンストラクタでMessageDropperを受け取りBattleUIBridgeに渡す |
| `BattleServices` | プロパティとして保持 |
| `UnityBattleRunner` | コンストラクタで保持、BattleManager生成時に渡す |

## DI・配線

MessageDropperはシーン上のGameObjectにアタッチされたMonoBehaviour。
各システムへの注入方法：

- **歩行**: `WalkingSystemManager` が `[SerializeField]` で直接参照。nullの場合 `FindObjectOfType<MessageDropper>()` でフォールバック
- **ノベル**: `NovelPartEventUI` が `[SerializeField]` で直接参照
- **戦闘**: `BattleServices` → `BattleInitializer` → `BattleUIBridge` のコンストラクタチェーンで注入

## 関連システムとの違い

| システム | 用途 | 表示方式 |
|---|---|---|
| **MessageDropper** | 歩行・ノベルの短いメッセージ | フローティング・上昇・自動消滅 |
| **SchizoLog** | 戦闘ターンログ | タイプライター演出・LitMotion・履歴管理 |
| **BattleEventHistory** | 戦闘ログ履歴保存 | 表示なし（データのみ）。SchizoLogの表示ソース |
