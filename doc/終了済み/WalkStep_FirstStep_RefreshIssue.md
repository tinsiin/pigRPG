# Walk Step First-Step Non-Decrement Issue

## Summary
The first Walk press sometimes did not reduce the remaining steps to the next gate.
The original hypothesis was a stale refresh-only flag short-circuiting the step.
Later reproduction attempts did not confirm that flag, so the root cause is still
unconfirmed. Two candidate explanations are recorded below.

## Symptom
- First Walk press: UI refreshed but step counters did not advance.
- Second and later Walk presses: counters advanced normally.

## Initial Hypothesis (Unconfirmed)
`RequestRefreshWithoutStep` was true before the first Walk press.
`AreaController.WalkStep` checks this flag at the start and, when true:
1) calls `RefreshWithoutStep()`
2) skips `Counters.Advance(1)`
3) returns early

That means the first walk press was consumed by a refresh-only path.

## Why The Flag Was Set
`RewindToAnchorEffect` always set `RequestRefreshWithoutStep = true`.
If a rewind effect fired outside an active walk step (for example, during load,
OnEnter, or another event), the flag remained true until the next Walk press.

The exact event or asset that triggers that rewind outside a walk step is still
unknown. This is the open part of the investigation.

## Alternative Hypothesis: UI Update Timing
Progress UI updates were originally emitted only near the end of `WalkStep`.
If the step was waiting on `HandleApproach` (approach selection), the UI would not
reflect the advanced counters until that selection resolved. When the player
pressed Walk again to skip the approach, the UI update happened on that second
press, creating the appearance that the first step did not decrement.

## Fix Implemented
Two defensive fixes were applied:

Changes:
- Added `GameContext.IsWalkingStep`.
- `WalkingSystemManager` sets `IsWalkingStep = true` only while `WalkStep` runs.
- `RewindToAnchorEffect` now sets `RequestRefreshWithoutStep` only when
  `IsWalkingStep` is true.
- `AreaController` now calls `NotifyProgressChanged()` once right after gate
  handling (before encounter/approach), so the UI reflects the step immediately.

Result:
The first Walk press can no longer be consumed by a refresh-only pass that was
queued earlier in the session, and progress UI no longer depends on approach
selection resolving before it updates.

## Logging (temporary)
During investigation, temporary logs were added to:

1) `RewindToAnchorEffect` (when it runs and whether refresh was requested)
2) `AreaController.WalkStep` (when refresh-only is consumed)

These logs were removed after confirmation to reduce noise.
Re-add them only if another reproduction is needed.

## Open Question
- Which event or data asset triggers `RewindToAnchorEffect` outside a walk step?
- Is the original symptom fully explained by approach-wait UI timing, or was
  the refresh-only flag involved in the earlier reproduction?

---

## 2026-01-?? 追加調査 (歩行1歩目ログ)

### 実測ログ
`WalkStep` の `Advance(1)` 直後にログを追加して確認したところ、
**1歩目で `global/nodeSteps/track` が 1 になっている**ことが確認できた。

例:
- `[Walk] WalkStep after-advance: node=エントランスホール, global=1, nodeSteps=1, track=1`

### そこから得られた判断
歩数は実際に進んでおり、**UI側の反映タイミングが遅れて見えていた**
可能性が高い。`HandleApproach` 待機中に UI が更新されず、次の操作時に
変化が見えるため「1歩目が減っていないように見える」状態になり得る。

### 対応
診断用ログはノイズになるため削除済み。
実運用では「歩数進行は正常、表示だけ遅延して見えていた」仮説を採用。

Next step if needed:
- Use the new logs to find the first `RewindToAnchorEffect` call after load.
- Trace the calling event or asset in the inspector (Find References).

---

## 2026-01-18 追加調査

### 修正解除テスト

修正を一時的に解除して問題の再現を試みた。

解除した内容:
- `RewindToAnchorEffect`: `= context.IsWalkingStep` → `= true` に戻す
- `AreaController` constructor: クリア処理をコメントアウト
- `WalkingSystemManager.OnEnable`: クリア処理をコメントアウト
- `WalkingSystemManager.InitializeIfReady`: クリア処理をコメントアウト

追加したログ:
- `GameContext.RequestRefreshWithoutStep` の setter に全書き込みログ
- 変更があれば `false → true` のようにスタックトレース付きで出力

### 結果

**問題が再現しなかった。**

- `RequestRefreshWithoutStep: false → true` のログが一切出ない
- `RewindToAnchorEffect` のログも一切出ない
- 修正を外しても最初の一歩で歩数が正常に減った

### MCP調査結果

シーン上のWalkingSystemManagerを確認:
```
WalkingSystemManager (instanceID: 36392)
├── rootGraph: FlowGraph_Stages.asset
└── progressUI: ProgressText
```

FlowGraph_Stages.asset:
```
nodes: [Node_0__________.asset]
startNodeId: "エントランスホール"
```

Node_0__________.asset:
```yaml
gates:
  - gateId: gate_checkpoint
    passConditions: [HasFlagCondition]  # 条件あり
    onPass: [CreateAnchorEffect]
    onFail: []  # 空

  - gateId: gate_loop
    passConditions: []  # ← 空！常にパス
    onPass: []
    onFail: [Effect_RewindAnchor.asset]  # RewindToAnchorEffect
```

**発見**: `RewindToAnchorEffect` は設定されていた。
しかし `gate_loop.passConditions` が空のため、ゲートは常にパスし、
`onFail` の `RewindToAnchorEffect` は発動しない。

これが問題が再現しなかった理由。

### 結論

- `RewindToAnchorEffect` はアセットに存在する
- しかし現在の設定では発動条件を満たさない
- 元の問題は `passConditions` に条件が設定されていた時に発生した可能性がある
- または別のシナリオ（OnEnterイベント等）で発動していた可能性がある

### 対応

修正を復元して防御的に維持する。
ログ機能も維持し、今後問題が発生した場合に追跡可能にする。
