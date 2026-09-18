import type { ReactNode } from "react";
import { ComponentLibrarySidebar } from "@/components/component-library/ComponentLibrarySidebar";

type WorkspaceShellProps = {
  children: ReactNode;
};

export function WorkspaceShell({ children }: WorkspaceShellProps) {
  return (
    <main className="min-h-screen bg-zinc-950 text-zinc-50">
      <ComponentLibrarySidebar />

      <section className="min-h-screen pb-28 md:pb-0 md:pl-24">{children}</section>
    </main>
  );
}
