namespace Asistencia.Maui.ViewModels;

public sealed record CheckpointItem(string Curso, string Detalle, bool YaRegistrado)
{
    public string Estado => YaRegistrado ? "✓ Registrado" : "Pendiente";
    public Color ColorEstado => YaRegistrado ? Color.FromArgb("#047857") : Color.FromArgb("#B45309");
    public Color FondoEstado => YaRegistrado ? Color.FromArgb("#D1FAE5") : Color.FromArgb("#FEF3C7");
}
