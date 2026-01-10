# PlayersStates Global Dependency Refactor Plan

## Goal
- Remove direct `PlayersStates.Instance` access from other systems.
- Keep gameplay behavior unchanged.
- Make dependencies explicit and testable.

## Scope
- Replace global access in `Assets/Script` with injected or hub-based interfaces.
- Do not modify walking system design rules (handled in walking system docs).

## Current State
- `PlayersStates` is a singleton used across battle, walking, and UI.
- BattleManager already uses `IBattleMetaProvider` (adapter pattern in place).
- Hubs exist for other domains (`BattleContextHub`, `UIStateHub`).

## Inventory (to fill at start of work)
- Run: `rg -n "PlayersStates.Instance" Assets/Script`
- Classify references:
  - Battle-related (already handled by `IBattleMetaProvider`)
  - Walking / stage progression
  - UI control
  - Data/bootstrap

## Strategy
1) Define small, purpose-specific interfaces.
2) Create adapters that wrap `PlayersStates`.
3) Introduce a hub/registry to bind the adapter at startup.
4) Replace call sites by subsystem.
5) Keep `PlayersStates.Instance` only as a temporary fallback.

## Proposed Interfaces (example)
- `IPlayersProgress`
  - `int NowProgress { get; }`
  - `void AddProgress(int addPoint)`
  - `void ProgressReset()`
  - `void SetArea(int id)`
- `IPlayersParty`
  - `BattleGroup GetParty()`
  - `void PlayersOnWin()`
  - `void PlayersOnLost()`
  - `void PlayersOnRunOut()`
  - `void PlayersOnWalks(int walkCount)`
- `IPlayersUIControl`
  - `void AllyAlliesUISetActive(bool isActive)`

## Hub Pattern (example)
- `PlayersStatesHub.Set(IPlayersProgress progress, IPlayersParty party, IPlayersUIControl ui)`
- `PlayersStates.Awake()` binds adapters to the hub.
- Call sites use hub interfaces instead of `PlayersStates.Instance`.

## Migration Steps
### Phase A: Inventory and grouping
- Map each `PlayersStates.Instance` usage to a minimal interface.
- Decide which interface each call site should depend on.

### Phase B: Adapter and hub setup
- Implement adapters:
  - `PlayersStatesProgressAdapter`
  - `PlayersStatesPartyAdapter`
  - `PlayersStatesUIAdapter`
- Bind adapters in `PlayersStates.Awake()` or a dedicated bootstrap.

### Phase C: Replace call sites
- Update each subsystem to use the hub:
  - Walking / stage progression -> `IPlayersProgress`
  - Battle outcomes -> `IPlayersParty` (already done via `IBattleMetaProvider`)
  - UI -> `IPlayersUIControl`
- Add null-guard logs where needed.

### Phase D: Cleanup
- Remove remaining `PlayersStates.Instance` references.
- Optionally keep `PlayersStates.Instance` only for editor/debug use.

## Verification Checklist
- Battle end: win/loss/runout callbacks still fire.
- Walk progression: progress and area still update correctly.
- UI: ally UI hides/shows exactly as before.

## Risks
- Initialization order (hub not bound before usage).
  - Mitigation: bind in `PlayersStates.Awake()` and log if hub is unbound.
- Hidden dependencies in editor-only or debug code.
