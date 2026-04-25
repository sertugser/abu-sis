import { useState } from 'react';
import { Outlet } from 'react-router-dom';
import Sidebar from './Sidebar';
import TopBar from './TopBar';
import RoleInvalidationBanner from './RoleInvalidationBanner';

export default function AppLayout() {
  const [isSidebarCollapsed, setIsSidebarCollapsed] = useState(false);

  return (
    <div className="flex min-h-screen bg-background">
      <Sidebar
        isCollapsed={isSidebarCollapsed}
        onToggleSidebar={() => setIsSidebarCollapsed(prev => !prev)}
      />

      <div
        className="flex min-w-0 flex-1 flex-col"
        style={{
          paddingLeft: isSidebarCollapsed ? '72px' : '290px',
          transition: 'padding-left 200ms cubic-bezier(0.25, 0.1, 0.25, 1)',
        }}
      >
        <TopBar />
        <RoleInvalidationBanner />
        <main className="flex min-w-0 flex-1 flex-col overflow-x-hidden px-6 py-5 lg:px-8">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
