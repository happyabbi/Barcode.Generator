# POS + 庫存輔助 MVP — Sprint 計畫

> 版本：v1.0  
> 範圍：單店門市（Single-store）

## 目標與時程

- **Sprint 1（Week 1）**：商品、條碼、庫存底座
- **Sprint 2（Week 2）**：POS 結帳主流程
- **Sprint 3（Week 3）**：盤點/報表/上線準備

---

## Sprint 1 — 商品 + 條碼 + 庫存底座

### 目標
- 可建立商品、綁定條碼、查詢庫存。

### 任務
1. 建立資料表與 migration（stores, app_users, products, barcodes, inventory_levels, inventory_movements, sales_orders, sales_order_items）
2. Product CRUD API
3. Barcode API（新增/查詢/主條碼）
4. Inventory 入庫 API
5. 前端商品管理頁（列表/新增/編輯）
6. 條碼單張下載（PNG/SVG）
7. 基礎權限（admin/manager/cashier）

### 驗收（DoD）
- 可新增商品並綁 EAN/Code128。
- 掃碼可查到商品。
- 入庫後庫存即時更新。

---

## Sprint 2 — POS 結帳主流程

### 目標
- 店員可掃碼結帳，系統自動扣庫存。

### 任務
1. Checkout service（DB transaction）
2. SalesOrder / SalesOrderItem API
3. POS 前端頁（掃碼、購物車、折扣、結帳）
4. 支付方式（CASH/CARD）
5. 庫存不足檢查與錯誤處理
6. 小票資料回傳（JSON）

### 驗收（DoD）
- 可完整結帳流程。
- 結帳成功會建立訂單與扣庫存。
- 庫存不足會阻擋且有明確訊息。

---

## Sprint 3 — 庫存操作 + 報表 + 上線準備

### 目標
- 支援日常營運：盤點、低庫存、日報表。

### 任務
1. 出庫/盤點 API（InventoryMovement 完整化）
2. 低庫存查詢 API + 前端頁
3. 日銷售報表 API（營收/單量/熱銷）
4. 異動紀錄查詢頁
5. E2E smoke tests
6. 部署參數整理（staging/prod）

### 驗收（DoD）
- 可執行盤點並留存異動紀錄。
- 可查低庫存名單。
- 可查看當日銷售摘要。

---

## 風險與對策

- **資料格式錯誤（條碼內容）**：後端格式驗證 + 前端提示
- **SQLite 併發限制（初期可接受）**：啟用 WAL、縮短交易時間、先單店部署
- **高峰結帳競態（成長期）**：切換 PostgreSQL + checkout transaction + row-level lock
- **誤操作**：異動審計 + 權限分層

---

## 工時粗估

- Sprint 1：25–35h
- Sprint 2：30–40h
- Sprint 3：25–35h
- **合計：80–110h（單人）**
