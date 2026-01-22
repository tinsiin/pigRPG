# FlowGraph 拡張案 v0.1（Gate/Track/Pool/Anchor ほか）

> 目的：中核仕様をベースに、イベント門（中央ブロッカー）、内部トラック（擬似“歩数”）、共有プール、巻き戻しアンカー、滞在ポリシーなどの拡張を整理。直線／分岐／寄り道／遭遇を崩さず、門だけを“ど真ん中・位置固定”で扱えるようにする。

---

## 0. 前提
- サイドオブジェクト／遭遇：**毎手ランダム**（左右提示／独立確率）。
- イベント門（Gate）：**中央に出現**し、**位置（内部歩数）で確定**。複数設置可。サイドとは別レイヤ。
- UIには歩数を出さないが、内部には**進捗カウンタ**を持つ。

---

## 1. TL;DR（拡張点一覧）
1) **TrackConfig**：ノード内に“進行トラック（length/step）”を追加。  
2) **GateMarker[]**：トラック上の絶対位置・確率で出現する**中央ゲート**を複数定義。  
3) **SpawnSource**：寄り道／遭遇の候補を、ローカル／共有プール／タグ拾いで**合流**。  
4) **WeightMod / Curve**：回数や状態に応じて**重み・確率を倍率補正**。  
5) **Variety/Cooldown/Exclusive**：反復抑止・クールダウン・排他キー。  
6) **Overlay Mods**：夜・警戒など状況で倍率／禁止。  
7) **Anchor/Rewind**：門ごとに**巻き戻し地点**と**復元対象（位置のみ／状態も）**を指定。  
8) **StayPolicy**：FixedSteps／SoftExit／ProgressGateで“長さ”と“出口”を制御。  
9) **Hard/Soft Block**：門発生ターンの左右・遭遇の扱いを切替。  
10) **デバッグ＆Lint**：候補ゼロ／重複合流／分岐不成立の検知、再現性の安定化。

---

## 2. データモデル拡張（ScriptableObject）

### 2.1 Node 拡張：TrackConfig（内部トラック）
- `length:int` … 内部トラックの全長（例：100）。
- `stepDelta:int` … 1手の基本進行量（既定=1）。SOで±可能。
- `progressKey:string` … 進捗カウンタ名（例：`trackProgress`）。

### 2.2 Node 拡張：GateMarker[]（中央ゲート）
- `gateId:string`
- `positionSpec`：
  - `absSteps = n` … 絶対歩数で固定。
  - `percent = 0..1` … 進捗割合。
  - `range = [a..b]` … **入場時**にシード付きで1度だけ位置を確定。
- `spawnPolicy`：`Always` / `RollOnEnter(p)` / `RollOnSegment(p)`
- `passConditions[]`：鍵・フラグ・確率・会話結果など合成可。
- `onPass`：`OpenAndAdvance` / `Jump(node)` / `SetFlag(..)` 等。
- `onFail`：`JumpToAnchor(..)` / `RewindToAnchor(.., PositionOnly|PositionAndState, keys[])` / `PlayHintSO(..)` / `PushOverlay(..)+Jump(..)` 等。
- `blockingMode`：`HardBlock`（門解決に専念） / `SoftBlock`（左右は続行、前進は停止）。
- `repeatable:bool`, `cooldown:int`, `priority:int`。

### 2.3 SpawnSource（候補ソースの合流）
- `type`：`LocalEntries` / `PoolRef` / `TagQuery`。
- `filters[]:Condition`（このソースに限る条件）。
- `weightScale:float`（倍率）。
- **合流規則**：ID重複は「重み合算」または「最大値採用」を選択。
- **scope**：oneShot/cooldown/exclusive の効き先＝ `Node | Region(tag) | Graph`。

### 2.4 WeightMod / CurveByCounter
- `Multiply(factor)`／`CurveByCounter(key, AnimationCurve)`／`When(condition){...}`。
- 例：`sideTripCount`に応じて×1.0→×1.8へ漸増、夜は×1.3。

### 2.5 Overlay Mods（状況レイヤの補正）
- `sideObjectMods[]`（カテゴリ/IDに倍率や禁止）
- `encounterMods[]`（遭遇率・個別重みの倍率）

### 2.6 Anchor / Rewind（巻き戻し）
- `anchorId`／`nodeId`／`snapshotKeys[]`（巻き戻すキー白リスト）
- 生成トリガ：ノード入場時自動／SO実行時／門試行前。
- `ttl`（任意）／`scope`（Node/Region/Graph）。

### 2.7 StayPolicy（滞在ポリシー）
- `FixedSteps(n)`：最低n手。
- `SoftExit(curve:t→p_exit)`：滞在手数に応じて退出確率を上げる。
- `ProgressGate(key, need)`：進捗カウンタで出口開放。

---

## 3. ランタイム・フロー（更新版）
1) **左右サイド**：SpawnSource合流→条件→重み→Variety→左右抽選→選択→効果。
2) **遭遇ロール**：`p = baseRate × overlays × mods`（メモリレス）。当たれば解決。
3) **前進**：`progress += stepDelta`（SOで前後可能）。
4) **門チェック**：`nextGate.position <= progress` なら中央ゲートUI。pass/failを解決。
5) **出口**：`progress >= length` か出口Gateが開けば、次ノードへ遷移。

