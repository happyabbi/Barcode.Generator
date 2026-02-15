# POS + 庫存輔助 MVP — GitHub Issues Backlog

> 建議 Labels：`sprint-1`, `sprint-2`, `sprint-3`, `backend`, `frontend`, `db`, `security`, `report`

## Sprint 1

### 1) [DB] 建立 POS 核心 schema 與 migration（SQLite-first）
- Labels: `sprint-1`, `db`, `backend`
- DoD:
  - migration 可執行/可回滾
  - 8 張核心表建立完成
  - 索引與 unique constraint 生效
  - SQLite 啟用 WAL 模式與 busy_timeout

### 2) [API] Product CRUD
- Labels: `sprint-1`, `backend`
- DoD:
  - Create/Read/Update API 完成
  - SKU(store scope) unique
  - 單元測試涵蓋成功/失敗路徑

### 3) [API] Barcode 綁定與查詢
- Labels: `sprint-1`, `backend`
- DoD:
  - 可新增條碼、設定 primary
  - `/api/barcodes/{codeValue}` 可查商品
  - 格式與文字規則驗證完整

### 4) [API] Inventory 入庫
- Labels: `sprint-1`, `backend`
- DoD:
  - 入庫後 inventory_levels 更新
  - movement 記錄完整

### 5) [Web] 商品管理頁
- Labels: `sprint-1`, `frontend`
- DoD:
  - 商品列表、建立、編輯可用
  - 錯誤提示清楚

### 6) [Web] 單張條碼下載（PNG/SVG）
- Labels: `sprint-1`, `frontend`
- DoD:
  - 可下載單張條碼
  - 顯示 format 與 code value

---

## Sprint 2

### 7) [API] Checkout transaction service
- Labels: `sprint-2`, `backend`
- DoD:
  - transaction 包住訂單 + 扣庫存 + movement
  - 庫存不足時 rollback

### 8) [API] Sales order endpoints
- Labels: `sprint-2`, `backend`
- DoD:
  - checkout endpoint 可回傳 orderNo/total/change
  - 支援 CASH/CARD

### 9) [Web] POS 結帳頁
- Labels: `sprint-2`, `frontend`
- DoD:
  - 掃碼加購、改數量、刪除、折扣、結帳
  - 顯示庫存不足錯誤

### 10) [Test] Checkout 整合測試
- Labels: `sprint-2`, `backend`
- DoD:
  - 正常結帳、庫存不足、支付不足三類案例

---

## Sprint 3

### 11) [API] 出庫/盤點/異動查詢
- Labels: `sprint-3`, `backend`
- DoD:
  - `/inventory/out`, `/inventory/count` 完成
  - movement 查詢可依日期/商品篩選

### 12) [API+Web] 低庫存清單
- Labels: `sprint-3`, `backend`, `frontend`
- DoD:
  - 可依 reorder_level 回傳清單
  - 前端可排序/搜尋

### 13) [API+Web] 日銷售報表
- Labels: `sprint-3`, `backend`, `frontend`, `report`
- DoD:
  - 顯示營收、單量、熱銷前 N

### 14) [Ops] Staging/Prod 設定與 smoke test
- Labels: `sprint-3`, `security`
- DoD:
  - env 文件更新
  - CI/CD workflow 成功
  - smoke test script 可執行

---

## 建議 Milestones

- Milestone 1: `MVP-S1 Products+Inventory Base`
- Milestone 2: `MVP-S2 POS Checkout`
- Milestone 3: `MVP-S3 Operations+Reports`
