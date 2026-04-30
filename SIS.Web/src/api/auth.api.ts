import apiClient from './axios';
import type {
  ChangePasswordRequest,
  AuthResponse,
  LoginRequest,
  PublicRuntimeSettings,
  SessionRuntimeSettings,
} from '../types/auth';
import { isAxiosError } from 'axios';

export async function login(data: LoginRequest): Promise<AuthResponse> {
  try {
    const response = await apiClient.post<AuthResponse>('/auth/login', data);
    return response.data;
  } catch (error) {
    if (isAxiosError(error) && error.response && error.response.data) {
      return error.response.data as AuthResponse;
    }
    throw error;
  }
}

export async function changePassword(data: ChangePasswordRequest): Promise<void> {
  await apiClient.post('/auth/change-password', data);
}

export async function getPublicRuntimeSettings(): Promise<PublicRuntimeSettings> {
  const response = await apiClient.get<PublicRuntimeSettings>('/runtime-settings/public');
  return response.data;
}

export async function getSessionRuntimeSettings(): Promise<SessionRuntimeSettings> {
  const response = await apiClient.get<SessionRuntimeSettings>('/runtime-settings/session');
  return response.data;
}
