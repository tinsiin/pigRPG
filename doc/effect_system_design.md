# エフェクトシステム設計書

## 概要

テキストファイル（JSON）で定義できるプログラマティック・エフェクトシステム。
基本図形（点、線、円など）とPen/Brushによる色指定を組み合わせ、タイムライン制御でパラパラアニメ的なエフェクトを実現する。

### 目的

- AIや人間がテキストファイルを書くだけでエフェクトを作成可能
- スキル発動、パッシブ効果、状態異常/バフ/デバフなど様々な場面で再利用
- エフェクトとSEをセットで管理

### 設計方針

- **パラパラアニメ方式**: フレーム間の補間なし、フレームごとに切り替え
- **軽量設計**: 同時30個程度の再生に耐える
- **正方形キャンバス**: アイコンサイズに合わせてスケール

### 座標系

```
(0,0) ────────→ X+
  │
  │    キャンバス
  │    (canvas × canvas)
  ↓
  Y+
```

- **原点**: 左上が (0, 0)
- **X軸**: 右方向が正
- **Y軸**: 下方向が正
- **角度**: 0度=右（3時方向）、正方向=時計回り
- **単位**: ピクセル

### Texture2D座標変換

エフェクト座標系（左上原点）からTexture2D座標系（左下原点）への変換:

```csharp
// 変換順序: 先に丸めてから反転
int texX = RoundCoord(x);
int texY = (canvas - 1) - RoundCoord(y);

// SetPixel呼び出し
texture.SetPixel(texX, texY, color);
```

| エフェクト座標 | Texture2D座標 (canvas=100) |
|---------------|---------------------------|
| (0, 0) 左上 | (0, 99) |
| (99, 0) 右上 | (99, 99) |
| (0, 99) 左下 | (0, 0) |
| (99, 99) 右下 | (99, 0) |

### 座標の丸め規則

JSONでは座標・サイズをfloatで指定可能だが、描画時はピクセル単位（int）に変換される。

| 項目 | 丸め方法 | 例 |
|------|----------|-----|
| 座標 (x, y) | 四捨五入 (Round) | 10.4 → 10, 10.5 → 11 |
| サイズ (radius, width, height) | 四捨五入 (Round) | 5.4 → 5, 5.5 → 6 |
| 線の太さ (pen.width) | 切り上げ (Ceil)、最小1 | 0.1 → 1, 1.5 → 2 |

```csharp
int RoundCoord(float v) => Mathf.RoundToInt(v);
int RoundSize(float v) => Mathf.Max(1, Mathf.RoundToInt(v));
int CeilWidth(float v) => Mathf.Max(1, Mathf.CeilToInt(v));
```

---

## 全体構成

```
[JSON定義ファイル]
       ↓ パース
[EffectData]
       ↓
[EffectRenderer] → [Texture2D] → [RawImage on BattleIconUI]
       ↓
[EffectAudioPlayer] → [AudioSource (SE)]
```

### 表示先

- `BattleIconUI`（敵・味方共通）のアイコン上に重ねて表示
- 透明キャンバスに描画部分だけが見える
- 味方アイコン: 476x722px（縦長）
- 敵アイコン: 可変サイズ
- エフェクトキャンバス: 正方形で統一し、アイコンに重ねる

### キャンバスとアイコンの配置

```
┌─────────────┐
│   余白(上)   │
│ ┌─────────┐ │
│ │エフェクト│ │  ← 中央揃え（等比スケール）
│ │ (正方形) │ │
│ └─────────┘ │
│   余白(下)   │
└─────────────┘
   アイコン
  (縦長等)
```

**スケーリング方式: 等比で中央合わせ（Uniform + Center）**

| 項目 | 仕様 |
|------|------|
| スケール方法 | アイコンの短辺に合わせて等比スケール |
| 配置 | アイコンの中央に配置 |
| 余白 | 長辺方向に余白が生じる（透明） |
| アスペクト比 | 常に1:1を維持（非等比フィットはしない） |

**計算例:**
- アイコン: 476×722px（縦長）
- 短辺: 476px
- エフェクト表示サイズ: 476×476px
- 配置Y: (722 - 476) / 2 = 123px（上から123pxの位置）

---

## JSON形式

### パーサー

Unity標準のJsonUtilityではなく **Newtonsoft.Json（Json.NET）** を使用する。

