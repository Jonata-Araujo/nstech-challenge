using System.Text.RegularExpressions;

namespace OrderService.Domain.Common;

public sealed partial record Currency
{
    private Currency(string code) => Code = code;

    public string Code { get; }

    public static Result<Currency> Create(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return CurrencyErrors.Required;

        var normalized = code.Trim().ToUpperInvariant();

        return IsoCodePattern().IsMatch(normalized)
            ? new Currency(normalized)
            : CurrencyErrors.Invalid(code);
    }

    public static Currency FromCode(string code)
    {
        var result = Create(code);
        return result.IsSuccess
            ? result.Value
            : throw new ArgumentException(result.Error!.Message, nameof(code));
    }

    public override string ToString() => Code;

    [GeneratedRegex("^[A-Z]{3}$")]
    private static partial Regex IsoCodePattern();
}

public static class CurrencyErrors
{
    public static readonly Error Required =
        Error.Validation("currency.required", "Currency is required.");

    public static Error Invalid(string code) =>
        Error.Validation("currency.invalid", $"'{code}' is not a valid ISO 4217 currency code.");
}
