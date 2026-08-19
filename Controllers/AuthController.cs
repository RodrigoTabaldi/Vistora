using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SaasVistoria.Application;

namespace SaasVistoria.Controllers;
[ApiController, Route("api/auth")]
public sealed class AuthController(IVistoraStore store, TokenService tokens) : ControllerBase
{
    [HttpPost("login"), EnableRateLimiting("login")]
    public IActionResult Login(LoginRequest request) =>
        store.Validate(request.Email, request.Password) is { } user
            ? Ok(tokens.Create(user))
            : Unauthorized(new { message = "E-mail ou senha inválidos." });

    [HttpPost("register"), EnableRateLimiting("register")]
    public IActionResult Register(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Preencha nome, e-mail e senha." });
        var email = request.Email.Trim();
        if (email.Count(c => c == '@') != 1 || email.StartsWith('@') || email.EndsWith('@') || email.Contains(' '))
            return BadRequest(new { message = "Informe um e-mail válido." });
        if (request.Password.Length < 8)
            return BadRequest(new { message = "A senha precisa ter ao menos 8 caracteres." });
        var user = store.Register(request.Name.Trim(), email, request.Password);
        return user is null
            ? Conflict(new { message = "Já existe uma conta com este e-mail." })
            : Ok(tokens.Create(user));
    }

    [HttpPost("forgot-password")]
    public IActionResult ForgotPassword([FromBody] string email) =>
        Accepted(new { message = "Se o e-mail existir, enviaremos as instruções de recuperação." });
}
