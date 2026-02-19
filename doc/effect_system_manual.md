# エフェクトシステム仕様書

## 1. 概要

pigRPGのエフェクトシステムは、バトル中のキャラクターアイコン上やフィールド全体にプログラマティックな視覚効果を描画する。
エフェクトの制作方法として**2つの形式**が並存し、どちらも同じランタイムで再生される。

```
┌──────────────────────────────────────────────────────────────┐
│                     制作フロー                                │
│                                                              │
│  【A】フレームベース形式        【B】KFX（キーフレーム）形式    │
│  ・全フレームを直接記述          ・キーフレーム＋自動補間        │
│  ・AI生成 / 音声同期向き         ・人間手動制作 / GUIエディタ   │
│  ・1フレーム単位の精密制御        ・少ないデータで滑らかな動き   │
│                                                              │
│         ↓                              ↓                     │
│    そのまま読込              KfxCompiler でコンパイル          │
│         ↓                              ↓                     │
│         └────────── EffectDefinition ──────────┘             │
│                          ↓                                   │
│                    EffectPlayer                               │
│                          ↓                                   │
│                    EffectRenderer                             │
│                          ↓                                   │
│                     ShapeDrawer                               │
│                          ↓                                   │
│                   Texture2D → 画面                            │
└──────────────────────────────────────────────────────────────┘
```

両形式とも `Assets/Resources/Effects/*.json` に配置され、ロード時に自動判別される。

---

## 2. ファイル配置

| リソース | パス | 備考 |
|---------|------|------|
| エフェクトJSON | `Assets/Resources/Effects/{名前}.json` | 拡張子なしの名前でコードから参照 |
| 効果音 | `Assets/Resources/Audio/{名前}.wav` | 16bit PCM WAV必須 |

---

## 3. 形式の自動判別

`KfxCompiler.LoadFromJson()` がJSONの内容を見て自動判別する。

| 条件 | 判定結果 |
|------|----------|
| `"format": "kfx"` がある | KFX形式 |
| `"layers"` キーがあり `"frames"` キーがない | KFX形式 |
| 上記以外 | フレームベース形式 |

---

## 4. 共通仕様

### 4.1 座標系

```
(0,0) ────────→ X+
  │
  │    キャンバス
  │    (canvas × canvas)
  ↓
  Y+
```

- **原点**: 左上 (0, 0)
- **X軸**: 右方向が正
- **Y軸**: 下方向が正
- **角度**: 0° = 右（3時方向）、時計回りが正
- **キャンバス**: 通常 100×100（1〜512の範囲で指定可能）

### 4.2 色フォーマット

| 形式 | 例 | 説明 |
|------|-----|------|
| `#RRGGBB` | `#FF0000` | 不透明色（赤） |
| `#RRGGBBAA` | `#FF000080` | 半透明（AA: 00=透明、FF=不透明） |

### 4.3 ブレンドモード

シェイプ単位で指定可能。

| 値 | 説明 | 用途 |
|----|------|------|
| `"normal"` | 通常のアルファブレンド（デフォルト） | ベースの形状 |
| `"additive"` | RGB値を加算（重なるほど明るい） | 光、火花、グロー |

```json
{ "type": "circle", "x": 50, "y": 50, "radius": 20,
  "brush": { "color": "#FFFFFF80" }, "blend": "additive" }
```

### 4.4 シェイプ一覧

| タイプ | 必須パラメータ | ペン | ブラシ | 用途 |
|--------|---------------|------|--------|------|
| `point` | x, y, size | o | x | 点 |
| `line` | x1, y1, x2, y2 | o | x | 直線 |
| `circle` | x, y, radius | o | o | 円 |
| `ellipse` | x, y, rx, ry, rotation | o | o | 楕円 |
| `arc` | x, y, radius, startAngle, endAngle | o | x | 円弧 |
| `rect` | x, y (中心), width, height, rotation | o | o | 矩形 |
| `polygon` | points: [{x,y}, ...] (3点以上) | o | o | 多角形 |
| `bezier` | points: [{x,y}, ...] (3-4点) | o | x | ベジェ曲線 |
| `tapered_line` | x1, y1, x2, y2, width_start, width_end | o | x | テーパーライン |
| `ring` | x, y, radius (外径), inner_radius | o | o | リング |
| `emitter` | x, y, count + パーティクルパラメータ | x | x | パーティクルエミッタ |

