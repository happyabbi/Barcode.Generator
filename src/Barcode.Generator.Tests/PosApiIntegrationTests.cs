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

    [Fact]
    public async Task Checkout_WithEnoughStockAndCash_ShouldCreateOrderAndDeductInventory()
    {
        var client = _factory.CreateClient();
        var products = await client.GetAsync("/api/products?page=1&pageSize=10");
        products.EnsureSuccessStatusCode();

        var body = await products.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var first = doc.RootElement.GetProperty("items")[0];
        var productId = first.GetProperty("id").GetGuid();
        var price = first.GetProperty("price").GetDecimal();
        var qtyBefore = first.GetProperty("qtyOnHand").GetInt32();

        var payload = JsonSerializer.Serialize(new
        {
            items = new[] { new { productId, qty = 1 } },
            paymentMethod = "CASH",
            paidAmount = price + 10,
            discount = 0
        });

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/checkout", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var checkoutBody = await response.Content.ReadAsStringAsync();
        using var checkoutDoc = JsonDocument.Parse(checkoutBody);
        Assert.StartsWith("SO", checkoutDoc.RootElement.GetProperty("orderNo").GetString());
        Assert.Equal(price, checkoutDoc.RootElement.GetProperty("total").GetDecimal());

        var afterRes = await client.GetAsync($"/api/products?page=1&pageSize=10&keyword={first.GetProperty("sku").GetString()}");
        afterRes.EnsureSuccessStatusCode();
        var afterBody = await afterRes.Content.ReadAsStringAsync();
        using var afterDoc = JsonDocument.Parse(afterBody);
        var qtyAfter = afterDoc.RootElement.GetProperty("items")[0].GetProperty("qtyOnHand").GetInt32();

        Assert.Equal(qtyBefore - 1, qtyAfter);
    }

    [Fact]
    public async Task Checkout_WithInsufficientStock_ShouldReturnBadRequest()
    {
        var client = _factory.CreateClient();
        var products = await client.GetAsync("/api/products?page=1&pageSize=10");
        products.EnsureSuccessStatusCode();

        var body = await products.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var first = doc.RootElement.GetProperty("items")[0];
        var productId = first.GetProperty("id").GetGuid();
        var qtyOnHand = first.GetProperty("qtyOnHand").GetInt32();

        var payload = JsonSerializer.Serialize(new
        {
            items = new[] { new { productId, qty = qtyOnHand + 999 } },
            paymentMethod = "CASH",
            paidAmount = 999999
        });

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/checkout", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var resp = await response.Content.ReadAsStringAsync();
        Assert.Contains("insufficient stock", resp, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Checkout_WithInsufficientPayment_ShouldReturnBadRequest()
    {
        var client = _factory.CreateClient();
        var products = await client.GetAsync("/api/products?page=1&pageSize=10");
        products.EnsureSuccessStatusCode();

        var body = await products.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var first = doc.RootElement.GetProperty("items")[0];
        var productId = first.GetProperty("id").GetGuid();
        var price = first.GetProperty("price").GetDecimal();

        var payload = JsonSerializer.Serialize(new
        {
            items = new[] { new { productId, qty = 1 } },
            paymentMethod = "CASH",
            paidAmount = price - 0.01m
        });

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/checkout", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var resp = await response.Content.ReadAsStringAsync();
        Assert.Contains("insufficient", resp, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OrdersEndpoints_ShouldReturnCheckoutOrder()
    {
        var client = _factory.CreateClient();

        var products = await client.GetAsync("/api/products?page=1&pageSize=10");
        products.EnsureSuccessStatusCode();

        var body = await products.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var first = doc.RootElement.GetProperty("items")[0];
        var productId = first.GetProperty("id").GetGuid();
        var price = first.GetProperty("price").GetDecimal();

        var checkoutPayload = JsonSerializer.Serialize(new
        {
            items = new[] { new { productId, qty = 1 } },
            paymentMethod = "CASH",
            paidAmount = price
        });

        using var checkoutContent = new StringContent(checkoutPayload, Encoding.UTF8, "application/json");
        var checkoutResponse = await client.PostAsync("/api/checkout", checkoutContent);
        checkoutResponse.EnsureSuccessStatusCode();

        var checkoutBody = await checkoutResponse.Content.ReadAsStringAsync();
        using var checkoutDoc = JsonDocument.Parse(checkoutBody);
        var orderId = checkoutDoc.RootElement.GetProperty("id").GetGuid();

        var listResponse = await client.GetAsync("/api/orders?page=1&pageSize=10");
        listResponse.EnsureSuccessStatusCode();
        var listBody = await listResponse.Content.ReadAsStringAsync();
        using var listDoc = JsonDocument.Parse(listBody);

        Assert.True(listDoc.RootElement.GetProperty("items").GetArrayLength() >= 1);

        var detailResponse = await client.GetAsync($"/api/orders/{orderId}");
        detailResponse.EnsureSuccessStatusCode();
        var detailBody = await detailResponse.Content.ReadAsStringAsync();
        using var detailDoc = JsonDocument.Parse(detailBody);

        Assert.Equal(orderId, detailDoc.RootElement.GetProperty("id").GetGuid());
        Assert.True(detailDoc.RootElement.GetProperty("items").GetArrayLength() >= 1);
    }

    [Fact]
    public async Task CashierRole_CannotCreateProduct_ButCanCheckout()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Role", "cashier");

        var createPayload = JsonSerializer.Serialize(new
        {
            sku = "SKU-ROLE-001",
            name = "Role Test",
            category = "Test",
            price = 99,
            cost = 60,
            initialQty = 1,
            reorderLevel = 1
        });

        using var createContent = new StringContent(createPayload, Encoding.UTF8, "application/json");
        var createResponse = await client.PostAsync("/api/products", createContent);
        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);

        var products = await client.GetAsync("/api/products?page=1&pageSize=10");
        products.EnsureSuccessStatusCode();
        var productsBody = await products.Content.ReadAsStringAsync();
        using var productsDoc = JsonDocument.Parse(productsBody);
        var first = productsDoc.RootElement.GetProperty("items")[0];
        var productId = first.GetProperty("id").GetGuid();
        var price = first.GetProperty("price").GetDecimal();

        var checkoutPayload = JsonSerializer.Serialize(new
        {
            items = new[] { new { productId, qty = 1 } },
            paymentMethod = "CASH",
            paidAmount = price
        });

        using var checkoutContent = new StringContent(checkoutPayload, Encoding.UTF8, "application/json");
        var checkoutResponse = await client.PostAsync("/api/checkout", checkoutContent);
        Assert.Equal(HttpStatusCode.OK, checkoutResponse.StatusCode);
    }
}
