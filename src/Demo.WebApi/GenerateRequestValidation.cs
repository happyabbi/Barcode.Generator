using System.Collections.Generic;

namespace Demo.WebApi;

public static class GenerateRequestValidation
{
    public const int DefaultDimension = 300;
    public const int MaxTextLength = 1024;
    public const int MinDimension = 64;
    public const int MaxDimension = 2048;

    public static IDictionary<string, string[]> Validate(string text, int? width, int? height)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(text))
        {
            errors["text"] = new[] { "text is required and cannot be empty." };
        }
        else if (text.Length > MaxTextLength)
        {
            errors["text"] = new[] { $"text length must be <= {MaxTextLength} characters." };
        }

        ValidateDimension("width", width, errors);
        ValidateDimension("height", height, errors);

        return errors;
    }

    public static int ResolveDimension(int? value)
    {
        return value ?? DefaultDimension;
    }

    private static void ValidateDimension(string name, int? value, IDictionary<string, string[]> errors)
    {
        if (!value.HasValue)
        {
            return;
        }

        if (value.Value < MinDimension || value.Value > MaxDimension)
        {
            errors[name] = new[] { $"{name} must be between {MinDimension} and {MaxDimension}." };
        }
    }
}
