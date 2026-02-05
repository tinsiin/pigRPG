# Event Policy (ログ/イベント方針)

## 目的
- 戦闘イベントの扱いを統一する
- UIログと分析ログの責務を分ける

## BattleEvent の扱い
- `BattleEvent` は戦闘コアの唯一の出力とする
- UIはイベントを購読して表示を行う
- 分析ログの保存先は `BattleEventHistory` を当面の基準とする

## ログの区分
- UIメッセージ: 画面に表示するメッセージ
- UIログ: SchizoLog 等に表示するログ
- 分析ログ: デバッグ/検証用のイベント列

## 基本方針
- `BattleEventType.Message` は UIメッセージとして表示する
- `BattleEventType.Log` は UIログとして表示する
- `BattleEventType.BattleStarted/TurnAdvanced/BattleEnded` は分析ログとして記録する
- `BattleEventType.Ui*` は UI操作のためのイベントでありログには表示しない
- `BattleEventType.BattleInputApplied` は再生用の入力ログとして記録する（UIには表示しない）
- 重要度は `Important` フラグで表現する

## イベントの出し分けルール
- UIに表示すべき文章は `BattleEvent.MessageOnly` を使う
- UIログは `BattleEvent.LogOnly` を使う
- 進行状況は `BattleStarted/TurnAdvanced/BattleEnded` で記録する

## 例外方針
- UIログに表示したい進行イベントがある場合は `MessageOnly` で明示する

## 既存仕様との整合
- 既存 UI のメッセージ表示は維持する
- 追加のイベント記録は UI/挙動を変更しない範囲で行う
- 現状では `MessageOnly` が完全に網羅されていない可能性があるため、移行時に追記する