理由:
- ネストしたオブジェクト配列のサポート
- null値の適切な処理
- より柔軟なデシリアライズ

※ Unity 2020以降はPackage Managerから `com.unity.nuget.newtonsoft-json` で導入可能

### 基本構造

```json
{
  "name": "fire_burst",
  "version": 1,
  "canvas": 100,
  "fps": 12,
  "se": "fire_burst",
  "frames": [
    {
      "shapes": [
        {
          "type": "circle",
          "x": 50,
          "y": 50,
          "radius": 10,
          "pen": { "color": "#FF4400", "width": 2 },
          "brush": { "color": "#FF880080" }
        }
      ]
    },
    {
      "duration": 0.2,
      "shapes": [
        {
          "type": "circle",
          "x": 50,
          "y": 50,
          "radius": 30,
          "pen": { "color": "#FF4400FF" }
        }
      ]
    },
    {
      "shapes": []
    }
  ]
}
```

### フィールド説明

| フィールド | 型 | 必須 | 説明 |
|-----------|-----|------|------|
| `name` | string | Yes | エフェクト名（識別子） |
| `version` | int | No | フォーマットバージョン（デフォルト: 1） |
| `canvas` | int | Yes | キャンバスサイズ（正方形、ピクセル単位） |
| `fps` | int | Yes | デフォルトフレームレート（1-120、範囲外はクランプ） |
| `se` | string | No | 効果音ファイル名（拡張子なし）、null で無音 |
| `frames` | array | Yes | フレーム配列（1つ以上必須） |

### フレーム

| フィールド | 型 | 必須 | 説明 |
|-----------|-----|------|------|
| `duration` | float | No | このフレームの表示時間（秒）、省略時は `1/fps`、0以下は `1/fps` に補正 |
| `shapes` | array | Yes | 描画する図形の配列、`[]` で空フレーム |

### バリデーション規則

| 項目 | 条件 | 挙動 |
|------|------|------|
| `fps` <= 0 | 無効値 | 12にクランプ（デフォルト） |
| `fps` > 120 | 上限超過 | 120にクランプ |
| `duration` <= 0 | 無効値 | `1/fps` に補正 |
| `frames` が空 | 必須違反 | 読み込みエラー（ログ出力、再生スキップ） |
| `canvas` <= 0 | 無効値 | 読み込みエラー |
| `canvas` > 512 | 上限超過 | 読み込みエラー（座標再スケールは行わない） |

---

## 図形タイプ

### point（点）

```json
{
  "type": "point",
  "x": 50,
  "y": 50,
  "size": 4,
  "pen": { "color": "#FFFFFF" }
}
```

| パラメータ | 型 | 説明 |
|-----------|-----|------|
| `x`, `y` | float | 中心座標 |
| `size` | float | 点のサイズ（直径） |

※ pointはpenの色で描画される（brushは無効）

### line（直線）

```json
{
  "type": "line",
  "x1": 10,
  "y1": 10,
  "x2": 90,
  "y2": 90,
  "pen": { "color": "#FF0000", "width": 2 }
}
```

| パラメータ | 型 | 説明 |
|-----------|-----|------|
| `x1`, `y1` | float | 始点座標 |
| `x2`, `y2` | float | 終点座標 |

### circle（円）

```json
{
  "type": "circle",
  "x": 50,
  "y": 50,
  "radius": 20,
  "pen": { "color": "#00FF00", "width": 1 },
  "brush": { "color": "#00FF0080" }
}
```

| パラメータ | 型 | 説明 |
|-----------|-----|------|
| `x`, `y` | float | 中心座標 |
| `radius` | float | 半径 |

### ellipse（楕円）

```json
{
  "type": "ellipse",
  "x": 50,
  "y": 50,
  "rx": 30,
  "ry": 15,
  "rotation": 45,
  "pen": { "color": "#0000FF", "width": 1 },
  "brush": { "color": "#0000FF80" }
}
```

| パラメータ | 型 | 説明 |
|-----------|-----|------|
| `x`, `y` | float | 中心座標 |
| `rx`, `ry` | float | X軸/Y軸方向の半径 |
| `rotation` | float | 回転角度（度、省略時: 0） |

### arc（弧）

