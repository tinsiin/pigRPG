using System;

public static class BattleProtocolExtensions
{
    public static string ToDisplayText(this BattleProtocol protocol)
    {
        switch (protocol)
        {
            case BattleProtocol.LowKey: return "地味";
            case BattleProtocol.Tricky: return "トライキー";
            case BattleProtocol.Showey: return "派手";
            case BattleProtocol.none:   return "なし";
            default: return protocol.ToString();
        }
    }

    public static string ToDisplayShortText(this BattleProtocol protocol)
    {
        switch (protocol)
        {
            case BattleProtocol.LowKey: return "地味";
            case BattleProtocol.Tricky: return "トラ";
            case BattleProtocol.Showey: return "派手";
            case BattleProtocol.none:   return "無";
            default: return protocol.ToString();
        }
    }
}
