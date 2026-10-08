namespace FileFlowHR.Web.Components.Shared;

public static class Formatting
{
    public static string Date(DateOnly? date) => date?.ToString("d MMM yyyy") ?? "";
    public static string Date(DateTime? date) => date?.ToLocalTime().ToString("d MMM yyyy, HH:mm") ?? "";
    public static string Period(DateOnly from, DateOnly? to) => to is null ? $"{Date(from)} onwards" : $"{Date(from)} to {Date(to)}";

    /// <summary>Display text for codes and enums: FullTime -> Full time, active -> Active.</summary>
    public static string Words(object value)
    {
        var text = value.ToString() ?? "";
        var chars = new List<char>();
        for (var i = 0; i < text.Length; i++)
        {
            if (i > 0 && char.IsUpper(text[i])) { chars.Add(' '); chars.Add(char.ToLowerInvariant(text[i])); }
            else chars.Add(text[i]);
        }
        if (chars.Count > 0) chars[0] = char.ToUpperInvariant(chars[0]);
        return new string(chars.ToArray());
    }
}
