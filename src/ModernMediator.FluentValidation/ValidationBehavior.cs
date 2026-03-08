using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;

namespace ModernMediator.FluentValidation
{
    /// <summary>
    /// Pipeline behavior that runs all registered FluentValidation validators
    /// for the request before passing it to the next handler in the pipeline.
    /// If any validator reports failures, a <see cref="ModernValidationException"/> is thrown.
    /// </summary>
    /// <typeparam name="TRequest">The request type. Must be a reference type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : class, IRequest<TResponse>
    {
        private readonly IEnumerable<IValidator<TRequest>> _validators;

        /// <summary>
        /// Initializes a new instance of <see cref="ValidationBehavior{TRequest, TResponse}"/>.
        /// </summary>
        /// <param name="validators">The validators registered for this request type.</param>
        public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
        {
            _validators = validators;
        }

        /// <summary>
        /// Validates the request using all registered validators.
        /// If validation passes, invokes the next delegate in the pipeline.
        /// If validation fails, throws <see cref="ModernValidationException"/>.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <param name="next">The next delegate in the pipeline.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The response from the next handler.</returns>
        /// <exception cref="ModernValidationException">Thrown when one or more validators report failures.</exception>
        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TRequest, TResponse> next, CancellationToken cancellationToken)
        {
            var validators = _validators.ToList();
            if (validators.Count == 0)
                return await next(request, cancellationToken);

            var results = await Task.WhenAll(
                validators.Select(v => v.ValidateAsync(new ValidationContext<TRequest>(request), cancellationToken)));

            var failures = results
                .SelectMany(r => r.Errors)
                .Where(f => f != null)
                .ToList();

            if (failures.Count > 0)
                throw new ModernValidationException(failures);

            return await next(request, cancellationToken);
        }
    }
}
