import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App'

// React.StrictMode kapalı: dnd-kit 6.3.1 React 19 StrictMode double-invocation'ında
// useDraggable hook'unun setNodeRef callback'ini ikinci mount'ta bağlamıyor
// (stable callback referansı + useUniqueId'nin yeniden hesaplanması kombinasyonu)
// → DOM rect ölçülemiyor → drag-drop çalışmıyor.
// Production build'lerde StrictMode no-op olduğu için son kullanıcı etkilenmez;
// development debugging için kapalı tutuluyor. dnd-kit upgrade ya da abstract paketine
// geçildiğinde geri açılabilir.
createRoot(document.getElementById('root')!).render(<App />)
