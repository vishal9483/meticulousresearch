namespace MeticulousResearch.Core.Export;

/// <summary>
/// The branded style set applied consistently across a rendered document (SPEC §3.4.2): a single
/// accent plus stable style names for headings, tables, captions, code, and lists so every block of
/// a kind is styled identically. The same instance is shared document-wide so styling is consistent.
/// </summary>
/// <param name="Accent">The resolved accent applied to the theme.</param>
public sealed record StyleSet(string Accent)
{
    /// <summary>The consistent heading style name.</summary>
    public const string HeadingStyle = "BrandedHeading";

    /// <summary>The consistent table style name.</summary>
    public const string TableStyle = "BrandedTable";

    /// <summary>The consistent caption style name.</summary>
    public const string CaptionStyle = "BrandedCaption";

    /// <summary>The consistent code-block style name.</summary>
    public const string CodeStyle = "BrandedCode";

    /// <summary>The consistent list style name.</summary>
    public const string ListStyle = "BrandedList";
}

/// <summary>A block of rendered document content within a <see cref="RenderedDocument"/>.</summary>
public abstract record DocumentBlock;

/// <summary>A heading block at a given level, carrying the branded heading style.</summary>
/// <param name="Level">The heading level (1-6).</param>
/// <param name="Text">The heading text.</param>
/// <param name="Style">The branded style name applied (<see cref="StyleSet.HeadingStyle"/>).</param>
public sealed record HeadingBlock(int Level, string Text, string Style) : DocumentBlock;

/// <summary>A paragraph of body text.</summary>
/// <param name="Text">The paragraph text.</param>
public sealed record ParagraphBlock(string Text) : DocumentBlock;

/// <summary>A bulleted list, carrying the branded list style.</summary>
/// <param name="Items">The list items in order.</param>
/// <param name="Style">The branded style name applied (<see cref="StyleSet.ListStyle"/>).</param>
public sealed record ListBlock(IReadOnlyList<string> Items, string Style) : DocumentBlock;

/// <summary>A table, carrying the branded table style. First row is the header.</summary>
/// <param name="Rows">The table rows (each a list of cells), header first.</param>
/// <param name="Style">The branded style name applied (<see cref="StyleSet.TableStyle"/>).</param>
public sealed record TableBlock(IReadOnlyList<IReadOnlyList<string>> Rows, string Style) : DocumentBlock;

/// <summary>A fenced code block, carrying the branded code style.</summary>
/// <param name="Language">The code language label (may be empty).</param>
/// <param name="Code">The code text.</param>
/// <param name="Style">The branded style name applied (<see cref="StyleSet.CodeStyle"/>).</param>
public sealed record CodeBlock(string Language, string Code, string Style) : DocumentBlock;

/// <summary>A figure/table caption, carrying the branded caption style.</summary>
/// <param name="Text">The caption text.</param>
/// <param name="Style">The branded style name applied (<see cref="StyleSet.CaptionStyle"/>).</param>
public sealed record CaptionBlock(string Text, string Style) : DocumentBlock;

/// <summary>
/// Raw Mermaid diagram source, present only in Markdown output (passthrough). For DOCX/PDF the tree
/// builder replaces it with an <see cref="ImageBlock"/> so no raw Mermaid appears in the deliverable.
/// </summary>
/// <param name="Source">The Mermaid diagram source.</param>
public sealed record MermaidBlock(string Source) : DocumentBlock;

/// <summary>
/// A rendered diagram image embedded in DOCX/PDF (SPEC §3.4.2): the Mermaid source has been rendered
/// to image bytes offline and deterministically, so the deliverable shows an image, not raw source.
/// </summary>
/// <param name="AltText">Alternative text describing the diagram.</param>
/// <param name="Image">The rendered image bytes.</param>
/// <param name="ImageFormat">The rendered image format label (e.g. <c>png</c>).</param>
/// <param name="SourceKind">The origin of the image (e.g. <c>mermaid</c>).</param>
public sealed record ImageBlock(string AltText, byte[] Image, string ImageFormat, string SourceKind) : DocumentBlock;

/// <summary>A cover page (SPEC §3.4.2): report title, subtitle, date, project, and firm logo.</summary>
/// <param name="Title">The report title.</param>
/// <param name="Subtitle">An optional subtitle.</param>
/// <param name="Date">The cover date (from the injected clock), formatted <c>yyyy-MM-dd</c>.</param>
/// <param name="Project">The owning project name, or null.</param>
/// <param name="LogoPath">The firm logo path placed on the cover, or null when unset.</param>
public sealed record CoverPage(string Title, string? Subtitle, string Date, string? Project, string? LogoPath);

/// <summary>One entry in an auto-generated table of contents.</summary>
/// <param name="Title">The heading text.</param>
/// <param name="Level">The heading level.</param>
/// <param name="PageNumber">The (deterministic) page number the heading falls on.</param>
public sealed record TocEntry(string Title, int Level, int PageNumber);

