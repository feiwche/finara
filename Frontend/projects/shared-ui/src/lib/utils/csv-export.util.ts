// CSV formül enjeksiyonu (CSV/Formula Injection, OWASP) önlemi — bir hücre değeri
// =, +, -, @, TAB veya CR ile başlıyorsa Excel/LibreOffice bunu bir formül olarak
// yorumlayabilir. Bu değerler CSV içe aktarma (serbest metin Ad/Soyad) ya da başka bir
// serbest metin alanından (Detay/Mesaj) gelip sonradan "Dışa Aktar" ile tekrar CSV'ye
// yazılabildiği için, dışa aktarım noktasında (giriş noktasından bağımsız, tek yerden)
// zararsız hale getiriliyor — başına bir tek tırnak (') eklemek, formül yorumlanmasını
// engelleyip hücreyi düz metin olarak gösteriyor.
const FORMULA_TRIGGER_CHARS = ['=', '+', '-', '@', '\t', '\r'];

export function sanitizeCsvField(value: string): string {
  if (value.length > 0 && FORMULA_TRIGGER_CHARS.includes(value[0])) {
    return `'${value}`;
  }
  return value;
}
