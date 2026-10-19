namespace CulinaryBlog.Application.Features.Auth.GoogleLogin;

public interface IGoogleIdTokenVerifier
{
    Task<GoogleIdTokenProfile> VerifyAsync(string idToken, string clientId, CancellationToken cancellationToken);
}

public sealed record GoogleIdTokenProfile(
    string? Subject,
    string? Email,
    bool EmailVerified,
    string? Name,
    string? Picture);
