# CharacterDataRegistry統一計画

## 概要

Legacy Config方式（`PlayersBootstrapper.Init_geino`等）を廃止し、CharacterDataRegistry方式に統一する。
同時に「初期パーティメンバー」フラグを追加し、ゲーム開始時のパーティ編成を柔軟に設定可能にする。

---

## 重要な設計原則

### Registry vs Roster の境界

| 概念 | 役割 | 内容 |
|------|------|------|
| **CharacterDataRegistry** | 全キャラデータの「定義」 | 静的、ScriptableObject、全キャラ分 |
| **PlayersRoster** | 解放済みキャラの「ランタイムインスタンス」 | 動的、ゲーム進行で変化 |

```
Registry: 「このゲームに存在するキャラクター一覧」（定義）
Roster:   「現在プレイヤーが使えるキャラクター」（解放状態）
```

**重要**: Rosterには**解放済みキャラのみ**登録する。未解放キャラはRegistryにデータがあっても、Rosterには登録しない。

### 初期化フロー

```
【新規ゲーム】
PlayersBootstrapper.Start()
    ↓
PlayersRuntime.Init()
    ↓
初期パーティメンバーのみRoster登録 + Composition設定

【ロード時】
PlayersBootstrapper.Start() → Init()は呼ばれるが...
    ↓
ApplySaveData()が後から呼ばれる
    ↓
UnlockedCharacterIdsからRoster再構築 + 保存済みComposition復元
```

**注意**: `Init()`の初期パーティ設定は、その後の`ApplySaveData()`で上書きされる。ロード時は保存済み編成が優先される。

---

## 質問への回答

### Q1: 初期パーティ = ゲーム開始時点？
**Yes**。セーブデータがない状態で新規ゲームを開始した時点のパーティ編成。

### Q2: Geino/Noramlia/Sitesはどうやって見分けるの？
`CharacterId.IsOriginalMember`プロパティで判定。ID文字列が"geino", "noramlia", "sites"かどうかで自動判定される。

```csharp
// CharacterId.cs（既存）
public bool IsOriginalMember =>
    this == Geino || this == Noramlia || this == Sites;
```

CharacterDataSOには別途フラグを持たない。ID文字列で自動判定。

### Q3: 固定3人と新キャラを分けるシステムを捨てる必要がある？
**No**。固定3人/新キャラの区分は維持できる。

| 概念 | 説明 | 判定方法 |
|------|------|----------|
| **固定メンバー** | ゲーム開始時から存在するキャラ | `CharacterId.IsOriginalMember` |
| **追加キャラ** | イベントで解放されるキャラ | `!CharacterId.IsOriginalMember` |
| **初期パーティ** | ゲーム開始時にパーティにいるキャラ | `CharacterDataSO.IsInitialPartyMember` ← **新規追加** |

これらは**別の軸**なので、両方維持できる。

### Q4: 既存のGeinoデータはどうやって移行するの？
`CharacterDataSO._template`フィールドに既存の`AllyClass`アセットをそのまま設定する。

```
CharacterDataSO (GeinoData.asset)
├── _id: "geino"
├── _displayName: "ゲイノ"
├── _template: [既存のAllyClass_Geino.asset] ← ここに設定
└── _isInitialPartyMember: true
```

`CharacterDataSO.CreateInstance()`は`_template.DeepCopy()`を呼ぶので、既存データがそのまま使われる。

---

## 現状の問題

### 2系統の初期化パス
```
【Legacy方式】
PlayersBootstrapper (Inspector)
├── Init_geino: AllyClass
├── Init_noramlia: AllyClass
└── Init_sites: AllyClass
    ↓
PlayersRuntime.InitFromLegacyConfig()
    ↓
roster.RegisterAlly()

【CharacterDataRegistry方式】
CharacterDataRegistry (Resources)
└── _characters: List<CharacterDataSO>
    ↓
PlayersRuntime.InitFromCharacterDataRegistry()
    ↓
characterData.CreateInstance()
    ↓
roster.RegisterAlly()
```

**問題点:**
- コードパスが2本あり、メンテナンスコストが高い
- `PlayersRuntimeConfig.UseCharacterDataRegistry`フラグで分岐している
- 初期パーティを設定する明確な方法がない

---

## 設計方針

