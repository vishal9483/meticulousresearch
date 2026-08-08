using MeticulousResearch.Core.Resources.Vision;

namespace MeticulousResearch.App.Services;

/// <summary>
/// Deterministic, offline <see cref="IImageCaptioner"/> used only under the FlaUI @ui harness so a
/// seeded image resource carries a cached caption (image-vision-caption, SPEC §3.2.1) without any
/// vision call, key, or network. Never registered in a normal run.
/// </summary>
internal sealed class SampleImageCaptioner : IImageCaptioner
{
    /// <inheritdoc />
    public string Caption(string imagePath) =>
        "Bar chart of 2026 EV battery cost per kWh by chemistry (NMC vs LFP).";
}
