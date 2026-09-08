using System.Text.Json.Serialization;
using Auth0.AspNetCore.Authentication.Api;
using MongoDB.Driver;
using TopologyServer;

var builder = WebApplication.CreateBuilder(args);

var auth0Section = builder.Configuration.GetSection("Auth0");
var auth0Domain = auth0Section["Domain"];
var auth0Audience = auth0Section["Audience"];

if (string.IsNullOrWhiteSpace(auth0Domain) || string.IsNullOrWhiteSpace(auth0Audience))
{
    throw new InvalidOperationException("Auth0 configuration is missing. Set Auth0:Domain and Auth0:Audience.");
}

builder.Services.AddSingleton<IMongoClient>(_ =>
    new MongoClient(builder.Configuration.GetConnectionString("MongoDB")));
builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IMongoClient>().GetDatabase("Topology"));

builder.Services.AddAuth0ApiAuthentication(auth0Section);
builder.Services.AddAuthorization();
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IRoomService, RoomService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program { }