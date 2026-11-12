using Asistencia.Api.Servicios;
using Asistencia.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Asistencia.Api.Controllers.Api;

/// <summary>POST /api/auth/login  — login del estudiante desde la app MAUI.</summary>
[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public sealed class AuthApiController(AuthService auth) : ControllerBase
{
    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest req)
    {
        var (respuesta, error) = await auth.LoginEstudianteAsync(req);
        return respuesta is null ? BadRequest(new ErrorResponse(error!)) : Ok(respuesta);
    }
}
