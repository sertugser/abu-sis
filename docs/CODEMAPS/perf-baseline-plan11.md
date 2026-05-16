# Runtime Check Performance Baseline — Plan 11

> **Status:** Pending measurement. Baseline ve V2 karşılaştırması Faz 4.6'da aynı harness ile yapılır (anlamlı kıyas için).

## Amaç

`SchedulingService.GetConflictsAsync` + `ValidateAsync` çağrılarının latency'sini ölçmek. Faz 4'te V2 RuleEngine flag açıldığında aynı metrik tekrar alınır; **p95 < baseline + %10** kabul kriteri.

## Ölçüm Yöntemi (Faz 4.6'da çalıştırılacak)

### Seçenek A — HTTP Loop (önerilen)

Backend ayakta, admin token mevcut. PowerShell:

```powershell
$token = (Invoke-RestMethod -Uri "http://localhost:5213/api/auth/login" -Method Post -Body '{"email":"sysadmin","password":"..."}' -ContentType "application/json").token
$headers = @{ Authorization = "Bearer $token" }
$samples = 1..20 | ForEach-Object {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    Invoke-RestMethod -Uri "http://localhost:5213/api/faculty-planning/conflicts?facultyId=1&termId=2" -Headers $headers | Out-Null
    $sw.Stop(); $sw.ElapsedMilliseconds
}
$sorted = $samples | Sort-Object
$p50 = $sorted[10]; $p95 = $sorted[18]
Write-Output "p50=$p50 ms, p95=$p95 ms"
```

### Seçenek B — xUnit Stopwatch Harness

`SIS.Tests/Performance/RuntimeCheckLatencyTests.cs` (Faz 4.6'da yazılacak):

```csharp
[Fact]
public async Task GetConflictsAsync_V1Baseline()
{
    var sw = Stopwatch.StartNew();
    for (int i = 0; i < 20; i++)
    {
        await _service.GetConflictsAsync(termId: 2);
    }
    sw.Stop();
    _output.WriteLine($"Avg: {sw.ElapsedMilliseconds / 20.0} ms");
}
```

## Faz 4 Karşılaştırma Tablosu (boş — Faz 4.6'da doldurulacak)

| Metric | V1 baseline (ms) | V2 RuleEngine (ms) | Delta (%) |
|---|---|---|---|
| `ValidateAsync` p50 | — | — | — |
| `ValidateAsync` p95 | — | — | — |
| `GetConflictsAsync` p50 | — | — | — |
| `GetConflictsAsync` p95 | — | — | — |

## Notlar

- Plan 0.3 görevi şu an placeholder olarak commit'lendi; gerçek ölçüm Faz 4.6'da yapılır (V2 path açıldığında karşılaştırma anlamlı).
- Eğer Faz 4.6'da p95 > baseline + %10 ise, `IRuntimeSnapshotCache` (request-scoped) eklenir. Hedef: 220ms p95 marjı.
- Test ortamı: production-benzeri 50+ section, 20+ classroom seed.
