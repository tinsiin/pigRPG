# Logging Guidelines (Phase 6a)

## 目的
- Debug.Log の種類と用途を統一する
- UIログと分析ログを混在させない

## 基本方針
- UIに出す文言は `BattleEvent.MessageOnly` / `BattleEvent.LogOnly` を使う
- `Debug.Log` は「開発中の低頻度な状況説明」に限定する
- `Debug.LogWarning` は「復帰可能だが異常な状態」に限定する
- `Debug.LogError` は「継続不能/仕様逸脱の状態」に限定する

## 具体例
- `Debug.Log`: 戦闘開始/終了の一回通知、検証用の短い説明
- `Debug.LogWarning`: 依存未注入、想定外の入力種別
- `Debug.LogError`: 必須依存が null、致命的な前提破綻

## 書き方ルール
- 先頭にクラス名を入れる（例: `BattleSession.ApplyInputAsync:`）
- 可能な限り actor/skill/turn の情報を含める
- 連続で大量に出るログは出さない（UIログ/分析ログに逃がす）
- CoreRuntime では `IBattleLogger` 経由で出力する

## 入力ログの扱い
- `BattleEventType.BattleInputApplied` は再生/分析向け
- UIログに流さない