### 4.5 ペン（アウトライン）

```json
"pen": { "color": "#FFFFFF", "width": 2 }
```

| フィールド | 説明 |
|-----------|------|
| `color` | 色（`#RRGGBB` または `#RRGGBBAA`） |
| `width` | 線の太さ（省略時: 1） |

### 4.6 ブラシ（塗りつぶし）

**単色塗り:**
```json
"brush": { "color": "#FF000080" }
```

**放射グラデーション（中心→端）:**
```json
"brush": { "type": "radial", "center": "#FFFFFFCC", "edge": "#FF000000" }
```

**線形グラデーション（角度方向）:**
```json
"brush": { "type": "linear", "start": "#FFFFFF", "end": "#00000000", "angle": 90 }
```

| フィールド | 説明 |
|-----------|------|
| `color` | 単色の色 |
| `type` | グラデーションタイプ（`"radial"` / `"linear"`、省略時は単色） |
| `center`, `edge` | 放射グラデーション用（中心色、端色） |
| `start`, `end` | 線形グラデーション用（開始色、終了色） |
| `angle` | 線形グラデーションの角度（度） |

描画順序: brush（塗り）→ pen（輪郭）の順で重ねて描画。

### 4.7 パーティクルエミッタ

決定的な乱数シード（`seed`）によるパーティクル物理シミュレーション。

| パラメータ | 説明 |
|-----------|------|
| `x`, `y` | 発生位置 |
| `count` | パーティクル数 |
| `angle_range` | 射出角度の範囲 [min, max]（度） |
| `speed_range` | 速度の範囲 [min, max] |
| `gravity` | Y軸加速度（正=下） |
| `drag` | 速度減衰率（0〜1、1=減衰なし） |
| `lifetime` | 寿命（**フレームベース: フレーム数** / **KFX: 秒**） |
| `size_range` | サイズの範囲 [min, max] |
| `color_start` | 開始色 |
| `color_end` | 終了色（寿命にわたって補間） |
| `seed` | 乱数シード（同じ値なら同じ結果） |
| `blend` | ブレンドモード |

emitterはpen/brush不要（color_start/color_endで色を指定）。

### 4.8 SE（効果音）

- JSONの `"se"` フィールドにファイル名（拡張子なし）を指定
- `Assets/Resources/Audio/{se名}.wav` に配置
- エフェクト再生開始時に1回だけ再生（ループでも1回）
- WAVファイルは16bit PCM形式が必須（非標準形式はFFmpegで変換が必要）

### 4.9 配置モード（target）

エフェクトは2つの配置モードを持つ。ルートの `"target"` フィールドで指定する。

| 値 | 説明 | デフォルト |
|----|------|-----------|
| `"icon"` | キャラクターアイコン（BattleIconUI）相対で表示 | o |
| `"field"` | バトルフィールド（ViewportArea）全体に表示 |  |

**Inspector でのフィルタリング**: `[EffectName("icon")]` / `[EffectName("field")]` 属性を使うと、ドロップダウンに対応する target のエフェクトのみ表示される。スキルの3スロット（§8.5参照）で使用。

```json
{
  "name": "slash_effect",
  "target": "icon",
  "icon_rect": { "x": 10, "y": 10, "width": 80, "height": 80 },
  ...
}
```

```json
{
  "name": "earthquake",
  "target": "field",
  "field_rect": { "x": 0, "y": 20, "width": 100, "height": 60 },
  ...
}
```

### 4.10 配置矩形（icon_rect / field_rect）

