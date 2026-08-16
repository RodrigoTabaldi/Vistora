using Microsoft.AspNetCore.Mvc;
using SaasVistoria.Application;

namespace SaasVistoria.Controllers;
[ApiController, Route("api/auth")]
public sealed class AuthController(IVistoraStore store, TokenService tokens) : ControllerBase
{
    [HttpPost("login")]
    public IActionResult Login(LoginRequest request) =>
        store.Validate(request.Email, request.Password) is { } user
            ? Ok(tokens.Create(user))
            : Unauthorized(new { message = "E-mail ou senha inválidos." });

    [HttpPost("forgot-password")]
    public IActionResult ForgotPassword([FromBody] string email) =>
        Accepted(new { message = "Se o e-mail existir, enviaremos as instruções de recuperação." });
}
