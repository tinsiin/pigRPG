/// <summary>
/// パッシブ致死ログ1件分の情報
/// </summary>
public class PassiveKillEntry
{
    /// <summary>死亡した敵の名前</summary>
    public string VictimName;

    /// <summary>致死パッシブの付与者の名前（nullなら付与者不明）</summary>
    public string GrantorName;

    /// <summary>致死パッシブの名前</summary>
    public string PassiveName;
}