キャンバス座標系で「参照領域」を定義する矩形。エフェクトのキャンバス内のどの領域が表示先にマッピングされるかを決定する。

```
┌───────────────── キャンバス (100×100) ─────────────────┐
│                                                         │
│    ┌─── icon_rect / field_rect ───┐                    │
│    │   この領域がアイコン/VPに     │                    │
│    │   フィットするようスケール    │                    │
│    └──────────────────────────────┘                    │
│          はみ出し部分も描画される                        │
└─────────────────────────────────────────────────────────┘
```

#### icon_rect（target="icon" 時）

キャンバス内のどの領域が実際のアイコンに対応するかを定義する。

| フィールド | 型 | 説明 |
|-----------|-----|------|
| `x` | float | 矩形左上のX座標（キャンバス座標） |
| `y` | float | 矩形左上のY座標（キャンバス座標） |
| `width` | float | 矩形の幅 |
| `height` | float | 矩形の高さ |

- 省略時: キャンバス全体がアイコン領域として扱われる（従来動作: アイコン短辺にフィット）
- 矩形をキャンバスより小さくすると、エフェクトがアイコンからはみ出して表示される
- 矩形をキャンバスより大きくすると、エフェクトがアイコンより小さく表示される

#### field_rect（target="field" 時）

キャンバス内のどの領域がビューポート（1080×1041.4）に対応するかを定義する。

- 省略時: キャンバス全体がビューポートにストレッチされる（従来動作）
- icon_rect と同じ数式で配置計算される

### 4.11 配置の計算式

icon_rect / field_rect ともに同じ計算式でスケーリングされる:

```
refW, refH = 表示先の実サイズ（アイコンまたはビューポート）
rectW, rectH = 参照矩形のサイズ

scale = min(refW / rectW, refH / rectH)
displaySize = canvas × scale

offsetX = (canvas/2 - rectCenterX) × scale
offsetY = (rectCenterY - canvas/2) × scale  // Y軸反転
```

---

## 5. フレームベース形式（形式A）

### 5.1 用途

- **AI（Claude）によるエフェクト自動生成**
- **音声波形に同期した精密なフレーム制御**
- audio-envelope MCPで音声解析 → 振幅・スペクトルデータを元にフレームごとの形状を決定

### 5.2 構造

```json
{
  "name": "effect_name",
  "canvas": 100,
  "fps": 30,
  "se": "sound_name",
  "target": "icon",
  "icon_rect": { "x": 10, "y": 10, "width": 80, "height": 80 },
  "frames": [
    {
      "shapes": [
        {
          "type": "circle",
          "x": 50, "y": 50, "radius": 20,
          "pen": { "color": "#FFFFFF", "width": 2 },
          "brush": { "color": "#FFFFFF80" },
          "blend": "additive"
        }
      ]
    },
    {
      "shapes": [ ... ]
    }
  ]
}
```

### 5.3 ルートプロパティ

| プロパティ | 型 | デフォルト | 説明 |
|-----------|-----|-----------|------|
| `name` | string | (必須) | エフェクト名 |
| `canvas` | int | 100 | キャンバスサイズ（1〜512） |
| `fps` | int | 12 | フレームレート（1〜120） |
| `se` | string | null | 効果音ファイル名（拡張子なし） |
| `target` | string | `"icon"` | 配置モード（§4.9参照） |
| `icon_rect` | object | null | アイコン参照矩形（§4.10参照） |
| `field_rect` | object | null | フィールド参照矩形（§4.10参照） |
| `frames` | array | (必須) | フレーム配列 |

### 5.4 フレーム構造

```json
{
  "duration": -1,
  "shapes": [ ... ]
}
```

- `duration`: フレーム表示時間（秒）。`-1` で `1/fps` 秒（デフォルト）
- `shapes`: そのフレームに描画するシェイプの配列。`[]`で空フレーム

### 5.5 特徴

- 全フレームの全シェイプを明示的に記述する
- 1フレーム単位で形状・色・サイズを完全制御
- 音声の振幅カーブに直接マッピング可能
- データ量は多いが、動きの自由度が最も高い

