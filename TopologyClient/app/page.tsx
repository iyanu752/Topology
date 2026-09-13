import { ComponentLibrarySidebar } from "@/components/component-library/ComponentLibrarySidebar";

export default function Home() {
  return (
    <main className="min-h-screen bg-zinc-950 text-zinc-50">
      <ComponentLibrarySidebar />

      <section className="mx-auto flex min-h-screen w-full max-w-7xl flex-col px-6 py-6 pb-28 md:pb-6 md:pl-28">
        <header className="flex items-center justify-between border-b border-zinc-800 pb-4">
          <div>
            <p className="text-sm text-emerald-300">Topology</p>
            <h1 className="text-2xl font-semibold tracking-normal">System design simulator</h1>
          </div>
        </header>

        <section className="flex flex-1 items-center justify-center text-center text-sm text-zinc-500">
          Build locally without signing in. Sign in later to save designs or collaborate.
        </section>
      </section>
    </main>
  );
}
