using MeticulousResearch.Core.Data;

namespace MeticulousResearch.Core.Conversations;

/// <summary>
/// Persists a message's image attachments (image-attachments, SPEC §3.2.1) as <em>message
/// content</em> — bytes stored under the message's own on-disk folder — deliberately separate from
/// project <c>Resource</c> storage. Attaching an image to a turn therefore never creates a project
/// resource; the bytes travel with the message so a re-opened turn still shows the thumbnail and a
/// retry can re-send it.
/// </summary>
public interface IMessageAttachmentStore
{
    /// <summary>Stores <paramref name="attachments"/> as content of message <paramref name="messageId"/>.</summary>
    /// <param name="projectId">The owning project id (for the on-disk layout).</param>
    /// <param name="messageId">The message the attachments belong to.</param>
    /// <param name="attachments">The image attachments to persist (may be empty).</param>
    void Save(string projectId, string messageId, IReadOnlyList<ImageAttachment> attachments);

    /// <summary>Returns the image attachments stored as content of message <paramref name="messageId"/>.</summary>
    /// <param name="projectId">The owning project id.</param>
    /// <param name="messageId">The message whose attachments to load.</param>
    IReadOnlyList<ImageAttachment> Get(string projectId, string messageId);
}

/// <summary>
/// Filesystem-backed <see cref="IMessageAttachmentStore"/>. Stores each attachment under
/// <c>projects/{projectId}/messages/{messageId}/</c> as its raw bytes plus a small sidecar manifest,
/// keeping message-scoped image content wholly separate from the project resource store.
/// </summary>
public sealed class MessageAttachmentStore : IMessageAttachmentStore
{
    private const string ManifestFileName = "attachments.tsv";

    private readonly IProjectFileStore _files;

    /// <summary>Creates the store over the project file layout.</summary>
    public MessageAttachmentStore(IProjectFileStore files)
        => _files = files ?? throw new ArgumentNullException(nameof(files));

    /// <inheritdoc />
    public void Save(string projectId, string messageId, IReadOnlyList<ImageAttachment> attachments)
    {
        ArgumentNullException.ThrowIfNull(attachments);
        if (attachments.Count == 0)
            return;

        var dir = MessageDirectory(projectId, messageId);
        Directory.CreateDirectory(dir);

        var manifest = new List<string>(attachments.Count);
        var index = 0;
        foreach (var attachment in attachments)
        {
            var blobName = $"{index:D4}_{Sanitize(attachment.FileName)}";
            File.WriteAllBytes(Path.Combine(dir, blobName), attachment.Bytes);
            manifest.Add(string.Join('\t',
                attachment.Id,
                attachment.FileName,
                attachment.MediaType,
                blobName,
                attachment.WidthPixels.ToString(System.Globalization.CultureInfo.InvariantCulture),
                attachment.HeightPixels.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            index++;
        }

        File.WriteAllLines(Path.Combine(dir, ManifestFileName), manifest);
    }

    /// <inheritdoc />
    public IReadOnlyList<ImageAttachment> Get(string projectId, string messageId)
    {
        var dir = MessageDirectory(projectId, messageId);
        var manifestPath = Path.Combine(dir, ManifestFileName);
        if (!File.Exists(manifestPath))
            return Array.Empty<ImageAttachment>();

        var result = new List<ImageAttachment>();
        foreach (var line in File.ReadAllLines(manifestPath))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            var parts = line.Split('\t');
            if (parts.Length < 6)
                continue;

            var bytes = File.ReadAllBytes(Path.Combine(dir, parts[3]));
            result.Add(new ImageAttachment(
                parts[0],
                parts[1],
                parts[2],
                bytes,
                int.Parse(parts[4], System.Globalization.CultureInfo.InvariantCulture),
                int.Parse(parts[5], System.Globalization.CultureInfo.InvariantCulture)));
        }

        return result;
    }

    private string MessageDirectory(string projectId, string messageId)
    {
        if (string.IsNullOrWhiteSpace(messageId))
            throw new ArgumentException("A message id is required.", nameof(messageId));
        return Path.Combine(_files.GetProjectDirectory(projectId), "messages", messageId);
    }

    private static string Sanitize(string fileName)
    {
        var name = Path.GetFileName(fileName);
        foreach (var invalid in Path.GetInvalidFileNameChars())
            name = name.Replace(invalid, '_');
        return string.IsNullOrWhiteSpace(name) ? "image" : name;
    }
}
