# Unityマルチ解像度UI対応ガイド

## 概要

様々なスマートフォン・タブレット端末でUIが正しく表示されるようにするための設計指針。

## 1. CanvasScalerの設定（最重要）

### 推奨設定

```
Canvas Scaler コンポーネント:
├── UI Scale Mode: "Scale With Screen Size"
├── Reference Resolution: 1080 x 1920 (基準解像度)
├── Screen Match Mode: "Match Width Or Height"
└── Match: 0.5 〜 1.0
```

### Match値の意味

| 値 | 動作 | 適したケース |
|----|------|-------------|
| 0 | 幅基準 | 横長端末で縦がはみ出る可能性 |
| 0.5 | バランス | 汎用的 |
| 1 | 高さ基準 | 縦長UIで横がはみ出る可能性 |

**縦持ちゲームでは `Match = 1`（高さ基準）を推奨。**
横幅は多少余白ができても許容し、縦方向のレイアウトを維持する。

## 2. アンカー設定（RectTransform）

### 基本パターン

```
┌─────────────────────────────────┐
│  ┌─┐                       ┌─┐  │  ← 四隅のUI: Anchor=各隅
│  └─┘                       └─┘  │
│                                 │
│         ┌───────────┐           │  ← 中央のUI: Anchor=中央
│         │   中央    │           │
│         └───────────┘           │
│                                 │
│  ┌─────────────────────────┐   │  ← 横幅いっぱい: Anchor=左右Stretch
│  └─────────────────────────┘   │
└─────────────────────────────────┘
```

### アンカータイプ

| タイプ | アンカー設定 | 用途 |
|--------|-------------|------|
| 固定サイズ | 4点を1点に集約 | ボタン、アイコン |
| 横伸縮 | 左右に分散 | ヘッダー、フッター |
| 縦伸縮 | 上下に分散 | サイドバー |
| 全伸縮 | 4隅に分散 | 背景、オーバーレイ |

## 3. 端末別アスペクト比

| 端末 | アスペクト比 | 備考 |
|------|-------------|------|
| iPhone SE | 9:16 | 基準に近い |
| iPhone 14 | 9:19.5 | 縦長 |
| iPhone 14 Pro Max | 9:19.5 | 縦長 + ノッチ |
| iPad | 3:4 | 大幅に横長 |
| iPad Pro 12.9 | 3:4 | 大幅に横長 + 大画面 |
| Android各種 | 9:16〜9:21 | 多様 |

## 4. 対応戦略

### A) 1レイアウトで対応（推奨）

**メリット**: 開発コスト低、保守しやすい
**方法**:
- CanvasScaler + アンカー設定で吸収
- 極端なアスペクト比では余白を許容

```csharp
// Safe Area対応パネル
public class SafeAreaPanel : MonoBehaviour
{
    void Start()
    {
        var safeArea = Screen.safeArea;
        var screenSize = new Vector2(Screen.width, Screen.height);

        var rt = GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(safeArea.x / screenSize.x, safeArea.y / screenSize.y);
        rt.anchorMax = new Vector2((safeArea.x + safeArea.width) / screenSize.x,
                                    (safeArea.y + safeArea.height) / screenSize.y);
    }
}
```

### B) アスペクト比でレイアウト切り替え

**メリット**: iPad等で最適なUI
**デメリット**: 2セットのUI管理が必要

```csharp
public class AspectRatioLayoutSwitcher : MonoBehaviour
{
    [SerializeField] private GameObject phoneLayout;
    [SerializeField] private GameObject tabletLayout;

    void Start()
    {
        float aspect = (float)Screen.width / Screen.height;
        bool isTablet = aspect > 0.6f; // 3:4より横長ならタブレット判定

        phoneLayout.SetActive(!isTablet);
        tabletLayout.SetActive(isTablet);
    }
}
```

### C) Layout Groupの活用

自動配置・折り返しで柔軟に対応。

```
HorizontalLayoutGroup / VerticalLayoutGroup
├── Child Alignment: 配置位置
├── Spacing: 要素間隔
├── Child Force Expand: 子要素を引き伸ばすか
└── Child Control Size: サイズ制御

GridLayoutGroup
├── Cell Size: セルサイズ
├── Spacing: 間隔
├── Constraint: 行/列数の制約
└── Start Corner/Axis: 配置開始位置
```

