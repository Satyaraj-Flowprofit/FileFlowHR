namespace FileFlowHR.Domain.Common;

/// <summary>A business rule was broken. The message is safe to show to the user.</summary>
public sealed class DomainException(string message) : Exception(message);
