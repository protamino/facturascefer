using FacturasCefer.Config;
using FacturasCefer.Importador;
using FacturasCefer.Services;

// Modos:
//   (sin argumentos)        servicio de Windows / consola: revisa Drive cada N minutos
//   --una-vez [--simular]   una sola pasada por Drive y termina
//   --probar <ruta.pdf>     analiza un PDF local en SIMULACIÓN (no guarda nada) y muestra el resultado

AppConfig cfg;
try
{
    cfg = AppConfig.Load();
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
if (args.Contains("--simular")) cfg.Importador.Simular = true;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(o => o.ServiceName = "FacturasCefer.Importador");
builder.Logging.AddProvider(new ArchivoLoggerProvider(Path.Combine(AppContext.BaseDirectory, "logs")));
builder.Services.AddSingleton(cfg);
builder.Services.AddSingleton<ExtraccionService>();
builder.Services.AddSingleton<ProveedorService>();
builder.Services.AddSingleton<FacturaService>();
builder.Services.AddSingleton<ImportacionRepository>();
builder.Services.AddSingleton<ImportacionAutomaticaService>();
builder.Services.AddSingleton<ImportadorWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ImportadorWorker>());
var host = builder.Build();

// ---------- Prueba de un PDF local (siempre en simulación)
var iProbar = Array.IndexOf(args, "--probar");
if (iProbar >= 0)
{
    if (iProbar + 1 >= args.Length || !File.Exists(args[iProbar + 1]))
    {
        Console.Error.WriteLine("Uso: FacturasCefer.Importador --probar <ruta del PDF>");
        return 1;
    }
    var ruta = args[iProbar + 1];
    var imp = host.Services.GetRequiredService<ImportacionAutomaticaService>();
    var r = await imp.ProcesarAsync(ruta, Path.GetFileName(ruta), "prueba@local", cfg.Importador.IdUsuario, simular: true, CancellationToken.None);
    Console.WriteLine($"Resultado: {r.Resultado}\n{r.Mensaje}");
    return 0;
}

if (!cfg.Importador.Simular && cfg.Importador.IdUsuario <= 0)
{
    Console.Error.WriteLine("Falta Importador.IdUsuario en appsettings.json (idUsuario de DMSTRA para las altas automáticas).");
    return 1;
}

// ---------- Una sola pasada
if (args.Contains("--una-vez"))
{
    var n = await host.Services.GetRequiredService<ImportadorWorker>().UnaPasadaAsync(CancellationToken.None);
    Console.WriteLine($"PDF procesados: {n}{(cfg.Importador.Simular ? " (simulación: no se ha guardado ni movido nada)" : "")}");
    return 0;
}

await host.RunAsync();
return 0;

namespace FacturasCefer.Importador
{
    public static class Programa
    {
        /// <summary>Ruta absoluta, o relativa a la carpeta del exe.</summary>
        public static string RutaJuntoAlExe(string ruta) =>
            Path.IsPathRooted(ruta) ? ruta : Path.Combine(AppContext.BaseDirectory, ruta);
    }
}
