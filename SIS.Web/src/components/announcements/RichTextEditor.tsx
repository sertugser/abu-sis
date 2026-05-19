import { useEditor, EditorContent, type Editor } from '@tiptap/react';
import type { AnyExtension } from '@tiptap/core';
import StarterKit from '@tiptap/starter-kit';
import Link from '@tiptap/extension-link';
import Image from '@tiptap/extension-image';
import { TableKit } from '@tiptap/extension-table';
import { useEffect } from 'react';
import {
  Bold,
  Italic,
  Strikethrough,
  Code,
  List,
  ListOrdered,
  Quote,
  Heading2,
  Heading3,
  Link as LinkIcon,
  Image as ImageIcon,
  Table as TableIcon,
  Undo,
  Redo,
} from 'lucide-react';

interface RichTextEditorProps {
  value: string;
  onChange: (html: string) => void;
  placeholder: string;
  maxLength: number;
  allowImages: boolean;
  allowTables: boolean;
}

export default function RichTextEditor({
  value,
  onChange,
  placeholder,
  maxLength,
  allowImages,
  allowTables,
}: RichTextEditorProps) {
  const extensions: AnyExtension[] = [
    StarterKit.configure({
      heading: { levels: [2, 3] },
    }),
    Link.configure({
      openOnClick: false,
      HTMLAttributes: { class: 'text-primary underline' },
    }),
  ];

  if (allowImages) {
    extensions.push(
      Image.configure({
        HTMLAttributes: { class: 'rounded-md max-w-full' },
      })
    );
  }

  if (allowTables) {
    extensions.push(
      TableKit.configure({
        table: { resizable: false },
      })
    );
  }

  const editor = useEditor({
    extensions,
    content: value || '',
    onUpdate: ({ editor }) => {
      const html = editor.getHTML();
      if (html.length <= maxLength) {
        onChange(html);
      }
    },
    editorProps: {
      attributes: {
        class: 'rich-text-content min-h-[200px] focus:outline-none px-3 py-2',
        'data-placeholder': placeholder,
      },
    },
  });

  // Dışarıdan value değişirse editor'ü güncelle (örn. düzenleme modunda mevcut veri)
  useEffect(() => {
    if (!editor) return;
    const current = editor.getHTML();
    if (value !== current) {
      editor.commands.setContent(value || '', { emitUpdate: false });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [value, editor]);

  if (!editor) {
    return (
      <div className="border border-input rounded-md min-h-[240px] bg-card animate-pulse" />
    );
  }

  return (
    <div className="border border-input rounded-md bg-card focus-within:ring-2 focus-within:ring-ring focus-within:ring-offset-2">
      <Toolbar editor={editor} allowImages={allowImages} allowTables={allowTables} />
      <EditorContent editor={editor} />
      <div className="text-[11px] text-muted-foreground px-3 py-1.5 border-t border-border/60 flex justify-between">
        <span>
          {editor.getText().length} / {maxLength} karakter
        </span>
        <span>HTML formatı</span>
      </div>
    </div>
  );
}

interface ToolbarProps {
  editor: Editor;
  allowImages: boolean;
  allowTables: boolean;
}

function Toolbar({ editor, allowImages, allowTables }: ToolbarProps) {
  const btn = (active: boolean) =>
    `p-1.5 rounded hover:bg-muted transition-colors ${
      active ? 'bg-muted text-primary' : 'text-muted-foreground'
    }`;

  function addLink() {
    const previous = editor.getAttributes('link').href as string | undefined;
    const url = window.prompt('Bağlantı URL', previous ?? '');
    if (url === null) return;
    if (url === '') {
      editor.chain().focus().extendMarkRange('link').unsetLink().run();
      return;
    }
    editor.chain().focus().extendMarkRange('link').setLink({ href: url }).run();
  }

  function addImage() {
    const url = window.prompt('Resim URL');
    if (!url) return;
    editor.chain().focus().setImage({ src: url }).run();
  }

  function addTable() {
    editor.chain().focus().insertTable({ rows: 3, cols: 3, withHeaderRow: true }).run();
  }

  return (
    <div className="flex flex-wrap items-center gap-0.5 px-2 py-1.5 border-b border-border/60 bg-muted/30">
      <button
        type="button"
        onClick={() => editor.chain().focus().toggleBold().run()}
        className={btn(editor.isActive('bold'))}
        title="Kalın (Ctrl+B)"
      >
        <Bold className="h-4 w-4" />
      </button>
      <button
        type="button"
        onClick={() => editor.chain().focus().toggleItalic().run()}
        className={btn(editor.isActive('italic'))}
        title="İtalik (Ctrl+I)"
      >
        <Italic className="h-4 w-4" />
      </button>
      <button
        type="button"
        onClick={() => editor.chain().focus().toggleStrike().run()}
        className={btn(editor.isActive('strike'))}
        title="Üstü çizili"
      >
        <Strikethrough className="h-4 w-4" />
      </button>
      <button
        type="button"
        onClick={() => editor.chain().focus().toggleCode().run()}
        className={btn(editor.isActive('code'))}
        title="Kod"
      >
        <Code className="h-4 w-4" />
      </button>

      <div className="w-px h-5 bg-border mx-1" />

      <button
        type="button"
        onClick={() => editor.chain().focus().toggleHeading({ level: 2 }).run()}
        className={btn(editor.isActive('heading', { level: 2 }))}
        title="Başlık 2"
      >
        <Heading2 className="h-4 w-4" />
      </button>
      <button
        type="button"
        onClick={() => editor.chain().focus().toggleHeading({ level: 3 }).run()}
        className={btn(editor.isActive('heading', { level: 3 }))}
        title="Başlık 3"
      >
        <Heading3 className="h-4 w-4" />
      </button>

      <div className="w-px h-5 bg-border mx-1" />

      <button
        type="button"
        onClick={() => editor.chain().focus().toggleBulletList().run()}
        className={btn(editor.isActive('bulletList'))}
        title="Madde listesi"
      >
        <List className="h-4 w-4" />
      </button>
      <button
        type="button"
        onClick={() => editor.chain().focus().toggleOrderedList().run()}
        className={btn(editor.isActive('orderedList'))}
        title="Sıralı liste"
      >
        <ListOrdered className="h-4 w-4" />
      </button>
      <button
        type="button"
        onClick={() => editor.chain().focus().toggleBlockquote().run()}
        className={btn(editor.isActive('blockquote'))}
        title="Alıntı"
      >
        <Quote className="h-4 w-4" />
      </button>

      <div className="w-px h-5 bg-border mx-1" />

      <button type="button" onClick={addLink} className={btn(editor.isActive('link'))} title="Bağlantı ekle">
        <LinkIcon className="h-4 w-4" />
      </button>
      {allowImages && (
        <button type="button" onClick={addImage} className={btn(false)} title="Resim ekle">
          <ImageIcon className="h-4 w-4" />
        </button>
      )}
      {allowTables && (
        <button type="button" onClick={addTable} className={btn(false)} title="Tablo ekle">
          <TableIcon className="h-4 w-4" />
        </button>
      )}

      <div className="w-px h-5 bg-border mx-1" />

      <button
        type="button"
        onClick={() => editor.chain().focus().undo().run()}
        disabled={!editor.can().undo()}
        className={`${btn(false)} disabled:opacity-30`}
        title="Geri al (Ctrl+Z)"
      >
        <Undo className="h-4 w-4" />
      </button>
      <button
        type="button"
        onClick={() => editor.chain().focus().redo().run()}
        disabled={!editor.can().redo()}
        className={`${btn(false)} disabled:opacity-30`}
        title="Yinele (Ctrl+Y)"
      >
        <Redo className="h-4 w-4" />
      </button>
    </div>
  );
}
