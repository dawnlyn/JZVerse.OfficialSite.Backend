using JZVerse.Business.Abstractions.Authentication;
using JZVerse.Business.Abstractions.Models;
using JZVerse.Business.Central.User.Arguments;
using JZVerse.Business.Central.User.Database;
using JZVerse.Business.Central.User.Services;
using JZVerse.DataAccess.Abstractions;

namespace JZVerse.Business.Tests.Unit.Central;

/// <summary>
/// 管理员服务单元测试
/// </summary>
[TestFixture]
public class AdminServiceTests
{
    private Mock<IDbExecutor> _dbExecutorMock = null!;
    private Mock<ISnowflakeIdGenerator> _idGeneratorMock = null!;
    private Mock<IJwtTokenService> _jwtTokenServiceMock = null!;
    private AdminService _adminService = null!;

    [SetUp]
    public void SetUp()
    {
        _dbExecutorMock = new Mock<IDbExecutor>();
        _idGeneratorMock = new Mock<ISnowflakeIdGenerator>();
        _jwtTokenServiceMock = new Mock<IJwtTokenService>();
        _adminService = new AdminService(
            _dbExecutorMock.Object,
            _idGeneratorMock.Object,
            _jwtTokenServiceMock.Object);
    }

    #region LoginAsync Tests

    [Test]
    public async Task LoginAsync_ValidCredentials_ShouldReturnToken()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("password123", workFactor: 12);
        var admin = new AdminEntity
        {
            Id = adminId,
            Username = "admin",
            PasswordHash = passwordHash,
            Status = AdminStatus.Active,
            Type = AdminType.SuperAdmin
        };

        _dbExecutorMock.Setup(x => x.QueryFirstOrDefaultAsync<AdminEntity>(
            UserSql.GetAdminByUsername, It.IsAny<object>(), default))
            .ReturnsAsync(admin);

        _dbExecutorMock.Setup(x => x.ExecuteAsync(
            UserSql.UpdateAdminLoginInfo, It.IsAny<object>(), default))
            .ReturnsAsync(1);

