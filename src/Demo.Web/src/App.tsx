import { FormEvent, useEffect, useMemo, useState } from 'react';

type BarcodeFormat =
  | 'QR_CODE'
  | 'CODE_128'
  | 'CODE_39'
  | 'EAN_13'
  | 'EAN_8'
  | 'ITF'
  | 'UPC_A'
  | 'PDF_417'
  | 'DATA_MATRIX';

type ProductItem = {
  id: string;
  sku: string;
  name: string;
  category?: string;
  price: number;
  cost: number;
  qtyOnHand: number;
  reorderLevel: number;
};

type ProductBarcode = {
  id: string;
  productId: string;
  format: string;
  codeValue: string;
  isPrimary: boolean;
};

type LowStockItem = {
  productId: string;
  sku: string;
  name: string;
  qtyOnHand: number;
  reorderLevel: number;
};

type ProductSortKey = 'sku' | 'name' | 'price' | 'qtyOnHand';

type ToastState = { type: 'success' | 'error'; message: string } | null;

const formats: { value: BarcodeFormat; label: string }[] = [
  { value: 'QR_CODE', label: 'QR Code' },
  { value: 'CODE_128', label: 'Code 128' },
  { value: 'CODE_39', label: 'Code 39' },
  { value: 'EAN_13', label: 'EAN-13' },
  { value: 'EAN_8', label: 'EAN-8' },
  { value: 'ITF', label: 'ITF' },
  { value: 'UPC_A', label: 'UPC-A' },
  { value: 'PDF_417', label: 'PDF417' },
  { value: 'DATA_MATRIX', label: 'Data Matrix' }
];

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000';

function parseError(body: string): string {
  let message = body || 'Request failed.';
  try {
    const parsed = JSON.parse(body) as { error?: string; errors?: Record<string, string[]> };
    if (parsed.error) {
      message = parsed.error;
    } else if (parsed.errors) {
      const first = Object.values(parsed.errors).flat()[0];
      if (first) message = first;
    }
  } catch {
    // keep raw body
  }
  return message;
}

