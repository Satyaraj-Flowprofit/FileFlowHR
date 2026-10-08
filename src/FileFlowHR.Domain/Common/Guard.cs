namespace FileFlowHR.Domain.Common;

internal static class Guard
{
    public static string Required(string? value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException($"{name} is required.");
        value = value.Trim();
        if (value.Length > maxLength)
            throw new DomainException($"{name} must be {maxLength} characters or fewer.");
        return value;
    }

    public static string? Optional(string? value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        if (value.Length > maxLength)
            throw new DomainException($"{name} must be {maxLength} characters or fewer.");
        return value;
    }

    public static string CountryCode(string? value)
    {
        var code = Required(value, "Country code", 2).ToUpperInvariant();
        if (code.Length != 2 || !code.All(char.IsLetter))
            throw new DomainException("Country code must be two letters, for example US or IN.");
        return code;
    }
}
