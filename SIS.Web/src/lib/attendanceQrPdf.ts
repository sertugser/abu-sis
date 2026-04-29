import type { AttendanceSession } from '../api/attendance.api';
import { downloadBlob } from './download';
import { createQrCodeMatrix, getQrCodeSize } from './qrCode';

const PAGE_WIDTH = 595;
const PAGE_HEIGHT = 842;

export function downloadAttendanceQrPdf(session: AttendanceSession, qrUrl: string) {
  const matrix = createQrCodeMatrix(qrUrl);
  const pdf = buildAttendanceQrPdf(session, qrUrl, matrix);
  const filename = `yoklama-qr-${sanitizeFilename(session.courseCode)}-sube-${session.sectionNumber}-hafta-${session.weekNumber}.pdf`;

  downloadBlob(
    {
      blob: new Blob([pdf], { type: 'application/pdf' }),
      filename,
    },
    filename
  );
}

function buildAttendanceQrPdf(session: AttendanceSession, qrUrl: string, matrix: boolean[][]) {
  const content = buildContentStream(session, qrUrl, matrix);
  const objects = [
    '<< /Type /Catalog /Pages 2 0 R >>',
    '<< /Type /Pages /Kids [3 0 R] /Count 1 >>',
    `<< /Type /Page /Parent 2 0 R /MediaBox [0 0 ${PAGE_WIDTH} ${PAGE_HEIGHT}] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>`,
    '<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>',
    `<< /Length ${content.length} >>\nstream\n${content}\nendstream`,
  ];

  let pdf = '%PDF-1.4\n';
  const offsets = [0];

  objects.forEach((object, index) => {
    offsets.push(pdf.length);
    pdf += `${index + 1} 0 obj\n${object}\nendobj\n`;
  });

  const xrefStart = pdf.length;
  pdf += `xref\n0 ${objects.length + 1}\n`;
  pdf += '0000000000 65535 f \n';
  offsets.slice(1).forEach(offset => {
    pdf += `${String(offset).padStart(10, '0')} 00000 n \n`;
  });
  pdf += `trailer\n<< /Size ${objects.length + 1} /Root 1 0 R >>\nstartxref\n${xrefStart}\n%%EOF`;

  return pdf;
}

function buildContentStream(session: AttendanceSession, qrUrl: string, matrix: boolean[][]) {
  const qrModuleCount = getQrCodeSize();
  const quietZone = 4;
  const moduleSize = 6;
  const qrTotalSize = (qrModuleCount + quietZone * 2) * moduleSize;
  const qrLeft = (PAGE_WIDTH - qrTotalSize) / 2;
  const qrBottom = 300;

  const lines = [
    '1 1 1 rg',
    `0 0 ${PAGE_WIDTH} ${PAGE_HEIGHT} re f`,
    '0 0 0 rg',
    textAt(72, 778, 22, 'Yoklama QR Kodu'),
    textAt(72, 748, 13, `${session.courseCode} - ${session.courseName}`),
    textAt(72, 728, 11, `Sube ${session.sectionNumber} | Hafta ${session.weekNumber} | Tarih ${formatDisplayDate(session.sessionDate)}`),
    textAt(72, 708, 11, session.qrExpiresAt ? `Bitis ${formatDisplayDateTime(session.qrExpiresAt)}` : 'QR suresi henuz acilmadi'),
    '1 1 1 rg',
    `${toPdfNumber(qrLeft)} ${qrBottom} ${qrTotalSize} ${qrTotalSize} re f`,
    '0 0 0 rg',
  ];

  matrix.forEach((row, y) => {
    row.forEach((isDark, x) => {
      if (!isDark) return;

      const rectX = qrLeft + (x + quietZone) * moduleSize;
      const rectY = qrBottom + (qrModuleCount - y - 1 + quietZone) * moduleSize;
      lines.push(`${toPdfNumber(rectX)} ${toPdfNumber(rectY)} ${moduleSize} ${moduleSize} re f`);
    });
  });

  lines.push(textAt(72, 240, 9, qrUrl));
  lines.push(textAt(72, 218, 9, 'Bu kodu okutan ogrenciler ad, soyad ve okul numarasi ile yoklamaya katilir.'));

  return `${lines.join('\n')}\n`;
}

function textAt(x: number, y: number, size: number, value: string) {
  return `BT /F1 ${size} Tf ${x} ${y} Td (${escapePdfText(value)}) Tj ET`;
}

function escapePdfText(value: string) {
  return toPdfAscii(value)
    .replace(/\\/g, '\\\\')
    .replace(/\(/g, '\\(')
    .replace(/\)/g, '\\)');
}

function toPdfAscii(value: string) {
  return value
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/[İıĞğÜüŞşÖöÇç]/g, character => {
      const map: Record<string, string> = {
        İ: 'I',
        ı: 'i',
        Ğ: 'G',
        ğ: 'g',
        Ü: 'U',
        ü: 'u',
        Ş: 'S',
        ş: 's',
        Ö: 'O',
        ö: 'o',
        Ç: 'C',
        ç: 'c',
      };
      return map[character] ?? character;
    })
    .replace(/[^\x20-\x7e]/g, '');
}

function formatDisplayDate(value: string) {
  return new Date(value).toLocaleDateString('tr-TR');
}

function formatDisplayDateTime(value: string) {
  return new Date(value).toLocaleString('tr-TR', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

function sanitizeFilename(value: string) {
  return toPdfAscii(value)
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '') || 'yoklama';
}

function toPdfNumber(value: number) {
  return Number.isInteger(value) ? String(value) : value.toFixed(2);
}
