using System.Globalization;
using System.Security.Claims;
using Asistencia.Api.Seguridad;
using Asistencia.Api.Servicios;
using Asistencia.Shared.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Asistencia.Api.Controllers.Api;

/// <summary>Endpoints del estudiante. Requieren JWT con rol Estudiante y claim "dev".</summary>
[ApiController]
[Route("api/estudiante")]
[Authorize(Policy = Politicas.EstudianteApi)]
public sealed class EstudianteApiController(AsistenciaService asistencia) : ControllerBase
{
    private int EstudianteId => int.Parse(User.FindFirstValue("sub")!, CultureInfo.InvariantCulture);
    private string DeviceId => User.FindFirstValue("dev")!;

    /// <summary>Checkpoints abiertos ahora en mis cursos.</summary>
    [HttpGet("checkpoints-activos")]
    public async Task<IReadOnlyList<CheckpointActivoDto>> CheckpointsActivos() =>
        await asistencia.CheckpointsActivosAsync(EstudianteId);

    /// <summary>Marca asistencia con el QR escaneado o el código de 6 dígitos.</summary>
    [HttpPost("marcar")]
    [EnableRateLimiting("marcacion")]
    public async Task<ActionResult<ResultadoMarcacion>> Marcar(MarcarRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Valor) || req.Valor.Length > 200)
            return BadRequest(new ErrorResponse("Valor no válido."));

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        return await asistencia.MarcarAsync(EstudianteId, DeviceId, ip, req);
    }

    [HttpGet("historial")]
    public async Task<IReadOnlyList<HistorialItemDto>> Historial([FromQuery] int dias = 30) =>
        await asistencia.HistorialAsync(EstudianteId, dias);
}
