import apiClient from './axios';
import type { LoginRequest, AuthResponse } from '../types/auth';
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
