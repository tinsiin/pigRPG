# CharaConfig 仕様書

## 概要

CharaConfig（キャラクター設定画面）は、USERUI領域内のタブの一つ。パーティーメンバーの情報閲覧・設定変更を行う画面。横バナー式レイアウト。

**コントローラ**: `CharaconfigController`（シングルトン、`IPlayersContextConsumer`実装）

---

## キャラクター切替

### ナビゲーション

左右ボタン（`m_LeftButton` / `m_RightButton`）でパーティーメンバーを切り替える。

- インデックスは `0` ～ `パーティー人数-1` の範囲でクランプ
- 先頭では左ボタン無効化、末尾では右ボタン無効化
- 切替時に `_onSelectionChanged`（`Observable<int>`）を発火

### パーティー順序

`_partyMemberIds` に固定順序で格納:
1. Geino
2. Noramlia
3. Sites
4. その他（将来の新キャラ）

`IPartyComposition.ActiveMemberIds` から取得し、固定順序→残りの順で並べる。

### キャラクター取得

`GetActor(index)` → `playersRoster.GetAlly(CharacterId)` で `AllyClass` を返す。

### 外部API

| メソッド | 用途 |
|---|---|
| `Next()` / `Prev()` | ボタンコールバック |
| `SetSelectedIndex(int)` | インデックス直指定 |
| `SetSelectedByCharacterId(CharacterId)` | ID指定 |
| `SetSelectedByActor(BaseStates)` | アクター指定（ID経由） |

---

## 表示要素

### ステータスバナー

`StatesBannerController`（`m_StatesBanner`）にキャラクターをバインドし、ステータスゲージ・テキスト・背景を一括反映。

```
m_StatesBanner.Bind(actor);
m_StatesBanner.SetCharacterIndex(m_CurrentIndex);
```

### キャラ名

`m_CharacterNameText`（TMP）に `actor.CharacterName` を表示。

---

## 操作ボタン群

### 武器選択ボタン

`m_OpenWeaponSelectButton` → `WeaponSelectArea` を開く。

- **戦闘中は無効化**（`BattleContextHub.IsInBattle` で判定）
- 武器変更後に `StatesBanner.Bind` で即時反映

### 規格サイクルボタン

`m_ProtocolCycleButton` → 武器の規格（Protocol）を順次切り替え。

- **表示条件**: 武器装備あり + 複数規格あり + 戦闘中でない
- ラベル（`m_ProtocolCycleLabel`）に現在規格の表示名を反映
- 条件を満たさない場合は `SetActive(false)` で非表示
- 規格切り替え時には適応ラグ（77〜100%）が発生する（→ `戦闘規格・狙い流れ仕様書.md`）

### 感情愛着（思い入れ）ボタン

`m_OpenEmotionalAttachmentButton` → スキル選択UI（感情愛着/思い入れフレーム設定）を開く。

- `skillUi.OpenEmotionalAttachmentSkillSelectUIArea(actor.CharacterId)` を呼び出し
- 思い入れはTLOAスキルの中から一つを排他的に選択するフレーム。歩行時のみCharaConfigから操作可能
- 戦闘開始時に思い入れUIが開いていた場合は自動で閉じる

### 凍結連続停止ボタン

`m_StopFreezeConsecutiveButton` → FreezeConsecutiveの停止予約。

- **表示条件**: `actor.IsNeedDeleteMyFreezeConsecutive() && !actor.IsDeleteMyFreezeConsecutive`
- 条件を満たさない場合は `SetActive(false)` で非表示
- クリック後に `RefreshUI()` で状態再同期

### 割り込みカウンタートグル

`m_InterruptCounterToggle`（`ToggleSingleController`）→ 割り込みカウンターの有効/無効。

- `actor.OnSelectInterruptCounterActiveBtnCallBack` をリスナー登録
- 現在の状態をUIに同期: `IsInterruptCounterActive ? 0 : 1`（0: 有効、1: 無効）

---

## パッシブ表示システム

### 通常表示（CharaConfig画面内）

**構成**: `m_PassivesTexts[]`（TMP配列）に `SmallPassiveName` ベースのトークンを分割表示。

**トークン形式**: `<SmallPassiveName>` を半角スペース区切りで連結。名前が空の場合は `<fa{ID}>` をフォールバック表示。

**表示ソート順**（短期的なパッシブほど先に表示。永続パッシブより一時的・短期脅威的なものを優先する設計意図）:
1. `DurationTurn`（昇順、負値=無期限は末尾）
2. `DurationTurnCounter`（昇順）
3. `DurationWalk`（昇順、負値=無期限は末尾）
4. `DurationWalkCounter`（昇順）
5. 元のリスト順序（安定化）

このソート順はkZoom（アイコン拡大ステータス表示）およびパッシブモーダルでも共通。

