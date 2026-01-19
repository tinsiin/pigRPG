# Unity Editorデバッグツール一覧

pigRPGプロジェクトで使用可能なデバッグ用Unity Editor拡張ツールのリファレンスです。

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

### AutoSave
**ファイル:** `Assets/Editor/AutoSave.cs`
**設定:** `Edit > Preferences > Auto Save`
**メニュー:** `File > Backup > Backup / Rollback`

シーンの自動保存とバックアップ機能。

**機能:**
- PlayMode開始時の自動保存
- 指定間隔での自動保存（Hierarchy変更時のみ）
- 手動保存時のバックアップ自動作成
- バックアップからのロールバック

**設定項目:**
- Auto Save: 自動保存の有効/無効
- Save Prefabs: プレハブも保存するか
- Save Scene: シーンを保存するか
- Timer Save: タイマー保存の有効/無効
- Interval: 保存間隔（秒、最小60秒）

---

### MetricsDefineToggle
**ファイル:** `Assets/Editor/MetricsDefineToggle.cs`
**メニュー:**
- `Tools > Metrics > Enable Metrics`
- `Tools > Metrics > Disable Metrics`
- `Tools > Metrics > Enable Metrics (All Common Targets)`
- `Tools > Metrics > Disable Metrics (All Common Targets)`

パフォーマンスメトリクス収集機能の有効/無効を切り替えるツール。

**影響範囲:**
- `PerformanceHUD`: 画面上のパフォーマンス表示
- `MetricsHub`: メトリクスデータの収集・集計

**使用タイミング:**
- **開発中:** Enable（有効）にしてパフォーマンス監視
- **リリースビルド:** Disable（無効）にしてオーバーヘッド削減
- **パフォーマンス調査時:** 一時的にEnableにして計測

**仕組み:**
`METRICS_DISABLED` Scripting Define Symbolを追加/削除することで、メトリクス関連コードをコンパイル時に除外/含める。

---

## 更新履歴

- 2025-01: WalkingSystemManagerEditor追加、ドキュメント初版作成
- 2025-01: 不要ツール削除、MetricsDefineToggle説明改善
