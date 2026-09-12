export type BusinessParty = {
  id: string;
  code: string;
  name: string;
  isActive: boolean;
  createdAt?: string;
};

export function partyLabel(p: Pick<BusinessParty, "code" | "name">): string {
  return `${p.code} — ${p.name}`;
}
