using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System;
using System.Text;
using System.Threading;
using RandomExtensions;

/// <summary>
/// 文と文の間に挟むブリッジ文節のコレクションSO
/// </summary>
[CreateAssetMenu(menuName = "Schizo/LogBridgeCollection", fileName = "LogBridgeCollection")]
public class LogBridgeCollection : ScriptableObject
{
    [Header("文章間に挿入するブリッジ文節")]
    [TextArea(1, 3)]
    public List<string> BridgePhrases = new List<string>
    {
        "。。、",
        "、。。、",
        "。、。",
        "、、、",
        "。。。",
        "、。、。",
        "。、、。"
    };
}

/// <summary>
/// ログエントリのデータクラス
/// </summary>
[System.Serializable]
public class SchizoLogEntry
{
    public string Sentence;      // 最終生成済み文章
    public int Priority;         // 高いほど上に表示される
    public int InsertOrder;      // 先着順維持用（同優先度の場合）
    
    public SchizoLogEntry(string sentence, int priority, int insertOrder)
    {
        Sentence = sentence;
        Priority = priority;
        InsertOrder = insertOrder;
    }
}

// テンプレ機能は撤廃したため、テンプレートセット定義は削除


/// <summary>
/// 統合失調的ログシステム
/// </summary>
public class SchizoLog : MonoBehaviour
{
    public static SchizoLog Instance { get; private set; }
    
    [Header("表示設定")]
    [SerializeField] private int _maxLines = 8;
    [SerializeField] private float _charInterval = 0.04f;
    [SerializeField] private bool _enableScrollAnimation = true;
    [SerializeField] private float _scrollAnimationDuration = 0.3f;
    
    private bool _enableDebugLog = false; // これで内部Debug.Log系のオン/オフを制御
    [Tooltip("AddLog(debug:true) で追加されるデバッグ用ログを表示するか")]
    [SerializeField] private bool _outputDebugEntries = false; // これでAddLogのデバッグエントリ出力を制御
    
    [Header("UI参照")]
    public TextMeshProUGUI LogText;
    
    [Header("ブリッジ文節コレクション（直アサイン必須）")]
    [SerializeField] private LogBridgeCollection _bridgeCollection;
    
    [Header("テスト用設定")]
    [SerializeField] private List<string> _testSentences = new List<string>
    {
        "aはbにヴェネンドガネストレカジハを実行した。。、。。、。するとそのスキルの精神補正は、92%で、最終ダメージは、、上が落ちてくる前に、クルーズ83438ダメージだったようです、！",
        "統合失調的な戦闘が始まった。。、混沌とした世界で、、、何かが起こりそうな予感がする。。。",
        "敵の攻撃が、、、なんだか変な感じで、、、こちらに向かってくる。。、でも大丈夫、、、多分。",
        "スキルの効果が、、、よくわからないけど、、、なんか凄そうな感じで発動した。。。！？"
    };
    [SerializeField] private List<int> _testPriorities = new List<int> { 10, 5, 3, 1 };
    
    // 内部データ
    private List<SchizoLogEntry> _entries = new List<SchizoLogEntry>();
    private int _insertOrderCounter = 0;
    // テンプレは廃止。辞書も撤去。
    private bool _isDisplaying = false;
    private System.Threading.CancellationTokenSource _displayCts;
    
    // 永続的な表示バッファ（アニメーション完了後も保持）
    private System.Text.StringBuilder _displayBuffer = new System.Text.StringBuilder();

    private bool _isQuitting = false;
    
    // キャンセル時に残り文字を即時描画しない抑止フラグ（既定: false = 既存挙動を維持）
    private bool _suppressFlushOnCancel = false;

    // 末尾付近にユーティリティ
    private bool IsUIAlive()
    {
        // Unity の “== null” は Destroy 判定込み
        return !_isQuitting && LogText != null;
    }
    
