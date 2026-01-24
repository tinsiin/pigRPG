/// <summary>
/// パッシブデータへのアクセスを抽象化するインターフェース。
/// Phase 3b: PassiveManager.Instanceへの直接依存を解消するため。
/// </summary>
public interface IPassiveProvider
{
    /// <summary>
    /// IDからパッシブを取得
    /// </summary>
    BasePassive GetAtID(int id);
}
