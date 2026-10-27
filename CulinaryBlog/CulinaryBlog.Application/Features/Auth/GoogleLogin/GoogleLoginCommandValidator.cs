using FluentValidation;

namespace CulinaryBlog.Application.Features.Auth.GoogleLogin;

public sealed class GoogleLoginCommandValidator : AbstractValidator<GoogleLoginCommand>
{
    public GoogleLoginCommandValidator()
    {
        RuleFor(command => command.IdToken)
            .Must(token => !string.IsNullOrWhiteSpace(token))
            .WithMessage("Google ID token is required.");
    }
}
