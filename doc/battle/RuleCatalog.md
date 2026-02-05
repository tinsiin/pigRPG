# Rule Catalog (互換レイヤー)

## 目的
- データ駆動化の入口を作る
- 既存仕様を維持したまま差し替え可能にする

## 構成
- `BattleRuleCatalog`  
  ルールIDのリスト（データ側の定義）
- `BattleRuleRegistry`  
  ルールID → 実装のマッピング

## デフォルトID
### Targeting
- `targeting.single`
- `targeting.all`
- `targeting.random_single`
- `targeting.random_multi`

### Effects
- `effect.flat_roze`
- `effect.help_recovery`
- `effect.revenge_bonus`

## 互換性
- Catalog 未指定 or 空の場合、既存の `CreateDefault()` を使用
- ID不一致でも全て未解決なら既定のルールにフォールバック

## 使い方（例）
```csharp
var catalog = BattleRuleCatalog.CreateDefault();
var registry = BattleRuleRegistry.CreateDefault();

var targeting = registry.BuildTargetingPolicies(catalog);
var effects = registry.BuildEffectPipeline(catalog);

var services = new BattleServices(
    messageDropper,
    skillUi,
    roster,
    targetingPolicies: targeting,
    effectPipeline: effects);
```

## 読み込み（JSON）
```csharp
if (BattleRuleCatalogIO.TryLoadDefault(out var catalog))
{
    var registry = BattleRuleRegistry.CreateDefault();
    var targeting = registry.BuildTargetingPolicies(catalog);
    var effects = registry.BuildEffectPipeline(catalog);
}
```

## BattleInitializerの自動読み込み
- `BattleInitializer` は `battle_rules.json` が存在する場合、戦闘開始時に自動で読み込みます。
- 明示的に差し替えたい場合は `InitializeBattle(..., ruleCatalogOverride, ruleRegistryOverride)` を使います。
