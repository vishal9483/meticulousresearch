using System.Text;

namespace MeticulousResearch.Core.Resources.Extraction;

/// <summary>
/// Extracts a CSV file into a readable tabular representation (SPEC §3.2): each record's fields are
/// joined with <c>" | "</c> so the header and rows read as a table and index well in full-text
/// search. Handles simple quoted fields (doubled quotes, embedded commas/newlines).
/// </summary>
public sealed class CsvTextExtractor : ITextExtractor
{
    /// <summary>The column separator used in the readable tabular output.</summary>
    public const string ColumnSeparator = " | ";

    /// <inheritdoc />
    public bool CanHandle(string extension) => string.Equals(extension, "csv", StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public ExtractedContent Extract(string filePath)
    {
        var raw = File.ReadAllText(filePath, Encoding.UTF8);
        var records = ParseCsv(raw);

        var lines = records
            .Where(fields => fields.Count > 0 && !(fields.Count == 1 && fields[0].Length == 0))
            .Select(fields => string.Join(ColumnSeparator, fields));

        return new ExtractedContent(string.Join('\n', lines));
    }

    private static List<List<string>> ParseCsv(string text)
    {
        var records = new List<List<string>>();
        var current = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                current.Add(field.ToString().Trim());
                field.Clear();
            }
            else if (c == '\n' || c == '\r')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    i++;
                current.Add(field.ToString().Trim());
                field.Clear();
                records.Add(current);
                current = new List<string>();
            }
            else
            {
                field.Append(c);
            }
        }

        if (field.Length > 0 || current.Count > 0)
        {
            current.Add(field.ToString().Trim());
            records.Add(current);
        }

        return records;
    }
}
