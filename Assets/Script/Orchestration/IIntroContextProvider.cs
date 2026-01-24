/// <summary>
/// IIntroContext を提供するためのプロバイダーI/F。
/// WatchUIUpdate など、文脈を持つクラスが実装する。
/// </summary>
public interface IIntroContextProvider
{
    IIntroContext BuildIntroContext();
}
