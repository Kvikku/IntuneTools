using IntuneTools.Utilities;
using Xunit;

namespace IntuneTools.Tests;

public class LogEntryTests
{
    [Fact]
    public void Info_CreatesInfoLevel()
    {
        var entry = LogEntry.Info("test message");

        Assert.Equal(LogLevel.Info, entry.Level);
        Assert.Equal("test message", entry.Message);
    }

    [Fact]
    public void Success_CreatesSuccessLevel()
    {
        var entry = LogEntry.Success("done");

        Assert.Equal(LogLevel.Success, entry.Level);
        Assert.Equal("done", entry.Message);
    }

    [Fact]
    public void Warning_CreatesWarningLevel()
    {
        var entry = LogEntry.Warning("careful");

        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("careful", entry.Message);
    }

    [Fact]
    public void Error_CreatesErrorLevel()
    {
        var entry = LogEntry.Error("failure");

        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal("failure", entry.Message);
    }

    [Fact]
    public void Timestamp_IsSetOnConstruction()
    {
        var before = DateTime.Now;
        var entry = LogEntry.Info("test");
        var after = DateTime.Now;

        Assert.InRange(entry.Timestamp, before, after);
    }

    [Fact]
    public void TimestampText_MatchesHHmmssFormat()
    {
        var entry = LogEntry.Info("test");

        var text = entry.TimestampText;

        // Should be parseable as HH:mm:ss
        Assert.Matches(@"^\d{2}:\d{2}:\d{2}$", text);
    }

    [Theory]
    [InlineData(LogLevel.Success, "\u2714")]
    [InlineData(LogLevel.Warning, "\u26A0")]
    [InlineData(LogLevel.Error, "\u2716")]
    [InlineData(LogLevel.Info, "\u2022")]
    public void LevelIndicator_ReturnsCorrectSymbol(LogLevel level, string expectedSymbol)
    {
        var entry = new LogEntry(level, "test");

        Assert.Equal(expectedSymbol, entry.LevelIndicator);
    }

    [Fact]
    public void Constructor_PreservesMessageExactly()
    {
        var message = "  spaces  and\nnewlines  ";
        var entry = new LogEntry(LogLevel.Info, message);

        Assert.Equal(message, entry.Message);
    }
}
