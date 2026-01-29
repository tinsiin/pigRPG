# パーティUI配置システム設計

## 概要

新パーティシステム導入に伴い、BattleIconUI（バトル時のキャラアイコン）の配置を動的に管理するシステムを設計する。

## 現状

- BattleIconUIは3人固定（Geino, BassJack/Normlia, Satelite/Sites）
- シーン内で位置・spriteが直接設定されている
- 位置: `AlwaysCanvas/EyeArea/ViewportArea/FrontFixedContainer/BattleContent/Charas/`

## 要件

1. **パーティ人数**: 最大3人（変更なし）
2. **UI位置**: Left, Center, Right の3スロット
3. **オリジナルメンバーの固定位置**:
   - Geino → 固定位置（要決定: Left/Center/Right）
   - Normlia → 固定位置（要決定: Left/Center/Right）
   - Sites → 固定位置（要決定: Left/Center/Right）
4. **新メンバーの配置**: オリジナルメンバーが抜けた位置に、パーティ順で左から詰めて配置
5. **Sprite設定**: AllyClass（CharacterDataSO）からspriteを取得して設定

## 設計

### 1. UIスロット管理

```
┌─────────┬─────────┬─────────┐
│  Left   │ Center  │  Right  │
│ (Slot0) │ (Slot1) │ (Slot2) │
└─────────┴─────────┴─────────┘
```

### 2. オリジナルメンバー固定位置マッピング

```csharp
// CharacterIdごとの固定スロット（要確認）
public enum UISlot { Left = 0, Center = 1, Right = 2 }

// 例:
// Geino → Left
// Normlia → Center
// Sites → Right
```

**要確認**: 各オリジナルメンバーの固定位置を決定してください。

### 3. 配置ロジック

```
入力: パーティメンバーリスト（順序付き、最大3人）
出力: 各UIスロットに配置するキャラクター

アルゴリズム:
1. オリジナルメンバーを固定位置に配置
2. 空きスロットを左から順に特定
3. 新メンバーをパーティ順で空きスロットに配置
```

#### 配置例

**例1: オリジナル3人**
```
パーティ: [Geino, Normlia, Sites]
結果: Left=Geino, Center=Normlia, Right=Sites
```

**例2: Normliaが抜けて新メンバーA**
```
パーティ: [Geino, NewMemberA, Sites]
結果: Left=Geino, Center=NewMemberA, Right=Sites
（NewMemberAは空いたCenterに入る）
```

**例3: Geino, Sitesが抜けて新メンバーA, B**
```
パーティ: [Normlia, NewMemberA, NewMemberB]
結果: Left=NewMemberA, Center=Normlia, Right=NewMemberB
（Normliaは固定位置Center、新メンバーは空きスロットに左から配置）
```

**例4: 全員新メンバー**
```
パーティ: [NewMemberA, NewMemberB, NewMemberC]
結果: Left=NewMemberA, Center=NewMemberB, Right=NewMemberC
（固定位置なし、パーティ順で左から配置）
```

### 4. データ構造

#### CharacterDataSO に追加

```csharp
[CreateAssetMenu]
public class CharacterDataSO : ScriptableObject
{
    // 既存フィールド...

    [Header("UI設定")]
    [Tooltip("バトルアイコン用スプライト")]
    public Sprite BattleIconSprite;

    [Tooltip("オリジナルメンバーの場合の固定UIスロット（-1 = 固定なし）")]
    public int FixedUISlot = -1;  // 0=Left, 1=Center, 2=Right, -1=新メンバー
}
```

#### BattleIconUI に追加

```csharp
public class BattleIconUI : MonoBehaviour
{
    // 既存フィールド...

    /// <summary>
    /// アイコンスプライトを設定
    /// </summary>
    public void SetIconSprite(Sprite sprite)
    {
        if (Icon != null && sprite != null)
        {
            Icon.sprite = sprite;
        }
    }
}
```

#### 新規: PartyUISlotManager

