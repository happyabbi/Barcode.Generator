using Barcode.Generator;
using Demo.WebApi;
using Xunit;

namespace Barcode.Generator.Tests;

public class DemoWebApiValidationTests
{
    [Fact]
    public void Validate_ShouldReject_EmptyText()
    {
        var errors = GenerateRequestValidation.Validate(" ", null, null, null);

        Assert.True(errors.ContainsKey("text"));
    }

    [Fact]
    public void Validate_ShouldReject_TooLongText()
    {
        var text = new string('A', GenerateRequestValidation.MaxTextLength + 1);

        var errors = GenerateRequestValidation.Validate(text, null, null, null);

        Assert.True(errors.ContainsKey("text"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(63)]
    [InlineData(2049)]
    public void Validate_ShouldReject_OutOfRangeWidth(int width)
    {
        var errors = GenerateRequestValidation.Validate("hello", width, null, null);

        Assert.True(errors.ContainsKey("width"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(63)]
    [InlineData(2049)]
    public void Validate_ShouldReject_OutOfRangeHeight(int height)
    {
        var errors = GenerateRequestValidation.Validate("hello", null, height, null);

        Assert.True(errors.ContainsKey("height"));
    }

    [Fact]
    public void Validate_ShouldReject_UnsupportedFormat()
    {
        var errors = GenerateRequestValidation.Validate("hello", null, null, "UPC_E");

        Assert.True(errors.ContainsKey("format"));
    }

    [Fact]
    public void Validate_ShouldAccept_SupportedFormat()
    {
        var errors = GenerateRequestValidation.Validate("hello", null, null, "code_128");

        Assert.Empty(errors);
        Assert.Equal(BarcodeFormat.CODE_128, GenerateRequestValidation.ResolveFormat("code_128"));
    }

    [Fact]
    public void Validate_ShouldAccept_ValidRequestWithOptionalDimensions()
    {
        var errors = GenerateRequestValidation.Validate("hello", null, null, null);

        Assert.Empty(errors);
        Assert.Equal(GenerateRequestValidation.DefaultDimension, GenerateRequestValidation.ResolveDimension(null));
        Assert.Equal(256, GenerateRequestValidation.ResolveDimension(256));
        Assert.Equal(GenerateRequestValidation.DefaultFormat, GenerateRequestValidation.ResolveFormat(null));
    }
}
