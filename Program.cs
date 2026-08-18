using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using SaasVistoria.Application;
using SaasVistoria.Domain;
using SaasVistoria.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddSingleton<IVistoraStore, DemoVistoraStore>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddCors(options => options.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));
// Limita tentativas de login/cadastro por IP — mitiga força bruta e spam de cadastro.
// AddFixedWindowLimiter (sem partição) compartilharia um único orçamento global entre todos os
// clientes; aqui particionamos por IP para que um cliente não consuma o limite de outro.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

var app = builder.Build();
app.UseExceptionHandler(error => error.Run(async context =>
{
    context.Response.StatusCode = 500;
    var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    await context.Response.WriteAsJsonAsync(new { title = "Não foi possível concluir esta operação.", traceId = context.TraceIdentifier, detail = app.Environment.IsDevelopment() ? exception?.Message : null });
}));
app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors();
app.UseRateLimiter();

// Autenticação por token assinado (JWT HS256) — protege todas as rotas /api exceto /api/auth
app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    if (path.StartsWithSegments("/api") && !path.StartsWithSegments("/api/auth"))
    {
        var tokens = context.RequestServices.GetRequiredService<TokenService>();
        var header = context.Request.Headers.Authorization.ToString();
        var raw = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header["Bearer ".Length..].Trim() : null;
        var userId = tokens.ValidateAndGetUserId(raw);
        if (userId is null)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { message = "Sessão expirada ou inválida. Faça login novamente." });
            return;
        }
        var user = context.RequestServices.GetRequiredService<IVistoraStore>().FindUser(userId.Value);
        if (user is null) { context.Response.StatusCode = 401; return; }
        context.Items["user"] = user;
    }
    await next();
});

app.MapOpenApi();
app.MapControllers();
app.MapFallbackToFile("index.html");
app.Run();
