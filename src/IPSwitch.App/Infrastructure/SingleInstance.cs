namespace IPSwitch.App.Infrastructure;

/// <summary>
/// Stellt sicher, dass pro Benutzersitzung nur eine Instanz läuft. Ein zweiter Start signalisiert
/// der ersten Instanz, das Verwaltungsfenster anzuzeigen.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _showSignal;
    private RegisteredWaitHandle? _registration;

    /// <param name="name">Name der Instanzsperre.</param>
    /// <param name="wait">Wie lange auf eine sich beendende Instanz gewartet wird.</param>
    public SingleInstance(string name, TimeSpan wait)
    {
        _mutex = new Mutex(initiallyOwned: false, $@"Local\{name}.Instance");
        try
        {
            IsFirstInstance = _mutex.WaitOne(wait);
        }
        catch (AbandonedMutexException)
        {
            // Vorherige Instanz wurde hart beendet – die Sperre gehört jetzt uns.
            IsFirstInstance = true;
        }

        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, $@"Local\{name}.Show");
    }

    public bool IsFirstInstance { get; }

    public void SignalFirstInstance() => _showSignal.Set();

    /// <summary>Ruft <paramref name="onSignal"/> (auf einem Threadpool-Thread) auf, wenn eine weitere Instanz gestartet wurde.</summary>
    public void Listen(Action onSignal) =>
        _registration = ThreadPool.RegisterWaitForSingleObject(_showSignal, (_, _) => onSignal(), null, Timeout.Infinite, executeOnlyOnce: false);

    public void Dispose()
    {
        _registration?.Unregister(null);
        _showSignal.Dispose();
        if (IsFirstInstance)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Mutex gehört nicht (mehr) diesem Thread.
            }
        }

        _mutex.Dispose();
    }
}
