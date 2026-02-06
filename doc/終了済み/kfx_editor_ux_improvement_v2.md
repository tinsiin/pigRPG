# KFX Editor UX改善計画 v2

## 現状の問題分析

### 問題1: 英語UIで日本語ユーザーに不親切

ボタン・ラベル・メッセージが全て英語。「Ease」「Blend」「Opacity」「Add Property」等、エフェクト制作の文脈を知らない人には意味が伝わらない。

### 問題2: 編集対象とプレビューが離れている

現在のレイアウト（上から下に一直線）:

```
File          ← ①ファイル操作
Metadata      ← ②基本設定
Preview       ← ③プレビュー画面     ← ここを見ながら
Playback      ← ④再生ボタン
Timeline      ← ⑤タイムライン
Layers        ← ⑥レイヤー一覧
Layer Detail  ← ⑦レイヤー設定
Keyframes     ← ⑧キーフレーム一覧
KF Detail     ← ⑨キーフレーム編集   ← ここを操作する
```

③と⑨が画面の上端と下端に分かれている。パラメータを変えるたびにスクロールしてプレビューを確認しなければならない。

### 問題3: 関連プロパティがバラバラに並んでいる

X と Y は別々の行。rx と ry も別々の行。本来ペアで操作するプロパティが1行ずつ使ってしまい、画面を圧迫し、関連性も見えない。

### 問題4: 全セクションが常に展開されていて情報過多

基本設定（Name, Canvas, FPS 等）は一度決めたら滅多に変えないのに、常にスペースを取っている。

### 問題5: 操作の効率が悪い

- キーフレームの複製ができない（似たキーフレームを作るとき毎回ゼロから）
- レイヤーの複製ができない
- 右クリックメニューがない
- キーボードショートカットがない（Ctrl+S で保存等）
- キーフレーム追加後、何のプロパティも設定されていない空のキーフレームが生まれる

### 問題6: 何がどう繋がっているか分からない

- タイムラインの◆をクリックしてもキーフレーム詳細が見えない位置にある
- イージングが「easeOut」とテキストで表示されるだけで、カーブの形が想像できない
- レイヤーの visible 範囲がタイムラインに反映されているが、編集との連動が弱い

---

## 改善計画

### A. 全面日本語化

| 現在 | 改善後 |
|------|--------|
| File | ファイル |
| New | 新規 |
| Save | 保存 |
| Refresh | 更新 |
| Metadata | 基本設定 |
| Name | エフェクト名 |
| Canvas | キャンバス |
| Duration (s) | 長さ（秒） |
| Preview | プレビュー |
| Play / Pause / Stop | 再生 / 一時停止 / 停止 |
| Loop | ループ |
| Speed | 速度 |
| Timeline | タイムライン |
| Layers | レイヤー |
| + Add Layer | + レイヤー追加 |
| - Remove | 削除 |
| Type | 図形 |
| Blend | ブレンド |
| (none) | 通常 |
| additive | 加算 |
| Visible | 表示範囲 |
| Emitter Properties | エミッター設定 |
| Count | 粒子数 |
| Lifetime (s) | 寿命（秒） |
| Angle | 角度 |
| Speed | 速度 |
| Gravity | 重力 |
| Drag | 抵抗 |
| Size | サイズ |
| Start Color / End Color | 開始色 / 終了色 |
| Seed | シード |
| Keyframes | キーフレーム |
| + Add Keyframe | + キーフレーム追加 |
| Time (s) | 時刻（秒） |
| Ease | 補間 |
| linear | 等速 |
| easeIn | 加速 |
| easeOut | 減速 |
| easeInOut | 緩急 |
| step | ステップ |
| Opacity | 不透明度 |
| Pen (outline) | 線（輪郭） |
| Brush (fill) | 塗り |
| Color | 色 |
| Width | 太さ |
| flat | 単色 |
| radial | 放射 |
| linear | 線形 |
| Center / Edge | 中心 / 端 |
| Start / End | 開始 / 終了 |
| Angle | 角度 |
| Radius | 半径 |
| Inner Radius | 内径 |
| Rotation | 回転 |
| + Add Property | + 変化を追加 |
| Unsaved changes | 未保存の変更あり |
| (base) | （基準） |

