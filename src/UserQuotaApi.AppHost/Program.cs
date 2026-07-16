var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.UserQuotaApi_API>("quota-api")
    .WithExternalHttpEndpoints();

builder.Build().Run();
