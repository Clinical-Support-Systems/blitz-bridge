namespace BlitzBridge.McpServer.Services;

/// <summary>
/// Structured exception raised for progressive-disclosure request failures.
/// </summary>
public sealed class ProgressiveDisclosureException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProgressiveDisclosureException"/> class.
    /// </summary>
    public ProgressiveDisclosureException()
        : this("progressive_disclosure_error", "Progressive disclosure request failed.", 500)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProgressiveDisclosureException"/> class.
    /// </summary>
    /// <param name="message">Human-readable error message.</param>
    public ProgressiveDisclosureException(string message)
        : this("progressive_disclosure_error", message, 500)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProgressiveDisclosureException"/> class.
    /// </summary>
    /// <param name="message">Human-readable error message.</param>
    /// <param name="innerException">Inner exception.</param>
    public ProgressiveDisclosureException(string message, Exception innerException)
        : this("progressive_disclosure_error", message, 500, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProgressiveDisclosureException"/> class.
    /// </summary>
    /// <param name="errorCode">Stable error code.</param>
    /// <param name="message">Human-readable error message.</param>
    /// <param name="statusCode">Suggested transport status code.</param>
    /// <param name="innerException">Optional inner exception.</param>
    public ProgressiveDisclosureException(
        string errorCode,
        string message,
        int statusCode,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
    }

    /// <summary>
    /// Stable machine-readable error code.
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    /// Suggested transport status code.
    /// </summary>
    public int StatusCode { get; }
}
