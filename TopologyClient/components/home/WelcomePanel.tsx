import Image from "next/image";

const welcomeActions = ["Open", "Help", "Live collaboration", "Sign up"];

export function WelcomePanel() {
  return (
    <section className="flex flex-1 items-center justify-center text-center">
      <div className="flex w-full max-w-lg flex-col items-center px-4">
        <Image
          src="/assets/brand/TopologyLogoGreen.png"
          alt="Topology"
          width={368}
          height={368}
          className="h-auto w-80 sm:w-[22rem]"
          priority
        />

        <p className="mt-8 max-w-md text-sm leading-6 text-zinc-400">
          Your designs are saved in your browser&apos;s storage. Browser storage can be cleared unexpectedly, so save your work to a file regularly to avoid losing it.
        </p>

        <WelcomeActions />
      </div>
    </section>
  );
}

function WelcomeActions() {
  return (
    <div className="mt-8 grid w-full max-w-sm grid-cols-2 gap-3 sm:max-w-none sm:grid-cols-4">
      {welcomeActions.map((action) => (
        <button
          key={action}
          type="button"
          className="rounded-md border border-zinc-800 bg-zinc-900 px-3 py-2 text-sm font-medium text-zinc-200 transition hover:border-emerald-400 hover:text-zinc-50 focus:outline-none focus:ring-2 focus:ring-emerald-400"
        >
          {action}
        </button>
      ))}
    </div>
  );
}