    //シングルトン
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            // 直参照SOからテンプレートを初期化。未設定ならデフォルトを使用。
            InitTemplates();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void OnDestroy()
    {
        _displayCts?.Cancel();
        _displayCts?.Dispose();
        _isQuitting = true;
        // Addressables依存は撤廃
    }
    private void OnApplicationQuit()
    {
        _isQuitting = true;
    }
    
    /// <summary>
    /// テンプレート初期化（直参照SO必須）
    /// </summary>
    private void InitTemplates()
    {
        // ブリッジ文節SOの必須チェックのみ
        if (_bridgeCollection == null)
        {
            LogError("SchizoLog: LogBridgeCollection が未設定です。インスペクターで _bridgeCollection をアサインしてください。");
            throw new InvalidOperationException("SchizoLog requires a LogBridgeCollection assigned.");
        }
        Log($"SchizoLog: ブリッジ文節数: {_bridgeCollection.BridgePhrases?.Count ?? 0}");
    }

    // テンプレ適用は不要になったため削除
    
    /// <summary>
    /// ログを追加（生テキスト）
    /// </summary>
    /// <param name="priority">優先度（高いほど上に表示）</param>
    /// <param name="debug">true の場合は "デバッグログ" として扱い、_outputDebugEntries の設定に従って出力可否を決める</param>
    public void AddLog(string sentence, bool debug = false,int priority = 0)
    {
        if (string.IsNullOrEmpty(sentence)) return;

        // 非同期中に設定が切り替わっても挙動がブレないよう、ここでスナップショットを取って判定
        bool allowDebugEntry = _outputDebugEntries;
        if (debug && !allowDebugEntry)
        {
            Log($"SchizoLog: デバッグエントリを抑制 - 優先度:{priority}, 文章:{sentence.Substring(0, Math.Min(30, sentence.Length))}...");
            return; // デバッグ指定かつ出力オフならキューに積まない
        }

        var entry = new SchizoLogEntry(sentence, priority, _insertOrderCounter++);
        _entries.Add(entry);
        Log($"SchizoLog: エントリ追加 - 優先度:{priority}, デバッグ:{debug}, 文章:{sentence.Substring(0, Math.Min(30, sentence.Length))}...");
    }
    
