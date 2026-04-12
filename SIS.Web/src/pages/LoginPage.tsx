import { useState, useEffect, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import apiClient from '../api/axios';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { AlertCircle, Loader2, HelpCircle, Mail, Globe, Eye, EyeOff, Megaphone } from 'lucide-react';

export default function LoginPage() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [showPassword, setShowPassword] = useState(false);
  const [showContact, setShowContact] = useState(false);
  const [lang, setLang] = useState<'tr' | 'en'>('tr');
  const [announcements, setAnnouncements] = useState<{ title: string; date: string; url: string }[]>([]);

  useEffect(() => {
    apiClient.get('/announcement')
      .then(res => setAnnouncements(res.data))
      .catch(() => {});
  }, []);

  const t = {
    tr: {
      heading: 'Giriş Yap',
      subtitle: 'Sisteme erişmek için bilgilerinizi giriniz.',
      email: 'E-POSTA / KULLANICI ADI',
      password: 'ŞİFRE',
      placeholder: 'kullanici@ankarabilim.edu.tr',
      submit: 'Giriş Yap',
      loading: 'Giriş yapılıyor...',
      error: 'Bağlantı hatası. Lütfen tekrar deneyin.',
      title: 'Öğrenci Bilgi Sistemi',
      help: 'Yardım',
      contact: 'İletişim',
      rememberMe: 'Beni Hatırla',
      forgotPassword: 'Şifremi Unuttum',
      or: 'VEYA',
      eDevlet: 'e-Devlet ile Giriş Yap',
    },
    en: {
      heading: 'Sign In',
      subtitle: 'Enter your credentials to access the system.',
      email: 'EMAIL / USERNAME',
      password: 'PASSWORD',
      placeholder: 'user@ankarabilim.edu.tr',
      submit: 'Sign In',
      loading: 'Signing in...',
      error: 'Connection error. Please try again.',
      title: 'Student Information System',
      help: 'Help',
      contact: 'Contact',
      rememberMe: 'Remember Me',
      forgotPassword: 'Forgot Password',
      or: 'OR',
      eDevlet: 'Sign in with e-Government',
    },
  }[lang];
  const { login, isAuthenticated } = useAuth();
  const navigate = useNavigate();

  useEffect(() => {
    if (isAuthenticated) {
      navigate('/', { replace: true });
    }
  }, [isAuthenticated, navigate]);

  if (isAuthenticated) {
    return null;
  }

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    setIsLoading(true);
    try {
      const err = await login(email, password);
      if (err) {
        setError(err);
      } else {
        navigate('/', { replace: true });
      }
    } catch {
      setError('Bağlantı hatası. Lütfen tekrar deneyin.');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="min-h-screen flex relative">
      {/* Left Panel — Branding */}
      <div className="hidden lg:flex lg:w-[55%] relative bg-gradient-to-br from-primary-dark via-primary to-primary-light overflow-hidden">
        {/* Background decorative elements */}
        <div className="absolute inset-0">
          {/* Campus silhouette */}
          <img src="/campus.jpg" alt="" className="absolute inset-0 w-full h-full object-cover scale-110" />
          <div className="absolute inset-0 bg-gradient-to-t from-primary-dark/90 via-primary/70 to-primary-dark/80" />
        </div>

        {/* University branding — top left */}
        <div className="absolute top-8 left-10 z-10">
          <img
            src="/ankara-bilim-uni-logo-d974021bc1773f29086da252095c557b.png"
            alt="Ankara Bilim Üniversitesi"
            className="h-14 w-auto object-contain brightness-0 invert opacity-90"
          />
        </div>

        {/* Announcements — bottom left */}
        <div className="relative flex flex-col justify-end items-start w-full pl-14 pr-10 pb-10 pt-28">
          {/* Announcements */}
          {announcements.length > 0 && (
            <div className="w-full max-w-sm">
              <div className="flex items-center gap-2 mb-4">
                <Megaphone className="h-4 w-4 text-white/70" />
                <h3 className="text-sm font-semibold text-white/80 tracking-wide uppercase">
                  {lang === 'tr' ? 'Duyurular' : 'Announcements'}
                </h3>
              </div>
              <div className="space-y-3">
                {announcements.map((item, i) => (
                  <a
                    key={i}
                    href={item.url}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="block bg-white/10 backdrop-blur-sm rounded-lg px-4 py-3 border border-white/10 hover:bg-white/15 transition-colors"
                  >
                    {item.date && <p className="text-xs text-white/50 mb-1">{item.date}</p>}
                    <p className="text-sm text-white/90 leading-snug">{item.title}</p>
                  </a>
                ))}
              </div>
            </div>
          )}
        </div>
      </div>

      {/* Right Panel — Login Form */}
      <div className="flex-1 flex flex-col bg-background relative overflow-hidden">
        {/* Decorative background logos */}
        <div className="absolute inset-0 pointer-events-none">
          <img src="/logo.png" alt="" className="absolute bottom-6 right-6 w-44 h-44 object-contain opacity-[0.03]" />
          <img src="/logo.png" alt="" className="absolute top-[18%] left-[6%] w-24 h-24 object-contain opacity-[0.03]" />
        </div>
        {/* Top Bar */}
        <div className="flex items-center justify-end gap-1 px-4 py-3">
          <button
            type="button"
            className="inline-flex items-center gap-1.5 px-3 py-1.5 text-sm text-muted-foreground hover:text-foreground rounded-md hover:bg-muted transition-colors"
          >
            <HelpCircle className="h-4 w-4" />
            {t.help}
          </button>
          <div className="relative">
            <button
              type="button"
              onClick={() => setShowContact(!showContact)}
              className="inline-flex items-center gap-1.5 px-3 py-1.5 text-sm text-muted-foreground hover:text-foreground rounded-md hover:bg-muted transition-colors"
            >
              <Mail className="h-4 w-4" />
              {t.contact}
            </button>
            {showContact && (
              <div className="absolute right-0 top-full mt-1 bg-card border border-border rounded-lg shadow-lg px-4 py-3 whitespace-nowrap z-50">
                <p className="text-sm font-medium text-foreground">ABÜ Destek ve Bilgi Hattı</p>
                <p className="text-lg font-bold text-primary tracking-wide">444 22 28</p>
              </div>
            )}
          </div>
          <div className="w-px h-4 bg-border mx-1" />
          <button
            type="button"
            onClick={() => setLang(lang === 'tr' ? 'en' : 'tr')}
            className="inline-flex items-center gap-1.5 px-3 py-1.5 text-sm text-muted-foreground hover:text-foreground rounded-md hover:bg-muted transition-colors"
          >
            <Globe className="h-4 w-4" />
            {lang === 'tr' ? 'TR' : 'EN'}
          </button>
        </div>

        {/* Centered Form */}
        <div className="flex-1 flex items-center justify-center p-6">
          <div className="w-full max-w-md animate-scale-in">
            {/* System Title */}
            <div className="flex flex-col items-center mb-8">
              <h1 className="font-heading text-xl font-bold text-foreground tracking-tight">
                {t.title}
              </h1>
            </div>

            <div className="bg-card rounded-2xl border border-border shadow-lg p-10 w-full">
              {/* Heading */}
              <h2 className="font-heading text-xl font-bold text-foreground mb-6">
                {t.heading}
              </h2>
              {error && (
                <Alert variant="destructive" className="mb-4">
                  <AlertCircle className="h-4 w-4" />
                  <AlertDescription>{error}</AlertDescription>
                </Alert>
              )}

              <form onSubmit={handleSubmit} className="space-y-5">
                {/* Email */}
                <div className="space-y-1.5">
                  <Label htmlFor="email" className="text-xs font-medium tracking-wide text-muted-foreground">
                    {t.email}
                  </Label>
                  <Input
                    id="email"
                    type="email"
                    value={email}
                    onChange={e => setEmail(e.target.value)}
                    required
                    placeholder=""
                    autoComplete="email"
                    className="h-12 text-base"
                  />
                </div>

                {/* Password */}
                <div className="space-y-1.5">
                  <Label htmlFor="password" className="text-xs font-medium tracking-wide text-muted-foreground">
                    {t.password}
                  </Label>
                  <div className="relative">
                    <Input
                      id="password"
                      type={showPassword ? 'text' : 'password'}
                      value={password}
                      onChange={e => setPassword(e.target.value)}
                      required
                      autoComplete="current-password"
                      className="h-12 text-base pr-10"
                    />
                    <button
                      type="button"
                      onClick={() => setShowPassword(!showPassword)}
                      className="absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground transition-colors"
                    >
                      {showPassword ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                    </button>
                  </div>
                </div>

                {/* Forgot password */}
                <div className="flex justify-end">
                  <button type="button" className="text-sm font-medium text-primary hover:underline">
                    {t.forgotPassword}
                  </button>
                </div>

                {/* Submit */}
                <Button type="submit" className="w-full h-12 text-base rounded-lg" size="lg" disabled={isLoading}>
                  {isLoading ? (
                    <>
                      <Loader2 className="h-4 w-4 animate-spin" />
                      {t.loading}
                    </>
                  ) : (
                    t.submit
                  )}
                </Button>

                {/* Divider */}
                <div className="flex items-center gap-3 my-2">
                  <div className="flex-1 h-px bg-border" />
                  <span className="text-xs text-muted-foreground font-medium">{t.or}</span>
                  <div className="flex-1 h-px bg-border" />
                </div>

                {/* e-Devlet */}
                <Button
                  type="button"
                  variant="outline"
                  className="w-full h-12 text-base rounded-lg gap-2"
                  size="lg"
                >
                  <img src="/unnamed.png" alt="e-Devlet" className="h-5 w-auto object-contain" />
                  {t.eDevlet}
                </Button>
              </form>
            </div>
          </div>
        </div>

        {/* Footer */}
        <div className="py-4 text-center">
          <p className="text-xs text-muted-foreground">
            © 2026 Ankara Bilim Üniversitesi. {lang === 'tr' ? 'Tüm hakları saklıdır.' : 'All rights reserved.'}
          </p>
        </div>
      </div>
    </div>
  );
}
