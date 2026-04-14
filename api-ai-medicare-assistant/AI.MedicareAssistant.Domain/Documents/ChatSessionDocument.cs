using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Domain.Documents;

public class ChatSessionDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    public Guid UserId { get; set; }

    public List<ChatMessageDoc> Messages { get; set; } = [];

    public ChatUiStateDoc UiState { get; set; } = new();

    public List<ChatSessionArchiveDoc> Archives { get; set; } = [];

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class ChatSessionArchiveDoc
{
    public DateTime ArchivedAt { get; set; } = DateTime.UtcNow;
    public List<ChatMessageDoc> Messages { get; set; } = [];
    public ChatUiStateDoc UiState { get; set; } = new();
}

public class ChatMessageDoc
{
    public string Role { get; set; } = "assistant";
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    /// <summary>Relative URL of the page where this message was created.</summary>
    public string? Context { get; set; }
}

public class ChatUiStateDoc
{
    public bool EditMode { get; set; }
}
