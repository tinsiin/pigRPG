# エフェクトプレビューウィンドウ設計書

## 概要

Unityエディタ内でエフェクトJSONをプレビュー・確認できるエディタ拡張ウィンドウ。
ゲームを実行せずにエフェクトの見た目を確認でき、開発効率を向上させる。

### 目的

- エフェクトJSONの作成・編集後、即座に結果を確認
- BattleIconUIがなくてもエフェクトを単体テスト可能
- フレームごとの描画内容を詳細に確認

### 配置

```
Assets/
└── Editor/
    └── Effects/
        └── EffectPreviewWindow.cs
```

※ `Editor`フォルダ内のスクリプトはビルドに含まれない

---

## UI設計

### ウィンドウレイアウト

```
┌──────────────────────────────────────────────────────┐
│ Effect Previewer                                 [×] │
├──────────────────────────────────────────────────────┤
│                                                      │
│  ┌─ Effect Selection ─────────────────────────────┐  │
│  │ Effect: [test_fire               ▼]  [Reload]  │  │
│  └────────────────────────────────────────────────┘  │
│                                                      │
│  ┌─ Preview ──────────────────────────────────────┐  │
│  │                                                │  │
│  │              ┌────────────┐                    │  │
│  │              │            │                    │  │
│  │              │  Preview   │                    │  │
│  │              │  Texture   │                    │  │
│  │              │            │                    │  │
│  │              └────────────┘                    │  │
│  │                                                │  │
│  │  Background: [Dark ▼]  Size: [200]px          │  │
│  └────────────────────────────────────────────────┘  │
│                                                      │
│  ┌─ Playback Control ─────────────────────────────┐  │
│  │                                                │  │
│  │  Frame: [|◀] [◀] [ 2 / 5 ] [▶] [▶|]           │  │
│  │                                                │  │
│  │  [▶ Play]  [⏸ Pause]  [■ Stop]   ☑ Loop      │  │
│  │                                                │  │
│  │  Speed: [========●==] 1.0x                    │  │
│  └────────────────────────────────────────────────┘  │
│                                                      │
│  ┌─ Effect Info ──────────────────────────────────┐  │
│  │  Name:    test_fire                           │  │
│  │  Canvas:  100 x 100                           │  │
│  │  FPS:     12                                  │  │
│  │  Frames:  5                                   │  │
│  │  SE:      fire_burst                          │  │
│  │  Duration: 0.42s                              │  │
│  └────────────────────────────────────────────────┘  │
│                                                      │
│  ┌─ Current Frame Detail ─────────────────────────┐  │
│  │  Frame #2  (Duration: 0.083s)                 │  │
│  │  Shapes: 2                                    │  │
│  │    [0] circle  x:50 y:50 r:15  brush:#FFA500 │  │
│  │    [1] circle  x:50 y:50 r:8   brush:#FFFF00 │  │
│  └────────────────────────────────────────────────┘  │
│                                                      │
└──────────────────────────────────────────────────────┘
```

### メニューからのアクセス

```
Window > Effects > Effect Previewer
```

ショートカット: `Ctrl+Shift+E` (任意)

---

## 機能仕様

### 1. エフェクト選択

| 項目 | 仕様 |
|------|------|
| ドロップダウン | `Resources/Effects/`内の全JSONファイルをリスト |
| Reloadボタン | 選択中のJSONを再読み込み（編集反映用） |
| Refreshボタン | ファイルリストを再スキャン |
| ドラッグ&ドロップ | JSONファイルをウィンドウにドロップで選択 |

### 2. プレビュー表示

| 項目 | 仕様 |
|------|------|
| 表示方式 | `Texture2D`を`GUI.DrawTexture`で描画 |
| 背景色 | 選択可能（Dark/Light/Checkerboard/Custom） |
| 表示サイズ | スライダーで調整（50px〜400px） |
| アスペクト比 | 常に1:1を維持 |

### 3. 再生制御

| ボタン | 機能 |
|--------|------|
| `\|◀` (First) | 最初のフレームへ |
| `◀` (Prev) | 前のフレームへ |
| `▶` (Next) | 次のフレームへ |
| `▶\|` (Last) | 最後のフレームへ |
| `▶ Play` | 再生開始 |
| `⏸ Pause` | 一時停止 |
| `■ Stop` | 停止（最初のフレームに戻る） |
| `☑ Loop` | ループ再生ON/OFF |

### 4. 速度調整

| 項目 | 仕様 |
|------|------|
| スライダー | 0.1x 〜 3.0x |
| デフォルト | 1.0x |
| 表示 | 実際のFPS = 元FPS × 速度倍率 |

### 5. エフェクト情報表示

| 表示項目 | 説明 |
|----------|------|
| Name | エフェクト名 |
| Canvas | キャンバスサイズ |
| FPS | フレームレート |
| Frames | 総フレーム数 |
| SE | 効果音ファイル名（nullなら「None」） |
| Duration | 総再生時間（計算値） |

### 6. フレーム詳細表示

| 表示項目 | 説明 |
|----------|------|
| Frame # | 現在のフレーム番号 |
| Duration | そのフレームの表示時間 |
| Shapes | 図形の数と各図形の概要 |

---

## クラス設計

### EffectPreviewWindow

