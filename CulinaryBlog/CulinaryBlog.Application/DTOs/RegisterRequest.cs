using System.ComponentModel.DataAnnotations;

namespace CulinaryBlog.Application.DTOs;

public class RegisterRequest : IValidatableObject
{
    [Required]
    [RegularExpression("^[a-zA-Z0-9._-]+$", ErrorMessage = "Tên định danh chỉ được chứa chữ cái, số, dấu chấm, gạch dưới hoặc gạch ngang.")]
    [MinLength(3)]
    [MaxLength(30)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(320)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    [MaxLength(128)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [Compare(nameof(Password), ErrorMessage = "Mật khẩu xác nhận không khớp.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required]
    [MinLength(2)]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    public bool TermsAccepted { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!TermsAccepted)
        {
            yield return new ValidationResult(
                "Bạn cần đồng ý với Quy chuẩn biên soạn và Điều khoản bảo vệ tác quyền.",
                new[] { nameof(TermsAccepted) });
        }
    }
}