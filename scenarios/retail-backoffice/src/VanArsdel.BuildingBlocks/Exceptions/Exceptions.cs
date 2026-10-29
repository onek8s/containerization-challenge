using System;
using System.Collections.Generic;

namespace VanArsdel.BuildingBlocks.Exceptions
{
    public class NotFoundException : Exception
    {
        public NotFoundException(string entity, object key)
            : base($"{entity} '{key}' was not found.")
        {
            Entity = entity;
            Key = key?.ToString();
        }

        public string Entity { get; }
        public string Key { get; }
    }

    public class ConflictException : Exception
    {
        public ConflictException(string message) : base(message) { }
    }

    public class DomainException : Exception
    {
        public DomainException(string message) : base(message) { }
    }

    public class UpstreamException : Exception
    {
        public UpstreamException(string upstream, string message, Exception inner = null)
            : base($"{upstream}: {message}", inner)
        {
            Upstream = upstream;
        }

        public string Upstream { get; }
    }

    public class ValidationFailedException : Exception
    {
        public ValidationFailedException(IDictionary<string, string[]> errors)
            : base("One or more validation errors occurred.")
        {
            Errors = errors;
        }

        public IDictionary<string, string[]> Errors { get; }
    }
}
