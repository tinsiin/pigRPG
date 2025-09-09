public interface IResultFormatter
{
    string Header(int presetCount, int repeat);
    string SummaryLine(object preset, BenchmarkSummary summary);
    string Footer();
}
