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

## CD（Vercel + Render）設定指南

本專案提供兩個 GitHub Actions workflow：

- `.github/workflows/deploy-staging.yml`
  - 觸發條件：`feat/**` 分支 push、所有 PR
  - 作用：執行前後端 build/test；PR 時可選擇性執行 Vercel preview deploy（若 secrets 齊全）
- `.github/workflows/deploy-production.yml`
  - 觸發條件：**僅手動** `workflow_dispatch`
  - 作用：部署前先做 secrets preflight，然後分別部署 frontend（Vercel）與 backend（Render hook）

### 1) GitHub Secrets（必要）

請在 GitHub repo → **Settings → Secrets and variables → Actions** 新增：

- `VERCEL_TOKEN`：Vercel Personal/Team Token
- `VERCEL_ORG_ID`：Vercel Org ID
- `VERCEL_PROJECT_ID`：Vercel Frontend Project ID（對應 `src/Demo.Web`）
- `RENDER_DEPLOY_HOOK_URL`：Render backend service 的 Deploy Hook URL

> Workflow 會在缺少 secrets 時明確 fail，避免「看似成功但未部署」。

### 2) Vercel（Frontend）建議設定（Free Tier 可用）

1. 在 Vercel 建立專案，Root 設為 `src/Demo.Web`
2. Build Command: `npm run build`
3. Output Directory: `dist`
4. Node 版本使用 20（與 CI 一致）
5. 在 Vercel 專案環境變數加入：

```env
VITE_API_BASE_URL=https://<your-render-service>.onrender.com
```

### 3) Render（Backend）建議設定（Free Tier 可用）

1. 建立 Web Service，指向本 repo
2. Build Command（範例）：

```bash
dotnet restore src/Barcode.Generator.sln && dotnet build src/Barcode.Generator.sln -c Release --no-restore
```

3. Start Command（範例）：

```bash
dotnet run --project src/Demo.WebApi --configuration Release --no-build --urls http://0.0.0.0:$PORT
```

4. 建立 Deploy Hook，並把 URL 放到 GitHub secret `RENDER_DEPLOY_HOOK_URL`

### 4) CORS / API Base URL 建議

- Frontend 透過 `VITE_API_BASE_URL` 指向 Render API（例如 `https://xxx.onrender.com`）
- 若要讓 WebAPI 僅允許指定網域，建議將 CORS allowlist 改為以環境變數管理（例如 `ALLOWED_ORIGINS`），並在 Render 設定你的 Vercel 網域
- 最低建議：正式環境不要使用 `AllowAnyOrigin`

### 5) Production 部署流程（手動）

1. 到 GitHub Actions 執行 **Deploy Production**
2. 輸入要部署的 `git_ref`（預設 `master`）
3. 選擇是否部署 frontend/backend
4. Workflow 會先做 preflight：缺少 secrets 立即停止
5. Frontend 使用 Vercel CLI `--prod` 部署；Backend 觸發 Render deploy hook

### 6) Rollback 建議

- Frontend（Vercel）：
  - 在 Vercel Dashboard 將上一個 stable deployment Promote 到 Production
- Backend（Render）：
  - 在 Render Dashboard 重新部署上一個穩定 commit
- Git 層級：
  - 建議用 revert commit，再手動觸發 production workflow 部署

### 7) Troubleshooting

- **Missing required GitHub secrets**
  - 依錯誤訊息補齊 `VERCEL_*` 或 `RENDER_DEPLOY_HOOK_URL`
- **Vercel deploy 失敗**
  - 確認 `VERCEL_ORG_ID` / `VERCEL_PROJECT_ID` 對應同一個專案
  - 確認 Vercel 專案 Root Directory 是 `src/Demo.Web`
- **Render hook 非 2xx**
  - 檢查 hook URL 是否過期、是否貼錯 service
- **前端呼叫 API 失敗（CORS / 404）**
  - 檢查 `VITE_API_BASE_URL` 是否為正確 Render 網址
  - 檢查 WebAPI CORS 設定是否允許 Vercel 網域