**ツールチップも日本語で付加**:

| フィールド | ツールチップ |
|-----------|-------------|
| キャンバス | 描画領域のピクセルサイズ（正方形） |
| FPS | 1秒あたりのフレーム数 |
| 長さ | エフェクト全体の再生時間 |
| 効果音 | Assets/Resources/Audio/ のファイル名（拡張子なし） |
| 表示範囲 | このレイヤーが表示される時間帯 |
| 補間 | キーフレーム間の変化の仕方 |
| 不透明度 | 0=透明、1=不透明。全ての色のアルファに乗算 |
| シード | 同じ値なら同じパーティクル配置になる |

### B. レイアウト再構成

#### 目標: 「見る場所」と「操作する場所」を近づける

```
┌─ ファイル ───────────────────────────────────┐
│ [新規▼] [保存] [更新]  effect_name.json      │
├─ プレビュー + 再生制御 ─────────────────────┤
│      ┌──────────────────┐                    │
│      │                  │                    │
│      │    プレビュー     │                    │
│      │                  │                    │
│      └──────────────────┘                    │
│  [◀◀][◀] 0.35秒 (11/30) [▶][▶▶]            │
│  [▶再生][■停止] ループ☑  速度:[===]          │
├─ タイムライン ──────────────────────────────┤
│  glow    ◆──────◆─────────◆                 │
│  ring       ◆──────────◆                    │
│  sparks  ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓                  │
│           0.0   0.5   1.0   1.5              │
├─ ▶ 基本設定（折りたたみ）──────────────────┤
│   エフェクト名:[____]  キャンバス:[100]       │
│   FPS:[30]  長さ:[1.5]秒  効果音:[____]      │
├─ レイヤー ──────────────────────────────────┤
│  [E]● glow    circle  加算  [▲][▼]          │
│  [E]● ring    ring    加算  [▲][▼]          │
│  [E]● sparks  emitter 加算  [▲][▼]          │
│  [+ レイヤー追加]  [削除]                     │
├─ レイヤー: glow ────────────────────────────┤
│  ID:[glow]  図形:[circle▼]  ブレンド:[加算▼] │
│  表示範囲: [0.0] - [1.5]                     │
│                                              │
│  ── キーフレーム ──                           │
│  ▶ t=0.00秒 減速 （基準）                     │
│    t=0.30秒 加速  radius, 塗り                │
│    t=1.50秒 等速  radius, 不透明度            │
│  [+ キーフレーム追加]                          │
│                                              │
│  ── 編集: t=0.00秒（基準）──                  │
│  ┌ 時刻・補間 ────────────────────────────┐ │
│  │ 時刻: [0.00]秒   補間: [減速 ▼] ～～  │ │
│  ├ 位置・形状 ────────────────────────────┤ │
│  │ 位置:  X [50]  Y [50]                 │ │
│  │ 半径:  [10]                            │ │
│  ├ 見た目 ────────────────────────────────┤ │
│  │ 塗り: [放射▼]                          │ │
│  │   中心: [■■] #FFFFFF00                 │ │
│  │   端:   [■■] #FF880000                 │ │
│  │ 不透明度: [====] 1.0                    │ │
│  └────────────────────────────────────────┘ │
└──────────────────────────────────────────────┘
```

#### 変更点

1. **基本設定を折りたたみ可能に**（`EditorGUILayout.Foldout`）
   - デフォルトは閉じた状態。一度設定したら触らない項目を隠す
   - ファイル操作行にエフェクト名を表示するので閉じていても名前は見える

2. **プレビュー + 再生制御を常に画面上部に**
   - スクロールしてもプレビューが見える状態を維持
   - ただし UnityのEditorWindowでスクロール外固定は難しいため、代替案として**プレビューサイズをコンパクトに**（デフォルト150px）して、全体のスクロール量を減らす

