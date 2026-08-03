using Microsoft.EntityFrameworkCore;
using MeticulousResearch.Core.Data;
using MeticulousResearch.Core.Data.Entities;

namespace MeticulousResearch.Core.Onboarding;

/// <summary>
/// <see cref="IOnboardingState"/> backed by the <c>Setting</c> key/value table (the same store
/// <c>ISettingsService</c> uses), so the completed flag survives a restart and a fresh install
/// reliably triggers onboarding. The completed flag is written through on
/// <see cref="MarkCompleted"/>/<see cref="Reset"/>; the current step is kept in memory for the
/// running wizard.
/// </summary>
public sealed class OnboardingState : IOnboardingState
{
    /// <summary>The stable <c>Setting</c>-table key for the "onboarding completed" flag (SPEC §3.8, §9.1(1)).</summary>
    public const string CompletedSettingKey = "onboarding_completed";

    private readonly DataStore _store;

    /// <summary>Creates the state and loads the persisted completed flag from the data store.</summary>
    public OnboardingState(DataStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));

        using var db = _store.CreateDbContext();
        var row = db.Settings.AsNoTracking().FirstOrDefault(s => s.Key == CompletedSettingKey);
        IsCompleted = row is not null && bool.TryParse(row.Value, out var done) && done;
    }

    /// <inheritdoc />
    public bool IsCompleted { get; private set; }

    /// <inheritdoc />
    public OnboardingStep CurrentStep { get; set; } = OnboardingStep.Welcome;

    /// <inheritdoc />
    public void MarkCompleted()
    {
        Persist(true);
        IsCompleted = true;
    }

    /// <inheritdoc />
    public void Reset()
    {
        Persist(false);
        IsCompleted = false;
        CurrentStep = OnboardingStep.Welcome;
    }

    private void Persist(bool completed)
    {
        using var db = _store.CreateDbContext();
        var existing = db.Settings.FirstOrDefault(s => s.Key == CompletedSettingKey);
        var value = completed ? bool.TrueString : bool.FalseString;
        if (existing is null)
            db.Settings.Add(new Setting { Key = CompletedSettingKey, Value = value });
        else
            existing.Value = value;
        db.SaveChanges();
    }
}
