using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;

/// <summary>
/// KZoom（アイコンタップ詳細表示）の内部状態を管理する構造体。
/// WatchUIUpdateから分離されたPhase 3bコンポーネント。
/// </summary>
public class KZoomState
{
    /// <summary>Kモードがアクティブか</summary>
    public bool IsActive { get; set; }

    /// <summary>Kモードアニメーション中か</summary>
    public bool IsAnimating { get; set; }

    /// <summary>キャンセルトークンソース</summary>
    public CancellationTokenSource Cts { get; set; }

    /// <summary>Kズーム前の元位置</summary>
    public Vector2 OriginalPos { get; set; }

    /// <summary>Kズーム前の元スケール</summary>
    public Vector3 OriginalScale { get; set; }

    /// <summary>スナップショットが有効か（EnterKで保存されたか）</summary>
    public bool SnapshotValid { get; set; }

    /// <summary>パッシブ表示用の生トークン文字列</summary>
    public string PassivesTokensRaw { get; set; } = string.Empty;

    /// <summary>パッシブ表示用TMPキャッシュ</summary>
    public TMP_Text PassivesTMP { get; set; }

    /// <summary>K中のクリック元UI（排他表示用）</summary>
    public BattleIconUI ExclusiveUI { get; set; }

    /// <summary>K開始時のActionMark表示状態</summary>
    public bool ActionMarkWasActive { get; set; }

    /// <summary>K開始時のSchizoLog表示状態</summary>
    public bool SchizoWasVisible { get; set; }

    /// <summary>K中に非表示にした他UIの退避リスト</summary>
    public List<(BattleIconUI ui, bool wasActive)> HiddenOtherUIs { get; set; }

    /// <summary>状態をリセットする</summary>
    public void Reset()
    {
        IsActive = false;
        IsAnimating = false;
        Cts?.Cancel();
        Cts?.Dispose();
        Cts = null;
        OriginalPos = Vector2.zero;
        OriginalScale = Vector3.one;
        SnapshotValid = false;
        PassivesTokensRaw = string.Empty;
        PassivesTMP = null;
        ExclusiveUI = null;
        ActionMarkWasActive = false;
        SchizoWasVisible = false;
        HiddenOtherUIs = null;
    }
}