3. **基本設定はタイムラインの下に移動**
   - 使用頻度: プレビュー > タイムライン > レイヤー操作 >> 基本設定
   - 頻度が低い基本設定は折りたたんで下方に

### C. プロパティのグルーピング

キーフレーム編集画面のプロパティをカテゴリで分ける:

#### 位置・形状

関連するプロパティを1行にまとめる:

```
位置・形状
  位置: X [50]  Y [50]          ← 1行にペアで表示
  半径: [20]
```

```
位置・形状
  始点: X1 [20]  Y1 [20]       ← 1行にペアで
  終点: X2 [80]  Y2 [80]       ← 1行にペアで
  太さ: 始 [5]  終 [0.5]       ← 1行にペアで
```

```
位置・形状
  位置: X [50]  Y [50]          ← 1行にペアで
  サイズ: Rx [20]  Ry [10]      ← 1行にペアで
  回転: [45]°
```

#### 見た目

```
見た目
  線: [■] #FFFFFF  太さ [2]     ← 色と太さを1行に
  塗り: [放射 ▼]
    中心: [■] #FFFFFFCC
    端:   [■] #FF880000
  不透明度: [====] 0.8
```

#### 実装

```csharp
// 位置・形状セクション
EditorGUILayout.LabelField("位置・形状", EditorStyles.boldLabel);

// X, Y を1行に
EditorGUILayout.BeginHorizontal();
EditorGUILayout.LabelField("位置", GUILayout.Width(40));
kf.X = EditorGUILayout.FloatField("X", kf.X ?? 50);
kf.Y = EditorGUILayout.FloatField("Y", kf.Y ?? 50);
if (canRemove) { /* x ボタン */ }
EditorGUILayout.EndHorizontal();
```

グルーピングのルール:

| カテゴリ | 含まれるプロパティ |
|---------|-------------------|
| 位置・形状 | x/y, radius, inner_radius, rx/ry, width/height, rotation, size, x1/y1, x2/y2, width_start/width_end, startAngle/endAngle |
| 見た目 | pen, brush, opacity |

### D. 操作効率の改善

#### D-1. キーフレーム複製

「キーフレーム追加」の横に「複製」ボタンを置く。選択中のキーフレームを丸ごとコピーし、時刻を +0.1秒ずらして挿入。

```csharp
if (GUILayout.Button("複製", GUILayout.Width(50)))
{
    DuplicateKeyframe(layer, _selectedKeyframeIndex);
}
```

#### D-2. レイヤー複製

レイヤーリストの「追加」横に「複製」ボタン。選択中のレイヤーをディープコピー。IDに `_copy` を付加。

#### D-3. 右クリックメニュー

**レイヤーの右クリック:**
- 複製
- 削除
- 上に移動 / 下に移動

**キーフレームの右クリック:**
- 複製
- 削除
- 現在のプレビュー時刻に移動

**タイムラインの右クリック:**
- その時刻にキーフレーム追加

```csharp
// レイヤー行で右クリック検出
if (Event.current.type == EventType.ContextClick && layerRect.Contains(Event.current.mousePosition))
{
    var menu = new GenericMenu();
    menu.AddItem(new GUIContent("複製"), false, () => DuplicateLayer(i));
    menu.AddItem(new GUIContent("削除"), false, () => RemoveLayer(i));
    menu.AddSeparator("");
    if (i > 0)
        menu.AddItem(new GUIContent("上に移動"), false, () => SwapLayers(i, i - 1));
    if (i < _kfxDef.Layers.Count - 1)
        menu.AddItem(new GUIContent("下に移動"), false, () => SwapLayers(i, i + 1));
    menu.ShowAsContext();
    Event.current.Use();
}
```

#### D-4. キーボードショートカット（体系的設計）

Windows標準操作 + タイムライン系ツール（After Effects / Premiere / DAW）の慣例に準拠した体系的なキーバインド設計。

##### グローバル操作（常時有効）

