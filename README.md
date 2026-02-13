# Barcode.Generator

`Barcode.Generator` 是基於 ZXing.NET 的 .NET 標準函式庫，專注於「產生條碼／QR Code 並輸出記憶體中的 BMP 位元組」。

## 專案結構

- `src/Barcode.Generator`：核心函式庫（`netstandard2.0`）
- `src/Demo.Console`：主控台範例，將 QR Code 寫入檔案
- `src/Demo.WebApi`：ASP.NET Core Minimal API 範例，提供條碼產生端點
- `src/Barcode.Generator.Tests`：xUnit 測試專案

## 快速開始

### 建置

```bash
dotnet build src/Barcode.Generator.sln
```

### 執行測試

```bash
dotnet test src/Barcode.Generator.sln
```

### 執行 Web API 範例

```bash
dotnet run --project src/Demo.WebApi
```

呼叫範例：

```http
GET /generate?text=Hello%20Barcode
```

回傳為 `image/bmp`。
