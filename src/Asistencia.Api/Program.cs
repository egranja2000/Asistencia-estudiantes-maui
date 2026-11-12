using Asistencia.Api.Extensiones;

var builder = WebApplication.CreateBuilder(args);

var jwt = builder.Services.AddAsistenciaConfiguracion(builder.Configuration);
builder.Services
    .AddAsistenciaServicios()
    .AddAsistenciaAutenticacion(jwt)
    .AddAsistenciaRateLimiting(builder.Configuration)
    .AddControllersWithViews();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Cuenta/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCulturaInstitucion();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();   // asigna HttpContext.User (incluido JWT) antes del rate limiter
app.UseRateLimiter();

app.MapControllers();
app.MapControllerRoute("default", "{controller=Cursos}/{action=Index}/{id?}");

app.Run();

/// <summary>Expuesto para pruebas de integración (WebApplicationFactory).</summary>
public partial class Program;