| ショートカット | 操作 | 分類 | 備考 |
|---------------|------|------|------|
| **Ctrl+S** | 保存 | ファイル | Windows標準。最重要 |
| **Ctrl+Z** | 元に戻す | 編集 | 簡易Undoシステム連携 |
| **Ctrl+Y** | やり直し | 編集 | Undo/Redoセット |
| **Space** | 再生/一時停止 | 再生 | 全動画・音楽ツール共通 |
| **Shift+Space** | 先頭から再生 | 再生 | Premiere慣例 |
| **Backspace** | 停止して先頭に戻る | 再生 | AE相当 |
| **Home** | 先頭フレームへ | 移動 | Windows標準 |
| **End** | 最終フレームへ | 移動 | Windows標準 |
| **← / →** | 1フレーム移動 | 移動 | AE/Premiere標準 |
| **Shift+← / Shift+→** | 10フレーム移動 | 移動 | 大ジャンプ |
| **Ctrl+← / Ctrl+→** | 前/次のキーフレームにジャンプ | 移動 | AE相当。KF確認に頻出 |
| **Ctrl+L** | ループ切替 | 再生 | |
| **Ctrl+= / Ctrl+-** | 再生速度 +0.25 / -0.25 | 再生 | |
| **?** または **F1** | ショートカット一覧を表示 | ヘルプ | H. モーダル参照 |

##### レイヤー操作（レイヤー選択中）

| ショートカット | 操作 | 備考 |
|---------------|------|------|
| **Alt+↑ / Alt+↓** | 前/次のレイヤーを選択 | レイヤー間ナビゲーション |
| **Ctrl+Shift+↑ / Ctrl+Shift+↓** | レイヤーの並べ替え | Photoshop慣例 |
| **Ctrl+D** | レイヤー複製 | KF未選択時はレイヤーが対象 |
| **Delete** | レイヤー削除 | KF未選択時はレイヤーが対象 |
| **K** | 現在時刻にキーフレーム追加 | AE の「KFを打つ」操作。最頻出 |
| **F2** | レイヤーID名の編集にフォーカス | Windows標準のリネーム |

##### キーフレーム操作（キーフレーム選択中）

| ショートカット | 操作 | 備考 |
|---------------|------|------|
| **↑ / ↓** | 前/次のキーフレームを選択 | KF一覧内のナビゲーション |
| **Ctrl+D** | キーフレーム複製 | +0.1秒ずらして挿入 |
| **Delete** | キーフレーム削除 | |
| **[ / ]** | 時刻を微調整（-0.01s / +0.01s） | DAW慣例。精密操作 |
| **Shift+[ / Shift+]** | 時刻を大調整（-0.1s / +0.1s） | 上の拡大版 |
| **Escape** | キーフレーム選択解除 | 段階的: KF解除→レイヤー解除 |

##### キーバインドの衝突回避

- **Ctrl+S**: Unity のシーン保存とバッティングするが、EditorWindow フォーカス中は `Event.current.Use()` で消費される。エフェクト編集中はシーンを触らないため問題なし。
- **Space**: Unity の GUI ボタンフォーカス操作と衝突しうるが、`HandleKeyboardShortcuts()` を `OnGUI` の先頭で呼ぶことで先にキャプチャする。
- **Delete**: EditorWindow 内で完結するため他と衝突しない。
- **← → ↑ ↓**: テキストフィールドにフォーカスがあるときはショートカットを無効化する（`EditorGUIUtility.editingTextField` で判定）。

##### 実装構造