**フィールド充填アルゴリズム**:
1. 先頭〜N-2番目のフィールド: トークンを順に連結し、TMPの `RectTransform` 高さに収まるだけ詰める（収まらなかったトークンは次のフィールドへ）
2. 最後のフィールド: 残り全トークンを連結し、`FitTextIntoRectWithEllipsis` で省略付き（`••••`）整形

**高さフィット判定**（`PassiveTextUtils.FitsHeight`）:
- TMPの `overflowMode` を一時的に `Overflow` に変更
- 候補テキストをセットし `ForceMeshUpdate()` で測定
- `preferredHeight <= rect.height - safety` なら収まると判定
- 測定後にTMPの状態を復元

### 設定パラメータ

| フィールド | デフォルト | 説明 |
|---|---|---|
| `m_PassivesEllipsisDotCount` | 4 | 省略ドット（`•`）の数 |
| `m_PassivesFitSafety` | 1.0 | 高さ方向の余白（px相当） |
| `m_PassivesAlwaysAppendEllipsis` | true | 常に末尾ドットを付加 |

### デバッグモード

`m_PassivesDebugMode = true` にすると、実データの代わりにダミートークン（`<pas1> <pas2> ...`）を生成。`m_PassivesDebugCount` で個数、`m_PassivesDebugPrefix` でプレフィックスを指定。

---

## パッシブモーダル（全表示）

### 導線

`m_PassiveModalButton`（専用ボタン）をクリックで開く。

- ボタンは常時表示。パッシブが0個の場合は `interactable = false`
- クリック時に `OpenPassivesModalForCurrent()` → `PassivesMordaleAreaController.ShowFor(actor)` を呼び出し

### モーダル表示

**コントローラ**: `PassivesMordaleAreaController`（`IPointerClickHandler` 実装）

- **正式名称表示**: `PassiveName` ベースのトークン（`BuildPassivesFullNameTokens`）
  - フォールバック: `PassiveName` → `SmallPassiveName` → `fa{ID}`
- **ページ構成**: 2つのTMPフィールド（`m_FirstText` / `m_SecondText`）をページ単位で表示
  - aTMP: 省略なしで入るだけ詰める
  - bTMP: 残りが次ページに溢れる場合は省略（`••••`）付き、収まる場合はそのまま
- **ページナビゲーション**: 左右ボタンでページ切替。1ページの場合はボタン無効化
- **閉じ方**: モーダルエリア全体をタップ（`OnPointerClick`）

### ModalAreaController連携

```
ModalAreaController.Instance?.ShowSingle(m_Root)  // 開く
ModalAreaController.Instance?.CloseFor(m_Root)    // 閉じる
```

---

## UI更新タイミング

`RefreshUI()` が呼ばれる契機:

| タイミング | トリガー |
|---|---|
| タブ選択時 | `ToggleButtons.OnCharaConfigSelectAsObservable` |
| キャラ切替時 | `SetSelectedIndex()` 内 |
| Start時 | `PlayersStates.Instance` 未初期化ケースのフォールバック |
| 凍結停止後 | `m_StopFreezeConsecutiveButton` クリック後 |

`RefreshUI()` の処理:
1. 武器選択パネルが開いていたら閉じる
2. 武器ボタンの戦闘中グレーアウト
3. パーティーメンバーリスト再構築
4. インデックス範囲補正
5. キャラ名表示
6. パッシブ一覧更新
7. 規格サイクルUI更新
8. 割り込みカウンタートグル同期
9. 凍結停止ボタン表示判定
10. ステータスバナーバインド + 背景切替
11. ナビゲーションボタン有効性更新

---

## 関連仕様書

| 仕様書 | 関連箇所 |
|---|---|
| `USERUI_EyeArea仕様書.md` | USERUI全体の設計原則、TabState/EyeAreaState |
| `戦闘規格・狙い流れ仕様書.md` | 規格サイクルの適応ラグ、戦闘規格の詳細 |
| `割り込みカウンター仕様書.md` | 割り込みカウンタートグルの対象システム |
| `ターゲット強制優先順位仕様書.md` | 吸引・イラつき等のターゲット強制 |

---

## 依存関係

| 依存先 | 用途 |
|---|---|
| `PlayersContextRegistry` / `IPlayersContextConsumer` | DI的なコンテキスト注入 |
| `IPlayersRoster` | キャラクター取得 |
| `IPartyComposition` | パーティー構成取得 |
| `IPlayersSkillUI` | 感情愛着スキル選択 |
| `IPlayersParty` | 凍結停止リクエスト |
| `BattleContextHub` | 戦闘中判定 |
| `StatesBannerController` | ステータス表示 |
| `WeaponSelectArea` | 武器選択 |
| `WeaponManager` | 武器一覧 |
| `ModalAreaController` | モーダル管理 |
| `PassiveTextUtils` | パッシブテキスト整形・計測 |
| `PassivesMordaleAreaController` | パッシブモーダル |
| `ToggleSingleController` | 割り込みカウンタートグル |
| `TenDaysMordaleAreaController` | 十日能力モーダル |
