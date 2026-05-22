import DOMPurify from 'dompurify';
import { useMemo } from 'react';

interface RichTextViewerProps {
  content: string;
  format?: 'html' | 'plain';
  className?: string;
}

const ALLOWED_TAGS = [
  'p', 'br', 'strong', 'em', 's', 'u', 'code', 'pre', 'blockquote',
  'h2', 'h3', 'h4',
  'ul', 'ol', 'li',
  'a', 'img',
  'table', 'thead', 'tbody', 'tr', 'td', 'th',
];

const ALLOWED_ATTR = ['href', 'src', 'alt', 'title', 'target', 'rel', 'class', 'colspan', 'rowspan'];

/**
 * HTML içeriği DOMPurify ile temizleyip render eder.
 * format="plain" ise pre-wrap textarea benzeri çıktı verir (geriye uyumluluk).
 */
export default function RichTextViewer({ content, format = 'html', className = '' }: RichTextViewerProps) {
  const sanitized = useMemo(() => {
    if (format !== 'html') return null;
    return DOMPurify.sanitize(content, {
      ALLOWED_TAGS,
      ALLOWED_ATTR,
      ALLOW_DATA_ATTR: false,
    });
  }, [content, format]);

  if (format !== 'html') {
    return (
      <div className={`text-sm whitespace-pre-wrap leading-relaxed ${className}`}>
        {content}
      </div>
    );
  }

  return (
    <div
      className={`rich-text-content ${className}`}
      // sanitized içerik DOMPurify'dan geliyor — XSS riski yok
      dangerouslySetInnerHTML={{ __html: sanitized ?? '' }}
    />
  );
}
