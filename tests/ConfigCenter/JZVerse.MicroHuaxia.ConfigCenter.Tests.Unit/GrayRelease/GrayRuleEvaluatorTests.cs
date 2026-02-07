using FluentAssertions;
using JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;
using JZVerse.MicroHuaxia.ConfigCenter.Core.GrayReleases;

namespace JZVerse.MicroHuaxia.ConfigCenter.Tests.Unit.GrayRelease;

[TestFixture]
public class GrayRuleEvaluatorTests
{
    [Test]
    public void Matches_ShouldReturnTrue_WhenIpMatches()
    {
        // Arrange
        var rules = new List<GrayReleaseRule>
        {
            new() { RuleType = GrayRuleType.IP, MatchPattern = "192.168.1.100", Priority = 1 },
        };
        var clientInfo = new ClientInfo { ClientId = "client1", IpAddress = "192.168.1.100" };

        // Act
        var result = GrayRuleEvaluator.Matches(rules, clientInfo);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void Matches_ShouldReturnTrue_WhenIpMatchesWildcard()
    {
        // Arrange
        var rules = new List<GrayReleaseRule>
        {
            new() { RuleType = GrayRuleType.IP, MatchPattern = "192.168.1.*", Priority = 1 },
        };
        var clientInfo = new ClientInfo { ClientId = "client1", IpAddress = "192.168.1.200" };

        // Act
        var result = GrayRuleEvaluator.Matches(rules, clientInfo);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void Matches_ShouldReturnFalse_WhenIpDoesNotMatch()
    {
        // Arrange
        var rules = new List<GrayReleaseRule>
        {
            new() { RuleType = GrayRuleType.IP, MatchPattern = "192.168.1.100", Priority = 1 },
        };
        var clientInfo = new ClientInfo { ClientId = "client1", IpAddress = "10.0.0.1" };

        // Act
        var result = GrayRuleEvaluator.Matches(rules, clientInfo);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void Matches_ShouldReturnTrue_WhenTagMatches()
    {
        // Arrange
        var rules = new List<GrayReleaseRule>
        {
            new() { RuleType = GrayRuleType.Tag, MatchPattern = "beta,canary", Priority = 1 },
        };
        var clientInfo = new ClientInfo { ClientId = "client1", Tags = ["beta", "production"] };

        // Act
        var result = GrayRuleEvaluator.Matches(rules, clientInfo);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void Matches_ShouldReturnFalse_WhenNoTagMatches()
    {
        // Arrange
        var rules = new List<GrayReleaseRule>
        {
            new() { RuleType = GrayRuleType.Tag, MatchPattern = "beta,canary", Priority = 1 },
        };
        var clientInfo = new ClientInfo { ClientId = "client1", Tags = ["production", "stable"] };

        // Act
        var result = GrayRuleEvaluator.Matches(rules, clientInfo);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void Matches_ShouldReturnTrue_WhenClientIdMatches()
    {
        // Arrange
        var rules = new List<GrayReleaseRule>
        {
            new() { RuleType = GrayRuleType.ClientId, MatchPattern = "client1,client2,client3", Priority = 1 },
        };
        var clientInfo = new ClientInfo { ClientId = "client2" };

        // Act
        var result = GrayRuleEvaluator.Matches(rules, clientInfo);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void Matches_ShouldReturnFalse_WhenClientIdDoesNotMatch()
    {
        // Arrange
        var rules = new List<GrayReleaseRule>
        {
            new() { RuleType = GrayRuleType.ClientId, MatchPattern = "client1,client2,client3", Priority = 1 },
        };
        var clientInfo = new ClientInfo { ClientId = "client99" };

        // Act
        var result = GrayRuleEvaluator.Matches(rules, clientInfo);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void Matches_ShouldReturnTrue_WhenPercentageIs100()
    {
        // Arrange
        var rules = new List<GrayReleaseRule>
        {
            new() { RuleType = GrayRuleType.Percentage, MatchPattern = "100", Priority = 1 },
        };
        var clientInfo = new ClientInfo { ClientId = "any-client" };

        // Act
        var result = GrayRuleEvaluator.Matches(rules, clientInfo);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void Matches_ShouldReturnFalse_WhenPercentageIs0()
    {
        // Arrange
        var rules = new List<GrayReleaseRule>
        {
            new() { RuleType = GrayRuleType.Percentage, MatchPattern = "0", Priority = 1 },
        };
        var clientInfo = new ClientInfo { ClientId = "any-client" };

        // Act
        var result = GrayRuleEvaluator.Matches(rules, clientInfo);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void Matches_ShouldBeConsistent_ForSameClientId()
    {
        // Arrange
        var rules = new List<GrayReleaseRule>
        {
            new() { RuleType = GrayRuleType.Percentage, MatchPattern = "50", Priority = 1 },
        };
        var clientInfo = new ClientInfo { ClientId = "consistent-client-id" };

        // Act - 多次调用
        var results = Enumerable.Range(0, 100)
            .Select(_ => GrayRuleEvaluator.Matches(rules, clientInfo))
            .ToList();

        // Assert - 结果应该一致
        results.All(r => r == results[0]).Should().BeTrue();
    }

    [Test]
    public void Matches_ShouldRespectPriority()
    {
        // Arrange - 第一个规则不匹配，第二个规则匹配
        var rules = new List<GrayReleaseRule>
        {
            new() { RuleType = GrayRuleType.IP, MatchPattern = "10.0.0.1", Priority = 1 },
            new() { RuleType = GrayRuleType.Tag, MatchPattern = "beta", Priority = 2 },
        };
        var clientInfo = new ClientInfo { ClientId = "client1", IpAddress = "192.168.1.1", Tags = ["beta"] };

        // Act
        var result = GrayRuleEvaluator.Matches(rules, clientInfo);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void Matches_ShouldReturnFalse_WhenNoRulesMatch()
    {
        // Arrange
        var rules = new List<GrayReleaseRule>
        {
            new() { RuleType = GrayRuleType.IP, MatchPattern = "10.0.0.1", Priority = 1 },
            new() { RuleType = GrayRuleType.Tag, MatchPattern = "beta", Priority = 2 },
        };
        var clientInfo = new ClientInfo { ClientId = "client1", IpAddress = "192.168.1.1", Tags = ["production"] };

        // Act
        var result = GrayRuleEvaluator.Matches(rules, clientInfo);

        // Assert
        result.Should().BeFalse();
    }

    [Test]
    public void Matches_ShouldReturnFalse_WhenRulesListIsEmpty()
    {
        // Arrange
        var rules = new List<GrayReleaseRule>();
        var clientInfo = new ClientInfo { ClientId = "client1" };

        // Act
        var result = GrayRuleEvaluator.Matches(rules, clientInfo);

        // Assert
        result.Should().BeFalse();
    }
}