```csharp
private void HandleKeyboardShortcuts()
{
    Event e = Event.current;
    if (e.type != EventType.KeyDown) return;

    // テキスト編集中はショートカット無効（←→等がテキスト操作と衝突するため）
    // ただし Ctrl+S, Ctrl+Z, Ctrl+Y, Space は常時有効
    bool editing = EditorGUIUtility.editingTextField;

    // === グローバル（常時有効） ===
    if (e.control && e.keyCode == KeyCode.S) { SaveFile(); e.Use(); return; }
    if (e.control && e.keyCode == KeyCode.Z) { Undo(); e.Use(); return; }
    if (e.control && e.keyCode == KeyCode.Y) { Redo(); e.Use(); return; }
    if (e.keyCode == KeyCode.Space && !editing) { TogglePlayback(); e.Use(); return; }
    if (e.shift && e.keyCode == KeyCode.Space) { PlayFromStart(); e.Use(); return; }
    if (e.keyCode == KeyCode.Backspace && !editing) { StopAndRewind(); e.Use(); return; }
    if (e.keyCode == KeyCode.F1 || (e.keyCode == KeyCode.Slash && e.shift))
        { _showShortcutModal = true; e.Use(); return; }

    if (editing) return; // 以降はテキスト編集中に無効

    // === 移動 ===
    if (e.keyCode == KeyCode.Home) { GoToFrame(0); e.Use(); return; }
    if (e.keyCode == KeyCode.End) { GoToFrame(TotalFrames - 1); e.Use(); return; }
    if (e.keyCode == KeyCode.LeftArrow)
    {
        if (e.control) JumpToPrevKeyframe();
        else if (e.shift) GoToFrame(_currentFrame - 10);
        else GoToFrame(_currentFrame - 1);
        e.Use(); return;
    }
    if (e.keyCode == KeyCode.RightArrow)
    {
        if (e.control) JumpToNextKeyframe();
        else if (e.shift) GoToFrame(_currentFrame + 10);
        else GoToFrame(_currentFrame + 1);
        e.Use(); return;
    }

    // === レイヤー/KF 操作 ===
    if (e.keyCode == KeyCode.K) { AddKeyframeAtCurrentTime(); e.Use(); return; }
    if (e.control && e.keyCode == KeyCode.D) { DuplicateSelected(); e.Use(); return; }
    if (e.keyCode == KeyCode.Delete) { DeleteSelected(); e.Use(); return; }
    if (e.keyCode == KeyCode.Escape) { DeselectStep(); e.Use(); return; }

    // === KFナビゲーション ===
    if (e.keyCode == KeyCode.UpArrow)
    {
        if (e.alt) SelectPrevLayer();
        else if (e.control && e.shift) MoveLayerUp();
        else SelectPrevKeyframe();
        e.Use(); return;
    }
    if (e.keyCode == KeyCode.DownArrow)
    {
        if (e.alt) SelectNextLayer();
        else if (e.control && e.shift) MoveLayerDown();
        else SelectNextKeyframe();
        e.Use(); return;
    }

    // === KF時刻微調整 ===
    if (e.keyCode == KeyCode.LeftBracket)
    {
        NudgeKeyframeTime(e.shift ? -0.1f : -0.01f);
        e.Use(); return;
    }
    if (e.keyCode == KeyCode.RightBracket)
    {
        NudgeKeyframeTime(e.shift ? 0.1f : 0.01f);
        e.Use(); return;
    }

    // === 再生速度 ===
    if (e.control && e.keyCode == KeyCode.Equals) { AdjustSpeed(0.25f); e.Use(); return; }
    if (e.control && e.keyCode == KeyCode.Minus) { AdjustSpeed(-0.25f); e.Use(); return; }
    if (e.control && e.keyCode == KeyCode.L) { _loop = !_loop; e.Use(); return; }
}
```

##### 簡易 Undo/Redo システム

KfxDefinition は通常のC#クラスのため Unity の `Undo.RecordObject` が使えない。
代わりに JSON スナップショットによる簡易 Undo を実装する。

```csharp
private readonly List<string> _undoStack = new List<string>();
private readonly List<string> _redoStack = new List<string>();
private const int MaxUndoDepth = 30;

// MarkDirty() の中で呼ぶ
private void PushUndo()
{
    string snapshot = JsonConvert.SerializeObject(_kfxDef,
        new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
    _undoStack.Add(snapshot);
    if (_undoStack.Count > MaxUndoDepth)
        _undoStack.RemoveAt(0);
    _redoStack.Clear();
}

private void Undo()
{
    if (_undoStack.Count == 0) return;
    // 現在の状態をRedoスタックに退避
    _redoStack.Add(JsonConvert.SerializeObject(_kfxDef,
        new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));
    // 直前の状態を復元
    string prev = _undoStack[_undoStack.Count - 1];
    _undoStack.RemoveAt(_undoStack.Count - 1);
    _kfxDef = JsonConvert.DeserializeObject<KfxDefinition>(prev);
    _needsRecompile = true;
}

private void Redo()
{
    if (_redoStack.Count == 0) return;
    _undoStack.Add(JsonConvert.SerializeObject(_kfxDef,
        new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }));
    string next = _redoStack[_redoStack.Count - 1];
    _redoStack.RemoveAt(_redoStack.Count - 1);
    _kfxDef = JsonConvert.DeserializeObject<KfxDefinition>(next);
    _needsRecompile = true;
}
```