---

## 4. 設定パターン集
- **旧“100歩エリア”の再現**：`length=100`, `FixedSteps(100)` + 途中Gate。SOで `DecCounter("stayRemaining",10)` などの近道。
- **揺らぎのある長さ**：`SoftExit`＋Overlay「ExitBoost」付与SO。
- **タスクで出口**：`ProgressGate("collect", 3)`、SOが `IncCounter("collect")`。
- **複数門同居**：`GateMarker`を 25/60/90歩に配置、Bは `range(55..70)+RollOnEnter(0.7)`。
- **Hard/Soft Block**：重要門はHard、演出門はSoft。
- **共有イベントの横断出現**：`SpawnSource=PoolRef(Tag="sewer")`＋場ごとの `weightScale`。

---

## 5. イベント門 詳細仕様
- **位置決定**：`abs/percent` は固定、`range` は入場時に**一度だけ**サンプリング（シード＝runId+nodeId）。
- **優先度**：同歩数で複数門が重なる場合は `priority` 昇順で処理。必要に応じて**複合ゲート**化。
- **UI**：門は**中央専用UI**（サイドUIと別枠）。HardBlock時はサイド/遭遇を抑止。
- **再挑戦**：`repeatable` と `cooldown` で調整（例：3手冷却後に再試行）。

---

## 6. 巻き戻し（Rewind）仕様
- **アンカー作成**：`AreaStart`自動／`BeforeGateX`手動。
- **モード**：`PositionOnly`（`progress`だけ戻す）／`PositionAndState(keys=...)`（一部状態も復元）。
- **救済策**：失敗回数に応じてヒントSO強制／NearestHubへ退避／ExitKey付与など。

---

## 7. 確率・バランス運用
- 遭遇は独立確率（ムラ対策に `cooldown` か**更新過程**を採用可）。
- 共有プール合流時の重みは「合算／最大」のポリシーを選ぶ。上限・正規化のオプションを用意。
- カテゴリ比率の保証が必要なら**簡易スケジューラ**（予算管理で重み補正）を導入。
- 候補ゼロ時は**フェイルオーバー**（固定SO／何も出さず前進）を既定で用意。

---

## 8. デバッグ／Lint／再現性
- **ログ**：候補一覧・除外理由・最終重み・抽選結果・遭遇率・`progress`・`nextGate`。
- **Lint**：
  - ノード到達不能／出口不成立の恐れ  
  - TagQueryの未定義タグ  
  - SpawnSource合流後の候補ゼロ  
  - 複数門の衝突（同一位置・優先度未設定）
- **再現性**：抽選前にIDで安定ソート、seedに `buildVersion` を混ぜる。

---

## 9. エディタ運用
- 最初は**配列UI**でOK：`GateMarker[]` と `SpawnSource[]` を素直に編集。
- 後でGraphView：ノード、エッジ、トラック、門位置（目盛り）をビジュアル化。
- サブグラフ（Prefab）化で大規模化に対応。命名規約・検索パネルを標準装備。

---

## 10. 旧仕様からの移行
- 旧「エリア=100歩」は：`length=100`／`FixedSteps(100)`／旧“出現歩数イベント”は `GateMarker.abs(n)` へ写し替え。
- 旧“歩数で候補解放”は：`CounterAtLeast("walk", n)`＋WeightModへ。

---

## 11. 既知の難所と対策（抜粋）
- **距離依存効果**：`Adjacency` + 近傍Effect で擬似対応。
- **確率のムラ**：cooldown／ポアソン更新／保証枠。
- **プール合流暴走**：重複正規化・最大値採用。
- **Gate詰み**：救済Edge／ヒント強制／RewindState。
- **巻戻しの整合**：アンカーの白リスト復元（位置のみ／状態も）。
- **性能**：条件ラムダのキャッシュ／インクリメンタル再計算／Addressables先読み＋LRU。

---

## 12. 推奨デフォルト
- VarietyBias：履歴4、同カテゴリ連続2まで。
- Encounter baseRate：0.20〜0.30（夜×1.3、警戒×1.5）。
- Gate onFail：`PushOverlay("looping") + Jump(NearestHub)`（演出と救済の両立）。
- Segment展開：`length<=8`/区間での自動分割を推奨（ログ可読性）。

---

## 13. 参考スニペット（抜粋）
```csharp
// 進捗と門チェック（擬似）
progress += stepDelta;
var gate = NextGate(progress);
if (gate != null) {
  if (Pass(gate.passConditions)) OnPass(gate);
  else OnFail(gate); // JumpToAnchor / RewindToAnchor / Hint / Overlay 等
}
```

```csharp
// SpawnSource合流（擬似）
var pool = Merge(LocalEntries, PoolRef, TagQuery)
  .Filter(conditions)
  .Dedup(Policy.CombineWeights)
  .Apply(WeightMods)
  .Apply(VarietyBias)
  .Apply(ScopeOneShotCooldown);
```

---

> この拡張案は、中核仕様の責務分離（場所＝ノード／進行＝エッジ／状況＝Overlay／ロジック＝DSL）を保ちながら、**門だけを中央・位置基準で制御**する仕組みです。既存のランダム寄り道・遭遇を壊さず、“旧100歩型の体験”と“複数門の同居”を両立します。

