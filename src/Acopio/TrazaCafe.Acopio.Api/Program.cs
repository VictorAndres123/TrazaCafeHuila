using System.Text.Json.Serialization;
using TrazaCafe.Acopio.Api;
using TrazaCafe.Acopio.Api.Endpoints;
using TrazaCafe.Acopio.Infrastructure;
using TrazaCafe.BuildingBlocks.Health;

var builder = WebApplication.CreateBuilder(args);

// Logging estructurado en una línea por evento (con scopes: MessageId, Tipo...)
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(o =>
{
    o.SingleLine = true;
    o.IncludeScopes = true;
    o.TimestampFormat = "HH:mm:ss.fff ";
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ManejadorGlobalDeExcepciones>();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();

builder.Services.AddAcopio(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapOpenApi(); // /openapi/v1.json
app.UseSwaggerUI(o =>
{
    o.SwaggerEndpoint("/openapi/v1.json", "Acopio y Lotes v1");
    o.RoutePrefix = "swagger";
    o.DocumentTitle = "TrazaCafé Huila — Acopio y Lotes";
});
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.MapEntregasEndpoints();
app.MapLotesEndpoints();
app.MapFincasEndpoints();
app.MapEndpointsDeSalud();

app.Run();

public partial class Program;