**設計判断**: ScriptableObject + `Undo.RecordObject` ではなくJSONスナップショット方式を採用する理由：
- KfxDefinition はエディタ専用のデータクラスであり、SerializedObject ではない
- JSONスナップショットは実装がシンプルで、深いネスト（KfxLayer → KfxKeyframe → KfxBrushKeyframe）を確実にキャプチャできる
- 30スナップショットのメモリコストは数十KB程度で問題にならない

#### D-5. キーフレーム追加時に直前のキーフレームの値をベースにする

現在は空のキーフレームが追加される。改善後は、直前のキーフレームで設定されているプロパティのうち、アニメーションしやすいもの（radius, opacity等）をコピーして初期値にする。

### E. イージングの視覚的表示

イージング選択ドロップダウンの横に、小さなカーブアイコンを表示。

```
補間: [減速 ▼]  ╭─╮   ← 小さなカーブ図
                │  ╰──
```

```csharp
private void DrawEasingPreview(string ease, Rect rect)
{
    // 30ピクセル幅のミニカーブを描画
    Handles.BeginGUI();
    Handles.color = new Color(0.6f, 0.8f, 1f, 0.8f);

    int steps = 20;
    Vector3 prev = new Vector3(rect.x, rect.yMax, 0);
    for (int i = 1; i <= steps; i++)
    {
        float t = i / (float)steps;
        float v = KfxEasing.Apply(ease, t);
        Vector3 next = new Vector3(
            rect.x + t * rect.width,
            rect.yMax - v * rect.height,
            0);
        Handles.DrawLine(prev, next);
        prev = next;
    }
    Handles.EndGUI();
}
```

### F. タイムラインの強化

#### F-1. ダブルクリックでキーフレーム追加

タイムラインの空白領域をダブルクリック → その時刻にキーフレーム追加。

```csharp
if (Event.current.type == EventType.MouseDown && Event.current.clickCount == 2)
{
    // タイムライン領域内かチェック
    // クリック位置から時刻を算出
    // 選択中レイヤーにキーフレーム追加
}
```

#### F-2. キーフレームのドラッグ移動

タイムラインの◆マーカーをドラッグして時刻を変更。

```csharp
// MouseDown で◆をキャプチャ
// MouseDrag で時刻を更新
// MouseUp で確定（時間順ソート）

private int _draggingKfIndex = -1;
private int _draggingLayerIndex = -1;
```

#### F-3. タイムラインの右クリック

```
右クリックメニュー:
  ここにキーフレームを追加
  ここに再生位置を移動
```

### G. プレビュー内でのドラッグ操作

プレビュー画面上でマウスドラッグ → 選択中キーフレームの x, y が変わる。

```csharp
private void HandlePreviewDrag(Rect previewRect)
{
    if (_selectedKeyframeIndex < 0) return;
    Event e = Event.current;

    if (e.type == EventType.MouseDrag && previewRect.Contains(e.mousePosition))
    {
        // マウス位置をキャンバス座標に変換
        float canvasX = ((e.mousePosition.x - previewRect.x) / previewRect.width) * _kfxDef.Canvas;
        float canvasY = ((e.mousePosition.y - previewRect.y) / previewRect.height) * _kfxDef.Canvas;

        var kf = GetSelectedKeyframe();
        if (kf != null)
        {
            kf.X = Mathf.Round(canvasX);
            kf.Y = Mathf.Round(canvasY);
            MarkDirty();
        }
        e.Use();
    }
}
```

