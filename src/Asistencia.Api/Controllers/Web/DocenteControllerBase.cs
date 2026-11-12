using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace Asistencia.Api.Controllers.Web;

public abstract class DocenteControllerBase : Controller
{
    protected int DocenteId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture);
}
