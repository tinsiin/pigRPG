# KFX Editor UI改善 実装計画

## 目的

現在のKFX Editorは「キーフレームのnullable管理」「16進カラー手入力」「テキストリストのタイムライン」など、プログラマ向けの設計になっている。これを**人間が直感的に操作できるGUIエディタ**に改善する。

---

## 改善一覧と優先順位

| # | 改善 | 効果 | 変更ファイル |
|---|------|------|-------------|
| 1 | カラーピッカー | 色を視覚的に選べる | KfxEditorWindow.cs, EffectColorUtility.cs |
| 2 | キーフレームUI刷新 | nullable概念を隠す | KfxEditorWindow.cs |
| 3 | テンプレート | ゼロから作らなくて良い | KfxEditorWindow.cs |
| 4 | ビジュアルタイムライン | 時間軸が一目で分かる | KfxEditorWindow.cs |
| 5 | MinMaxSlider | 範囲入力が直感的になる | KfxEditorWindow.cs |
| 6 | レイヤーリスト改善 | 視認性・操作性向上 | KfxEditorWindow.cs |

---

## 1. カラーピッカー

### 現状の問題

```
[Color] [________________#FFAA4499__]  ← 16進テキスト入力
```

色を想像しながらRRGGBBAA 8桁の16進数を入力するのは非現実的。

### 改善後

```
[Color] [■■■■ ← 色プレビュー+クリックでピッカー] #FFAA4499
```

Unity標準の `EditorGUILayout.ColorField` を使い、視覚的に色を選択。横にHEX文字列を表示（コピペ用）。EffectPreviewWindowでは既に `EditorGUILayout.ColorField` を使用しているため実績あり。

### 実装詳細

#### 1-1. EffectColorUtility に ColorToHex を移動

`KfxCompiler.ColorToHex()` は現在 `private static` で、Compiler内部でしか使えない。これを `EffectColorUtility` に public メソッドとして移動する。

**EffectColorUtility.cs に追加:**
```csharp
/// <summary>
/// Color を "#RRGGBBAA" 形式の文字列に変換
/// </summary>
public static string ColorToHex(Color color)
{
    Color32 c32 = color;
    return $"#{c32.r:X2}{c32.g:X2}{c32.b:X2}{c32.a:X2}";
}
```

**KfxCompiler.cs の変更:**
```csharp
// 変更前
private static string ColorToHex(Color c) { ... }
// 呼び出し: ColorToHex(result)

// 変更後（private削除、EffectColorUtilityへ委譲）
// 呼び出し: EffectColorUtility.ColorToHex(result)
```

#### 1-2. KfxEditorWindow にヘルパーメソッド追加

```csharp
/// <summary>
/// カラーピッカー＋HEX表示。hex文字列を受け取り、hex文字列を返す
/// </summary>
private string DrawColorField(string label, string hexColor, bool showAlpha = true)
{
    Color color = EffectColorUtility.ParseColor(hexColor);
    EditorGUILayout.BeginHorizontal();
    EditorGUILayout.LabelField(label, GUILayout.Width(80));
    color = EditorGUILayout.ColorField(
        GUIContent.none, color, showEyedropper: true,
        showAlpha: showAlpha, hdr: false, GUILayout.Width(50));
    string newHex = EffectColorUtility.ColorToHex(color);
    // HEX表示（読み取り専用、コピー用）
    EditorGUILayout.SelectableLabel(newHex, EditorStyles.miniLabel,
        GUILayout.Width(80), GUILayout.Height(16));
    EditorGUILayout.EndHorizontal();
    return newHex;
}
```

#### 1-3. 置換箇所

