# SchizoLog（統合失調的ログシステム）仕様書

## 概要

戦闘中の出来事を「統合失調者のブログ」のような乱雑な文章体で表示するログシステム。
Elonaのログをさらに砕いた、囲いも装飾もない散文的なテキスト表示。

## 設計思想（メモより）

- 色が付いただけの雑な統合失調症の文章のようなログシステム
- 囲いを作らず、左から文字がポップアウトする
- スキルの名前は出さない（矢印や詳細ログで見せる）
- 高速ボタン押しで文字が被る現象はコンセプトに合うのであえて残す
- ログはAddして溜め、ボタン処理1回分の最後にまとめて放出する

## ソースファイル

`Assets/Script/SchizoLog.cs`

## クラス構成

### SchizoLog（MonoBehaviour, シングルトン）

DontDestroyOnLoad で永続化。

#### インスペクタ設定

| フィールド | 型 | デフォルト | 説明 |
|---|---|---|---|
| `_maxLines` | int | 8 | 表示の最大行数。超過分は上から削除 |
| `_charInterval` | float | 0.04f | 1文字あたりの表示間隔（秒） |
| `LogText` | TextMeshProUGUI | - | テキスト表示先（1つのTMPに全ログを保持） |
| `_bridgeCollection` | LogBridgeCollection | - | ブリッジ文節SO（直アサイン必須） |

#### 内部状態

| フィールド | 説明 |
|---|---|
| `_entries` | 未表示のログエントリリスト |
| `_displayBuffer` | 永続的な表示テキストバッファ（StringBuilder）。アニメーション完了後も保持 |
| `_isDisplaying` | 表示処理中フラグ（二重起動防止・割り込み制御） |
| `_textHandle` | LitMotion のモーションハンドル |
| `_suppressFlushOnCancel` | true時、キャンセルでの残り文字即時描画を抑止 |

### SchizoLogEntry

| フィールド | 型 | 説明 |
|---|---|---|
| Sentence | string | 表示する文章 |
| Priority | int | 高いほど上に表示される |
| InsertOrder | int | 同優先度での先着順維持用 |

### LogBridgeCollection（ScriptableObject）

文と文の間に挿入する「ブリッジ文節」のリスト。

デフォルト値: `。。、` / `、。。、` / `。、。` / `、、、` / `。。。` / `、。、。` / `。、、。`

## API

### 外部向け

| メソッド | シグネチャ | 説明 |
|---|---|---|
| AddLog | `(string sentence, bool important = false, int priority = 0)` | ログをキューに追加 |
| DisplayAllAsync | `() → UniTask` | キュー内のログを優先度順に表示。タイプライターアニメーション付き |
| HardStopAndClearAsync | `() → UniTask` | アニメーション即時停止 + 全クリア |
| ClearLogs | `()` | エントリとバッファとUI全てをクリア |
| SetVisible | `(bool visible)` | LogText の GameObject を表示/非表示 |
| IsVisible | `() → bool` | 現在の表示状態 |

### テスト用（ContextMenu）

- `テストログ実行` — テスト文章でアニメーション確認
- `単一テストログ追加` — ランダムな1件を追加
- `現在のログを表示` — DisplayAllAsync 実行
- `エントリ内容表示` — 現在キューの内容をDebug.Log
- `ログクリア` — ClearLogs 実行

## 処理フロー

### 1. ログ蓄積（AddBattleLog → eventHistory）

戦闘中、各処理から `AddBattleLog()` が呼ばれると `eventHistory` に蓄積される。
SchizoLog には直接追加せず、eventHistory が唯一のデータソースとなる。

```
BaseStates.AddBattleLog(message)
  → BattleUIBridge.AddLog(message, important)
    → eventHistory.Add(message, important)  ← ここに溜まるだけ
```

### 2. ログ放出（ACTPop → DisplayLogs）

ACTPopのタイミングで、前回放出以降の新規エントリだけを SchizoLog に渡して表示する。

```
TurnExecutor.ACTPop()
  → BattleEvent.UiDisplayLogs()
    → BattleUIBridge.DisplayLogs()
      → eventHistory.AdvanceDisplayCursor() で新規分の範囲を取得
      → 新規エントリのみ _schizoLog.AddLog()
      → _schizoLog.DisplayAllAsync().Forget()
```

### 3. 表示（DisplayAllAsync）

```
DisplayAllAsync()
  ├─ 割り込み検出（_isDisplaying==true）
  │   ├─ CancelTypingInternal(flush) → LitMotionのモーション停止
  │   ├─ maxVisibleCharacters = text.Length（現在行を即時確定）
  │   └─ await _isDisplaying==false
  ├─ エントリを優先度降順 → 挿入順昇順でソート
  ├─ ConvertEntriesToString() → _displayBuffer に追記
  │   └─ エントリ間に70%確率でブリッジ文節を挿入
  ├─ DisplayTextAsync() → LitMotionでタイプライターアニメーション
  │   ├─ duration = _charInterval × 追加文字数
  │   ├─ LMotion.Create(0, targetCount, duration).Bind(...)
  │   │   └─ AppendCharsRangeToDisplayBuffer() で複数文字まとめてUI更新
  │   └─ 行数が_maxLines超過 → RemoveTopLineFromDisplayBuffer()で上から削除
  └─ RemoveProcessedEntries() → 表示済みエントリを _entries から除去
```

### 4. 戦闘終了（HardStopAndClear）

```
BattleManager.OnBattleEnd()
  → BattleUIBridge.HardStopAndClearLogs()
    → eventHistory.Clear()（カーソルもリセット）
    → _schizoLog.HardStopAndClearAsync().Forget()
```

## タイプライターアニメーション

- **LitMotion** で `0 → 追加文字数` を線形補間
- `MotionScheduler.UpdateIgnoreTimeScale` でポーズ中も進行
- 1文字ごとではなく `AppendCharsRangeToDisplayBuffer()` で複数文字をまとめて追加しUI更新を1回に抑制
- 行数が `_maxLines` を超えたら `RemoveTopLineFromDisplayBuffer()` で上から行単位で削除
- キャンセル時: デフォルトでは残り文字を一気に描画（`_suppressFlushOnCancel=true` なら抑止）

## 現在のログメッセージ一覧

| ファイル | 内容 |
|---|---|
| BaseStates.Damage.cs | `"{攻撃者名}が{対象名}を攻撃した-「{totalDmg}」"` |
| BaseStates.ReactionSkill.cs | `"{攻撃者名}は外した"` |

## シーン上の配置

```
AlwaysCanvas
  └─ EyeArea
      └─ ViewportArea
          └─ SchizoLog
              └─ LogText (TextMeshProUGUI)
```

## 現状の課題

- **ログメッセージが2種類のみ**: ダメージ通知と攻撃ミスだけ。今後、複雑な戦闘状況（特殊ターン処理、割り込みカウンター等）の専用メッセージを追加していく必要がある
- **コンセプトとの乖離**: メモで描かれた「統合失調的な乱雑さ」の演出（ランダム句読点、文字サイズ揺らぎ、グリッチ等）は未実装。現状はタイプライター＋ブリッジ文節のみ
- **表示位置の問題**: EyeArea内はランダム配置のアイコンとエフェクトで埋まるため、テキストの安全地帯が確保しにくい。表示方式の再検討が必要
