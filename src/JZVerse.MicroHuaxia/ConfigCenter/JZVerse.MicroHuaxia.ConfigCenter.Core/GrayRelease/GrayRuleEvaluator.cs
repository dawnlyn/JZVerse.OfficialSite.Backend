using System.Security.Cryptography;
using System.Text;
using JZVerse.MicroHuaxia.ConfigCenter.Abstractions.Models;

namespace JZVerse.MicroHuaxia.ConfigCenter.Core.GrayReleases;

/// <summary>
/// 灰度规则评估器
/// </summary>
public static class GrayRuleEvaluator
{
    /// <summary>
    /// 评估客户端是否匹配灰度规则
    /// </summary>
    public static bool Matches(IEnumerable<GrayReleaseRule> rules, ClientInfo clientInfo)
    {
        // 按优先级排序
        var sortedRules = rules.OrderBy(r => r.Priority);

        foreach (var rule in sortedRules)
        {
            if (MatchesRule(rule, clientInfo))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesRule(GrayReleaseRule rule, ClientInfo clientInfo)
    {
        return rule.RuleType switch
        {
            GrayRuleType.IP => MatchesIpRule(rule.MatchPattern, clientInfo.IpAddress),
            GrayRuleType.Tag => MatchesTagRule(rule.MatchPattern, clientInfo.Tags),
            GrayRuleType.ClientId => MatchesClientIdRule(rule.MatchPattern, clientInfo.ClientId),
            GrayRuleType.Percentage => MatchesPercentageRule(rule.MatchPattern, clientInfo.ClientId),
            _ => false,
        };
    }

    /// <summary>
    /// IP 规则匹配
    /// 支持格式：
    /// - 精确匹配：192.168.1.1
    /// - 通配符：192.168.1.*
    /// - 多个 IP：192.168.1.1,192.168.1.2
    /// </summary>
    private static bool MatchesIpRule(string pattern, string? ipAddress)
    {
        if (string.IsNullOrEmpty(ipAddress))
            return false;

        var patterns = pattern.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var p in patterns)
        {
            if (p.Contains('*'))
            {
                // 通配符匹配
                var prefix = p.Replace("*", "");
                if (ipAddress.StartsWith(prefix, StringComparison.Ordinal))
                    return true;
            }
            else
            {
                // 精确匹配
                if (ipAddress == p)
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 标签规则匹配
    /// 支持格式：tag1,tag2（OR 关系）
    /// </summary>
    private static bool MatchesTagRule(string pattern, IEnumerable<string> tags)
    {
        var requiredTags = pattern.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var tagSet = tags.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return requiredTags.Any(tag => tagSet.Contains(tag));
    }

    /// <summary>
    /// 客户端 ID 规则匹配
    /// 支持格式：clientId1,clientId2
    /// </summary>
    private static bool MatchesClientIdRule(string pattern, string clientId)
    {
        var allowedIds = pattern.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return allowedIds.Contains(clientId, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 百分比规则匹配
    /// 使用客户端 ID 哈希取模实现稳定的百分比分配
    /// </summary>
    private static bool MatchesPercentageRule(string pattern, string clientId)
    {
        if (!int.TryParse(pattern, out var percentage) || percentage is < 0 or > 100)
            return false;

        if (percentage == 100)
            return true;

        if (percentage == 0)
            return false;

        // 使用 MD5 哈希取模，确保同一客户端始终得到相同结果
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(clientId));
        var hashInt = BitConverter.ToUInt32(hash, 0);
        var modResult = hashInt % 100;

        return modResult < percentage;
    }
}