| 現在のコード | 置換後 |
|-------------|--------|
| `kf.Pen.Color = EditorGUILayout.TextField("Color", kf.Pen.Color ?? "#FFFFFF");` | `kf.Pen.Color = DrawColorField("Color", kf.Pen.Color ?? "#FFFFFF");` |
| `kf.Brush.Color = EditorGUILayout.TextField("Color", kf.Brush.Color ?? "#FFFFFF80");` | `kf.Brush.Color = DrawColorField("Color", kf.Brush.Color ?? "#FFFFFF80");` |
| `kf.Brush.Center = EditorGUILayout.TextField("Center", ...);` | `kf.Brush.Center = DrawColorField("Center", ...);` |
| `kf.Brush.Edge = EditorGUILayout.TextField("Edge", ...);` | `kf.Brush.Edge = DrawColorField("Edge", ...);` |
| `kf.Brush.Start = EditorGUILayout.TextField("Start", ...);` | `kf.Brush.Start = DrawColorField("Start", ...);` |
| `kf.Brush.End = EditorGUILayout.TextField("End", ...);` | `kf.Brush.End = DrawColorField("End", ...);` |
| `layer.ColorStart = EditorGUILayout.TextField("Color Start", ...);` | `layer.ColorStart = DrawColorField("Start", ...);` |
| `layer.ColorEnd = EditorGUILayout.TextField("Color End", ...);` | `layer.ColorEnd = DrawColorField("End", ...);` |

合計8箇所。

---

## 2. キーフレームUI刷新（「プロパティ追加」方式）

### 現状の問題

```
☑ X: [50]        ← チェックボックスの意味が分からない
☑ Y: [50]           nullableという内部概念がUIに漏れている
☐ Radius: [0]    ← チェックOFFだと灰色になるが、何が起きるか不明
```

### 改善後

**最初のキーフレーム（index 0）:**
```
Keyframe @ 0.00s  ease: [easeOut ▼]
  X: [50]             ← 全プロパティ表示、チェックボックスなし
  Y: [50]                プロパティ削除もできない（全部必須）
  Radius: [10]
  Brush: [■■] radial
```

**2番目以降のキーフレーム（index > 0）:**
```
Keyframe @ 0.50s  ease: [linear ▼]
  radius: [30]        [x]   ← 設定済みプロパティのみ表示
  opacity: [0.5]      [x]      [x]で削除（=前から継承に戻す）

  [+ プロパティを追加 ▼]    ← まだ設定していないプロパティをドロップダウンで選ぶ
     x, y, brush, pen ...
```

### 実装詳細

#### 2-1. シェイプタイプごとの利用可能プロパティ定義

```csharp
private static readonly Dictionary<string, string[]> ShapeProperties =
    new Dictionary<string, string[]>
{
    { "circle",       new[] { "x", "y", "radius", "pen", "brush", "opacity" } },
    { "ellipse",      new[] { "x", "y", "rx", "ry", "rotation", "pen", "brush", "opacity" } },
    { "rect",         new[] { "x", "y", "width", "height", "rotation", "pen", "brush", "opacity" } },
    { "ring",         new[] { "x", "y", "radius", "inner_radius", "pen", "brush", "opacity" } },
    { "arc",          new[] { "x", "y", "radius", "startAngle", "endAngle", "pen", "opacity" } },
    { "line",         new[] { "x1", "y1", "x2", "y2", "pen", "opacity" } },
    { "tapered_line", new[] { "x1", "y1", "x2", "y2", "width_start", "width_end", "pen", "opacity" } },
    { "point",        new[] { "x", "y", "size", "pen", "opacity" } },
};
```

#### 2-2. キーフレームのプロパティ有無を判定するヘルパー

```csharp
/// <summary>
/// 指定プロパティがキーフレームに設定されているかを判定
/// </summary>
private bool HasProperty(KfxKeyframe kf, string prop)
{
    return prop switch
    {
        "x" => kf.X.HasValue,
        "y" => kf.Y.HasValue,
        "radius" => kf.Radius.HasValue,
        "inner_radius" => kf.InnerRadius.HasValue,
        // ... 全プロパティ
        "pen" => kf.Pen != null,
        "brush" => kf.Brush != null,
        "opacity" => kf.Opacity.HasValue,
        _ => false
    };
}

/// <summary>
/// 指定プロパティをクリア（null化）
/// </summary>
private void ClearProperty(KfxKeyframe kf, string prop) { ... }

/// <summary>
/// 指定プロパティをデフォルト値で初期化
/// </summary>
private void InitProperty(KfxKeyframe kf, string prop) { ... }
```

