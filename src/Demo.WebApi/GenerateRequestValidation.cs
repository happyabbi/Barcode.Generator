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
}
