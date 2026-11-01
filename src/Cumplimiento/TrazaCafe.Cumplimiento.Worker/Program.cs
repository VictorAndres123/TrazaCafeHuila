using TrazaCafe.BuildingBlocks.Health;
using TrazaCafe.Cumplimiento.Infrastructure;
using TrazaCafe.Cumplimiento.Infrastructure.Geoespacial;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(o =>
{
    o.SingleLine = true;
    o.IncludeScopes = true;
    o.TimestampFormat = "HH:mm:ss.fff ";
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddCumplimiento(builder.Configuration);

var app = builder.Build();

// Falla rápido si la capa geoespacial no se puede cargar.
_ = app.Services.GetRequiredService<NtsVerificadorDeforestacion>();

app.MapGet("/", () => Results.Ok(new
{
    servicio = "TrazaCafé Huila — Cumplimiento (worker)",
    consume = "cumplimiento.eventos",
    salud = "/health/ready"
}));
app.MapEndpointsDeSalud();

app.Run();