```json
{
  "type": "arc",
  "x": 50,
  "y": 50,
  "radius": 30,
  "startAngle": 0,
  "endAngle": 180,
  "pen": { "color": "#FFFF00", "width": 2 }
}
```

| パラメータ | 型 | 説明 |
|-----------|-----|------|
| `x`, `y` | float | 中心座標 |
| `radius` | float | 半径 |
| `startAngle` | float | 開始角度（度、0=右） |
| `endAngle` | float | 終了角度（度） |

**角度の処理:**
- 角度は時計回りが正方向
- **正規化**: 入力値は `mod 360` で 0〜360 の範囲に正規化（負値や360超も許容）
  - 例: -90° → 270°、450° → 90°
- `startAngle > endAngle` の場合: startAngleからendAngleまで時計回りに描画（360度をまたぐ）
- 例: startAngle=270, endAngle=90 → 270°→360°→0°→90°の弧を描画

### rect（矩形）

```json
{
  "type": "rect",
  "x": 20,
  "y": 20,
  "width": 60,
  "height": 40,
  "rotation": 0,
  "pen": { "color": "#FF00FF", "width": 1 },
  "brush": { "color": "#FF00FF40" }
}
```

| パラメータ | 型 | 説明 |
|-----------|-----|------|
| `x`, `y` | float | 矩形の中心座標 |
| `width`, `height` | float | 幅と高さ |
| `rotation` | float | 回転角度（度、中心を軸に回転、省略時: 0） |

※ x,yは矩形の中心。rotation=0の場合、左上は (x-width/2, y-height/2)

### polygon（多角形）

```json
{
  "type": "polygon",
  "points": [
    {"x": 25, "y": 10},
    {"x": 50, "y": 90},
    {"x": 75, "y": 10}
  ],
  "pen": { "color": "#00FFFF", "width": 1 },
  "brush": { "color": "#00FFFF60" }
}
```

| パラメータ | 型 | 説明 |
|-----------|-----|------|
| `points` | array | 頂点座標の配列 `[{x,y}, ...]` |

※ 配列形式 `[[x,y], ...]` は非対応（Json.NETでのパース簡易化のため）

### bezier（ベジェ曲線）

```json
{
  "type": "bezier",
  "points": [
    {"x": 10, "y": 50},
    {"x": 30, "y": 10},
    {"x": 70, "y": 90},
    {"x": 90, "y": 50}
  ],
  "pen": { "color": "#FFA500", "width": 2 }
}
```

| パラメータ | 型 | 説明 |
|-----------|-----|------|
| `points` | array | 制御点の配列 `[{x,y}, ...]`（3点: 2次ベジェ、4点: 3次ベジェ） |

※ 配列形式 `[[x,y], ...]` は非対応

---

## Pen / Brush

Delphi/GDI的な描画モデルを採用。**penが基本**で図形の輪郭・線を描き、**brushはpenで描かれた図形の内部**を塗りつぶす。

### Pen（線・輪郭）- 基本

```json
"pen": {
  "color": "#RRGGBBAA",
  "width": 2
}
```

| フィールド | 型 | 説明 |
|-----------|-----|------|
| `color` | string | 色（`#RRGGBB` または `#RRGGBBAA`） |
| `width` | int | 線の太さ（ピクセル、1以上、**省略時: 1**） |

- 全ての図形描画の基本
- `pen: null` または省略で輪郭線なし（brushのみで塗りつぶし）

### Brush（塗りつぶし）- 内部

```json
"brush": {
  "color": "#RRGGBBAA"
}
```

| フィールド | 型 | 説明 |
|-----------|-----|------|
| `color` | string | 色（`#RRGGBB` または `#RRGGBBAA`） |

- penで描かれた図形の内部を塗りつぶす
- `brush: null` または省略で塗りなし（輪郭のみ）

### 図形ごとのPen/Brush対応

| 図形 | pen | brush | 説明 |
|------|-----|-------|------|
| `point` | ○ | × | penの色で点を描画 |
| `line` | ○ | × | penの色・太さで線を描画 |
| `arc` | ○ | × | penの色・太さで弧を描画 |
| `bezier` | ○ | × | penの色・太さで曲線を描画 |
| `circle` | ○ | ○ | pen=輪郭、brush=内部 |
| `ellipse` | ○ | ○ | pen=輪郭、brush=内部 |
| `rect` | ○ | ○ | pen=輪郭、brush=内部 |
| `polygon` | ○ | ○ | pen=輪郭、brush=内部 |

