# 敵AI 第二次多角的分析レポート

本書は敵AIシステムの未実装要素について、第一次レポート（`doc/終了済み/敵AI未実装要素_多角的分析レポート.md`）とは異なる5つの視点から分析した結果をまとめたものである。

分析日: 2026-03-07
前提: 第一次レポートのPhase 1-4ロードマップ、敵AI仕様書セクション11

> **実装状況（2026-03-07更新）:** 本レポートの指摘・提案に基づくPhase 1〜3は全て実装完了。詳細は `doc/終了済み/敵AIロードマップ.md` 参照。Phase 4（AIAPI）は将来構想として保留。

---

## 目次

1. [エージェントF: リスク・エッジケース分析](#1-エージェントf-リスクエッジケース分析)
2. [エージェントG: プレイヤー体験・ゲームデザイン分析](#2-エージェントg-プレイヤー体験ゲームデザイン分析)
3. [エージェントH: テスト・デバッグ・観測可能性分析](#3-エージェントh-テストデバッグ観測可能性分析)
4. [エージェントI: AIAPI共通基盤設計分析](#4-エージェントi-aiapi共通基盤設計分析)
5. [エージェントJ: 既存コード品質レビュー](#5-エージェントj-既存コード品質レビュー)
6. [横断的発見・統合分析](#6-横断的発見統合分析)

---

## 1. エージェントF: リスク・エッジケース分析

**視点:** 各Phase要素の実装時に何が壊れうるか、エッジケース、破壊的かどうか

### 1.1 実装時に最も注意すべきTop 5リスク

| 順位 | リスク | 等級 | 影響 |
|---|---|---|---|
| **1** | SO共有によるasync状態汚染 | **高** | PostBattleActRunのasync中に他敵のSkillActRunが割り込み、同一SOの`user`が差し替わる→回復が間違ったキャラに適用される致命的バグ |
| **2** | ~~PostBattleDecision.AddAction()のSkills未設定~~ ✅修正済み | ~~**高**~~ | `AddAction(target, skill)`で追加した行動のSkillsフィールドがnullだった→修正済み |
| **3** | ShouldAbortFreeze()のタイミング設計 | **高** | ResumeFreezeSkillの後に呼ぶとNowUseSkillが復元済み状態で中断→状態不整合。**必ず前に呼ぶこと** |
| **4** | HpRatioのゼロ除算 | **中** | MaxHP=0で`HP/MaxHP`がNaN→後続の全比較が壊れる（C# floatではクラッシュしないが静かに全判定が狂う） |
| **5** | BasicTacticalAIの部品組み合わせ順序 | **中** | 逃走→ストック→スキル選択の順序ミスで「HPが低いのにストックする」等の不整合 |

### 1.2 Phase別エッジケース要約

#### Phase 1

| 要素 | 主要エッジケース | 破壊的か |
|---|---|---|
| HpRatio | MaxHP=0でNaN、HP負値、精神HPの連鎖ゼロ除算 | 静かに壊れる |
| ターン数取得 | Freeze中のカウント問題、「全体ターン」vs「自分の行動回数」の定義曖昧 | 意図と違うだけ |
| 敵グループ列挙 | 逃走済みキャラがpotentialTargetsに残る→1ターン無駄 | 空振り |
| ShouldEscape() | 逃走後のターンスケジューラキュー残留、Plan内でreturn忘れ | 概ね安全 |
| SimulateHitRate | パッシブ回避無視で「当たる」と判断→実際は外れる | 設計意図通り |
| FindSkill | スキル名重複で意図と違うレベルが選ばれる、結果null→DoNothing | 安全 |
| BasicTacticalAI | AnalyzeBestDamageがnullを返す場合のnull.Skill→NullRef | **要ガード** |

#### Phase 2

| 要素 | 主要エッジケース | 破壊的か |
|---|---|---|
| BattleMemory | NormalEnemy以外（ボス等）でBattleMemory=null、SOのuser参照リーク | **致命的になりうる** |
| 記録書き込みフック | 間接ダメージ（パッシブ継続ダメージ等）の記録漏れ | 精度低下 |
| トラウマ率 | 0/1.0張り付き、全連続攻撃トラウマ回避でスキル0→要フォールバック | 体験劣化 |

#### Phase 3

| 要素 | 主要エッジケース | 破壊的か |
|---|---|---|
| ShouldAbortFreeze() | タイミング逆転で状態不整合、FreezeUseSkillがnullの場合 | **設計次第で致命的** |
| ReadTargetPassives | `target.Passives`への直接参照でList変更→戦闘システム破壊 | **コピー必須** |
| 思慮推測レベル | レベル境界曖昧、SO共有で全個体同一レベル | 設計問題 |

### 1.3 横断的リスク: SO共有問題の詳細

BattleAIBrainの以下フィールドがSOインスタンスに直接保持されている:

| フィールド | リスク |
|---|---|
| `protected BaseStates user` | PostBattleActRunのasync中に他敵で上書き→**致命的** |
| `protected IBattleContext manager` | 同一戦闘内なら同一contextだが将来並行戦闘で破綻 |
| `protected List<BaseSkill> availableSkills` | Plan()外で参照すると前の敵のリストが残る |

**現状なぜ問題が表面化していないか:** ターン制で1体ずつ順次処理しているため同時呼び出しが起きていない。

**対策案:**
1. **即時:** PostBattleActRunでuser/managerをローカル変数にコピーしawait以降はローカルを使う
2. **根本:** user/managerをSOフィールドに保持せず各メソッドのパラメータとして渡す設計にリファクタ

### 1.4 トラウマ率のバランスリスク

| 問題 | 条件 | 影響 |
|---|---|---|
| 0張り付き | 減衰(0.05/戦闘)が蓄積を常に上回る | 設計通りだが「いつまでもビビらない」 |
| 1.0張り付き | 高頻度カウンター+減衰なし | ほぼ全連続攻撃が使用不可→通常攻撃のみ |
| フィルタ後スキル0 | トラウマが高く全スキルが連続攻撃 | 使えるスキルが0→要フォールバック |

**対策案:** トラウマ率上限(0.8)を設ける。トラウマフィルタ後にスキル0なら無視してフォールバック。

---

## 2. エージェントG: プレイヤー体験・ゲームデザイン分析

**視点:** プレイヤーから見た体験品質、「賢く見える」設計、演出との連携

### 2.1 「賢く見える」最小コスト要素（効果順）

| 順位 | 要素 | コスト | 体験変化 |
|---|---|---|---|
| **1** | ターゲット選択の合理化（HP低い味方を狙う） | 極小 | 「油断できなくなった」。緊張感が根本的に変わる |
| **2** | ダメージ最大スキルの選択 | 小 | 「適当に殴ってこなくなった」。防御・回復の判断を迫る |
| **3** | 逃走の導入 | 小 | 「敵も生き延びようとしている」。世界の生命感 |
| **4** | トラウマによる行動変化 | 中 | 「前回の戦いを覚えている」。世界の連続性 |
| **5** | パッシブ読み・Freeze中断 | 高 | 「読まれている！」。ボス戦の恐怖 |

**最重要指摘:** 1位と2位はBasicTacticalAIのPlan()を書くだけで実現する。SimpleRandomTestAI→BasicTacticalAIの切り替え1つで体験が「テスト戦闘」→「本物の戦闘」に変わる。

### 2.2 「ズルい」と感じる行動の分析

| 行動 | ズルさ | 理由 |
|---|---|---|
| 防御を選んだターンに限って精神攻撃 | **極高** | 入力を覗いた行動に見える（同時入力なので構造的に起きにくいが偶然の一致で疑われる） |
| パッシブの正確な残りターンを知っている | **高** | プレイヤー自身が正確に覚えていないのに敵が知っている |
| カウンターされそうなスキルを完璧に避ける | **高** | 「読み」のレベルであるべきで100%回避は不自然 |
| HP低いキャラを狙う | **低** | 自然な判断 |

**設計上の示唆:** 仕様書の「簡易シミュレート」設計（「予想と違った！」感）は正解。`damageStep`による丸めは計算精度を意図的に落とす優れた仕組み。

### 2.3 3軸パーソナリティの体験的分析

**プレイヤーの体感に最も影響する軸: トラウマ率**

理由: 行動パターンの**変化**が最もドラマチックに見えるため。「さっきまでガンガン攻めてたのにビビり始めた」はプレイヤーが物語として読める。思慮推測レベルの差（良い手/悪い手）はプレイヤーの分析力がなければ区別できない。

### 2.4 トラウマ率の体験設計

#### 段階的気づかせ設計

| トラウマ率 | 演出レベル | プレイヤーの認知 |
|---|---|---|
| 0〜0.2 | 変化なし | 気づかない |
| 0.2〜0.5 | 行動変化のみ | 注意深い人だけ「あの技使わなくなったな」 |
| 0.5〜0.7 | **微演出**（ターン開始時に震え等） | 多くの人が「こいつビビってる！」 |
| 0.7〜1.0 | **明示演出**（怯えモーション、テキストログ） | 全員が気づく。達成感 |

#### テキストログ演出案

| トラウマレベル | ログ |
|---|---|
| 中 | 「...は警戒している」 |
| 高 | 「...は怯えている！」 |
| 極高 | 「...は動揺している！」「...は○○を思い出した！」 |
| 再戦時 | 「...は以前のことを覚えているようだ」→逃走なら「...は逃げ出した！」 |

**設計原則:** ビビり演出は**プレイヤーに快感を与えるためのもの**。「自分の行動が敵に影響を与えた」実感がRPGで最も価値ある体験の一つ。

#### 精神属性による蓄積速度差別化の提案

EscapeHandlerの`GetRunOutRateByCharacterImpression`で精神属性ごとに逃走率を変えている設計が既にある。トラウマ蓄積率にも同じ思想を適用する余地がある:
- **Psycho**: トラウマが溜まりにくい（精神が頑強）
- **Kindergarten**: トラウマが溜まりやすい（繊細）

### 2.5 逃走の体験設計

**逃走が「面白い」条件:**

1. **タイミングがギリギリ:** HP閾値 **0.15〜0.25が最適帯**。HP90%で逃げる→「臆病すぎ」、HP20%以下→「あと一撃だったのに！」
2. **頻度が予想外:** 逃走確率 **10〜20%程度**（Base）。トラウマ蓄積で上昇
3. **失敗がある:** 現行EscapeHandlerの50%失敗設計（`RollPercent(50)`）は優れた仕組み。「逃げようとして失敗する」→「チャンスだ！」

### 2.6 思慮推測レベルの体感差とフラストレーション管理

| レベル | プレイヤー体感 |
|---|---|
| 0（本能） | 「倒しやすい」。雑魚戦の爽快感 |
| 1（基本） | 「油断できない」。大半の通常敵 |
| 2（中程度） | 「手強い」。中ボス級。戦術的対応を求める |
| 3（高度） | 「読まれてる！？」。ボス戦の恐怖。戦術変更を強制 |

**フラストレーション回避策:**
- レベル3でもパッシブ読み精度は100%にしない
- 「読みが外れた」演出を入れる: 「...は○○の効果を見誤った！」
- 対抗手段（ブラフスキル等）の存在が前提

### 2.7 Phase 1時点での個性出し

BasicTacticalAI 1つだけ作り、Inspector設定の組み合わせで個性を出す:

1. **SkillAnalysisPolicy:** 物理重視vs精神重視、単体狙いvs範囲好き → 4パターン
2. **逃走確率:** 0（勇敢）vs 0.15（慎重） → 2パターン
3. **variationStages:** 0（冷静）vs 3（気まぐれ） → 2パターン

計16通りの組み合わせが1つのSOクラスから生まれる。

### 2.8 pigRPG固有の設計優位性

1. **精神属性ごとの連鎖逃走確率** — トラウマシステムとの相性極めて良い
2. **友情コンビ登録+個体GUID** — 再戦時の記憶保持が技術的に可能（他RPGでは稀）
3. **割り込みカウンターの存在** — AIの「恐怖」の対象が具体的で感情移入しやすい
4. **刻み・ブレ段階システム** — AIの「人間らしい不完全さ」が構造的に保証済み

---

## 3. エージェントH: テスト・デバッグ・観測可能性分析

**視点:** AIの判断をどうテストし、デバッグし、観測するか

### 3.1 既存デバッグ基盤の調査結果

| カテゴリ | 現状 |
|---|---|
| Debug.LogError | 24箇所（致命的異常の報告のみ） |
| Debug.Log | 4箇所（情報レベル） |
| Debug.LogWarning | 3箇所 |
| Plan()の判断過程ログ | **完全に不在** |
| SimulateDamage結果ログ | **ゼロ** |
| テスト用フック | **なし** |
| 乱数シード制御 | SystemBattleRandomにseed対応済みだがAIレイヤーから固定起動手段なし |
| BattleAIBrain系テスト | **ゼロ** |

### 3.2 デバッグ基盤として最初に整備すべきTop 3

#### 1位: AI思考ログ（LogThinkシステム）

**理由:** AIが「なぜその行動を選んだか」を追跡する手段が完全にゼロ。

**設計案:**

```csharp
[Header("デバッグ")]
[SerializeField] private int _logLevel = 0; // 0=Result, 1=Candidates, 2=Scored, 3=Full

protected void LogThink(int level, string message)
{
    if (level > _logLevel) return;
    Debug.Log($"[AI:{user?.CharacterName}][T{manager?.BattleTurnCount}] {message}");
}
```

ログレベル:

| レベル | 出力内容 | 用途 |
|---|---|---|
| 0 Result | 最終選択のみ | リリース |
| 1 Candidates | 候補一覧+選択結果 | プレイテスト |
| 2 Scored | スコア/ダメージ値+選択理由 | AI調整 |
| 3 Full | 全スキル×全ターゲット試算+却下理由+乱数結果 | デバッグ |

最初にログを入れるべき箇所:
- `SkillActRun`の各分岐（Freeze/Cancel/Plan/DoNothing）
- `SingleBestDamageAnalyzer`のforeachループ内
- `MultiBestDamageAndTargetAnalyzer`の最終選択結果
- `MustSkillSelect`のフィルタ前後スキル数

実装量: 約50-80行追加。

#### 2位: テスト共通基盤 + DamageStepAnalysisHelperテスト

**理由:** `DamageStepAnalysisHelper`は純粋関数に近く最もテストしやすい。テスト共通基盤（`TestBattleContext`, `TestCharacterFactory`）は後続全AIテストの基礎になる。

既存の`MockBattleContext`が3箇所で重複定義されている状態→共通化が必要。

テスト設計の要点:
- `Plan()`は`protected virtual`→テスト用薄い派生クラス(`TestableAIBrain`)で突破
- SOは`ScriptableObject.CreateInstance<T>()`で生成
- `SystemBattleRandom`のseed固定でリプレイ可能なテスト
- 統計的検証: N回実行で期待分布と比較（ブレ段階の確率検証等）

実装量: 約200-300行。

#### 3位: SO状態汚染の検出ガード

```csharp
#if UNITY_EDITOR
private BaseStates _lastActer;
// SkillActRunの冒頭で:
if (_lastActer != null && _lastActer != user)
    Debug.LogWarning($"[AI汚染検出] 前回={_lastActer.CharacterName} 今回={user.CharacterName} 同一SO共有");
_lastActer = user;
#endif
```

### 3.3 「静かな失敗」パターン一覧

| 箇所 | 症状 | 改善案 |
|---|---|---|
| MustSkillSelectが全スキル排除 | availableSkills空→DoNothing | フィルタ前後のスキル数を常にログ |
| SimulateDamageが全スキルで0 | maxDamage=-3fより大きい0が勝つ→最初のスキル | 全スキル0以下の場合に警告ログ |
| AnalyzeBestDamageがnull | スキル1つの場合。SelectSkillが`candidates[0]`で拾うがPlan側でnull.Skill→NullRef | Plan側のnullチェック必須パターン化 |

### 3.4 パフォーマンス見積もり

| 処理 | 最悪ケース | 問題 |
|---|---|---|
| AnalyzeBestDamage (Group) | S=8, T=4, L=5で160回SimulateDamage | 問題なし |
| ReadTargetPassives（将来） | 全敵×全パッシブ走査で数百回 | 問題なし |
| AIAPI（将来） | ネットワーク1-5秒 | 演出で隠す |

現行規模（敵最大4-6体、スキル最大8）では全くボトルネックにならない。

---

## 4. エージェントI: AIAPI共通基盤設計分析

**視点:** 情報取得ユーティリティが手書きAI/AIAPIの共通基盤になる具体設計

### 4.1 核心設計: struct-first, JSON-second

structを正とし、JSONはstructからの射影として生成する。二重管理しない唯一の方法。

```csharp
public interface IAISerializable
{
    void WriteTo(AIJsonWriter writer);
}
```

- 手書きAI: structをそのまま使う（ゼロコスト）
- AIAPI: `WriteTo()`でJSON断片を生成

**WriteTo()はstructの全フィールドを出すのではなく、LLMが理解すべき情報のみを射影する。** これがトークン数削減の第一歩。

### 4.2 BattleStateSnapshot設計

```
BattleStateSnapshot
├── TurnCount: int
├── Self: CharacterSnapshot（常に全公開）
├── Allies: List<CharacterSnapshot>（簡易情報）
├── Enemies: List<CharacterSnapshot>（思慮レベルでフィルタ）
├── AvailableSkills: List<SkillSnapshot>（シミュレート結果付き）
└── Memory: MemorySnapshot（思慮レベル2以上のみ）
```

`ToJson(deliberationLevel, mentalLevel)`で思慮レベルに応じたフィルタリングを適用。LLMに渡す情報を物理的に削ることで「思慮レベルが低いからこの情報を知らない」を強制。

生成JSON例（思慮レベル2、精神レベル1）:

```json
{
  "turn": 7,
  "self": {"name": "ヴォルトガイスト", "hpRatio": 0.65, "mentalHpRatio": 0.80},
  "enemies": [
    {"name": "エハト", "hpRatio": 0.85, "isVanguard": true, "mentalHpRatio": 0.90, "passives": ["鉄壁"]},
    {"name": "リコ", "hpRatio": 0.70, "isVanguard": false, "mentalHpRatio": 0.60}
  ],
  "skills": [
    {"name": "雷撃斬", "type": "attack", "estimatedDamage": 45.20, "hitRate": 0.78},
    {"name": "暗黒砲", "type": "attack", "estimatedDamage": 62.50, "hitRate": 0.55,
     "stock": {"current": 2, "max": 5, "isFull": false, "fillRate": 0.40}}
  ],
  "memory": {"traumaRate": 0.25, "recentCounterRate": 0.33}
}
```

トークン数見積もり: 250-400トークン（思慮レベル2）、600-800トークン（レベル3フル情報）。

### 4.3 LLM出力→AIDecisionマッピング

LLMが返すJSON:
```json
{
  "action": "skill",
  "skillName": "暗黒砲",
  "rangeWill": "single",
  "targetWill": "one",
  "reasoning": "HPが低い敵リコに高火力の暗黒砲を撃つ"
}
```

スキル名→BaseSkill解決は**曖昧一致**（完全→前方→部分の順）。不正出力は全てフォールバック（AIAPI→手書きロジック→ランダムの3段）。

### 4.4 Plan()同期制約への対処

Plan()は同期メソッドだがAIAPI呼び出しは非同期。

**推奨案: PreThinkAsync()パターン**

```csharp
// BattleAIBrain基底に追加
public virtual bool RequiresAsyncThinking => false;
public virtual UniTask PreThinkAsync(CancellationToken ct) => UniTask.CompletedTask;

// NormalEnemy.SkillAI()を拡張
public async UniTask SkillAIAsync()
{
    if (_brain.RequiresAsyncThinking)
        await _brain.PreThinkAsync(destroyCancellationToken);
    _brain.SkillActRun(manager);
}
```

AIAPIBrainの`PreThinkAsync()`でAPI呼び出し→結果を内部に格納→`Plan()`が同期的にそれを読む。演出（黒い渦アニメーション）はPreThinkAsync内で並列実行。

### 4.5 二重設計コスト分析

| 項目 | 値 |
|---|---|
| WriteTo()追加の合計コード量 | **約40行**（全struct合計） |
| 新ユーティリティ追加時の追加作業 | 5-10分/ユーティリティ |
| コンパイル時整合性保証 | あり（フィールド名変更でWriteToもエラーになる） |

却下した代替案:
- **自動シリアライズ（リフレクション）:** GCアロケーション、不要フィールド出力、フィルタ不可
- **[AIExport]アトリビュート:** 将来的にはあり得るがstruct数10個程度では過剰

### 4.6 段階的移行パス

```
Phase 1-2: 手書きAIのみ。IAISerializableはまだ不要
Phase 3:   IAISerializableをstructに追加開始、BattleStateSnapshot生成器
Phase 4:   AIAPIBrain派生、AIAPIResponseMapper、思考中演出、API通信層
```

### 4.7 コスト見積もり

| 思慮レベル | 入力 | 出力 | 合計 | 1ターンコスト(GPT-4o) |
|---|---|---|---|---|
| 1 | ~150 | ~80 | ~530 | $0.003 |
| 2 | ~350 | ~80 | ~830 | $0.005 |
| 3 | ~600 | ~100 | ~1200 | $0.008 |

1戦闘(10ターン)あたり約$0.05。GPT-4o-miniなら1/10以下。

### 4.8 共通基盤アーキテクチャ図

```
情報取得ユーティリティ層（BattleAIBrain基底 + BaseStates）
┌─────────────────────────────────────────────┐
│ GetStockInfo() → StockInfo                  │
│ GetTriggerInfo() → TriggerInfo              │
│ HpRatio → float                             │
│ SimulateHitRate() → float                   │
│ GetMemory() → BattleMemory                  │
│ ReadTargetPassives() → List<PassiveSnapshot> │
│                                             │
│ 全structはIAISerializableを実装              │
└─────────────┬──────────────┬────────────────┘
              │              │
   ┌──────────▼──┐    ┌──────▼──────────────┐
   │ 手書きAI    │    │ AIAPI              │
   │ struct直接  │    │ WriteTo()→JSON     │
   │ 参照        │    │ →LLM→AIDecision   │
   └──────┬──────┘    └──────┬──────────────┘
          │                  │
          └────────┬─────────┘
                   │
          ┌────────▼────────┐
          │   AIDecision    │
          │ (共通出力層)    │
          └────────┬────────┘
                   │
          ┌────────▼────────┐
          │ CommitDecision  │
          │ (共通コミット)  │
          └─────────────────┘
```

---

## 5. エージェントJ: 既存コード品質レビュー

**視点:** BattleAIBrain.cs, BattleBrainSimlate.cs, SimpleRandomTestAI.csの実コードレビュー

### 5.1 修正優先度Top 5

| 順位 | ファイル:行 | 深刻度 | 問題 |
|---|---|---|---|
| **1** | `BattleAIBrain.cs:705` | ~~Critical~~ ✅修正済み | `AddAction()`が`Skills`を設定していなかった→修正済み |
| **2** | `BattleBrainSimlate.cs:125` | ~~Critical~~ ✅修正済み | `SimulateBarrierLayers`が`atker.NowUseSkill`を参照していた→引数`skill`を使うよう修正済み |
| **3** | `BattleAIBrain.cs:508-512` | **Major** | `potentialTargets.Count==1`でreturnせず後続のgroupType分岐に突入→結果上書き |
| **4** | `BattleAIBrain.cs:136-139` | **Major** | スキル未設定のままRangeWill/TargetWillだけがコミットされうる |
| **5** | `BattleAIBrain.cs:138` | **Major** | RangeWillの設定がプレイヤー側と不一致（AI=置換、プレイヤー=加算(OR)） |

### 5.2 Critical詳細

#### AddAction()のSkills未設定（#1）✅修正済み

`AddAction()`が引数`skill`を受け取りながら`PostBattleAction.Skills`に設定していなかった。`ResolveActionSkills`が常に空リストを返し、全アクションが無言スキップされていた。

**修正内容:** `Skills = new List<BaseSkill> { skill }` を追加。

#### SimulateBarrierLayersのNowUseSkill参照（#2）✅修正済み

`SimulateBarrierLayers`が引数の`skill`ではなく`atker.NowUseSkill.SkillPhysical`を参照していた。ブルートフォース分析時にNowUseSkillはループ中のskillと異なり、物理属性の耐性計算が間違う+NullRef可能性があった。

**修正内容:** `SimulateBarrierLayers`に`BaseSkill skill`パラメータを追加し、`skill.SkillPhysical`を参照するよう変更。

### 5.3 Major詳細

#### AnalyzeBestDamageのreturn漏れ（#3）

`potentialTargets.Count == 1`で`ResultTarget`と`ResultSkill`を設定するが`return`しない→後続のgroupType分岐で上書き。ターゲット1人でも全組み合わせ再計算する無駄。

#### CommitDecisionのスキル未設定パス（#4）

`HasSkill == false`でも`HasRangeWill`/`HasTargetWill`が`true`ならそれらだけがコミットされる。`NowUseSkill`は前ターンの値のまま、またはnull。

#### RangeWillのセマンティクス不一致（#5）

プレイヤー側: `actor.RangeWill = actor.RangeWill.Add(normalizedTrait);` （加算/OR）
AI側: `user.RangeWill = SkillZoneTraitNormalizer.Normalize(decision.RangeWill.Value);` （置換）

フラグ型のSkillZoneTraitを加算するか置換するかで挙動が変わる可能性。

### 5.4 Minor/Info

| 場所 | カテゴリ | 問題 |
|---|---|---|
| `BattleBrainSimlate.cs:26-27` | Dead code | 未使用変数 `simulateHP`, `simulateMentalHP` |
| `BattleBrainSimlate.cs:236-237` | Dead code | 到達不能コード（全パスがif/elseでカバー済み） |
| `BattleAIBrain.cs:560,604` | Style | `potential = -3f`のマジックナンバー→`float.NegativeInfinity`が明確 |
| `BattleAIBrain.cs:307-310` | Design | `Plan()`のデフォルトが空実装→`abstract`にするか警告ログ |
| 各所 | Style | 命名不一致: `SKillUseCall`(大文字K), `Simlate`(typo), `BattleBrainSimlate.cs`(ファイル名typo) |

### 5.5 CommitDecision全分岐パスレビュー

| パス | 条件 | 問題 |
|---|---|---|
| 1 | decision==null → DoNothing+LogError | OK |
| 2 | IsEscape → SelectedEscape | OK |
| 3 | IsStock+HasSkill+IsFullStock → DoNothing | OK |
| 4 | IsStock+HasSkill+!IsFullStock → 直接代入+SkillStock | OK |
| 5 | reserved!=null+!HasSkill → DoNothing+LogError | OK |
| 6 | reserved!=null+HasSkill → SKillUseCallのみ | OK |
| **7** | HasSkill+!HasRangeWill+!HasTargetWill | **RangeWillが設定されない**（プレイヤー側は自動Add） |
| **8** | !HasSkill+HasRangeWill | **Bug: RangeWillだけ設定、スキルなし** |
| **9** | !HasSkill+HasTargetWill | **Bug: Targetだけ設定、スキルなし** |

---

## 6. 横断的発見・統合分析

### 6.1 全チーム共通認識: 即時修正すべきバグ

5チーム中3チーム（F, H, J）が独立して同一のバグを最優先として指摘:

1. **AddAction()のSkills未設定** — 1行修正で直る致命的バグ
2. **SimulateBarrierLayersのNowUseSkill参照** — エージェントJが新発見。バリア層持ちの敵へのダメージシミュレーションが根本的に間違っている

**これらはPhase 1着手前に修正すべき。**

### 6.2 SO共有問題の深刻度再評価

エージェントF（リスク）とH（テスト）が独立して同じ構造的問題を最重要リスクとして指摘。

- **F:** PostBattleActRunのasync中にuserが差し替わる→致命的
- **H:** 検出ガードの具体コード提案
- **J:** SO上の可変フィールド5つを特定

3チームの知見を統合した対策:
1. **即時:** PostBattleActRunでローカル変数化 + `#if UNITY_EDITOR`検出ガード
2. **中期:** user/managerをメソッドパラメータ化（大きなリファクタリング）

### 6.3 「デバッグ不能なAI」の問題

エージェントHの分析で判明: **現在のAIは判断過程の記録手段が完全にゼロ。** これはPhase 1の部品実装時に最も困る問題。

- LogThinkシステム（50-80行）の導入がPhase 1部品と同時に必要
- テスト基盤も同時整備すべき（DamageStepAnalysisHelperが最有望のテスト対象）

### 6.4 体験設計がロードマップを裏付ける

エージェントGの分析で判明: **Phase 1の「ターゲット選択合理化 + ダメージ最大スキル選択」だけでプレイヤー体験が劇的に変わる。** Phase 2以降の高度な仕組みは、この基礎的な「賢さ」の上に乗せて初めて意味を持つ。

仕様書の段階的ロードマップは体験設計の観点からも正しい順序であることが確認された。

### 6.5 AIAPI共通基盤は「今作る必要はないが、設計は意識すべき」

エージェントIの分析結果:
- Phase 1-2ではIAISerializableは不要
- Phase 3でstructにWriteTo()を追加開始（追加コスト約40行）
- 二重設計リスクは実質ゼロに近い

**ただし、Phase 1で情報取得ユーティリティをstructで返す設計にしておくことが前提条件。** float直返しではなくstruct経由にすることで、将来のJSON化パスが自然に開く。これは既存のStockInfo/TriggerInfoが正しいパターンを示している。

### 6.6 優先順位の統合結論

| 順序 | 作業 | 根拠（エージェント） | 状態 |
|---|---|---|---|
| **0** | AddAction()バグ修正 + SimulateBarrierLayers修正 | F, J（即時修正必須） | ✅完了 |
| **0** | SO汚染検出ガード追加 | F, H（即時修正必須） | ✅完了 |
| **1** | LogThinkシステム導入 | H（Phase 1部品と同時に必要） | ✅完了 |
| **2** | Phase 1部品実装（第一次レポートのロードマップ通り） | 全チーム合意 | ✅完了 |
| **3** | BasicTacticalAI作成 | G（体験インパクト最大の変化点） | ✅完了 |
| **4** | テスト基盤整備 | H（DamageStepHelperから開始） | 未着手 |
| **5** | Phase 2以降 | 第一次レポートの順序に従う | ✅Phase 3まで完了 |

---

## 付録: 関連ファイルパス

| ファイル | 内容 |
|---|---|
| `Assets/Script/BattleAIBrains/BattleAIBrain.cs` | AI基底クラス（1218行+） |
| `Assets/Script/BattleAIBrains/SimpleRandomTestAI.cs` | テスト用派生AI |
| `Assets/Script/BaseStates/Battle/BaseStates.BattleBrainSimlate.cs` | ダメージシミュレート |
| `Assets/Script/Enemy/NormalEnemy.cs` | 敵クラス（AI呼び出し元） |
| `Assets/Script/Battle/Core/IBattleContext.cs` | IBattleContextインターフェース |
| `Assets/Script/Battle/CoreRuntime/Services/SystemBattleRandom.cs` | seed対応済み乱数 |
| `Assets/Script/Battle/CoreRuntime/EscapeHandler.cs` | 逃走・連鎖逃走処理 |
| `Assets/Editor/Tests/BattleContextHubTests.cs` | MockBattleContext既存実装例 |
| `doc/敵AI仕様書.md` | 現行AI仕様書 |
| `doc/終了済み/敵AI未実装要素_多角的分析レポート.md` | 第一次分析レポート |
