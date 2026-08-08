namespace MeticulousResearch.Core.Onboarding;

/// <summary>
/// The shipped, static content for the first-run sample project (SPEC §3.8(4)). Held as bundled
/// strings so the sample project — a couple of resources plus an example Market Research Report
/// artifact — is created deterministically and entirely offline (no network, no API key). The
/// example report mirrors the Market Research Report template shape so it is representative for
/// downstream template/export reviewers.
/// </summary>
public static class SampleContent
{
    /// <summary>The sample project's display name.</summary>
    public const string ProjectName = "Sample: EV Battery Market 2026";

    /// <summary>The sample project's short description.</summary>
    public const string ProjectDescription =
        "A ready-made example project showing how resources ground a research deliverable.";

    /// <summary>Custom instructions seeded on the sample project.</summary>
    public const string ProjectInstructions =
        "Write concise, source-grounded market research suitable for an executive audience.";

    /// <summary>Title of the first bundled resource.</summary>
    public const string ResourceOneTitle = "Market Overview";

    /// <summary>Body of the first bundled resource.</summary>
    public const string ResourceOneText =
        "The global electric-vehicle battery market is projected to grow steadily through 2026, "
        + "driven by falling cell costs, expanding charging infrastructure, and tightening emissions "
        + "regulation across major economies. NMC and LFP chemistries dominate current deployments.";

    /// <summary>Title of the second bundled resource.</summary>
    public const string ResourceTwoTitle = "Competitive Landscape";

    /// <summary>Body of the second bundled resource.</summary>
    public const string ResourceTwoText =
        "A handful of cell manufacturers hold the majority of global capacity, competing on energy "
        + "density, cycle life, and supply-chain resilience. New entrants differentiate on LFP cost "
        + "leadership and on solid-state research roadmaps.";

    /// <summary>Title of the bundled example image resource (image-vision-caption, SPEC §3.2.1).</summary>
    public const string ImageResourceTitle = "Battery cost chart";

    /// <summary>
    /// A tiny valid PNG (2×2) with a correct signature + IHDR so the image header reader yields
    /// positive pixel dimensions. Used to seed a bundled image resource offline; it is not a
    /// renderable image, only exercises the add/store/preview path deterministically.
    /// </summary>
    public static byte[] ImageBytes { get; } =
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D,
        0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x02,
        0x00, 0x00, 0x00, 0x02,
        0x08, 0x06, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00,
    };


    /// <summary>The example artifact's title (matches the Market Research Report template shape).</summary>
    public const string ArtifactTitle = "Market Research Report";

    /// <summary>The example Market Research Report artifact content (bundled Markdown, not live-generated).</summary>
    public const string MarketResearchReport =
        "# Market Research Report: EV Battery Market 2026\n\n"
        + "## Executive Summary\n"
        + "The EV battery market continues a multi-year expansion, underpinned by cost declines and "
        + "supportive policy. This example report demonstrates how MeticulousResearch grounds a "
        + "deliverable in your project resources.\n\n"
        + "## Market Overview\n"
        + "Demand growth is broad-based across passenger and commercial segments, with NMC and LFP "
        + "chemistries serving distinct cost/performance niches.\n\n"
        + "## Competitive Landscape\n"
        + "Capacity remains concentrated among a few incumbents, while challengers pursue LFP cost "
        + "leadership and solid-state roadmaps.\n\n"
        + "## Outlook\n"
        + "Continued cost reductions and infrastructure build-out are expected to sustain growth "
        + "through 2026.\n";

    /// <summary>A second, lightly-revised version of the sample report (adds a sizing figure).</summary>
    public const string MarketResearchReportV2 =
        "# Market Research Report: EV Battery Market 2026\n\n"
        + "## Executive Summary\n"
        + "The EV battery market continues a multi-year expansion, underpinned by cost declines and "
        + "supportive policy. The 2026 market is estimated near $130B. This example report "
        + "demonstrates how MeticulousResearch grounds a deliverable in your project resources.\n\n"
        + "## Market Overview\n"
        + "Demand growth is broad-based across passenger and commercial segments, with NMC and LFP "
        + "chemistries serving distinct cost/performance niches.\n\n"
        + "## Competitive Landscape\n"
        + "Capacity remains concentrated among a few incumbents, while challengers pursue LFP cost "
        + "leadership and solid-state roadmaps.\n\n"
        + "## Outlook\n"
        + "Continued cost reductions and infrastructure build-out are expected to sustain growth "
        + "through 2026.\n";

    /// <summary>A third, current version of the sample report (sharpens the outlook section).</summary>
    public const string MarketResearchReportV3 =
        "# Market Research Report: EV Battery Market 2026\n\n"
        + "## Executive Summary\n"
        + "The EV battery market continues a multi-year expansion, underpinned by cost declines and "
        + "supportive policy. The 2026 market is estimated near $130B. This example report "
        + "demonstrates how MeticulousResearch grounds a deliverable in your project resources.\n\n"
        + "## Market Overview\n"
        + "Demand growth is broad-based across passenger and commercial segments, with NMC and LFP "
        + "chemistries serving distinct cost/performance niches.\n\n"
        + "## Competitive Landscape\n"
        + "Capacity remains concentrated among a few incumbents, while challengers pursue LFP cost "
        + "leadership and solid-state roadmaps.\n\n"
        + "## Outlook\n"
        + "Continued cost reductions and infrastructure build-out are expected to sustain double-digit "
        + "annual growth through 2026, with LFP capturing a growing share of entry-level demand.\n";
}