### 色フォーマット

- `#RRGGBB`: 不透明色（例: `#FF0000` = 赤）
- `#RRGGBBAA`: 透明度付き（例: `#FF000080` = 半透明赤）
- AA: 00=完全透明、FF=完全不透明

---

## 再生モード

| モード | 用途 | 説明 |
|--------|------|------|
| **OneShot** | スキル発動など | 1回再生して終了 |
| **Loop** | パッシブ効果など | 最終フレーム後に最初に戻り繰り返し |

### 使用例

```csharp
// スキル発動時（1回再生）
EffectManager.Play("fire_burst", target.BattleIcon, loop: false);

// パッシブ効果（ループ再生）
EffectManager.Play("passive_glow", target.BattleIcon, loop: true);

// 停止
EffectManager.Stop(target.BattleIcon, "passive_glow");
```

---

## クラス構成

```
Assets/Script/Effects/
├── Core/
│   ├── EffectDefinition.cs      # JSONデシリアライズ用データクラス
│   ├── EffectData.cs            # パース後の内部データ
│   ├── EffectFrame.cs           # フレームデータ
│   └── EffectShape.cs           # 図形データ
├── Rendering/
│   ├── EffectRenderer.cs        # Texture2D描画エンジン
│   └── ShapeDrawer.cs           # 各図形の描画実装
├── Playback/
│   ├── EffectPlayer.cs          # 再生制御（タイムライン進行）
│   └── EffectAudioPlayer.cs     # SE再生
├── Integration/
│   ├── EffectLayer.cs           # BattleIconUIに追加するコンポーネント
│   └── EffectManager.cs         # 読み込み・キャッシュ・全体管理
└── EffectConstants.cs           # 定数定義
```

### 主要クラス説明

#### EffectDefinition
JSONからデシリアライズされるデータクラス。Newtonsoft.Json（Json.NET）でデシリアライズ。

#### EffectRenderer
Texture2Dに図形を描画するエンジン。各図形タイプごとの描画ロジックを持つ。

#### EffectPlayer
MonoBehaviourとして動作し、タイムラインに沿ってフレームを進行。
ループ制御、再生/停止を管理。

#### EffectLayer
BattleIconUIに追加されるコンポーネント。
RawImageを持ち、EffectPlayerからTexture2Dを受け取って表示。

**EffectLayerとEffectPlayerの関係:**
```
BattleIconUI
└── EffectLayer (1つ)
    ├── EffectPlayer #1 (パッシブA: ループ再生中)
    ├── EffectPlayer #2 (パッシブB: ループ再生中)
    └── EffectPlayer #3 (スキル: 1回再生中)
```

- 1つのEffectLayerに複数のEffectPlayerが存在可能
- 各EffectPlayerは独自のTexture2Dを持つ
- EffectLayerは複数のRawImageを子として持ち、重ねて表示

#### EffectManager
シングルトンまたは静的クラス。
- エフェクト定義の読み込みとキャッシュ
- 再生リクエストの受付
- アクティブなエフェクトの管理

### SE再生

- **タイミング**: エフェクト再生開始時に1回再生
- **ループ時**: ループ再生でもSEは最初の1回のみ（毎ループ再生しない）
- **SE長 > エフェクト長**: SEは最後まで再生される（途中で切らない）
- **SE長 < エフェクト長**: エフェクトは継続、SEは終了

---

## 描画方式

### 採用方式: Texture2D + SetPixels（CPU描画）

```csharp
// 基本的な描画フロー
Texture2D texture = new Texture2D(canvasSize, canvasSize, TextureFormat.RGBA32, false);
texture.filterMode = FilterMode.Point;  // ピクセルアート的な表現

// 透明でクリア
Color[] clearColors = new Color[canvasSize * canvasSize];
for (int i = 0; i < clearColors.Length; i++)
    clearColors[i] = Color.clear;
texture.SetPixels(clearColors);

// 図形を描画
ShapeDrawer.DrawCircle(texture, cx, cy, radius, penColor, penWidth);
ShapeDrawer.FillCircle(texture, cx, cy, radius, brushColor);

// GPUに転送
texture.Apply();

// RawImageに表示
rawImage.texture = texture;
```

