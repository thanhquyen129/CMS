import { cookies } from "next/headers";
import Link from "next/link";
import { redirect, notFound } from "next/navigation";
import { AppShell } from "@/components/AppShell";
import { EditBusinessPartyForm } from "@/components/EditBusinessPartyForm";
import { PartyBankAccountsPanel } from "@/components/PartyBankAccountsPanel";
import { PartyContactsPanel } from "@/components/PartyContactsPanel";
import { PartyRolesPanel } from "@/components/PartyRolesPanel";
import { AUTH_COOKIE } from "@/lib/auth";
import { fetchTerminology, term } from "@/lib/api";
import {
  formatCreditLimit,
  getBusinessParty,
  listPartyBankAccounts,
  listPartyContacts,
  partyLabel,
  partyRoleLabel,
} from "@/lib/parties";

type Ctx = { params: Promise<{ id: string }> };

export default async function AdminPartyDetailPage({ params }: Ctx) {
  const jar = await cookies();
  if (!jar.get(AUTH_COOKIE)?.value) {
    redirect("/login");
  }

  const { id } = await params;
  const terms = await fetchTerminology();
  const dashboardLabel = term(terms, "DASHBOARD", "Bảng điều khiển");

  const [partyResult, banksResult, contactsResult] = await Promise.all([
    getBusinessParty(id),
    listPartyBankAccounts(id),
    listPartyContacts(id),
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
        <h1>{partyLabel(party)}</h1>
        <p className="lede">
          {party.isActive ? "Đang dùng" : "Ngừng"}
          {roles.length > 0
            ? ` · ${roles.map(partyRoleLabel).join(", ")}`
            : ""}
          {party.taxId ? ` · MST ${party.taxId}` : ""}
          {party.creditLimit != null
            ? ` · Hạn mức ${formatCreditLimit(party.creditLimit, party.creditLimitCurrencyCode)}`
            : ""}
        </p>

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

        <div className="layout-cols-2">
          <EditBusinessPartyForm party={party} />
          <div className="stack-panels">
            <PartyRolesPanel partyId={party.id} roleCodes={roles} />
            <PartyBankAccountsPanel partyId={party.id} accounts={banks} />
            <PartyContactsPanel partyId={party.id} contacts={contacts} />
          </div>
        </div>
      </section>
    </AppShell>
  );
}
