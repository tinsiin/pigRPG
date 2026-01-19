# Unity Editorデバッグツール一覧

pigRPGプロジェクトで使用可能なUnity Editor拡張ツールのリファレンスです。

---

## 歩行システム関連

### GraphValidator
**ファイル:** `Assets/Editor/Walk/GraphValidator.cs`
**メニュー:** `Walk > Validate Selected Graph`

FlowGraphSOの整合性を検証するツール。

**機能:**
- 孤立ノード検出
- 欠損Edge参照検出
- 必須設定の検証

**使用方法:**
1. Projectウィンドウで検証したいFlowGraphSOを選択
2. メニューから `Walk > Validate Selected Graph` を実行
3. Consoleに結果が出力される

---

### SimRunner (Walk Simulation)
**ファイル:** `Assets/Editor/Walk/SimRunner.cs`
**メニュー:** `Walk > Simulation Runner`

歩行システムのシミュレーションを行うEditorWindow。

**機能:**
- FlowGraphの歩行を手動ステップ実行
- ノード遷移の追跡
- ゲート/出口条件の確認

**使用方法:**
1. メニューから `Walk > Simulation Runner` でウィンドウを開く
2. FlowGraphをドラッグ&ドロップ
3. Step実行で歩行をシミュレート

---

### WalkingSystemManager Debug Inspector
**ファイル:** `Assets/Editor/Walk/WalkingSystemManagerEditor.cs`
**起動:** WalkingSystemManagerを選択（PlayMode中のみ表示）

PlayMode中の歩行システム状態をリアルタイムで監視・操作するインスペクタ拡張。

**表示内容:**
- **Status:** 現在ノード、歩数情報、エンカウント倍率
- **Tags:** 使用中タグの一覧と設定/解除
- **Flags:** フラグ状態の一覧と設定/解除
- **Counters:** カウンター値の一覧と編集
- **Encounter Overlays:** オーバーレイの一覧と追加/削除

**操作ボタン:**
- `Refresh Conditions`: FlowGraphから条件を再収集
- `Clear Debug State`: 全タグ/フラグ/カウンター/オーバーレイをクリア

**補助クラス:**
- `WalkConditionCollector.cs`: FlowGraphから使用条件を収集

---

### WalkPlayModeCleanup
**ファイル:** `Assets/Editor/Walk/WalkPlayModeCleanup.cs`
**起動:** PlayMode終了時に自動実行

PlayMode終了時にスポーンされたオブジェクトを自動クリーンアップする。

**対象:**
- SideObject系のスポーン済みオブジェクト
- CentralObject系のスポーン済みオブジェクト

---

## スキル関連

### SkillTraitValidator
**ファイル:** `Assets/Editor/SkillTraitValidator.cs`
**メニュー:** `Tools > pigRPG > Validate SkillZoneTrait Value`

SkillZoneTraitの値を検証するツール。

**機能:**
- 無効なTrait値の検出
- Trait設定の整合性チェック

---

## システム/ユーティリティ

### AutoSave / SceneBackup
**ファイル:**
- `Assets/Editor/AutoSave.cs`
- `Assets/Editor/SceneBackup.cs`

**設定:** `Edit > Preferences > Auto Save`

シーンの自動保存とバックアップ機能。

**機能:**
- 指定間隔での自動保存
- 手動保存時のバックアップ作成
- バックアップファイルの世代管理

**設定項目:**
- 自動保存の有効/無効
- 保存間隔
- バックアップ保持数

---

### MetricsDefineToggle
**ファイル:** `Assets/Editor/MetricsDefineToggle.cs`
**メニュー:**
- `Tools > Metrics > Enable Metrics`
- `Tools > Metrics > Disable Metrics`

METRICS_DISABLEDシンボル定義の切り替えツール。

**機能:**
- メトリクス収集の有効/無効切り替え
- Scripting Define Symbolsの自動更新

---

### PresetSweepSafeStop
**ファイル:** `Assets/Editor/PresetSweepSafeStop.cs`
**メニュー:** `Tools > Benchmark > Safe Stop Preset Sweep` (Ctrl+Alt+S)
**自動起動:** PlayMode終了時

Preset Sweepを安全に停止するツール。

**機能:**
- PlayMode終了時のズーム状態復元
- スイープ中断時の状態クリーンアップ
- 手動停止コマンド（Ctrl+Alt+S）

---

## カスタムインスペクタ（参考）

以下はデバッグツールではなく、Inspector表示のカスタマイズ用です：

| ファイル | 対象 |
|----------|------|
| `ToggleButtonEditor.cs` | ToggleButton |
| `SideObjectMovePositionEditor.cs` | SideObjectMovePosition |
| `UILineRendererEditor.cs` | UILineRenderer |
| `AttackPowerCoefficientsTableViewEditor.cs` | 攻撃力係数テーブル |
| `PowerCoefficientsTableViewBaseEditor.cs` | 能力係数テーブル基底 |

---

## 更新履歴

- 2025-01: WalkingSystemManagerEditor追加、ドキュメント初版作成