### 方式比較

| 方式 | メリット | デメリット | 採用 |
|------|----------|------------|------|
| **Texture2D + SetPixels** | シンプル、座標系が直感的 | 描画アルゴリズム自前実装 | **採用** |
| Graphic継承 + Mesh | GPU描画で高速 | 実装複雑、塗りつぶしが困難 | - |
| GL API | 線/点がシンプル | uGUI統合困難 | - |
| RenderTexture + Shader | GPU描画 | シェーダー知識必要 | - |

**採用理由:**
- パラパラアニメなのでフレーム切り替え時のみ描画（毎フレーム更新不要）
- AIがエフェクトを作る前提で、ピクセル座標系が直感的
- 実装がシンプルでデバッグしやすい
- 30個同時でも負荷が分散される

### クリッピング

キャンバス範囲外への描画はクリップ（無視）される。

```
有効範囲: 0 <= x < canvas, 0 <= y < canvas
```

| ケース | 挙動 |
|--------|------|
| 負の座標 | その部分はクリップ、範囲内部分のみ描画 |
| キャンバス外にはみ出す図形 | 範囲内部分のみ描画 |
| 完全にキャンバス外 | 何も描画しない（エラーではない） |

```csharp
void SetPixelSafe(Texture2D tex, int x, int y, Color color)
{
    if (x >= 0 && x < tex.width && y >= 0 && y < tex.height)
        tex.SetPixel(x, y, color);
    // 範囲外は単に無視
}
```

---

## 描画アルゴリズム

### ShapeDrawer API設計

座標・サイズはfloatで受け取り、内部で丸め規則に従ってintに変換する。

```csharp
public static class ShapeDrawer
{
    // === 点 ===
    public static void DrawPoint(Texture2D tex, float x, float y, float size, Color color);

    // === 線 ===
    // Bresenhamアルゴリズム + 太さ対応
    public static void DrawLine(Texture2D tex, float x1, float y1, float x2, float y2, Color color, float width);

    // === 円 ===
    // Midpoint Circle Algorithm
    public static void DrawCircle(Texture2D tex, float cx, float cy, float radius, Color color, float width);
    public static void FillCircle(Texture2D tex, float cx, float cy, float radius, Color color);

    // === 楕円 ===
    // Midpoint Ellipse Algorithm + 回転対応
    public static void DrawEllipse(Texture2D tex, float cx, float cy, float rx, float ry, float rotation, Color color, float width);
    public static void FillEllipse(Texture2D tex, float cx, float cy, float rx, float ry, float rotation, Color color);

    // === 弧 ===
    public static void DrawArc(Texture2D tex, float cx, float cy, float radius, float startAngle, float endAngle, Color color, float width);

    // === 矩形 ===
    public static void DrawRect(Texture2D tex, float x, float y, float w, float h, float rotation, Color color, float width);
    public static void FillRect(Texture2D tex, float x, float y, float w, float h, float rotation, Color color);

    // === 多角形 ===
    // 辺は線分で描画、塗りはスキャンライン法（偶奇規則）
    public static void DrawPolygon(Texture2D tex, Vector2[] points, Color color, float width);
    public static void FillPolygon(Texture2D tex, Vector2[] points, Color color);

    // === ベジェ曲線 ===
    // De Casteljauアルゴリズム
    public static void DrawBezier(Texture2D tex, Vector2[] controlPoints, Color color, float width);

    // === 内部ヘルパー ===
    private static int RoundCoord(float v) => Mathf.RoundToInt(v);
    private static int RoundSize(float v) => Mathf.Max(1, Mathf.RoundToInt(v));
    private static int CeilWidth(float v) => Mathf.Max(1, Mathf.CeilToInt(v));
}
```

### 主要アルゴリズム

#### 線描画（Bresenham + 太さ）

```
1. Bresenhamアルゴリズムで線分上のピクセルを列挙
2. 各ピクセルを中心に、width分の円（または正方形）を描画
3. アンチエイリアスなし（パラパラアニメ向け）
```

#### 円描画（Midpoint Circle）

```
1. 8分円の対称性を利用
2. 輪郭: 各点にwidth分の太さを適用
3. 塗り: 水平スキャンラインで内部を塗りつぶし
```

