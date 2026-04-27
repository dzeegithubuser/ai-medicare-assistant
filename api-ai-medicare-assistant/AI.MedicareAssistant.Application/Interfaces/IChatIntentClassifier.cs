using Application.DTOs;

namespace Application.Interfaces;

public interface IChatIntentClassifier
{
    Task<ChatIntentResponse> ClassifyAsync(ChatIntentRequest request, CancellationToken cancellationToken = default);
}
