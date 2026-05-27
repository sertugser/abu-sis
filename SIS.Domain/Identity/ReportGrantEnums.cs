namespace SIS.Domain.Identity;

/// <summary>Yetki hedefi: kategori (tüm raporları) mı, tek rapor mu.</summary>
public enum ReportGrantScopeKind { Category = 0, Report = 1 }

/// <summary>Yetkinin verildiği özne tipi.</summary>
public enum ReportGrantSubjectKind { Role = 0, User = 1, Group = 2 }

/// <summary>İzin mi yasak mı. Deny her zaman Allow'dan baskındır.</summary>
public enum ReportGrantEffect { Allow = 0, Deny = 1 }
