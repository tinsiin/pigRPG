# PlayersStates リファクタリング設計案（改善提案・具体化）

## 目的
- `PlayersStates` の責務過多（状態/ロジック/UI参照/初期化が同居）を解消する。
- UI側の参照・操作と、ゲーム状態（永続的データ/ランタイム）を分離して保守性を上げる。
- インスペクタでの参照は維持しつつ、構造化して「散らかり」を減らす。

## 現状の課題（要約）
- `PlayersStates` が UI参照とUI操作を直接持つため、仕様変更が波及しやすい。
- `DefaultButtonArea[]` などの並列配列が多く、インデックス不整合の温床。
- 1ファイル内に `AllyClass` / `AllySkill` まで含まれ、読みづらい。
- UI更新が直接呼び出しで密結合（イベント駆動にしづらい）。

## 改善方針（責務分離）
- **PlayersStates**: 状態（Progress/Roster/Tuning/Party）とサービス初期化のみ。
- **PlayersUIRefs**: UI参照の集約（インスペクタの入口）だけを担当。
- **PlayersUIService / UI Facade**: UIの更新・画面遷移を担当。
- **Ally/Skill 実体**: データ・ロジックのみ。UI参照は持たない。

## 新しい構成（ファイル/責務の具体案）
### 1) Core
- `Assets/Script/Players/PlayersStates.cs`
  - シングルトン管理
  - Servicesの生成/DI
  - Progress/Roster/Tuning/Party に触れる窓口

### 2) UI参照（インスペクタ集約）
- `Assets/Script/Players/UI/PlayersUIRefs.cs`
  - UI参照のみを持つ MonoBehaviour
  - `PlayersStates` はここから参照を受け取り `PlayersUIService` に渡す

### 3) UI操作
- `Assets/Script/Players/UI/PlayersUIService.cs`（既存）
  - UI更新、ボタン有効/無効、画面切り替え
- `Assets/Script/Players/UI/PlayersUIFacade.cs`（新規）
  - UI操作の窓口（PlayersStatesが呼ぶ）

### 4) データ/ロジック
- `Assets/Script/Players/Runtime/AllyClass.cs`
- `Assets/Script/Players/Runtime/AllySkill.cs`
- 既存の `PlayersProgressTracker`, `PlayersRoster`, `PlayersTuningConfig` を継続使用

## UI参照の整理（並列配列をやめる）
### 現状
- `GameObject[] DefaultButtonArea`, `Button[] DoNothingButton` など並列配列が多数

### 提案
- `AllyUISet` を定義し、キャラ単位でUIをまとめる

例:
```csharp
[Serializable]
public class AllyUISet {
  public GameObject DefaultButtonArea;
  public Button DoNothingButton;
  public SelectCancelPassiveButtons CancelPassiveButtonField;
  public Button GoToCancelPassiveFieldButton;
  public Button ReturnCancelPassiveToDefaultAreaButton;
  public AllySkillUILists SkillUILists;
}
```

- `PlayersUIRefs` に `AllyUISet[]` を持たせる
- `PlayersUIService` は `AllyId` で `AllyUISet` を引く（配列の暗黙整合を減らす）

## インスペクタの集約はどうするべきか？
### 結論
- **「入口は1つ」だが、内部は分割**が最適。

### 具体化
- `Managers` に **PlayersUIRefs** を置き、そこを入口にする。
- その内部は画面ごとに以下のように分割:
  - `SkillSelectionUIRefs`
  - `CancelPassiveUIRefs`
  - `ModalUIRefs`
  - `EmotionalAttachmentUIRefs`

### こうする理由
- インスペクタ1つで追える利点は残る
- UIの用途ごとにまとまるため、参照の混線が減る

## 移行ステップ（具体化）
### Phase 1: ファイル分割（動作影響なし）
- `AllyClass` / `AllySkill` / UI補助クラスを別ファイルへ
- `PlayersStates` の可読性を上げる

### Phase 2: UI参照を `PlayersUIRefs` へ移管
- `PlayersStates` から `SerializeField` UI参照を削除
- `PlayersUIRefs` が参照を持ち、`PlayersStates` は注入を受ける

#### Phase 2 詳細: Unity 操作と安全な移行手順
**結論:** MCP の `set_property` で直接コピーするより、**Editor 移行スクリプト**を作って `execute_menu_item` で実行するのが最安全。  
（MCP はネスト配列/参照の一括コピーが苦手で、人力での詰め替えはミスが出やすい）

**具体手順**
1. `PlayersUIRefs` を `Managers` に追加（UI参照の入口を1つに保つ）
2. `Editor/PlayersUIRefsMigration.cs` を作成  
   - メニュー: `Tools/Players/Migrate UI Refs`  
   - `PlayersStates` → `PlayersUIRefs` へ参照をコピー  
   - `Undo.RecordObject` / `EditorUtility.SetDirty` を使う  
   - 未設定/不足参照を検出してログ出力
3. MCP から `execute_menu_item` で移行を実行  
4. Unity で参照が正しく移行されたことを確認  
5. `PlayersStates` 側の旧 `SerializeField` を削除（ここで初めて分離が完了）

**MCP 直操作を採用しない理由**
- `skillUILists` のようなネスト構造や `ButtonAndSkillIDHold` の参照コピーが複雑
- 参照抜けの検知が難しく、失敗した際の復旧が重い

### Phase 3: AllyUISet へ置き換え
- 並列配列を廃止
- `PlayersUIService` の引数を `AllyUISet` ベースに変更

### Phase 4: UI操作の窓口を `PlayersUIFacade` に統一
- `PlayersStates` から UI操作メソッドを移動
- UI更新はFacadeに委譲

### Phase 5: イベント駆動への寄せ（任意）
- 状態変化をイベントで通知し、UI側が購読

## 期待できる効果
- 仕様変更時の影響範囲が明確になる
- UI参照の整理でバグを減らせる
- PlayersStatesの役割が単純化される

## 懸念と対策
- 移行中はUI参照漏れが起きやすい → `PlayersUIRefs` で一括点検
- インスペクタの差し替え作業が発生 → Phase 2 で一度だけやる
 - Editor移行スクリプトを用意し、MCPから一括実行できるようにして人的ミスを減らす

## 決定事項（次のアクション）
- 本設計に沿って、Phase 1〜3までを先行で実施する
- Phase 4以降は実装の様子を見て調整

## クローズ
- Phase 1〜5 を完了。
- 追加のリファクタリング計画は `doc/PlayersStates_Refactor_MasterPlan.md` に移行。
- 本ドキュメントは完了扱い。