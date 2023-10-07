using MongoDB.Bson;
using MongoDB.Bson.IO;

public class DebugViewModelLog
{
    public DateTime TimeStamp { get; set; }
    public string Header { get; set; }
    public string Detail { get; set; }

    public bool ShowDetail { get; set; }

    public DebugViewModelLog(DateTime timeStamp, string header, string detail)
    {
        TimeStamp = timeStamp;
        Header = header;
        Detail = detail;
    }
}

public class DebugViewModel
{
    private static JsonWriterSettings jsonWriterSettings = new JsonWriterSettings { OutputMode = JsonOutputMode.CanonicalExtendedJson, Indent = true };

    public List<DebugViewModelLog> Logs { get; set; } = new List<DebugViewModelLog>();

    public void AddLog(string header, string detail)
    {
        Logs.Add(new DebugViewModelLog(DateTime.Now, header, detail));
    }

    public void AddLog(string header, object detail)
    {
        Logs.Add(new DebugViewModelLog(DateTime.Now, header, detail.ToJson(jsonWriterSettings)));
    }

    public void Clear()
    {
        Logs.Clear();
    }
}
