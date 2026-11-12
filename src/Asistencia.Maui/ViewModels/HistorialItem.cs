using Asistencia.Shared.Constantes;

namespace Asistencia.Maui.ViewModels;

public sealed record HistorialItem(string Fecha, string Curso, string Detalle, string Estado)
{
    /// <summary>Color del texto de la insignia de estado (paleta Tailwind, tono 700).</summary>
    public Color ColorEstado => Estado switch
    {
        EstadosAsistencia.Presente => Color.FromArgb("#047857"),
        EstadosAsistencia.Parcial => Color.FromArgb("#B45309"),
        EstadosAsistencia.Ausente => Color.FromArgb("#BE123C"),
        EstadosAsistencia.Justificado => Color.FromArgb("#0369A1"),
        _ => Color.FromArgb("#475569")
    };

    /// <summary>Fondo de la insignia de estado (tono 50/100).</summary>
    public Color FondoEstado => Estado switch
    {
        EstadosAsistencia.Presente => Color.FromArgb("#D1FAE5"),
        EstadosAsistencia.Parcial => Color.FromArgb("#FEF3C7"),
        EstadosAsistencia.Ausente => Color.FromArgb("#FFE4E6"),
        EstadosAsistencia.Justificado => Color.FromArgb("#E0F2FE"),
        _ => Color.FromArgb("#F1F5F9")
    };
}