```csharp
public class EffectPreviewWindow : EditorWindow
{
    // === 状態 ===
    private string[] _effectFiles;           // JSONファイルリスト
    private int _selectedIndex;              // 選択中のインデックス
    private EffectDefinition _definition;    // 読み込んだ定義
    private Texture2D _previewTexture;       // プレビュー用テクスチャ
    private EffectRenderer _renderer;        // 描画エンジン

    // === 再生状態 ===
    private int _currentFrame;               // 現在フレーム
    private bool _isPlaying;                 // 再生中か
    private bool _loop;                      // ループ再生
    private float _playbackSpeed = 1f;       // 再生速度
    private double _lastFrameTime;           // 最後のフレーム更新時刻

    // === 表示設定 ===
    private int _previewSize = 200;          // プレビューサイズ
    private Color _backgroundColor;          // 背景色
    private BackgroundType _bgType;          // 背景タイプ

    // === メソッド ===
    [MenuItem("Window/Effects/Effect Previewer")]
    public static void ShowWindow();

    private void OnEnable();                 // ウィンドウ有効化時
    private void OnDisable();                // ウィンドウ無効化時
    private void OnGUI();                    // UI描画
    private void Update();                   // 再生更新（EditorApplication.update）

    private void RefreshEffectList();        // ファイルリスト更新
    private void LoadEffect(string path);    // エフェクト読み込み
    private void RenderCurrentFrame();       // 現在フレームを描画
    private void AdvanceFrame();             // フレームを進める

    private void DrawEffectSelector();       // UI: 選択部
    private void DrawPreview();              // UI: プレビュー部
    private void DrawPlaybackControls();     // UI: 再生制御部
    private void DrawEffectInfo();           // UI: 情報表示部
    private void DrawFrameDetail();          // UI: フレーム詳細部
}
```

### 背景タイプ

```csharp
public enum BackgroundType
{
    Dark,           // #1E1E1E
    Light,          // #F0F0F0
    Checkerboard,   // 市松模様（透明度確認用）
    Custom          // カスタム色
}
```

---

## 実装詳細

### エディタ更新ループ

```csharp
private void OnEnable()
{
    EditorApplication.update += OnEditorUpdate;
}

private void OnDisable()
{
    EditorApplication.update -= OnEditorUpdate;
}

private void OnEditorUpdate()
{
    if (!_isPlaying || _definition == null) return;

    double currentTime = EditorApplication.timeSinceStartup;
    float frameDuration = _definition.Frames[_currentFrame].Duration / _playbackSpeed;

    if (currentTime - _lastFrameTime >= frameDuration)
    {
        _lastFrameTime = currentTime;
        AdvanceFrame();
        RenderCurrentFrame();
        Repaint(); // ウィンドウ再描画
    }
}
```

### プレビューテクスチャ描画

```csharp
private void DrawPreview()
{
    // 背景描画
    Rect previewRect = GUILayoutUtility.GetRect(_previewSize, _previewSize);
    EditorGUI.DrawRect(previewRect, _backgroundColor);

    // チェッカーボード（透明度確認用）
    if (_bgType == BackgroundType.Checkerboard)
    {
        DrawCheckerboard(previewRect);
    }

    // テクスチャ描画
    if (_previewTexture != null)
    {
        GUI.DrawTexture(previewRect, _previewTexture, ScaleMode.ScaleToFit);
    }
}
```

### JSONファイルスキャン

```csharp
private void RefreshEffectList()
{
    var guids = AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets/Resources/Effects" });
    var list = new List<string>();

    foreach (var guid in guids)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        if (path.EndsWith(".json"))
        {
            list.Add(Path.GetFileNameWithoutExtension(path));
        }
    }

    _effectFiles = list.ToArray();
}
```

---

## ファイル構成

```
Assets/
├── Editor/
│   └── Effects/
│       ├── EffectPreviewWindow.cs      # メインウィンドウ
│       └── EffectPreviewStyles.cs      # スタイル定義（任意）
└── Script/
    └── Effects/                        # 既存のエフェクトシステム
        ├── Core/
        ├── Rendering/
        ├── Playback/
        └── Integration/
```

---

## 依存関係

| 依存先 | 用途 |
|--------|------|
| `Effects.Core.EffectDefinition` | JSON定義クラス |
| `Effects.Rendering.EffectRenderer` | Texture2D描画 |
| `Effects.Rendering.ShapeDrawer` | 図形描画 |
| `Newtonsoft.Json` | JSONパース |

※ エディタ拡張から既存のEffectsシステムを再利用

---

## 実装優先度

### Phase 1: 基本機能（MVP）

1. ウィンドウ表示
2. エフェクト選択（ドロップダウン）
3. プレビュー表示（静止画）
4. フレーム送り（前/次）
5. エフェクト情報表示

### Phase 2: 再生機能

1. 再生/停止
2. ループ切替
3. 速度調整

### Phase 3: 拡張機能

1. 背景色切替
2. プレビューサイズ調整
3. フレーム詳細表示
4. ドラッグ&ドロップ対応
5. チェッカーボード背景

---

## 注意事項

### エディタ専用コード

- `Assets/Editor/`以下に配置必須
- `UnityEditor`名前空間を使用
- ビルドに含まれない

### リソース管理

```csharp
private void OnDisable()
{
    // テクスチャ破棄
    if (_previewTexture != null)
    {
        DestroyImmediate(_previewTexture);
        _previewTexture = null;
    }
}
```

### 再描画タイミング

- `Repaint()`を呼ばないとウィンドウが更新されない
- 再生中は`EditorApplication.update`から定期的に`Repaint()`

---

## 将来拡張案

| 機能 | 説明 |
|------|------|
| JSONエディタ統合 | ウィンドウ内でJSONを直接編集 |
| 複数エフェクト比較 | 2つのエフェクトを並べて表示 |
| エクスポート | GIF/連番PNGとして書き出し |
| パラメータ調整 | UIからcanvas/fpsを一時的に変更してテスト |
| SE再生 | プレビュー時にSEも再生 |
