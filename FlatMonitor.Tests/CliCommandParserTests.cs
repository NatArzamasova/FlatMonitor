using NUnit.Framework;
using FluentAssertions;

namespace FlatMonitor.Tests;

[TestFixture]
public class CliCommandParserTests
{
    [TestCase(new string[0], CliCommand.WebServer, TestName = "NoArguments_StartsTheWebServer")]
    [TestCase(new[] { "scrape" }, CliCommand.Scrape)]
    [TestCase(new[] { "scrape", "--source", "Immowelt" }, CliCommand.Scrape, TestName = "Scrape_TakesTheSourceOption")]
    [TestCase(new[] { "explore-is24" }, CliCommand.ExploreImmobilienScout24)]
    [TestCase(new[] { "explore-immowelt" }, CliCommand.ExploreImmowelt)]
    [TestCase(new[] { "explore-immowelt", "--headless" }, CliCommand.ExploreImmowelt, TestName = "ExploreImmowelt_TakesTheHeadlessOption")]
    [TestCase(new[] { "explore-immowelt", "--cdp" }, CliCommand.ExploreImmowelt, TestName = "ExploreImmowelt_TakesTheCdpOption")]
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

    [TestCase("scrape", null, TestName = "ParseSources_WithoutOption_IsNull")]
    [TestCase("scrape --source Immowelt", "Immowelt")]
    [TestCase("scrape --source OhneMakler,Immowelt", "OhneMakler,Immowelt")]
    public void ParseSources_ReturnsTheOptionsValue(string commandLine, string? expected)
    {
        CliCommandParser.ParseSources(commandLine.Split(' ')).Should().Be(expected);
    }

    [TestCase("scrape --source", TestName = "ParseSources_OptionAtTheEnd_Fails")]
    [TestCase("scrape --source --verbose", TestName = "ParseSources_OptionFollowedByAnOption_Fails")]
    public void ParseSources_WithoutValue_FailsWithAnExample(string commandLine)
    {
        var act = () => CliCommandParser.ParseSources(commandLine.Split(' '));

        act.Should().Throw<ArgumentException>().WithMessage("--source needs a source name, e.g. --source Immowelt");
    }
}