    /// <summary>
    /// エントリのログを表示（優先度順、行数制限付き）
    /// </summary>
    /*
     * Note: 高速連打時に「文章が不自然に二連続で連なる／重なって見える」現象について
     * 
     * 現象の概要
     * - 呼び出し元（例: BattleManager.ACTPop など）で DisplayAllAsync() を Forget で多重起動しうる。
     * - 進行中の表示（タイプアニメーション）を次の呼び出しが割り込むと、本メソッドは以下の流れで処理する：
     *   1) 進行中フラグ _isDisplaying を検出し、_displayCts.Cancel() でアニメーションをキャンセル
     *   2) 現在表示中文字を maxVisibleCharacters = text.Length にして「今ある行を即時確定」
     *   3) 前の DisplayAllAsync の終了 (_isDisplaying = false) を待つ
     *   4) その後、新規エントリ群を _displayBuffer に追記し、アニメーションを再開
     * 
     * なぜ「二連続」に見えるのか
     * - _displayBuffer は累積テキストバッファ。ConvertEntriesToString() は既存 _displayBuffer 末尾に新規エントリを追記する設計。
     * - 割り込みタイミングによっては、
     *   ・前回途中まで追記→キャンセル→今回もう一度同じ（又は似た）エントリが末尾に追記
     *   ・成功時のみ RemoveProcessedEntries() で _entries を消すため、キャンセル時は同一エントリが残って再度追記対象になる
     * - これらが視覚的に「文章が二連続で並ぶ」ように見える原因。
     * 
     * 仕様的補足
     * - この挙動は、ログが統合失調的に不気味に連なるという本システムの意図にも合致するため、現時点では仕様として許容。
     * - 重複の抑制が必要になった場合は、(a) 追記のトランザクション化（ローカルSBで構築→成功時のみ _displayBuffer へ一括反映）、
     *   (b) 多重起動のデバウンス／合流、(c) キャンセル時の processed エントリ除去戦略 見直し、などで改善可能。
     */
    public async UniTask DisplayAllAsync()
    {
        if (!IsUIAlive()) return;
        
        Log("SchizoLog: DisplayAllAsync 開始");
        
        // ===== 割り込み対応 =====
        if (_isDisplaying)
        {
            Log("SchizoLog: 表示処理を割り込みキャンセルし、現在の行削除結果を即時反映します");
            // 進行中のアニメーションをキャンセル
            _displayCts?.Cancel();

            // 現在のテキストを即時全表示（行削除結果を保持）
            if (LogText != null)
            {
                LogText.maxVisibleCharacters = LogText.text.Length;
                Log($"SchizoLog: 割り込み時の現在テキスト長: {LogText.text.Length}");
            }

            // 旧DisplayAllAsyncの終了を待つ（_isDisplaying が false になるまで）
            await UniTask.WaitUntil(() => !_isDisplaying);
        }
        
        Log($"SchizoLog: エントリ数: {_entries.Count}");
        
        if (_entries.Count == 0)
        {
            Log("SchizoLog: 表示するログがありません");
            return;
        }
        
        Log($"SchizoLog: LogText状態 = {(LogText != null ? "OK" : "NULL")}");
        

        _displayCts?.Cancel();
        _displayCts?.Dispose();
        _displayCts = new System.Threading.CancellationTokenSource();
        
        // 二重呼び出し防止
        if (_isDisplaying)
        {
            Log("SchizoLog: 既に表示処理中のため、新しい要求をスキップします");
            return;
        }
        
        _isDisplaying = true;
        
        try
        {
            // 既存ログをクリア
            if (LogText != null)
            {
                LogText.text = "";
                LogText.maxVisibleCharacters = 0;
                Log("SchizoLog: LogTextをクリアしました");
            }
            else
            {
                LogError("SchizoLog: LogTextがNULLです！");
            }
            
            // エントリを優先度順にソート
            var sortedEntries = _entries.OrderByDescending(e => e.Priority).ThenBy(e => e.InsertOrder).ToList();
            Log($"SchizoLog: {sortedEntries.Count}個のエントリをソートしました");
            
            // 既存バッファ長を記録
            int beforeLength = _displayBuffer.Length;

            // エントリをstring化（ブリッジ文節付き）
            ConvertEntriesToString(sortedEntries); // ここで _displayBuffer に追記される
            int afterLength = _displayBuffer.Length;

            // 追加された長さ
            int addedLength = afterLength - beforeLength;
            Log($"SchizoLog: 追加テキスト長: {addedLength}");

            // バッファ全体を最終テキストとしてアニメーション
            string finalText = _displayBuffer.ToString();
            Log($"SchizoLog: 最終テキスト長: {finalText.Length}");
            
            if (addedLength > 0 && LogText != null)
            {
                string preview = finalText.Substring(beforeLength, Math.Min(30, addedLength));
                Log($"SchizoLog: アニメーション開始 - テキスト(先頭): {preview}...");
                await DisplayTextAsync(finalText, beforeLength, _displayCts.Token);
                Log("SchizoLog: アニメーション完了");
            }
            else
            {
                LogWarning($"SchizoLog: アニメーションをスキップ - finalText: {finalText?.Length ?? 0}, LogText: {(LogText != null ? "OK" : "NULL")}");
            }
            
            // 表示完了後、エントリをクリア（_displayBufferは保持）
            RemoveProcessedEntries(sortedEntries);  
            Log($"SchizoLog: 処理済み {sortedEntries.Count} 件を除去。残:{_entries.Count}");
            Log($"SchizoLog: エントリをクリアしました。_displayBufferは保持（長さ: {_displayBuffer.Length}）");
        }
        catch (OperationCanceledException)
        {
            Log("SchizoLog: 表示処理がキャンセルされました");
        }
        catch (Exception e)
        {
            LogError($"SchizoLog: 表示処理エラー - {e.Message}");
        }
        finally
        {
            _isDisplaying = false;
        }
    }
    /// <summary>今回描画したエントリだけを _entries から削除</summary>
    private void RemoveProcessedEntries(List<SchizoLogEntry> processed)
    {
        if (processed == null || processed.Count == 0) return;
        // 同一参照を直接比較して削除
        foreach (var e in processed)
        {
            _ = _entries.Remove(e);
        }
}
    // テンプレ生成機能は撤廃
    