/// <summary>An auto-generated table of contents built from the document's headings (SPEC §3.4.2).</summary>
/// <param name="Entries">The TOC entries in document order, each with a page number.</param>
public sealed record TableOfContents(IReadOnlyList<TocEntry> Entries);

/// <summary>
/// The running header/footer chrome carried on every page (SPEC §3.4.2): the report title, a page
/// number, and the confidentiality notice. A minimal chrome (internal draft) shows only a page
/// number.
/// </summary>
/// <param name="Title">The report title shown in the running header, or null when minimal.</param>
/// <param name="ShowsPageNumber">Whether every page shows a page number.</param>
/// <param name="Confidentiality">The confidentiality notice shown on every page, or null when minimal.</param>
public sealed record RunningChrome(string? Title, bool ShowsPageNumber, string? Confidentiality)
{
    /// <summary>Whether this is the full chrome (title + page number + confidentiality).</summary>
    public bool IsFull => !string.IsNullOrEmpty(Title) && ShowsPageNumber && !string.IsNullOrEmpty(Confidentiality);
}

/// <summary>The sources / methodology section appended to a client-ready deliverable (SPEC §3.4.2).</summary>
/// <param name="Title">The section title (e.g. <c>Sources &amp; Methodology</c>).</param>
/// <param name="Sources">The cited sources listed in the section.</param>
public sealed record SourcesSection(string Title, IReadOnlyList<string> Sources);

/// <summary>The type of a workbook cell for XLSX export (SPEC §3.4.2).</summary>
public enum WorkbookCellType
{
    /// <summary>A text cell.</summary>
    Text,

    /// <summary>A numeric cell.</summary>
    Number,

    /// <summary>A date cell.</summary>
    Date,

    /// <summary>A formula cell (its content is a formula, not a static value).</summary>
    Formula,
}

/// <summary>One workbook cell with its raw content and its declared type.</summary>
/// <param name="Raw">The raw cell text (a formula still begins with <c>=</c>).</param>
/// <param name="Type">The cell's declared type.</param>
public sealed record WorkbookCell(string Raw, WorkbookCellType Type);

/// <summary>One workbook column with its header name and inferred type.</summary>
/// <param name="Name">The column header.</param>
/// <param name="Type">The column's declared type (text, number, or date).</param>
public sealed record WorkbookColumn(string Name, WorkbookCellType Type);

/// <summary>
/// A tabular workbook produced for XLSX export (SPEC §3.4.2): typed columns and typed cells, with
/// formula cells preserving their formula rather than a static value.
/// </summary>
/// <param name="Columns">The typed columns in order.</param>
/// <param name="Rows">The data rows (each a list of typed cells), excluding the header.</param>
public sealed record Workbook(IReadOnlyList<WorkbookColumn> Columns, IReadOnlyList<IReadOnlyList<WorkbookCell>> Rows);

/// <summary>
/// The in-memory rendered branded document (SPEC §3.4.2): the deterministic intermediate tree the
/// preview shows and the format writers serialize. Holds the shared cover/TOC/chrome/styles plus the
/// content blocks (or the <see cref="Workbook"/> for XLSX). This tree is produced once from the
/// source and is identical for two runs on identical input with a fixed clock.
/// </summary>
public sealed record RenderedDocument
{
    /// <summary>The format this document was rendered for.</summary>
    public required ExportFormat Format { get; init; }

    /// <summary>The preset controlling how much chrome is present.</summary>
    public required ExportPreset Preset { get; init; }

    /// <summary>The resolved accent applied to the theme.</summary>
    public required string Accent { get; init; }

    /// <summary>The shared branded style set applied consistently to all styled blocks.</summary>
    public required StyleSet Styles { get; init; }

    /// <summary>The cover page, or null when the preset carries no cover.</summary>
    public CoverPage? Cover { get; init; }

    /// <summary>The auto TOC, or null when the preset carries no TOC.</summary>
    public TableOfContents? Toc { get; init; }

    /// <summary>The running header/footer chrome, or null when the preset carries no chrome.</summary>
    public RunningChrome? Chrome { get; init; }

    /// <summary>The sources/methodology section, or null when the preset omits it.</summary>
    public SourcesSection? Sources { get; init; }

    /// <summary>The content blocks in document order (empty for XLSX).</summary>
    public IReadOnlyList<DocumentBlock> Blocks { get; init; } = Array.Empty<DocumentBlock>();

    /// <summary>The workbook for XLSX exports, or null for other formats.</summary>
    public Workbook? Workbook { get; init; }

    /// <summary>The deterministic serialized bytes of the deliverable file.</summary>
    public required byte[] Bytes { get; init; }

    /// <summary>The Markdown text for MD exports, or null for other formats.</summary>
    public string? Markdown { get; init; }
}