#### 2-3. DrawKeyframeDetail の書き替え

```csharp
private void DrawKeyframeDetail(KfxLayer layer, KfxKeyframe kf, int kfIndex)
{
    bool isFirstKeyframe = (kfIndex == 0);
    string shapeType = layer.Type?.ToLowerInvariant();
    string[] availableProps = ShapeProperties.GetValueOrDefault(shapeType, Array.Empty<string>());

    // time + ease は常に表示
    kf.Time = EditorGUILayout.FloatField("Time (s)", kf.Time);
    // ease ドロップダウン...

    if (isFirstKeyframe)
    {
        // 最初のKF: 全プロパティを通常表示（チェックボックスなし）
        foreach (var prop in availableProps)
            DrawPropertyField(kf, prop, canRemove: false);
    }
    else
    {
        // 2番目以降: 設定済みプロパティのみ表示（[x]削除ボタン付き）
        foreach (var prop in availableProps)
        {
            if (HasProperty(kf, prop))
                DrawPropertyField(kf, prop, canRemove: true);
        }

        // [+ プロパティを追加] ドロップダウン
        var unsetProps = availableProps.Where(p => !HasProperty(kf, p)).ToArray();
        if (unsetProps.Length > 0)
            DrawAddPropertyDropdown(kf, unsetProps);
    }
}
```

#### 2-4. 「プロパティを追加」ドロップダウン

```csharp
private void DrawAddPropertyDropdown(KfxKeyframe kf, string[] unsetProps)
{
    EditorGUILayout.Space(4);
    EditorGUILayout.BeginHorizontal();
    GUILayout.FlexibleSpace();
    if (EditorGUILayout.DropdownButton(
        new GUIContent("+ プロパティを追加"), FocusType.Keyboard, GUILayout.Width(160)))
    {
        var menu = new GenericMenu();
        foreach (var prop in unsetProps)
        {
            string p = prop; // クロージャキャプチャ用
            string label = GetPropertyDisplayName(p);
            menu.AddItem(new GUIContent(label), false, () =>
            {
                InitProperty(kf, p);
                MarkDirty();
            });
        }
        menu.ShowAsContext();
    }
    EditorGUILayout.EndHorizontal();
}
```

#### 2-5. 各プロパティ描画（削除ボタン付き）

```csharp
private void DrawPropertyField(KfxKeyframe kf, string prop, bool canRemove)
{
    EditorGUILayout.BeginHorizontal();

    // プロパティに応じた入力フィールドを描画
    switch (prop)
    {
        case "x":
            kf.X = EditorGUILayout.FloatField("X", kf.X ?? 50);
            break;
        case "radius":
            kf.Radius = EditorGUILayout.FloatField("Radius", kf.Radius ?? 10);
            break;
        case "pen":
            // pen セクションを描画（DrawPenFields相当）
            break;
        case "brush":
            // brush セクションを描画（DrawBrushFields相当）
            break;
        case "opacity":
            kf.Opacity = EditorGUILayout.Slider("Opacity", kf.Opacity ?? 1f, 0f, 1f);
            break;
        // ... 他のプロパティ
    }

    // 削除ボタン（2番目以降のキーフレームのみ）
    if (canRemove)
    {
        if (GUILayout.Button("x", GUILayout.Width(20), GUILayout.Height(18)))
        {
            ClearProperty(kf, prop);
            MarkDirty();
        }
    }

    EditorGUILayout.EndHorizontal();
}
```

pen/brush は複数フィールドからなるので、`DrawPropertyField` 内部で `BeginVertical` を使って囲み表示する:

```
  Pen:                                          [x]
    Color: [■■] #FFFFFF
    Width: [2]

  Brush:                                        [x]
    Type: [radial ▼]
    Center: [■■] #FFFFFFCC
    Edge:   [■■] #FF000000
```

---

## 3. テンプレート

### 現状の問題

