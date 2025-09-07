using System.Text;
using System.Linq;
using TMPro;
using UnityEngine;

/// <summary>
/// パッシブのテキスト整形/計測で共通利用するユーティリティ。
/// - TMPの描画設定（測定と描画の前提統一）
/// - パッシブトークン列の生成（実データ/ダミー）
/// - 高さフィット判定
/// - 省略（••••）付きフィット
/// </summary>
public static class PassiveTextUtils
{
    // 共通TMP設定（描画）
    public static void SetupTmpBasics(TextMeshProUGUI tmp, bool truncate = true)
    {
        if (tmp == null) return;
        tmp.enableWordWrapping = true;
        tmp.richText = false; // <> をリテラル表示
        tmp.enableAutoSizing = false;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.overflowMode = truncate ? TextOverflowModes.Truncate : TextOverflowModes.Overflow;
    }

    // 実データから <token> 列を生成
    public static string BuildPassivesTokens(BaseStates actor)
    {
        if (actor == null || actor.Passives == null || actor.Passives.Count == 0)
            return string.Empty;

        // 並び替えキーを算出（優先順位：DurationTurn → DurationTurnCounter → DurationWalk → DurationWalkCounter）
        // -1 などの負値は未使用/無期限扱いとして最後に寄せるため int.MaxValue にマップ
        var ordered = actor.Passives
            .Select((p, idx) => new { p, idx })
            .Where(x => x.p != null)
            .Select(x => new
            {
                x.p,
                x.idx,
                kTurn = x.p.DurationTurn < 0 ? int.MaxValue : x.p.DurationTurn,
                kTurnCnt = (x.p.DurationTurn < 0 || x.p.DurationTurnCounter < 0) ? int.MaxValue : x.p.DurationTurnCounter,
                kWalk = x.p.DurationWalk < 0 ? int.MaxValue : x.p.DurationWalk,
                kWalkCnt = (x.p.DurationWalk < 0 || x.p.DurationWalkCounter < 0) ? int.MaxValue : x.p.DurationWalkCounter
            })
            .OrderBy(x => x.kTurn)
            .ThenBy(x => x.kTurnCnt)
            .ThenBy(x => x.kWalk)
            .ThenBy(x => x.kWalkCnt)
            .ThenBy(x => x.idx) // 安定化（完全同値時は元の順序）
            .Select(x => x.p);

        var sb = new StringBuilder();
        bool first = true;
        foreach (var p in ordered)
        {
            string raw = string.IsNullOrWhiteSpace(p.SmallPassiveName) ? p.ID.ToString() : p.SmallPassiveName;
            string token = $"<{raw}>";
            if (!first) sb.Append(' ');
            sb.Append(token);
            first = false;
        }
        return sb.ToString();
    }

    // ダミー <token> 列を生成
    public static string BuildDummyPassivesTokens(int count, string prefix)
    {
        if (count <= 0) return string.Empty;
        var sb = new StringBuilder();
        bool first = true;
        string pre = string.IsNullOrEmpty(prefix) ? "pas" : prefix;
        for (int i = 1; i <= count; i++)
        {
            string token = $"<{pre}{i}>";
            if (!first) sb.Append(' ');
            sb.Append(token);
            first = false;
        }
        return sb.ToString();
    }

    // 高さフィット（ドット無しでの事前判定）。候補を tmp の RectTransform 高さに収められるか
    public static bool FitsHeight(TextMeshProUGUI tmp, string candidate, float safety)
    {
        if (tmp == null) return false;
        if (string.IsNullOrEmpty(candidate)) return true;

        string original = tmp.text;
        var prevRich = tmp.richText;
        var prevWrap = tmp.enableWordWrapping;
        var prevAuto = tmp.enableAutoSizing;
        var prevOverflow = tmp.overflowMode;

        Canvas.ForceUpdateCanvases();
        var rt = tmp.rectTransform;
        var container = rt.rect;

        tmp.richText = false;
        tmp.enableWordWrapping = true;
        tmp.enableAutoSizing = false;
        tmp.overflowMode = TextOverflowModes.Overflow; // 測定時はOverflow
        tmp.text = candidate;
        tmp.ForceMeshUpdate();

        float height = tmp.preferredHeight;
        bool ok = height <= container.height - Mathf.Max(0f, safety);

        // 戻す
        tmp.richText = prevRich;
        tmp.enableWordWrapping = prevWrap;
        tmp.enableAutoSizing = prevAuto;
        tmp.overflowMode = prevOverflow;
        tmp.text = original;

        return ok;
    }

