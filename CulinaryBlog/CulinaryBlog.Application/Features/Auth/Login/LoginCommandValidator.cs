using FluentValidation;

namespace CulinaryBlog.Application.Features.Auth.Login;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(command => command.Request.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(command => command.Request.Password).NotEmpty().MinimumLength(8).MaximumLength(128);
    }
}