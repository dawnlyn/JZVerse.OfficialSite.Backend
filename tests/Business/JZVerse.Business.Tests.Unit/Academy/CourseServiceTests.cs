using JZVerse.Business.Abstractions.Models;
using JZVerse.Business.Academy.Arguments;
using JZVerse.Business.Academy.Database;
using JZVerse.Business.Academy.Services;
using JZVerse.DataAccess.Abstractions;

namespace JZVerse.Business.Tests.Unit.Academy;

/// <summary>
/// 课程服务单元测试
/// </summary>
[TestFixture]
public class CourseServiceTests
{
    private Mock<IDbExecutor> _dbExecutorMock = null!;
    private Mock<ISnowflakeIdGenerator> _idGeneratorMock = null!;
    private CourseService _courseService = null!;

    [SetUp]
    public void SetUp()
    {
        _dbExecutorMock = new Mock<IDbExecutor>();
        _idGeneratorMock = new Mock<ISnowflakeIdGenerator>();
        _courseService = new CourseService(_dbExecutorMock.Object, _idGeneratorMock.Object);
    }

    #region SaveAsync Tests

    [Test]
    public async Task SaveAsync_NewCourse_ShouldCreateAndReturnNewId()
    {
        // Arrange
        var newId = Guid.NewGuid();
        var instructorId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        _idGeneratorMock.Setup(x => x.GenerateGuid()).Returns(newId);
        _dbExecutorMock.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>(), default))
            .ReturnsAsync(1);

        var arg = new ArgSaveCourse
        {
            Title = "Test Course",
            Summary = "Test Summary",
            Description = "Test Description",
            CategoryId = categoryId,
            Difficulty = (int)CourseDifficulty.Beginner
        };

        // Act
        var result = await _courseService.SaveAsync(arg, instructorId, "TestInstructor");

        // Assert
        result.Should().Be(newId);
        _idGeneratorMock.Verify(x => x.GenerateGuid(), Times.Once);
    }

    [Test]
    public async Task SaveAsync_ExistingCourse_ShouldUpdateAndReturnExistingId()
    {
        // Arrange
        var existingId = Guid.NewGuid();
        var instructorId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        _dbExecutorMock.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>(), default))
            .ReturnsAsync(1);

        var arg = new ArgSaveCourse
        {
            Id = existingId,
            Title = "Updated Course",
            Summary = "Updated Summary",
            Description = "Updated Description",
            CategoryId = categoryId,
            Difficulty = (int)CourseDifficulty.Intermediate
        };

        // Act
        var result = await _courseService.SaveAsync(arg, instructorId, "TestInstructor");

        // Assert
        result.Should().Be(existingId);
        _idGeneratorMock.Verify(x => x.GenerateGuid(), Times.Never);
    }

    #endregion

    #region GetByIdAsync Tests

    [Test]
    public async Task GetByIdAsync_ExistingCourse_ShouldReturnCourseDetail()
    {
        // Arrange
        var courseId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var course = new CourseEntity
        {
            Id = courseId,
            Title = "Test Course",
            Summary = "Test Summary",
            Description = "Test Description",
            CategoryId = categoryId,
            Status = CourseStatus.Published,
            PlayCount = 100,
            Difficulty = CourseDifficulty.Beginner,
            CreatedAt = DateTime.UtcNow
        };

        _dbExecutorMock.Setup(x => x.QueryFirstOrDefaultAsync<CourseEntity>(It.IsAny<string>(), It.IsAny<object>(), default))
            .ReturnsAsync(course);
        _dbExecutorMock.Setup(x => x.QueryAsync<ChapterEntity>(It.IsAny<string>(), It.IsAny<object>(), default))
            .ReturnsAsync([]);
        _dbExecutorMock.Setup(x => x.QueryAsync<LessonEntity>(It.IsAny<string>(), It.IsAny<object>(), default))
            .ReturnsAsync([]);
        _dbExecutorMock.Setup(x => x.QueryAsync<CourseCategoryEntity>(It.IsAny<string>(), It.IsAny<object>(), default))
            .ReturnsAsync([new CourseCategoryEntity { Id = categoryId, Name = "Test Category" }]);

        // Act
        var result = await _courseService.GetByIdAsync(courseId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(courseId);
        result.Title.Should().Be("Test Course");
        result.CategoryName.Should().Be("Test Category");
    }

    [Test]
    public async Task GetByIdAsync_NonExistingCourse_ShouldReturnNull()
    {
        // Arrange
        var courseId = Guid.NewGuid();
        _dbExecutorMock.Setup(x => x.QueryFirstOrDefaultAsync<CourseEntity>(It.IsAny<string>(), It.IsAny<object>(), default))
            .ReturnsAsync((CourseEntity?)null);

        // Act
        var result = await _courseService.GetByIdAsync(courseId);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region PublishAsync Tests

    [Test]
    public async Task PublishAsync_ExistingCourse_ShouldPublishAndReturnTrue()
    {
        // Arrange
        var courseId = Guid.NewGuid();
        _dbExecutorMock.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>(), default))
            .ReturnsAsync(1);

        // Act
        var result = await _courseService.PublishAsync(courseId);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public async Task PublishAsync_NonExistingCourse_ShouldReturnFalse()
    {
        // Arrange
        var courseId = Guid.NewGuid();
        _dbExecutorMock.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<object>(), default))
            .ReturnsAsync(0);

        // Act
        var result = await _courseService.PublishAsync(courseId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region GetListAdminAsync Tests

    [Test]
    public async Task GetListAdminAsync_ShouldReturnPagedResult()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var courses = new List<CourseEntity>
        {
            new() { Id = Guid.NewGuid(), Title = "Course 1", CategoryId = categoryId, Status = CourseStatus.Published },
            new() { Id = Guid.NewGuid(), Title = "Course 2", CategoryId = categoryId, Status = CourseStatus.Draft }
        };

        _dbExecutorMock.Setup(x => x.QueryAsync<CourseEntity>(It.IsAny<string>(), It.IsAny<object>(), default))
            .ReturnsAsync(courses);
        _dbExecutorMock.Setup(x => x.ExecuteScalarAsync<int>(It.IsAny<string>(), It.IsAny<object>(), default))
            .ReturnsAsync(2);
        _dbExecutorMock.Setup(x => x.QueryAsync<CourseCategoryEntity>(It.IsAny<string>(), It.IsAny<object>(), default))
            .ReturnsAsync([new CourseCategoryEntity { Id = categoryId, Name = "Test Category" }]);

        var arg = new ArgQueryCoursesAdmin { PageIndex = 1, PageSize = 10 };

        // Act
        var result = await _courseService.GetListAdminAsync(arg);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
    }

    #endregion
}
