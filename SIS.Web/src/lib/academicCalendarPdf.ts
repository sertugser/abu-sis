import { jsPDF } from 'jspdf';
import { downloadBlob } from './download';

const PDF_PAGE_WIDTH = 595;
const PDF_PAGE_HEIGHT = 842;
const CANVAS_SCALE = 2;
const CANVAS_WIDTH = PDF_PAGE_WIDTH * CANVAS_SCALE;
const CANVAS_HEIGHT = PDF_PAGE_HEIGHT * CANVAS_SCALE;
const PAGE_MARGIN = 28 * CANVAS_SCALE;
const PAGE_TOP = 74 * CANVAS_SCALE;
const PAGE_BOTTOM = CANVAS_HEIGHT - PAGE_MARGIN;
const SECTION_HEADER_HEIGHT = 28 * CANVAS_SCALE;
const SECTION_GAP = 10 * CANVAS_SCALE;
const ROW_MIN_HEIGHT = 34 * CANVAS_SCALE;
const ROW_VERTICAL_PADDING = 10 * CANVAS_SCALE;
const ROW_HORIZONTAL_PADDING = 12 * CANVAS_SCALE;
const ROW_LINE_HEIGHT = 11 * CANVAS_SCALE;
const DATE_COLUMN_WIDTH = 150 * CANVAS_SCALE;
const DOCUMENT_TITLE_SIZE = 16 * CANVAS_SCALE;
const DOCUMENT_META_SIZE = 9 * CANVAS_SCALE;
const SECTION_TITLE_SIZE = 13 * CANVAS_SCALE;
const BODY_TEXT_SIZE = 10 * CANVAS_SCALE;
const ZEBRA_EVEN = '#f2f4f7';
const ZEBRA_ODD = '#fbfcfd';
const TITLE_COLOR = '#0b3567';
const MUTED_COLOR = '#7a8798';
const BODY_COLOR = '#2f3640';
const DATE_COLOR = '#16457b';

export interface AcademicCalendarPdfRow {
  dateText: string;
  title: string;
}

export interface AcademicCalendarPdfSection {
  title: string;
  rows: AcademicCalendarPdfRow[];
}

export function downloadAcademicCalendarPdf(options: {
  documentTitle: string;
  fileName: string;
  sections: AcademicCalendarPdfSection[];
}) {
  if (typeof document === 'undefined' || options.sections.length === 0)
    return;

  const blob = buildAcademicCalendarPdf(options.documentTitle, options.sections);
  downloadBlob(
    {
      blob,
      filename: options.fileName,
    },
    options.fileName,
  );
}

function buildAcademicCalendarPdf(documentTitle: string, sections: AcademicCalendarPdfSection[]) {
  const pages = buildPageCanvases(documentTitle, sections);
  const doc = new jsPDF({
    orientation: 'portrait',
    unit: 'pt',
    format: [PDF_PAGE_WIDTH, PDF_PAGE_HEIGHT],
    compress: true,
  });

  pages.forEach((page, index) => {
    if (index > 0)
      doc.addPage([PDF_PAGE_WIDTH, PDF_PAGE_HEIGHT], 'portrait');

    doc.addImage(page, 'JPEG', 0, 0, PDF_PAGE_WIDTH, PDF_PAGE_HEIGHT, undefined, 'FAST');
  });

  return doc.output('blob');
}

function buildPageCanvases(documentTitle: string, sections: AcademicCalendarPdfSection[]) {
  const pages: HTMLCanvasElement[] = [];
  let page = createPageCanvas(documentTitle);
  let context = getCanvasContext(page);
  let y = PAGE_TOP;

  const nextPage = () => {
    pages.push(page);
    page = createPageCanvas(documentTitle);
    context = getCanvasContext(page);
    y = PAGE_TOP;
  };

  sections.forEach(section => {
    const rows = section.rows.length > 0
      ? section.rows
      : [{ dateText: '-', title: 'Bu donem icin takvim etkinligi tanimli degil.' }];

    let rowIndex = 0;
    let stripeIndex = 0;

    while (rowIndex < rows.length) {
      const firstRowLayout = measureRow(context, rows[rowIndex]);
      if (y + SECTION_HEADER_HEIGHT + firstRowLayout.height > PAGE_BOTTOM)
        nextPage();

      drawSectionHeader(context, y, section.title);
      y += SECTION_HEADER_HEIGHT;

      while (rowIndex < rows.length) {
        const rowLayout = measureRow(context, rows[rowIndex]);
        if (y + rowLayout.height > PAGE_BOTTOM) {
          nextPage();
          break;
        }

        drawRow(context, y, rowLayout, stripeIndex % 2 === 0);
        y += rowLayout.height;
        rowIndex += 1;
        stripeIndex += 1;
      }

      y += SECTION_GAP;
    }
  });

  pages.push(page);
  return pages;
}

