public sealed class PlayersProgressTracker : IPlayersProgress
{
    public int NowProgress { get; private set; }
    public int NowStageID { get; private set; }
    public int NowAreaID { get; private set; }

    public void ResetProgress()
    {
        NowProgress = 0;
    }

    public void ProgressReset()
    {
        ResetProgress();
    }

    public void ResetAll()
    {
        NowProgress = 0;
        NowStageID = 0;
        NowAreaID = 0;
    }

    public void AddProgress(int addPoint)
    {
        NowProgress += addPoint;
    }

    public void SetProgress(int value)
    {
        NowProgress = value;
    }

    public void SetStage(int id)
    {
        NowStageID = id;
    }

    public void SetArea(int id)
    {
        NowAreaID = id;
    }
}
