# 音声エンベロープMCP設計書

## 概要

効果音ファイルの音量エンベロープ（時間×音量カーブ）を取得し、エフェクトJSON生成の入力データとして使用するためのMCPサーバー。

### 目的

```
効果音ファイル (.wav/.mp3/.ogg)
       ↓ MCP
エンベロープデータ (times[], amp[])
       ↓ AI
エフェクトJSON (frames[])
```

音の大きさに合わせてエフェクトを自動生成する。

---

## 必要環境

### 1. Python

```bash
# Python 3.10以上推奨
python --version
```

### 2. FFmpeg

音声デコードに使用。PATHに通っている必要がある。

```bash
# 確認
ffmpeg -version
```

Windows: https://www.gyan.dev/ffmpeg/builds/ からダウンロード

### 3. Pythonパッケージ

```bash
pip install fastmcp numpy
```

---

## ファイル構成

```
C:\mcp\
└── audio_envelope\
    └── server.py       ← MCPサーバー本体
```

※ 場所は任意。設定ファイルでパスを指定する。

---

## MCPサーバー実装

### server.py

```python
"""
Audio Envelope MCP Server
効果音の音量エンベロープを取得するMCPサーバー
"""

import os
import subprocess
from pathlib import Path
import numpy as np
from fastmcp import FastMCP

mcp = FastMCP("audio-envelope")

# 許可するディレクトリ（環境変数から取得、未設定時はデフォルト）
ALLOWED_ROOT = Path(os.environ.get(
    "AUDIO_ALLOWED_ROOT",
    r"C:\Users\teinshiiin\Documents\GitHub\pigRPG\Assets\Resources\Audio"
)).resolve()


def _safe_path(p: str) -> Path:
    """パスが許可ディレクトリ内かチェック"""
    rp = Path(p).resolve()
    if rp == ALLOWED_ROOT or ALLOWED_ROOT in rp.parents:
        return rp
    raise ValueError(f"Path not allowed: {rp}. Must be under {ALLOWED_ROOT}")


def _decode_to_f32_mono(path: Path, sr: int) -> np.ndarray:
    """FFmpegで音声をモノラルfloat32にデコード"""
    cmd = [
        "ffmpeg", "-v", "error",
        "-i", str(path),
        "-ac", "1",           # モノラル
        "-ar", str(sr),       # サンプルレート
        "-f", "f32le",        # float32 little-endian
        "pipe:1"
    ]
    raw = subprocess.check_output(cmd)
    return np.frombuffer(raw, dtype=np.float32)


def _compute_envelope(
    x: np.ndarray,
    sr: int,
    fps: int,
    attack_ms: float,
    release_ms: float
) -> tuple[list[float], list[float]]:
    """RMSエンベロープを計算（Attack/Releaseスムージング付き）"""
    hop = max(1, sr // fps)
    n = (len(x) // hop) * hop
    x = x[:n].reshape(-1, hop)

    # フレームごとのRMS
    rms = np.sqrt(np.mean(x * x, axis=1))

    # Attack/Release 1-pole smoothing
    a_attack = np.exp(-1.0 / (fps * (attack_ms / 1000.0))) if attack_ms > 0 else 0
    a_release = np.exp(-1.0 / (fps * (release_ms / 1000.0))) if release_ms > 0 else 0

    smoothed = np.zeros_like(rms)
    for i in range(len(rms)):
        if i == 0:
            smoothed[i] = rms[i]
        else:
            if rms[i] > smoothed[i - 1]:
                # Attack（音が大きくなる時）
                smoothed[i] = a_attack * smoothed[i - 1] + (1 - a_attack) * rms[i]
            else:
                # Release（音が小さくなる時）
                smoothed[i] = a_release * smoothed[i - 1] + (1 - a_release) * rms[i]

    # 正規化（0〜1）
    max_val = float(smoothed.max()) if smoothed.max() > 0 else 1.0
    normalized = smoothed / max_val

    times = (np.arange(len(normalized)) / fps).astype(float).tolist()
    amp = normalized.astype(float).tolist()

    return times, amp


def _detect_peaks(amp: list[float], threshold: float = 0.6) -> list[dict]:
    """ピーク（ローカル最大値）を検出"""
    peaks = []
    for i in range(1, len(amp) - 1):
        if amp[i] > amp[i - 1] and amp[i] > amp[i + 1] and amp[i] >= threshold:
            peaks.append({"index": i, "amp": amp[i]})
    return peaks


@mcp.tool
def get_audio_envelope(
    path: str,
    fps: int = 60,
    sr: int = 16000,
    attack_ms: float = 10,
    release_ms: float = 100
) -> dict:
    """
    音声ファイルのエンベロープを取得

    Args:
        path: 音声ファイルパス（許可ディレクトリ内）
        fps: 出力のフレームレート（デフォルト: 60）
        sr: 解析用サンプルレート（デフォルト: 16000）
        attack_ms: Attack時間（ms、デフォルト: 10）
        release_ms: Release時間（ms、デフォルト: 100）

    Returns:
        dict: {
            file: ファイルパス,
            duration_sec: 音声の長さ（秒）,
            fps: フレームレート,
            frame_count: フレーム数,
            times: 時間配列（秒）,
            amp: 音量配列（0〜1）,
            peaks: ピーク情報
        }
    """
    p = _safe_path(path)

    if not p.exists():
        raise FileNotFoundError(f"File not found: {p}")

    x = _decode_to_f32_mono(p, sr)
    times, amp = _compute_envelope(x, sr, fps, attack_ms, release_ms)
    peaks = _detect_peaks(amp)

    # ピークに時間情報を追加
    for peak in peaks:
        peak["time"] = times[peak["index"]]

    return {
        "file": str(p),
        "duration_sec": float(len(x) / sr),
        "fps": fps,
        "frame_count": len(amp),
        "times": times,
        "amp": amp,
        "peaks": peaks
    }


@mcp.tool
def get_audio_envelope_summary(
    path: str,
    fps: int = 12,
    sr: int = 16000,
    attack_ms: float = 10,
    release_ms: float = 100
) -> dict:
    """
    エンベロープの要約版（エフェクトJSON生成向け、少ないフレーム数）

    Args:
        path: 音声ファイルパス
        fps: 出力フレームレート（デフォルト: 12、エフェクトシステムに合わせる）
        その他: get_audio_envelopeと同じ

    Returns:
        dict: get_audio_envelopeと同じ形式（フレーム数が少ない）
    """
    return get_audio_envelope(path, fps, sr, attack_ms, release_ms)


@mcp.tool
def list_audio_files(subdir: str = "") -> dict:
    """
    許可ディレクトリ内の音声ファイル一覧を取得

    Args:
        subdir: サブディレクトリ（空文字でルート）

    Returns:
        dict: {
            root: 検索ディレクトリ,
            files: ファイルリスト
        }
    """
    search_dir = ALLOWED_ROOT / subdir if subdir else ALLOWED_ROOT

    if not search_dir.exists():
        return {"root": str(search_dir), "files": []}

    audio_extensions = {".wav", ".mp3", ".ogg", ".flac", ".m4a"}
    files = []

    for f in search_dir.rglob("*"):
        if f.is_file() and f.suffix.lower() in audio_extensions:
            files.append({
                "path": str(f),
                "name": f.name,
                "relative": str(f.relative_to(ALLOWED_ROOT))
            })

    return {
        "root": str(ALLOWED_ROOT),
        "files": files
    }


if __name__ == "__main__":
    mcp.run()
```

