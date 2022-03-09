using System.Text;

using Spectre.Console;

namespace ThemesOfDotNet.Indexing;

public static class GitHubActions
{
    public struct Group : IDisposable
    {
        public void Dispose()
        {
            EndGroup();
        }
    }

    public static bool SeenErrors { get; private set; }

    public static bool IsRunningInside
    {
        get => Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true";
    }

    public static Group BeginGroup(string title)
    {
        if (IsRunningInside)
            Console.WriteLine($"::group::{title}");
        return new Group();
    }

    public static void EndGroup()
    {
        if (IsRunningInside)
            Console.WriteLine($"::endgroup::");
    }

    public static void Notice(string message,
                              string? fileName = null,
                              int? line = null,
                              int? endLine = null,
                              int? column = null,
                              int? endColumn = null)
    {
        Message("notice", message, fileName, line, endLine, column, endColumn);
    }

    public static void Warning(string message,
                               string? fileName = null,
                               int? line = null,
                               int? endLine = null,
                               int? column = null,
                               int? endColumn = null)
    {
        Message("warning", message, fileName, line, endLine, column, endColumn);
    }

    public static void Warning(Exception exception)
    {
        if (!IsRunningInside)
        {
            AnsiConsole.WriteException(exception);
        }
        else
        {
            Warning(exception.ToString());
        }
    }

    public static void Error(Exception exception)
    {
        if (!IsRunningInside)
        {
            AnsiConsole.WriteException(exception);
        }
        else
        {
            Error(exception.ToString());
        }
    }

    public static void Error(string message,
                             string? fileName = null,
                             int? line = null,
                             int? endLine = null,
                             int? column = null,
                             int? endColumn = null)
    {
        Message("error", message, fileName, line, endLine, column, endColumn);
    }

    private static void Message(string kind,
                                string message,
                                string? fileName = null,
                                int? line = null,
                                int? endLine = null,
                                int? column = null,
                                int? endColumn = null)
    {
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(message);

        if (kind == "error")
            SeenErrors = true;

        if (!IsRunningInside)
        {
            var sb = new StringBuilder();

            if (kind == "error")
                sb.Append("[red]");
            else if (kind == "warning")
                sb.Append("[yellow]");

            sb.Append(kind);
            sb.Append(": ");

            if (fileName is not null)
            {
                sb.Append(fileName);
                sb.Append(": ");
            }

            sb.Append(message.EscapeMarkup());

            if (kind == "error" || kind == "warning")
                sb.Append("[/]");

            AnsiConsole.MarkupLine(sb.ToString());
        }
        else
        {
            var sb = new StringBuilder();
            sb.Append($"::{kind}");

            if (fileName is not null)
            {
                sb.Append($" file={fileName}");

                if (line is not null)
                    sb.Append($",line={line}");

                if (endLine is not null)
                    sb.Append($",endLine={endLine}");

                if (column is not null)
                    sb.Append($",col={column}");

                if (endColumn is not null)
                    sb.Append($",endCol={endColumn}");
            }

            sb.Append($"::{message}");

            Console.WriteLine(sb.ToString());
            Console.WriteLine(sb.ToString().Substring(2));
        }
    }
}
