using FoodyBackend;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllApproved",
        builder =>
        {
            builder.WithOrigins("http://localhost:3000", "https://robinhout.github.io")
                   .AllowAnyMethod()
                   .AllowAnyHeader()
                   .AllowCredentials();
        });
});
builder.Services.AddDbContext<DatabaseContext>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseCors("AllowAllApproved");
app.UseSwagger();
app.UseSwaggerUI();

app.UseRouting();
// app.UseAuthentication();
// app.UseAuthorization();
app.MapControllers();

app.Run();
