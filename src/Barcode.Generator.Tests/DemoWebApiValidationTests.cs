using Demo.WebApi;
using Xunit;

namespace Barcode.Generator.Tests;

public class DemoWebApiValidationTests
{
    [Fact]
    public void Validate_ShouldReject_EmptyText()
    {
        var errors = GenerateRequestValidation.Validate(" ", null, null);

        Assert.True(errors.ContainsKey("text"));
    }

    [Fact]
    public void Validate_ShouldReject_TooLongText()
    {
        var text = new string('A', GenerateRequestValidation.MaxTextLength + 1);

        var errors = GenerateRequestValidation.Validate(text, null, null);

        Assert.True(errors.ContainsKey("text"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(63)]
    [InlineData(2049)]
    public void Validate_ShouldReject_OutOfRangeWidth(int width)
    {
        var errors = GenerateRequestValidation.Validate("hello", width, null);

        Assert.True(errors.ContainsKey("width"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(63)]
    [InlineData(2049)]
    public void Validate_ShouldReject_OutOfRangeHeight(int height)
    {
        var errors = GenerateRequestValidation.Validate("hello", null, height);

        Assert.True(errors.ContainsKey("height"));
    }

    [Fact]
    public void Validate_ShouldAccept_ValidRequestWithOptionalDimensions()
    {
        var errors = GenerateRequestValidation.Validate("hello", null, null);

        Assert.Empty(errors);
        Assert.Equal(GenerateRequestValidation.DefaultDimension, GenerateRequestValidation.ResolveDimension(null));
        Assert.Equal(256, GenerateRequestValidation.ResolveDimension(256));
    }
}
