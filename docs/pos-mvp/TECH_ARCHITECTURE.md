# POS + 庫存輔助 MVP — 技術架構

## 系統組件

- **Frontend**：React + Vite
- **Backend**：ASP.NET Core Minimal API
- **Database（Phase 1）**：SQLite（單店 MVP）
- **Database（Phase 2）**：PostgreSQL（多店/高併發）
- **Barcode Engine**：既有 Barcode.Generator
- **Deployment**：Frontend on Vercel, Backend on Render

---

## 功能模組

1. **商品管理（Products）**
   - 商品主檔
   - 條碼綁定（多條碼）

2. **POS（Sales）**
   - 掃碼加入購物車
   - 結帳（交易寫入 + 扣庫存）

3. **庫存（Inventory）**
   - 入庫/出庫/盤點
   - 異動流水
   - 低庫存警示

4. **報表（Reports）**
   - 每日營收
   - 單量
   - 熱銷商品

---

## 資料流（Checkout）

1. POS 前端送出 checkout request
2. API 驗證商品/庫存/支付
3. DB transaction:
   - 建立 sales_order / sales_order_items
   - 更新 inventory_levels
   - 寫入 inventory_movements（SALE）
4. 回傳訂單結果（orderNo、total、change）

---

## API 邊界（v1）

- `POST /api/products`
- `GET /api/products`
- `POST /api/products/{id}/barcodes`
- `GET /api/barcodes/{codeValue}`
- `POST /api/inventory/in`
- `POST /api/inventory/out`
- `POST /api/inventory/count`
- `GET /api/inventory/low-stock`
- `POST /api/sales/checkout`
- `GET /api/reports/daily-sales`

---

## 安全與穩定性

- API Rate Limiting（已導入）
- CORS Allowlist（已導入）
- 格式別條碼文字驗證（已導入）
- CI：npm audit + gitleaks + SHA-pinned actions（已導入）

---

## 非功能需求（MVP）

- 單店 1~3 台收銀同時使用
- 95% API 回應 < 300ms（不含外部依賴）
- 交易一致性優先於極速（使用 transaction）
