function App() {
  return (
    <div className="min-h-screen bg-neo-bg p-8">
      <div className="max-w-2xl mx-auto">
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
