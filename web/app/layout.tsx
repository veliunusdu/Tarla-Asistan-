import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Tarla Asistanı | Uzman Paneli",
  description: "Ziraat mühendisleri için Tarla Asistanı yönetim paneli",
};

export default function RootLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="tr">
      <body>{children}</body>
    </html>
  );
}