## 5. 実践的なチェックリスト

### 設計時

- [ ] CanvasScalerを「Scale With Screen Size」に設定
- [ ] Reference Resolutionを決定（例: 1080x1920）
- [ ] Matchを0.5〜1.0に設定
- [ ] 各UIのアンカーを適切に設定

### テスト時

- [ ] Game Viewで複数解像度をテスト
  - 9:16 (基準)
  - 9:19.5 (iPhone縦長)
  - 9:21 (Android縦長)
  - 3:4 (iPad)
- [ ] Safe Area（ノッチ/パンチホール）対応確認
- [ ] 横持ち時の動作確認（必要な場合）

## 6. iPad対応は必要か？

### 判断基準

| ターゲット | iPad対応 |
|-----------|---------|
| カジュアルゲーム | 余白許容でOK |
| RPG/ストーリー重視 | 余白許容でOK |
| タブレット市場重視 | 専用UI検討 |
| 教育/ビジネス | 専用UI推奨 |

### 最小コスト対応

1. **レターボックス**: 上下/左右に黒帯を入れてアスペクト比を固定
2. **余白追加**: 端に装飾的な余白を追加して違和感を軽減
3. **UI拡大**: タブレットではUI要素を大きく表示（読みやすさ向上）

## 7. pigRPGの現状分析（2026-01-18 MCP調査）

### Canvas構成

| Canvas | 用途 |
|--------|------|
| AlwaysCanvas | 常時表示UI（EyeArea, Message等） |
| DynamicCanvas | 動的UI（USERUI, Walking, Modal等） |

### CanvasScaler設定（両Canvas共通）

```
UI Scale Mode:      Scale With Screen Size ✅
Reference Resolution: 1080 x 1920 ✅
Screen Match Mode:  Match Width Or Height
Match:              0.0 ⚠️ 問題！
```

**問題点**: `Match = 0`（幅基準）になっている。
- 基準より縦長の端末 → 縦方向がはみ出る
- 基準より横長の端末（iPad等） → 縦方向が余る

**修正案**: `Match = 1`（高さ基準）に変更

### 主要UIのアンカー設定

| GameObject | anchorMin | anchorMax | sizeDelta | 問題 |
|------------|-----------|-----------|-----------|------|
| EyeArea | (0.5, 0.5) | (0.5, 0.5) | 1080 x 1041 | 幅固定 |
| USERUI | (0.5, 0.5) | (0.5, 0.5) | 1080 x 878 | 幅固定 |

**問題点**: すべてのUIがアンカー中央固定 + サイズ固定（1080幅）
- 基準解像度より狭い端末 → 左右がはみ出る
- 基準解像度より広い端末 → 左右に余白

### 推奨修正

#### 1. CanvasScaler修正（優先度高）

```
AlwaysCanvas / DynamicCanvas:
  Match Width Or Height → Match = 1.0（高さ基準）
```

これだけで縦方向のレイアウトが維持され、横幅は自動調整される。

#### 2. アンカー修正（優先度中）

横幅いっぱいに表示したい要素:
```
変更前: anchorMin=(0.5, 0.5), anchorMax=(0.5, 0.5), sizeDelta=(1080, h)
変更後: anchorMin=(0, 0.5), anchorMax=(1, 0.5), sizeDelta=(0, h)
        または Left/Right マージンを設定
```

#### 3. Safe Area対応（優先度低）

ノッチ/パンチホール対応が必要な場合のみ。

### 修正の影響範囲

| 修正 | 影響 | リスク |
|------|------|--------|
| Match変更 | 全UI | 低（スケール比率のみ変更） |
| アンカー修正 | 個別UI | 中（レイアウト崩れ要確認） |

### テスト手順

1. Game Viewで以下の解像度をテスト:
   - 1080x1920 (基準)
   - 1080x2340 (縦長スマホ)
   - 1080x2400 (超縦長)
   - 1620x2160 (iPad 3:4相当)

2. 各解像度で確認:
   - UIが見切れていないか
   - タップ領域が正しいか
   - テキストが読めるか

---

*作成日: 2026-01-18*
*更新日: 2026-01-18 - MCP調査結果追加*
