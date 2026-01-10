# PlayersStates Split Design

## Goal
- Split the large PlayersStates class into smaller, focused components.
- Keep gameplay behavior unchanged.
- Make dependencies explicit and testable.

## Scope
- This plan only covers PlayersStates responsibility split.
- Walking system design changes are out of scope and handled elsewhere.

## Current Responsibilities (summary)
- Progress and stage/area state (NowProgress/NowStageID/NowAreaID)
- Party roster and indexing (Allies, TryGetAllyIndex, GetParty)
- Walk loop callbacks (PlayersOnWalks)
- Battle callbacks (OnBattleStart, PlayersOnWin/Lost/RunOut)
- UI control (AllyAlliesUISetActive, skill UI filtering/selection)
- Skill-passive selection UI (GoToSelectSkillPassiveTargetSkillButtonsArea)
- Emotional attachment UI and tuning (OpenEmotionalAttachmentSkillSelectUIArea)
- Tuning constants (HP/Mental conversion factors, ExplosionVoid)

## Target Components
1) **PlayersProgressTracker**
   - Owns: NowProgress/NowStageID/NowAreaID
   - Methods: AddProgress, ProgressReset, SetArea

2) **PlayersRoster**
   - Owns: Allies array, TryGetAllyIndex, GetAllyByIndex
   - Methods: GetParty (or delegate to PartyBuilder)

3) **PartyBuilder**
   - Owns: building BattleGroup and compatibility data
   - Inputs: Roster + tuning + impressions

4) **WalkLoopService**
   - Owns: PlayersOnWalks
   - Inputs: Roster, per-ally walk callback

5) **BattleCallbacks**
   - Owns: OnBattleStart + PlayersOnWin/Lost/RunOut
   - Inputs: Roster + UI services

6) **PlayersUIService**
   - Owns: AllyAlliesUISetActive, skill UI filtering/selection
   - Inputs: UI references (skill buttons, cancel areas, etc.)

7) **SkillPassiveSelectionUI**
   - Owns: GoToSelectSkillPassiveTargetSkillButtonsArea, ReturnSelectSkillPassiveTargetSkillButtonsArea

8) **EmotionalAttachmentUI**
   - Owns: OpenEmotionalAttachmentSkillSelectUIArea

9) **PlayersTuningConfig**
   - Owns: ExplosionVoid, HP/Mental conversion factors, EmotionalAttachmentSkillWeakeningPassive

## Interface Mapping
Use hub interfaces already introduced:
- IPlayersProgress -> PlayersProgressTracker
- IPlayersParty -> PartyBuilder + BattleCallbacks + WalkLoopService
- IPlayersUIControl -> PlayersUIService
- IPlayersSkillUI -> PlayersUIService + SkillPassiveSelectionUI + EmotionalAttachmentUI
- IPlayersTuning -> PlayersTuningConfig
- IPlayersRoster -> PlayersRoster

## Migration Phases
### Phase 0: Baseline
- Keep PlayersStates as the facade.
- Add wrapper fields that delegate to new components (no behavior change).

### Phase 1: Extract Tuning + Progress
- Move ExplosionVoid and conversion factors to PlayersTuningConfig.
- Move progress fields to PlayersProgressTracker.
- PlayersStates delegates to these components.

### Phase 2: Extract Roster + PartyBuilder
- Move Allies array + indexing to PlayersRoster.
- Move GetParty logic to PartyBuilder.

### Phase 3: Extract WalkLoop + BattleCallbacks
- Move PlayersOnWalks to WalkLoopService.
- Move OnBattleStart / PlayersOnWin/Lost/RunOut to BattleCallbacks.

### Phase 4: Extract UI Services
- Move skill UI filtering/selection to PlayersUIService.
- Move skill-passive selection to SkillPassiveSelectionUI.
- Move emotional attachment UI to EmotionalAttachmentUI.

### Phase 5: Replace call sites
- Update hubs to bind each component instead of PlayersStates.
- Remove direct use of PlayersStates methods where possible.

### Phase 6: Shrink PlayersStates
- PlayersStates becomes a bootstrapper that wires components.
- Optionally remove PlayersStates.Instance usage in new code paths.

## Verification Checklist
- Battle start/end callbacks still fire.
- Walk progression still updates ally walk effects.
- UI state (skill filters, cancel passive area) unchanged.
- Emotional attachment UI opens/closes correctly.
- Progress/stage/area updates remain consistent.

## Risks
- Initialization order between PlayersStates and UI singletons.
- Hidden dependencies inside UI code.
- Risk of partial migration leaving duplicated behavior.

## Non-goals
- Walking system design changes.
- BattleManager refactor (already handled).

## 実装済み責務分離一覧
- Progress/進行度: `PlayersProgressTracker`（IPlayersProgress）
- Tuning/中央決定値: `PlayersTuningConfig`（IPlayersTuning）
- Roster/味方配列: `PlayersRoster`（IPlayersRoster）
- Party構築: `PartyBuilder` → `PlayersPartyService.GetParty()`
- WalkLoop: `WalkLoopService` → `PlayersPartyService.PlayersOnWalks()`
- BattleCallbacks: `PlayersBattleCallbacks` → `PlayersPartyService.PlayersOnWin/Lost/RunOut()`
- UI制御: `PlayersUIService`（IPlayersUIControl/IPlayersSkillUI）
- パッシブ対象選択UI: `SkillPassiveSelectionUI`
- 思い入れスキルUI: `EmotionalAttachmentUI`
- Facade/初期化: `PlayersStates` が各サービスを構築し `PlayersStatesHub` にバインド

## クローズ
- Status: Completed
- Closed: 2026-01-11
