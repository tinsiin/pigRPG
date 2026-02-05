# Data-Driven Candidate Points

## 前提
- 既存仕様を維持したまま、将来のデータ駆動化に備えるための候補整理
- 今は「置き換え可能な境界」を明確にするフェーズ

## 優先候補（戦闘コア）
- ターゲティング方針  
  `TargetingPolicyRegistry` でルール差し替えが可能
- スキル効果パイプライン  
  `ISkillEffectPipeline` により効果の追加・順序変更が可能
- コンボ規則  
  `ISkillComboRule` による組み合わせ効果の差し替え
- バトル入力 → 状態遷移  
  `BattleSession`/`BattleFlow` の入力処理をイベント化・設定化可能

## 次点候補（仕様影響が大きい）
- ダメージ計算ロジック  
  `BaseStates.Damage` などを分離し、式を差し替え可能に
- 状態異常/持続時間ロジック  
  `BaseStates.StatesState` 周りの条件分岐をルール化
- 敵AIの選択基準  
  `BattleAIBrain` の評価関数を設定で切替

## 非推奨（現時点）
- UI演出のデータ化  
  視覚/演出は仕様変更が多いため、後回し
- 歩行/遭遇のルール  
  戦闘コアが固まってから連携側を整理
