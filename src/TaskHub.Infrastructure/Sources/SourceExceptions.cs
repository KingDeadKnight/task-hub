namespace TaskHub.Infrastructure.Sources;

/// <summary>Thrown when a source is queried while its required settings are missing.</summary>
public sealed class SourceNotConfiguredException : Exception
{
    public SourceNotConfiguredException()
    {
    }

    public SourceNotConfiguredException(string message)
        : base(message)
    {
    }

    public SourceNotConfiguredException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Thrown when a remote source rejects or fails a request. Messages never contain secrets or payloads.</summary>
public sealed class SourceRequestException : Exception
{
    public SourceRequestException()
    {
    }

    public SourceRequestException(string message)
        : base(message)
    {
    }

    public SourceRequestException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
