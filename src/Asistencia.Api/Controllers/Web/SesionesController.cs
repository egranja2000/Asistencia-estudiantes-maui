using Asistencia.Api.Infraestructura;
using Asistencia.Api.Seguridad;
using Asistencia.Api.Servicios;
using Asistencia.Api.ViewModels;
using Asistencia.Shared.Constantes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRCoder;

namespace Asistencia.Api.Controllers.Web;

[Authorize(Policy = Politicas.Docente)]
public sealed class SesionesController(
    SesionService sesiones, CursoService cursos, SimuladorService simulador,
    ZonaHoraria zona, TimeProvider reloj, IWebHostEnvironment env) : DocenteControllerBase
{
    /// <summary>SOLO DESARROLLO: estudiantes mock marcan el checkpoint activo (misma validación que la app).</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Simular(int id, int porcentaje, bool incluirErrores)
    {
        if (!env.IsDevelopment()) return NotFound();
        var sesion = await sesiones.ObtenerAsync(id, DocenteId);
        if (sesion is null) return NotFound();

        var r = await simulador.SimularAsync(sesion, porcentaje, incluirErrores);
        if (r.Error is not null)
            TempData["Error"] = r.Error;
        else
            TempData["Ok"] = r.Total == 0
                ? "No quedan estudiantes mock pendientes en este checkpoint."
                : "Simulación: " + string.Join(" · ", r.PorCodigo.OrderByDescending(x => x.Value).Select(x => $"{x.Key} × {x.Value}"));
        return RedirectToAction(nameof(Detalle), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Crear(int cursoId)
    {
        var curso = await cursos.ObtenerAsync(cursoId, DocenteId);
        if (curso is null) return NotFound();

        return View(CrearSesionViewModel.Para(curso, zona.HoyLocal(reloj)));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Crear(CrearSesionViewModel m)
    {
        if (ModelState.IsValid)
        {
            var (id, error) = await sesiones.CrearAsync(DocenteId, m);
            if (id is not null) return RedirectToAction(nameof(Detalle), new { id });
            ModelState.AddModelError("", error!);
        }
        m.NombreCurso = (await cursos.ObtenerAsync(m.CursoId, DocenteId))?.Nombre;
        return View(m);
    }

    public async Task<IActionResult> Detalle(int id)
    {
        var vm = await sesiones.DetalleAsync(id, DocenteId);
        return vm is null ? NotFound() : View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> LanzarCheckpoint(int id, int duracionSegundos, int rotacionSegundos, bool proyectar)
    {
        var error = await sesiones.LanzarCheckpointAsync(id, DocenteId, duracionSegundos, rotacionSegundos);
        if (error is not null) TempData["Error"] = error;
        return proyectar && error is null
            ? RedirectToAction(nameof(Proyectar), new { id })
            : RedirectToAction(nameof(Detalle), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelarCheckpoint(int id, int checkpointId)
    {
        await sesiones.CancelarCheckpointAsync(checkpointId, DocenteId);
        return RedirectToAction(nameof(Detalle), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cerrar(int id)
    {
        await sesiones.CerrarAsync(id, DocenteId);
        TempData["Ok"] = "Sesión cerrada. La asistencia quedó consolidada.";
        return RedirectToAction(nameof(Detalle), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Ajustar(int id, int estudianteId, string? estado, string? motivo)
    {
        string[] validos = [EstadosAsistencia.Presente, EstadosAsistencia.Parcial, EstadosAsistencia.Ausente, EstadosAsistencia.Justificado];
        if (!string.IsNullOrEmpty(estado) && !validos.Contains(estado)) return BadRequest();

        await sesiones.AjustarAsync(id, estudianteId, estado, motivo, DocenteId);
        return RedirectToAction(nameof(Detalle), new { id });
    }

    /// <summary>Pantalla completa para proyectar en el aula o compartir en Zoom/Teams/Meet.</summary>
    public async Task<IActionResult> Proyectar(int id)
    {
        var sesion = await sesiones.ObtenerAsync(id, DocenteId);
        return sesion is null ? NotFound() : View(sesion);
    }

    /// <summary>Consultado por la pantalla de proyección cada ~2 s. Nunca devuelve el secreto.</summary>
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Estado(int id)
    {
        var e = await sesiones.EstadoProyeccionAsync(id, DocenteId);
        if (e is null) return NotFound();

        string? qr = null;
        if (e.PayloadQr is not null)
        {
            using var generador = new QRCodeGenerator();
            using var datos = generador.CreateQrCode(e.PayloadQr, QRCodeGenerator.ECCLevel.M);
            qr = "data:image/png;base64," + Convert.ToBase64String(new PngByteQRCode(datos).GetGraphic(12));
        }

        return Json(new
        {
            e.EstadoSesion, e.Activo, e.CheckpointId, e.Orden, e.Paso, e.Codigo,
            e.SegundosToken, e.RotacionSegundos, e.SegundosCheckpoint, e.Registrados, e.Matriculados,
            Qr = qr
        });
    }
}
