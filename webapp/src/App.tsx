import { useState } from 'react'
import { QRCodeSVG } from 'qrcode.react'
import { toast, Toaster } from 'sonner'
import { Copy, Link as LinkIcon, QrCode } from 'lucide-react'

function App() {
  // State management
  const [url, setUrl] = useState<string>('')
  const [shortenedUrl, setShortenedUrl] = useState<string>('')
  const [isLoading, setIsLoading] = useState<boolean>(false)
  const [error, setError] = useState<string>('')
  const [showQR, setShowQR] = useState<boolean>(false)

  // URL validation function
  const isValidUrl = (urlString: string): boolean => {
    try {
      const url = new URL(urlString)
      return url.protocol === 'https:'
    } catch {
      return false
    }
  }

  // Handle Enter key press
  const handleKeyPress = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !isLoading) {
      handleShorten()
    }
  }

  // Handle URL shortening
  const handleShorten = async () => {
    // Clear any previous errors
    setError('')

    // Validate URL
    if (!url.trim()) {
      setError('Please enter a URL')
      return
    }

    if (!isValidUrl(url)) {
      setError('Please enter a valid URL https://')
      return
    }

    // Set loading state
    setIsLoading(true)

    try {
      // Call the API to shorten the URL
      const response = await fetch(
        `${import.meta.env.VITE_API_BASE_URL}api/shorten`,
        {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({ longUrl: url }),
        }
      )

      // Handle successful response
      if (response.ok) {
        const data = await response.json()
        setShortenedUrl(data.shortUrl) // e.g., "tiney.to/abc123"
        setShowQR(false) // Reset QR visibility
        toast.success('URL shortened successfully! 🎉', {
          style: {
            background: '#00FF87',
            color: '#000000',
            border: '5px solid #000000',
            boxShadow: '8px 8px 0px #000000',
            fontWeight: 'bold',
            fontSize: '18px',
            textTransform: 'uppercase',
          },
        })
      } else {
        // Handle HTTP errors (4xx, 5xx)
        const errorData = await response.json().catch(() => ({}))
        const errorMessage = errorData.error || `HTTP ${response.status}`
        throw new Error(errorMessage)
      }
      
      setIsLoading(false)
    } catch (err) {
      // Handle network errors and HTTP errors
      const errorMessage = err instanceof Error ? err.message : 'Something went wrong. Please try again!'
      setError(errorMessage)
      toast.error('Failed to shorten URL! 😢', {
        style: {
          background: '#FFE6F0',
          color: '#FF006E',
          border: '5px solid #FF006E',
          boxShadow: '8px 8px 0px #FF006E',
          fontWeight: 'bold',
          fontSize: '18px',
          textTransform: 'uppercase',
        },
      })
      console.error('API Error:', err)
      setIsLoading(false)
    }
  }

  // Handle copy to clipboard
  const handleCopy = () => {
    navigator.clipboard.writeText(shortenedUrl)
    toast.success('Copied to clipboard!', {
      style: {
        background: '#00FF87',
        color: '#000000',
        border: '5px solid #000000',
        boxShadow: '8px 8px 0px #000000',
        fontWeight: 'bold',
        fontSize: '18px',
        textTransform: 'uppercase',
      },
    })
  }

  return (
    <div className="min-h-screen bg-neo-bg p-8">
      <Toaster position="top-center" />
      
      {/* Header */}
      <div className="max-w-4xl mx-auto mb-12">
        <h1 
          className="text-7xl font-black text-black mb-4 transform -rotate-1 cursor-pointer"
          onClick={() => {
            setUrl('')
            setShortenedUrl('')
            setError('')
            setShowQR(false)
          }}
        >
          <span className="inline-block bg-neo-blue text-white px-6 py-2 border-[5px] border-black shadow-neo-sm hover:shadow-neo-md hover:-translate-y-1 hover:-translate-x-1 transition-all active:shadow-none active:translate-x-0 active:translate-y-0">
            Tiney.to
          </span>
        </h1>
        <p className="text-2xl font-extrabold text-black transform rotate-1">
          Make URLs SHORT and SNAPPY! ⚡
        </p>
      </div>

      {/* Main Card */}
      <div className="max-w-4xl mx-auto mb-8">
        <div className="bg-white border-[5px] border-black p-8 shadow-neo-lg">
          {/* URL Input Section */}
          <div className="mb-8">
            <label className="block text-xl font-black text-black mb-3 uppercase tracking-wider">
              Paste your loooong URL here:
            </label>
            <div className="relative">
              <input
                type="text"
                value={url}
                onChange={(e) => {
                  setUrl(e.target.value)
                  setError('')
                }}
                onKeyPress={handleKeyPress}
                placeholder="https://example.com/your-very-long-url-here..."
                className={`w-full px-6 py-5 text-xl font-bold border-[5px] ${
                  error ? 'border-neo-pink' : 'border-black'
                } focus:outline-none focus:shadow-[8px_8px_0px_#0066FF] transition-shadow bg-white`}
              />
              <LinkIcon className="absolute right-6 top-1/2 -translate-y-1/2 w-8 h-8" />
            </div>
            {error && (
              <div className="mt-4 bg-neo-pink-light border-[4px] border-neo-pink p-4 shadow-neo-error">
                <p className="text-lg font-black text-neo-pink uppercase">⚠️ {error}</p>
              </div>
            )}
          </div>

          {/* Shorten Button */}
          <button
            onClick={handleShorten}
            disabled={isLoading}
            className={`w-full py-6 text-3xl font-black uppercase bg-neo-yellow text-black border-[5px] border-black shadow-neo-sm hover:shadow-neo-lg hover:-translate-y-1 hover:-translate-x-1 transition-all active:shadow-none active:translate-x-0 active:translate-y-0 ${
              isLoading ? 'opacity-70 cursor-not-allowed' : 'cursor-pointer'
            }`}
          >
            {isLoading ? '⏳ SHORTENING...' : '✨ SHORTEN IT!'}
          </button>
        </div>
      </div>

      {/* Result Card - Step 37-44 */}
      {shortenedUrl && (
        <div className="max-w-4xl mx-auto mb-8 animate-bounce-in">
          <div className="bg-neo-bright-yellow border-[5px] border-black p-8 shadow-neo-xl">
            {/* Success Badge - Step 38 */}
            <div className="flex items-center gap-3 mb-4">
              <div className="bg-neo-green border-[4px] border-black px-4 py-2">
                <span className="text-2xl font-black">🎉</span>
              </div>
              <h2 className="text-3xl font-black text-black uppercase">
                Your Short URL:
              </h2>
            </div>

            {/* Shortened URL Display - Step 39 */}
            <div className="bg-white border-[5px] border-black p-6 mb-6 shadow-neo-sm">
              <p className="text-3xl font-black text-neo-blue break-all">
                {shortenedUrl}
              </p>
            </div>

            {/* Action Buttons Grid - Step 40-42 */}
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              {/* Copy Button - Step 41 */}
              <button
                onClick={handleCopy}
                className="flex items-center justify-center gap-3 py-5 px-6 text-xl font-black uppercase bg-neo-pink text-white border-[5px] border-black shadow-neo-sm hover:shadow-neo-md hover:-translate-y-1 hover:-translate-x-1 transition-all active:shadow-none active:translate-x-0 active:translate-y-0"
              >
                <Copy size={28} />
                Copy URL
              </button>

              {/* Show QR Button - Step 42 */}
              <button
                onClick={() => setShowQR(!showQR)}
                className="flex items-center justify-center gap-3 py-5 px-6 text-xl font-black uppercase bg-neo-blue text-white border-[5px] border-black shadow-neo-sm hover:shadow-neo-md hover:-translate-y-1 hover:-translate-x-1 transition-all active:shadow-none active:translate-x-0 active:translate-y-0"
              >
                <QrCode size={28} />
                {showQR ? 'Hide QR' : 'Show QR'}
              </button>
            </div>

            {/* QR Code Section - Step 43-44 */}
            {showQR && (
              <div className="mt-6 bg-white border-[5px] border-black p-8 shadow-neo-sm animate-slide-down">
                <div className="flex justify-center">
                  <div className="bg-neo-blue-light border-[4px] border-black p-4 inline-block">
                    <QRCodeSVG
                      value={shortenedUrl}
                      size={200}
                      level="H"
                      includeMargin={true}
                    />
                  </div>
                </div>
              </div>
            )}
          </div>
        </div>
      )}

      {/* Feature Cards */}
      <div className="max-w-4xl mx-auto mb-12">
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
          {/* Card 1: Fast */}
          <div className="bg-neo-pink-light border-[5px] border-black p-6 shadow-neo-sm transform rotate-1">
            <div className="text-5xl mb-3">⚡</div>
            <h3 className="text-2xl font-black text-black mb-2 uppercase">FAST</h3>
            <p className="text-lg font-bold">Lightning quick URL shortening!</p>
          </div>
          
          {/* Card 2: Secure */}
          <div className="bg-neo-blue-light border-[5px] border-black p-6 shadow-neo-sm transform -rotate-1">
            <div className="text-5xl mb-3">🔒</div>
            <h3 className="text-2xl font-black text-black mb-2 uppercase">SECURE</h3>
            <p className="text-lg font-bold">Your links are safe with us!</p>
          </div>
          
          {/* Card 3: Bold */}
          <div className="bg-neo-bright-yellow border-[5px] border-black p-6 shadow-neo-sm transform rotate-1">
            <div className="text-5xl mb-3">🎨</div>
            <h3 className="text-2xl font-black text-black mb-2 uppercase">BOLD</h3>
            <p className="text-lg font-bold">Stand out with style!</p>
          </div>
        </div>
      </div>

      {/* Footer */}
      <footer className="max-w-6xl mx-auto mt-20">
        

        {/* Disclaimer Banner */}
        <div className="bg-neo-pink-light border-[5px] border-neo-pink p-6 shadow-neo-error mb-8">
          <div className="flex items-start gap-4">
            <div className="text-4xl flex-shrink-0">⚠️</div>
            <div>
              <h4 className="text-xl font-black text-neo-pink uppercase mb-2">Important Disclaimer</h4>
              <p className="font-bold text-black text-sm">
                TINEY IS CURRENTLY FREE FOR LIMITED & RESTRICTED USAGE. 
              </p>
            </div>
          </div>
        </div>

        {/* Bottom Bar */}
        <div className="bg-black border-[5px] border-black p-6 shadow-neo-blue-accent mb-8">
          <div className="flex flex-col md:flex-row justify-between items-center gap-4">
            <p className="text-xl font-black text-white uppercase">
              © 2025 Tiney.to - ALL RIGHTS RESERVED 💥
            </p>
            <div className="flex gap-4">
              <a href="https://github.com/sumantmodak/tiney.to" target="_blank" rel="noopener noreferrer" className="bg-white border-[4px] border-white px-4 py-2 font-black text-black uppercase hover:bg-neo-pink hover:text-white transition-colors">
                GitHub
              </a>
            </div>
          </div>
        </div>
      </footer>
    </div>
  )
}

export default App
