import { useMutation, useQuery, useQueryClient, type QueryClient } from '@tanstack/react-query';
import { useCallback } from 'react';
import type { HubConnection } from '@microsoft/signalr';
import apiClient from '../api/axios';
import { useSignalR } from './useSignalR';

const BASE = '/me/notifications';

export interface NotificationDto {
  id: number;
  type: string;
  severity: string;
  title: string;
  body: string;
  link: string | null;
  isRead: boolean;
  createdAt: string;
}

export interface NotificationPage {
  items: NotificationDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface NotificationListParams {
  type?: string | null;
  onlyUnread?: boolean;
  page?: number;
  pageSize?: number;
}

const NOTIFICATIONS_ROOT = ['notifications'] as const;

function invalidateNotifications(qc: QueryClient) {
  void qc.invalidateQueries({ queryKey: NOTIFICATIONS_ROOT });
}

export function useNotificationUnreadCount(enabled = true, refetchIntervalMs?: number) {
  return useQuery({
    queryKey: ['notifications', 'unread-count'],
    queryFn: async () => {
      const { data } = await apiClient.get<{ count: number }>(`${BASE}/unread-count`);
      return data.count;
    },
    enabled,
    staleTime: 30_000,
    refetchInterval: refetchIntervalMs,
  });
}

export function useRecentNotifications(max = 10, enabled = true) {
  return useQuery({
    queryKey: ['notifications', 'recent', max],
    queryFn: async () => {
      const { data } = await apiClient.get<NotificationDto[]>(`${BASE}/recent`, { params: { max } });
      return data;
    },
    enabled,
  });
}

export function useNotificationsPage(params: NotificationListParams) {
  return useQuery({
    queryKey: ['notifications', 'list', params],
    queryFn: async () => {
      const { data } = await apiClient.get<NotificationPage>(BASE, {
        params: {
          type: params.type || undefined,
          onlyUnread: params.onlyUnread || undefined,
          page: params.page ?? 1,
          pageSize: params.pageSize ?? 20,
        },
      });
      return data;
    },
  });
}

export function useMarkNotificationRead() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => {
      await apiClient.post(`${BASE}/${id}/read`);
    },
    onSuccess: () => invalidateNotifications(qc),
  });
}

export function useMarkAllNotificationsRead() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async () => {
      await apiClient.post(`${BASE}/read-all`);
    },
    onSuccess: () => invalidateNotifications(qc),
  });
}

export function useDeleteNotification() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: async (id: number) => {
      await apiClient.delete(`${BASE}/${id}`);
    },
    onSuccess: () => invalidateNotifications(qc),
  });
}

/**
 * SignalR /hubs/notifications: sunucudan "Notification" event'i gelince ilgili
 * query'leri tazeleyip rozet/listeyi anında günceller (30sn polling'e ek).
 */
export function useNotificationRealtime(enabled = true) {
  const qc = useQueryClient();
  const onConnect = useCallback((connection: HubConnection) => {
    connection.on('Notification', () => {
      invalidateNotifications(qc);
    });
  }, [qc]);

  return useSignalR({ hubUrl: '/hubs/notifications', onConnect, enabled });
}
