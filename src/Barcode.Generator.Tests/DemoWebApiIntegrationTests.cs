using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Barcode.Generator.Tests;

public class DemoWebApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public DemoWebApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Generate_WithValidText_ReturnsBmp()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/generate?text=hello");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/bmp", response.Content.Headers.ContentType?.MediaType);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 2);
        Assert.Equal((byte)'B', bytes[0]);
        Assert.Equal((byte)'M', bytes[1]);
    }

    [Fact]
    public async Task Generate_WithSupportedNonQrFormat_ReturnsBmp()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/generate?text=ABC-123&format=CODE_128&width=500&height=200");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/bmp", response.Content.Headers.ContentType?.MediaType);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 2);
        Assert.Equal((byte)'B', bytes[0]);
        Assert.Equal((byte)'M', bytes[1]);
    }

    [Fact]
    public async Task Generate_WithPostBody_ReturnsBmp()
    {
        var client = _factory.CreateClient();
        var payload = JsonSerializer.Serialize(new
        {
            text = "post-content",
            format = "QR_CODE",
            width = 256,
            height = 256
        });

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/generate", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/bmp", response.Content.Headers.ContentType?.MediaType);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 2);
        Assert.Equal((byte)'B', bytes[0]);
        Assert.Equal((byte)'M', bytes[1]);
    }

    [Fact]
    public async Task Generate_WithoutText_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/generate");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Generate_WithInvalidWidth_ReturnsValidationProblem()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/generate?text=hello&width=10");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("width", body);
    }

    [Fact]
    public async Task Generate_WithInvalidFormat_ReturnsValidationProblem()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/generate?text=hello&format=UPC_E");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("format", body);
    }

    [Fact]
    public async Task Generate_ResponseContainsSecurityHeaders()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/generate?text=hello");

        Assert.True(response.Headers.Contains("X-Content-Type-Options"));
        Assert.True(response.Headers.Contains("X-Frame-Options"));
        Assert.True(response.Headers.Contains("Referrer-Policy"));
    }
}
