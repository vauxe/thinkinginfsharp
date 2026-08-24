using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

builder
    .AddProject<Projects.CloudService>("cloud-service")
    .WithHttpEndpoint(name: "http")
    .WithEnvironment("DEPLOYMENT_MODE", "aspire-local")
    .WithHttpHealthCheck("/health/ready");

builder.Build().Run();
