using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace WarehouseDuel.EditorTools
{
    /// <summary>
    /// Collects results from setup/validation steps and prints one readable summary to the Console.
    /// </summary>
    internal sealed class SetupReport
    {
        private enum Severity { Pass, Info, Warning, Error }

        private readonly List<(Severity severity, string message)> entries = new List<(Severity, string)>();

        public int ErrorCount { get; private set; }
        public int WarningCount { get; private set; }

        public string Summary => ErrorCount == 0 && WarningCount == 0
            ? "All checks passed."
            : $"{ErrorCount} error(s), {WarningCount} warning(s).";

        public void Pass(string message) => entries.Add((Severity.Pass, message));
        public void Info(string message) => entries.Add((Severity.Info, message));

        public void Warn(string message)
        {
            WarningCount++;
            entries.Add((Severity.Warning, message));
        }

        public void Error(string message)
        {
            ErrorCount++;
            entries.Add((Severity.Error, message));
        }

        public void PrintToConsole(string title)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[Warehouse Duel] {title}: {Summary}");
            foreach (var (severity, message) in entries)
            {
                sb.Append("  ").Append(Tag(severity)).Append(' ').AppendLine(message);
            }

            if (ErrorCount > 0) Debug.LogError(sb.ToString());
            else if (WarningCount > 0) Debug.LogWarning(sb.ToString());
            else Debug.Log(sb.ToString());
        }

        private static string Tag(Severity severity) => severity switch
        {
            Severity.Pass => "[ OK ]",
            Severity.Info => "[INFO]",
            Severity.Warning => "[WARN]",
            _ => "[FAIL]",
        };
    }
}