function createPageCanvas(documentTitle: string) {
  const canvas = document.createElement('canvas');
  canvas.width = CANVAS_WIDTH;
  canvas.height = CANVAS_HEIGHT;

  const context = getCanvasContext(canvas);
  context.fillStyle = '#ffffff';
  context.fillRect(0, 0, canvas.width, canvas.height);

  context.fillStyle = TITLE_COLOR;
  context.font = `700 ${DOCUMENT_TITLE_SIZE}px "Segoe UI", Arial, sans-serif`;
  context.textBaseline = 'top';
  context.fillText(documentTitle, PAGE_MARGIN, PAGE_MARGIN);

  context.fillStyle = MUTED_COLOR;
  context.font = `400 ${DOCUMENT_META_SIZE}px "Segoe UI", Arial, sans-serif`;
  context.fillText(`Olusturma tarihi: ${formatGeneratedAt()}`, PAGE_MARGIN, PAGE_MARGIN + DOCUMENT_TITLE_SIZE + (6 * CANVAS_SCALE));

  context.strokeStyle = '#d7dde5';
  context.lineWidth = 1;
  const ruleY = PAGE_TOP - (18 * CANVAS_SCALE);
  context.beginPath();
  context.moveTo(PAGE_MARGIN, ruleY);
  context.lineTo(CANVAS_WIDTH - PAGE_MARGIN, ruleY);
  context.stroke();

  return canvas;
}

function drawSectionHeader(context: CanvasRenderingContext2D, y: number, title: string) {
  context.fillStyle = TITLE_COLOR;
  context.fillRect(PAGE_MARGIN, y, CANVAS_WIDTH - PAGE_MARGIN * 2, SECTION_HEADER_HEIGHT);

  context.fillStyle = '#ffffff';
  context.font = `700 ${SECTION_TITLE_SIZE}px "Segoe UI", Arial, sans-serif`;
  context.textBaseline = 'middle';
  context.textAlign = 'center';
  context.fillText(title, CANVAS_WIDTH / 2, y + SECTION_HEADER_HEIGHT / 2);
  context.textAlign = 'left';
  context.textBaseline = 'top';
}

function drawRow(context: CanvasRenderingContext2D, y: number, row: MeasuredRow, isEven: boolean) {
  const width = CANVAS_WIDTH - PAGE_MARGIN * 2;

  context.fillStyle = isEven ? ZEBRA_EVEN : ZEBRA_ODD;
  context.fillRect(PAGE_MARGIN, y, width, row.height);

  context.font = `400 ${BODY_TEXT_SIZE}px "Segoe UI", Arial, sans-serif`;
  context.fillStyle = DATE_COLOR;
  row.dateLines.forEach((line, index) => {
    context.fillText(
      line,
      PAGE_MARGIN + ROW_HORIZONTAL_PADDING,
      y + ROW_VERTICAL_PADDING + index * ROW_LINE_HEIGHT,
    );
  });

  context.fillStyle = BODY_COLOR;
  row.titleLines.forEach((line, index) => {
    context.fillText(
      line,
      PAGE_MARGIN + DATE_COLUMN_WIDTH + ROW_HORIZONTAL_PADDING,
      y + ROW_VERTICAL_PADDING + index * ROW_LINE_HEIGHT,
    );
  });
}

type MeasuredRow = AcademicCalendarPdfRow & {
  dateLines: string[];
  titleLines: string[];
  height: number;
};

function measureRow(context: CanvasRenderingContext2D, row: AcademicCalendarPdfRow): MeasuredRow {
  context.font = `400 ${BODY_TEXT_SIZE}px "Segoe UI", Arial, sans-serif`;

  const dateLines = wrapText(context, row.dateText, DATE_COLUMN_WIDTH - ROW_HORIZONTAL_PADDING * 2);
  const titleLines = wrapText(
    context,
    row.title,
    CANVAS_WIDTH - PAGE_MARGIN * 2 - DATE_COLUMN_WIDTH - ROW_HORIZONTAL_PADDING * 2,
  );

  const lineCount = Math.max(dateLines.length, titleLines.length, 1);
  const contentHeight = lineCount * ROW_LINE_HEIGHT + ROW_VERTICAL_PADDING * 2;

  return {
    ...row,
    dateLines,
    titleLines,
    height: Math.max(ROW_MIN_HEIGHT, contentHeight),
  };
}

function wrapText(context: CanvasRenderingContext2D, text: string, maxWidth: number) {
  if (!text.trim())
    return ['-'];

  const words = text.trim().split(/\s+/);
  const lines: string[] = [];
  let current = words[0] ?? '';

  for (let index = 1; index < words.length; index += 1) {
    const candidate = `${current} ${words[index]}`;
    if (context.measureText(candidate).width <= maxWidth) {
      current = candidate;
      continue;
    }

    lines.push(current);
    current = words[index];
  }

  if (current)
    lines.push(current);

  return lines.length > 0 ? lines : ['-'];
}

function getCanvasContext(canvas: HTMLCanvasElement) {
  const context = canvas.getContext('2d');
  if (!context)
    throw new Error('Canvas context could not be created.');
  return context;
}

function formatGeneratedAt() {
  return new Date().toLocaleString('tr-TR', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}
