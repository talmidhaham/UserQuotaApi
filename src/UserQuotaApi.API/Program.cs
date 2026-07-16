using UserQuotaApi.API.Extensions;
using UserQuotaApi.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddDatabase();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "User Quota API", Version = "v1" });
});
builder.Services.AddRepositories();

var app = builder.Build();

// Ensure SQLite schema exists (no migrations needed)
await app.MigrateDbAsync();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();
app.MapDefaultEndpoints();

app.Run();

// Expose for integration testing
public partial class Program { }
