namespace Starkive;

public class HistoryViewModel
{
    private readonly HistoryEntry _entry;
    public HistoryViewModel(HistoryEntry entry) => _entry = entry;
    public string TypeIcon    => _entry.Type == OperationType.Zip ? "" : "";
    public string SourcePath  => _entry.SourcePath;
    public string OutputPath  => _entry.OutputPath;
    public string FormattedTime => _entry.Timestamp.ToString("MMM d, h:mm tt");
}
