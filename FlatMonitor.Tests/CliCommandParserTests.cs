using NUnit.Framework;
using FluentAssertions;

namespace FlatMonitor.Tests;

[TestFixture]
public class CliCommandParserTests
{
    [TestCase(new string[0], CliCommand.WebServer, TestName = "NoArguments_StartsTheWebServer")]
    [TestCase(new[] { "scrape" }, CliCommand.Scrape)]
    [TestCase(new[] { "explore-is24" }, CliCommand.ExploreImmobilienScout24)]
    [TestCase(new[] { "--help" }, CliCommand.Help)]
    [TestCase(new[] { "-h" }, CliCommand.Help)]
    [TestCase(new[] { "--urls", "http://localhost:5050" }, CliCommand.WebServer, TestName = "Options_GoToTheWebServer")]
    [TestCase(new[] { "scrap" }, CliCommand.Unknown, TestName = "Typo_IsUnknownInsteadOfStartingTheWebServer")]
    [TestCase(new[] { "report" }, CliCommand.Unknown)]
    [TestCase(new[] { "Scrape" }, CliCommand.Unknown, TestName = "Commands_AreCaseSensitive")]
    public void Parse_ReturnsTheCommand(string[] args, CliCommand expected)
    {
        CliCommandParser.Parse(args).Should().Be(expected);
    }
}
