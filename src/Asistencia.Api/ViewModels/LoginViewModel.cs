using System.ComponentModel.DataAnnotations;

namespace Asistencia.Api.ViewModels;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Ingresa tu correo o identificación")]
    public string Usuario { get; set; } = "";

    [Required(ErrorMessage = "Ingresa tu contraseña"), DataType(DataType.Password)]
    public string Password { get; set; } = "";

    public string? ReturnUrl { get; set; }
}
