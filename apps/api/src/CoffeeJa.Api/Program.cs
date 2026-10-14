using System.Text;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using CoffeeJa.Api.Common;
using CoffeeJa.Api.Features;
using CoffeeJa.Api.Services;
using CoffeeJa.Domain;
using CoffeeJa.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
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

// Em produção, recusa configurações inseguras (chaves de desenvolvimento, origens locais, dados demo...).
if (!builder.Environment.IsDevelopment())
{
    var problemas = new List<string>();
    if (jwt.Key.Contains("desenvolvimento", StringComparison.OrdinalIgnoreCase)) problemas.Add("Jwt:Key é a chave de desenvolvimento");
    if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("Default"))) problemas.Add("ConnectionStrings:Default vazio");
    if (plataforma.Origens.Any(o => o.Contains("localhost", StringComparison.OrdinalIgnoreCase))) problemas.Add("Plataforma:Origens aponta para localhost");
    if (!plataforma.UrlPublica.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) problemas.Add("Plataforma:UrlPublica precisa ser https");
    if (builder.Configuration.GetValue("Banco:DadosDemo", false)) problemas.Add("Banco:DadosDemo deve ser false");
    if (plataforma.AdminSenha is "Admin@12345" or "Demo@12345") problemas.Add("Plataforma:AdminSenha é a senha de desenvolvimento");
    if (plataforma.AprovarFornecedoresAutomaticamente) problemas.Add("Plataforma:AprovarFornecedoresAutomaticamente deve ser false");
    if (problemas.Count > 0) throw new InvalidOperationException("Configuração insegura para produção: " + string.Join("; ", problemas) + ".");
}

builder.WebHost.ConfigureKestrel(k =>
{
    k.AddServerHeader = false;
    k.Limits.MaxRequestBodySize = 12 * 1024 * 1024; // NF até 10 MB + overhead do multipart
    k.Limits.MaxRequestHeadersTotalSize = 32 * 1024;
    k.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddDataProtection().SetApplicationName("CoffeeJa")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.IsPathRooted(plataforma.PastaChaves) ? plataforma.PastaChaves : Path.Combine(builder.Environment.ContentRootPath, plataforma.PastaChaves)));
builder.Services.AddSingleton<SegredoProtetor>();
builder.Services.AddSingleton<SessaoCache>();
builder.Services.AddSingleton<AuditoriaService>();

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
    o.Events = new JwtBearerEvents
    {
        // Token vem do cookie HttpOnly (ou do cabeçalho Authorization); nunca pela URL, para não vazar em logs.
        OnMessageReceived = SegurancaHttp.LerTokenDoCookie,
        OnTokenValidated = SegurancaHttp.ValidarSessao,
    };
});
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Politicas.Contratante, p => p.RequireRole(nameof(Papel.Contratante)))
    .AddPolicy(Politicas.Fornecedor, p => p.RequireRole(nameof(Papel.Fornecedor)))
    .AddPolicy(Politicas.Admin, p => p.RequireRole(nameof(Papel.Admin)).RequireClaim("mfa", "1"))
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
    // Limite geral por usuário autenticado (ou IP); os limites nomeados abaixo se somam a ele.
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
    {
        if (ctx.Request.Path.StartsWithSegments("/health")) return RateLimitPartition.GetNoLimiter("health");
        var chave = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) is { } uid ? $"u:{uid}" : $"ip:{Rede.Ip(ctx)}";
        return RateLimitPartition.GetSlidingWindowLimiter(chave, _ => new SlidingWindowRateLimiterOptions
            { PermitLimit = plataforma.LimiteGlobalPorMinuto, Window = TimeSpan.FromMinutes(1), SegmentsPerWindow = 6 });
    });
    o.AddPolicy("mensagens", ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? Rede.Ip(ctx),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1) }));
    o.AddPolicy("upload", ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? Rede.Ip(ctx),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
    o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(Rede.Ip(ctx),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = plataforma.LimiteAuthPorMinuto, Window = TimeSpan.FromMinutes(1) }));
    o.AddPolicy("geo", ctx => RateLimitPartition.GetFixedWindowLimiter(ctx.User.Identity?.Name ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "?",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1) }));
});
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Só confia em X-Forwarded-* vindo dos proxies configurados (padrão: loopback). Evita falsificar o IP para escapar dos limites.
    foreach (var ip in plataforma.ProxiesConfiaveis)
        if (System.Net.IPAddress.TryParse(ip, out var addr)) o.KnownProxies.Add(addr);
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
if (!app.Environment.IsDevelopment()) app.UseHsts();
app.UseCabecalhosSeguranca();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseLimiteJson();
app.UseCors();
app.UseProtecaoCsrf();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapGet("/health/live", () => Results.Ok(new { status = "live" })).AllowAnonymous();
app.MapGet("/health/ready", async (CoffeeJaDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct) ? Results.Ok(new { status = "ready" }) : Results.Json(new { status = "db-indisponivel" }, statusCode: 503)).AllowAnonymous();
app.MapAuth();
app.MapFornecedor();
app.MapDemandas();
app.MapPropostas();
app.MapPedidos();
app.MapOutros();
app.MapContas();
app.MapSeguranca();
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
