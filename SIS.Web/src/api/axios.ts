import axios from 'axios';

const apiClient = axios.create({
  baseURL: '/api',
  headers: { 'Content-Type': 'application/json' },
});

apiClient.interceptors.request.use((config) => {
  const token = localStorage.getItem('sis_token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    const url = error.config?.url ?? '';
    // Otomatik logout SADECE /auth/* endpoint'lerinin 401'inde — JWT'nin gerçekten
    // geçersiz olduğu durumlar. Diğer endpoint'lerin 401'leri çağrı yerine bırakılır
    // (yanlış endpoint kullanımı, eksik kullanıcı profili vb. logout'a sebep olmamalı).
    const isAuthEndpoint = url.includes('/auth/');
    if (error.response?.status === 401 && isAuthEndpoint && !url.includes('/auth/login')) {
      localStorage.removeItem('sis_token');
      localStorage.removeItem('sis_token_expires');
      localStorage.removeItem('sis_roles');
      window.location.href = '/login';
    }

    if (error.response?.status === 403 && error.response?.data?.code === 'PasswordExpired') {
      localStorage.setItem('sis_password_rotation_required', 'true');
      if (error.response.data.passwordExpiresAt) {
        localStorage.setItem('sis_password_rotation_expires_at', error.response.data.passwordExpiresAt);
      }

      if (window.location.pathname !== '/sifre-degistir') {
        window.location.href = '/sifre-degistir';
      }
    }

    return Promise.reject(error);
  }
);

export default apiClient;
