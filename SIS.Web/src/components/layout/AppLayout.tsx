import { useState } from 'react';
import { Outlet } from 'react-router-dom';
import Sidebar from './Sidebar';
import TopBar from './TopBar';

export default function AppLayout() {
  const [isSidebarCollapsed, setIsSidebarCollapsed] = useState(false);

  return (
    <div className="flex min-h-screen bg-background">
      <Sidebar isCollapsed={isSidebarCollapsed} />

      <div
        className="flex flex-1 flex-col lg:pl-[var(--sidebar-width)]"
        style={{ ['--sidebar-width' as string]: isSidebarCollapsed ? '88px' : '280px' }}
      >
        <TopBar
          isSidebarCollapsed={isSidebarCollapsed}
          onToggleSidebar={() => setIsSidebarCollapsed(prev => !prev)}
        />
        <main className="flex-1 p-6 lg:p-8">
          <div className="mx-auto max-w-6xl">
            <Outlet />
          </div>
        </main>
      </div>
    </div>
  );
}
