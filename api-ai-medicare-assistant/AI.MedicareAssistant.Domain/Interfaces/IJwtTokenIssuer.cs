using Domain.Documents;

namespace Domain.Interfaces;

/// <summary>
/// Single point of truth for JWT issuance. Used for normal sign-in tokens, post
/// password-change reissues, and impersonation tokens (when <paramref name="actingAs"/> is set).
/// </summary>
public interface IJwtTokenIssuer
{
    /// <param name="user">The principal the token represents (the impersonated user during impersonation).</param>
    /// <param name="actingAs">Optional FP user id when this token is issued as part of impersonation.</param>
    /// <param name="ttl">Optional override of the configured token lifetime.</param>
    (string Token, DateTime ExpiresAt) Issue(UserDocument user, Guid? actingAs = null, TimeSpan? ttl = null);
}
