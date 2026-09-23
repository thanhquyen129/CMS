export const PARTY_CSV_SAMPLE = [
  "code,name,taxId,roleCodes,partyKind,countryCode,defaultCurrencyCode,paymentTermDays,creditLimit,phone,email,addressLine1,city,bankName,bankAccountNumber,bankAccountName,contactName,contactPhone,contactEmail,contactFunction",
  'KH-MAU,"Công ty TNHH ABC, chi nhánh",0312345678,Khách hàng;Bên trả tiền,Tổ chức,VN,VND,30,500000000,02873001234,ketoan@abc.example,"12 Nguyễn Huệ, Quận 1",Hồ Chí Minh,Vietcombank,0123456789,CONG TY TNHH ABC,Nguyễn Văn An,0903123456,an.nv@abc.example,Kế toán',
  "NCC-MAU,Công ty CP Vận tải Biển,0309876543,Nhà cung cấp;Bên nhận tiền,Tổ chức,VN,VND,15,,02839001111,ap@bien.example,45 Lê Lợi,Hồ Chí Minh,ACB,9876543210,CONG TY CP VAN TAI BIEN,Trần Thị Bình,0918123456,binh.tt@bien.example,Kế toán",
].join("\n");

export type PartyCsvRow = {
  code: string;
  name: string;
  taxId?: string | null;
  roleCodes?: string[] | null;
  isCustomer?: boolean | null;
  isVendor?: boolean | null;
  isPayer?: boolean | null;
  isPayee?: boolean | null;
  legalName?: string | null;
  phone?: string | null;
  email?: string | null;
  partyKind?: string | null;
  countryCode?: string | null;
  defaultCurrencyCode?: string | null;
  paymentTermDays?: number | null;
  creditLimit?: number | null;
  groupCode?: string | null;
  externalCode?: string | null;
  shortName?: string | null;
  notes?: string | null;
  addressLine1?: string | null;
  city?: string | null;
  province?: string | null;
  bankName?: string | null;
  bankAccountNumber?: string | null;
  bankAccountName?: string | null;
  bankCurrencyCode?: string | null;
  contactName?: string | null;
  contactPhone?: string | null;
  contactEmail?: string | null;
  contactFunction?: string | null;
};

const HEADER_ALIASES: Record<string, keyof PartyCsvRow> = {
  code: "code",
  ma: "code",
  madoitac: "code",
  name: "name",
  ten: "name",
  tendoitac: "name",
  taxid: "taxId",
  mst: "taxId",
  masothue: "taxId",
  rolecodes: "roleCodes",
  vaitro: "roleCodes",
  iscustomer: "isCustomer",
  khachhang: "isCustomer",
  isvendor: "isVendor",
  nhacungcap: "isVendor",
  ispayer: "isPayer",
  bentratien: "isPayer",
  ispayee: "isPayee",
  bennhantien: "isPayee",
  legalname: "legalName",
  tenphaply: "legalName",
  phone: "phone",
  dienthoai: "phone",
  sdt: "phone",
  email: "email",
  partykind: "partyKind",
  loai: "partyKind",
  loaidoitac: "partyKind",
  countrycode: "countryCode",
  quocgia: "countryCode",
  defaultcurrencycode: "defaultCurrencyCode",
  tiente: "defaultCurrencyCode",
  paymenttermdays: "paymentTermDays",
  hanthanhtoan: "paymentTermDays",
  creditlimit: "creditLimit",
  hanmuc: "creditLimit",
  groupcode: "groupCode",
  nhom: "groupCode",
  externalcode: "externalCode",
  madoichieu: "externalCode",
  shortname: "shortName",
  tenviettat: "shortName",
  notes: "notes",
  ghichu: "notes",
  addressline1: "addressLine1",
  diachi: "addressLine1",
  city: "city",
  thanhpho: "city",
  province: "province",
  tinh: "province",
  bankname: "bankName",
  nganhang: "bankName",
  bankaccountnumber: "bankAccountNumber",
  sotaikhoan: "bankAccountNumber",
  stk: "bankAccountNumber",
  bankaccountname: "bankAccountName",
  tentaikhoan: "bankAccountName",
  bankcurrencycode: "bankCurrencyCode",
  tientetaikhoan: "bankCurrencyCode",
  contactname: "contactName",
  nguoilienhe: "contactName",
  contactphone: "contactPhone",
  sdtlienhe: "contactPhone",
  contactemail: "contactEmail",
  emaillienhe: "contactEmail",
  contactfunction: "contactFunction",
  chucnanglienhe: "contactFunction",
};

