import { cookies } from "next/headers";
import Link from "next/link";
import { redirect, notFound } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { AuditTrailPanel } from "@/components/AuditTrailPanel";
import { EditBusinessPartyForm } from "@/components/EditBusinessPartyForm";
import { PartyBankAccountsPanel } from "@/components/PartyBankAccountsPanel";
import { PartyBlockActions } from "@/components/PartyBlockActions";
import { PartyContactsPanel } from "@/components/PartyContactsPanel";
import { PartyFinancialPanel } from "@/components/PartyFinancialPanel";
import { PartyRolesPanel } from "@/components/PartyRolesPanel";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import {
  formatCreditLimit,
  getBusinessParty,
  getPartyFinancial,
  listPartyBankAccounts,
  listPartyContacts,
  partyLabel,
  partyRoleLabel,
  partyStatusLabel,
} from "@/lib/parties";

type Ctx = {
  params: Promise<{ id: string }>;
  searchParams: Promise<{ tab?: string }>;
};

export default async function AdminPartyDetailPage({ params, searchParams }: Ctx) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const { id } = await params;
  const { tab: tabRaw } = await searchParams;
  const tab =
    tabRaw === "finance" ||
    tabRaw === "banks" ||
    tabRaw === "contacts" ||
    tabRaw === "roles" ||
    tabRaw === "history"
      ? tabRaw
      : "profile";
  const terms = await fetchTerminology();
  const dashboardLabel = term(terms, "DASHBOARD", "Bảng điều khiển");

  const [partyResult, banksResult, contactsResult, financeResult] = await Promise.all([
    getBusinessParty(id),
    listPartyBankAccounts(id),
    listPartyContacts(id),
    getPartyFinancial(id),
  ]);

  if (!partyResult.ok) {
    if (partyResult.status === 404) {
      notFound();
    }
    return (
      <AppShell terms={terms} active="admin">
        <section className="panel panel-wide">
          <p className="breadcrumb">
            <Link href="/dashboard">{dashboardLabel}</Link>
            {" / "}
            <Link href="/admin">Danh mục</Link>
            {" / "}
            <Link href="/admin/parties">Đối tác</Link>
          </p>
          <h1>Đối tác kinh doanh</h1>
          <div className="alert alert-error" role="alert">
            {partyResult.message}
          </div>
        </section>
      </AppShell>
    );
  }

  const party = partyResult.data;
  const roles = party.roleCodes ?? [];
  const banks = banksResult.ok ? banksResult.data : [];
  const contacts = contactsResult.ok ? contactsResult.data : [];
  const status = party.statusCode ?? (party.isActive ? "active" : "inactive");

  const tabs = [
    { id: "profile", label: "Hồ sơ" },
    { id: "finance", label: "Tài chính" },
    { id: "roles", label: "Vai trò" },
    { id: "banks", label: "Tài khoản NH" },
    { id: "contacts", label: "Liên hệ" },
    { id: "history", label: "Nhật ký" },
  ] as const;

  return (
    <AppShell terms={terms} active="admin">
      <section className="panel panel-wide">
        <p className="breadcrumb">
          <Link href="/dashboard">{dashboardLabel}</Link>
          {" / "}
          <Link href="/admin">Danh mục</Link>
          {" / "}
          <Link href="/admin/parties">Đối tác</Link>
          {" / "}
          {party.code}
        </p>
        <div className="page-header-row">
          <div>
            <h1>{partyLabel(party)}</h1>
            <p className="lede">
              <span className={`status-pill status-${status}`}>
                {partyStatusLabel(status, party.isActive)}
              </span>
              {roles.length > 0 ? ` · ${roles.map(partyRoleLabel).join(", ")}` : ""}
              {party.taxId ? ` · MST ${party.taxId}` : ""}
              {party.creditLimit != null
                ? ` · Hạn mức ${formatCreditLimit(party.creditLimit, party.creditLimitCurrencyCode)}`
                : ""}
            </p>
          </div>
          <Link className="btn btn-ghost" href="/admin/parties">
            ← Danh sách
          </Link>
        </div>

        {!banksResult.ok ? (
          <div className="alert alert-error" role="alert">
            {banksResult.message}
          </div>
        ) : null}
        {!contactsResult.ok ? (
          <div className="alert alert-error" role="alert">
            {contactsResult.message}
          </div>
        ) : null}

        <div className="hub-module-tabs" role="tablist" aria-label="Hồ sơ đối tác">
          {tabs.map((t) => (
            <Link
              key={t.id}
              href={t.id === "profile" ? `/admin/parties/${party.id}` : `/admin/parties/${party.id}?tab=${t.id}`}
              className={`hub-module-tab${tab === t.id ? " is-active" : ""}`}
              role="tab"
              aria-selected={tab === t.id}
            >
              <strong>{t.label}</strong>
            </Link>
          ))}
        </div>

        {tab === "profile" ? (
          <div className="layout-cols-2" style={{ marginTop: "1rem" }}>
            <EditBusinessPartyForm party={party} />
            <PartyBlockActions party={party} />
          </div>
        ) : null}

        {tab === "finance" ? (
          <div style={{ marginTop: "1rem" }}>
            {!financeResult.ok ? (
              <div className="alert alert-error" role="alert">
                {financeResult.message}
              </div>
            ) : (
              <PartyFinancialPanel view={financeResult.data} />
            )}
          </div>
        ) : null}

        {tab === "roles" ? (
          <div style={{ marginTop: "1rem" }}>
            <PartyRolesPanel partyId={party.id} roleCodes={roles} />
          </div>
        ) : null}

        {tab === "banks" ? (
          <div style={{ marginTop: "1rem" }}>
            <PartyBankAccountsPanel partyId={party.id} accounts={banks} />
          </div>
        ) : null}

        {tab === "contacts" ? (
          <div style={{ marginTop: "1rem" }}>
            <PartyContactsPanel partyId={party.id} contacts={contacts} />
          </div>
        ) : null}

        {tab === "history" ? (
          <div style={{ marginTop: "1rem" }}>
            <AuditTrailPanel
              terms={terms}
              objectType="business_party"
              objectId={party.id}
              title="Nhật ký hồ sơ đối tác"
            />
          </div>
        ) : null}
      </section>
    </AppShell>
  );
}
