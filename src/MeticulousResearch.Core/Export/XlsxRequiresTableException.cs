namespace MeticulousResearch.Core.Export;

/// <summary>
/// Thrown when a non-tabular artifact is exported to XLSX (SPEC §3.4.2): only a table/dataset
/// artifact can produce a workbook. No file is produced when this is thrown.
/// </summary>
public sealed class XlsxRequiresTableException : InvalidOperationException
{
    /// <summary>The user-facing message shown when XLSX export is attempted on non-tabular content.</summary>
    public const string DefaultMessage = "XLSX export requires a table/dataset artifact.";

    /// <summary>Creates the exception with the default message.</summary>
    public XlsxRequiresTableException() : base(DefaultMessage)
    {
    }
}