    /// <summary>
    /// エントリリストを既存表示バッファに追加してstring化（ブリッジ文節付き）
    /// </summary>
    /*
     * 設計メモ: _displayBuffer 追記設計と重複見えの理由
     * - 本関数は既存の _displayBuffer に対して、新規エントリを末尾に「直接追記」する。
     * - DisplayAllAsync() が割り込みキャンセルされた場合でも、成功時のみ processed エントリを除去するため、
     *   直前に追記対象だったエントリが _entries に残留し、次回呼び出しで再度追記される可能性がある。
     * - このため、連打などで呼び出しが密に重なると、同一内容が視覚上「連なって」見える場合がある。
     * 
     * 改善の方向性（必要になった場合）
     * - ローカル StringBuilder に一旦構築 → 末尾にまとめて Append する（途中キャンセル時に _displayBuffer を汚さない）。
     * - 差分追記（前回 finalText.Length を記録して、そこからの差分だけを対象にする）。
     * - キャンセル時の processed エントリ除去、もしくは重複抑止のための一意キー付与。
     */
    private string ConvertEntriesToString(List<SchizoLogEntry> entries)
    {
        Log($"SchizoLog: ConvertEntriesToString 開始 - entries: {entries?.Count ?? 0}, 既存バッファ長: {_displayBuffer.Length}");
        
        if (entries == null || entries.Count == 0)
        {
            LogWarning("SchizoLog: エントリが空またはNULLです");
            return _displayBuffer.ToString(); // 既存内容をそのまま返す
        }
        
        if (LogText == null)
        {
            LogError("SchizoLog: LogTextがNULLです");
            return _displayBuffer.ToString();
        }
        
        // 新しいエントリを既存バッファに追加
        for (int i = 0; i < entries.Count; i++)
        {
            Log($"SchizoLog: エントリ[{i}] - 優先度:{entries[i].Priority}, 文章:{entries[i].Sentence?.Substring(0, Math.Min(30, entries[i].Sentence?.Length ?? 0))}...");
            
            // ブリッジ文節をランダムで追加（既存バッファが空でない場合、または最初のエントリ以外）
            if ((_displayBuffer.Length > 0 || i > 0) && RandomEx.Shared.NextFloat(0f, 1f) < 0.7f)
            {
                string bridgePhrase = GetRandomBridgePhrase();
                if (!string.IsNullOrEmpty(bridgePhrase))
                {
                    _displayBuffer.Append(bridgePhrase);
                    Log($"SchizoLog: ブリッジ文節追加: {bridgePhrase}");
                }
            }
            
            if (!string.IsNullOrEmpty(entries[i].Sentence))
            {
                _displayBuffer.Append(entries[i].Sentence);
            }
            else
            {
                LogWarning($"SchizoLog: エントリ[{i}]の文章が空です");
            }
        }
        
        string finalText = _displayBuffer.ToString();
        Log($"SchizoLog: 最終テキスト長: {finalText.Length}");
        
        // 先頭文字デバッグ
        if (finalText.Length > 0)
        {
            string headChars = finalText.Substring(0, Math.Min(5, finalText.Length));
            Log($"SchizoLog: 先頭5文字: '{headChars}'");
        }
        
        return finalText;
    }
    
