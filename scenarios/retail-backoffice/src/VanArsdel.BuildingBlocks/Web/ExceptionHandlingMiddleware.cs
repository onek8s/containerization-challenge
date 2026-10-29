using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using VanArsdel.BuildingBlocks.Exceptions;

namespace VanArsdel.BuildingBlocks.Web
{
    /// <summary>Maps domain exceptions to RFC 7807 problem details.</summary>
    public class ExceptionHandlingMiddleware
    {
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore,
        };

        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex) when (!context.Response.HasStarted)
            {
                var problem = Map(ex, context);
                if (problem.Status >= 500)
                {
                    _logger.LogError(ex, "Unhandled error on {Method} {Path}", context.Request.Method, context.Request.Path);
                }
                else
                {
                    _logger.LogInformation("{Method} {Path} -> {Status}: {Message}", context.Request.Method, context.Request.Path, problem.Status, ex.Message);
                }

                context.Response.Clear();
                context.Response.StatusCode = problem.Status ?? 500;
                context.Response.ContentType = "application/problem+json";
                await context.Response.WriteAsync(JsonConvert.SerializeObject(problem, JsonSettings));
            }
        }

        private static ProblemDetails Map(Exception ex, HttpContext context)
        {
            ProblemDetails problem;
            switch (ex)
            {
                case ValidationFailedException v:
                    problem = new ValidationProblemDetails(v.Errors) { Status = 400, Title = v.Message };
                    break;
                case FluentValidation.ValidationException fv:
                    var errors = new System.Collections.Generic.Dictionary<string, string[]>();
                    foreach (var e in fv.Errors)
                    {
                        errors[e.PropertyName] = new[] { e.ErrorMessage };
                    }
                    problem = new ValidationProblemDetails(errors) { Status = 400, Title = "Validation failed" };
                    break;
                case NotFoundException nf:
                    problem = new ProblemDetails { Status = 404, Title = "Not found", Detail = nf.Message };
                    break;
                case ConflictException c:
                    problem = new ProblemDetails { Status = 409, Title = "Conflict", Detail = c.Message };
                    break;
                case DomainException d:
                    problem = new ProblemDetails { Status = 422, Title = "Request cannot be processed", Detail = d.Message };
                    break;
                case UpstreamException u:
                    problem = new ProblemDetails { Status = 502, Title = "Upstream system error", Detail = u.Message };
                    break;
                default:
                    problem = new ProblemDetails { Status = 500, Title = "An unexpected error occurred" };
                    break;
            }
            problem.Instance = context.Request.Path;
            problem.Extensions["traceId"] = context.TraceIdentifier;
            return problem;
        }
    }
}
