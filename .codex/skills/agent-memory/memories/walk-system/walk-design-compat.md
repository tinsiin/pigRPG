---
summary: "Walk system design decisions + compatibility notes (exit mandatory, rewind, counters)."
created: 2026-01-10
tags: [walking-system, design, compatibility]
---

# Context
- Spec: doc/歩行システム設計/ゼロトタイプ歩行システム設計書.md

# Design decisions
- Walk cycle order: side update -> central update -> encounter roll -> approach -> progress update.
- Encounter can interrupt; approach happens after encounter resolves.
- Exits are mandatory; no Auto edge in walk flow (only Jump/forced transitions allowed). Exit object always shown and requires approach.
- Exit selection UI always shown even if only one candidate (consistent UX).
- Exit/gate back objects and central objects are image-based (Sprite/UI Image), separate from side line renderer.
- Exit spawn: step-based or probability-based; gates must all be cleared before exit appears; exit/gate can be skipped and re-rolled per rules.
- Defeat handling: OutcomeHook (Lose) triggers Rewind/Jump; rewind can include track progress, gate states, exit availability.
- After rewind, do a "walk refresh without step": re-roll side/central objects only, skip encounter, do not advance step counters (run away flavor).

# Compatibility / migration notes
- NowProgress is used as distance for enemy re-encounter and revive steps; conflicts with node transitions and rewinds.
  - NormalEnemy.ReEncountCallback uses NowProgress delta; NormalEnemy.Reborn uses RecovelySteps via NowProgress.
- Linear map UI based on NowProgress conflicts with branching graph; move to node-local progress/gate position UI.
- Walk effects are tied to step counters (passive decay, recovery, attr point decay); need a "visual-only refresh" path for rewind without stepping.
- StageData/StageCut/AreaDates -> Node/Edge conversion plan:
  - AreaDate -> NodeSO; NextID/NextStageID -> ExitCandidate(toNodeId/uiLabel/conditions)
  - EncounterRate/EscapeRate -> EncounterTableSO/EncounterSO
  - Side object lists -> SideObjectTableSO.entries
  - StageThemeColorUI/MapLine -> uiHints/presenter data
  - StageID/AreaID/NowProgress -> nodeId + trackProgress + walkCounters
- BattleManager is independent; invoke via EncounterResolver/EventKernel.

# Suggested counters
- WalkCounters: globalSteps, nodeSteps, trackProgress. Decide which counters rewind.

# Key files
- Spec: doc/歩行システム設計/ゼロトタイプ歩行システム設計書.md
- NowProgress dependencies: Assets/Script/Enemy/NormalEnemy.cs, Assets/Script/Stages.cs, Assets/Script/PlayersStates.cs