「New」ボタンを押すと、circle 1つ + keyframe 1つの空エフェクトが生成される。毎回ゼロからパラメータを設定する必要がある。

### 改善後

「New」ボタンを押すとテンプレート選択メニューが開く。

### 実装詳細

#### 3-1. テンプレート定義

```csharp
private static class KfxTemplates
{
    public static KfxDefinition Empty() { /* 現行のNewFile()と同じ */ }
    public static KfxDefinition Pulse() { ... }
    public static KfxDefinition Shockwave() { ... }
    public static KfxDefinition Flash() { ... }
    public static KfxDefinition FadeInOut() { ... }
    public static KfxDefinition ParticleBurst() { ... }
    public static KfxDefinition Slash() { ... }
}
```

#### 3-2. 各テンプレートの内容

**パルス（Pulse）**
- 用途: パッシブ効果、状態異常の常時表示
- レイヤー1: circle, easeOut, radius 5→25→5, opacity 0.3→1→0.3
- duration: 1.0s, 推奨: loop

```json
{
  "layers": [
    {
      "id": "pulse", "type": "circle", "blend": "additive",
      "keyframes": [
        { "time": 0.0, "ease": "easeOut", "x": 50, "y": 50, "radius": 5,
          "brush": { "type": "radial", "center": "#FFFFFF80", "edge": "#FFFFFF00" }, "opacity": 0.3 },
        { "time": 0.5, "radius": 25, "opacity": 1.0 },
        { "time": 1.0, "radius": 5, "opacity": 0.3 }
      ]
    }
  ]
}
```

**衝撃波（Shockwave）**
- 用途: ヒット演出、スキル発動
- レイヤー1: ring, easeOut, radius 5→40, inner_radius 3→37, opacity 1→0
- レイヤー2: circle フラッシュ, 0.0→0.1s
- duration: 0.5s

**フラッシュ（Flash）**
- 用途: クリティカルヒット、閃光
- レイヤー1: circle, 全画面サイズ, 加算, opacity 1→0, easeIn
- duration: 0.3s

**フェードイン/アウト（FadeInOut）**
- 用途: バフ付与/解除
- レイヤー1: circle, opacity 0→1→1→0
- duration: 1.0s

**パーティクル噴出（ParticleBurst）**
- 用途: 爆発、破壊
- レイヤー1: emitter, count 20, 全方向, gravity あり
- duration: 1.5s

**斬撃（Slash）**
- 用途: 物理攻撃
- レイヤー1: tapered_line, 斜め方向
- レイヤー2: ring 衝撃波 (visible [0.1, 0.5])
- duration: 0.5s

#### 3-3. NewFile ボタンの変更

```csharp
private void DrawFileSection()
{
    // ...
    if (GUILayout.Button("New", GUILayout.Width(50)))
    {
        var menu = new GenericMenu();
        menu.AddItem(new GUIContent("空のエフェクト"),    false, () => ApplyTemplate(KfxTemplates.Empty()));
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("パルス"),            false, () => ApplyTemplate(KfxTemplates.Pulse()));
        menu.AddItem(new GUIContent("衝撃波"),            false, () => ApplyTemplate(KfxTemplates.Shockwave()));
        menu.AddItem(new GUIContent("フラッシュ"),        false, () => ApplyTemplate(KfxTemplates.Flash()));
        menu.AddItem(new GUIContent("フェードイン/アウト"), false, () => ApplyTemplate(KfxTemplates.FadeInOut()));
        menu.AddItem(new GUIContent("パーティクル噴出"),   false, () => ApplyTemplate(KfxTemplates.ParticleBurst()));
        menu.AddItem(new GUIContent("斬撃"),              false, () => ApplyTemplate(KfxTemplates.Slash()));
        menu.ShowAsContext();
    }
    // ...
}

private void ApplyTemplate(KfxDefinition template)
{
    _kfxDef = template;
    _filePath = null;
    _selectedLayerIndex = _kfxDef.Layers.Count > 0 ? 0 : -1;
    _selectedKeyframeIndex = 0;
    _isDirty = true;
    MarkDirty();
}
```

---

## 4. ビジュアルタイムライン

