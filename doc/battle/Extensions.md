# Battle Extensions

## 目的
- ルール/AI/ターゲティングの差し替えをプラグイン的に行えるようにする

## 最小API
- `IBattleExtension`
  - `Info` (id/version/author)
  - `Register(BattleRuleRegistry registry)`
- `BattleExtensionRegistry`
  - `Register(IBattleExtension ext)`
  - `ApplyTo(BattleRuleRegistry registry)`
- `BattleExtensionRegistryHub`
  - `Set / Clear`

## 使い方（例）
```csharp
public sealed class DemoExtension : IBattleExtension
{
    public BattleExtensionInfo Info => new BattleExtensionInfo(
        id: "demo.effect",
        version: "1.0.0",
        apiVersion: "1.0",
        author: "you");

    public void Register(BattleRuleRegistry registry)
    {
        registry.RegisterEffect("effect.demo", () => new DemoSkillEffect());
    }
}

var registry = new BattleExtensionRegistry();
registry.Register(new DemoExtension());
BattleExtensionRegistryHub.Set(registry);
```

## 適用タイミング
- `BattleInitializer` が `BattleRuleRegistry` を作るタイミングで `ApplyTo` が呼ばれる

## 互換性ポリシー
- `BattleExtensionCompatibilityPolicy.ApiVersion` と `IBattleExtension.Info.ApiVersion` の互換をチェック
- 既定では **メジャーバージョン一致** を要求
- `BattleExtensionRegistryHub.Set(registry, policy)` で上書き可能

### バージョン運用の方針
- `Info.ApiVersion` は **拡張API互換性** を示す
  - 破壊的変更が入ったら **メジャーを上げる**
  - 互換性を保った追加は **マイナー/パッチ**
- `Info.Version` は **拡張自身のバージョン**
- 既定ポリシーは **メジャー一致のみ必須**

### 互換性ポリシー例
```csharp
var policy = new BattleExtensionCompatibilityPolicy
{
    ApiVersion = "1.0",
    RequireSameMajor = true,
    AllowUnknownApiVersion = false
};

var registry = new BattleExtensionRegistry();
registry.Register(new DemoExtension());
BattleExtensionRegistryHub.Set(registry, policy);
```

### 適用レポート例
```csharp
var ruleRegistry = BattleRuleRegistry.CreateDefault();
var report = registry.ApplyTo(ruleRegistry, policy);
foreach (var skip in report.Skipped)
{
    Debug.LogWarning($"Skip: {skip.Id} ({skip.Reason})");
}
```