```csharp
/// <summary>
/// パーティUIスロットの配置管理
/// </summary>
public class PartyUISlotManager
{
    public enum Slot { Left = 0, Center = 1, Right = 2 }

    private readonly BattleIconUI[] _slots = new BattleIconUI[3];

    /// <summary>
    /// UIスロットを初期化（シーンの3つのBattleIconUIを登録）
    /// </summary>
    public void Initialize(BattleIconUI left, BattleIconUI center, BattleIconUI right)
    {
        _slots[0] = left;
        _slots[1] = center;
        _slots[2] = right;
    }

    /// <summary>
    /// パーティメンバーをUIスロットに配置
    /// </summary>
    public void AssignPartyToSlots(IReadOnlyList<BaseStates> partyMembers)
    {
        // 1. 全スロットをクリア
        ClearAllSlots();

        // 2. 固定位置メンバーを配置
        var assignedSlots = new bool[3];
        var unassignedMembers = new List<BaseStates>();

        foreach (var member in partyMembers)
        {
            var fixedSlot = GetFixedSlot(member);
            if (fixedSlot >= 0 && fixedSlot < 3)
            {
                AssignToSlot(member, fixedSlot);
                assignedSlots[fixedSlot] = true;
            }
            else
            {
                unassignedMembers.Add(member);
            }
        }

        // 3. 新メンバーを空きスロットに左から配置
        var slotIndex = 0;
        foreach (var member in unassignedMembers)
        {
            while (slotIndex < 3 && assignedSlots[slotIndex])
            {
                slotIndex++;
            }
            if (slotIndex < 3)
            {
                AssignToSlot(member, slotIndex);
                assignedSlots[slotIndex] = true;
                slotIndex++;
            }
        }
    }

    private int GetFixedSlot(BaseStates member)
    {
        // CharacterDataSOから固定スロットを取得
        var dataRegistry = CharacterDataRegistry.Instance;
        if (dataRegistry == null) return -1;

        var data = dataRegistry.GetData(member.CharacterId);
        return data?.FixedUISlot ?? -1;
    }

    private void AssignToSlot(BaseStates member, int slotIndex)
    {
        var ui = _slots[slotIndex];
        if (ui == null) return;

        // BattleIconUIをバインド
        member.BindBattleIconUI(ui);

        // スプライト設定
        var dataRegistry = CharacterDataRegistry.Instance;
        var data = dataRegistry?.GetData(member.CharacterId);
        if (data?.BattleIconSprite != null)
        {
            ui.SetIconSprite(data.BattleIconSprite);
        }

        // UIを有効化
        ui.gameObject.SetActive(true);
        ui.Init();
    }

    private void ClearAllSlots()
    {
        foreach (var slot in _slots)
        {
            if (slot != null)
            {
                slot.gameObject.SetActive(false);
            }
        }
    }
}
```

### 5. 統合ポイント

#### PlayersRuntime での変更

```csharp
public class PlayersRuntime
{
    private PartyUISlotManager _uiSlotManager;

    private void BindAllyContext()
    {
        // 既存のCharacterUIRegistry経由のバインドを削除
        // 代わりにPartyUISlotManagerを使用

        if (_uiSlotManager == null)
        {
            InitializeUISlotManager();
        }

        _uiSlotManager.AssignPartyToSlots(roster.CurrentParty);
    }

    private void InitializeUISlotManager()
    {
        // シーンから3つのBattleIconUIを取得
        // 方法1: CharacterUIRegistryから取得
        // 方法2: タグ/名前で検索
        // 方法3: SerializeFieldで参照
    }
}
```

### 6. 実装タスク

1. [ ] CharacterDataSOに `BattleIconSprite`, `FixedUISlot` フィールド追加
2. [ ] BattleIconUIに `SetIconSprite()` メソッド追加
3. [ ] PartyUISlotManager クラス作成
4. [ ] PlayersRuntimeにPartyUISlotManager統合
5. [ ] 各オリジナルメンバーのCharacterDataSOに固定スロット設定
6. [ ] CharacterUIRegistryの役割見直し（BattleIconUI管理を移管？）

### 7. 既知の問題（設計完了後に対応）

- [ ] **CharacterUnlockEffect でBattleIconUIがバインドされない**
  - 新しく解放されたキャラクターに `BattleIconUI` がバインドされていない
  - バトル参加時にUIが表示されない可能性
  - **対応**: PartyUISlotManager 実装時に、パーティー変更時の動的バインドで解決予定

### 8. 未決定事項

- [ ] オリジナルメンバーの固定位置（Left/Center/Right）を決定
  - Geino → ?
  - Normlia → ?
  - Sites → ?

- [ ] PartyUISlotManagerの配置場所（PlayersRuntime内？独立MonoBehaviour？）

- [ ] CharacterUIRegistryとの役割分担
  - CharacterUIRegistry: AllyUISet（スキルUI）管理
  - PartyUISlotManager: BattleIconUI配置管理
  - または統合？

## 関連ファイル

- `Assets/Script/Players/CharacterDataSO.cs`
- `Assets/Script/Players/CharacterDataRegistry.cs`
- `Assets/Script/Players/UI/CharacterUIRegistry.cs`
- `Assets/Script/EYEAREA_UI/BattleIconUI.cs`
- `Assets/Script/Players/PlayersRuntime.cs`