### 現状の問題

```
  t=0.00s  ease:easeOut    ← テキストリスト
  t=0.30s  ease:easeIn        時間関係が把握しにくい
  t=1.50s  ease:linear
```

### 改善後

```
glow         ◆────────◆──────────────────────────◆
ring_burst         ◆────────────────◆
sparks       ▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓
             |    |    |    |    |    |
            0.0  0.3  0.6  0.9  1.2  1.5
                         ↑ 現在位置
```

レイヤーごとに水平バーを描画し、キーフレームを◆で表示。現在の再生位置を縦線で示す。

### 実装詳細

#### 4-1. タイムラインの描画メソッド

```csharp
private void DrawTimeline()
{
    if (_kfxDef == null || _kfxDef.Layers.Count == 0) return;

    float duration = _kfxDef.Duration;
    float timelineWidth = EditorGUIUtility.currentViewWidth - 120; // レイヤー名分を引く
    float rowHeight = 24f;
    float totalHeight = _kfxDef.Layers.Count * rowHeight + 20; // +目盛り

    var timelineRect = GUILayoutUtility.GetRect(
        EditorGUIUtility.currentViewWidth, totalHeight);

    float labelWidth = 100f;
    float barX = timelineRect.x + labelWidth;
    float barWidth = timelineRect.width - labelWidth - 10;

    // 背景
    EditorGUI.DrawRect(timelineRect, new Color(0.18f, 0.18f, 0.18f));

    // 各レイヤーの行
    for (int i = 0; i < _kfxDef.Layers.Count; i++)
    {
        var layer = _kfxDef.Layers[i];
        float rowY = timelineRect.y + i * rowHeight;
        bool isSelected = (i == _selectedLayerIndex);

        // レイヤー名ラベル
        var labelRect = new Rect(timelineRect.x, rowY, labelWidth, rowHeight);
        var labelStyle = isSelected ? EditorStyles.boldLabel : EditorStyles.label;
        GUI.Label(labelRect, layer.Id ?? $"layer_{i}", labelStyle);

        // クリックでレイヤー選択
        if (Event.current.type == EventType.MouseDown && labelRect.Contains(Event.current.mousePosition))
        {
            _selectedLayerIndex = i;
            _selectedKeyframeIndex = -1;
            Event.current.Use();
            Repaint();
        }

        // visible 範囲バー
        float visStart = (layer.Visible != null && layer.Visible.Count >= 1) ? layer.Visible[0] : 0;
        float visEnd = (layer.Visible != null && layer.Visible.Count >= 2) ? layer.Visible[1] : duration;
        float barStartX = barX + (visStart / duration) * barWidth;
        float barEndX = barX + (visEnd / duration) * barWidth;
        var barRect = new Rect(barStartX, rowY + 4, barEndX - barStartX, rowHeight - 8);

        Color barColor = isSelected
            ? new Color(0.3f, 0.5f, 0.8f, 0.5f)
            : new Color(0.3f, 0.3f, 0.4f, 0.5f);
        if (layer.IsEmitter)
            barColor = isSelected
                ? new Color(0.6f, 0.4f, 0.2f, 0.5f)
                : new Color(0.4f, 0.3f, 0.2f, 0.5f);
        EditorGUI.DrawRect(barRect, barColor);

        // キーフレーム◆マーカー（エミッター以外）
        if (!layer.IsEmitter && layer.Keyframes != null)
        {
            foreach (var kf in layer.Keyframes)
            {
                float kfX = barX + (kf.Time / duration) * barWidth;
                float kfY = rowY + rowHeight / 2;
                DrawDiamond(kfX, kfY, 5f, isSelected ? Color.white : Color.gray);
            }
        }
    }

    // 現在位置インジケーター（縦線）
    if (_compiledDef != null && _compiledDef.Fps > 0)
    {
        float currentTime = _currentFrame / (float)_compiledDef.Fps;
        float lineX = barX + (currentTime / duration) * barWidth;
        var lineRect = new Rect(lineX - 1, timelineRect.y,
            2, _kfxDef.Layers.Count * rowHeight);
        EditorGUI.DrawRect(lineRect, new Color(1f, 1f, 1f, 0.8f));
    }

    // 目盛り（下部）
    float scaleY = timelineRect.y + _kfxDef.Layers.Count * rowHeight;
    DrawTimeScale(barX, scaleY, barWidth, duration);

    // タイムライン内クリックでスクラブ
    if (Event.current.type == EventType.MouseDown)
    {
        var scrubRect = new Rect(barX, timelineRect.y,
            barWidth, _kfxDef.Layers.Count * rowHeight);
        if (scrubRect.Contains(Event.current.mousePosition))
        {
            float clickT = (Event.current.mousePosition.x - barX) / barWidth;
            float clickTime = clickT * duration;
            int frame = Mathf.RoundToInt(clickTime * _kfxDef.Fps);
            GoToFrame(Mathf.Clamp(frame, 0,
                (_compiledDef?.Frames.Count ?? 1) - 1));
            Event.current.Use();
        }
    }
}

/// <summary>
/// ◆マーカーを描画
/// </summary>
private void DrawDiamond(float cx, float cy, float size, Color color)
{
    // 4つの三角形で菱形を描く（HandleUtility or GL使用）
    // 簡易実装: 小さな正方形を45度回転 → EditorGUI.DrawRect を重ねて近似
    float s = size * 0.7f;
    var rect = new Rect(cx - s/2, cy - s/2, s, s);
    // GUIUtility.RotateAroundPivot は使えないので、2つの三角形を矩形で近似
    EditorGUI.DrawRect(rect, color);
}

/// <summary>
/// 下部の時刻目盛りを描画
/// </summary>
private void DrawTimeScale(float x, float y, float width, float duration)
{
    // 0.1秒または0.5秒刻みで目盛り
    float step = duration <= 1.0f ? 0.1f : 0.5f;
    var style = EditorStyles.centeredGreyMiniLabel;
    for (float t = 0; t <= duration + 0.001f; t += step)
    {
        float tx = x + (t / duration) * width;
        var tickRect = new Rect(tx - 1, y, 1, 4);
        EditorGUI.DrawRect(tickRect, Color.gray);
        var labelRect = new Rect(tx - 15, y + 4, 30, 14);
        GUI.Label(labelRect, $"{t:F1}", style);
    }
}
```

