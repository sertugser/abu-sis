import axios from 'axios';
import { clearStoredAuth, readStoredAuth } from '../lib/authStorage';

let isRedirectingToLogin = false;

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
  const url = config.url ?? '';
  const auth = readStoredAuth({ clearInvalid: true, notify: true });

  if (auth.status === 'valid') {
    config.headers.Authorization = `Bearer ${auth.session.token}`;
    return config;
  }

  if (auth.status !== 'missing' && !isLoginRequest(url)) {
    redirectToLogin();
  }

  return config;
});

apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    const url = error.config?.url ?? '';
    const isAuthEndpoint = url.includes('/auth/');
    if (error.response?.status === 401 && isAuthEndpoint && !url.includes('/auth/login')) {
      clearStoredAuth({ reason: 'unauthorized' });
      redirectToLogin();
    }
    if (error.response?.status === 403 && error.response?.data?.code === 'PasswordChangeRequired') {
      localStorage.setItem('sis_password_change_required', 'true');
      if (window.location.pathname !== '/sifre-degistir') {
        window.location.href = '/sifre-degistir';
      }
    }
    return Promise.reject(error);
  }
);

export default apiClient;
