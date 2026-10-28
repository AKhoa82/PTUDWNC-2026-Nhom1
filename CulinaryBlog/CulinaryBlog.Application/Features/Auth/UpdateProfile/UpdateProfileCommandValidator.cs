using FluentValidation;

namespace CulinaryBlog.Application.Features.Auth.UpdateProfile;

public sealed class UpdateProfileCommandValidator
    : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("Người dùng không hợp lệ.");

        RuleFor(x => x)
            .Must(x => x.HasFullName || x.HasAvatarUrl)
            .WithMessage("Cần cung cấp ít nhất một thông tin để cập nhật.");

        RuleFor(x => x.FullName)
            .Must(name =>
                !string.IsNullOrWhiteSpace(name)
                && name.Trim().Length >= 2
                && name.Trim().Length <= 100)
            .WithMessage("Họ tên phải có từ 2 đến 100 ký tự.")
            .When(x => x.HasFullName);

        RuleFor(x => x.AvatarUrl)
            .Must(IsValidAvatarUrl)
            .WithMessage("Ảnh đại diện phải là URL HTTP/HTTPS hợp lệ, tối đa 500 ký tự.")
            .When(x => x.HasAvatarUrl);
    }

    private static bool IsValidAvatarUrl(string? value)
    {
        // null có nghĩa là xóa ảnh đại diện.
        if (value is null)
            return true;

        var url = value.Trim();

        if (url.Length == 0 || url.Length > 500)
            return false;

        if (!Uri.TryCreate(
                url,
                UriKind.Absolute,
                out var uri))
        {
            return false;
        }

        return (uri.Scheme == Uri.UriSchemeHttp
                || uri.Scheme == Uri.UriSchemeHttps)
            && string.IsNullOrEmpty(uri.UserInfo)
            && uri.IsWellFormedOriginalString();
    }
}