### 統一後の構造（初期段階）
```
CharacterDataRegistry (Resources/CharacterDataRegistry.asset)
└── _characters:
    └── GeinoData.asset     (IsOriginalMember=自動判定, IsInitialParty=true)
```

**Note**: Noramlia/Sites/新キャラのSOは必要になった時点で追加する。

### CharacterDataSOの変更
```csharp
public sealed class CharacterDataSO : ScriptableObject
{
    // 既存フィールド...

    [Header("初期パーティ設定")]
    [Tooltip("ゲーム開始時にパーティに含めるかどうか")]
    [SerializeField] private bool _isInitialPartyMember;

    /// <summary>初期パーティメンバーかどうか</summary>
    public bool IsInitialPartyMember => _isInitialPartyMember;

    /// <summary>固定メンバー（Geino/Noramlia/Sites）かどうか（ID文字列で自動判定）</summary>
    public bool IsOriginalMember => Id.IsOriginalMember;
}
```

### CharacterDataRegistryの変更
```csharp
public sealed class CharacterDataRegistry : ScriptableObject
{
    // 既存メソッド...

    /// <summary>
    /// 初期パーティメンバーのデータを取得する。
    /// </summary>
    public IEnumerable<CharacterDataSO> GetInitialPartyMembers()
    {
        foreach (var data in _characters)
        {
            if (data != null && data.IsInitialPartyMember)
            {
                yield return data;
            }
        }
    }
}
```

### PlayersRuntimeの変更（修正版）
```csharp
public void Init()
{
    var registry = CharacterDataRegistry.Instance;
    if (registry == null)
    {
        Debug.LogError("PlayersRuntime.Init: CharacterDataRegistry が見つかりません");
        return;
    }

    // ★重要: 初期パーティメンバーのみRoster登録（全キャラではない）
    var initialParty = new List<CharacterId>();
    foreach (var characterData in registry.GetInitialPartyMembers())
    {
        if (characterData == null)
        {
            Debug.LogWarning("PlayersRuntime.Init: null の CharacterDataSO がスキップされました");
            continue;
        }

        var id = characterData.Id;
        if (!id.IsValid)
        {
            Debug.LogError($"PlayersRuntime.Init: 無効なID '{characterData.name}'");
            continue;
        }

        if (roster.IsUnlocked(id))
        {
            Debug.LogWarning($"PlayersRuntime.Init: {id} は既に登録済みです（重複スキップ）");
            continue;
        }

        var instance = characterData.CreateInstance();
        if (instance == null)
        {
            Debug.LogError($"PlayersRuntime.Init: {id} のインスタンス生成に失敗しました（_templateが未設定？）");
            continue;
        }

        roster.RegisterAlly(id, instance);
        initialParty.Add(id);
        Debug.Log($"PlayersRuntime.Init: {id} を初期パーティとして登録");
    }

    // 初期パーティ編成
    if (initialParty.Count > 0)
    {
        composition.SetMembers(initialParty.ToArray());
    }
    else
    {
        Debug.LogWarning("PlayersRuntime.Init: 初期パーティメンバーが0人です");
    }

    // 以下、既存の初期化処理...
    BindAllyContext();
    // ...
}
```

### セーブ時の処理（PlayersSaveService.Build）
```csharp
public PlayersSaveData Build(PlayersRoster roster, IPartyComposition composition)
{
    var data = new PlayersSaveData();

    // ★ UnlockedCharacterIds は Roster から生成する（唯一の情報源）
    data.UnlockedCharacterIds = roster.AllIds.Select(id => id.Value).ToList();

    // ActivePartyIds
    data.ActivePartyIds = composition.ActiveMemberIds.Select(id => id.Value).ToList();

    // Allies（各キャラのステータス）
    data.Allies = new List<PlayersAllySaveData>();
    foreach (var id in roster.AllIds)
    {
        var ally = roster.GetAlly(id);
        if (ally != null)
        {
            data.Allies.Add(BuildAllyData(ally));
        }
    }

    return data;
}
```

