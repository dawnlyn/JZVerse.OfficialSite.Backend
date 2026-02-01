var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddGarnet("cache").WithDataVolume("garnet-data").WithPersistence(TimeSpan.FromMinutes(5));
var postgres = builder.AddPostgres("postgres").WithDataVolume("postgre-data").WithPgAdmin().WithLifetime(ContainerLifetime.Persistent);
var elasticsearch = builder.AddElasticsearch("elasticsearch").WithDataVolume("elasticsearch-data");
var rabbitmq = builder.AddRabbitMQ("messaging").WithDataVolume("rabbitmq-data");

var secrityCentralDb = postgres.AddDatabase("secrity-central-db");
var userCentralDb = postgres.AddDatabase("user-central-db");
var fileCentralDb = postgres.AddDatabase("file-central-db");
var blogBusinessDb = postgres.AddDatabase("blog-business-db");
var projectBusinessDb = postgres.AddDatabase("project-business-db");
var administratorBusinessDb = postgres.AddDatabase("administrator-business-db");
var academyBusinessDb = postgres.AddDatabase("academy-business-db");

var securityCentralService = builder.AddProject<Projects.JZVerse_OfficialSite_Backend_Central_Security>("security-central-service").WithReference(cache).WithReference(secrityCentralDb).WaitFor(secrityCentralDb).WaitFor(cache);
var fileCentralService = builder.AddProject<Projects.JZVerse_OfficialSite_Backend_Central_File>("file-central-service").WithReference(cache).WithReference(fileCentralDb).WaitFor(fileCentralDb).WaitFor(cache);
var userCentralService = builder
    .AddProject<Projects.JZVerse_OfficialSite_Backend_Central_User>("user-central-service")
    .WithReference(fileCentralService)
    .WithReference(securityCentralService)
    .WithReference(cache)
    .WithReference(userCentralDb)
    .WaitFor(userCentralDb)
    .WaitFor(fileCentralService)
    .WaitFor(securityCentralService)
    .WaitFor(cache);

var administratorBusinessService = builder
    .AddProject<Projects.JZVerse_OfficialSite_Backend_Business_Administrator>("administrator-business-service")
    .WithReference(fileCentralService)
    .WithReference(securityCentralService)
    .WithReference(userCentralService)
    .WithReference(cache)
    .WithReference(administratorBusinessDb)
    .WaitFor(administratorBusinessDb)
    .WaitFor(fileCentralService)
    .WaitFor(securityCentralService)
    .WaitFor(userCentralService)
    .WaitFor(cache);

var blogBusinessService = builder
    .AddProject<Projects.JZVerse_OfficialSite_Backend_Business_Blog>("blog-business-service")
    .WithReference(fileCentralService)
    .WithReference(securityCentralService)
    .WithReference(userCentralService)
    .WithReference(cache)
    .WithReference(blogBusinessDb)
    .WaitFor(blogBusinessDb)
    .WaitFor(fileCentralService)
    .WaitFor(securityCentralService)
    .WaitFor(userCentralService)
    .WaitFor(cache);

var projectBusinessService = builder
    .AddProject<Projects.JZVerse_OfficialSite_Backend_Business_Project>("project-business-service")
    .WithReference(fileCentralService)
    .WithReference(securityCentralService)
    .WithReference(userCentralService)
    .WithReference(cache)
    .WithReference(projectBusinessDb)
    .WaitFor(projectBusinessDb)
    .WaitFor(fileCentralService)
    .WaitFor(securityCentralService)
    .WaitFor(userCentralService)
    .WaitFor(cache);

var academyBusinessService = builder
    .AddProject<Projects.JZVerse_OfficialSite_Backend_Business_Academy>("academy-business-service")
    .WithReference(fileCentralService)
    .WithReference(securityCentralService)
    .WithReference(userCentralService)
    .WithReference(cache)
    .WithReference(academyBusinessDb)
    .WaitFor(academyBusinessDb)
    .WaitFor(fileCentralService)
    .WaitFor(securityCentralService)
    .WaitFor(userCentralService)
    .WaitFor(cache);

builder
    .AddJavaScriptApp("frontend", "../wwwroot")
    .WithPnpm()
    .WithHttpEndpoint(env: "PORT", port: 3000, targetPort: 3000)
    .WaitFor(fileCentralService)
    .WaitFor(securityCentralService)
    .WaitFor(userCentralService)
    .WaitFor(administratorBusinessService)
    .WaitFor(blogBusinessService)
    .WaitFor(projectBusinessService)
    .WaitFor(academyBusinessService)
    .WaitFor(cache)
    .WaitFor(userCentralDb);

builder.Build().Run();