#### 4-2. OnGUI での配置

タイムラインはプレビューの直下、キーフレームリストの直上に配置する。

```csharp
private void OnGUI()
{
    // ...
    DrawPreviewSection();
    DrawPlaybackControls();    // スクラバー・ボタン
    EditorGUILayout.Space(4);
    DrawTimeline();             // ← 新規追加
    EditorGUILayout.Space(8);
    DrawLayerList();            // レイヤー詳細
    DrawSelectedLayer();
}
```

タイムラインがスクラバーの役割も兼ねるため、既存の `Slider` スクラバーは削除可能（PlaybackControlsのSlider行を除去）。

---

## 5. MinMaxSlider

### 現状の問題

```
[Angle] [__0__] [__360__]   ← 2つの独立したFloatField
```

### 改善後

```
[Angle] 0° [====|==========|====] 360°   ← 範囲を視覚的に表示
            45         270                   MinMaxSliderで操作
```

### 実装詳細

#### 5-1. ヘルパーメソッド

```csharp
/// <summary>
/// MinMaxSlider + 数値表示
/// </summary>
private void DrawRangeSlider(string label, ref float min, ref float max,
    float sliderMin, float sliderMax)
{
    EditorGUILayout.BeginHorizontal();
    EditorGUILayout.LabelField(label, GUILayout.Width(60));
    min = EditorGUILayout.FloatField(min, GUILayout.Width(50));
    EditorGUILayout.MinMaxSlider(ref min, ref max, sliderMin, sliderMax);
    max = EditorGUILayout.FloatField(max, GUILayout.Width(50));
    EditorGUILayout.EndHorizontal();
}
```

#### 5-2. 置換箇所（DrawEmitterProperties内）

