import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Topology",
  description: "Design and simulate system architecture."
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="en">
      <body>{children}</body>
    </html>
  );
}
