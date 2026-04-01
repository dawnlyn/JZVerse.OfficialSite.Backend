using JZVerse.Business.Abstractions.Models;
using JZVerse.Business.Dashboard.Arguments;
using JZVerse.Business.Dashboard.Database;
using JZVerse.Business.Dashboard.Services;
using JZVerse.MicroHuaxia.DataAccess.Abstractions;

namespace JZVerse.Business.Tests.Unit.Dashboard;

/// <summary>
/// 日志服务单元测试
/// </summary>
[TestFixture]
public class LogServiceTests
{
    private Mock<IDbExecutor> _dbExecutorMock = null!;
    private Mock<ISnowflakeIdGenerator> _idGeneratorMock = null!;
    private LogService _logService = null!;

    [SetUp]
    public void SetUp()
    {
        _dbExecutorMock = new Mock<IDbExecutor>();
        _idGeneratorMock = new Mock<ISnowflakeIdGenerator>();
        _logService = new LogService(_dbExecutorMock.Object, _idGeneratorMock.Object);
    }

    #region LogOperationAsync Tests

    [Test]
    public async Task LogOperationAsync_ShouldCreateLogAndReturnId()
    {
        // Arrange
        var newId = Guid.NewGuid();
        var operatorId = Guid.NewGuid();

        _idGeneratorMock.Setup(x => x.GenerateGuid()).Returns(newId);
        _dbExecutorMock.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>(), default))
            .ReturnsAsync(1);

        // Act
        var result = await _logService.LogOperationAsync(
            operatorId, "Admin", "Blog", "Create", "Created article",
            "POST", "/api/articles", "{}", "{}",
            "127.0.0.1", "Mozilla/5.0", 200, 100);

        // Assert
        result.Should().Be(newId);
    }

    #endregion

    #region GetOperationLogsAsync Tests

    [Test]
    public async Task GetOperationLogsAsync_ShouldReturnPagedResult()
    {
        // Arrange
        var logs = new List<OperationLogEntity>
        {
            new() { Id = Guid.NewGuid(), Module = "Blog", Action = "Create", CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Module = "Blog", Action = "Update", CreatedAt = DateTime.UtcNow }
        };

        _dbExecutorMock.Setup(x => x.QueryAsync<OperationLogEntity>(It.IsAny<string>(), It.IsAny<object>(), default))
            .ReturnsAsync(logs);
        _dbExecutorMock.Setup(x => x.ExecuteScalarAsync<int>(It.IsAny<string>(), It.IsAny<object>(), default))
            .ReturnsAsync(2);

        var arg = new ArgQueryOperationLogs { PageIndex = 1, PageSize = 10 };

        // Act
        var result = await _logService.GetOperationLogsAsync(arg);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
    }

    #endregion

    #region LogExceptionAsync Tests

    [Test]
    public async Task LogExceptionAsync_ShouldCreateLogAndReturnId()
    {
        // Arrange
        var newId = Guid.NewGuid();

        _idGeneratorMock.Setup(x => x.GenerateGuid()).Returns(newId);
        _dbExecutorMock.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>(), default))
            .ReturnsAsync(1);

        // Act
        var result = await _logService.LogExceptionAsync(
            "Blog", "NullReferenceException", "Object reference not set",
            "at BlogService.SaveAsync()", "/api/articles", "{}", "127.0.0.1");

        // Assert
        result.Should().Be(newId);
    }

    #endregion

    #region HandleExceptionLogAsync Tests

    [Test]
    public async Task HandleExceptionLogAsync_ShouldUpdateStatus()
    {
        // Arrange
        var logId = Guid.NewGuid();
        var handlerId = Guid.NewGuid();

        _dbExecutorMock.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>(), default))
            .ReturnsAsync(1);

        var arg = new ArgHandleExceptionLog
        {
            Id = logId,
            Status = ExceptionLogStatus.Resolved,
            Solution = "Fixed the bug"
        };

        // Act
        var result = await _logService.HandleExceptionLogAsync(arg, handlerId);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region LogLoginAsync Tests

    [Test]
    public async Task LogLoginAsync_SuccessfulLogin_ShouldCreateLog()
    {
        // Arrange
        var newId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        _idGeneratorMock.Setup(x => x.GenerateGuid()).Returns(newId);
        _dbExecutorMock.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>(), default))
            .ReturnsAsync(1);

        // Act
        var result = await _logService.LogLoginAsync(
            userId, "admin", LoginLogType.Admin,
            "127.0.0.1", "Beijing", "Mozilla/5.0",
            "Desktop", "Chrome", "Windows",
            isSuccess: true, failReason: null);

        // Assert
        result.Should().Be(newId);
    }

    [Test]
    public async Task LogLoginAsync_FailedLogin_ShouldCreateLogWithFailReason()
    {
        // Arrange
        var newId = Guid.NewGuid();

        _idGeneratorMock.Setup(x => x.GenerateGuid()).Returns(newId);
        _dbExecutorMock.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>(), default))
            .ReturnsAsync(1);

        // Act
        var result = await _logService.LogLoginAsync(
            null, "unknown", LoginLogType.Admin,
            "192.168.1.1", "Unknown", "curl",
            "CLI", "curl", "Linux",
            isSuccess: false, failReason: "Invalid credentials");

        // Assert
        result.Should().Be(newId);
    }

    #endregion

    #region GetLoginLogsAsync Tests

    [Test]
    public async Task GetLoginLogsAsync_ShouldReturnPagedResult()
    {
        // Arrange
        var logs = new List<LoginLogEntity>
        {
            new() { Id = Guid.NewGuid(), Username = "admin", IsSuccess = true, CreatedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Username = "user", IsSuccess = false, CreatedAt = DateTime.UtcNow }
        };

        _dbExecutorMock.Setup(x => x.QueryAsync<LoginLogEntity>(It.IsAny<string>(), It.IsAny<object>(), default))
            .ReturnsAsync(logs);
        _dbExecutorMock.Setup(x => x.ExecuteScalarAsync<int>(It.IsAny<string>(), It.IsAny<object>(), default))
            .ReturnsAsync(2);

        var arg = new ArgQueryLoginLogs { PageIndex = 1, PageSize = 10 };

        // Act
        var result = await _logService.GetLoginLogsAsync(arg);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
    }

    #endregion

    #region CleanupOperationLogsAsync Tests

    [Test]
    public async Task CleanupOperationLogsAsync_ShouldDeleteOldLogs()
    {
        // Arrange
        _dbExecutorMock.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>(), default))
            .ReturnsAsync(100);

        // Act
        var result = await _logService.CleanupOperationLogsAsync(90);

        // Assert
        result.Should().Be(100);
    }

    #endregion
}
