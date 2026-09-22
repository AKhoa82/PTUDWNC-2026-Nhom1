using FluentValidation;
using MediatR;

namespace CulinaryBlog.Application.Common.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators) => _validators = validators;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var context = new ValidationContext<TRequest>(request);
        var failures = (await Task.WhenAll(_validators.Select(validator => validator.ValidateAsync(context, cancellationToken))))
            .SelectMany(result => result.Errors).Where(error => error is not null).ToList();
        if (failures.Count > 0) throw new ValidationException(failures);
        return await next();
    }
}