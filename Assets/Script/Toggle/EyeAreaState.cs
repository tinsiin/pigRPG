/// <summary>
/// EyeArea側の表示状態。
/// USERUIのTabStateとは別に、EyeArea内のContent切替を管理する。
/// </summary>
public enum EyeAreaState
{
    /// <summary>歩行中（ActionMark等）</summary>
    Walk,

    /// <summary>ノベルパート（ディノイド、立ち絵、背景あり/なし全て含む）</summary>
    Novel,

    /// <summary>バトル中（EnemyArea等）</summary>
    Battle,
}
