# Combat Scenarios (検証基盤)

## 目的
- 仕様を変えずにリファクタリングできているかを確認する
- 歩行→戦闘→歩行の流れが壊れていないかを確認する

## 事前条件
- `BattleLifecycle.md` の流れが現行実装の前提
- UI/演出は現行通りであること

## シナリオ一覧
### 1. 通常戦闘（敵全滅）
前提
- 通常のエンカウントで開始
手順
- 戦闘開始→スキル選択→対象選択→行動→ターン進行
期待結果
- 敵全滅で終了メッセージ表示
- UIが歩行に戻る
- `BattleStarted/TurnAdvanced/BattleEnded` が連続で受け取れる
- `UIStateHub.EyeState` が `Walk` に戻る

### 2. 逃走成功
前提
- 逃走が可能なエンカウント
手順
- 逃走コマンドを選択→成功
期待結果
- 逃走メッセージ表示
- UIが歩行に戻る
- `BattleStarted/TurnAdvanced/BattleEnded` が連続で受け取れる
- `UIStateHub.EyeState` が `Walk` に戻る

### 3. 味方全滅
前提
- 味方が敗北する戦闘
手順
- 戦闘を継続し味方が全滅する
期待結果
- 敗北メッセージ表示
- UIが歩行に戻る
- `BattleStarted/TurnAdvanced/BattleEnded` が連続で受け取れる
- `UIStateHub.EyeState` が `Walk` に戻る

### 4. 連続攻撃/凍結
前提
- 連続攻撃/凍結が発動するスキルが存在
手順
- 対象スキルを使用し連続行動へ遷移
期待結果
- 連続攻撃や凍結状態が正しく反映される
- 次のターンへ正常に進む
- `BattleStarted/TurnAdvanced/BattleEnded` が連続で受け取れる

### 5. 連鎖逃走
前提
- Domino 逃走が発生する条件
手順
- 逃走関連の行動で連鎖逃走を発生させる
期待結果
- Domino 逃走処理が実行される
- UIが歩行に戻る
- `BattleStarted/TurnAdvanced/BattleEnded` が連続で受け取れる
- `UIStateHub.EyeState` が `Walk` に戻る

## 記録
- 実施結果を `EventPolicy.md` の方針に沿ってログ記録する