---

## 6. KFX形式（形式B）

### 6.1 用途

- **人間によるGUIエディタでの制作**（KFX Editor）
- **AIによるキーフレームベースの記述**（テキスト編集も容易）
- 少数のキーフレームから自動補間でフレームを生成

### 6.2 構造

```json
{
  "format": "kfx",
  "name": "effect_name",
  "canvas": 100,
  "fps": 30,
  "duration": 1.5,
  "se": "sound_name",
  "target": "icon",
  "icon_rect": { "x": 10, "y": 10, "width": 80, "height": 80 },
  "layers": [
    {
      "id": "glow",
      "type": "circle",
      "blend": "additive",
      "visible": [0.0, 1.5],
      "keyframes": [
        {
          "time": 0.0, "ease": "easeOut",
          "x": 50, "y": 50, "radius": 3,
          "brush": { "type": "radial", "center": "#FFFFFF00", "edge": "#FF880000" }
        },
        {
          "time": 0.3,
          "radius": 20,
          "brush": { "type": "radial", "center": "#FFFFFFCC", "edge": "#FF880000" }
        },
        {
          "time": 1.5,
          "radius": 5, "opacity": 0.0
        }
      ]
    },
    {
      "id": "sparks",
      "type": "emitter",
      "x": 50, "y": 50,
      "count": 12,
      "angle_range": [0, 360],
      "speed_range": [1.0, 3.5],
      "gravity": 0.05,
      "drag": 0.96,
      "lifetime": 1.2,
      "size_range": [1.0, 2.5],
      "color_start": "#FFCC44BB",
      "color_end": "#FF440000",
      "blend": "additive",
      "seed": 42
    }
  ]
}
```

### 6.3 ルートプロパティ

| プロパティ | 型 | デフォルト | 説明 |
|-----------|-----|-----------|------|
| `format` | string | — | `"kfx"` を指定 |
| `name` | string | (必須) | エフェクト名 |
| `canvas` | int | 100 | キャンバスサイズ（1〜512） |
| `fps` | int | 30 | フレームレート（1〜120） |
| `duration` | float | 1.0 | エフェクト全体の長さ（秒） |
| `se` | string | null | 効果音ファイル名（拡張子なし） |
| `target` | string | `"icon"` | 配置モード（§4.9参照） |
| `icon_rect` | object | null | アイコン参照矩形（§4.10参照） |
| `field_rect` | object | null | フィールド参照矩形（§4.10参照） |
| `layers` | array | (必須) | レイヤー配列 |

### 6.4 レイヤー

1つのレイヤー = 1つのアニメーションされるシェイプ。

| プロパティ | 型 | 説明 |
|-----------|-----|------|
| `id` | string | レイヤー識別子（一意） |
| `type` | string | シェイプタイプ（§4.4参照） |
| `blend` | string | ブレンドモード（デフォルト: `"normal"`） |
| `visible` | [float, float] | 表示範囲 [開始秒, 終了秒]（省略時は全区間） |
| `keyframes` | array | キーフレーム配列（エミッター以外） |

**エミッターレイヤー**は `keyframes` を持たず、代わりにパーティクルパラメータ（§4.7）をレイヤーレベルに直接記述する。`lifetime` は秒単位で指定（コンパイル時にフレーム数に自動変換）。

### 6.5 キーフレーム

時刻ごとのプロパティスナップショット。キーフレーム間は自動補間される。

```json
{
  "time": 0.3,
  "ease": "easeOut",
  "x": 50, "y": 50,
  "radius": 20,
  "pen": { "color": "#FFFFFF", "width": 2 },
  "brush": { "type": "radial", "center": "#FFFFFFCC", "edge": "#FF000000" },
  "opacity": 0.8
}
```

#### 部分キーフレーム

- **最初のキーフレーム**: そのシェイプに必要な全プロパティを指定
- **2番目以降**: 変更したいプロパティのみ指定（省略 = 前のキーフレームから継承）

