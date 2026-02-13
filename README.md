# Barcode.Generator

`Barcode.Generator` 是基於 ZXing.NET 的 .NET 函式庫，專注於「產生條碼／QR Code 並輸出記憶體中的 BMP 位元組」。

## 相容性與目標框架

- `src/Barcode.Generator`（核心函式庫）：`netstandard2.0` + `net8.0`
  - 保留 `netstandard2.0` 以維持舊版 .NET 生態系可用性
  - 新增 `net8.0` 以獲得現代執行環境與工具鏈支援
- `src/Demo.Console`：`net8.0`
- `src/Demo.WebApi`：`net8.0`
- `src/Demo.Web`：React + TypeScript + Vite
- `src/Barcode.Generator.Tests`：`net8.0`

## 專案結構

- `src/Barcode.Generator`：核心函式庫
- `src/Demo.Console`：主控台範例，將 QR Code 寫入檔案
- `src/Demo.WebApi`：ASP.NET Core Minimal API 範例，提供條碼產生端點
- `src/Demo.Web`：前端 Web UI（React）
- `src/Barcode.Generator.Tests`：xUnit 測試專案

## 快速開始（Library API）

```csharp
using Barcode.Generator;
using Barcode.Generator.Rendering;

var writer = new BarcodeWriterPixelData
{
    Format = BarcodeFormat.QR_CODE,
    Options = new Common.EncodingOptions
    {
        Width = 300,
        Height = 300,
        Margin = 1
    }
};

PixelData pixelData = writer.Write("Hello Barcode");
byte[] bmpBytes = BitmapConverter.FromPixelData(pixelData);
File.WriteAllBytes("qrcode.bmp", bmpBytes);
```

## 本機建置與測試

### 需求

- .NET SDK 8.0（建議使用最新 patch）
- Node.js 20+（前端）

### 建置

```bash
dotnet restore src/Barcode.Generator.sln
dotnet build src/Barcode.Generator.sln --configuration Release
```

### 測試

```bash
dotnet test src/Barcode.Generator.sln --configuration Release
```

## 執行 Web API 範例

```bash
dotnet run --project src/Demo.WebApi
```

`/generate` 支援參數：

- `text`：必填，不可為空白，長度上限 `1024`
- `width`：選填，若提供需介於 `64` ~ `2048`（預設 `300`）
- `height`：選填，若提供需介於 `64` ~ `2048`（預設 `300`）
- `format`：選填，預設 `QR_CODE`
  - 支援：`QR_CODE`, `CODE_128`, `CODE_39`, `EAN_13`, `EAN_8`, `ITF`, `UPC_A`, `PDF_417`, `DATA_MATRIX`

範例：

```http
GET /generate?text=Hello%20Barcode
GET /generate?text=ABC-123&format=CODE_128&width=500&height=200
GET /generate?text=471234567890&format=EAN_13&width=500&height=220
```

成功回傳 `image/bmp`。

若驗證失敗，回傳 `400`（validation problem），例如：

```json
{
  "errors": {
    "format": [
      "format must be one of: QR_CODE, CODE_128, CODE_39, EAN_13, EAN_8, ITF, UPC_A, PDF_417, DATA_MATRIX."
    ]
  }
}
```

## 執行前端（Demo.Web）

```bash
cd src/Demo.Web
cp .env.example .env
npm install
npm run dev
```

預設前端開在 `http://localhost:5173`。

可透過環境變數設定 API 位址：

```env
VITE_API_BASE_URL=http://localhost:5000
```

### 前後端一起啟動（建議）

1. Terminal A 啟動 Web API

```bash
dotnet run --project src/Demo.WebApi
```

2. Terminal B 啟動 React 前端

```bash
cd src/Demo.Web
npm run dev
```

3. 打開瀏覽器到 `http://localhost:5173`

### 前端建置

```bash
cd src/Demo.Web
npm run build
npm run preview
```

## CI

- GitHub Actions：`.github/workflows/ci.yml`
- push / pull request 會同時檢查前後端：
  - **Backend (.NET)**：`dotnet restore` → `dotnet build` → `dotnet test`
  - **Frontend (React)**：`npm ci` → `npm run build`（工作目錄：`src/Demo.Web`）

如需在本機模擬 CI：

```bash
# backend
dotnet restore src/Barcode.Generator.sln
dotnet build src/Barcode.Generator.sln --configuration Release
dotnet test src/Barcode.Generator.sln --configuration Release

# frontend
cd src/Demo.Web
npm ci
npm run build
```
