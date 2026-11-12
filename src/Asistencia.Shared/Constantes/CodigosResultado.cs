namespace Asistencia.Shared.Constantes;

/// <summary>Códigos de resultado de una marcación. La app decide qué hacer según el código.</summary>
public static class CodigosResultado
{
    public const string Registrado = "REGISTRADO";
    public const string YaRegistrado = "YA_REGISTRADO";
    public const string QrInvalido = "QR_INVALIDO";
    public const string CodigoInvalido = "CODIGO_INVALIDO";
    public const string TokenExpirado = "TOKEN_EXPIRADO";
    public const string CheckpointCerrado = "CHECKPOINT_CERRADO";
    public const string NoMatriculado = "NO_MATRICULADO";
    public const string DispositivoNoVinculado = "DISPOSITIVO_NO_VINCULADO";
    public const string DispositivoYaUsado = "DISPOSITIVO_YA_USADO";
    /// <summary>No es un error: la app debe obtener el GPS y reenviar.</summary>
    public const string UbicacionRequerida = "UBICACION_REQUERIDA";
    public const string UbicacionSimulada = "UBICACION_SIMULADA";
    public const string FueraDeRango = "FUERA_DE_RANGO";
}