export function parsePartyCsv(text: string): { rows: PartyCsvRow[]; parseErrors: string[] } {
  const records = splitRecords(text.replace(/^\uFEFF/, ""));
  if (records.length === 0) return { rows: [], parseErrors: ["File trống."] };

  const header = records[0].map(foldKey);
  const index = new Map<keyof PartyCsvRow, number>();
  header.forEach((cell, i) => {
    const key = HEADER_ALIASES[cell];
    if (key && !index.has(key)) index.set(key, i);
  });
  if (!index.has("code") || !index.has("name")) {
    return {
      rows: [],
      parseErrors: ["Thiếu cột Mã và Tên. Tải file mẫu để lấy đúng header."],
    };
  }

  const rows: PartyCsvRow[] = [];
  for (let i = 1; i < records.length; i++) {
    const cols = records[i];
    if (cols.every((c) => !c)) continue;
    const cell = (key: keyof PartyCsvRow) => {
      const at = index.get(key);
      if (at == null) return "";
      return cols[at]?.trim() ?? "";
    };
    const code = cell("code");
    const name = cell("name");
    if (!code && !name) continue;

    const roleRaw = cell("roleCodes");
    rows.push({
      code,
      name,
      taxId: empty(cell("taxId")),
      roleCodes: roleRaw
        ? roleRaw.split(/[;|]/).map((r) => r.trim()).filter(Boolean)
        : null,
      isCustomer: flag(cell("isCustomer")),
      isVendor: flag(cell("isVendor")),
      isPayer: flag(cell("isPayer")),
      isPayee: flag(cell("isPayee")),
      legalName: empty(cell("legalName")),
      phone: empty(cell("phone")),
      email: empty(cell("email")),
      partyKind: empty(cell("partyKind")),
      countryCode: empty(cell("countryCode")),
      defaultCurrencyCode: empty(cell("defaultCurrencyCode")),
      paymentTermDays: integer(cell("paymentTermDays")),
      creditLimit: number(cell("creditLimit")),
      groupCode: empty(cell("groupCode")),
      externalCode: empty(cell("externalCode")),
      shortName: empty(cell("shortName")),
      notes: empty(cell("notes")),
      addressLine1: empty(cell("addressLine1")),
      city: empty(cell("city")),
      province: empty(cell("province")),
      bankName: empty(cell("bankName")),
      bankAccountNumber: empty(cell("bankAccountNumber")),
      bankAccountName: empty(cell("bankAccountName")),
      bankCurrencyCode: empty(cell("bankCurrencyCode")),
      contactName: empty(cell("contactName")),
      contactPhone: empty(cell("contactPhone")),
      contactEmail: empty(cell("contactEmail")),
      contactFunction: empty(cell("contactFunction")),
    });
  }

  if (rows.length === 0) {
    return { rows: [], parseErrors: ["Không có dòng dữ liệu sau header."] };
  }
  return { rows, parseErrors: [] };
}

function empty(value: string): string | null {
  return value ? value : null;
}

function flag(value: string): boolean | null {
  if (!value) return null;
  const v = foldKey(value);
  if (["1", "true", "yes", "y", "x", "co"].includes(v)) return true;
  if (["0", "false", "no", "n", "khong"].includes(v)) return false;
  return null;
}

function number(value: string): number | null {
  if (!value) return null;
  let raw = value.replace(/\s/g, "");
  if (/^\d{1,3}(\.\d{3})+$/.test(raw)) raw = raw.replace(/\./g, "");
  else if (/^\d{1,3}(,\d{3})+$/.test(raw)) raw = raw.replace(/,/g, "");
  else raw = raw.replace(",", ".");
  const n = Number(raw);
  return Number.isFinite(n) ? n : null;
}

function integer(value: string): number | null {
  const n = number(value);
  return n == null ? null : Math.trunc(n);
}

function foldKey(value: string): string {
  return value
    .trim()
    .toLowerCase()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/đ/g, "d")
    .replace(/[^a-z0-9]/g, "");
}

function splitRecords(text: string): string[][] {
  const delimiter = detectDelimiter(text);
  const rows: string[][] = [];
  let row: string[] = [];
  let cell = "";
  let quoted = false;
  for (let i = 0; i < text.length; i++) {
    const c = text[i];
    if (quoted) {
      if (c === '"') {
        if (text[i + 1] === '"') {
          cell += '"';
          i++;
        } else {
          quoted = false;
        }
      } else {
        cell += c;
      }
      continue;
    }
    if (c === '"') {
      quoted = true;
      continue;
    }
    if (c === delimiter) {
      row.push(cell.trim());
      cell = "";
      continue;
    }
    if (c === "\n" || c === "\r") {
      if (c === "\r" && text[i + 1] === "\n") i++;
      row.push(cell.trim());
      if (row.some((part) => part.length > 0)) rows.push(row);
      row = [];
      cell = "";
      continue;
    }
    cell += c;
  }
  row.push(cell.trim());
  if (row.some((part) => part.length > 0)) rows.push(row);
  return rows;
}

function detectDelimiter(text: string): "," | ";" | "\t" {
  let commas = 0;
  let semis = 0;
  let tabs = 0;
  let quoted = false;
  for (let i = 0; i < text.length; i++) {
    const c = text[i];
    if (c === '"') {
      if (quoted && text[i + 1] === '"') i++;
      else quoted = !quoted;
      continue;
    }
    if (quoted) continue;
    if (c === "\n" || c === "\r") break;
    if (c === ",") commas++;
    else if (c === ";") semis++;
    else if (c === "\t") tabs++;
  }
  if (semis > commas && semis >= tabs) return ";";
  if (tabs > commas && tabs > semis) return "\t";
  return ",";
}
