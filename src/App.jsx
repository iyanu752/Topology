function App() {
  return (
    <main className="min-h-screen bg-zinc-950 px-6 py-12 text-white">
      <section className="mx-auto flex max-w-4xl flex-col gap-6">
        <p className="text-sm font-medium uppercase tracking-widest text-cyan-300">
          React + Tailwind CSS
        </p>
        <h1 className="text-4xl font-semibold tracking-tight sm:text-6xl">
          Topology frontend is ready.
        </h1>
        <p className="max-w-2xl text-lg leading-8 text-zinc-300">
          Start building from <span className="font-mono text-cyan-200">src/App.jsx</span>.
          Tailwind classes are already wired through Vite.
        </p>
        <div className="flex flex-wrap gap-3">
          <a
            className="rounded-md bg-cyan-300 px-4 py-2 font-semibold text-zinc-950 transition hover:bg-cyan-200"
            href="https://react.dev"
            target="_blank"
            rel="noreferrer"
          >
            React docs
          </a>
          <a
            className="rounded-md border border-zinc-700 px-4 py-2 font-semibold text-zinc-100 transition hover:border-zinc-500 hover:bg-zinc-900"
            href="https://tailwindcss.com/docs"
            target="_blank"
            rel="noreferrer"
          >
            Tailwind docs
          </a>
        </div>
      </section>
    </main>
  )
}

export default App
