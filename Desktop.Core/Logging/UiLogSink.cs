using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;

namespace OpenShock.Desktop.Logging;

public class UiLogSink : ILogEventSink
{
    private readonly MessageTemplateTextFormatter _messageProvider;
    /// <summary>
    /// Shows an error to the user. Set by the UI, which decides how (a snackbar, today).
    /// </summary>
    public static Action<string>? ErrorNotification { get; set; }

    public UiLogSink()
    {
        _messageProvider = new MessageTemplateTextFormatter("{Message:lj} {NewLine}{Exception}");
    }

    public void Emit(LogEvent logEvent)
    {
        using var textWriter = new StringWriter();
        _messageProvider.Format(logEvent, textWriter);
        
        var logMessage = textWriter.ToString();
        if (string.IsNullOrEmpty(logMessage)) return;
        if (logMessage.StartsWith("[Microsoft.AspNetCore.Http.Connections.Client.Internal.LoggingHttpMessageHandler] "))
            ErrorNotification?.Invoke(logMessage[82..]);

        var sourceContextString = string.Empty;

        if (logEvent.Properties.TryGetValue("SourceContext", out var sourceContext) && sourceContext is ScalarValue
            {
                Value: string scalarString
            }) sourceContextString = scalarString;

        var lastIndexSlash = Math.Min(sourceContextString.Length, sourceContextString.LastIndexOf('.') + 1);

        LogStore.AddLog(new LogStore.LogEntry
        {
            Message = logMessage,
            Time = logEvent.Timestamp,
            Level = logEvent.Level,
            SourceContext = sourceContextString,
            SourceContextShort = sourceContextString[lastIndexSlash..]
        });
    }
}

public static class UiLogSinkExtensions
{
    public static LoggerConfiguration UiLogSink(
        this LoggerSinkConfiguration sinkConfiguration)
    {
        var sink = new UiLogSink();
        return sinkConfiguration.Sink(sink);
    }
}