---

## Claude Codeへの登録

### 設定ファイルの場所

プロジェクト単位: `.claude/settings.json`
または
グローバル: `~/.claude/settings.json`

### 設定内容

```json
{
  "mcpServers": {
    "audio-envelope": {
      "command": "python",
      "args": ["C:\\mcp\\audio_envelope\\server.py"],
      "env": {
        "AUDIO_ALLOWED_ROOT": "C:\\Users\\teinshiiin\\Documents\\GitHub\\pigRPG\\Assets\\Resources\\Audio"
      }
    }
  }
}
```

### 設定項目

| 項目 | 説明 |
|------|------|
| `command` | 実行コマンド（pythonへのパス） |
| `args` | サーバースクリプトのパス |
| `env.AUDIO_ALLOWED_ROOT` | アクセス許可するディレクトリ |

---

## 使用方法

### 1. 音声ファイル一覧を取得

```
list_audio_files()
list_audio_files(subdir="SE/Effects")
```

### 2. エンベロープを取得

```
get_audio_envelope(path="C:\\...\\fire_burst.wav")
```

返り値:
```json
{
  "file": "C:\\...\\fire_burst.wav",
  "duration_sec": 0.5,
  "fps": 60,
  "frame_count": 30,
  "times": [0.0, 0.0167, 0.0333, ...],
  "amp": [0.1, 0.5, 0.9, 0.7, 0.3, ...],
  "peaks": [
    {"index": 2, "amp": 0.9, "time": 0.0333}
  ]
}
```

