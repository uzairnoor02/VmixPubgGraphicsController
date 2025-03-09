using Microsoft.Extensions.Logging;
using System;
using System.IO;

public class FileLogger : ILogger
{
    private readonly string filePath;
    private static readonly object _lock = new object();

    public FileLogger(string path)
    {
        filePath = path;
    }

    public IDisposable BeginScope<TState>(TState state)
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        if (formatter != null)
        {
            lock (_lock)
            {
                DateTime now = DateTime.Now;
                int minutes = now.Minute < 30 ? 0 : 30; // Round to the nearest half hour
                string timestamp = new DateTime(now.Year, now.Month, now.Day, now.Hour, minutes, 0).ToString("yyyy-MM-dd-HH-mm");
                string fullFilePath = Path.Combine(filePath, timestamp + "_log.txt");

                // Ensure the directory exists
                Directory.CreateDirectory(filePath);

                var n = Environment.NewLine;
                string exc = "";
                if (exception != null) exc = n + exception.GetType() + ": " + exception.Message + n + exception.StackTrace + n;
                File.AppendAllText(fullFilePath, logLevel.ToString() + ": " + DateTime.Now.ToString() + " " + formatter(state, exception) + n + exc);
            }
        }
    }
}