### ロード時の処理（PlayersSaveService.Apply）
```csharp
public void Apply(PlayersSaveData data, PlayersRoster roster, IPartyComposition composition)
{
    if (data == null) return;

    var registry = CharacterDataRegistry.Instance;
    if (registry == null)
    {
        Debug.LogError("PlayersSaveService.Apply: CharacterDataRegistry が見つかりません");
        return;
    }

    // Rosterをクリア（Init()で登録された初期パーティを上書き）
    roster.Clear();

    // 解放済みキャラをRosterに復元
    var unlockedIds = data.UnlockedCharacterIds ?? new List<string>();
    foreach (var idStr in unlockedIds)
    {
        var id = new CharacterId(idStr);
        if (!id.IsValid)
        {
            Debug.LogWarning($"PlayersSaveService.Apply: 無効なID '{idStr}' をスキップ");
            continue;
        }

        var characterData = registry.GetCharacter(id);
        if (characterData == null)
        {
            Debug.LogError($"PlayersSaveService.Apply: {id} のCharacterDataSOが見つかりません");
            continue;
        }

        var instance = characterData.CreateInstance();
        if (instance == null)
        {
            Debug.LogError($"PlayersSaveService.Apply: {id} のインスタンス生成に失敗");
            continue;
        }

        roster.RegisterAlly(id, instance);
    }

    // Alliesからステータス復元
    foreach (var allyData in data.Allies ?? new List<PlayersAllySaveData>())
    {
        var id = new CharacterId(allyData.CharacterId);
        var ally = roster.GetAlly(id);
        if (ally != null)
        {
            ApplyAllyData(ally, allyData);
        }
        else
        {
            // AlliesにあるがRosterにない → 整合性復旧（Rosterに追加）
            Debug.LogWarning($"PlayersSaveService.Apply: {id} がRosterになかったため復旧");
            var characterData = registry.GetCharacter(id);
            if (characterData != null)
            {
                var instance = characterData.CreateInstance();
                if (instance != null)
                {
                    roster.RegisterAlly(id, instance);
                    ApplyAllyData(instance, allyData);
                }
            }
        }
    }

    // パーティ編成を復元
    if (data.ActivePartyIds != null && data.ActivePartyIds.Count > 0)
    {
        var partyIds = data.ActivePartyIds
            .Select(s => new CharacterId(s))
            .Where(id => roster.IsUnlocked(id))  // 解放済みのみ
            .ToArray();

        if (partyIds.Length > 0)
        {
            composition.SetMembers(partyIds);
        }
        else
        {
            // ActivePartyIdsの全員がRosterにいない場合 → フォールバック
            Debug.LogWarning("PlayersSaveService.Apply: ActivePartyIdsが全て無効、Roster全員をパーティに設定");
            composition.SetMembers(roster.AllIds.ToArray());
        }
    }
    else
    {
        // ★ ActivePartyIdsが空/欠落 → Roster全員をパーティに設定
        Debug.LogWarning("PlayersSaveService.Apply: ActivePartyIdsが空、Roster全員をパーティに設定");
        composition.SetMembers(roster.AllIds.ToArray());
    }
}
```

### PlayersRuntime.ApplySaveData()（ロード後の再バインド）
```csharp
public void ApplySaveData(PlayersSaveData data)
{
    saveService.Apply(data, roster, composition);

    // ★ ロード後にUI/コンテキストを再バインド
    // （Init()で設定されたバインドは古いRosterを参照しているため）
    BindAllyContext();

    // スキルボタンを再バインド
    ApplySkillButtons();
    UpdateSkillButtonVisibility();
}
```

**Note**: `BindAllyContext()`は内部で`ClearButtonFunc()`してから追加するため、重複バインドの問題はない。

---

## マイグレーション計画

### Phase 1: CharacterDataSOにフラグ追加
| ファイル | 変更内容 |
|----------|----------|
| `CharacterDataSO.cs` | `_isInitialPartyMember`フィールド追加 |
| `CharacterDataRegistry.cs` | `GetInitialPartyMembers()`メソッド追加 |

### Phase 2: GeinoデータSO作成
| 作業 | 詳細 |
|------|------|
| SOアセット作成 | `Assets/Resources/Characters/GeinoData.asset` |
| ID設定 | `_id: "geino"` |
| テンプレート設定 | `_template: [既存のAllyClass_Geino.asset]` |
| 初期パーティ設定 | `_isInitialPartyMember: true` |

**既存AllyClassアセットの場所**: PlayersBootstrapper Inspector の `Init_geino` から参照を確認

**Note**: Noramlia/SitesのSOは後から必要になった時点で作成する。初期パーティに含まれないキャラはRegistryに登録しなくてもバグは発生しない。

