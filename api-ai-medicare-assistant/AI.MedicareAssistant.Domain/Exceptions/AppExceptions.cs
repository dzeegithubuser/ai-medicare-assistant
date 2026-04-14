namespace Domain.Exceptions;

/// <summary>
/// Base exception for all application-level exceptions.
/// </summary>
public abstract class AppException : Exception
{
    public int StatusCode { get; }

    protected AppException(string message, int statusCode = 500, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
    }
}

/// <summary>
/// Thrown when a requested resource is not found (HTTP 404).
/// </summary>
public class NotFoundException : AppException
{
    public NotFoundException(string entity, object key)
        : base($"{entity} with identifier '{key}' was not found.", 404) { }
}

/// <summary>
/// Thrown when a business rule or validation fails (HTTP 400).
/// </summary>
public class ValidationException : AppException
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException(string message)
        : base(message, 400)
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationException(IDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.", 400)
    {
        Errors = errors;
    }
}

/// <summary>
/// Thrown when a user performs an unauthorized action (HTTP 401).
/// </summary>
public class UnauthorizedException : AppException
{
    public UnauthorizedException(string message = "Unauthorized.")
        : base(message, 401) { }
}

/// <summary>
/// Thrown when a conflict occurs (e.g. duplicate email) (HTTP 409).
/// </summary>
public class ConflictException : AppException
{
    public ConflictException(string message)
        : base(message, 409) { }
}
