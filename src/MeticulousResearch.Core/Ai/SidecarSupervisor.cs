using MeticulousResearch.Core.Time;

namespace MeticulousResearch.Core.Ai;

/// <summary>
/// Supervises the sidecar process (SPEC §8): launches it on demand, auto-restarts it after a crash
/// with a fresh token and endpoint, and throttles after repeated <em>immediate</em> crashes so a
/// persistently-broken sidecar surfaces a "backend unavailable" error instead of a restart storm.
/// The <em>backoff policy</em> for retrying a turn belongs to <c>rate-limit-backoff</c>; this only
/// governs process lifecycle.
/// </summary>
public sealed class SidecarSupervisor : IDisposable
{
    private readonly ISidecarProcessFactory _factory;
    private readonly IClock _clock;
    private readonly object _gate = new();

    private ISidecarProcess? _current;
    private int _immediateCrashes;
    private DateTimeOffset _lastLaunch;
    private DateTimeOffset? _throttledUntil;

    /// <summary>Creates the supervisor over a process factory and the clock.</summary>
    public SidecarSupervisor(ISidecarProcessFactory factory, IClock clock)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>How many immediate crashes in a row trigger throttling. Defaults to 3.</summary>
    public int MaxImmediateCrashes { get; init; } = 3;

    /// <summary>A crash within this window of launch counts as "immediate". Defaults to 2 seconds.</summary>
    public TimeSpan ImmediateWindow { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>How long to refuse relaunching once throttled. Defaults to 30 seconds.</summary>
    public TimeSpan ThrottleBackoff { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>The number of consecutive immediate crashes observed (0 after a stable launch).</summary>
    public int ImmediateCrashCount => _immediateCrashes;

    /// <summary>
    /// Returns a running sidecar, launching or relaunching one as needed. Throws
    /// <see cref="SidecarUnavailableException"/> while throttled after repeated immediate crashes.
    /// </summary>
    public ISidecarProcess EnsureRunning(SidecarStartInfo startInfo)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        lock (_gate)
        {
            var now = _clock.UtcNow;

            if (_throttledUntil is { } until)
            {
                if (now < until)
                    throw new SidecarUnavailableException(ChatErrorMessages.BackendUnavailable);

                // Throttle window elapsed — allow a fresh attempt.
                _throttledUntil = null;
                _immediateCrashes = 0;
            }

            if (_current is { HasExited: false })
                return _current;

            return Launch(startInfo, now);
        }
    }

    private ISidecarProcess Launch(SidecarStartInfo startInfo, DateTimeOffset now)
    {
        ISidecarProcess process;
        try
        {
            process = _factory.Start(startInfo);
        }
        catch (Exception ex) when (ex is not SidecarUnavailableException)
        {
            RegisterImmediateCrash(now);
            throw new SidecarUnavailableException(ChatErrorMessages.BackendUnavailable, ex);
        }

        _current = process;
        _lastLaunch = now;
        process.Exited += OnProcessExited;

        if (process.HasExited)
        {
            // Crashed on launch — count it and possibly throttle.
            RegisterImmediateCrash(now);
            if (_throttledUntil is { } until && now < until)
                throw new SidecarUnavailableException(ChatErrorMessages.BackendUnavailable);
            return process;
        }

        _immediateCrashes = 0;
        return process;
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        lock (_gate)
        {
            var now = _clock.UtcNow;
            if (now - _lastLaunch < ImmediateWindow)
                RegisterImmediateCrash(now);
            else
                _immediateCrashes = 0;
        }
    }

    private void RegisterImmediateCrash(DateTimeOffset now)
    {
        _immediateCrashes++;
        if (_immediateCrashes >= MaxImmediateCrashes)
            _throttledUntil = now + ThrottleBackoff;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            if (_current is not null)
                _current.Exited -= OnProcessExited;
            _current?.Dispose();
            _current = null;
        }
    }
}
