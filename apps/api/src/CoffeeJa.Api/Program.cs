using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using CoffeeJa.Api.Common;
using CoffeeJa.Api.Features;
using CoffeeJa.Api.Services;
using CoffeeJa.Domain;
using CoffeeJa.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<PlataformaOptions>(builder.Configuration.GetSection("Plataforma"));
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new();
var plataforma = builder.Configuration.GetSection("Plataforma").Get<PlataformaOptions>() ?? new();
if (jwt.Key.Length < 32) throw new InvalidOperationException("Configure Jwt:Key com ao menos 32 caracteres (variável de ambiente Jwt__Key).");

builder.Services.Configure<PushOptions>(builder.Configuration.GetSection("Push"));
builder.Services.AddSignalR();
builder.Services.AddSingleton<PosSalvarInterceptor>();
builder.Services.AddDbContext<CoffeeJaDbContext>((sp, o) =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("Default"), sql => sql.EnableRetryOnFailure())
     .AddInterceptors(sp.GetRequiredService<PosSalvarInterceptor>()));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.MapInboundClaims = false;
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = jwt.Issuer, ValidAudience = jwt.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
        ClockSkew = TimeSpan.FromMinutes(1),
        NameClaimType = System.Security.Claims.ClaimTypes.Name,
        RoleClaimType = System.Security.Claims.ClaimTypes.Role,
    };
    // WebSocket do SignalR não envia cabeçalho Authorization: o token vai na query string.
    o.Events = new JwtBearerEvents
    {
        OnMessageReceived = ctx =>
        {
            var token = ctx.Request.Query["access_token"];
            if (!string.IsNullOrEmpty(token) && ctx.HttpContext.Request.Path.StartsWithSegments("/hubs")) ctx.Token = token;
            return Task.CompletedTask;
        },
    };
});
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Politicas.Contratante, p => p.RequireRole(nameof(Papel.Contratante)))
    .AddPolicy(Politicas.Fornecedor, p => p.RequireRole(nameof(Papel.Fornecedor)))
    .AddPolicy(Politicas.Admin, p => p.RequireRole(nameof(Papel.Admin)))
    .AddPolicy(Politicas.AdminConta, p => p.RequireClaim("funcao", nameof(FuncaoUsuario.AdminConta)));

builder.Services.ConfigureHttpJsonOptions(o =>
{
    // Enums [Flags] viajam como número (bitmask); os demais, como texto. Conversores específicos vêm antes do genérico.
    o.SerializerOptions.Converters.Add(new JsonNumberEnumConverter<Restricoes>());
    o.SerializerOptions.Converters.Add(new JsonNumberEnumConverter<Descartaveis>());
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(plataforma.Origens).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "?",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = plataforma.LimiteAuthPorMinuto, Window = TimeSpan.FromMinutes(1) }));
    o.AddPolicy("geo", ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.User.Identity?.Name ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "?",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1) }));
});
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

builder.Services.AddSingleton<TokenService>();
builder.Services.AddScoped<INotificador, NotificadorEmBanco>();
builder.Services.AddSingleton<IPagamentoProvider, PagamentoManualProvider>();
builder.Services.AddSingleton<IArquivoStorage, ArquivoStorageLocal>();
builder.Services.AddHttpClient<GeocodingService>(c =>
{
    c.BaseAddress = new Uri("https://nominatim.openstreetmap.org/");
    c.DefaultRequestHeaders.UserAgent.ParseAdd("CoffeeJa/1.0 (contato@coffeeja.com.br)");
    c.Timeout = TimeSpan.FromSeconds(8);
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapAuth();
app.MapFornecedor();
app.MapDemandas();
app.MapPropostas();
app.MapPedidos();
app.MapOutros();
app.MapContas();
app.MapPush();
app.MapHub<ChatHub>("/hubs/chat");
if (app.Environment.IsDevelopment()) app.MapOpenApi();

if (app.Configuration.GetValue("Banco:MigrarNaInicializacao", false))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CoffeeJaDbContext>();
    try { await db.Database.MigrateAsync(); }
    catch (Exception ex) when (app.Environment.IsDevelopment())
    {
        app.Logger.LogCritical(ex, "Falha ao aplicar as migrations. Em desenvolvimento, rode `npm run db:reset` para recriar o banco e tente de novo.");
        throw;
    }
    if (app.Configuration.GetValue("Banco:DadosDemo", false)) await DadosDemo.GerarAsync(db);
    if (!string.IsNullOrWhiteSpace(plataforma.AdminEmail) && !string.IsNullOrWhiteSpace(plataforma.AdminSenha)
        && !await db.Usuarios.AnyAsync(u => u.Papel == Papel.Admin))
    {
        db.Usuarios.Add(AuthEndpoints.NovoUsuario("Administrador", plataforma.AdminEmail, null, plataforma.AdminSenha, Papel.Admin));
        await db.SaveChangesAsync();
    }
}

app.Run();

public partial class Program;
