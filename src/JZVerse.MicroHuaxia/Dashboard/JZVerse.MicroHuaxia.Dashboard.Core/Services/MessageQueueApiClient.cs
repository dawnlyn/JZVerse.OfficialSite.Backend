using System.Net.Http.Json;
using JZVerse.MicroHuaxia.Dashboard.Abstractions.Configuration;
using JZVerse.MicroHuaxia.Dashboard.Abstractions.Models;
using JZVerse.MicroHuaxia.Dashboard.Abstractions.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JZVerse.MicroHuaxia.Dashboard.Core.Services;

/// <summary>
/// 消息队列 API 客户端实现
/// </summary>
public class MessageQueueApiClient : IMessageQueueApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MessageQueueApiClient> _logger;

    public MessageQueueApiClient(
        HttpClient httpClient,
        IOptions<DashboardOptions> options,
        ILogger<MessageQueueApiClient> logger)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(options.Value.ServiceEndpoints.MessageQueue);
        _logger = logger;
    }

    public async Task<MessageQueueStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var dto = await _httpClient.GetFromJsonAsync<StatsDto>("/api/v1/mq/stats", cancellationToken);
            return new MessageQueueStats
            {
                TotalTopics = dto?.TotalTopics ?? 0,
                TotalMessages = dto?.TotalMessages ?? 0,
                PendingMessages = dto?.PendingMessages ?? 0,
                MessagesPerSecond = dto?.MessagesPerSecond ?? 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取消息队列统计数据失败");
            return new MessageQueueStats();
        }
    }

    public async Task<List<MqTopicInfo>> GetTopicsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var dtos = await _httpClient.GetFromJsonAsync<List<TopicDto>>("/api/v1/mq/topics", cancellationToken);
            return dtos?.Select(d => new MqTopicInfo
            {
                Name = d.Name,
                PartitionCount = d.PartitionCount,
                TotalMessages = d.TotalMessages,
                SubscriberCount = d.SubscriberCount
            }).ToList() ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取 Topic 列表失败");
            return [];
        }
    }

    public async Task<MqTopicDetail?> GetTopicDetailAsync(string topicName, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/v1/mq/topics/{Uri.EscapeDataString(topicName)}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var dto = await response.Content.ReadFromJsonAsync<TopicDetailDto>(cancellationToken);
            if (dto is null) return null;

            return new MqTopicDetail
            {
                Name = dto.Name,
                Partitions = dto.Partitions?.Select(p => new MqPartitionInfo
                {
                    PartitionId = p.PartitionId,
                    LatestOffset = p.LatestOffset,
                    EarliestOffset = p.EarliestOffset,
                    MessageCount = p.MessageCount
                }).ToList() ?? [],
                Subscribers = dto.Subscribers ?? []
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取 Topic {TopicName} 详情失败", topicName);
            return null;
        }
    }

    public async Task<List<MqConsumerGroupInfo>> GetConsumerGroupsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var dtos = await _httpClient.GetFromJsonAsync<List<ConsumerGroupDto>>("/api/v1/mq/consumers", cancellationToken);
            return dtos?.Select(d => new MqConsumerGroupInfo
            {
                GroupName = d.GroupName,
                SubscribedTopics = d.SubscribedTopics ?? [],
                TotalLag = d.TotalLag
            }).ToList() ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取消费者组列表失败");
            return [];
        }
    }

    public async Task<List<MqClientConnectionInfo>> GetConnectionsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var dtos = await _httpClient.GetFromJsonAsync<List<ClientConnectionDto>>("/api/v1/mq/connections", cancellationToken);
            return dtos?.Select(d => new MqClientConnectionInfo
            {
                ClientId = d.ClientId,
                SessionId = d.SessionId,
                ConnectedAt = d.ConnectedAt.DateTime,
                Topics = d.Topics ?? []
            }).ToList() ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取客户端连接列表失败");
            return [];
        }
    }

    // 内部 DTO 类型，匹配 MQ 服务端 JSON 结构
    private class StatsDto
    {
        public int TotalTopics { get; set; }
        public long TotalMessages { get; set; }
        public int ConnectedClients { get; set; }
        public long PendingMessages { get; set; }
        public double MessagesPerSecond { get; set; }
    }

    private class TopicDto
    {
        public string Name { get; set; } = string.Empty;
        public int PartitionCount { get; set; }
        public long TotalMessages { get; set; }
        public int SubscriberCount { get; set; }
    }

    private class TopicDetailDto
    {
        public string Name { get; set; } = string.Empty;
        public List<PartitionDto>? Partitions { get; set; }
        public List<string>? Subscribers { get; set; }
    }

    private class PartitionDto
    {
        public int PartitionId { get; set; }
        public long LatestOffset { get; set; }
        public long EarliestOffset { get; set; }
        public long MessageCount { get; set; }
    }

    private class ConsumerGroupDto
    {
        public string GroupName { get; set; } = string.Empty;
        public List<string>? SubscribedTopics { get; set; }
        public long TotalLag { get; set; }
    }

    private class ClientConnectionDto
    {
        public string ClientId { get; set; } = string.Empty;
        public string SessionId { get; set; } = string.Empty;
        public DateTimeOffset ConnectedAt { get; set; }
        public List<string>? Topics { get; set; }
    }
}