    /// <summary>
    /// テキストに行数制限を適用（行単位で上から削除）
    /// </summary>
    private string ApplyLineLimit(string text)
    {
        Log($"SchizoLog: ApplyLineLimit 開始 - 入力テキスト長: {text?.Length ?? 0}");
        
        if (string.IsNullOrEmpty(text))
        {
            LogWarning("SchizoLog: 入力テキストが空です");
            return "";
        }
        
        if (LogText == null)
        {
            LogError("SchizoLog: LogTextがNULLです");
            return text;
        }
        
        // 一時的にテキストを設定して行数を取得
        string originalText = LogText.text;
        LogText.text = text;
        LogText.ForceMeshUpdate();
        
        int lineCount = LogText.textInfo.lineCount;
        Log($"SchizoLog: 計算された行数: {lineCount}, 最大行数: {_maxLines}");
        
        LogText.text = originalText; // 元に戻す
        
        // 行数が制限内ならそのまま返す
        if (lineCount <= _maxLines)
        {
            Log($"SchizoLog: 行数制限内です（{lineCount}/{_maxLines}行） - テキストをそのまま返します");
            return text;
        }
        
        // 行数が超過している場合、上の行から削除
        Log($"SchizoLog: 行数超過（{lineCount}/{_maxLines}行） - 上の行から削除します");
        return TrimTopLinesToFit(text);
    }
    
    /// <summary>
    /// 文字単位で削除して行数制限内に収める（TextMeshProの自動改行対応）
    /// </summary>
    private string TrimTopLinesToFit(string text)
    {
        Log($"SchizoLog: TrimTopLinesToFit 開始 - 入力テキスト長: {text?.Length ?? 0}");
        
        if (LogText == null || string.IsNullOrEmpty(text))
        {
            LogWarning("SchizoLog: LogTextがNULLまたはテキストが空です");
            return text;
        }
        
        // 文字単位で削除する方式に変更
        string currentText = text;
        int iteration = 0;
        int deleteStep = Math.Max(1, text.Length / 20); // 初期削除ステップ
        
        Log($"SchizoLog: 文字単位削除開始 - 初期ステップ: {deleteStep}文字");
        
        while (currentText.Length > 0)
        {
            iteration++;
            
            // 一時的にテキストを設定して行数を確認
            string originalText = LogText.text;
            LogText.text = currentText;
            LogText.ForceMeshUpdate();
            
            int lineCount = LogText.textInfo.lineCount;
            Log($"SchizoLog: 反復{iteration} - 文字数: {currentText.Length}, 行数: {lineCount}/{_maxLines}");
            
            LogText.text = originalText; // 元に戻す
            
            if (lineCount <= _maxLines)
            {
                int removedChars = text.Length - currentText.Length;
                Log($"SchizoLog: 成功! {removedChars}文字を削除して行数制限内に調整しました（{lineCount}/{_maxLines}行）");
                return currentText;
            }
            
            // 文字を削除（ステップを調整）
            int charsToDelete = Math.Min(deleteStep, currentText.Length);
            if (charsToDelete <= 0) charsToDelete = 1;
            
            string deletedPart = currentText.Substring(0, Math.Min(30, charsToDelete));
            currentText = currentText.Substring(charsToDelete);
            
            Log($"SchizoLog: {charsToDelete}文字削除: {deletedPart}...");
            
            // 進捗が遅い場合はステップを大きくする
            if (iteration > 10 && iteration % 5 == 0)
            {
                deleteStep = Math.Max(deleteStep * 2, currentText.Length / 10);
                Log($"SchizoLog: 削除ステップを増加: {deleteStep}文字");
            }
            
            // 無限ループ防止
            if (iteration > 100)
            {
                LogError("SchizoLog: TrimTopLinesToFitで無限ループが発生しました");
                break;
            }
        }
        
        LogWarning("SchizoLog: 全ての文字を削除しても行数制限内に収まりませんでした");
        return ""; // 全て削除しても収まらない場合
    }
    
