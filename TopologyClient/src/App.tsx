function App() {
  return (
    <main className="min-h-screen bg-zinc-950 px-6 py-12 text-white">
      <section className="mx-auto flex max-w-4xl flex-col gap-6">
        <p className="text-sm font-medium uppercase text-cyan-300">
          React + TypeScript + Tailwind CSS
        </p>
        <h1 className="text-4xl font-semibold sm:text-6xl">
          Topology frontend is ready.
        </h1>
        <p className="max-w-2xl text-lg leading-8 text-zinc-300">
          Start building from <span className="font-mono text-cyan-200">src/App.tsx</span>.
          Tailwind is wired through Vite, and Docker is ready for local development or production builds.
        </p>
      </section>
    </main>
  )
}

export default App