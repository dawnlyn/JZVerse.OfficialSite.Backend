using JZVerse.Business.Abstractions.Models;
using JZVerse.Business.Central.File.Arguments;
using JZVerse.Business.Central.File.Database;
using JZVerse.Business.Central.File.Results;
using JZVerse.Business.Central.File.Services;
using JZVerse.DataAccess.Abstractions;
using Microsoft.Extensions.Options;

namespace JZVerse.Business.Tests.Unit.Central;

/// <summary>
/// 文件服务单元测试
/// </summary>
[TestFixture]
public class FileServiceTests
{
    private Mock<IDbExecutor> _dbExecutorMock = null!;
    private Mock<ISnowflakeIdGenerator> _idGeneratorMock = null!;
    private Mock<IOptions<FileStorageOptions>> _optionsMock = null!;
    private FileService _fileService = null!;

    [SetUp]
    public void SetUp()
    {
        _dbExecutorMock = new Mock<IDbExecutor>();
        _idGeneratorMock = new Mock<ISnowflakeIdGenerator>();
        _optionsMock = new Mock<IOptions<FileStorageOptions>>();
        _optionsMock.Setup(x => x.Value).Returns(new FileStorageOptions
        {
            RootPath = "uploads",
            BaseUrl = "/files",
            TotalCapacity = 100L * 1024 * 1024 * 1024,
            RecycleBinRetentionDays = 7
        });
        _fileService = new FileService(_dbExecutorMock.Object, _idGeneratorMock.Object, _optionsMock.Object);
    }

    #region GetByIdAsync Tests

    [Test]
    public async Task GetByIdAsync_ExistingFile_ShouldReturnFileInfo()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        var file = new FileEntity
        {
            Id = fileId,
            FileName = "test_file.jpg",
            OriginalName = "test.jpg",
            FileType = "image",
            Module = "blog",
            FileSize = 1024,
            MimeType = "image/jpeg",
            StoragePath = "blog/2026/02/test_file.jpg",
            Status = FileStatus.Normal,
            CreatedAt = DateTime.UtcNow
        };

        _dbExecutorMock.Setup(x => x.QueryFirstOrDefaultAsync<FileEntity>(
            It.IsAny<string>(), It.IsAny<object>(), default))
            .ReturnsAsync(file);

        // Act
        var result = await _fileService.GetByIdAsync(fileId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(fileId);
        result.OriginalName.Should().Be("test.jpg");
    }

    [Test]
    public async Task GetByIdAsync_NonExistingFile_ShouldReturnNull()
    {
        // Arrange
        _dbExecutorMock.Setup(x => x.QueryFirstOrDefaultAsync<FileEntity>(
            It.IsAny<string>(), It.IsAny<object>(), default))
            .ReturnsAsync((FileEntity?)null);

        // Act
        var result = await _fileService.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region DeleteAsync Tests

    [Test]
    public async Task DeleteAsync_ExistingFile_ShouldSoftDeleteAndReturnTrue()
    {
        // Arrange
        var fileId = Guid.NewGuid();

        _dbExecutorMock.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>(), default))
            .ReturnsAsync(1);

        // Act
        var result = await _fileService.DeleteAsync(fileId);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public async Task DeleteAsync_NonExistingFile_ShouldReturnFalse()
    {
        // Arrange
        _dbExecutorMock.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>(), default))
            .ReturnsAsync(0);

        // Act
        var result = await _fileService.DeleteAsync(Guid.NewGuid());

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region GetListAsync Tests

    [Test]
    public async Task GetListAsync_ShouldReturnPagedResult()
    {
        // Arrange
        var files = new List<FileEntity>
        {
            new() { Id = Guid.NewGuid(), FileName = "file1.jpg", OriginalName = "file1.jpg", Module = "blog", StoragePath = "blog/file1.jpg" },
            new() { Id = Guid.NewGuid(), FileName = "file2.png", OriginalName = "file2.png", Module = "blog", StoragePath = "blog/file2.png" }
        };

        _dbExecutorMock.Setup(x => x.QueryAsync<FileEntity>(It.IsAny<string>(), It.IsAny<object>(), default))
            .ReturnsAsync(files);
        _dbExecutorMock.Setup(x => x.ExecuteScalarAsync<int>(It.IsAny<string>(), It.IsAny<object>(), default))
            .ReturnsAsync(2);

        var arg = new ArgQueryFiles { PageIndex = 1, PageSize = 10 };

        // Act
        var result = await _fileService.GetListAsync(arg);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
    }

    #endregion

    #region DisableAsync Tests

    [Test]
    public async Task DisableAsync_ExistingFile_ShouldReturnTrue()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        _dbExecutorMock.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>(), default))
            .ReturnsAsync(1);

        // Act
        var result = await _fileService.DisableAsync(fileId);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region EnableAsync Tests

    [Test]
    public async Task EnableAsync_ExistingFile_ShouldReturnTrue()
    {
        // Arrange
        var fileId = Guid.NewGuid();
        _dbExecutorMock.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>(), default))
            .ReturnsAsync(1);

        // Act
        var result = await _fileService.EnableAsync(fileId);

        // Assert
        result.Should().BeTrue();
    }

    #endregion
}
