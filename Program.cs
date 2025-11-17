using AcademicResourceApp.Data;
using AcademicResourceApp.Helpers;
using AcademicResourceApp.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;


var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = Directory.GetCurrentDirectory()
});

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables();


builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<EmailService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
    };
});

// this is for the database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));


// this is for cloudinary
builder.Services.Configure<CloudinarySettings>(builder.Configuration.GetSection("CloudinarySettings"));
builder.Services.AddSingleton(x =>
{
    var settings = builder.Configuration.GetSection("CloudinarySettings").Get<CloudinarySettings>();
    var account = new CloudinaryDotNet.Account(settings.CloudName, settings.ApiKey, settings.ApiSecret);
    return new CloudinaryDotNet.Cloudinary(account);
});

builder.Services.AddScoped<AuthService>(); 

builder.Services.AddScoped<Microsoft.AspNetCore.Identity.IPasswordHasher<AcademicResourceApp.Models.User>, Microsoft.AspNetCore.Identity.PasswordHasher<AcademicResourceApp.Models.User>>();

builder.Services.AddScoped<NotificationService>();    //Notification service

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins",
        builder => builder.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader());
});

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v2", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "PeerShelf API",
        Version = "v2"
    });
//    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
//    {
//        In = ParameterLocation.Header,
//        Description = "Enter JWT token: Bearer {your token}",
//        Name = "Authorization",
//        Type = SecuritySchemeType.ApiKey,
//    });

//    c.AddSecurityRequirement(new OpenApiSecurityRequirement
//{
//    {
//        new OpenApiSecurityScheme
//        {
//            Reference = new OpenApiReference
//            {
//                Type = ReferenceType.SecurityScheme,
//                Id = "Bearer"
//            }
//        },
//        new string[] {}
//    }
//});

});


var app = builder.Build();


app.MapGet("/health", () => Results.Ok("API is running"));

//just to check if the database os working fine.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        db.Database.Migrate(); // Applies any pending migrations, creates DB if not exists
        Console.WriteLine("Database connection and migration successful.");
    }
    catch (Exception ex)    
    {
        Console.WriteLine($"Database connection failed: {ex.Message}");
        throw;
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v2/swagger.json", "PeerShelf API v2");
        c.RoutePrefix = "docs"; // Swagger available at /docs
    });
}




app.UseHttpsRedirection();

app.UseCors("AllowAllOrigins");

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();
