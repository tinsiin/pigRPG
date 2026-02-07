# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## プロジェクト概要

pigRPG - Unity製Android向けRPG（日本語プロジェクト）
- Unity 2022.3.x / Windows

## 編集制約

**編集可能:**
- `Assets/Script/**/*.cs`
- `doc/` フォルダ
- シーン(`.unity`) / プレハブ(`.prefab`): Unity MCP経由で操作。必要なら直接テキスト編集も可（GUID/fileIDに注意）

**編集禁止:**
- `*.meta` ファイル（Unity自動生成）
- `Packages/`, `ProjectSettings/`, `Assets/Plugins/`

## 使用ライブラリ

| 用途 | ライブラリ |
|------|-----------|
| 非同期処理 | UniTask |
| アニメーション | LitMotion |
| 乱数 | NRandom（NRandom.Numericsでベクトル乱数も可） |
| リアクティブ | R3 |

## 新規ファイル作成時の注意

Assetsフォルダ以下を調べ、同一機能・同一役割のファイル、フォルダが既に存在しないか確認すること。
