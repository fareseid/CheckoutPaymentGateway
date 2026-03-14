using System.Text;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

using PaymentGateway.Api.Configuration;
using PaymentGateway.Api.Infrastructure.BankSimulator;
using PaymentGateway.Api.Infrastructure.Repositories;
using PaymentGateway.Api.Middleware;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Services;
using PaymentGateway.Api.Services.Auth;
using PaymentGateway.Api.Validation;

var builder = WebApplication.CreateBuilder(args);

// -----------------------------------------------------------------------
// Configuration
// -----------------------------------------------------------------------
builder.Services.Configure<BankSimulatorOptions>(
    builder.Configuration.GetSection(BankSimulatorOptions.SectionName));
builder.Services.Configure<HmacOptions>(
    builder.Configuration.GetSection(HmacOptions.SectionName));
builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection(JwtOptions.SectionName));

// -----------------------------------------------------------------------
// Authentication — JWT bearer
// -----------------------------------------------------------------------
var jwtOptions = builder.Configuration
    .GetSection(JwtOptions.SectionName)
    .Get<JwtOptions>()!;

var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
builder.Services.Configure<JwtOptions>(jwtSection);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        // Read directly from configuration — never .Get<>() which can return null
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"] ?? string.Empty,
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"] ?? string.Empty,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSection["SecretKey"] ?? string.Empty)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// -----------------------------------------------------------------------
// Services
// -----------------------------------------------------------------------
builder.Services.AddSingleton<IHmacSignatureService, HmacSignatureService>();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddSingleton<IValidator<PostPaymentRequest>, PaymentRequestValidator>();
builder.Services.AddSingleton<IPaymentService, PaymentService>();
builder.Services.AddSingleton<IBankSimulatorClient, BankSimulatorClient>();

// -----------------------------------------------------------------------
// Controllers + API
// -----------------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();

builder.Services.AddSingleton<IPaymentsRepository, PaymentsRepository>();

var app = builder.Build();

// -----------------------------------------------------------------------
// Middleware pipeline — ORDER MATTERS
// -----------------------------------------------------------------------

// 1. Global exception handler — must be outermost so it catches everything
app.UseMiddleware<GlobalExceptionMiddleware>();

// 2. HTTPS redirection
app.UseHttpsRedirection();

// 3. Authentication + authorisation — must come before HMAC so the
//    JWT identity is established before we check the request signature
app.UseAuthentication();
app.UseAuthorization();

// 4. HMAC validation — only fires on authenticated, mutating requests
app.UseMiddleware<HmacValidationMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();
public partial class Program { }