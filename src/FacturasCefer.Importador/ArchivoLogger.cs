namespace FacturasCefer.Importador;

/// <summary>Log diario en <c>logs\importador-AAAA-MM-DD.log</c> junto al exe (además del Visor de eventos).</summary>
public sealed class ArchivoLoggerProvider : ILoggerProvider
{
    private readonly string _carpeta;
    private readonly object _bloqueo = new();

    public ArchivoLoggerProvider(string carpeta)
    {
        _carpeta = carpeta;
        Directory.CreateDirectory(carpeta);
    }

    public ILogger CreateLogger(string categoria) => new ArchivoLogger(this);

    public void Dispose() { }

    private void Escribir(LogLevel nivel, string mensaje, Exception? ex)
    {
        var linea = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{nivel}] {mensaje}{(ex is null ? "" : Environment.NewLine + ex)}{Environment.NewLine}";
        var ruta = Path.Combine(_carpeta, $"importador-{DateTime.Today:yyyy-MM-dd}.log");
        lock (_bloqueo)
        {
            try { File.AppendAllText(ruta, linea); } catch { /* no romper el servicio por el log */ }
        }
    }

    private sealed class ArchivoLogger : ILogger
    {
        private readonly ArchivoLoggerProvider _p;
        public ArchivoLogger(ArchivoLoggerProvider p) => _p = p;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel nivel) => nivel >= LogLevel.Information;

        public void Log<TState>(LogLevel nivel, EventId id, TState state, Exception? ex, Func<TState, Exception?, string> formato)
        {
            if (IsEnabled(nivel)) _p.Escribir(nivel, formato(state, ex), ex);
        }
    }
}