```json
"keyframes": [
  { "time": 0.0, "x": 50, "y": 50, "radius": 5, "brush": {"color": "#FF0000"} },
  { "time": 0.5, "radius": 30 },
  { "time": 1.0, "radius": 5, "opacity": 0.0 }
]
```

上記の例では `x`, `y`, `brush` は最初のキーフレームの値が最後まで継承され、`radius` と `opacity` だけがアニメーションする。

#### キーフレームプロパティ一覧

| プロパティ | 対応シェイプ | 説明 |
|-----------|-------------|------|
| `time` | 全て | 時刻（秒、昇順必須） |
| `ease` | 全て | イージング関数名（§6.6参照） |
| `x`, `y` | point, circle, ellipse, arc, rect, ring | 中心座標 |
| `radius` | circle, arc, ring | 半径（ringでは外径） |
| `inner_radius` | ring | 内径 |
| `rx`, `ry` | ellipse | X/Y方向の半径 |
| `startAngle`, `endAngle` | arc | 弧の開始/終了角度 |
| `width`, `height` | rect | 矩形の幅/高さ |
| `rotation` | ellipse, rect | 回転角度（度） |
| `size` | point | 点のサイズ |
| `x1`, `y1`, `x2`, `y2` | line, tapered_line | 線の始点/終点 |
| `width_start`, `width_end` | tapered_line | テーパー幅（始点/終点） |
| `pen` | 全て | ペン設定 `{ "color", "width" }` |
| `brush` | circle, ellipse, rect, polygon, ring | ブラシ設定 |
| `opacity` | 全て | 全色のアルファを一括変調（0〜1） |

#### opacity

`opacity` は pen と brush の全ての色のアルファチャンネルに乗算される。

- `1.0` = 変化なし（デフォルト）
- `0.5` = 半透明
- `0.0` = 完全に透明（フェードアウトの終点に使う）

### 6.6 イージング関数

キーフレーム間の補間カーブを制御する。各キーフレームの `ease` に指定した関数は、そのキーフレームから**次のキーフレーム**までの区間に適用される。

| 名前 | 挙動 | 数式 |
|------|------|------|
| `linear` | 等速（デフォルト） | `t` |
| `easeIn` | ゆっくり開始→加速 | `t²` |
| `easeOut` | 速く開始→減速 | `t(2-t)` |
| `easeInOut` | 両端スムーズ | `t<0.5: 2t²` / `else: -1+(4-2t)t` |
| `step` | 補間なし（次のキーフレームまで現在値を保持） | `0` |

### 6.7 コンパイルプロセス

KFX形式はロード時に以下の手順でフレームベースの `EffectDefinition` に変換される:

1. **フレーム数計算**: `ceil(duration × fps)`
2. **キーフレーム解決**: 部分キーフレーム → 全プロパティが埋まった完全スナップショットに展開
3. **フレーム生成**: 各フレームについて:
   - 時刻 = `frameIndex / fps`
   - `visible` 範囲をチェック（範囲外ならスキップ）
   - 前後のキーフレームを見つけて補間（イージング適用）
   - 色はRGBA各成分ごとにlerp
   - `opacity` はpen/brushの全色のアルファに乗算
4. **エミッター処理**: `lifetime`（秒）→ フレーム数に変換、全表示フレームに配置
5. **結果**: 通常のフレームベース `EffectDefinition` と同一の構造

---

## 7. エディタツール

### 7.1 Effect Previewer

**メニュー**: Window → Effects → Effect Previewer（Ctrl+Shift+E）

両形式のエフェクトをプレビュー可能な汎用ビューア。

| 機能 | 説明 |
|------|------|
| Effect選択 | ドロップダウンでJSONを選択 |
| Reload / Refresh | 再読み込み / ファイルリスト更新 |
| フレーム送り | `|◀` `◀` `▶` `▶|` ボタン |
| 再生/停止 | Play / Pause / Stop |
| SE再生 | Play時にSEも同時再生 |
| Loop | ループ再生ON/OFF |
| Speed | 再生速度（0.1x〜3.0x） |
| Background | 背景色（Dark/Light/Checkerboard/Custom） |
| Size | プレビューサイズ（50〜400px） |

