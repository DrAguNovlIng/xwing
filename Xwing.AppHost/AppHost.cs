using Projects;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.Xwing_Web>("web");

await builder.Build().RunAsync();
