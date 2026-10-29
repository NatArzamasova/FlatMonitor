using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using FluentAssertions;
using NUnit.Framework;

namespace FlatMonitor.Tests;

/// <summary>
/// Logging goes through ILogger&lt;T&gt; so levels are configurable. Only Program.cs may write to the console
/// (CLI usage, the scrape summary, fatal errors before a host exists).
/// </summary>
[TestFixture]
public class NoConsoleOutputTests
{
    private static readonly Regex ConsoleCall = new(@"\bConsole\s*\.", RegexOptions.Compiled);

    [Test]
    public void ProductionCode_OutsideProgram_DoesNotWriteToConsole()
    {
        var projectDir = Path.Combine(FindRepositoryRoot(), "FlatMonitor");
        var files = Directory.EnumerateFiles(projectDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsGenerated(f) && Path.GetFileName(f) != "Program.cs")
            .ToList();
        files.Should().Contain(f => f.EndsWith("ScraperCoordinator.cs"), "the scan must cover the production code");

        var offenders = files
            .SelectMany(f => File.ReadLines(f)
                .Select((line, i) => (File: Path.GetRelativePath(projectDir, f), Line: i + 1, Text: line.Trim()))
                .Where(l => !l.Text.StartsWith("//") && ConsoleCall.IsMatch(l.Text)))
            .Select(l => $"{l.File}:{l.Line}: {l.Text}")
            .ToList();

        offenders.Should().BeEmpty("use an injected ILogger instead of Console");
    }

    private static bool IsGenerated(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Contains("obj") || parts.Contains("bin") || parts.Contains("Migrations");
    }

    /// <summary>
    /// The folder with FlatMonitor.sln: above the test output (normal builds), else above this source file
    /// (builds with a separate --artifacts-path).
    /// </summary>
    private static string FindRepositoryRoot([CallerFilePath] string sourceFile = "")
    {
        return FindAbove(TestContext.CurrentContext.TestDirectory)
            ?? FindAbove(Path.GetDirectoryName(sourceFile))
            ?? throw new InvalidOperationException("FlatMonitor.sln not found above the test output or the test sources");

        static string? FindAbove(string? start)
        {
            var dir = string.IsNullOrEmpty(start) ? null : new DirectoryInfo(start);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "FlatMonitor.sln")))
                dir = dir.Parent;
            return dir?.FullName;
        }
    }
}