### 7.2 KFX Editor

**メニュー**: Window → Effects → KFX Editor（Ctrl+Shift+K）

KFX形式専用のGUIエディタ。UIは日本語。

| 機能 | 説明 |
|------|------|
| ファイル操作 | 新規作成（テンプレート選択） / 保存 / 読込 / リロード |
| メタデータ編集 | name, canvas, fps, duration, se（折りたたみ可能） |
| ライブプレビュー | 編集 → 即座にコンパイル → リアルタイム描画。ドラッグでx/y直接編集可能 |
| 再生制御 | Play / Pause / Stop / Loop / Speed / フレーム送り |
| タイムライン | レイヤー別キーフレーム表示。ドラッグで時刻移動、ダブルクリックでKF追加、右クリックメニュー |
| レイヤー管理 | 追加 / 削除 / 複製 / 並替 / 表示切替 |
| キーフレーム編集 | 追加 / 削除 / 複製 / プロパティグルーピング表示 |
| エミッター編集 | エミッターレイヤー専用のプロパティパネル |
| Undo/Redo | Ctrl+Z / Ctrl+Y（JSONスナップショット方式、最大30段） |
| SE再生 | プレビュー時にSEも再生 |

**キーボードショートカット（F1 または [?] ボタンで一覧表示）:**

| キー | 操作 |
|------|------|
| Ctrl+S | 保存 |
| Space / Shift+Space | 再生/一時停止 / 先頭から再生 |
| ←/→ | 1フレーム移動（+Shift: 10F, +Ctrl: KFジャンプ） |
| Home/End | 先頭/末尾 |
| K | キーフレーム追加 |
| Ctrl+D | 選択中を複製 |
| Delete | 選択中を削除 |
| ↑/↓ | キーフレーム選択移動 |
| Alt+↑/↓ | レイヤー選択移動 |
| F2 | レイヤーID名フォーカス |

**部分キーフレームの編集:**
最初のキーフレームでは全プロパティが常に有効。2番目以降のキーフレームでは各プロパティにチェックボックスがあり、チェックを入れたプロパティのみ上書き、チェックなしは前のキーフレームから継承される。

### 7.3 Effect Placement Editor

**メニュー**: Window → Effects → Effect Placement Editor（Ctrl+Shift+P）

エフェクトの `icon_rect` / `field_rect` をインタラクティブに編集するツール。

| 機能 | 説明 |
|------|------|
| Effect選択 | ドロップダウンでJSONを選択 |
| target切替 | icon / field モードの切り替え |
| キャンバスプレビュー | エフェクトの生描画。緑枠（エフェクトキャンバス）をドラッグ移動・四隅リサイズ |
| 結果プレビュー | icon_rect/field_rect 適用後の最終表示をプレビュー |
| 数値入力 | X / Y / Width / Height を直接入力 |
| プリセットボタン | フィット / ×2 / ×1.3 / 中央寄せ |
| モックアイコン | 味方(170×257) / 小敵(100×100) / 中敵(200×200) / 大敵(400×300) |
| 再生制御 | Play / Pause / Stop / Loop / Speed |
| Save / Reload | JSONへの書き込み / 読み込み |

**icon モード**: 灰色矩形（アイコンモック）を固定表示し、緑枠のエフェクトキャンバスをドラッグで配置調整。icon_rect が自動計算される。

**field モード**: ビューポート（1080×1041.4）上で緑枠のエフェクトキャンバスをドラッグで配置調整。field_rect が自動計算される。

---

## 8. ランタイムAPI

### 8.1 アイコンモード（target="icon"）

BattleIconUI 上にエフェクトを表示する。

