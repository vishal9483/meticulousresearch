namespace MeticulousResearch.Core.Onboarding;

/// <summary>
/// Builds the optional first-run sample project (SPEC §3.8(4)) from bundled content: a research
/// project seeded with a couple of resources and one example "Market Research Report" artifact.
/// Everything is created from shipped strings — no network call and no API key required — so the
/// sample is deterministic and works offline (proven by the no-key onboarding scenario).
/// </summary>
public interface ISampleProjectFactory
{
    /// <summary>
    /// Creates the sample project with its bundled resources and example Market Research Report
    /// artifact, and returns it. Uses only bundled content; performs no network call.
    /// </summary>
    /// <returns>The created sample <see cref="Data.Entities.Project"/>.</returns>
    Data.Entities.Project CreateSampleProject();
}
