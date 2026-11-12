using Asistencia.Api.Infraestructura;
using Asistencia.Api.Seguridad;
using Asistencia.Api.Servicios;
using Asistencia.Api.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistencia.Api.Controllers.Web;

[Authorize(Policy = Politicas.Docente)]
public sealed class CursosController(
    CursoService cursos, SesionService sesiones, ZonaHoraria zona, TimeProvider reloj) : DocenteControllerBase
{
    public async Task<IActionResult> Index() => View(await cursos.ListarAsync(DocenteId));

    public async Task<IActionResult> Detalle(int id)
    {
        var curso = await cursos.ObtenerAsync(id, DocenteId);
        if (curso is null) return NotFound();

        return View(new DetalleCursoViewModel
        {
            Curso = curso,
            Sesiones = await sesiones.ListarPorCursoAsync(id),
            Porcentajes = await cursos.PorcentajesAsync(id),
            NuevaSesion = CrearSesionViewModel.Para(curso, zona.HoyLocal(reloj))
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Desvincular(int cursoId, int estudianteId, int? sesionId)
    {
        await cursos.DesvincularDispositivoAsync(cursoId, estudianteId, DocenteId);
        TempData["Ok"] = "Dispositivo desvinculado. El estudiante puede iniciar sesión en su nuevo celular.";
        return sesionId is null
            ? RedirectToAction(nameof(Detalle), new { id = cursoId })
            : RedirectToAction("Detalle", "Sesiones", new { id = sesionId });
    }
}