```csharp
using Effects.Integration;

// ワンショット再生
EffectPlayer player = EffectManager.Play("effect_name", target.BattleIcon);

// ループ再生
EffectPlayer player = EffectManager.Play("effect_name", target.BattleIcon, loop: true);

// 指定エフェクトを停止
EffectManager.Stop(target.BattleIcon, "effect_name");

// 全エフェクトを停止
EffectManager.StopAll(target.BattleIcon);

// 再生中か確認
bool playing = EffectManager.IsPlaying(target.BattleIcon, "effect_name");

// キャッシュクリア（JSONを編集した場合）
EffectManager.ClearCache();
```

### 8.2 フィールドモード（target="field"）

バトルフィールド全体にエフェクトを表示する。BattleIconUI は不要。

```csharp
using Effects.Integration;

// フィールドエフェクト再生
EffectPlayer player = EffectManager.PlayField("effect_name");

// ループ再生
EffectPlayer player = EffectManager.PlayField("effect_name", loop: true);

// 指定エフェクトを停止
EffectManager.StopField("effect_name");

// 全フィールドエフェクトを停止
EffectManager.StopAllField();
```

**前提条件**: シーン内に `FieldEffectLayer` が必要（§8.3参照）。

### 8.3 FieldEffectLayer のセットアップ

フィールドエフェクトを再生するには、シーン内に FieldEffectLayer が必要。

**メニュー**: Tools → Effects → Setup Field Effect Layer

このメニューを実行すると `AlwaysCanvas/EyeArea/ViewportArea` 配下に `FieldEffectLayer` が自動生成される。`EffectLayer` コンポーネント（`IsFieldLayer = true`）を持つ RectTransform で、ビューポート全体をカバーする。

### 8.4 テスターコンポーネント

Play モード中にエフェクトを手軽に確認するためのコンポーネント。

#### EffectSystemTester（アイコンモード用）

BattleIconUI を持つ GameObject にアタッチして使用。

| Inspector | 説明 |
|-----------|------|
| `targetIcon` | 対象の BattleIconUI（未指定なら自身から取得） |
| `effectName` | エフェクト名 |
| `loop` | ループ再生 |
| `playOnStart` | Start() で自動再生 |

コンテキストメニュー: Play Effect / Stop Effect / Stop All Effects / Check Is Playing

#### FieldEffectTester（フィールドモード用）

任意の GameObject にアタッチして使用（BattleIconUI 不要）。

| Inspector | 説明 |
|-----------|------|
| `effectName` | エフェクト名 |
| `loop` | ループ再生 |
| `playOnStart` | Start() で自動再生 |

コンテキストメニュー: Play Field Effect / Stop Field Effect / Stop All Field Effects

### 8.5 スキル連携（3スロット方式）

スキル発動時にエフェクトを自動再生する仕組み。`SkillLevelData` に3つのスロットを持つ。

| スロット | フィールド | target | 再生先 | Inspector属性 |
|---------|-----------|--------|--------|--------------|
| 術者エフェクト | `CasterEffectName` | `"icon"` | 術者の BattleIconUI | `[EffectName("icon")]` |
| 対象エフェクト | `TargetEffectName` | `"icon"` | 各ターゲットの BattleIconUI | `[EffectName("icon")]` |
| フィールドエフェクト | `FieldEffectName` | `"field"` | ViewportArea 全体 | `[EffectName("field")]` |

- 全て `string` 型（エフェクトJSON名、拡張子なし）
- null / 空文字 → そのスロットはスキップ
- `[EffectName]` 属性のフィルタにより、Inspector上で対応するtargetのエフェクトのみ選択可能
- 再生タイミング: ダメージ計算と同時（fire-and-forget）

```csharp
// SkillExecutor.cs 内
PlaySkillVisualEffects(skill);
  → EffectManager.Play(skill.CasterEffectName, acter.BattleIcon)
  → EffectManager.Play(skill.TargetEffectName, target.BattleIcon) × 各対象
  → EffectManager.PlayField(skill.FieldEffectName)
```