#### 楕円描画（Midpoint Ellipse + 回転）

```
1. 回転なし: 4分円の対称性を利用したMidpoint Ellipse
2. 回転あり: 各点を回転行列で変換
3. 塗り: スキャンライン法
```

#### 多角形塗りつぶし（スキャンライン法 + 偶奇規則）

```
1. Y座標でソートしたエッジテーブル作成
2. 各スキャンラインで交点を計算
3. 偶奇規則（Even-Odd Rule）で内外判定
4. 交点間を塗りつぶし
```

**塗りつぶしルール: 偶奇規則（Even-Odd Rule）**
- ある点から無限遠へ線を引き、辺との交差回数が奇数なら内部
- 自己交差する多角形では交差部分が抜ける（塗られない）
- 非零規則（Non-Zero）は採用しない（実装簡易化のため）

#### ベジェ曲線（De Casteljau）

```
1. 制御点数で次数を判定（3点=2次、4点=3次）
2. パラメータtを0→1で分割（セグメント数は曲線長に応じて調整）
3. 各セグメントをDrawLineで描画
```

---

## Pen / Brush 実装

### 描画順序

```
1フレームの描画順序:
1. キャンバスを透明(Color.clear)でクリア
2. shapes配列を順番に処理（後の図形が上に重なる）
3. 各図形について:
   a. brushがあれば先に塗りつぶし
   b. penがあれば輪郭を描画（塗りの上に重なる）
```

### Pen（輪郭線）

```csharp
public struct PenStyle
{
    public Color Color;     // 線の色（RGBA）
    public float Width;     // 線の太さ（描画時にCeilで丸め、最小1）
}
```

- `width=1`: 1ピクセル幅の線
- `width=2以上`: 線の各点を中心に円を描くことで太さを表現
- `pen=null`: 輪郭線なし

### Brush（塗りつぶし）

```csharp
public struct BrushStyle
{
    public Color Color;     // 塗りの色（RGBA）
}
```

- 図形の内部をBrush色で塗りつぶす
- アルファブレンディング: 既存ピクセルとSrcOver合成
- `brush=null`: 塗りなし（輪郭のみ）

### 透明度（アルファ）の扱い

```csharp
// アルファブレンディング（標準的なSrcOver）
Color BlendPixel(Color dst, Color src)
{
    float srcA = src.a;
    float dstA = dst.a * (1 - srcA);
    float outA = srcA + dstA;

    if (outA < 0.001f) return Color.clear;

    return new Color(
        (src.r * srcA + dst.r * dstA) / outA,
        (src.g * srcA + dst.g * dstA) / outA,
        (src.b * srcA + dst.b * dstA) / outA,
        outA
    );
}
```

### 色パース

```csharp
// "#RRGGBB" または "#RRGGBBAA" をパース
Color ParseColor(string hex)
{
    // #を除去
    hex = hex.TrimStart('#');

    byte r = Convert.ToByte(hex.Substring(0, 2), 16);
    byte g = Convert.ToByte(hex.Substring(2, 2), 16);
    byte b = Convert.ToByte(hex.Substring(4, 2), 16);
    byte a = hex.Length >= 8
        ? Convert.ToByte(hex.Substring(6, 2), 16)
        : (byte)255;

    return new Color32(r, g, b, a);
}
```

---

## パフォーマンス考慮

### 描画タイミング

- フレーム切り替え時のみ `texture.Apply()` を呼ぶ
- 同一フレーム内で複数図形を描画後、1回だけApply

### メモリ管理

```csharp
// Texture2Dの再利用
// 毎フレームnewせず、既存テクスチャをクリアして再利用
texture.SetPixels(clearColors);

// エフェクト終了時にTexture2Dを破棄
UnityEngine.Object.Destroy(texture);
```

### 事前レンダリングキャッシュ（オプション）

```csharp
// 頻繁に使うエフェクトは全フレームを事前生成
public class PrerenderedEffect
{
    public Texture2D[] Frames;  // 事前レンダリング済みフレーム
    public float[] Durations;   // 各フレームの表示時間
}
```

- メリット: 再生時の描画負荷ゼロ
- デメリット: メモリ使用量増加
- 用途: ループ再生するパッシブエフェクト等

### 同時再生の負荷分散

