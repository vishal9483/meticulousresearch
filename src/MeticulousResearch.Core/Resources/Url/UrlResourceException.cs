namespace MeticulousResearch.Core.Resources.Url;

/// <summary>
/// Raised when a URL resource cannot be created because the page could not be fetched or contained
/// no readable content (SPEC §3.2, §3.7). The <see cref="System.Exception.Message"/> is a
/// human-readable, actionable error safe to surface inline; no resource is created when this throws.
/// </summary>
public sealed class UrlResourceException : Exception
{
    /// <summary>Creates the exception with a human-readable message.</summary>
    public UrlResourceException(string message) : base(message)
    {
    }
}