    /// <summary>
    /// ランダムなブリッジ文節を取得
    /// </summary>
    private string GetRandomBridgePhrase()
    {
        var list = _bridgeCollection?.BridgePhrases;
        if (list == null || list.Count == 0) return "";
        if (RandomEx.Shared.NextFloat(0f, 1f) >= 0.7f) return ""; // 70%の確率で挿入
        return list[RandomEx.Shared.NextInt(0, list.Count - 1)];
    }
    

    
    /// <summary>
    /// 一文字ずつ追加表示（割り込み時は残りを即描画）
    /// </summary>
    private async UniTask DisplayTextAsync(string fullText,
                                        int startIndex,
                                        CancellationToken token)
    {
        if (!IsUIAlive()) return;               

        Log($"SchizoLog: DisplayTextAsync 開始 full:{fullText.Length}  start:{startIndex}");
        if (string.IsNullOrEmpty(fullText)) return;

        // 既存部分と追加部分に分割
        string firstText = fullText.Substring(0, startIndex);
        string newText   = fullText.Substring(startIndex);
        if (newText.Length == 0) return;

        // バッファを巻き戻し & 既存部分を即表示
        _displayBuffer.Length = startIndex;
        LogText.text = firstText;
        LogText.maxVisibleCharacters = firstText.Length;
        LogText.ForceMeshUpdate();

        //----- 文字送り本体 ----------------------------------------------------
        int i = 0;
        try
        {
            for (; i < newText.Length; i++)
            {
                token.ThrowIfCancellationRequested();

                AppendOneChar(newText[i]);
                await UniTask.Delay(TimeSpan.FromSeconds(_charInterval), cancellationToken: token);
            }
        }
        catch (OperationCanceledException)
        {
            // ====== 割り込み時：既定では残りを一気に描画 ======
            if (!_suppressFlushOnCancel)
            {
                for (; i < newText.Length; i++)
                {
                    AppendOneChar(newText[i]);
                }
                Log("SchizoLog: 中断→残り文字を即時描画完了");
            }
            else
            {
                Log("SchizoLog: 中断→即時描画を抑止しました");
            }
            // 例外は握りつぶして正常終了扱いにする
        }

        Log("SchizoLog: DisplayTextAsync 終了");

        // ---- ローカル関数 ----
        void AppendOneChar(char c)
        {
            if (!IsUIAlive()) return;            

            _displayBuffer.Append(c);

            // 描画更新
            LogText.text = _displayBuffer.ToString();
            LogText.maxVisibleCharacters = _displayBuffer.Length;
            LogText.ForceMeshUpdate();

            // 行数超過なら上行削除
            while (IsUIAlive() && LogText.textInfo.lineCount > _maxLines)
            {
                RemoveTopLineFromDisplayBuffer();
                LogText.text = _displayBuffer.ToString();
                LogText.maxVisibleCharacters = _displayBuffer.Length;
                LogText.ForceMeshUpdate();
            }
        }
    }    
    /// <summary>
    /// _displayBufferから最上段行を削除（TextMeshProのlineInfoに基づく）
    /// </summary>
    private void RemoveTopLineFromDisplayBuffer()
    {
        if (!IsUIAlive()) return;
        if (LogText == null || _displayBuffer.Length == 0)
        {
            LogWarning("SchizoLog: RemoveTopLineFromDisplayBuffer - LogTextがNULLまたは_displayBufferが空です");
            return;
        }
        
        try
        {
            // 現在のテキストでlineInfoを取得
            string currentText = _displayBuffer.ToString();
            LogText.text = currentText;
            LogText.ForceMeshUpdate();
            
            if (LogText.textInfo == null || LogText.textInfo.lineCount == 0)
            {
                LogWarning("SchizoLog: textInfoがNULLまたはlineCountが0です");
                return;
            }
            
            // lineInfo配列の境界チェック
            if (LogText.textInfo.lineInfo == null || LogText.textInfo.lineInfo.Length == 0)
            {
                LogWarning("SchizoLog: lineInfo配列が空またはNULLです");
                return;
            }
            
            // 最上段行の情報を取得
            var firstLine = LogText.textInfo.lineInfo[0];
            int firstChar = firstLine.firstCharacterIndex;
            int lastChar = firstLine.lastCharacterIndex;
            int removeLength = lastChar - firstChar + 1; // 改行を含まない行文字数のみ
            
            Log($"SchizoLog: 最上段行削除 - 削除文字数: {removeLength}, 行文字数: {firstLine.characterCount}");
            
            if (removeLength > 0 && removeLength <= _displayBuffer.Length)
            {
                string removedText = _displayBuffer.ToString(0, Math.Min(30, removeLength));
                // 行本体を削除
                _displayBuffer.Remove(0, removeLength);
                // 行末に改行が含まれていた場合、表示上は次行先頭に来るため追加で1文字削除
                if (_displayBuffer.Length > 0 && _displayBuffer[0] == '\n')
                {
                    _displayBuffer.Remove(0, 1);
                }
                Log($"SchizoLog: 削除されたテキスト: '{removedText}...'");
            }
            else
            {
                LogWarning($"SchizoLog: 削除長が異常です - removeLength: {removeLength}, _displayBuffer.Length: {_displayBuffer.Length}");
            }
        }
        catch (System.Exception e)
        {
            LogError($"SchizoLog: RemoveTopLineFromDisplayBufferでエラーが発生しました - {e.Message}");
            // エラー時は先頭の10文字を強制削除して続行
            if (_displayBuffer.Length > 10)
            {
                _displayBuffer.Remove(0, 10);
                Log("SchizoLog: エラー回避のため先頭10文字を強制削除しました");
            }
        }
    }
    