        _jwtTokenServiceMock.Setup(x => x.GenerateAccessTokenAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<IDictionary<string, string>>()))
            .ReturnsAsync("access_token");

        _jwtTokenServiceMock.Setup(x => x.GenerateRefreshTokenAsync(It.IsAny<Guid>()))
            .ReturnsAsync("refresh_token");

        var arg = new ArgAdminLogin { Username = "admin", Password = "password123" };

        // Act
        var result = await _adminService.LoginAsync(arg, "127.0.0.1", "Chrome");

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().Be("access_token");
        result.RefreshToken.Should().Be("refresh_token");
        result.Admin.Username.Should().Be("admin");
    }

    [Test]
    public void LoginAsync_InvalidPassword_ShouldThrowAuthenticationException()
    {
        // Arrange
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("password123", workFactor: 12);
        var admin = new AdminEntity
        {
            Id = Guid.NewGuid(),
            Username = "admin",
            PasswordHash = passwordHash,
            Status = AdminStatus.Active
        };

        _dbExecutorMock.Setup(x => x.QueryFirstOrDefaultAsync<AdminEntity>(
            UserSql.GetAdminByUsername, It.IsAny<object>(), default))
            .ReturnsAsync(admin);

        var arg = new ArgAdminLogin { Username = "admin", Password = "wrongpassword" };

        // Act & Assert
        var act = () => _adminService.LoginAsync(arg, null, null);
        act.Should().ThrowAsync<AuthenticationException>()
            .WithMessage("用户名或密码错误");
    }

    [Test]
    public void LoginAsync_FrozenAccount_ShouldThrowAuthenticationException()
    {
        // Arrange
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("password123", workFactor: 12);
        var admin = new AdminEntity
        {
            Id = Guid.NewGuid(),
            Username = "admin",
            PasswordHash = passwordHash,
            Status = AdminStatus.Frozen
        };

        _dbExecutorMock.Setup(x => x.QueryFirstOrDefaultAsync<AdminEntity>(
            UserSql.GetAdminByUsername, It.IsAny<object>(), default))
            .ReturnsAsync(admin);

        var arg = new ArgAdminLogin { Username = "admin", Password = "password123" };

        // Act & Assert
        var act = () => _adminService.LoginAsync(arg, null, null);
        act.Should().ThrowAsync<AuthenticationException>()
            .WithMessage("账号已被冻结");
    }

    [Test]
    public void LoginAsync_NonExistingUser_ShouldThrowAuthenticationException()
    {
        // Arrange
        _dbExecutorMock.Setup(x => x.QueryFirstOrDefaultAsync<AdminEntity>(
            UserSql.GetAdminByUsername, It.IsAny<object>(), default))
            .ReturnsAsync((AdminEntity?)null);

        var arg = new ArgAdminLogin { Username = "nonexistent", Password = "password123" };

        // Act & Assert
        var act = () => _adminService.LoginAsync(arg, null, null);
        act.Should().ThrowAsync<AuthenticationException>()
            .WithMessage("用户名或密码错误");
    }

    #endregion

    #region CreateAsync Tests

    [Test]
    public async Task CreateAsync_NewAdmin_ShouldCreateAndReturnInfo()
    {
        // Arrange
        var newId = Guid.NewGuid();

        _dbExecutorMock.Setup(x => x.ExecuteScalarAsync<bool>(
            UserSql.CheckAdminUsernameExists, It.IsAny<object>(), default))
            .ReturnsAsync(false);

        _idGeneratorMock.Setup(x => x.GenerateGuid()).Returns(newId);

        _dbExecutorMock.Setup(x => x.ExecuteAsync(UserSql.CreateAdmin, It.IsAny<object>(), default))
            .ReturnsAsync(1);

        var arg = new ArgCreateAdmin
        {
            Username = "newadmin",
            Password = "password123",
            Email = "admin@example.com",
            Phone = "13800138000"
        };

        // Act
        var result = await _adminService.CreateAsync(arg);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(newId);
        result.Username.Should().Be("newadmin");
    }

    [Test]
    public void CreateAsync_DuplicateUsername_ShouldThrowValidationException()
    {
        // Arrange
        _dbExecutorMock.Setup(x => x.ExecuteScalarAsync<bool>(
            UserSql.CheckAdminUsernameExists, It.IsAny<object>(), default))
            .ReturnsAsync(true);

        var arg = new ArgCreateAdmin
        {
            Username = "existingadmin",
            Password = "password123"
        };

        // Act & Assert
        var act = () => _adminService.CreateAsync(arg);
        act.Should().ThrowAsync<ValidationException>()
            .WithMessage("用户名已存在");
    }

    #endregion

    #region UpdatePasswordAsync Tests

    [Test]
    public async Task UpdatePasswordAsync_ValidOldPassword_ShouldUpdateAndReturnTrue()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var oldPasswordHash = BCrypt.Net.BCrypt.HashPassword("oldpassword", workFactor: 12);
        var admin = new AdminEntity
        {
            Id = adminId,
            PasswordHash = oldPasswordHash
        };

        _dbExecutorMock.Setup(x => x.QueryFirstOrDefaultAsync<AdminEntity>(
            UserSql.GetAdminById, It.IsAny<object>(), default))
            .ReturnsAsync(admin);

        _dbExecutorMock.Setup(x => x.ExecuteAsync(UserSql.UpdateAdminPassword, It.IsAny<object>(), default))
            .ReturnsAsync(1);

        var arg = new ArgUpdateAdminPassword
        {
            OldPassword = "oldpassword",
            NewPassword = "newpassword"
        };

        // Act
        var result = await _adminService.UpdatePasswordAsync(adminId, arg);

        // Assert
        result.Should().BeTrue();
    }

    [Test]
    public void UpdatePasswordAsync_InvalidOldPassword_ShouldThrowValidationException()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var oldPasswordHash = BCrypt.Net.BCrypt.HashPassword("oldpassword", workFactor: 12);
        var admin = new AdminEntity
        {
            Id = adminId,
            PasswordHash = oldPasswordHash
        };

        _dbExecutorMock.Setup(x => x.QueryFirstOrDefaultAsync<AdminEntity>(
            UserSql.GetAdminById, It.IsAny<object>(), default))
            .ReturnsAsync(admin);

        var arg = new ArgUpdateAdminPassword
        {
            OldPassword = "wrongpassword",
            NewPassword = "newpassword"
        };

        // Act & Assert
        var act = () => _adminService.UpdatePasswordAsync(adminId, arg);
        act.Should().ThrowAsync<ValidationException>()
            .WithMessage("原密码错误");
    }

    #endregion

    #region GetByIdAsync Tests

    [Test]
    public async Task GetByIdAsync_ExistingAdmin_ShouldReturnAdminInfo()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var admin = new AdminEntity
        {
            Id = adminId,
            Username = "admin",
            Email = "admin@example.com",
            Status = AdminStatus.Active,
            Type = AdminType.SuperAdmin
        };

        _dbExecutorMock.Setup(x => x.QueryFirstOrDefaultAsync<AdminEntity>(
            UserSql.GetAdminById, It.IsAny<object>(), default))
            .ReturnsAsync(admin);

        // Act
        var result = await _adminService.GetByIdAsync(adminId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(adminId);
        result.Username.Should().Be("admin");
    }

    [Test]
    public async Task GetByIdAsync_NonExistingAdmin_ShouldReturnNull()
    {
        // Arrange
        _dbExecutorMock.Setup(x => x.QueryFirstOrDefaultAsync<AdminEntity>(
            UserSql.GetAdminById, It.IsAny<object>(), default))
            .ReturnsAsync((AdminEntity?)null);

        // Act
        var result = await _adminService.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region UpdateStatusAsync Tests

    [Test]
    public async Task UpdateStatusAsync_ShouldUpdateAndReturnTrue()
    {
        // Arrange
        var adminId = Guid.NewGuid();

        _dbExecutorMock.Setup(x => x.ExecuteAsync(UserSql.UpdateAdminStatus, It.IsAny<object>(), default))
            .ReturnsAsync(1);

        // Act
        var result = await _adminService.UpdateStatusAsync(adminId, AdminStatus.Frozen);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region GetListAsync Tests

    [Test]
    public async Task GetListAsync_ShouldReturnPagedResult()
    {
        // Arrange
        var admins = new List<AdminEntity>
        {
            new() { Id = Guid.NewGuid(), Username = "admin1", Status = AdminStatus.Active },
            new() { Id = Guid.NewGuid(), Username = "admin2", Status = AdminStatus.Frozen }
        };

        _dbExecutorMock.Setup(x => x.QueryAsync<AdminEntity>(UserSql.GetAdminList, It.IsAny<object>(), default))
            .ReturnsAsync(admins);
        _dbExecutorMock.Setup(x => x.ExecuteScalarAsync<int>(UserSql.GetAdminCount, It.IsAny<object>(), default))
            .ReturnsAsync(2);

        // Act
        var result = await _adminService.GetListAsync(1, 10);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
    }

    #endregion
}
