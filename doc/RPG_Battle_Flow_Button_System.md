# pigRPG 戦闘システム ボタンフロー完全ガイド

## 🎯 このドキュメントの目的
BattleManagerにおけるボタン操作のフローを、他のAIが読んでも完全に理解できるように詳細に記述する。

## 📊 ボタンフロー全体図

```mermaid
[戦闘開始]
    ↓
[Encount] → StartBattle() → CurrentUiState設定
    ↓
[NextWaitボタン登録]
    ↓
┌─────────────────────────────┐
│      メインボタンループ        │
│                              │
│  NextWaitボタン              │
│      ↓                      │
│  CharacterActBranching       │
│      ↓                      │
│  TabState分岐                │
│   ├─ NextWait → (ループ継続)  │
│   ├─ Skill → スキル選択      │
│   ├─ SelectRange → 範囲選択  │
│   └─ SelectTarget → 対象選択 │
└─────────────────────────────┘
```

## 🔴 NextWaitボタン - 戦闘進行の心臓部

### 1️⃣ 初期登録 (Walking.cs:200付近)
```csharp
// エンカウント時の初期設定
orchestrator = result.Orchestrator;
USERUI_state.Value = initializer.SetupInitialBattleUI(orchestrator); // StartBattle内包
_nextWaitBtn.onClick.RemoveAllListeners();
_nextWaitBtn.onClick.AddListener(() => OnClickNextWaitBtn().Forget());
```

### 2️⃣ ボタンクリック処理 (Walking.cs:150付近)
```csharp
private async UniTask OnClickNextWaitBtn()
{
    WatchUIUpdate.Instance?.ForceExitKImmediate();
    if (orchestrator == null || orchestrator.Phase == BattlePhase.Completed)
    {
        return;
    }
    await orchestrator.RequestAdvance();
    USERUI_state.Value = orchestrator.CurrentUiState;
}
```

### 3️⃣ ACTPop → TabState決定フロー (BattleManager.cs:617-757)

```
ACTPop()が返すTabState
├─ 戦闘終了系 → TabState.NextWait
│   ├─ 全滅判定
│   ├─ 逃走判定
│   └─ 敵グループ空
│
├─ 味方行動 → TabState.Skill または NextWait
│   ├─ 強制続行中 → NextWait
│   ├─ 行動不能 → NextWait  
│   └─ 通常 → Skill (スキル選択へ)
│
└─ 敵行動 → TabState.NextWait
    └─ SkillAI()で自動決定
```

## 🟡 スキル選択ボタン

### 1️⃣ スキルボタンのコールバック (PlayersStates.cs:780付近)
```csharp
public void OnSkillBtnCallBack(int skillListIndex)
{
    var orchestrator = BattleOrchestratorHub.Current;
    var input = new ActionInput
    {
        Kind = ActionInputKind.SkillSelect,
        RequestId = orchestrator.CurrentChoiceRequest.RequestId,
        Actor = this,
        Skill = SkillList[skillListIndex]
    };
    var state = orchestrator.ApplyInput(input);
    BattleUIBridge.Active?.SetUserUiState(state, false);
}
```

### 2️⃣ UI状態の分岐決定（Orchestrator内で利用）
```csharp
public static TabState DetermineNextUIState(BaseSkill skill)
{
    // 範囲選択が必要？
    if (skill.HasZoneTrait(SkillZoneTrait.CanSelectRange))
        return TabState.SelectRange;
    
    // ターゲット選択が必要？
    if (skill.HasZoneTrait(SkillZoneTrait.CanSelectSingleTarget))
        return TabState.SelectTarget;
    
    // それ以外
    return TabState.NextWait;
}
```

## 🟢 範囲選択ボタン

### 1️⃣ 範囲ボタン生成時の登録 (SelectRangeButtons.cs)
```csharp
button.onClick.AddListener(() => OnClickRangeBtn(button, SkillZoneTrait.CanSelectSingleTarget));
```

### 2️⃣ 範囲選択処理 (SelectRangeButtons.cs:507付近)
```csharp
public void OnClickRangeBtn(Button thisbtn, SkillZoneTrait range)
{
    var input = new ActionInput
    {
        Kind = ActionInputKind.RangeSelect,
        RequestId = orchestrator.CurrentChoiceRequest.RequestId,
        Actor = battle?.Acter,
        RangeWill = range
    };
    var state = orchestrator.ApplyInput(input);
    BattleUIBridge.Active?.SetUserUiState(state, false);
}
```

### 3️⃣ 次画面決定（Orchestrator内で自動判定）
```csharp
// AllTarget なら NextWait、そうでなければ SelectTarget へ
```

## 🔵 ターゲット選択ボタン

### 1️⃣ ターゲットボタン生成時の登録 (SelectTargetButtons.cs:317,372)
```csharp
// 敵ボタン
button.onClick.AddListener(() => OnClickSelectTarget(chara, button, allyOrEnemy.Enemyiy, DirectedWill.One));

// 味方ボタン
button.onClick.AddListener(() => OnClickSelectTarget(chara, button, allyOrEnemy.alliy, DirectedWill.One));
```

