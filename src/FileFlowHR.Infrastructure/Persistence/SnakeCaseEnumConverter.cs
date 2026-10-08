using System.Text;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FileFlowHR.Infrastructure.Persistence;

/// <summary>Stores enums as lowercase codes (FullTime -> full_time), matching the agreed schema.</summary>
public sealed class SnakeCaseEnumConverter<TEnum>() : ValueConverter<TEnum, string>(
    v => ToCode(v), v => FromCode(v)) where TEnum : struct, Enum
{
    private static readonly Dictionary<TEnum, string> ToCodes =
        Enum.GetValues<TEnum>().ToDictionary(v => v, v => Snake(v.ToString()));
    private static readonly Dictionary<string, TEnum> FromCodes = ToCodes.ToDictionary(kv => kv.Value, kv => kv.Key);

    private static string ToCode(TEnum value) => ToCodes[value];
    private static TEnum FromCode(string code) => FromCodes[code];

    public static string Snake(string name)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]) && i > 0) sb.Append('_');
            sb.Append(char.ToLowerInvariant(name[i]));
        }
        return sb.ToString();
    }

    public static IEnumerable<string> Codes => ToCodes.Values;
}
