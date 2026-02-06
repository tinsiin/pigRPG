# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## プロジェクト概要

pigRPG - Unity製Android向けRPG（日本語プロジェクト）
- Unity 2022.3.x / Windows
- ライセンス: The Unlicense（MITライセンスのライブラリを使用）

## 編集制約

**編集可能:**
- `Assets/Script/**/*.cs`
- `doc/` フォルダ
- `*.unity` シーンファイル（Unity MCP経由）
- `*.prefab` プレハブファイル（Unity MCP経由）

**編集禁止:**
- `*.meta` ファイル（Unity自動生成）
- `Packages/`, `ProjectSettings/`, `Assets/Plugins/`

## コードアーキテクチャ

### 階層構造
```
Walking (UI層)
    ↓
WalkingSystemManager (歩行システム)
    ↓ [敵遭遇時]
BattleInitializer
    ↓
BattleOrchestrator (UI入力制御)
    ├→ BattleUIBridge (UI/バトル中継)
    └→ BattleManager (バトルロジック)
        ├→ BattleGroup×2 (敵/味方)
        └→ ActionQueue (行動キュー)
```

### 主要コンポーネント

**BaseStates** (`Assets/Script/BaseStates/`)
- 全キャラクターの基盤クラス（partial classで18ファイルに分割）
- HP/能力値管理、ダメージ計算、状態異常、パッシブスキル

**PlayersRuntime** (`Assets/Script/Players/`)
- プレイヤー3人の統合管理
- PlayersContext経由で依存性注入
- AllyId: Geino, Noramlia, Sites

**BattleManager** (`Assets/Script/Battle/`)
- IBattleContext実装、ターン管理
- BattleGroup: 敵/味方グループ管理
- ActionQueue: 行動順序管理

**歩行システム** (`Assets/Script/Walk/`)
- FlowGraphSO: ノードベースのステージ進行
- Stages: ステージ/エリア/敵データ管理
- NormalEnemy.RecovelySteps: 敵復活メカニズム

### グローバルコンテキスト
```csharp
BattleContextHub.Current      // 現在のバトルコンテキスト
BattleOrchestratorHub.Current // 現在のオーケストレーター
UIStateHub.UserState          // UI状態（R3 ReactiveProperty）
```

## 使用ライブラリ

| 用途 | ライブラリ |
|------|-----------|
| 非同期処理 | UniTask |
| アニメーション | LitMotion |
| 乱数 | NRandom（NRandom.Numericsでベクトル乱数も可） |
| リアクティブ | R3 |

## Unity特有ルール

- MonoBehaviour継承クラス: ファイル名とクラス名を一致させる
- Inspector表示が必要なprivateフィールド: `[SerializeField]`を付ける
- 名前空間: 既存コードに従う

## テスト

unityMCP の `run_tests` / `get_test_job` ツールでUnityテストを実行可能。
- EditMode: `run_tests(mode="EditMode")`
- PlayMode: `run_tests(mode="PlayMode")`

## 新規ファイル作成時の注意

Assetsフォルダ以下を調べ、同一機能・同一役割のファイルが既に存在しないか確認すること。
