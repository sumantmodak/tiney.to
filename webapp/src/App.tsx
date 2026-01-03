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

    // TODO: API call will go here in step 33
    console.log('Shortening URL:', url)

    // Temporary delay to simulate API call
    setTimeout(() => {
      console.log('Would call API here')
      setIsLoading(false)
    }, 1000)
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
        <h1 className="text-7xl font-black text-black mb-4 transform -rotate-1">
          <span className="inline-block bg-neo-blue text-white px-6 py-2 border-[5px] border-black shadow-neo-sm">
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
        {/* Footer Main Content */}
        <div className="bg-white border-[5px] border-black p-8 shadow-neo-md mb-8">
          <div className="grid grid-cols-1 md:grid-cols-4 gap-8">
            {/* About Section */}
            <div className="transform rotate-1">
              <div className="bg-neo-blue border-[4px] border-black px-4 py-2 mb-4 inline-block">
                <h3 className="text-2xl font-black text-white uppercase">About</h3>
              </div>
              <p className="font-bold text-black mb-4">
                Tiney.to is a FREE, BOLD, and UNAPOLOGETIC URL shortener that makes your links TINY and MIGHTY!
              </p>
              <p className="font-bold text-black text-sm">
                Est. 2025 🚀
              </p>
            </div>

            {/* Quick Links */}
            <div className="transform -rotate-1">
              <div className="bg-neo-pink border-[4px] border-black px-4 py-2 mb-4 inline-block">
                <h3 className="text-2xl font-black text-white uppercase">Links</h3>
              </div>
              <ul className="space-y-2">
                <li>
                  <a href="#" className="font-black text-black hover:text-neo-blue transition-colors uppercase text-sm border-b-4 border-transparent hover:border-neo-blue">
                    → Home
                  </a>
                </li>
                <li>
                  <a href="#" className="font-black text-black hover:text-neo-blue transition-colors uppercase text-sm border-b-4 border-transparent hover:border-neo-blue">
                    → Features
                  </a>
                </li>
                <li>
                  <a href="#" className="font-black text-black hover:text-neo-blue transition-colors uppercase text-sm border-b-4 border-transparent hover:border-neo-blue">
                    → API Docs
                  </a>
                </li>
                <li>
                  <a href="#" className="font-black text-black hover:text-neo-blue transition-colors uppercase text-sm border-b-4 border-transparent hover:border-neo-blue">
                    → Contact
                  </a>
                </li>
              </ul>
            </div>

            {/* Legal */}
            <div className="transform rotate-1">
              <div className="bg-neo-yellow border-[4px] border-black px-4 py-2 mb-4 inline-block">
                <h3 className="text-2xl font-black text-black uppercase">Legal</h3>
              </div>
              <ul className="space-y-2">
                <li>
                  <a href="#" className="font-black text-black hover:text-neo-pink transition-colors uppercase text-sm border-b-4 border-transparent hover:border-neo-pink">
                    → Privacy Policy
                  </a>
                </li>
                <li>
                  <a href="#" className="font-black text-black hover:text-neo-pink transition-colors uppercase text-sm border-b-4 border-transparent hover:border-neo-pink">
                    → Terms of Service
                  </a>
                </li>
                <li>
                  <a href="#" className="font-black text-black hover:text-neo-pink transition-colors uppercase text-sm border-b-4 border-transparent hover:border-neo-pink">
                    → Disclaimer
                  </a>
                </li>
                <li>
                  <a href="#" className="font-black text-black hover:text-neo-pink transition-colors uppercase text-sm border-b-4 border-transparent hover:border-neo-pink">
                    → Cookie Policy
                  </a>
                </li>
              </ul>
            </div>

            {/* Support */}
            <div className="transform -rotate-1">
              <div className="bg-neo-green border-[4px] border-black px-4 py-2 mb-4 inline-block">
                <h3 className="text-2xl font-black text-black uppercase">Support</h3>
              </div>
              <ul className="space-y-2">
                <li>
                  <a href="#" className="font-black text-black hover:text-neo-blue transition-colors uppercase text-sm border-b-4 border-transparent hover:border-neo-blue">
                    → Help Center
                  </a>
                </li>
                <li>
                  <a href="#" className="font-black text-black hover:text-neo-blue transition-colors uppercase text-sm border-b-4 border-transparent hover:border-neo-blue">
                    → FAQ
                  </a>
                </li>
                <li>
                  <a href="#" className="font-black text-black hover:text-neo-blue transition-colors uppercase text-sm border-b-4 border-transparent hover:border-neo-blue">
                    → Report Abuse
                  </a>
                </li>
                <li>
                  <a href="mailto:hello@tiney.to" className="font-black text-black hover:text-neo-blue transition-colors uppercase text-sm border-b-4 border-transparent hover:border-neo-blue">
                    → Email Us
                  </a>
                </li>
              </ul>
            </div>
          </div>
        </div>

        {/* Disclaimer Banner */}
        <div className="bg-neo-pink-light border-[5px] border-neo-pink p-6 shadow-neo-error mb-8">
          <div className="flex items-start gap-4">
            <div className="text-4xl flex-shrink-0">⚠️</div>
            <div>
              <h4 className="text-xl font-black text-neo-pink uppercase mb-2">Important Disclaimer</h4>
              <p className="font-bold text-black text-sm">
                Tiney.to IS CURRENTLY FREE FOR LIMITED RESTRICTED USAGE BOUND BY REASONABLE REQUEST RESTRICTIONS. 
                ALL THE LINKS WILL AUTO EXPIRE AFTER 30 DAYS IN ORDER TO KEEP THE LOAD LOW, COST IN CONTROL AND APP SNAPPY. 
                WE PLAN TO COME BACK WITH ADVANCED USAGE VERY SOON.... STAY TUNED! 🚀
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
              <a href="#" className="bg-white border-[4px] border-white px-4 py-2 font-black text-black uppercase hover:bg-neo-blue hover:text-white transition-colors">
                Twitter
              </a>
              <a href="#" className="bg-white border-[4px] border-white px-4 py-2 font-black text-black uppercase hover:bg-neo-pink hover:text-white transition-colors">
                GitHub
              </a>
              <a href="#" className="bg-white border-[4px] border-white px-4 py-2 font-black text-black uppercase hover:bg-neo-green hover:text-black transition-colors">
                Discord
              </a>
            </div>
          </div>
        </div>

        {/* Made With Love */}
        <div className="text-center mb-8">
          <div className="inline-block bg-neo-bright-yellow text-black px-8 py-4 border-[5px] border-black shadow-neo-xs transform -rotate-2">
            <p className="text-2xl font-black uppercase">
              Made In Hyderabad, India - For the world! 💥
            </p>
          </div>
        </div>
      </footer>

      {/* Temporary: Keep test section below */}
      <div className="max-w-2xl mx-auto">
        {/* Test State and Functions */}
        <div className="bg-white border-[5px] border-black p-6 shadow-neo-lg mb-6">
          <h2 className="text-2xl font-black mb-4">Function Tests</h2>
          
          {/* Test URL Input */}
          <div className="mb-4">
            <label className="block font-bold mb-2">Test URL Input:</label>
            <input
              type="text"
              value={url}
              onChange={(e) => setUrl(e.target.value)}
              placeholder="https://example.com"
              className="w-full px-4 py-2 border-[4px] border-black font-bold"
            />
            <p className="text-sm mt-1">Current: {url || 'empty'}</p>
          </div>

          <div className="flex flex-col gap-3">
            <button
              onClick={() => {
                console.log('Current state:', { url, shortenedUrl, isLoading, error, showQR })
              }}
              className="bg-neo-blue text-white px-4 py-2 font-bold border-[4px] border-black shadow-neo-sm hover:shadow-neo-md hover:-translate-y-1 hover:-translate-x-1 transition-all active:shadow-none active:translate-x-0 active:translate-y-0"
            >
              Log State
            </button>
            <button
              onClick={() => {
                setShortenedUrl('tiney.to/test123')
                console.log('Set test shortened URL')
              }}
              className="bg-neo-yellow text-black px-4 py-2 font-bold border-[4px] border-black shadow-neo-sm hover:shadow-neo-md hover:-translate-y-1 hover:-translate-x-1 transition-all active:shadow-none active:translate-x-0 active:translate-y-0"
            >
              Set Test URL
            </button>
            <button
              onClick={handleCopy}
              disabled={!shortenedUrl}
              className="bg-neo-pink text-white px-4 py-2 font-bold border-[4px] border-black shadow-neo-sm hover:shadow-neo-md hover:-translate-y-1 hover:-translate-x-1 transition-all active:shadow-none active:translate-x-0 active:translate-y-0 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              Test Copy Function
            </button>
            <button
              onClick={handleShorten}
              className="bg-neo-green text-black px-4 py-2 font-bold border-[4px] border-black shadow-neo-sm hover:shadow-neo-md hover:-translate-y-1 hover:-translate-x-1 transition-all active:shadow-none active:translate-x-0 active:translate-y-0"
            >
              Test Shorten Function
            </button>
          </div>
        </div>

        {/* Test Neo-Brutalism Colors and Shadows */}
        <div className="bg-white border-[5px] border-black p-8 shadow-neo-lg mb-6">
          <h1 className="text-4xl font-black text-black mb-4">
            Neo-Brutalism Test
          </h1>
          <p className="text-lg font-bold mb-4">
            Testing custom colors and shadows
          </p>
          
          {/* Test Buttons with Different Colors */}
          <div className="grid grid-cols-2 gap-4">
            <button className="bg-neo-blue text-white px-6 py-4 text-xl font-black border-[5px] border-black shadow-neo-sm hover:shadow-neo-md transition-all">
              Blue Button
            </button>
            <button className="bg-neo-yellow text-black px-6 py-4 text-xl font-black border-[5px] border-black shadow-neo-sm hover:shadow-neo-md transition-all">
              Yellow Button
            </button>
            <button className="bg-neo-pink text-white px-6 py-4 text-xl font-black border-[5px] border-black shadow-neo-sm hover:shadow-neo-md transition-all">
              Pink Button
            </button>
            <button className="bg-neo-green text-black px-6 py-4 text-xl font-black border-[5px] border-black shadow-neo-sm hover:shadow-neo-md transition-all">
              Green Button
            </button>
          </div>
        </div>

        {/* Test Colored Cards */}
        <div className="grid grid-cols-3 gap-4">
          <div className="bg-neo-pink-light border-[4px] border-black p-4 shadow-neo-xs">
            <p className="font-bold">Pink Light</p>
          </div>
          <div className="bg-neo-blue-light border-[4px] border-black p-4 shadow-neo-xs">
            <p className="font-bold">Blue Light</p>
          </div>
          <div className="bg-neo-bright-yellow border-[4px] border-black p-4 shadow-neo-xs">
            <p className="font-bold">Bright Yellow</p>
          </div>
        </div>
      </div>
    </div>
  )
}

export default App