```
30エフェクト同時の場合:
- 各エフェクトのフレーム切り替えタイミングはバラバラ
- 例: 12FPSエフェクト → 83ms間隔で1回だけ描画
- 実際のUnityフレーム（60FPS）では、1フレームあたり平均1-2エフェクトの更新
```

---

## ファイル配置

### エフェクト定義ファイル

```
Assets/
└── Resources/
    └── Effects/
        ├── skill_fire.json
        ├── skill_ice.json
        ├── passive_guard.json
        └── state_poison.json
```

### 効果音ファイル

```
Assets/
└── Resources/
    └── Audio/
        └── SE/
            └── Effects/
                ├── skill_fire.wav
                ├── skill_ice.wav
                └── ...
```

---

## サンプルエフェクト

### 炎の爆発（fire_burst.json）

```json
{
  "name": "fire_burst",
  "version": 1,
  "canvas": 100,
  "fps": 15,
  "se": "fire_burst",
  "frames": [
    {
      "shapes": [
        {
          "type": "circle",
          "x": 50, "y": 50,
          "radius": 5,
          "brush": { "color": "#FFFF00" }
        }
      ]
    },
    {
      "shapes": [
        {
          "type": "circle",
          "x": 50, "y": 50,
          "radius": 15,
          "brush": { "color": "#FFA500" }
        },
        {
          "type": "circle",
          "x": 50, "y": 50,
          "radius": 8,
          "brush": { "color": "#FFFF00" }
        }
      ]
    },
    {
      "shapes": [
        {
          "type": "circle",
          "x": 50, "y": 50,
          "radius": 30,
          "pen": { "color": "#FF4500", "width": 3 }
        },
        {
          "type": "circle",
          "x": 50, "y": 50,
          "radius": 20,
          "brush": { "color": "#FFA50080" }
        }
      ]
    },
    {
      "shapes": [
        {
          "type": "circle",
          "x": 50, "y": 50,
          "radius": 40,
          "pen": { "color": "#FF450080", "width": 2 }
        }
      ]
    },
    {
      "shapes": []
    }
  ]
}
```

### パッシブ輝き（passive_glow.json）

```json
{
  "name": "passive_glow",
  "version": 1,
  "canvas": 100,
  "fps": 8,
  "se": null,
  "frames": [
    {
      "shapes": [
        {
          "type": "circle",
          "x": 50, "y": 50,
          "radius": 45,
          "pen": { "color": "#00FFFF40", "width": 2 }
        }
      ]
    },
    {
      "shapes": [
        {
          "type": "circle",
          "x": 50, "y": 50,
          "radius": 47,
          "pen": { "color": "#00FFFF60", "width": 3 }
        }
      ]
    },
    {
      "shapes": [
        {
          "type": "circle",
          "x": 50, "y": 50,
          "radius": 48,
          "pen": { "color": "#00FFFF80", "width": 4 }
        }
      ]
    },
    {
      "shapes": [
        {
          "type": "circle",
          "x": 50, "y": 50,
          "radius": 47,
          "pen": { "color": "#00FFFF60", "width": 3 }
        }
      ]
    }
  ]
}
```

---

## 関連ファイル

| ファイル | 役割 |
|---------|------|
| `Assets/Script/EYEAREA_UI/BattleIconUI.cs` | エフェクト表示先のUIコンポーネント |
| `Assets/Script/BaseStates/BaseStates.cs` | `BattleIcon`プロパティでUIを参照 |
| `Assets/Script/Battle/UI/BattleUIBridge.cs` | UI中央仲介 |
| `doc/味方バトルアイコン仕様書.md` | アイコンサイズ規格 |

---

## スコープ定義

### 今回実装するもの（機能要件）

1. **エフェクト定義システム**
   - JSON形式でのエフェクト定義
   - 図形、色、タイムラインの記述

2. **描画・再生システム**
   - Texture2Dへの図形描画
   - フレーム進行とループ制御
   - SE再生

3. **BattleIconUI連携**
   - EffectLayerコンポーネント
   - アイコン上へのエフェクト重ね表示
   - 複数エフェクトの同時再生

4. **管理システム**
   - エフェクト定義の読み込み・キャッシュ
   - 再生/停止API

### 今回実装しないもの（非機能要件・将来課題）

