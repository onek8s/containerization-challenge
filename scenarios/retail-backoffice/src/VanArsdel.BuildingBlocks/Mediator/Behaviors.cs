using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using VanArsdel.BuildingBlocks.Exceptions;

namespace VanArsdel.BuildingBlocks.Mediator
{
    public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        private readonly IEnumerable<IValidator<TRequest>> _validators;

        public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
        {
            _validators = validators;
        }

        public async Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken, RequestHandlerDelegate<TResponse> next)
        {
            if (_validators.Any())
            {
                var context = new ValidationContext<TRequest>(request);
                var results = await Task.WhenAll(_validators.Select(v => v.ValidateAsync(context, cancellationToken)));
                var failures = results.SelectMany(r => r.Errors).Where(f => f != null).ToList();
                if (failures.Count > 0)
                {
                    throw new ValidationFailedException(failures
                        .GroupBy(f => f.PropertyName)
                        .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).ToArray()));
                }
            }
            return await next();
        }
    }

    public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
    {
        private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

        public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
        {
            _logger = logger;
        }

        public async Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken, RequestHandlerDelegate<TResponse> next)
        {
            var sw = Stopwatch.StartNew();
            var response = await next();
            sw.Stop();
            if (sw.ElapsedMilliseconds > 500)
            {
                _logger.LogWarning("Slow request {Request} took {Elapsed} ms", typeof(TRequest).Name, sw.ElapsedMilliseconds);
            }
            else
            {
                _logger.LogDebug("Handled {Request} in {Elapsed} ms", typeof(TRequest).Name, sw.ElapsedMilliseconds);
            }
            return response;
        }
    }
}
