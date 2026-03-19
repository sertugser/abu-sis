import { Outlet } from 'react-router-dom';
import Sidebar from './Sidebar';
import TopBar from './TopBar';

export default function AppLayout() {
  return (
    <div className="flex min-h-screen bg-background">
      {/* Sidebar — fixed */}
      <Sidebar />

      {/* Main content area */}
      <div className="flex-1 flex flex-col lg:pl-[260px]">
        <TopBar />
        <main className="flex-1 p-6 lg:p-8">
          <div className="mx-auto max-w-6xl">
            <Outlet />
          </div>
        </main>
      </div>
    </div>
  );
}