### Phase 3: CharacterDataRegistry設定
| 作業 | 詳細 |
|------|------|
| Registryアセット確認 | `Assets/Resources/CharacterDataRegistry.asset` |
| キャラ登録 | `_characters`リストにGeinoData.assetを追加 |
| バリデーション実行 | ContextMenu "Auto-collect Character Data" |

### Phase 4: PlayersRuntime統一
| ファイル | 変更内容 |
|----------|----------|
| `PlayersRuntime.cs` | `InitFromLegacyConfig()`削除 |
| `PlayersRuntime.cs` | `Init()`を修正版に置き換え |
| `PlayersRuntime.cs` | Legacy関連フィールド削除 |
| `PlayersRuntimeConfig.cs` | Legacy関連フィールド削除 |

### Phase 5: PlayersSaveService修正
| ファイル | 変更内容 |
|----------|----------|
| `PlayersSaveService.cs` | `Apply()`でRosterクリア＋再構築 |
| `PlayersSaveService.cs` | 整合性復旧ロジック追加 |
| `PlayersRoster.cs` | `Clear()`メソッド追加（必要なら） |

### Phase 6: PlayersBootstrapperクリーンアップ
| ファイル | 変更内容 |
|----------|----------|
| `PlayersBootstrapper.cs` | `Init_geino`, `Init_noramlia`, `Init_sites`削除 |
| `PlayersBootstrapper.cs` | `UseCharacterDataRegistry`フラグ削除 |

### Phase 7: テスト
| テスト項目 | 確認内容 |
|------------|----------|
| **新規ゲーム開始** | 初期パーティメンバーのみがRosterに登録される |
| **初期パーティ確認** | `IsInitialPartyMember=true`のキャラのみがCompositionにいる |
| **バトル開始** | パーティメンバーが正常に動作する |
| **UIバインド** | スキルボタン等が正常に機能する |
| **セーブ** | UnlockedCharacterIds、ActivePartyIds、Alliesが保存される |
| **ロード** | 保存済み編成が復元される（Init()の設定を上書き） |
| **ロード後バトル** | ロード後のパーティで正常にバトルできる |
| **解放ゲート** | 新キャラ解放Effect後にRoster登録される |
| **解放後セーブロード** | 解放したキャラがロード後も維持される |

---

## 削除対象コード

### PlayersRuntimeConfig
```csharp
// 削除
public bool UseCharacterDataRegistry;
public AllyClass InitGeino;
public AllyClass InitNoramlia;
public AllyClass InitSites;
```

### PlayersRuntime
```csharp
// 削除
private bool useCharacterDataRegistry;
private AllyClass initGeino;
private AllyClass initNoramlia;
private AllyClass initSites;

private void InitFromLegacyConfig() { ... }
private void BindTemplateContext() { ... }  // BindAllyContextに統合
```

### PlayersBootstrapper
```csharp
// 削除
public AllyClass Init_geino;
public AllyClass Init_noramlia;
public AllyClass Init_sites;
```

---

## 設定例

### Geinoのみでテスト（現在の計画）
```
CharacterDataRegistry
└── _characters:
    └── GeinoData.asset     (IsInitialParty=true)  ← Roster登録＋パーティ
```
Noramlia/SitesのSOは未作成。必要になった時点で作成する。

### 将来：全員でテスト
```
CharacterDataRegistry
└── _characters:
    ├── GeinoData.asset     (IsInitialParty=true)  ← Roster登録＋パーティ
    ├── NoramliaData.asset  (IsInitialParty=true)  ← Roster登録＋パーティ
    └── SitesData.asset     (IsInitialParty=true)  ← Roster登録＋パーティ
```

### 将来：新キャラ追加後
```
CharacterDataRegistry
└── _characters:
    ├── GeinoData.asset     (IsInitialParty=true)
    ├── NoramliaData.asset  (IsInitialParty=true)
    ├── SitesData.asset     (IsInitialParty=true)
    └── NewCharData.asset   (IsInitialParty=false) ← Roster未登録（イベント解放待ち）
```

---

## 実装順序

1. **CharacterDataSO.cs** - `_isInitialPartyMember`追加
2. **CharacterDataRegistry.cs** - `GetInitialPartyMembers()`追加
3. **GeinoData.asset作成** - 既存AllyClassをテンプレートに設定
4. **CharacterDataRegistry.asset更新** - GeinoData.assetを登録
5. **PlayersRoster.cs** - `Clear()`メソッド追加（必要なら）
6. **PlayersSaveService.cs** - `Apply()`修正
7. **PlayersRuntime.cs** - Legacy削除、Init()修正
8. **PlayersRuntimeConfig.cs** - Legacy削除
9. **PlayersBootstrapper.cs** - Legacy削除
10. **テスト実行**

