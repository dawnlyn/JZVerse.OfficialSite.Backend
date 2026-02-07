using FluentAssertions;
using JZVerse.MicroHuaxia.ServiceDiscovery.AppHost.Builder;
using JZVerse.MicroHuaxia.ServiceDiscovery.AppHost.Models;

namespace JZVerse.MicroHuaxia.ServiceDiscovery.Tests.Unit.AppHost;

/// <summary>
/// DistributedApplicationBuilder 单元测试
/// </summary>
[TestFixture]
public class DistributedApplicationBuilderTests
{
    [Test]
    public void AddProject_ShouldAddProjectResource()
    {
        // Arrange
        var builder = new DistributedApplicationBuilder();

        // Act
        builder.AddProject("api", "src/Api/Api.csproj");
        var app = builder.Build();

        // Assert
        app.Resources.Should().ContainSingle();
        app.Resources[0].Should().BeOfType<ProjectResource>();
        app.Resources[0].Name.Should().Be("api");
    }

    [Test]
    public void AddContainer_ShouldAddContainerResource()
    {
        // Arrange
        var builder = new DistributedApplicationBuilder();

        // Act
        builder.AddContainer("redis", "redis")
            .WithTag("7-alpine")
            .WithEndpoint(6379, 6379);
        
        var app = builder.Build();

        // Assert
        app.Resources.Should().ContainSingle();
        var container = app.Resources[0].Should().BeOfType<ContainerResource>().Subject;
        container.Name.Should().Be("redis");
        container.Image.Should().Be("redis");
        container.Tag.Should().Be("7-alpine");
    }

    [Test]
    public void AddExecutable_ShouldAddExecutableResource()
    {
        // Arrange
        var builder = new DistributedApplicationBuilder();

        // Act
        builder.AddExecutable("worker", "/usr/bin/worker")
            .WithArgs("--config", "config.yaml");
        
        var app = builder.Build();

        // Assert
        app.Resources.Should().ContainSingle();
        var executable = app.Resources[0].Should().BeOfType<ExecutableResource>().Subject;
        executable.Name.Should().Be("worker");
        executable.Args.Should().Contain("--config");
    }

    [Test]
    public void AddExternalService_ShouldAddExternalServiceResource()
    {
        // Arrange
        var builder = new DistributedApplicationBuilder();

        // Act
        builder.AddExternalService("external-api", "https://api.example.com");
        var app = builder.Build();

        // Assert
        app.Resources.Should().ContainSingle();
        var external = app.Resources[0].Should().BeOfType<ExternalServiceResource>().Subject;
        external.Url.Should().Be("https://api.example.com");
    }

    [Test]
    public void Build_DuplicateResourceNames_ShouldThrowException()
    {
        // Arrange
        var builder = new DistributedApplicationBuilder();
        builder.AddProject("api", "src/Api.csproj");
        builder.AddContainer("api", "nginx"); // 重复名称

        // Act & Assert
        var act = () => builder.Build();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Duplicate resource name*");
    }

    [Test]
    public void ProjectBuilder_WithEnvironment_ShouldSetEnvironmentVariables()
    {
        // Arrange
        var builder = new DistributedApplicationBuilder();

        // Act
        var projectBuilder = builder.AddProject("api", "src/Api.csproj")
            .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development")
            .WithEnvironment("LOG_LEVEL", "Debug");

        // Assert
        projectBuilder.Resource.Environment.Should().HaveCount(2);
        projectBuilder.Resource.Environment["ASPNETCORE_ENVIRONMENT"].Should().Be("Development");
    }

    [Test]
    public void ProjectBuilder_WithHttpEndpoint_ShouldAddEndpoint()
    {
        // Arrange
        var builder = new DistributedApplicationBuilder();

        // Act
        var projectBuilder = builder.AddProject("api", "src/Api.csproj")
            .WithHttpEndpoint(5000, "http")
            .WithHttpsEndpoint(5001, "https");

        // Assert
        projectBuilder.Resource.Endpoints.Should().HaveCount(2);
        projectBuilder.Resource.Endpoints[0].Port.Should().Be(5000);
        projectBuilder.Resource.Endpoints[1].Port.Should().Be(5001);
    }

    [Test]
    public void ProjectBuilder_WithReference_ShouldAddDependency()
    {
        // Arrange
        var builder = new DistributedApplicationBuilder();
        var redis = builder.AddContainer("redis", "redis")
            .WithEndpoint(6379, 6379, "tcp", "tcp");

        // Act
        builder.AddProject("api", "src/Api.csproj")
            .WithReference(redis);

        var app = builder.Build();

        // Assert
        var api = app.GetResource<ProjectResource>("api");
        api!.Dependencies.Should().ContainSingle();
        api.Dependencies[0].Name.Should().Be("redis");
    }

    [Test]
    public void ContainerBuilder_WithVolume_ShouldAddVolumeMount()
    {
        // Arrange
        var builder = new DistributedApplicationBuilder();

        // Act
        builder.AddContainer("postgres", "postgres")
            .WithVolume("pg-data", "/var/lib/postgresql/data")
            .WithBindMount("/local/config", "/etc/config", readOnly: true);

        var app = builder.Build();

        // Assert
        var postgres = app.GetResource<ContainerResource>("postgres");
        postgres!.Volumes.Should().HaveCount(2);
        postgres.Volumes[0].Type.Should().Be(VolumeMountType.Volume);
        postgres.Volumes[1].Type.Should().Be(VolumeMountType.Bind);
        postgres.Volumes[1].ReadOnly.Should().BeTrue();
    }

    [Test]
    public void GetResource_ExistingResource_ShouldReturnResource()
    {
        // Arrange
        var builder = new DistributedApplicationBuilder();
        builder.AddProject("api", "src/Api.csproj");
        builder.AddContainer("db", "postgres");
        var app = builder.Build();

        // Act
        var api = app.GetResource("api");
        var db = app.GetResource<ContainerResource>("db");

        // Assert
        api.Should().NotBeNull();
        db.Should().NotBeNull();
        db!.Image.Should().Be("postgres");
    }

    [Test]
    public void GetResource_NonExistingResource_ShouldReturnNull()
    {
        // Arrange
        var builder = new DistributedApplicationBuilder();
        var app = builder.Build();

        // Act
        var result = app.GetResource("non-existing");

        // Assert
        result.Should().BeNull();
    }

    [Test]
    public void GetResources_ByType_ShouldReturnMatchingResources()
    {
        // Arrange
        var builder = new DistributedApplicationBuilder();
        builder.AddProject("api-1", "src/Api1.csproj");
        builder.AddProject("api-2", "src/Api2.csproj");
        builder.AddContainer("redis", "redis");
        var app = builder.Build();

        // Act
        var projects = app.GetResources<ProjectResource>().ToList();
        var containers = app.GetResources<ContainerResource>().ToList();

        // Assert
        projects.Should().HaveCount(2);
        containers.Should().ContainSingle();
    }
}
