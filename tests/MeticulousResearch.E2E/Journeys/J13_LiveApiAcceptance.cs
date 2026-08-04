using MeticulousResearch.E2E.Support;

namespace MeticulousResearch.E2E.Journeys;

/// <summary>
/// J-13 — Live-API acceptance (single, gated, opt-in). Runs the complete
/// source→branded-deliverable flow against the real Anthropic API. It needs a real
/// <c>ANTHROPIC_API_KEY</c> and network access, so it is excluded from the headless gate
/// (Category=manual + requires-key) and executed by hand as the release acceptance run.
/// </summary>
public sealed class J13_LiveApiAcceptance
{
    // @e2e @requires-key @requires-network @manual
    // Scenario: The complete source-to-branded-deliverable flow works against the live API
    //   Manual acceptance checklist (run only with a real key; cap token spend on cheap tiers, §7 Q5):
    //   1. Configure a real ANTHROPIC_API_KEY.
    //   2. Create a project from the Market Research Report template with real resources.
    //   3. Hold a grounded, streaming conversation and generate a report artifact.
    //   4. Iterate with Edit-with-Claude; export a branded PDF and an XLSX forecast.
    //   5. Verify per-turn and consolidated cost reflect authoritative API usage fields.
    //   6. Verify the whole flow completes with no crashes, placeholder screens, or raw errors.
    [Fact(Skip = "Live-API acceptance: requires a real ANTHROPIC_API_KEY + network; run by hand as the release gate.")]
    [Trait("Category", "manual")]
    [Trait("Category", "requires-key")]
    public void The_complete_source_to_branded_deliverable_flow_works_against_the_live_api()
    {
    }
}
