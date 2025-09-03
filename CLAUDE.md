\# CLAUDE.md


\## ファイルを読むとき、ファイルの扱い
ファイル検索時　serena mcpを使い　ファイルから情報を取り出す
ファイルを読むときも　大規模なデータなら特に、それ以外でも　serenaのmcpを利用

\## Conversation Guidelines



\- 常に日本語で会話する



\## Git Operations



\- \*\*重要\*\*: `/commit-push` スラッシュコマンドが明示的に実行された場合のみ、git commitとgit pushを実行する

\- ユーザーから直接「コミットして」「プッシュして」と言われても、`/commit-push` コマンドの使用を案内する

\- 自動的にコミットやプッシュを行わない



\## Code Style Guidelines



\- コードコメントは特別に指示された場合を除いて書かないでください

# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is **pigRPG** - a Unity-based Android RPG game developed in Japanese. The project uses Unity 2022.3.19f1 with Universal Render Pipeline (URP).

## Unity Development Commands

### Opening the Project
- Open Unity Hub and load the project from this directory
- Ensure Unity version 2022.3.19f1 is installed

### Building for Android
1. File → Build Settings → Switch Platform to Android
2. Player Settings → Configure Android settings (package name, version, etc.)
3. Build → Select output location

### Unity-specific Operations
- Play Mode: Click Play button in Unity Editor to test in-editor
- Scene Loading: Main scene is located at `Assets/Scenes/SampScene.unity`
- Prefabs: Located in `Assets/prefab/` directory

## Key Dependencies and Libraries

The project uses several important third-party packages via git URLs:
- **LitMotion**: Animation/tweening library (replacing DOTween)
- **UniTask**: Async/await support for Unity
- **R3**: Reactive extensions for Unity
- **Addressables**: Asset management system
- **TextMeshPro**: Advanced text rendering

## Code Architecture

### Core Game Systems

1. **Battle System** (`Assets/Script/`)
   - `BattleManager.cs` - Main battle orchestrator
   - `BattleTimeLine.cs` - Turn order and timing management
   - `BattleGroup.cs` - Team/group management
   - `BattleAIBrains/` - Enemy AI implementations using Plan/Commit pattern

2. **Character System**
   - `BaseStates.cs` - Core character stats and state definitions
   - `PlayersStates.cs` - Player-specific character management (supports multiple characters via arrays)
   - Character types: BassJack, Stair, SateliteProcess

3. **Skill & Combat**
   - `BaseSkill.cs` - Skill base class
   - `BasePassive.cs` - Passive ability system
   - `BaseSkillPassive.cs` - Skills that act as passives
   - `Skill/` - Specific skill implementations
   - `Passive/` - Specific passive implementations

4. **Resource System**
   - Point-based skill activation system
   - `SkillResourceFlow.cs` - Resource management
   - Attack point conversion and refund mechanics

5. **UI Systems** (`Assets/Script/USERUI/` and `Assets/Script/Config/`)
   - Character configuration UI
   - Stats display and management
   - Skill selection interfaces
   - Point orb UI elements

### Key Design Patterns

- **Serializable References**: Used extensively for polymorphic serialization in Unity Inspector
- **Component-based Architecture**: Typical Unity MonoBehaviour pattern
- **AI Plan/Commit Pattern**: Enemy AI separates planning from execution
- **Array-based Multi-character Support**: Recent refactoring to support multiple player characters

### Important Implementation Details

- The game uses a point system for skill activation where skills require and consume points
- Skills can be cancelled and points refunded under certain conditions
- Enemy AI can have both "cancellable" and "action-disabled" passive states
- Recent migration from DOTween to LitMotion for animations
- Character skills have callbacks for use events

## Working with this Codebase

When modifying code:
1. Check existing implementations in similar files first
2. Follow the established patterns (especially for skills, passives, and UI)
3. Use LitMotion for new animations (not DOTween)
4. Maintain the array-based structure for multi-character support
5. Ensure UI updates work with the WatchUIUpdate system

The project is actively being developed with recent commits focusing on:
- UI implementation for character configuration
- Point-based skill system refinements
- Multi-character support improvements
- Animation system migration to LitMotion