| プロパティ | スライダー範囲 |
|-----------|-------------|
| Angle Range | 0〜360 |
| Speed Range | 0〜10 |
| Size Range | 0〜10 |

```csharp
// Angle range
float angMin = ...; float angMax = ...;
DrawRangeSlider("Angle", ref angMin, ref angMax, 0f, 360f);
layer.AngleRange = new List<float> { angMin, angMax };

// Speed range
float spdMin = ...; float spdMax = ...;
DrawRangeSlider("Speed", ref spdMin, ref spdMax, 0f, 10f);
layer.SpeedRange = new List<float> { spdMin, spdMax };

// Size range
float szMin = ...; float szMax = ...;
DrawRangeSlider("Size", ref szMin, ref szMax, 0f, 10f);
layer.SizeRange = new List<float> { szMin, szMax };
```

---

## 6. レイヤーリスト改善

### 現状の問題

```
>  glow        circle    additive     ← 色が分からない
   sparks      emitter   additive        表示/非表示もない
```

### 改善後

```
[目] [●] glow        circle    additive   [▲][▼]
[目] [●] ring_burst  ring      additive   [▲][▼]
[目] [●] sparks      emitter   additive   [▲][▼]
```

### 実装詳細

#### 6-1. レイヤー表示制御用のフラグ

タイムライン上の表示/非表示と、プレビュー上の表示/非表示は分ける。Editorの表示制御はプレビュー目的なので、`_layerVisibility` を Editor 内部状態として保持する（JSONには保存しない）。

```csharp
// フィールド追加
private readonly Dictionary<int, bool> _layerVisibility = new Dictionary<int, bool>();

private bool IsLayerVisible(int index)
{
    return !_layerVisibility.TryGetValue(index, out bool hidden) || !hidden;
}
```

#### 6-2. レイヤーの代表色を取得

```csharp
private Color GetLayerRepresentativeColor(KfxLayer layer)
{
    if (layer.IsEmitter)
    {
        return EffectColorUtility.ParseColor(layer.ColorStart ?? "#FFFFFF");
    }
    if (layer.Keyframes != null && layer.Keyframes.Count > 0)
    {
        var kf = layer.Keyframes[0];
        if (kf.Brush != null)
        {
            if (!string.IsNullOrEmpty(kf.Brush.Color))
                return EffectColorUtility.ParseColor(kf.Brush.Color);
            if (!string.IsNullOrEmpty(kf.Brush.Center))
                return EffectColorUtility.ParseColor(kf.Brush.Center);
            if (!string.IsNullOrEmpty(kf.Brush.Start))
                return EffectColorUtility.ParseColor(kf.Brush.Start);
        }
        if (kf.Pen != null && !string.IsNullOrEmpty(kf.Pen.Color))
            return EffectColorUtility.ParseColor(kf.Pen.Color);
    }
    return Color.white;
}
```

#### 6-3. DrawLayerList の書き替え

```csharp
for (int i = 0; i < _kfxDef.Layers.Count; i++)
{
    var layer = _kfxDef.Layers[i];
    bool selected = i == _selectedLayerIndex;

    EditorGUILayout.BeginHorizontal(selected ? "SelectionRect" : "box");
    {
        // 表示/非表示トグル（目アイコン）
        bool visible = IsLayerVisible(i);
        bool newVisible = GUILayout.Toggle(visible, visible ? "👁" : "ー",
            "Button", GUILayout.Width(24), GUILayout.Height(18));
        if (newVisible != visible)
        {
            _layerVisibility[i] = !newVisible;
            MarkDirty();
        }

        // 代表色ドット
        Color repColor = GetLayerRepresentativeColor(layer);
        var colorRect = GUILayoutUtility.GetRect(14, 14, GUILayout.Width(14));
        EditorGUI.DrawRect(colorRect, repColor);

        // レイヤー名（クリックで選択）
        if (GUILayout.Button(layer.Id ?? $"layer_{i}",
            selected ? EditorStyles.boldLabel : EditorStyles.label,
            GUILayout.Width(100)))
        {
            _selectedLayerIndex = i;
            _selectedKeyframeIndex = -1;
        }

        EditorGUILayout.LabelField(layer.Type ?? "?", GUILayout.Width(70));
        EditorGUILayout.LabelField(layer.Blend ?? "-", GUILayout.Width(60));

        // 順序変更ボタン
        GUI.enabled = i > 0;
        if (GUILayout.Button("▲", GUILayout.Width(22)))
        {
            SwapLayers(i, i - 1);
        }
        GUI.enabled = i < _kfxDef.Layers.Count - 1;
        if (GUILayout.Button("▼", GUILayout.Width(22)))
        {
            SwapLayers(i, i + 1);
        }
        GUI.enabled = true;
    }
    EditorGUILayout.EndHorizontal();
}
```

