export type TerminologyMap = Record<string, string>;

export function term(map: TerminologyMap, key: string, fallback: string): string {
  return map[key] ?? fallback;
}
