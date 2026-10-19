using CulinaryBlog.Application.Common.Exceptions;
using CulinaryBlog.Application.Features.Auth.Login;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using CulinaryBlog.Application.Features.Recipes.Images;
using CulinaryBlog.Domain.Exceptions;
namespace CulinaryBlog.API.Infrastructure;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Exception occurred: {Message}", exception.Message);

        var problemDetails = new ProblemDetails
        {
            Type = "about:blank",
            Instance = httpContext.Request.Path
        };

        if (exception is ValidationException validationException)
        {
            var errors = validationException.Errors
                .GroupBy(x => x.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(x => x.ErrorMessage).ToArray());

            var validationProblemDetails = new HttpValidationProblemDetails(errors)
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Validation Failed",
                Type = "https://tools.ietf.org/html/rfc4918#section-11.2",
                Instance = httpContext.Request.Path
            };

            httpContext.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            await httpContext.Response.WriteAsJsonAsync(
                validationProblemDetails,
                options: null,
                contentType: "application/problem+json",
                cancellationToken: cancellationToken);
            return true;
        }
        else if (exception is RecipeImageValidationException imageValidationException)
        {
            problemDetails.Status = StatusCodes.Status422UnprocessableEntity;
            problemDetails.Title = "Invalid Image Data";
            problemDetails.Detail = imageValidationException.Message;
            httpContext.Response.StatusCode = problemDetails.Status.Value;
            await httpContext.Response.WriteAsJsonAsync(
                problemDetails,
                options: null,
                contentType: "application/problem+json",
                cancellationToken: cancellationToken);
            return true;
        }



        if (exception is ConflictException conflictException)
        {
            problemDetails.Status = StatusCodes.Status409Conflict;
            problemDetails.Title = "Conflict";
            problemDetails.Detail = conflictException.Message;
        }
        else if (exception is NotFoundException notFoundException)
        {
            problemDetails.Status = StatusCodes.Status404NotFound;
            problemDetails.Title = "Not Found";
            problemDetails.Detail = notFoundException.Message;
        }
        else if (exception is ForbiddenException forbiddenException)
        {
            problemDetails.Status = StatusCodes.Status403Forbidden;
            problemDetails.Title = "Forbidden";
            problemDetails.Detail = forbiddenException.Message;
        }
        else if (exception is UnauthorizedAccessException unauthorizedAccessException)
        {
            problemDetails.Status = StatusCodes.Status401Unauthorized;
            problemDetails.Title = "Unauthorized";
            problemDetails.Detail = "Bạn không có quyền thực hiện hành động này hoặc thông tin đăng nhập không chính xác.";
        }
        else if (exception is AccountLockedException accountLockedException)
        {
            problemDetails.Status = StatusCodes.Status423Locked;
            problemDetails.Title = "Locked";
            problemDetails.Detail = "Tài khoản của bạn đã bị khóa tạm thời.";
        }
        else if (exception is DomainException domainException)
        {
            problemDetails.Status = StatusCodes.Status422UnprocessableEntity;
            problemDetails.Title = "Business Rule Violation";
            problemDetails.Detail = domainException.Message;
        }
        else if (exception is DbUpdateConcurrencyException dbUpdateConcurrencyException)
        {
            problemDetails.Status = StatusCodes.Status409Conflict;
            problemDetails.Title = "Concurrency Conflict";
            problemDetails.Detail = "Dữ liệu đã bị thay đổi bởi người khác, vui lòng tải lại trang và thử lại.";
        }
        else if (exception is DbUpdateException dbUpdateException && dbUpdateException.InnerException is PostgresException { SqlState: "23505" })
        {
            problemDetails.Status = StatusCodes.Status409Conflict;
            problemDetails.Title = "Duplicate Data";
            problemDetails.Detail = "Dữ liệu bị trùng lặp.";
        }
        else if (exception is PostgresException postgresException && postgresException.SqlState == "23505")
        {
            problemDetails.Status = StatusCodes.Status409Conflict;
            problemDetails.Title = "Duplicate Data";
            problemDetails.Detail = "Dữ liệu bị trùng lặp.";
        }
        else
        {
            problemDetails.Status = StatusCodes.Status500InternalServerError;
            problemDetails.Title = "Internal Server Error";
            problemDetails.Detail = "Đã xảy ra lỗi hệ thống không mong muốn.";
        }

        httpContext.Response.StatusCode = problemDetails.Status.Value;
        await httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken);

        return true;
    }
}