**Note**: Noramlia/SitesのSOは、それらを初期パーティに含めたい時点で作成する。

---

## バリデーション方針

### CharacterDataSO.CreateInstance()
```csharp
public AllyClass CreateInstance()
{
    if (_template == null)
    {
        Debug.LogError($"CharacterDataSO.CreateInstance: {_id} の _template が null です");
        return null;
    }
    // ...
}
```

### CharacterDataRegistry
```csharp
// エディタ用バリデーション（既存）
[ContextMenu("Auto-collect Character Data")]
private void AutoCollectCharacterData() { ... }

// ランタイムバリデーション
private void EnsureCacheInitialized()
{
    // 重複IDチェック（既存）
    if (_lookupCache.ContainsKey(id))
    {
        Debug.LogWarning($"CharacterDataRegistry: 重複するID '{id}'");
        continue;
    }
}
```

### PlayersRuntime.Init()
- null CharacterDataSO → LogWarning + スキップ
- 無効なID → LogError + スキップ
- 重複登録 → LogWarning + スキップ
- CreateInstance失敗 → LogError + スキップ
- 初期パーティ0人 → LogWarning

### PlayersSaveService.Apply()
- AlliesにあるがRosterにない → LogWarning + 整合性復旧（Rosterに追加）
- CharacterDataSO見つからない → LogError + スキップ
- 未解放キャラがActivePartyにいる → スキップ
- ActivePartyIdsが空/欠落 → LogWarning + Roster全員をパーティに設定
- ActivePartyIds全員がRosterにいない → LogWarning + Roster全員をパーティに設定

---

## セーブデータ互換性

### 前提条件
1. `Init()`は常に実行され、初期パーティをRoster/Compositionに設定する
2. ロード時は`ApplySaveData()`が`Init()`の設定を**上書き**し、最終状態を決定する
3. `UnlockedCharacterIds`と`Allies`の整合性は`Apply()`内で復旧

### セーブ時の原則
- **UnlockedCharacterIds は Roster.AllIds から生成**（唯一の情報源）
- Roster に登録されているキャラ = 解放済みキャラ
- セーブ時に別途 UnlockedCharacterIds を管理する必要はない

### ロード時のフォールバック
| 状況 | 対応 |
|------|------|
| UnlockedにあるがAlliesにない | CharacterDataSO.CreateInstance()で初期状態生成 |
| AlliesにあるがRosterにない | Rosterに追加して復旧 |
| CharacterDataSOが見つからない | エラーログ、そのキャラをスキップ |
| ActivePartyに未解放キャラ | そのIDをスキップ |
| **ActivePartyIdsが空/欠落** | **Roster全員をパーティに設定** |
| **ActivePartyIds全員がRosterにいない** | **Roster全員をパーティに設定** |

### フロー図
```
【新規ゲーム】
Start() → Init() → 初期パーティのみRoster登録 → Composition設定 → BindAllyContext() → ゲーム開始

【ロード時】
Start() → Init() → 初期パーティRoster登録 → Composition設定 → BindAllyContext()
                            ↓
         ApplySaveData() → Roster.Clear() → UnlockedからRoster再構築
                            ↓
                   Composition復元（空ならRoster全員）
                            ↓
                   ★BindAllyContext()再実行 → ゲーム開始
```

**重要**: ロード時は`ApplySaveData()`内で`BindAllyContext()`を再実行する。これにより、新しいRosterに対してUI/コンテキストが正しくバインドされる。

---

## 注意事項

- **既存のAllyClassアセットは削除しない**: CharacterDataSOの`_template`として再利用
- **CharacterDataRegistryはResourcesフォルダに配置**: `Resources.Load`でアクセスするため
- **IsOriginalMemberは自動判定**: CharacterDataSOにフラグを追加する必要なし
- **Rosterには解放済みのみ登録**: Registry≠Roster の境界を守る
- **ロード時はInit()の設定が上書きされる**: ApplySaveData()が後から呼ばれる
- **SOは必要なキャラのみ作成**: 初期パーティに含めないキャラのSOは後から作成でOK
