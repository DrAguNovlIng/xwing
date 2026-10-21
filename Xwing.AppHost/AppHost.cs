using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume();

var xwingDb = sql.AddDatabase("xwing");

builder.AddProject<Projects.Xwing_Web>("web")
    .WithReference(xwingDb)
    .WaitFor(xwingDb);

await builder.Build().RunAsync();