これは circle, ellipse, rect, ring, point, arc の x/y に有効。line, tapered_line は始点/終点を別途扱う必要がある（将来対応）。

### H. ショートカット一覧モーダル

エディタ内に「ショートカット一覧」ボタンを設置し、押すとモーダルウィンドウで全キーバインドを一覧表示する。

#### 目的

- ショートカットは覚えるまでが障壁。**いつでも確認できる場所**があることで学習コストを下げる
- モーダル内にカテゴリ分けされた一覧を表示し、目的の操作からキーを逆引きできる

#### UIデザイン

```
┌─ ファイル ─────────────────────────────────────────┐
│ [新規▼] [保存] [更新]  effect_name.json    [? キー] │
│                                              ↑ここ  │
```

ファイルセクションの右端に `[?]` または `[キー一覧]` ボタンを配置。
押すと以下のモーダルウィンドウが開く:

```
┌─ キーボードショートカット一覧 ───────────────── [×] ┐
│                                                     │
│  ■ ファイル・編集                                     │
│    Ctrl+S        保存                                │
│    Ctrl+Z        元に戻す                             │
│    Ctrl+Y        やり直し                             │
│                                                     │
│  ■ 再生制御                                          │
│    Space          再生 / 一時停止                     │
│    Shift+Space    先頭から再生                        │
│    Backspace      停止して先頭に戻る                   │
│    Ctrl+L         ループ切替                          │
│    Ctrl+= / -     再生速度 変更                       │
│                                                     │
│  ■ タイムライン移動                                    │
│    ← / →          1フレーム移動                       │
│    Shift+← / →    10フレーム移動                      │
│    Ctrl+← / →     前/次のキーフレームへ                │
│    Home / End     先頭 / 末尾                         │
│                                                     │
│  ■ レイヤー操作                                       │
│    Alt+↑ / ↓      レイヤー選択移動                    │
│    Ctrl+Shift+↑/↓ レイヤー並べ替え                    │
│    K              現在時刻にキーフレーム追加            │
│    F2             レイヤーID名を編集                   │
│                                                     │
│  ■ キーフレーム操作                                    │
│    ↑ / ↓          キーフレーム選択移動                 │
│    Ctrl+D         複製                               │
│    Delete         削除                               │
│    [ / ]          時刻を微調整（±0.01秒）              │
│    Shift+[ / ]    時刻を大調整（±0.1秒）               │
│    Escape         選択解除                            │
│                                                     │
│              このウィンドウ: ? または F1 で開く          │
└─────────────────────────────────────────────────────┘
```

#### 実装方式

**PopupWindow（EditorWindow内のポップアップ）** を使用。`EditorWindow.ShowAsDropDown` や `PopupWindow` よりも、独立した小さな `EditorWindow` として実装する方が柔軟で、スクロールにも対応できる。

