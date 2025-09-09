# pigRPG 戦闘システム ボタンフロー完全ガイド

## 🎯 このドキュメントの目的
BattleManagerにおけるボタン操作のフローを、他のAIが読んでも完全に理解できるように詳細に記述する。

## 📊 ボタンフロー全体図

```mermaid
[戦闘開始]
    ↓
[Encount] → ACTPop() → USERUI_state設定
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

### 1️⃣ 初期登録 (Walking.cs:178-181)
```csharp
// エンカウント時の初期設定
USERUI_state.Value = bm.ACTPop();  // 最初のTabState決定
_nextWaitBtn.onClick.RemoveAllListeners();
_nextWaitBtn.onClick.AddListener(()=>OnClickNextWaitBtn().Forget());
```

### 2️⃣ ボタンクリック処理 (Walking.cs:113-146)
```csharp
private async UniTask OnClickNextWaitBtn()
{
    // 1. 再入防止チェック
    if (_isProcessingNext) {
        _pendingNextClick = true;  // 次回処理予約
        return;
    }
    
    // 2. 処理開始
    _isProcessingNext = true;
    
    // 3. 行動分岐処理実行
    var next = await bm.CharacterActBranching();
    USERUI_state.Value = next;  // 次の画面へ遷移
    
    // 4. ペンディング処理
    if (_pendingNextClick && USERUI_state.Value == TabState.NextWait) {
        OnClickNextWaitBtn().Forget();  // 自動進行
    }
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

### 1️⃣ スキルボタンのコールバック (PlayersStates.cs:960-976)
```csharp
public void OnSkillBtnCallBack(int skillListIndex)
{
    // 1. スキル使用
    SKillUseCall(SkillList[skillListIndex]);
    
    // 2. 次の画面決定
    if(Acts.GetAtSingleTarget(0) != null) {
        // 先約単体指定あり → 即実行
        USERUI_state.Value = TabState.NextWait;
    } else {
        // スキル性質で分岐
        USERUI_state.Value = DetermineNextUIState(NowUseSkill);
    }
}
```

### 2️⃣ UI状態の分岐決定 (PlayersStates.cs:1067-1092)
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

### 2️⃣ 範囲選択処理 (SelectRangeButtons.cs:475-484)
```csharp
public void OnClickRangeBtn(Button thisbtn, SkillZoneTrait range)
{
    // 1. 範囲意志を設定
    bm.Acter.RangeWill |= range;
    
    // 2. ボタン削除
    foreach (var button in buttonList)
        Destroy(button);
    
    // 3. 次へ
    NextTab();
}
```

### 3️⃣ 次画面決定 (SelectRangeButtons.cs:504-517)
```csharp
private void NextTab()
{
    if (bm.Acter.HasRangeWill(SkillZoneTrait.AllTarget)) {
        // 全範囲なら対象選択不要
        USERUI_state.Value = TabState.NextWait;
    } else {
        // 対象選択へ
        USERUI_state.Value = TabState.SelectTarget;
    }
}
```

## 🔵 ターゲット選択ボタン

### 1️⃣ ターゲットボタン生成時の登録 (SelectTargetButtons.cs:317,372)
```csharp
// 敵ボタン
button.onClick.AddListener(() => OnClickSelectTarget(chara, button, allyOrEnemy.Enemyiy, DirectedWill.One));

// 味方ボタン
button.onClick.AddListener(() => OnClickSelectTarget(chara, button, allyOrEnemy.alliy, DirectedWill.One));
```

### 2️⃣ ターゲット選択処理 (SelectTargetButtons.cs:438-506)
```csharp
void OnClickSelectTarget(BaseStates target, Button thisBtn, allyOrEnemy faction, DirectedWill will)
{
    // 1. キャッシュに追加
    CashUnders.Add(target);
    
    // 2. 陣営違いのボタン削除
    if (faction == allyOrEnemy.Enemyiy)
        // 味方ボタン全削除
    
    // 3. カウントダウン
    if (faction == allyOrEnemy.alliy)
        NeedSelectCountAlly--;
    
    // 4. 終了判定
    if (残りボタンなし || カウント0以下) {
        ReturnNextWaitView();
    }
    
    // 5. ボタン削除
    Destroy(thisBtn);
}
```

### 3️⃣ 戦闘続行 (SelectTargetButtons.cs:510-534)
```csharp
private void ReturnNextWaitView()
{
    // 1. NextWaitへ戻る
    Walking.Instance.USERUI_state.Value = TabState.NextWait;
    
    // 2. 選択結果を反映
    foreach(var cash in CashUnders)
        bm.unders.CharaAdd(cash);
    
    // 3. ボタン全削除
    // 4. UI非表示
}
```

## 🔄 ボタンループの完全な流れ

```
1. [戦闘開始]
   Walking.Encount()
   ├─ BattleManager生成
   ├─ ACTPop()実行 → TabState取得
   ├─ USERUI_state.Value = TabState設定
   └─ NextWaitBtn.onClick.AddListener(OnClickNextWaitBtn)

2. [NextWaitボタンクリック]
   OnClickNextWaitBtn()
   ├─ CharacterActBranching()実行
   ├─ 行動内容に応じた処理
   └─ 次のTabState返却 → USERUI_state更新

3. [TabState.Skill時]
   スキルボタン表示
   ├─ OnSkillBtnCallBack(skillID)
   ├─ DetermineNextUIState()で次画面決定
   └─ TabState.SelectRange or SelectTarget or NextWait

4. [TabState.SelectRange時]
   範囲選択ボタン表示
   ├─ OnClickRangeBtn()
   ├─ RangeWill設定
   └─ TabState.SelectTarget or NextWait

5. [TabState.SelectTarget時]
   ターゲット選択ボタン表示
   ├─ OnClickSelectTarget()
   ├─ ターゲットリスト構築
   └─ TabState.NextWait (必ず戻る)

6. [ループ]
   TabState.NextWait → 2へ戻る
```

## ⚠️ 重要な制御ポイント

### 再入防止機構
```csharp
_isProcessingNext    // 処理中フラグ
_pendingNextClick    // 保留クリック
```

### TabState遷移ルール
- **NextWait** → すべての画面へ遷移可能
- **Skill** → SelectRange/SelectTarget/NextWaitへ
- **SelectRange** → SelectTarget/NextWaitへ
- **SelectTarget** → NextWaitのみ（必ず戻る）

### ボタン削除タイミング
1. **スキルボタン**: 選択後も残る（画面遷移で自動消去）
2. **範囲ボタン**: 選択後即削除
3. **ターゲットボタン**: 選択後即削除

## 📝 まとめ

このシステムの核心は：
1. **NextWaitボタン**が戦闘進行のトリガー
2. **TabState**が画面遷移を制御
3. **各選択ボタン**がTabStateを更新
4. 最終的に必ず**NextWait**に戻る循環構造

この循環により、ボタンクリックだけで複雑な戦闘フローを実現している。