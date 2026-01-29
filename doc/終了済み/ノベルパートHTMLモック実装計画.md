# ノベルパート HTMLモック実装計画

## 概要

ノベルパート設計の技術デモをHTML/CSS/JSで作成する。
1ファイルで完結するインタラクティブなモック。

## 関連ドキュメント

- [ノベルパート設計.md](./ノベルパート設計.md) - 設計本体

---

## 再現する機能

### 1. 表示モード切り替え

**ディノイド ⇔ 立ち絵**

| 要素 | 実装方法 |
|------|---------|
| ディノイド用テキストボックス | アイコン内包型のdiv |
| 立ち絵用テキストボックス | 別デザインのdiv |
| 切り替えアニメーション | CSS transition (opacity, scale, rotate) |

### 2. テキストボックス演出

- 開く/閉じるアニメーション
- ダンロン風の点滅演出（CSS animation）
- 右斜め下からシュッと出現（transform: translate + rotate）

### 3. 立ち絵登場トランジション（3案）

**案1: ロックマン案**
```css
/* キャラ色の縦線が降りてくる */
.rain-line {
  animation: fall 0.3s ease-in;
}
/* 着地後に立ち絵フェードイン */
.portrait {
  animation: fadeIn 0.2s ease-out 0.3s forwards;
}
```

**案2: 上からフェードイン**
```css
.portrait {
  animation: slideFromTop 0.3s ease-out;
}
```

**案3: 下からフェードイン**
```css
.portrait {
  animation: slideFromBottom 0.3s ease-out;
}
```

### 4. 立ち絵退場

- 右の立ち絵 → 右へ捌ける（translateX(100%)）
- 左の立ち絵 → 左へ捌ける（translateX(-100%)）

### 5. 背景切り替え

| 遷移 | 実装 |
|------|------|
| 背景なし → あり | スライドイン or フェードイン |
| 背景あり → なし | フェードアウト |

### 6. ズーム（中央オブジェクト）

```css
.central-object {
  transition: transform 0.5s ease;
}
.central-object.zoomed {
  transform: scale(1.5) translateY(-10%);
}
```

### 7. 雑音システム（弾幕コメント風）

```css
.noise {
  position: absolute;
  animation: flowLeft 3s linear forwards;
}
@keyframes flowLeft {
  from { transform: translateX(100vw); }
  to { transform: translateX(-100%); }
}
```

- セリフ送り時に発火
- 主セリフが飛ばされたら加速（animation-duration短縮）

### 8. MessageDropper風ログ

```css
.log-message {
  animation: floatUp 5s linear forwards;
}
@keyframes floatUp {
  from { transform: translateY(0); opacity: 1; }
  to { transform: translateY(-200px); opacity: 0; }
}
```

### 9. 選択肢と精神属性

```html
<div class="choice" data-spirit="kindergarden">
  「まあいいか」 [幼稚園]
</div>
<div class="choice" data-spirit="sacrifaith">
  「俺がやる」 [自己犠牲]
</div>
```

- 選択時にインジケータ表示（「精神属性: 幼稚園」など）

---

## 画面構成

```
┌─────────────────────────────────────────┐
│  [背景レイヤー]                          │
│  ┌─────┐                    ┌─────┐    │
│  │左立絵│                    │右立絵│    │
│  └─────┘                    └─────┘    │
│                                         │
│        [中央オブジェクト]                │
│                                         │
│  ～～～ 雑音コメント ～～～→              │
│                                         │
│  ┌─────────────────────────────────┐   │
│  │  テキストボックス                 │   │
│  │  「セリフがここに表示される」      │   │
│  └─────────────────────────────────┘   │
│                                         │
│  [ログ↑]                               │
└─────────────────────────────────────────┘
```

---

## インタラクション

| 操作 | 動作 |
|------|------|
| クリック/タップ | 次のセリフへ進む + 雑音発火 |
| 選択肢クリック | 精神属性表示 + 分岐 |
| モード切り替えボタン | ディノイド ⇔ 立ち絵 |
| 背景切り替えボタン | 背景なし ⇔ あり |
| ズームボタン | 中央オブジェクトにズーム |

---

## デモシナリオ（サンプルデータ）

```javascript
const scenario = [
  {
    mode: "dinoid",
    speaker: "Geino",
    text: "なんか静かだな",
    noises: []
  },
  {
    mode: "dinoid",
    speaker: "Noramlia",
    text: "そうね",
    noises: [
      { speaker: "Sites", text: "確かに" }
    ]
  },
  {
    mode: "portrait",  // ここで立ち絵モードに切り替わる
    speaker: "Geino",
    text: "あれ、なんかいるぞ",
    noises: [
      { speaker: "Noramlia", text: "え？" },
      { speaker: "Sites", text: "どこ？" }
    ]
  },
  {
    type: "choice",
    choices: [
      { text: "近づいてみる", spirit: "kindergarden" },
      { text: "様子を見る", spirit: "cquiest" },
      { text: "俺が確認する", spirit: "sacrifaith" }
    ]
  },
  {
    mode: "portrait",
    background: "forest.png",  // 背景スライドイン
    speaker: "Geino",
    text: "...自販機か"
  }
];
```

---

## 実装フェーズ

### Phase 1: 基本構造
- [ ] HTML骨格（レイヤー構成）
- [ ] CSS変数でテーマカラー定義
- [ ] 基本的なテキストボックス表示

### Phase 2: モード切り替え
- [ ] ディノイド用テキストボックス
- [ ] 立ち絵用テキストボックス
- [ ] 切り替えアニメーション

### Phase 3: 立ち絵
- [ ] 左右立ち絵の配置
- [ ] 登場トランジション3案
- [ ] 退場（捌ける）

### Phase 4: 背景・ズーム
- [ ] 背景レイヤー
- [ ] スライドイン/アウト
- [ ] 中央オブジェクトズーム

### Phase 5: 雑音・ログ
- [ ] 雑音コメント（右→左）
- [ ] ログ（下→上）

### Phase 6: インタラクション
- [ ] クリックで進行
- [ ] 選択肢表示
- [ ] 精神属性インジケータ

---

## ファイル構成

```
doc/歩行システム設計/
  └── novel_part_mock.html  （1ファイルで完結）
```

CSS/JSはすべてインラインで記述し、単体で動作するようにする。

---

## 備考

- 歩行システムの再現は不要（モックなので）
- 精神属性は選択肢に `[属性名]` と表示するだけでOK
- 画像はプレースホルダー（色付きdiv）で代用可能