```csharp
// ===== ショートカット一覧ウィンドウ =====
private class ShortcutReferenceWindow : EditorWindow
{
    private Vector2 _scroll;

    public static void Show()
    {
        var window = GetWindow<ShortcutReferenceWindow>(
            utility: true, title: "キーボードショートカット一覧");
        window.minSize = new Vector2(380, 480);
        window.maxSize = new Vector2(420, 600);
        window.ShowUtility(); // モーダルに近い挙動（常に前面）
    }

    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        DrawCategory("ファイル・編集", new[] {
            ("Ctrl+S",          "保存"),
            ("Ctrl+Z",          "元に戻す"),
            ("Ctrl+Y",          "やり直し"),
        });

        DrawCategory("再生制御", new[] {
            ("Space",           "再生 / 一時停止"),
            ("Shift+Space",     "先頭から再生"),
            ("Backspace",       "停止して先頭に戻る"),
            ("Ctrl+L",          "ループ切替"),
            ("Ctrl+= / Ctrl+-", "再生速度 変更"),
        });

        DrawCategory("タイムライン移動", new[] {
            ("← / →",           "1フレーム移動"),
            ("Shift+← / →",     "10フレーム移動"),
            ("Ctrl+← / →",      "前/次のキーフレームへジャンプ"),
            ("Home / End",      "先頭 / 末尾"),
        });

        DrawCategory("レイヤー操作", new[] {
            ("Alt+↑ / ↓",       "レイヤー選択移動"),
            ("Ctrl+Shift+↑/↓",  "レイヤー並べ替え"),
            ("K",               "現在時刻にキーフレーム追加"),
            ("F2",              "レイヤーID名を編集"),
        });

        DrawCategory("キーフレーム操作", new[] {
            ("↑ / ↓",           "キーフレーム選択移動"),
            ("Ctrl+D",          "複製"),
            ("Delete",          "削除"),
            ("[ / ]",           "時刻を微調整（±0.01秒）"),
            ("Shift+[ / ]",     "時刻を大調整（±0.1秒）"),
            ("Escape",          "選択解除"),
        });

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField(
            "このウィンドウ: ? または F1 で開く",
            EditorStyles.centeredGreyMiniLabel);

        EditorGUILayout.EndScrollView();
    }

    private void DrawCategory(string title, (string key, string desc)[] entries)
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

        foreach (var (key, desc) in entries)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(key, EditorStyles.miniLabel,
                GUILayout.Width(140));
            EditorGUILayout.LabelField(desc);
            EditorGUILayout.EndHorizontal();
        }
    }
}
```

#### ボタン配置

ファイルセクション（`DrawFileSection`）の右端に `[?]` ボタンを追加:

```csharp
private void DrawFileSection()
{
    EditorGUILayout.BeginHorizontal();
    {
        // ... 既存のPopup, 新規, 保存, 更新 ボタン ...

        GUILayout.FlexibleSpace();
        if (GUILayout.Button(new GUIContent("?", "キーボードショートカット一覧 (F1)"),
            GUILayout.Width(24), GUILayout.Height(18)))
        {
            ShortcutReferenceWindow.Show();
        }
    }
    EditorGUILayout.EndHorizontal();
}
```

#### F1 / ? キーからの起動

`HandleKeyboardShortcuts()` 内（D-4参照）で:

```csharp
if (e.keyCode == KeyCode.F1 ||
    (e.keyCode == KeyCode.Slash && e.shift)) // Shift+/ = ?
{
    ShortcutReferenceWindow.Show();
    e.Use();
    return;
}
```

---

## 実装優先度

| 優先度 | 改善 | 効果 | 工数 |
|--------|------|------|------|
| 1 | A. 日本語化 | 全体の理解しやすさが劇的に向上 | 小 |
| 2 | C. プロパティグルーピング | 画面の圧迫が減り関連性が明確に | 小 |
| 3 | B. レイアウト再構成（折りたたみ + 順序変更） | スクロール量が減る | 小 |
| 4 | D-1/D-2. キーフレーム/レイヤー複製 | 操作効率が大幅向上 | 小 |
| 5 | D-4. キーボードショートカット + Undo/Redo | 操作の安心感と効率が劇的に向上 | 中 |
| 6 | H. ショートカット一覧モーダル | ショートカットの学習コストを下げる（D-4とセット） | 小 |
| 7 | D-3. 右クリックメニュー | 発見性は低いが効率向上 | 小 |
| 8 | E. イージング視覚化 | カーブの形が分かる | 中 |
| 9 | F. タイムライン強化（ダブルクリック/ドラッグ） | 直感的な操作 | 中 |
| 10 | G. プレビュー内ドラッグ | 位置調整が直感的 | 中 |

**注記**: D-4（キーボードショートカット）と H（ショートカット一覧モーダル）はセットで実装すべき。ショートカットだけあっても見つけられなければ使われない。

---

## 変更ファイル

| ファイル | 変更内容 |
|---------|---------|
| `Assets/Editor/Effects/KfxEditorWindow.cs` | 全改善項目（A〜H） |

新規ファイルは不要。KfxEditorWindow.cs への変更のみ。
`ShortcutReferenceWindow` はインナークラスとして同ファイル内に配置。
