import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { AUTH_COOKIE } from "@/lib/auth";

/** W-J6: canonical FX UI is `/rate-cards/fx` (AppShell nav). */
export default async function AdminFxRatesRedirectPage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }
  redirect("/rate-cards/fx");
}
