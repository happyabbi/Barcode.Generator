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
  const [productsInfo, setProductsInfo] = useState<string | null>(null);
  const [isLoadingProducts, setIsLoadingProducts] = useState(false);

  const [sku, setSku] = useState('');
  const [name, setName] = useState('');
  const [category, setCategory] = useState('');
  const [price, setPrice] = useState(99);
  const [cost, setCost] = useState(50);
  const [initialQty, setInitialQty] = useState(0);
  const [reorderLevel, setReorderLevel] = useState(10);

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

  useEffect(() => {
    if (!selectedProduct) return;
    setEditName(selectedProduct.name);
    setEditCategory(selectedProduct.category ?? '');
    setEditPrice(selectedProduct.price);
    setEditCost(selectedProduct.cost);
    setEditReorder(selectedProduct.reorderLevel);
  }, [selectedProduct]);

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
    } catch {
      setBarcodeError('Unable to connect to API. Check WebApi URL and availability.');
    } finally {
      setIsGenerating(false);
    }
  };

  const onCreateProduct = async (event: FormEvent) => {
    event.preventDefault();
    setProductsError(null);
    setProductsInfo(null);

    if (!sku.trim() || !name.trim()) {
      setProductsError('SKU and Name are required.');
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
        setProductsError(parseError(body));
        return;
      }

      setProductsInfo(`Product ${sku} created.`);
      setSku('');
      setName('');
      setCategory('');
      setPrice(99);
      setCost(50);
      setInitialQty(0);
      setReorderLevel(10);
      await loadProducts('');
      await loadLowStock();
    } catch {
      setProductsError('Failed to create product.');
    }
  };

  const onUpdateProduct = async (event: FormEvent) => {
    event.preventDefault();
    setProductsError(null);
    setProductsInfo(null);

    if (!selectedProductId) {
      setProductsError('Select a product first.');
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
        setProductsError(parseError(body));
        return;
      }

      setProductsInfo('Product updated.');
      await loadProducts();
      await loadLowStock();
    } catch {
      setProductsError('Failed to update product.');
    }
  };

  const onAddBarcode = async (event: FormEvent) => {
    event.preventDefault();
    setProductsError(null);
    setProductsInfo(null);

    if (!selectedProductId || !codeValue.trim()) {
      setProductsError('Select a product and enter code value.');
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
        setProductsError(parseError(body));
        return;
      }

      setProductsInfo(`Barcode ${barcodeFormat} added.`);
      setCodeValue('');
      await loadProducts();
      await loadBarcodes(selectedProductId);
    } catch {
      setProductsError('Failed to add barcode.');
    }
  };

  const onStockIn = async (event: FormEvent) => {
    event.preventDefault();
    setProductsError(null);
    setProductsInfo(null);

    if (!selectedProductId || stockInQty <= 0) {
      setProductsError('Select a product and enter qty > 0.');
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
        setProductsError(parseError(body));
        return;
      }

      setProductsInfo(`Stock in +${stockInQty} completed.`);
      await loadProducts();
      await loadLowStock();
    } catch {
      setProductsError('Failed to stock in.');
    }
  };

  return (
    <div className="page">
      <div className="card">
        <h1>Barcode Generator + Product Manager</h1>
        <p className="subtitle">Sprint 1: barcode generation, products, barcodes and inventory-in.</p>

        <div className="tabs">
          <button className={tab === 'barcode' ? 'tab active' : 'tab'} onClick={() => setTab('barcode')}>Barcode</button>
          <button className={tab === 'products' ? 'tab active' : 'tab'} onClick={() => setTab('products')}>Products</button>
        </div>

        {tab === 'barcode' && (
          <>
            <form className="form" onSubmit={onGenerate}>
              <label>
                Text
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
            <div className="section">
              <h2>Create Product</h2>
              <form className="form" onSubmit={onCreateProduct}>
                <div className="grid">
                  <label>SKU<input value={sku} onChange={(e) => setSku(e.target.value)} /></label>
                  <label>Name<input value={name} onChange={(e) => setName(e.target.value)} /></label>
                  <label>Category<input value={category} onChange={(e) => setCategory(e.target.value)} /></label>
                  <label>Price<input type="number" min={0} step="0.01" value={price} onChange={(e) => setPrice(Number(e.target.value))} /></label>
                  <label>Cost<input type="number" min={0} step="0.01" value={cost} onChange={(e) => setCost(Number(e.target.value))} /></label>
                  <label>Initial Qty<input type="number" min={0} value={initialQty} onChange={(e) => setInitialQty(Number(e.target.value))} /></label>
                  <label>Reorder Level<input type="number" min={0} value={reorderLevel} onChange={(e) => setReorderLevel(Number(e.target.value))} /></label>
                </div>
                <button type="submit">Create Product</button>
              </form>
            </div>

            <div className="section">
              <h2>Search & List</h2>
              <div className="row">
                <input value={keyword} onChange={(e) => setKeyword(e.target.value)} placeholder="Search by SKU or name" />
                <button onClick={() => loadProducts()}>Search</button>
              </div>

              <div className="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>SKU</th><th>Name</th><th>Category</th><th>Price</th><th>Qty</th><th>Reorder</th>
                    </tr>
                  </thead>
                  <tbody>
                    {products.map((p) => (
                      <tr key={p.id} className={selectedProductId === p.id ? 'selected' : ''} onClick={() => setSelectedProductId(p.id)}>
                        <td>{p.sku}</td>
                        <td>{p.name}</td>
                        <td>{p.category ?? '-'}</td>
                        <td>{p.price}</td>
                        <td>{p.qtyOnHand}</td>
                        <td>{p.reorderLevel}</td>
                      </tr>
                    ))}
                    {!products.length && !isLoadingProducts && (
                      <tr><td colSpan={6}>No products yet.</td></tr>
                    )}
                  </tbody>
                </table>
              </div>
            </div>

            <div className="section">
              <h2>Edit Selected Product</h2>
              <p className="subtitle small">Selected: {selectedProduct ? `${selectedProduct.sku} - ${selectedProduct.name}` : 'None'}</p>
              <form className="form" onSubmit={onUpdateProduct}>
                <div className="grid">
                  <label>Name<input value={editName} onChange={(e) => setEditName(e.target.value)} /></label>
                  <label>Category<input value={editCategory} onChange={(e) => setEditCategory(e.target.value)} /></label>
                  <label>Price<input type="number" min={0} step="0.01" value={editPrice} onChange={(e) => setEditPrice(Number(e.target.value))} /></label>
                  <label>Cost<input type="number" min={0} step="0.01" value={editCost} onChange={(e) => setEditCost(Number(e.target.value))} /></label>
                  <label>Reorder Level<input type="number" min={0} value={editReorder} onChange={(e) => setEditReorder(Number(e.target.value))} /></label>
                </div>
                <button type="submit">Update Product</button>
              </form>
            </div>

            <div className="section">
              <h2>Selected Product Actions</h2>

              <form className="form" onSubmit={onAddBarcode}>
                <div className="grid">
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
                  </label>
                </div>
                <button type="submit">Add Primary Barcode</button>
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

              <form className="form" onSubmit={onStockIn}>
                <div className="grid">
                  <label>
                    Stock In Qty
                    <input type="number" min={1} value={stockInQty} onChange={(e) => setStockInQty(Number(e.target.value))} />
                  </label>
                </div>
                <button type="submit">Stock In</button>
              </form>
            </div>

            <div className="section">
              <h2>Low Stock</h2>
              <div className="table-wrap slim">
                <table>
                  <thead>
                    <tr><th>SKU</th><th>Name</th><th>Qty</th><th>Reorder</th></tr>
                  </thead>
                  <tbody>
                    {lowStockItems.map((i) => (
                      <tr key={i.productId}>
                        <td>{i.sku}</td>
                        <td>{i.name}</td>
                        <td>{i.qtyOnHand}</td>
                        <td>{i.reorderLevel}</td>
                      </tr>
                    ))}
                    {!lowStockItems.length && <tr><td colSpan={4}>No low stock items.</td></tr>}
                  </tbody>
                </table>
              </div>
            </div>

            {productsError && <div className="error">{productsError}</div>}
            {productsInfo && <div className="info">{productsInfo}</div>}
          </>
        )}
      </div>
    </div>
  );
}
