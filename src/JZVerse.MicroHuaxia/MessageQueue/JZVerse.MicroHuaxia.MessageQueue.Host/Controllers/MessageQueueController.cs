using JZVerse.MicroHuaxia.MessageQueue.Server;
using JZVerse.MicroHuaxia.MessageQueue.Server.Management;
using Microsoft.AspNetCore.Mvc;

namespace JZVerse.MicroHuaxia.MessageQueue.Host.Controllers;

/// <summary>
/// 消息队列管理控制器
/// </summary>
[ApiController]
[Route("api/v1/mq")]
public class MessageQueueController : ControllerBase
{
    private readonly IBrokerManagement _management;
    private readonly BrokerOptions _options;
    private readonly ILogger<MessageQueueController> _logger;

    public MessageQueueController(
        IBrokerManagement management,
        BrokerOptions options,
        ILogger<MessageQueueController> logger)
    {
        _management = management;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// 获取 Broker 统计信息
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(BrokerStats), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
    {
        var stats = await _management.GetStatsAsync(cancellationToken);
        return Ok(stats);
    }

    /// <summary>
    /// 获取所有 Topic 列表
    /// </summary>
    [HttpGet("topics")]
    [ProducesResponseType(typeof(IReadOnlyList<TopicInfo>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTopics(CancellationToken cancellationToken)
    {
        var topics = await _management.GetTopicsAsync(cancellationToken);
        return Ok(topics);
    }

    /// <summary>
    /// 获取 Topic 详情
    /// </summary>
    [HttpGet("topics/{name}")]
    [ProducesResponseType(typeof(TopicDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTopicDetail(string name, CancellationToken cancellationToken)
    {
        var detail = await _management.GetTopicDetailAsync(name, cancellationToken);
        if (detail is null)
        {
            return NotFound(new { error = $"Topic '{name}' 不存在" });
        }
        return Ok(detail);
    }

    /// <summary>
    /// 获取消费者组列表
    /// </summary>
    [HttpGet("consumers")]
    [ProducesResponseType(typeof(IReadOnlyList<ConsumerGroupInfo>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetConsumerGroups(CancellationToken cancellationToken)
    {
        var groups = await _management.GetConsumerGroupsAsync(cancellationToken);
        return Ok(groups);
    }

    /// <summary>
    /// 获取客户端连接列表
    /// </summary>
    [HttpGet("connections")]
    [ProducesResponseType(typeof(IReadOnlyList<ClientConnectionInfo>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetConnections(CancellationToken cancellationToken)
    {
        var connections = await _management.GetConnectionsAsync(cancellationToken);
        return Ok(connections);
    }

    /// <summary>
    /// 获取 Broker 配置
    /// </summary>
    [HttpGet("config")]
    [ProducesResponseType(typeof(BrokerOptions), StatusCodes.Status200OK)]
    public IActionResult GetConfig()
    {
        return Ok(_options);
    }
}
