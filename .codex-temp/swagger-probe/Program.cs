using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:5056");

var controllerAssembly = Assembly.LoadFrom(Path.GetFullPath(Path.Combine(
    AppContext.BaseDirectory,
    "..", "..", "..", "..", "..",
    "bin", "Debug", "net9.0", "FoodyBackend.dll")));

builder.Services.AddControllers()
    .ConfigureApplicationPartManager(manager => manager.ApplicationParts.Add(new AssemblyPart(controllerAssembly)));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseSwagger();
app.MapControllers();
app.Run();