    /// <summary>
    /// StringBuilderから最上段行を削除（TextMeshProのlineInfoに基づく）
    /// </summary>
    private void RemoveTopLineFromBuilder(System.Text.StringBuilder builder)
    {
        if (!IsUIAlive()) return;
        if (LogText == null || builder.Length == 0)
        {
            LogWarning("SchizoLog: RemoveTopLineFromBuilder - LogTextがNULLまたはbuilderが空です");
            return;
        }
        
        // 現在のテキストでlineInfoを取得
        LogText.text = builder.ToString();
        LogText.ForceMeshUpdate();
        
        if (LogText.textInfo.lineCount == 0)
        {
            LogWarning("SchizoLog: lineCountが0です");
            return;
        }
        
        // 最上段行の情報を取得
        var firstLine = LogText.textInfo.lineInfo[0];
        int removeLength = firstLine.lastCharacterIndex + 1; // 改行文字も含める
        
        Log($"SchizoLog: 最上段行削除 - 削除文字数: {removeLength}, 行文字数: {firstLine.characterCount}");
        
        if (removeLength > 0 && removeLength <= builder.Length)
        {
            string removedText = builder.ToString(0, Math.Min(30, removeLength));
            builder.Remove(0, removeLength);
            Log($"SchizoLog: 削除されたテキスト: '{removedText}...'");
        }
        else
        {
            LogWarning($"SchizoLog: 削除長が異常です - removeLength: {removeLength}, builderLength: {builder.Length}");
        }
    }
    
    // ===========================================
    // テスト用関数（Inspector のボタンから呼び出し用）
    // ===========================================
    
    /// <summary>
    /// テスト用：固定文章を使ってログアニメーションをテスト
    /// </summary>
    [ContextMenu("テストログ実行")]
    public void TestLogAnimation()
    {
        TestLogAnimationAsync().Forget();
    }
    
    /// <summary>
    /// テスト用：非同期でログアニメーションを実行
    /// </summary>
    private async UniTaskVoid TestLogAnimationAsync()
    {
        Log("SchizoLog: TestLogAnimationAsync 開始");
        
        if (_testSentences == null || _testSentences.Count == 0)
        {
            LogWarning("SchizoLog: テスト用文章が設定されていません");
            return;
        }
        
        Log($"SchizoLog: LogTextの状態 = {(LogText != null ? "OK" : "NULL")}");
        
        // 既存のエントリをクリア
        _entries.Clear();
        
        // テスト文章をエントリとして追加
        for (int i = 0; i < _testSentences.Count; i++)
        {
            int priority = i < _testPriorities.Count ? _testPriorities[i] : 1;
            var entry = new SchizoLogEntry(_testSentences[i], priority, _insertOrderCounter++);
            _entries.Add(entry);
            
            Log($"SchizoLog: エントリ{i}追加 - 優先度:{priority}, 文章:{_testSentences[i].Substring(0, Math.Min(20, _testSentences[i].Length))}...");
        }
        
        Log($"SchizoLog: エントリ総数: {_entries.Count}個");
        
        // ログを表示
        await DisplayAllAsync();
        
        Log("SchizoLog: TestLogAnimationAsync 完了");
    }
    
