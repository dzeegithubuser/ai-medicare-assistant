namespace Application.DTOs;

public class ChatSessionResponse
{
    public List<ChatSessionMessageDto> Messages { get; set; } = [];
    public ChatUiStateDto UiState { get; set; } = new();
}

public class ChatSessionMessageDto
{
    public string Role { get; set; } = "assistant";
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    /// <summary>Relative URL of the page where this message was created.</summary>
    public string? Context { get; set; }
}

public class ChatUiStateDto
{
    public bool EditMode { get; set; }
}

public class UpdateChatMessagesRequest
{
    public List<ChatSessionMessageDto> Messages { get; set; } = [];
}

public class UpdateChatUiStateRequest
{
    public bool EditMode { get; set; }
}
