namespace PhysicalKBLayoutSwitcher.App.Services;

public static class AppLog
{
    private static readonly Lock SyncRoot = new();
    private static readonly string LogFilePath;

    static AppLog()
    {
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PhysicalKBLayoutSwitcher");

        Directory.CreateDirectory(logDirectory);
        LogFilePath = Path.Combine(logDirectory, "session.log");
    }

    public static string LogPath => LogFilePath;

    public static void Info(string message)
    {
        Write("INFO", message);
    }

    public static void Error(string message, Exception? exception = null)
    {
        var fullMessage = exception is null
            ? message
            : $"{message}{Environment.NewLine}{exception}";

        Write("ERROR", fullMessage);
    }

    private static void Write(string level, string message)
    {
        lock (SyncRoot)
        {
            File.AppendAllText(
                LogFilePath,
                $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{level}] {message}{Environment.NewLine}");
        }
    }
}
