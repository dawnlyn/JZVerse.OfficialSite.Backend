using JZVerse.Business.Abstractions.Models;
using JZVerse.Business.Project.Arguments;
using JZVerse.Business.Project.Database;
using JZVerse.Business.Project.Services;
using JZVerse.MicroHuaxia.DataAccess.Abstractions;

namespace JZVerse.Business.Tests.Unit.Project;

/// <summary>
/// 项目服务单元测试
/// </summary>
[TestFixture]
public class ProjectServiceTests
{
    private Mock<IDbExecutor> _dbExecutorMock = null!;
    private Mock<ISnowflakeIdGenerator> _idGeneratorMock = null!;
    private ProjectService _projectService = null!;

    [SetUp]
    public void SetUp()
    {
        _dbExecutorMock = new Mock<IDbExecutor>();
        _idGeneratorMock = new Mock<ISnowflakeIdGenerator>();
        _projectService = new ProjectService(_dbExecutorMock.Object, _idGeneratorMock.Object);
    }

    #region SaveAsync Tests

    [Test]
    public async Task SaveAsync_NewProject_ShouldCreateAndReturnNewId()
    {
        // Arrange
        var newId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        _idGeneratorMock.Setup(x => x.GenerateGuid()).Returns(newId);
        _dbExecutorMock.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>(), default))
            .ReturnsAsync(1);

        var arg = new ArgSaveProject
        {
            Name = "Test Project",
            Summary = "Test Summary",
            Description = "Test Description",
            CategoryId = categoryId,
            TechStack = "C#, .NET"
        };

        // Act
        var result = await _projectService.SaveAsync(arg, authorId, "TestAuthor");

        // Assert
        result.Should().Be(newId);
        _idGeneratorMock.Verify(x => x.GenerateGuid(), Times.Once);
        _dbExecutorMock.Verify(x => x.ExecuteAsync(ProjectSql.CreateProject, It.IsAny<object>(), default), Times.Once);
    }

    [Test]
    public async Task SaveAsync_ExistingProject_ShouldUpdateAndReturnExistingId()
    {
        // Arrange
        var existingId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        _dbExecutorMock.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>(), default))
            .ReturnsAsync(1);

        var arg = new ArgSaveProject
        {
            Id = existingId,
            Name = "Updated Project",
            Summary = "Updated Summary",
            Description = "Updated Description",
            CategoryId = categoryId
        };

        // Act
        var result = await _projectService.SaveAsync(arg, authorId, "TestAuthor");

        // Assert
        result.Should().Be(existingId);
        _idGeneratorMock.Verify(x => x.GenerateGuid(), Times.Never);
        _dbExecutorMock.Verify(x => x.ExecuteAsync(ProjectSql.UpdateProject, It.IsAny<object>(), default), Times.Once);
    }

    #endregion

    #region GetByIdAsync Tests

    [Test]
    public async Task GetByIdAsync_ExistingProject_ShouldReturnProjectDetail()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var project = new ProjectEntity
        {
            Id = projectId,
            Name = "Test Project",
            Summary = "Test Summary",
            Description = "Test Description",
            CategoryId = categoryId,
            Status = ProjectStatus.Published,
            ViewCount = 50,
            CreatedAt = DateTime.UtcNow
        };

        _dbExecutorMock.Setup(x => x.QueryFirstOrDefaultAsync<ProjectEntity>(ProjectSql.GetProjectById, It.IsAny<object>(), default))
            .ReturnsAsync(project);
        _dbExecutorMock.Setup(x => x.QueryAsync<ProjectCategoryEntity>(ProjectSql.GetAllCategories, It.IsAny<object>(), default))
            .ReturnsAsync([new ProjectCategoryEntity { Id = categoryId, Name = "Test Category" }]);

        // Act
        var result = await _projectService.GetByIdAsync(projectId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(projectId);
        result.Name.Should().Be("Test Project");
        result.CategoryName.Should().Be("Test Category");
    }

    [Test]
    public async Task GetByIdAsync_NonExistingProject_ShouldReturnNull()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        _dbExecutorMock.Setup(x => x.QueryFirstOrDefaultAsync<ProjectEntity>(ProjectSql.GetProjectById, It.IsAny<object>(), default))
            .ReturnsAsync((ProjectEntity?)null);

        // Act
        var result = await _projectService.GetByIdAsync(projectId);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region PublishAsync Tests

    [Test]
    public async Task PublishAsync_ExistingProject_ShouldPublishAndReturnTrue()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        _dbExecutorMock.Setup(x => x.ExecuteAsync(ProjectSql.PublishProject, It.IsAny<object>(), default))
            .ReturnsAsync(1);

        // Act
        var result = await _projectService.PublishAsync(projectId);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public async Task PublishAsync_NonExistingProject_ShouldReturnFalse()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        _dbExecutorMock.Setup(x => x.ExecuteAsync(ProjectSql.PublishProject, It.IsAny<object>(), default))
            .ReturnsAsync(0);

        // Act
        var result = await _projectService.PublishAsync(projectId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region DeleteAsync Tests

    [Test]
    public async Task DeleteAsync_ExistingProject_ShouldSoftDeleteAndReturnTrue()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        _dbExecutorMock.Setup(x => x.ExecuteAsync(ProjectSql.SoftDeleteProject, It.IsAny<object>(), default))
            .ReturnsAsync(1);

        // Act
        var result = await _projectService.DeleteAsync(projectId);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region GetListAdminAsync Tests

    [Test]
    public async Task GetListAdminAsync_ShouldReturnPagedResult()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var projects = new List<ProjectEntity>
        {
            new() { Id = Guid.NewGuid(), Name = "Project 1", CategoryId = categoryId, Status = ProjectStatus.Published },
            new() { Id = Guid.NewGuid(), Name = "Project 2", CategoryId = categoryId, Status = ProjectStatus.Draft }
        };

        _dbExecutorMock.Setup(x => x.QueryAsync<ProjectEntity>(ProjectSql.GetProjectListAdmin, It.IsAny<object>(), default))
            .ReturnsAsync(projects);
        _dbExecutorMock.Setup(x => x.ExecuteScalarAsync<int>(ProjectSql.GetProjectCount, It.IsAny<object>(), default))
            .ReturnsAsync(2);
        _dbExecutorMock.Setup(x => x.QueryAsync<ProjectCategoryEntity>(ProjectSql.GetAllCategories, It.IsAny<object>(), default))
            .ReturnsAsync([new ProjectCategoryEntity { Id = categoryId, Name = "Test Category" }]);

        var arg = new ArgQueryProjectsAdmin { PageIndex = 1, PageSize = 10 };

        // Act
        var result = await _projectService.GetListAdminAsync(arg);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
    }

    #endregion
}
