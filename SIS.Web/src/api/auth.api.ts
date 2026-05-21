import apiClient from './axios';
import type { LoginRequest, AuthResponse } from '../types/auth';
import { isAxiosError } from 'axios';

export interface LoginSettingsResponse {
  defaultLanguage: 'tr-TR' | 'en-US';
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

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

export async function getLoginSettings(email?: string): Promise<LoginSettingsResponse> {
  const response = await apiClient.get<LoginSettingsResponse>('/auth/login-settings', {
    params: email ? { email } : undefined,
  });
  return response.data;
}

export async function changePassword(data: ChangePasswordRequest): Promise<AuthResponse> {
  try {
    const response = await apiClient.post<AuthResponse>('/auth/change-password', data);
    return response.data;
  } catch (error) {
    if (isAxiosError(error) && error.response && error.response.data) {
      return error.response.data as AuthResponse;
    }
    throw error;
  }
}