### 3. 要約版（エフェクト生成向け）

```
get_audio_envelope_summary(path="...", fps=12)
```

fps=12でエフェクトシステムのデフォルトFPSに合わせる。

---

## エフェクト生成ワークフロー

### 1. 音声ファイルを指定

```
「fire_burst.wavに合わせたエフェクトを作って」
```

### 2. AIがエンベロープを取得

```python
# AIが内部でMCPツールを呼び出し
envelope = get_audio_envelope_summary("fire_burst.wav", fps=12)
```

### 3. エンベロープからエフェクトJSON生成

AIがamp[]を見て、各フレームの図形パラメータを決定:

| amp値 | エフェクトへの反映例 |
|-------|---------------------|
| 0.0〜0.2 | 小さい/暗い/空フレーム |
| 0.2〜0.5 | 中程度のサイズ/明るさ |
| 0.5〜0.8 | 大きい/明るい |
| 0.8〜1.0 | 最大サイズ/フラッシュ |
| peaks | 瞬間的な追加エフェクト |

### 4. 生成されるJSON例

音声: 0.4秒、ピーク0.1秒付近
```json
{
  "name": "fire_burst",
  "canvas": 100,
  "fps": 12,
  "se": "fire_burst",
  "frames": [
    { "shapes": [{ "type": "circle", "x": 50, "y": 50, "radius": 10, "brush": {"color": "#FF660033"} }] },
    { "shapes": [{ "type": "circle", "x": 50, "y": 50, "radius": 40, "brush": {"color": "#FF6600FF"} }] },
    { "shapes": [{ "type": "circle", "x": 50, "y": 50, "radius": 30, "brush": {"color": "#FF6600AA"} }] },
    { "shapes": [{ "type": "circle", "x": 50, "y": 50, "radius": 15, "brush": {"color": "#FF660055"} }] },
    { "shapes": [] }
  ]
}
```

---

## パラメータ調整ガイド

### fps

| 値 | 用途 |
|----|------|
| 12 | 標準エフェクト |
| 24 | 滑らかなエフェクト |
| 60 | 高精度解析 |

### attack_ms / release_ms

| 設定 | 効果 |
|------|------|
| attack=10, release=100 | 標準（瞬間的な立ち上がり、ゆっくり減衰） |
| attack=5, release=50 | 鋭い反応（パンチ音向け） |
| attack=20, release=200 | 滑らか（持続音向け） |

---

## セキュリティ

### 許可ディレクトリ

`AUDIO_ALLOWED_ROOT`で指定したディレクトリ以外はアクセス不可。

```python
# 許可: C:\...\pigRPG\Assets\Resources\Audio\SE\fire.wav
# 拒否: C:\Windows\System32\config\SAM
```

### 入力検証

- パスはresolve()で正規化
- 親ディレクトリチェック
- ファイル存在チェック

---

## トラブルシューティング

### FFmpegが見つからない

```
FileNotFoundError: [WinError 2] The system cannot find the file specified
```

→ FFmpegをインストールしてPATHに追加

### パスが許可されていない

```
ValueError: Path not allowed: ...
```

→ `AUDIO_ALLOWED_ROOT`の設定を確認

### 音声ファイルが読めない

```
ffmpeg error
```

→ ファイル形式を確認（wav/mp3/ogg/flac/m4a対応）

---

## 将来拡張案

| 機能 | 説明 |
|------|------|
| キーフレーム圧縮 | 点数を減らして渡す（Ramer-Douglas-Peucker） |
| 区間検出 | 無音区間、盛り上がり区間を自動検出 |
| スペクトル解析 | 周波数帯域ごとのエンベロープ |
| プリセット | 「爆発音向け」「UI音向け」などのパラメータセット |
