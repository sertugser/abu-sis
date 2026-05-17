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
    if (error.response?.status === 401 && !isLoginRequest(url)) {
      clearStoredAuth({ reason: 'unauthorized' });
      redirectToLogin();
    }
    return Promise.reject(error);
  }
);

export default apiClient;
