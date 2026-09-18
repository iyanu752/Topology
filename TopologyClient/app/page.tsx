import { DesignCanvas } from "@/components/design/DesignCanvas";
import { WorkspaceShell } from "@/components/home/WorkspaceShell";

export default function Home() {
  return (
    <WorkspaceShell>
      <DesignCanvas />
    </WorkspaceShell>
  );
}
