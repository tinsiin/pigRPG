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

---

## JSON形式

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
| `fps` | int | Yes | デフォルトフレームレート |
| `se` | string | No | 効果音ファイル名（拡張子なし）、null で無音 |
| `frames` | array | Yes | フレーム配列 |

### フレーム

| フィールド | 型 | 必須 | 説明 |
|-----------|-----|------|------|
| `duration` | float | No | このフレームの表示時間（秒）、省略時は `1/fps` |
| `shapes` | array | Yes | 描画する図形の配列、`[]` で空フレーム |

---

## 図形タイプ

### point（点）

```json
{
  "type": "point",
  "x": 50,
  "y": 50,
  "size": 4,
  "brush": { "color": "#FFFFFF" }
}
```

| パラメータ | 型 | 説明 |
|-----------|-----|------|
| `x`, `y` | float | 中心座標 |
| `size` | float | 点のサイズ（直径） |

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
| `x`, `y` | float | 左上座標（rotation=0の場合） |
| `width`, `height` | float | 幅と高さ |
| `rotation` | float | 回転角度（度、中心を軸に回転、省略時: 0） |

### polygon（多角形）

```json
{
  "type": "polygon",
  "points": [[25, 10], [50, 90], [75, 10]],
  "pen": { "color": "#00FFFF", "width": 1 },
  "brush": { "color": "#00FFFF60" }
}
```

| パラメータ | 型 | 説明 |
|-----------|-----|------|
| `points` | array | 頂点座標の配列 `[[x1,y1], [x2,y2], ...]` |

### bezier（ベジェ曲線）

```json
{
  "type": "bezier",
  "points": [[10, 50], [30, 10], [70, 90], [90, 50]],
  "pen": { "color": "#FFA500", "width": 2 }
}
```

| パラメータ | 型 | 説明 |
|-----------|-----|------|
| `points` | array | 制御点の配列（3点: 2次ベジェ、4点: 3次ベジェ） |

---

## Pen / Brush

### Pen（線・輪郭）

```json
"pen": {
  "color": "#RRGGBBAA",
  "width": 2
}
```

| フィールド | 型 | 説明 |
|-----------|-----|------|
| `color` | string | 色（`#RRGGBB` または `#RRGGBBAA`） |
| `width` | float | 線の太さ（ピクセル） |

- `pen: null` または省略で線なし

### Brush（塗りつぶし）

```json
"brush": {
  "color": "#RRGGBBAA"
}
```

| フィールド | 型 | 説明 |
|-----------|-----|------|
| `color` | string | 色（`#RRGGBB` または `#RRGGBBAA`） |

- `brush: null` または省略で塗りなし
- `line`, `arc`, `bezier` は塗りなし（pen のみ有効）

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
JSONからデシリアライズされるデータクラス。Unity JsonUtility対応。

#### EffectRenderer
Texture2Dに図形を描画するエンジン。各図形タイプごとの描画ロジックを持つ。

#### EffectPlayer
MonoBehaviourとして動作し、タイムラインに沿ってフレームを進行。
ループ制御、再生/停止を管理。

#### EffectLayer
BattleIconUIに追加されるコンポーネント。
RawImageを持ち、EffectPlayerからTexture2Dを受け取って表示。
複数エフェクトの重ね合わせに対応。

#### EffectManager
シングルトンまたは静的クラス。
- エフェクト定義の読み込みとキャッシュ
- 再生リクエストの受付
- アクティブなエフェクトの管理

---

## 描画方式

### Texture2D動的生成 + RawImage表示

1. フレーム切り替え時のみ再描画（パラパラアニメなので毎フレーム更新不要）
2. 透明キャンバス（Color.clear）に図形を描画
3. RawImageで表示

### パフォーマンス考慮

- 同時30エフェクトでも、各々が毎フレーム更新するわけではない
- フレーム切り替えタイミングが分散されるため負荷も分散
- オプション: 頻繁に使うエフェクトは全フレーム事前レンダリングしてキャッシュ

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

---

## 今後の拡張案

- グラデーション対応
- パーティクル的表現（複数インスタンス生成）
- シェーダーエフェクト（グロー、ブラーなど）
- エフェクトエディタ（GUI）
