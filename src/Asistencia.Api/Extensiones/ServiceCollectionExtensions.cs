using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Asistencia.Api.Configuracion;
using Asistencia.Api.Infraestructura;
using Asistencia.Api.Seguridad;
using Asistencia.Api.Servicios;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Asistencia.Api.Extensiones;

/// <summary>Registro de servicios de la API, agrupado por responsabilidad.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Opciones de configuración (Jwt y Checkpoints). Devuelve las opciones JWT ya validadas.</summary>
    public static JwtOptions AddAsistenciaConfiguracion(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        services.Configure<CheckpointOptions>(configuration.GetSection("Checkpoints"));

        var jwt = configuration.GetSection("Jwt").Get<JwtOptions>()
                  ?? throw new InvalidOperationException("Falta la sección Jwt");
        if (Encoding.UTF8.GetByteCount(jwt.Clave) < 32)
            throw new InvalidOperationException("Jwt:Clave debe tener al menos 32 bytes");

        return jwt;
    }

    public static IServiceCollection AddAsistenciaServicios(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<DbConnectionFactory>();
        services.AddSingleton<ZonaHoraria>();
        services.AddSingleton<PasswordHasher>();
        services.AddSingleton<JwtService>();
        services.AddSingleton<CheckpointTokenService>();
        services.AddScoped<AuthService>();
        services.AddScoped<AsistenciaService>();
        services.AddScoped<SesionService>();
        services.AddScoped<CursoService>();
        services.AddScoped<SimuladorService>();   // solo se usa en Development
        return services;
    }

    /// <summary>Cookie (web docente) + JWT (app estudiante) y sus políticas de autorización.</summary>
    public static IServiceCollection AddAsistenciaAutenticacion(this IServiceCollection services, JwtOptions jwt)
    {
        services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(o =>
            {
                o.LoginPath = "/Cuenta/Login";
                o.AccessDeniedPath = "/Cuenta/Login";
                o.ExpireTimeSpan = TimeSpan.FromHours(10);
                o.SlidingExpiration = true;
                o.Cookie.HttpOnly = true;
                o.Cookie.SameSite = SameSiteMode.Lax;
            })
            .AddJwtBearer(o =>
            {
                o.MapInboundClaims = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Emisor,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audiencia,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Clave)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = "name",
                    RoleClaimType = "rol"
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(Politicas.Docente, p => p
                .AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .RequireRole("Docente", "Admin"))
            .AddPolicy(Politicas.EstudianteApi, p => p
                .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .RequireRole("Estudiante")
                .RequireClaim("dev"));

        return services;
    }

    /// <summary>Rate limiting: frena la fuerza bruta del código de 6 dígitos y del login.</summary>
    public static IServiceCollection AddAsistenciaRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var marcacionesPorMinuto = configuration.GetValue("RateLimit:MarcacionesPorMinuto", 10);
        var loginsPorMinuto = configuration.GetValue("RateLimit:LoginsPorMinuto", 10);

        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy("marcacion", ctx => RateLimitPartition.GetFixedWindowLimiter(
                ctx.User.FindFirstValue("sub") ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = marcacionesPorMinuto, Window = TimeSpan.FromMinutes(1) }));
            o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
                ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = loginsPorMinuto, Window = TimeSpan.FromMinutes(1) }));
        });

        return services;
    }
}