export default function App() {
  const [tab, setTab] = useState<'barcode' | 'products'>('barcode');
  const [showCreateProduct, setShowCreateProduct] = useState(false);

  const [text, setText] = useState('Hello Barcode');
  const [format, setFormat] = useState<BarcodeFormat>('QR_CODE');
  const [width, setWidth] = useState(300);
  const [height, setHeight] = useState(300);
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);
  const [barcodeError, setBarcodeError] = useState<string | null>(null);
  const [isGenerating, setIsGenerating] = useState(false);

  const [products, setProducts] = useState<ProductItem[]>([]);
  const [keyword, setKeyword] = useState('');
  const [productsError, setProductsError] = useState<string | null>(null);
  const [isLoadingProducts, setIsLoadingProducts] = useState(false);
  const [sortKey, setSortKey] = useState<ProductSortKey>('sku');
  const [sortAsc, setSortAsc] = useState(true);

  const [toast, setToast] = useState<ToastState>(null);

  const [sku, setSku] = useState('');
  const [name, setName] = useState('');
  const [category, setCategory] = useState('');
  const [price, setPrice] = useState(99);
  const [cost, setCost] = useState(50);
  const [initialQty, setInitialQty] = useState(0);
  const [reorderLevel, setReorderLevel] = useState(10);
  const [createErrors, setCreateErrors] = useState<Record<string, string>>({});

  const [selectedProductId, setSelectedProductId] = useState('');
  const [barcodeFormat, setBarcodeFormat] = useState<BarcodeFormat>('CODE_128');
  const [codeValue, setCodeValue] = useState('');
  const [stockInQty, setStockInQty] = useState(10);

  const [editName, setEditName] = useState('');
  const [editCategory, setEditCategory] = useState('');
  const [editPrice, setEditPrice] = useState(0);
  const [editCost, setEditCost] = useState(0);
  const [editReorder, setEditReorder] = useState(10);

  const [barcodes, setBarcodes] = useState<ProductBarcode[]>([]);
  const [lowStockItems, setLowStockItems] = useState<LowStockItem[]>([]);

  const selectedProduct = useMemo(
    () => products.find((p) => p.id === selectedProductId) ?? null,
    [products, selectedProductId]
  );

  const sortedProducts = useMemo(() => {
    const cloned = [...products];
    cloned.sort((a, b) => {
      const av = a[sortKey];
      const bv = b[sortKey];
      if (typeof av === 'number' && typeof bv === 'number') {
        return sortAsc ? av - bv : bv - av;
      }
      const aa = String(av).toLowerCase();
      const bb = String(bv).toLowerCase();
      if (aa < bb) return sortAsc ? -1 : 1;
      if (aa > bb) return sortAsc ? 1 : -1;
      return 0;
    });
    return cloned;
  }, [products, sortAsc, sortKey]);

  useEffect(() => {
    if (!selectedProduct) return;
    setEditName(selectedProduct.name);
    setEditCategory(selectedProduct.category ?? '');
    setEditPrice(selectedProduct.price);
    setEditCost(selectedProduct.cost);
    setEditReorder(selectedProduct.reorderLevel);
  }, [selectedProduct]);

  useEffect(() => {
    if (!toast) return;
    const timer = setTimeout(() => setToast(null), 2200);
    return () => clearTimeout(timer);
  }, [toast]);

  const showSuccess = (message: string) => setToast({ type: 'success', message });
  const showError = (message: string) => setToast({ type: 'error', message });

  const loadProducts = async (search?: string) => {
    setIsLoadingProducts(true);
    setProductsError(null);
    try {
      const query = new URLSearchParams({ page: '1', pageSize: '100' });
      const q = search ?? keyword;
      if (q.trim()) query.set('keyword', q.trim());

      const response = await fetch(new URL(`/api/products?${query.toString()}`, apiBaseUrl).toString());
      const body = await response.text();
      if (!response.ok) {
        setProductsError(parseError(body));
        return;
      }

      const parsed = JSON.parse(body) as { items: ProductItem[] };
      setProducts(parsed.items ?? []);
      if (!selectedProductId && parsed.items?.length) {
        setSelectedProductId(parsed.items[0].id);
      }
    } catch {
      setProductsError('Unable to load products.');
    } finally {
      setIsLoadingProducts(false);
    }
  };

  const loadBarcodes = async (productId: string) => {
    setBarcodes([]);
    try {
      const response = await fetch(new URL(`/api/products/${productId}/barcodes`, apiBaseUrl).toString());
      if (!response.ok) return;
      const body = await response.json() as { items: ProductBarcode[] };
      setBarcodes(body.items ?? []);
    } catch {
      // ignore
    }
  };

  const loadLowStock = async () => {
    try {
      const response = await fetch(new URL('/api/inventory/low-stock', apiBaseUrl).toString());
      if (!response.ok) return;
      const body = await response.json() as { items: LowStockItem[] };
      setLowStockItems(body.items ?? []);
    } catch {
      // ignore
    }
  };

  useEffect(() => {
    loadProducts('');
    loadLowStock();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    if (selectedProductId) {
      loadBarcodes(selectedProductId);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedProductId]);

  const toggleSort = (key: ProductSortKey) => {
    if (sortKey === key) {
      setSortAsc((v) => !v);
      return;
    }
    setSortKey(key);
    setSortAsc(true);
  };

  const getBarcodeValidationHint = (): string | null => {
    const value = codeValue.trim();
    if (!value) return null;

    const onlyDigits = /^\d+$/.test(value);
    if (barcodeFormat === 'EAN_13' && (!onlyDigits || !(value.length === 12 || value.length === 13))) {
      return 'EAN-13 需為 12 或 13 位數字';
    }
    if (barcodeFormat === 'EAN_8' && (!onlyDigits || !(value.length === 7 || value.length === 8))) {
      return 'EAN-8 需為 7 或 8 位數字';
    }
    if (barcodeFormat === 'UPC_A' && (!onlyDigits || !(value.length === 11 || value.length === 12))) {
      return 'UPC-A 需為 11 或 12 位數字';
    }
    if (barcodeFormat === 'ITF' && (!onlyDigits || value.length % 2 !== 0)) {
      return 'ITF 需為偶數位數字';
    }
    return null;
  };

  const onQuickStockIn = async (item: LowStockItem) => {
    const qty = Math.max(item.reorderLevel - item.qtyOnHand + 1, 1);
    try {
      const response = await fetch(new URL('/api/inventory/in', apiBaseUrl).toString(), {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ productId: item.productId, qty, reason: 'Quick stock-in from low stock panel' })
      });
      const body = await response.text();
      if (!response.ok) {
        const err = parseError(body);
        setProductsError(err);
        showError(err);
        return;
      }

      setSelectedProductId(item.productId);
      setStockInQty(qty);
      showSuccess(`已快速補貨 ${item.sku} +${qty}`);
      await loadProducts();
      await loadLowStock();
    } catch {
      const err = 'Quick stock-in failed.';
      setProductsError(err);
      showError(err);
    }
  };

  const onGenerate = async (event: FormEvent) => {
    event.preventDefault();
    setBarcodeError(null);

    if (!text.trim()) {
      setBarcodeError('Please enter text for barcode generation.');
      return;
    }

    if (width < 64 || width > 2048 || height < 64 || height > 2048) {
      setBarcodeError('Width and height must be between 64 and 2048.');
      return;
    }

    setIsGenerating(true);
    try {
      const response = await fetch(new URL('/generate', apiBaseUrl).toString(), {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({ text, format, width, height })
      });

      if (!response.ok) {
        const body = await response.text();
        setBarcodeError(parseError(body));
        setPreviewUrl((current) => {
          if (current) URL.revokeObjectURL(current);
          return null;
        });
        return;
      }

      const blob = await response.blob();
      const objectUrl = URL.createObjectURL(blob);
      setPreviewUrl((current) => {
        if (current) URL.revokeObjectURL(current);
        return objectUrl;
      });
      showSuccess('Barcode generated.');
    } catch {
      setBarcodeError('Unable to connect to API. Check WebApi URL and availability.');
    } finally {
      setIsGenerating(false);
    }
  };

  const onCreateProduct = async (event: FormEvent) => {
    event.preventDefault();
    setProductsError(null);
    setCreateErrors({});

    const nextErrors: Record<string, string> = {};
    if (!sku.trim()) nextErrors.sku = 'SKU is required';
    if (!name.trim()) nextErrors.name = 'Name is required';
    if (price < 0) nextErrors.price = 'Price must be >= 0';
    if (cost < 0) nextErrors.cost = 'Cost must be >= 0';
    if (initialQty < 0) nextErrors.initialQty = 'Initial Qty must be >= 0';
    if (reorderLevel < 0) nextErrors.reorderLevel = 'Reorder Level must be >= 0';

    if (Object.keys(nextErrors).length) {
      setCreateErrors(nextErrors);
      return;
    }

    try {
      const response = await fetch(new URL('/api/products', apiBaseUrl).toString(), {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          sku,
          name,
          category: category || null,
          price,
          cost,
          initialQty,
          reorderLevel
        })
      });
      const body = await response.text();
      if (!response.ok) {
        const err = parseError(body);
        setProductsError(err);
        showError(err);
        return;
      }

      showSuccess(`Product ${sku} created.`);
      setSku('');
      setName('');
      setCategory('');
      setPrice(99);
      setCost(50);
      setInitialQty(0);
      setReorderLevel(10);
      setShowCreateProduct(false);
      await loadProducts('');
      await loadLowStock();
    } catch {
      const err = 'Failed to create product.';
      setProductsError(err);
      showError(err);
    }
  };

  const onUpdateProduct = async (event: FormEvent) => {
    event.preventDefault();
    setProductsError(null);

    if (!selectedProductId) {
      const err = 'Select a product first.';
      setProductsError(err);
      showError(err);
      return;
    }

    try {
      const response = await fetch(new URL(`/api/products/${selectedProductId}`, apiBaseUrl).toString(), {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          name: editName,
          category: editCategory || null,
          price: editPrice,
          cost: editCost,
          reorderLevel: editReorder
        })
      });

      const body = await response.text();
      if (!response.ok) {
        const err = parseError(body);
        setProductsError(err);
        showError(err);
        return;
      }

      showSuccess('Product updated.');
      await loadProducts();
      await loadLowStock();
    } catch {
      const err = 'Failed to update product.';
      setProductsError(err);
      showError(err);
    }
  };

  const onAddBarcode = async (event: FormEvent) => {
    event.preventDefault();
    setProductsError(null);

    if (!selectedProductId || !codeValue.trim()) {
      const err = 'Select a product and enter code value.';
      setProductsError(err);
      showError(err);
      return;
    }

    const barcodeHint = getBarcodeValidationHint();
    if (barcodeHint) {
      setProductsError(barcodeHint);
      showError(barcodeHint);
      return;
    }

    try {
      const response = await fetch(new URL(`/api/products/${selectedProductId}/barcodes`, apiBaseUrl).toString(), {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ format: barcodeFormat, codeValue, isPrimary: true })
      });
      const body = await response.text();
      if (!response.ok) {
        const err = parseError(body);
        setProductsError(err);
        showError(err);
        return;
      }

      showSuccess(`Barcode ${barcodeFormat} added.`);
      setCodeValue('');
      await loadProducts();
      await loadBarcodes(selectedProductId);
    } catch {
      const err = 'Failed to add barcode.';
      setProductsError(err);
      showError(err);
    }
  };

  const onStockIn = async (event: FormEvent) => {
    event.preventDefault();
    setProductsError(null);

    if (!selectedProductId || stockInQty <= 0) {
      const err = 'Select a product and enter qty > 0.';
      setProductsError(err);
      showError(err);
      return;
    }

    try {
      const response = await fetch(new URL('/api/inventory/in', apiBaseUrl).toString(), {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ productId: selectedProductId, qty: stockInQty, reason: 'Manual stock in' })
      });
      const body = await response.text();
      if (!response.ok) {
        const err = parseError(body);
        setProductsError(err);
        showError(err);
        return;
      }

      showSuccess(`Stock in +${stockInQty} completed.`);
      await loadProducts();
      await loadLowStock();
    } catch {
      const err = 'Failed to stock in.';
      setProductsError(err);
      showError(err);
    }
  };

  return (
    <div className="page">
      <div className="card">
        <div className="brand-header">
          <div className="brand-left">
            <div className="brand-logo">BG</div>
            <div>
              <h1>BarcodeOps Console</h1>
              <p className="subtitle">Professional barcode + product workflow for retail teams.</p>
            </div>
          </div>
          <div className="brand-badge">Local MVP</div>
        </div>

        <div className="tabs">
          <button className={tab === 'barcode' ? 'tab active' : 'tab'} onClick={() => setTab('barcode')}>Barcode</button>
          <button className={tab === 'products' ? 'tab active' : 'tab'} onClick={() => setTab('products')}>Products</button>
        </div>

        {toast && (
          <div className={`toast ${toast.type === 'success' ? 'toast-success' : 'toast-error'}`}>
            {toast.message}
          </div>
        )}

        {tab === 'barcode' && (
          <>
            <form className="form" onSubmit={onGenerate}>
              <label>
                Text <span className="req">*</span>
                <textarea value={text} onChange={(e) => setText(e.target.value)} rows={4} placeholder="Enter content..." />
              </label>

              <div className="grid">
                <label>
                  Format
                  <select value={format} onChange={(e) => setFormat(e.target.value as BarcodeFormat)}>
                    {formats.map((item) => (
                      <option key={item.value} value={item.value}>{item.label}</option>
                    ))}
                  </select>
                </label>

                <label>
                  Width
                  <input type="number" min={64} max={2048} value={width} onChange={(e) => setWidth(Number(e.target.value))} />
                </label>

                <label>
                  Height
                  <input type="number" min={64} max={2048} value={height} onChange={(e) => setHeight(Number(e.target.value))} />
                </label>
              </div>

              <button type="submit" disabled={isGenerating}>{isGenerating ? 'Generating...' : 'Generate barcode'}</button>
            </form>

            {barcodeError && <div className="error">{barcodeError}</div>}

            <div className="preview">
              {previewUrl ? <img src={previewUrl} alt="Generated barcode" /> : <div className="placeholder">Preview will appear here</div>}
            </div>

            <div className="actions">
              <a
                href={previewUrl ?? '#'}
                download={`barcode-${format.toLowerCase()}.bmp`}
                className={`download ${previewUrl ? '' : 'disabled'}`}
                onClick={(e) => !previewUrl && e.preventDefault()}
              >
                Download BMP
              </a>
              <span className="api">API: {apiBaseUrl}</span>
            </div>
          </>
        )}

        {tab === 'products' && (
          <>
            <div className="section header-row">
              <h2>Products</h2>
              <button onClick={() => setShowCreateProduct((v) => !v)}>{showCreateProduct ? 'Close Create Form' : 'Create Product'}</button>
            </div>

            {showCreateProduct && (
              <div className="section">
                <h3>Create Product</h3>
                <form className="form" onSubmit={onCreateProduct}>
                  <div className="grid create-grid">
                    <label>SKU <span className="req">*</span>
                      <input value={sku} onChange={(e) => setSku(e.target.value)} />
                      {createErrors.sku && <span className="field-error">{createErrors.sku}</span>}
                    </label>
                    <label>Name <span className="req">*</span>
                      <input value={name} onChange={(e) => setName(e.target.value)} />
                      {createErrors.name && <span className="field-error">{createErrors.name}</span>}
                    </label>
                    <label>Category
                      <input value={category} onChange={(e) => setCategory(e.target.value)} />
                    </label>
                    <label>Price
                      <input className="num" type="number" min={0} step="0.01" value={price} onChange={(e) => setPrice(Number(e.target.value))} />
                      {createErrors.price && <span className="field-error">{createErrors.price}</span>}
                    </label>
                    <label>Cost
                      <input className="num" type="number" min={0} step="0.01" value={cost} onChange={(e) => setCost(Number(e.target.value))} />
                      {createErrors.cost && <span className="field-error">{createErrors.cost}</span>}
                    </label>
                    <label>Initial Qty
                      <input className="num" type="number" min={0} value={initialQty} onChange={(e) => setInitialQty(Number(e.target.value))} />
                      {createErrors.initialQty && <span className="field-error">{createErrors.initialQty}</span>}
                    </label>
                    <label>Reorder Level
                      <input className="num" type="number" min={0} value={reorderLevel} onChange={(e) => setReorderLevel(Number(e.target.value))} />
                      {createErrors.reorderLevel && <span className="field-error">{createErrors.reorderLevel}</span>}
                    </label>
                  </div>
                  <button type="submit">Create Product</button>
                </form>
              </div>
            )}

            {!products.length && !isLoadingProducts && !showCreateProduct && (
              <div className="empty-state">
                <p>尚未有商品，先建立第一筆商品。</p>
                <button onClick={() => setShowCreateProduct(true)}>Create First Product</button>
              </div>
            )}

            <div className="products-layout">
              <div className="left-panel">
                <div className="row">
                  <input value={keyword} onChange={(e) => setKeyword(e.target.value)} placeholder="Search by SKU or name" />
                  <button onClick={() => loadProducts()}>Search</button>
                </div>

                <div className="table-wrap">
                  <table>
                    <thead>
                      <tr>
                        <th><button className="th-btn" onClick={() => toggleSort('sku')}>SKU {sortKey === 'sku' ? (sortAsc ? '↑' : '↓') : ''}</button></th>
                        <th><button className="th-btn" onClick={() => toggleSort('name')}>Name {sortKey === 'name' ? (sortAsc ? '↑' : '↓') : ''}</button></th>
                        <th><button className="th-btn" onClick={() => toggleSort('price')}>Price {sortKey === 'price' ? (sortAsc ? '↑' : '↓') : ''}</button></th>
                        <th><button className="th-btn" onClick={() => toggleSort('qtyOnHand')}>Qty {sortKey === 'qtyOnHand' ? (sortAsc ? '↑' : '↓') : ''}</button></th>
                      </tr>
                    </thead>
                    <tbody>
                      {sortedProducts.map((p) => (
                        <tr key={p.id} className={selectedProductId === p.id ? 'selected' : ''} onClick={() => setSelectedProductId(p.id)}>
                          <td>{p.sku}</td>
                          <td>{p.name}</td>
                          <td>{p.price}</td>
                          <td>{p.qtyOnHand}</td>
                        </tr>
                      ))}
                      {!products.length && !isLoadingProducts && (
                        <tr><td colSpan={4}>No products yet.</td></tr>
                      )}
                    </tbody>
                  </table>
                </div>
              </div>

              <div className="right-panel">
                <h3>Edit Selected Product</h3>
                <p className="subtitle small">{selectedProduct ? `${selectedProduct.sku} - ${selectedProduct.name}` : 'Select one from list'}</p>
                <form className="form" onSubmit={onUpdateProduct}>
                  <div className="grid detail-grid">
                    <label>Name<input value={editName} onChange={(e) => setEditName(e.target.value)} /></label>
                    <label>Category<input value={editCategory} onChange={(e) => setEditCategory(e.target.value)} /></label>
                    <label>Price<input className="num" type="number" min={0} step="0.01" value={editPrice} onChange={(e) => setEditPrice(Number(e.target.value))} /></label>
                    <label>Cost<input className="num" type="number" min={0} step="0.01" value={editCost} onChange={(e) => setEditCost(Number(e.target.value))} /></label>
                    <label>Reorder<input className="num" type="number" min={0} value={editReorder} onChange={(e) => setEditReorder(Number(e.target.value))} /></label>
                  </div>
                  <button type="submit" disabled={!selectedProductId}>Update Product</button>
                </form>

                <h3>Add Primary Barcode</h3>
                <form className="form" onSubmit={onAddBarcode}>
                  <div className="grid detail-grid">
                    <label>
                      Format
                      <select value={barcodeFormat} onChange={(e) => setBarcodeFormat(e.target.value as BarcodeFormat)}>
                        {formats.map((item) => (
                          <option key={item.value} value={item.value}>{item.label}</option>
                        ))}
                      </select>
                    </label>
                    <label>
                      Code Value
                      <input value={codeValue} onChange={(e) => setCodeValue(e.target.value)} placeholder="e.g. 471234567890" />
                      {getBarcodeValidationHint() && <span className="field-error">{getBarcodeValidationHint()}</span>}
                    </label>
                  </div>
                  <button type="submit" disabled={!selectedProductId || !!getBarcodeValidationHint()}>Add Barcode</button>
                </form>

                <div className="table-wrap slim">
                  <table>
                    <thead>
                      <tr><th>Format</th><th>Code</th><th>Primary</th></tr>
                    </thead>
                    <tbody>
                      {barcodes.map((b) => (
                        <tr key={b.id}>
                          <td>{b.format}</td>
                          <td>{b.codeValue}</td>
                          <td>{b.isPrimary ? 'Yes' : 'No'}</td>
                        </tr>
                      ))}
                      {!barcodes.length && <tr><td colSpan={3}>No barcodes yet.</td></tr>}
                    </tbody>
                  </table>
                </div>

                <h3>Stock In</h3>
                <form className="form" onSubmit={onStockIn}>
                  <div className="grid detail-grid">
                    <label>
                      Qty
                      <input className="num" type="number" min={1} value={stockInQty} onChange={(e) => setStockInQty(Number(e.target.value))} />
                    </label>
                  </div>
                  <button type="submit" disabled={!selectedProductId}>Stock In</button>
                </form>
              </div>
            </div>

            <div className="section">
              <h2>Low Stock</h2>
              <div className="table-wrap slim">
                <table>
                  <thead>
                    <tr><th>SKU</th><th>Name</th><th>Qty</th><th>Reorder</th><th>Action</th></tr>
                  </thead>
                  <tbody>
                    {lowStockItems.map((i) => (
                      <tr key={i.productId}>
                        <td>{i.sku}</td>
                        <td>{i.name}</td>
                        <td>{i.qtyOnHand}</td>
                        <td>{i.reorderLevel}</td>
                        <td><button className="mini-btn" onClick={() => onQuickStockIn(i)}>Quick +</button></td>
                      </tr>
                    ))}
                    {!lowStockItems.length && <tr><td colSpan={5}>No low stock items.</td></tr>}
                  </tbody>
                </table>
              </div>
            </div>

            {productsError && <div className="error">{productsError}</div>}
          </>
        )}
      </div>
    </div>
  );
}