---

## 9. 形式の使い分けガイド

| 観点 | フレームベース（A） | KFX（B） |
|------|-------------------|----------|
| 主な制作者 | AI（Claude） | 人間 / AI |
| 制作ツール | テキストエディタ / スクリプト | KFX Editor GUI / テキストエディタ |
| データ量 | 多い（全フレーム記述） | 少ない（キーフレームのみ） |
| 制御精度 | フレーム単位 | キーフレーム間は自動補間 |
| 音声同期 | audio-envelope MCPで振幅→フレーム直接マッピング | 手動でキーフレーム配置 |
| 編集しやすさ | AIには容易、人間には困難 | GUIで直感的に編集可能 |
| 適した表現 | 音に精密同期する複雑な動き | シンプルなパルス、フェード、拡縮 |

**選択の目安:**
- 効果音に合わせた精密な演出 → フレームベース（AI生成）
- 手作業でパラメータを調整しながら作りたい → KFX
- テキストで手軽に書きたいがフレーム量は減らしたい → KFX
- どちらでも迷ったら → KFX（後から手動調整しやすい）

---

## 10. ソースコード構成

```
Assets/Script/Effects/
├── EffectConstants.cs              # 定数定義
├── EffectSystemTester.cs           # アイコンモード テスター（§8.4）
├── FieldEffectTester.cs            # フィールドモード テスター（§8.4）
├── Core/
│   ├── EffectDefinition.cs         # フレームベースのデータモデル
│   ├── KfxDefinition.cs            # KFXのデータモデル
│   ├── KfxEasing.cs                # イージング関数
│   └── KfxCompiler.cs              # KFX→EffectDefinition変換 + 形式自動判別
├── Rendering/
│   ├── EffectRenderer.cs           # フレーム描画（Texture2Dへ）
│   ├── ShapeDrawer.cs              # 図形描画プリミティブ
│   └── EffectColorUtility.cs       # 色パース・ブラシコンテキスト生成
├── Playback/
│   └── EffectPlayer.cs             # 再生制御（フレーム進行・ループ）
└── Integration/
    ├── EffectManager.cs            # 統合API（ロード・キャッシュ・SE再生）
    └── EffectLayer.cs              # エフェクト表示レイヤー（icon/field両対応）

Assets/Editor/Effects/
├── EffectPreviewWindow.cs          # Effect Previewer（両形式対応）
├── EffectPlacementEditor.cs        # Effect Placement Editor（配置調整）
├── EffectNameDrawer.cs             # [EffectName] 属性の PropertyDrawer（targetフィルタ対応）
├── EffectTesterEditors.cs          # EffectSystemTester / FieldEffectTester カスタムインスペクタ
├── FieldEffectLayerSetup.cs        # FieldEffectLayer セットアップユーティリティ
├── KfxEditorWindow.cs              # KFX Editor コア（状態管理・ショートカット・Undo）
└── KfxEditorWindow.Drawing.cs      # KFX Editor 描画（UI描画・プロパティ編集・テンプレート）

Assets/Script/BaseSkill/
└── EffectNameAttribute.cs          # [EffectName] PropertyAttribute（targetフィルタパラメータ付き）

Assets/Resources/Effects/           # エフェクトJSON（両形式混在可）
Assets/Resources/Audio/             # 効果音WAVファイル
```

---

## 11. 制限事項

- **キャンバスサイズ**: 最大512px
- **FPS**: 1〜120
- **polygon / bezier のKFXアニメーション**: 頂点の補間に非対応。最初のキーフレームの形状が維持される
- **エミッターのキーフレーム**: エミッターレイヤーはキーフレームを持てない。パラメータはレイヤーレベルで固定
- **KFXの `visible` とエミッター**: `visible[0] > 0` でもパーティクルは表示開始時点から正しくシミュレーションされる（ローカルフレーム番号で計算）
- **emitterの物理シミュレーション**: 毎フレーム再計算のため、lifetimeが長い場合はコスト注意
