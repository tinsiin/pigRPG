# 敵AI 統合ロードマップ

本書は第一次・第二次多角的分析レポートの結論を統合し、実装の優先順位と依存関係を整理したものである。

- 第一次レポート: `doc/敵AI未実装要素_多角的分析レポート.md`（エージェントA-E）
- 第二次レポート: `doc/敵AI第二次多角的分析レポート.md`（エージェントF-J）
- 現行仕様: `doc/敵AI仕様書.md`

作成日: 2026-03-07

---

## 完了済み

| 内容 | 根拠 | 修正コミット |
|---|---|---|
| AddAction() Skills未設定バグ | J (#1), F (#2) | 061022d |
| SimulateBarrierLayers NowUseSkill参照バグ | J (#2) | 061022d |
| 総合戦闘仕様書 Vond式/DEFATK継承の記述修正 | レビューエージェント | 061022d |
| SO汚染検出ガード（Run()に`#if UNITY_EDITOR`検出 + PostBattleActRun汚染リスクをコメント記録） | F (1.3), H (3.2 #3) | — |
| AnalyzeBestDamage return漏れ（Count==1で早期return追加） | J (#3) | — |
| CommitDecision スキル未設定ガード（!HasSkillで安全脱出） | J (#4) | — |
| RangeWill Add vs 置換 → バグではなく設計意図通り（コメント追記） | J (#5) | — |

---

## Priority 0: 即時修正 → 完了

### 0-1. SO汚染検出ガード → 即時対策完了

BattleAIBrain（ScriptableObject）が`user`, `manager`, `availableSkills`を可変フィールドとして保持しており、同一SOを複数敵が共有するとasync中に状態汚染が起きる。

- **即時対策:** Run()に`#if UNITY_EDITOR`検出ガード追加 → **完了**。PostBattleActRunはselfがメソッドパラメータで安全、manager間接参照(Roll経由)の影響は軽微のためコメント記録に留める
- **中期対策:** user/managerをSOフィールドに保持せず各メソッドのパラメータとして渡す設計にリファクタ（未着手）
- 根拠: F (1.3), H (3.2 #3), J (5.1)

### 0-2. 既存Majorバグ3件 → 完了

| # | 箇所 | 問題 | 状態 |
|---|---|---|---|
| 3 | `BattleAIBrain.cs` AnalyzeBestDamage | `potentialTargets.Count==1`時にreturnせず後続分岐で結果上書き | **修正済み**（早期return追加） |
| 4 | `BattleAIBrain.cs` CommitDecision | スキル未設定のままRangeWill/TargetWillだけがコミットされうる | **修正済み**（!HasSkillガード追加） |
| 5 | `BattleAIBrain.cs` CommitDecision | RangeWillのAdd vs 置換 | **問題なし**（ターン開始時RangeWill=0リセット済みで実質同値。コメント追記） |

---

## Priority 1: LogThinkシステム → 完了

AIの判断過程を追跡するログ基盤。

- `_thinkLogLevel`: Inspector設定可能なログレベル（0=最終結果, 1=候補一覧, 2=スコア詳細, 3=全試算）
- `LogThink(level, message)`: レベルフィルタ付きログ出力（`[AI:キャラ名][Tターン]`プレフィックス）
- ログ挿入箇所: SkillActRun各分岐、Plan結果、MustSkillSelectフィルタ前後、SingleBestDamageAnalyzer各スキル試算+最終選択、MultiBestDamageAndTargetAnalyzer全組み合わせ試算+最終選択
- 根拠: H (3.2 #1)

---

## Phase 1: 基盤整備（~200-400行追加）

**目標:** BasicTacticalAIが書けるだけの部品を揃える

| 順序 | 作業 | 行数目安 | 根拠 |
|---|---|---|---|
| 1-1 | 自HP/精神HP情報取得（HpRatio等） | 20-40 | A, D |
| 1-2 | ターン数取得の整備 | 10-20 | A, D |
| 1-3 | 敵グループ列挙ヘルパー | 20-30 | D |
| 1-4 | ShouldEscape() virtual + Inspector逃走パラメータ | 30-50 | A |
| 1-5 | SimulateHitRate + HitSimulatePolicy | 80-150 | B |
| 1-6 | SkillAnalysisPolicy.useExpectedDamage統合 | 30-50 | B |
| 1-7 | FindSkill(name/id) ヘルパー | 10-15 | A |
| 1-8 | **BasicTacticalAI作成**（実証） | 60-100 | D, G |

### BasicTacticalAIで実現する体験変化

SimpleRandomTestAI（ランダム選択）→ BasicTacticalAI で:
- ターゲット選択の合理化（HP低い味方を狙う） → 「油断できなくなった」
- ダメージ最大スキルの選択 → 「適当に殴ってこなくなった」
- 逃走の導入 → 「敵も生き延びようとしている」

Inspector設定の組み合わせ（SkillAnalysisPolicy / 逃走確率 / variationStages）だけで16通り以上の個性が1つのSOクラスから生まれる。（根拠: G 2.7）

### 依存関係

```
1-1 HpRatio ──→ 1-4 ShouldEscape()
1-2 ターン数 ──→ 1-8 BasicTacticalAI（決め打ちパターン）
1-3 敵列挙 ───→ 1-8 BasicTacticalAI
1-5 HitRate ──→ 1-6 期待ダメージ統合
1-1～1-7 全て → 1-8 BasicTacticalAI
```

---

## Phase 2: 記憶・学習（~300-500行追加）

**目標:** 敵キャラごとの個性と深み

| 順序 | 作業 | 行数目安 | 根拠 |
|---|---|---|---|
| 2-1 | BattleMemory + DamageRecord/ActionRecord | 100-150 | C |
| 2-2 | 被害記録の書き込みフック（BM側、各箇所1行追加） | 20-30 | C |
| 2-3 | 記録参照ユーティリティ（基底クラス） | 40-60 | C |
| 2-4 | トラウマ率 + IsTraumaAvoided() | 60-100 | C |
| 2-5 | _lastTurnHP + HpDropRate | 15-25 | A |
| 2-6 | 行動記録参照 | 30-50 | C, D |
| 2-7 | 逃走思考（上級: トラウマ連携） | 40-60 | A |

### 設計上の重要決定

- **BattleMemoryはNormalEnemyに保持**（SOに持たせない）。SO共有問題を回避。根拠: C (3.1)
- **トラウマ回避は思慮レベル不問**（感情・本能軸）。被害記録の戦略利用は思慮レベル依存（知性軸）。根拠: C (3.6)
- **トラウマ率上限0.8を設ける** + フィルタ後スキル0件ならフォールバック。根拠: F (1.4)

### トラウマの体験設計

| トラウマ率 | 演出 | プレイヤーの認知 |
|---|---|---|
| 0～0.2 | 変化なし | 気づかない |
| 0.2～0.5 | 行動変化のみ | 注意深い人だけ気づく |
| 0.5～0.7 | 微演出（震え等） | 多くの人が「ビビってる！」 |
| 0.7～1.0 | 明示演出（怯えモーション、テキストログ） | 全員が気づく。達成感 |

根拠: G (2.4)

---

## Phase 3: 高度な要素（~400-800行追加）

**目標:** 高知能ボスキャラの実現

| 順序 | 作業 | 行数目安 | 根拠 |
|---|---|---|---|
| 3-1 | ShouldAbortFreeze() virtualフック | 5-10 | E |
| 3-2 | GetFreezeInfo / EstimateCounterRisk | 60-100 | E |
| 3-3 | ReadTargetPassives + 思慮レベル可視性 | 80-120 | E |
| 3-4 | 思慮推測レベル基盤（_deliberationLevel） | 150-300 | C, E |
| 3-5 | 精神レベル基盤（damageType切り替え、spiritualModifier活用） | 80-120 | D, 第一次6.3 |

### 3軸パーソナリティ

```
思慮推測レベル = 頭の良さ（パッシブ読み、行動予測、ポイント推測）
精神レベル    = 精神分析力（敵の精神HPの削れ具合、精神属性相性の見抜き）
トラウマ率    = 感情反応（カウンターされた恐怖、HP急減の記憶）
```

### 注意事項

- ShouldAbortFreeze()は**ResumeFreezeSkillの前に呼ぶこと**（後だと状態不整合）。根拠: F (#3)
- ReadTargetPassivesは**target.Passivesのコピーを返すこと**（直接参照で戦闘システム破壊）。根拠: F (1.2)
- 思慮レベル3でもパッシブ読み精度は100%にしない（「ズルい」と感じさせない）。根拠: G (2.2, 2.6)

---

## Phase 4: 構想段階

### AIAPI（情報取得ユーティリティ → JSON → LLM → AIDecision）

- Phase 1-2ではIAISerializableは不要
- Phase 3でstructにWriteTo()を追加開始（追加コスト約40行）
- PreThinkAsync()パターンでPlan()の同期制約を回避
- 3段フォールバック: AIAPI → 手書きロジック → ランダム
- 1戦闘(10ターン)あたり約$0.05（GPT-4o想定）
- 根拠: I (4.1-4.7)

### 戦闘後AI自動選定エンジン（仕様書11.7）

現行方式で十分なうちは不要。

---

## 横断的な設計原則

1. **情報取得ユーティリティはstructで返す** — 手書きAIはstruct直接参照、AIAPIはWriteTo()でJSON化。既存のStockInfo/TriggerInfoが正しいパターン。（根拠: I 4.1）
2. **「予想と違った！」感を維持する** — シミュレートは意図的に不完全。damageStepによる丸め、パッシブ無視、落ち着きカウント無視。（根拠: B 2.4, G 2.2）
3. **SO共有問題を常に意識** — 可変状態はSOに持たせない。パラメータ（しきい値等）はSO、状態（記録等）は個体。（根拠: C 3.1, F 1.3）
4. **テスト基盤を並行整備** — DamageStepAnalysisHelperが最有望のテスト対象。MockBattleContextの共通化が先決。（根拠: H 3.2 #2）
