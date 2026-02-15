# POS 結帳流程圖（Sprint 2）

> 用途：先對齊實作流程，再開始前端 POS 結帳頁開發。

## High-level 流程

```mermaid
flowchart TD
    A[收銀員進入 POS 結帳頁] --> B[掃碼 / 輸入條碼]
    B --> C{條碼是否有效?}
    C -- 否 --> C1[顯示錯誤提示: 找不到商品] --> B
    C -- 是 --> D[加入購物車]

    D --> E[可調整數量 / 刪除品項]
    E --> F[輸入折扣]
    F --> G[計算小計/總額]

    G --> H[選擇付款方式 CASH/CARD]
    H --> I[輸入付款金額]
    I --> J[按下結帳]

    J --> K[POST /api/checkout]
    K --> L{後端驗證成功?}

    L -- 否: 庫存不足 --> L1[顯示庫存不足訊息] --> E
    L -- 否: 付款不足 --> L2[顯示付款不足訊息] --> I
    L -- 否: 其他錯誤 --> L3[顯示通用錯誤] --> J

    L -- 是 --> M[建立 SalesOrder + SalesOrderItems]
    M --> N[扣減 InventoryLevel]
    N --> O[寫入 InventoryMovement OUT]
    O --> P[回傳 orderNo/total/change]
    P --> Q[前端顯示結帳成功]
    Q --> R[清空購物車，準備下一單]
```

## 前端狀態（建議）

- `cartItems[]`: 目前購物車品項（productId, sku, name, unitPrice, qty, lineTotal）
- `discount`: 折扣金額
- `paymentMethod`: `CASH | CARD`
- `paidAmount`: 付款金額
- `checkoutError`: 結帳錯誤訊息（庫存不足 / 付款不足 / 其他）
- `lastOrderResult`: 最近一筆結帳結果（orderNo, total, change）

## API 契約（目前）

- `POST /api/checkout`
  - request:
    - `items[]`: `{ productId, qty }`
    - `paymentMethod`: `CASH | CARD`
    - `paidAmount`: decimal
    - `discount`?: decimal
    - `note`?: string
  - response:
    - `id`, `orderNo`, `total`, `changeAmount`, `items[]` ...

- `GET /api/orders`
- `GET /api/orders/{id}`

## 錯誤路徑（前端必做）

1. 條碼找不到商品
2. 庫存不足（後端回傳 `insufficient stock`）
3. 付款不足（後端回傳 `paid amount is insufficient`）
4. 卡片支付金額不等於總額（`CARD` 限制）
