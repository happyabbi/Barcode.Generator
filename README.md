# Barcode.Generator

`Barcode.Generator` 是基於 ZXing.NET 的 .NET 函式庫，專注於「產生條碼／QR Code 並輸出記憶體中的 BMP 位元組」。

## 相容性與目標框架

- `src/Barcode.Generator`（核心函式庫）：`netstandard2.0` + `net8.0`
  - 保留 `netstandard2.0` 以維持舊版 .NET 生態系可用性
  - 新增 `net8.0` 以獲得現代執行環境與工具鏈支援
- `src/Demo.Console`：`net8.0`
- `src/Demo.WebApi`：`net8.0`
- `src/Barcode.Generator.Tests`：`net8.0`

## 專案結構

- `src/Barcode.Generator`：核心函式庫
- `src/Demo.Console`：主控台範例，將 QR Code 寫入檔案
- `src/Demo.WebApi`：ASP.NET Core Minimal API 範例，提供條碼產生端點
- `src/Barcode.Generator.Tests`：xUnit 測試專案

## 本機建置與測試

### 需求

- .NET SDK 8.0（建議使用最新 patch）

### 建置

```bash
dotnet restore src/Barcode.Generator.sln
dotnet build src/Barcode.Generator.sln --configuration Release
```

### 測試

```bash
dotnet test src/Barcode.Generator.sln --configuration Release
```

### 執行 Web API 範例

```bash
dotnet run --project src/Demo.WebApi
```

呼叫範例：

```http
GET /generate?text=Hello%20Barcode
GET /generate?text=Hello%20Barcode&width=512&height=512
```

參數限制（`/generate`）：

- `text`：必填，不可為空白，長度上限 `1024` 字元
- `width`：選填，若提供則需介於 `64` ~ `2048`
- `height`：選填，若提供則需介於 `64` ~ `2048`
- 未提供 `width` / `height` 時，預設為 `300 x 300`

成功回傳：`image/bmp`。

錯誤回應範例：

```http
GET /generate?text=
```

會回傳 `400`（validation problem），例如：

```json
{
  "errors": {
    "text": ["text is required and cannot be empty."]
  }
}
```

## CI

- GitHub Actions：`.github/workflows/ci.yml`
  - 會在 push / pull request 時執行 restore、build、test