    // 省略（••••）を考慮して、src を tmp の高さに収まるように整形して返す
    // 注意: 測定時はOverflowにし、描画は呼び出し側で Truncate を基本とする
    public static string FitTextIntoRectWithEllipsis(string src, TextMeshProUGUI tmp, int dotCount, float safety, bool alwaysAppendEllipsis)
    {
        if (tmp == null) return string.Empty;
        if (string.IsNullOrEmpty(src)) return string.Empty;

        string ellipsis = new string('•', Mathf.Max(1, dotCount));
        string original = tmp.text;

        bool Fits(string candidate)
        {
            Canvas.ForceUpdateCanvases();
            var tmpRT = tmp.rectTransform;
            var containerRect = tmpRT.rect;

            var prevRich = tmp.richText;
            var prevWrap = tmp.enableWordWrapping;
            var prevAuto = tmp.enableAutoSizing;
            var prevOverflow = tmp.overflowMode;

            tmp.richText = false;
            tmp.enableWordWrapping = true;
            tmp.enableAutoSizing = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.text = candidate;
            tmp.ForceMeshUpdate();

            float height = tmp.preferredHeight;
            bool ok = height <= containerRect.height - Mathf.Max(0f, safety);

            tmp.richText = prevRich;
            tmp.enableWordWrapping = prevWrap;
            tmp.enableAutoSizing = prevAuto;
            tmp.overflowMode = prevOverflow;
            return ok;
        }

        bool srcFits = Fits(src);
        bool srcWithDotsFits = Fits(src + ellipsis);
        if (alwaysAppendEllipsis)
        {
            if (srcWithDotsFits)
            {
                tmp.text = original;
                return src + ellipsis;
            }
        }
        else
        {
            if (srcFits)
            {
                tmp.text = original;
                return src;
            }
            if (srcWithDotsFits)
            {
                tmp.text = original;
                return src + ellipsis;
            }
        }

        int lo = 0, hi = src.Length, bestLen = 0;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            string cand = src.Substring(0, mid);
            cand = AvoidLoneOpeningBracket(cand);
            string composed = cand + ellipsis;
            if (Fits(composed))
            {
                bestLen = cand.Length;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        string best = src.Substring(0, Mathf.Min(bestLen, src.Length));
        best = AvoidLoneOpeningBracket(best);
        string result = best.Length > 0 ? best + ellipsis : string.Empty;

        int guard = 0;
        while (!string.IsNullOrEmpty(result) && !Fits(result) && guard++ < 128)
        {
            string withoutDots = result.EndsWith(ellipsis) ? result.Substring(0, result.Length - ellipsis.Length) : result;
            int prevSpace = withoutDots.LastIndexOf(' ');
            if (prevSpace <= 0)
            {
                withoutDots = withoutDots.Length > 0 ? withoutDots.Substring(0, withoutDots.Length - 1) : string.Empty;
            }
            else
            {
                withoutDots = withoutDots.Substring(0, prevSpace);
            }
            withoutDots = AvoidLoneOpeningBracket(withoutDots);
            result = string.IsNullOrEmpty(withoutDots) ? string.Empty : withoutDots + ellipsis;
        }

        if (string.IsNullOrEmpty(result))
        {
            var tokens = src.Split(' ');
            var acc = new StringBuilder();
            for (int i = 0; i < tokens.Length; i++)
            {
                string next = tokens[i];
                string trial = acc.Length == 0 ? next + ellipsis : acc.ToString() + " " + next + ellipsis;
                if (Fits(trial))
                {
                    if (acc.Length > 0) acc.Append(' ');
                    acc.Append(next);
                }
                else
                {
                    break;
                }
            }
            result = acc.Length == 0 ? ellipsis : acc.ToString() + ellipsis;
        }

        tmp.text = original;
        return result;
    }

    // 末尾が "<" 単体で終わる文字列を避ける（直前の空白まで戻す）。
    public static string AvoidLoneOpeningBracket(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        int lastOpen = s.LastIndexOf('<');
        int lastClose = s.LastIndexOf('>');
        if (lastOpen > lastClose)
        {
            int i = lastOpen + 1;
            bool hasVisible = i < s.Length;
            if (!hasVisible)
            {
                int prevSpace = s.LastIndexOf(' ', lastOpen - 1);
                if (prevSpace >= 0) return s.Substring(0, prevSpace);
                else return string.Empty;
            }
        }
        return s;
    }
}
