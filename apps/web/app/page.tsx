import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { AUTH_COOKIE } from "@/lib/auth";
import { UI_PREFS_COOKIE, parseUiPreferences } from "@/lib/ui-preferences";

/** Logged-in home → user-preferred landing (Settings). */
export default async function HomePage() {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }
  const raw = jar.get(UI_PREFS_COOKIE)?.value;
  const prefs = parseUiPreferences(raw ? decodeURIComponent(raw) : null);
  redirect(prefs.homePath);
}
