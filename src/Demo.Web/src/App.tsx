import { FormEvent, useMemo, useState } from 'react';

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

export default function App() {
  const [text, setText] = useState('Hello Barcode');
  const [format, setFormat] = useState<BarcodeFormat>('QR_CODE');
  const [width, setWidth] = useState(300);
  const [height, setHeight] = useState(300);
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  const queryUrl = useMemo(() => {
    const url = new URL('/generate', apiBaseUrl);
    url.searchParams.set('text', text);
    url.searchParams.set('format', format);
    url.searchParams.set('width', String(width));
    url.searchParams.set('height', String(height));
    return url;
  }, [text, format, width, height]);

  const onGenerate = async (event: FormEvent) => {
    event.preventDefault();
    setError(null);

    if (!text.trim()) {
      setError('Please enter text for barcode generation.');
      return;
    }

    if (width < 64 || width > 2048 || height < 64 || height > 2048) {
      setError('Width and height must be between 64 and 2048.');
      return;
    }

    setIsLoading(true);
    try {
      const response = await fetch(queryUrl.toString());
      if (!response.ok) {
        const body = await response.text();
        setError(body || 'Request failed.');
        return;
      }

      const blob = await response.blob();
      const objectUrl = URL.createObjectURL(blob);
      setPreviewUrl((current) => {
        if (current) {
          URL.revokeObjectURL(current);
        }
        return objectUrl;
      });
    } catch {
      setError('Unable to connect to API. Check WebApi URL and availability.');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="page">
      <div className="card">
        <h1>Barcode Generator</h1>
        <p className="subtitle">Generate professional barcode images from the Demo.WebApi endpoint.</p>

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
                  <option key={item.value} value={item.value}>
                    {item.label}
                  </option>
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

          <button type="submit" disabled={isLoading}>{isLoading ? 'Generating...' : 'Generate barcode'}</button>
        </form>

        {error && <div className="error">{error}</div>}

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
      </div>
    </div>
  );
}