### 2️⃣ ターゲット選択処理 (SelectTargetButtons.cs:446付近)
```csharp
void OnClickSelectTarget(BaseStates target, Button thisBtn, allyOrEnemy faction, DirectedWill will)
{
    // 1. キャッシュに追加
    CashUnders.Add(target);
    selectedTargetWill = will;
    
    // 2. 陣営違いのボタン削除・カウントダウン
    // 3. 終了判定で ReturnNextWaitView()
    Destroy(thisBtn);
}
```

### 3️⃣ 戦闘続行 (SelectTargetButtons.cs:505付近)
```csharp
private void ReturnNextWaitView()
{
    var input = new ActionInput
    {
        Kind = ActionInputKind.TargetSelect,
        RequestId = orchestrator.CurrentChoiceRequest.RequestId,
        Actor = battle?.Acter,
        TargetWill = selectedTargetWill,
        Targets = new List<BaseStates>(CashUnders)
    };
    var state = orchestrator.ApplyInput(input);
    BattleUIBridge.Active?.SetUserUiState(state, false);
}
```

## 🔄 ボタンループの完全な流れ

```
1. [戦闘開始]
   Walking.Encount()
   ├─ BattleOrchestrator生成
   ├─ StartBattle() → ChoiceRequest生成
   ├─ USERUI_state.Value = CurrentUiState設定
   └─ NextWaitBtn.onClick.AddListener(OnClickNextWaitBtn)

2. [NextWaitボタンクリック]
   OnClickNextWaitBtn()
   ├─ Orchestrator.RequestAdvance() 実行
   ├─ StepInternal() で分岐処理
   └─ CurrentUiState 反映

3. [TabState.Skill時]
   スキルボタン表示
   ├─ ActionInput(SkillSelect/Stock/DoNothing)
   └─ Orchestrator.ApplyInput → CurrentUiState

4. [TabState.SelectRange時]
   範囲選択ボタン表示
   ├─ ActionInput(RangeSelect)
   └─ Orchestrator.ApplyInput → CurrentUiState

5. [TabState.SelectTarget時]
   ターゲット選択ボタン表示
   ├─ ActionInput(TargetSelect)
   └─ Orchestrator.ApplyInput → CurrentUiState

6. [ループ]
   TabState.NextWait → 2へ戻る
```

## ⚠️ 重要な制御ポイント

### 再入防止機構
```csharp
BattleOrchestrator.RequestAdvance()
// _isAdvancing / _pendingAdvance で多重入力を吸収
```
### 入力ガード
- フェーズ/選択種別/RequestId/Actor 不一致は Orchestrator が拒否

### TabState遷移ルール
- **NextWait** → すべての画面へ遷移可能
- **Skill** → SelectRange/SelectTarget/NextWaitへ
- **SelectRange** → SelectTarget/NextWaitへ
- **SelectTarget** → NextWaitのみ（必ず戻る）

### ボタン削除タイミング
1. **スキルボタン**: 選択後も残る（画面遷移で自動消去）
2. **範囲ボタン**: 選択後即削除
3. **ターゲットボタン**: 選択後即削除

### ログ履歴（BattleEventHistory）
- `BattleUIBridge.AddLog` が履歴に蓄積
- `DisplayLogs` は履歴から再生成して表示
- `HardStopAndClearLogs` で履歴もクリア

## 📝 まとめ

このシステムの核心は：
1. **NextWaitボタン**が戦闘進行のトリガー
2. **TabState**が画面遷移を制御
3. **各選択ボタン**がTabStateを更新
4. 最終的に必ず**NextWait**に戻る循環構造

この循環により、ボタンクリックだけで複雑な戦闘フローを実現している。

---

## ✅ 改善計画（ボタン中心設計の強化）

### 目的
- ボタン操作の体験は維持したまま、ロジック主導に寄せる
- UI/ロジックの結合を減らし、拡張・自動化を容易にする

### 改善方針
1) **ChoiceRequest → ActionInput の流れに統一**
- ロジック側（Orchestrator）が「選択要求」を出す
- UIは「入力を返すだけ」に徹する

2) **入力経路の一元化**
- ボタン/ショートカット/自動行動を同じ ActionInput へ統合

3) **入力ガードの追加**
- 期待フェーズ外の入力や対象不正はロジック側で弾く

4) **フェーズ管理の明確化**
- `TabState` だけに頼らず、戦闘フェーズ（選択中/演出中/待機）を分離

5) **ログ/タイムラインの分離**
- 戦闘ログは `BattleEventHistory` に蓄積し、UIは参照のみ

### 作業ステップ（完了）
1. ✅ ChoiceRequest / ActionInput の共通型を定義
2. ✅ ボタン処理は ActionInput 生成だけを行う（UIは Orchestrator を直接動かさない）
3. ✅ Orchestrator に入力検証とフェーズ判定を集約
4. ✅ NextWait の自動進行条件を Orchestrator 側に移す
5. ✅ BattleEventHistory を追加し、ログ表示を履歴参照型にする

### 期待効果
- UI表示のズレや多重入力の事故が減る
- 自動戦闘/リプレイ/テストの導入が容易になる
- ボタン中心の操作感は維持したまま拡張性が上がる

### 注意点
- UXは現行と同じ操作感を維持する
- 既存 `TabState` は段階的に置き換え（急に削除しない）