以下は本システムの設計段階では考慮するが、実装は将来の課題とする。

#### 1. スキルとエフェクトの紐付け

```
[将来の実装イメージ]

BaseSkill (または SkillSO)
├── EffectOnCast: string      // 発動時エフェクト名
├── EffectOnHit: string       // 命中時エフェクト名
└── EffectOnTarget: string    // 対象者エフェクト名
```

- スキルデータ（ScriptableObject等）にエフェクト名を持たせる
- どの形式で紐付けるかは未定（SO、JSON、コード直書き等）

#### 2. BattleManager / SkillExecutorとの連携

```
[将来の実装イメージ]

// スキル実行時
void ExecuteSkill(BaseSkill skill, BaseStates caster, BaseStates target)
{
    // エフェクト再生（将来実装）
    EffectManager.Play(skill.EffectOnCast, caster.BattleIcon);
    EffectManager.Play(skill.EffectOnHit, target.BattleIcon);
}
```

- BattleManager内のスキル実行フローからエフェクト再生を呼び出す
- タイミング制御（ダメージ表示との同期等）

#### 3. パッシブ効果の自動再生

```
[将来の実装イメージ]

// パッシブ取得時
void OnPassiveAcquired(PassiveSkill passive, BaseStates owner)
{
    if (passive.HasEffect)
    {
        EffectManager.Play(passive.EffectName, owner.BattleIcon, loop: true);
    }
}
```

- パッシブスキル保持中の常時エフェクト表示
- バフ/デバフ/状態異常のエフェクト表示

#### 4. 状態表示との統合

- フラットロゼ等の特殊状態のエフェクト
- BaseStatesの状態変化イベントとの連携

#### 5. エフェクト定義の格納形式

現時点では `Resources/Effects/*.json` を想定しているが、以下も検討対象：

| 方式 | メリット | デメリット |
|------|----------|------------|
| Resources/JSON | シンプル、AIが直接編集可能 | ビルド時に含まれる |
| ScriptableObject | Inspectorで編集可能 | AI編集が困難 |
| StreamingAssets | ビルド後も差し替え可能 | パス管理が必要 |
| Addressables | 非同期ロード、DLC対応 | 設定が複雑 |

#### 6. エフェクト終了通知

OneShotエフェクト終了時に呼び出し元へ通知する仕組み。

```
[将来の実装イメージ]

// コールバック方式
EffectManager.Play("fire_burst", target.BattleIcon, onComplete: () => {
    // エフェクト終了後の処理
});

// UniTask方式
await EffectManager.PlayAsync("fire_burst", target.BattleIcon);
// エフェクト終了後の処理
```

- スキル実行側がエフェクト終了を待機したい場合に必要
- ダメージ表示とのタイミング同期に利用

---

## エラーハンドリング

### 基本方針

| エラー | 挙動 |
|--------|------|
| 無効なJSON | ログ出力、エフェクト再生をスキップ |
| 存在しないエフェクト名 | ログ出力、何も再生しない |
| SE読み込み失敗 | ログ出力、エフェクトは再生（無音） |
| 無効な図形パラメータ | 該当図形をスキップ、他は描画 |

### 無効な図形パラメータの定義

**判定タイミング: 丸め後の値で判定する**

| 図形 | 無効条件（該当時はスキップ） |
|------|---------------------------|
| point | `RoundSize(size)` <= 0 |
| line | 丸め後の始点と終点が同一座標 |
| circle | `RoundSize(radius)` <= 0 |
| ellipse | `RoundSize(rx)` <= 0 または `RoundSize(ry)` <= 0 |
| arc | `RoundSize(radius)` <= 0 |
| rect | `RoundSize(width)` <= 0 または `RoundSize(height)` <= 0 |
| polygon | `points` が2点以下 |
| bezier | `points` が2点以下 |
| 共通 | `pen` と `brush` が両方null/省略 |

### ログレベル

- **Error**: JSON解析失敗、必須フィールド欠落
- **Warning**: SEファイル未発見、無効な図形パラメータ
- **Info**: エフェクト読み込み成功（デバッグ時のみ）

---

## 今後の拡張案

- グラデーション対応
- パーティクル的表現（複数インスタンス生成）
- シェーダーエフェクト（グロー、ブラーなど）
- エフェクトエディタ（GUI）