    /// <summary>
    /// テスト用：単一ログを追加
    /// </summary>
    [ContextMenu("単一テストログ追加")]
    public void AddSingleTestLog()
    {
        if (_testSentences == null || _testSentences.Count == 0)
        {
            LogWarning("SchizoLog: テスト用文章が設定されていません");
            return;
        }
        
        int randomIndex = RandomEx.Shared.NextInt(0, _testSentences.Count - 1);
        string sentence = _testSentences[randomIndex];
        int priority = randomIndex < _testPriorities.Count ? _testPriorities[randomIndex] : RandomEx.Shared.NextInt(1, 10);
        
        var entry = new SchizoLogEntry(sentence, priority, _insertOrderCounter++);
        _entries.Add(entry);
        
        Log($"SchizoLog: 単一エントリ追加 - 優先度:{priority}, 文章:{sentence.Substring(0, Math.Min(30, sentence.Length))}...");
    }
    
    /// <summary>
    /// テスト用：現在登録されているログを表示
    /// </summary>
    [ContextMenu("現在のログを表示")]
    public void DisplayCurrentLogs()
    {
        DisplayAllAsync().Forget();
    }
    
    /// <summary>
    /// テスト用：現在のエントリ内容を表示
    /// </summary>
    [ContextMenu("エントリ内容表示")]
    public void ShowCurrentBuffer()
    {
        if (_entries.Count == 0)
        {
            Log("SchizoLog: エントリは空です");
        }
        else
        {
            Log($"SchizoLog: エントリ数: {_entries.Count}");
            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                Log($"  [{i}] 優先度:{entry.Priority}, 順序:{entry.InsertOrder}, 文章:{entry.Sentence.Substring(0, Math.Min(50, entry.Sentence.Length))}...");
            }
        }
    }
    
    /// <summary>
    /// テスト用：ログをクリア
    /// </summary>
    [ContextMenu("ログクリア")]
    public void ClearLogs()
    {
        _entries.Clear();
        _insertOrderCounter = 0;
        _displayBuffer.Clear(); // 永続バッファもクリア
        if (LogText != null)
        {
            LogText.text = "";
            LogText.maxVisibleCharacters = 0;
        }
        Log("SchizoLog: エントリと_displayBufferをクリアしました");
    }

    /// <summary>
    /// 進行中の表示をフラッシュなしで停止し、その後すべてをクリアする（既存仕様に影響しない安全な停止）
    /// </summary>
    public async UniTask HardStopAndClearAsync()
    {
        // フラッシュ抑止を有効化
        _suppressFlushOnCancel = true;

        try
        {
            // 進行中であればキャンセルを投げる
            _displayCts?.Cancel();

            // 表示ループの完全終了を待機（DisplayAllAsync の finally で _isDisplaying=false になる）
            await UniTask.WaitUntil(() => !_isDisplaying);
        }
        catch (Exception)
        {
            // 念のため握りつぶし（停止優先）
        }
        finally
        {
            // すべてクリア（エントリ、バッファ、UI）
            ClearLogs();
            // 抑止を元に戻す（既存挙動へ）
            _suppressFlushOnCancel = false;
        }
    }

    // =========================
    // デバッグログ・ラッパー
    // =========================
    private void Log(string message)
    {
        if (_enableDebugLog)
        {
            Debug.Log(message);
        }
    }

    private void LogWarning(string message)
    {
        if (_enableDebugLog)
        {
            Debug.LogWarning(message);
        }
    }

    private void LogError(string message)
    {
        if (_enableDebugLog)
        {
            Debug.LogError(message);
        }
    }
}