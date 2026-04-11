export interface DownloadableFile {
  blob: Blob;
  filename?: string;
}

export function getFilenameFromContentDisposition(disposition?: string | null) {
  if (!disposition) {
    return undefined;
  }

  const utf8Match = disposition.match(/filename\*=UTF-8''([^;]+)/i);
  if (utf8Match?.[1]) {
    return decodeURIComponent(utf8Match[1]);
  }

  const basicMatch = disposition.match(/filename="?([^"]+)"?/i);
  return basicMatch?.[1];
}

export function downloadBlob(file: DownloadableFile, fallbackFilename: string) {
  const url = window.URL.createObjectURL(file.blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = file.filename ?? fallbackFilename;
  document.body.appendChild(anchor);
  anchor.click();
  document.body.removeChild(anchor);
  window.URL.revokeObjectURL(url);
}
