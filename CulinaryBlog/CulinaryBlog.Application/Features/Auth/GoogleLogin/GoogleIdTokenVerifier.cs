using Google.Apis.Auth;

namespace CulinaryBlog.Application.Features.Auth.GoogleLogin;

public sealed class GoogleIdTokenVerifier : IGoogleIdTokenVerifier
{
    public async Task<GoogleIdTokenProfile> VerifyAsync(
        string idToken, string clientId, CancellationToken cancellationToken)
    {
        var settings = new GoogleJsonWebSignature.ValidationSettings
        {
            Audience = [clientId]
        };

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings)
                .WaitAsync(cancellationToken);
        }
        catch (InvalidJwtException ex)
        {
            throw new UnauthorizedAccessException("Google ID token is invalid or expired.", ex);
        }
        catch (FormatException ex)
        {
            throw new UnauthorizedAccessException("Google ID token is malformed.", ex);
        }
        catch (Newtonsoft.Json.JsonException ex)
        {
            throw new UnauthorizedAccessException("Google ID token is malformed.", ex);
        }

        return new GoogleIdTokenProfile(
            payload.Subject,
            payload.Email,
            payload.EmailVerified,
            payload.Name,
            payload.Picture);
    }
}
