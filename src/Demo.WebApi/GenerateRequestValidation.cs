using System;
using System.Collections.Generic;
using System.Linq;
using Barcode.Generator;

namespace Demo.WebApi;

public static class GenerateRequestValidation
{
    public const int DefaultDimension = 300;
    public const int MaxTextLength = 1024;
    public const int MinDimension = 64;
    public const int MaxDimension = 2048;

    public static readonly BarcodeFormat DefaultFormat = BarcodeFormat.QR_CODE;

    public static readonly BarcodeFormat[] SupportedFormats =
    [
        BarcodeFormat.QR_CODE,
        BarcodeFormat.CODE_128,
        BarcodeFormat.CODE_39,
        BarcodeFormat.EAN_13,
        BarcodeFormat.EAN_8,
        BarcodeFormat.ITF,
        BarcodeFormat.UPC_A,
        BarcodeFormat.PDF_417,
        BarcodeFormat.DATA_MATRIX
    ];

    public static IDictionary<string, string[]> Validate(string text, int? width, int? height, string? format)
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
        ValidateFormat(format, errors);

        if (!errors.ContainsKey("format") && !errors.ContainsKey("text"))
        {
            ValidateTextForFormat(text, ResolveFormat(format), errors);
        }

        return errors;
    }

    public static int ResolveDimension(int? value)
    {
        return value ?? DefaultDimension;
    }

    public static BarcodeFormat ResolveFormat(string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return DefaultFormat;
        }

        return Enum.Parse<BarcodeFormat>(format, ignoreCase: true);
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

    private static void ValidateFormat(string? format, IDictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return;
        }

        var isParsed = Enum.TryParse<BarcodeFormat>(format, ignoreCase: true, out var parsedFormat);
        if (!isParsed || !SupportedFormats.Contains(parsedFormat))
        {
            errors["format"] =
            [
                $"format must be one of: {string.Join(", ", SupportedFormats.Select(x => x.ToString()))}."
            ];
        }
    }

    private static void ValidateTextForFormat(string text, BarcodeFormat format, IDictionary<string, string[]> errors)
    {
        var isDigitsOnly = text.All(char.IsDigit);

        switch (format)
        {
            case BarcodeFormat.EAN_13:
                if (!isDigitsOnly || (text.Length != 12 && text.Length != 13))
                {
                    errors["text"] = ["EAN-13 requires 12 or 13 numeric digits."];
                }
                break;

            case BarcodeFormat.EAN_8:
                if (!isDigitsOnly || (text.Length != 7 && text.Length != 8))
                {
                    errors["text"] = ["EAN-8 requires 7 or 8 numeric digits."];
                }
                break;

            case BarcodeFormat.UPC_A:
                if (!isDigitsOnly || (text.Length != 11 && text.Length != 12))
                {
                    errors["text"] = ["UPC-A requires 11 or 12 numeric digits."];
                }
                break;

            case BarcodeFormat.ITF:
                if (!isDigitsOnly || text.Length % 2 != 0)
                {
                    errors["text"] = ["ITF requires an even number of numeric digits."];
                }
                break;
        }
    }
}
