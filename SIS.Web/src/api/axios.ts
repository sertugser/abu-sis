import axios from 'axios';

const AUTH_STORAGE_KEYS = ['sis_token', 'sis_token_expires', 'sis_roles'] as const;

let isRedirectingToLogin = false;

function clearAuthStorage() {
  AUTH_STORAGE_KEYS.forEach(key => localStorage.removeItem(key));
}

function isLoginRequest(url: string) {
  return url.includes('/auth/login');
}

function redirectToLogin() {
  if (isRedirectingToLogin || window.location.pathname === '/login')
    return;

  isRedirectingToLogin = true;
  window.location.assign('/login');
}

const apiClient = axios.create({
  baseURL: '/api',
  headers: { 'Content-Type': 'application/json' },
});

apiClient.interceptors.request.use((config) => {
  const token = localStorage.getItem('sis_token');
  const expiresAt = localStorage.getItem('sis_token_expires');
  const url = config.url ?? '';

  if (token && expiresAt && new Date(expiresAt) <= new Date()) {
    clearAuthStorage();
    if (!isLoginRequest(url))
      redirectToLogin();
    return config;
  }

  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    const url = error.config?.url ?? '';
    if (error.response?.status === 401 && !isLoginRequest(url)) {
      clearAuthStorage();
      redirectToLogin();
    }
    return Promise.reject(error);
  }
);

export default apiClient;
