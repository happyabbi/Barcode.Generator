using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Barcode.Generator.Tests;

public class PosApiIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public PosApiIntegrationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SeededProducts_ShouldBeAvailable_FromProductsApi()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/products?page=1&pageSize=50");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        var items = doc.RootElement.GetProperty("items");
        Assert.True(items.GetArrayLength() >= 5);

        var hasSeedSku = false;
        foreach (var item in items.EnumerateArray())
        {
            if (item.GetProperty("sku").GetString() == "SKU-COFFEE-001")
            {
                hasSeedSku = true;
                break;
            }
        }

        Assert.True(hasSeedSku);
    }

    [Fact]
    public async Task ProductBarcodeInventory_Flow_ShouldWork()
    {
        var client = _factory.CreateClient();
        var sku = $"SKU-TEST-{Guid.NewGuid():N}"[..17];

        var createPayload = JsonSerializer.Serialize(new
        {
            sku,
            name = "Flow Test Product",
            category = "Test",
            price = 199,
            cost = 100,
            initialQty = 2,
            reorderLevel = 5
        });

        using var createContent = new StringContent(createPayload, Encoding.UTF8, "application/json");
        var createResponse = await client.PostAsync("/api/products", createContent);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var createdBody = await createResponse.Content.ReadAsStringAsync();
        using var createdDoc = JsonDocument.Parse(createdBody);
        var productId = createdDoc.RootElement.GetProperty("id").GetGuid();

        var barcodeValue = $"{Math.Abs(Guid.NewGuid().GetHashCode()):D12}";
        var barcodePayload = JsonSerializer.Serialize(new
        {
            format = "UPC_A",
            codeValue = barcodeValue,
            isPrimary = true
        });

        using var barcodeContent = new StringContent(barcodePayload, Encoding.UTF8, "application/json");
        var barcodeResponse = await client.PostAsync($"/api/products/{productId}/barcodes", barcodeContent);
        Assert.Equal(HttpStatusCode.Created, barcodeResponse.StatusCode);

        var findBarcodeResponse = await client.GetAsync($"/api/barcodes/{barcodeValue}");
        findBarcodeResponse.EnsureSuccessStatusCode();

        var stockInPayload = JsonSerializer.Serialize(new
        {
            productId,
            qty = 3,
            reason = "test stock in"
        });

        using var stockInContent = new StringContent(stockInPayload, Encoding.UTF8, "application/json");
        var stockInResponse = await client.PostAsync("/api/inventory/in", stockInContent);
        stockInResponse.EnsureSuccessStatusCode();

        var listResponse = await client.GetAsync($"/api/products?page=1&pageSize=100&keyword={sku}");
        listResponse.EnsureSuccessStatusCode();

        var listBody = await listResponse.Content.ReadAsStringAsync();
        using var listDoc = JsonDocument.Parse(listBody);
        var items = listDoc.RootElement.GetProperty("items");
        Assert.True(items.GetArrayLength() >= 1);

        var found = items[0];
        Assert.Equal(sku, found.GetProperty("sku").GetString());
        Assert.Equal(5, found.GetProperty("qtyOnHand").GetInt32());
    }
}