#### 6-4. レイヤー順序入れ替え

```csharp
private void SwapLayers(int a, int b)
{
    var temp = _kfxDef.Layers[a];
    _kfxDef.Layers[a] = _kfxDef.Layers[b];
    _kfxDef.Layers[b] = temp;

    // 選択状態を追従
    if (_selectedLayerIndex == a) _selectedLayerIndex = b;
    else if (_selectedLayerIndex == b) _selectedLayerIndex = a;

    MarkDirty();
}
```

#### 6-5. レイヤー非表示のプレビュー反映

`Recompile()` 後に非表示レイヤーのシェイプを除去する。ただし、コンパイル結果自体は変えず、レンダリング時にスキップする方がクリーン。

```csharp
// RenderCurrentFrame にフィルタを追加
private void RenderCurrentFrame()
{
    if (_compiledDef == null || _renderer == null || _previewTexture == null) return;
    if (_currentFrame < 0 || _currentFrame >= _compiledDef.Frames.Count) return;

    var frame = _compiledDef.Frames[_currentFrame];

    // レイヤー非表示フィルタが必要な場合は、
    // フレームのシェイプをフィルタしたコピーを作ってレンダリング
    // （実装は Recompile 時にレイヤーindex→シェイプindex のマッピングを保持する方式が最適）

    _renderer.RenderFrame(_previewTexture, frame, _currentFrame);
}
```

実際にはコンパイル時に各シェイプがどのレイヤー由来かのマッピングを保持し、非表示レイヤーのシェイプを除外した一時フレームを生成してレンダリングする。

---

## 実装順序

改善ごとの依存関係と作業量:

```
1. カラーピッカー          依存なし    小（ヘルパー1つ + 置換8箇所）
2. キーフレームUI刷新      依存なし    中（DrawKeyframeDetail 全体書き替え）
3. テンプレート            依存なし    小（テンプレート定義 + NewFileメニュー化）
4. ビジュアルタイムライン  依存なし    中（新規描画コード + スクラブ操作）
5. MinMaxSlider           依存なし    小（ヘルパー1つ + 置換3箇所）
6. レイヤーリスト改善      依存なし    小（DrawLayerList 書き替え + 状態管理）
```

推奨実装順:

1. **カラーピッカー** + **MinMaxSlider** — 小さな変更で効果大、先にやると後の作業が楽
2. **テンプレート** — 独立した追加機能、他に影響しない
3. **キーフレームUI刷新** — 最も構造変更が大きいが、カラーピッカー完了後に着手すると色フィールドの扱いが確定済み
4. **レイヤーリスト改善** — キーフレームUI完了後にまとめて見た目を統一
5. **ビジュアルタイムライン** — 最後に追加、全体のレイアウト確定後が調整しやすい

---

## 変更ファイル一覧

| ファイル | 変更内容 |
|---------|---------|
| `Assets/Script/Effects/Core/EffectColorUtility.cs` | `ColorToHex()` を public メソッドとして追加 |
| `Assets/Script/Effects/Core/KfxCompiler.cs` | `ColorToHex()` を `EffectColorUtility.ColorToHex()` に置換 |
| `Assets/Editor/Effects/KfxEditorWindow.cs` | UI全面改善（全6項目） |

新規ファイルは作成しない。既存の3ファイルの変更のみ。
