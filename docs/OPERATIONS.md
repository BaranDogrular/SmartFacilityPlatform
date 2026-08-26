# Release and Production Operations Checklist

Bu runbook, mevcut v1.0 release candidate için minimum production hazırlık ve smoke adımlarını tanımlar. Deployment artifact'i veya otomatik deployment script'i değildir. Production database değişiklikleri ayrıca onaylanmış bakım adımı olmalıdır.

## 1. Release input

- [ ] Release commit/tag ve build kaynağı belirli.
- [ ] Backend/frontend regression, import integrity, security, configuration, performance ve browser E2E gate sonuçları kayıtlı.
- [ ] Repository worktree temiz; dependency lock dosyaları beklenen revision'da.
- [ ] Hedef ortam, SQL Server ve reverse-proxy sahipleri belirli.

## 2. Build

Repository kökünde:

```powershell
dotnet tool restore
dotnet restore .\SmartFacilityPlatform.sln
dotnet build .\SmartFacilityPlatform.sln --configuration Release
dotnet test .\SmartFacilityPlatform.sln --configuration Release

Set-Location .\frontend\smart-facility-web
npm ci
npm test
npm exec -- tsc -b
npm run lint
npm run build
```

Backend publish ve frontend `dist` artifact'leri deployment platformunun kontrollü artifact store'una alınmalıdır. Repository bu dağıtımı otomatik yapmaz.

## 3. Production configuration

- [ ] `ASPNETCORE_ENVIRONMENT=Production`.
- [ ] `ConnectionStrings__SmartFacilityDatabase` tracked dosya dışında secret/config provider ile veriliyor.
- [ ] Integrated auth kullanılıyorsa process/service identity SQL Server'da yetkilendirilmiş.
- [ ] Identity'nin yetkileri least-privilege; import çalıştırılmayacak analytics service için gereksiz write yetkisi verilmemiş.
- [ ] Remote SQL Server certificate/hostname doğrulaması etkin; production connection string geliştirme amaçlı `TrustServerCertificate=True` taşımıyor.
- [ ] `SqlServer__CommandTimeoutSeconds` pozitif ve hedef ortama uygun; varsayılan repository değeri otomatik SLA değildir.
- [ ] Secret, log, command output veya frontend bundle içine yazılmamış.

## 4. Migration history

API startup'ı migration uygulamaz ve schema/data write yapmaz.

Migration listesini ve hedef database history'sini read-only doğrula:

```powershell
dotnet ef migrations list `
  --project .\backend\SmartFacility.Infrastructure\SmartFacility.Infrastructure.csproj `
  --startup-project .\backend\SmartFacility.Api\SmartFacility.Api.csproj
```

```sql
SELECT [MigrationId], [ProductVersion]
FROM [dbo].[__EFMigrationsHistory]
ORDER BY [MigrationId];
```

- [ ] Repository migrations ile target history karşılaştırıldı.
- [ ] Eksik migration varsa ayrıca onaylı change window, backup/rollback planı ve yetkili identity hazır.
- [ ] `dotnet ef database update` yalnız bu ayrı, write-enabled bakım adımında çalıştırılacak; normal API startup veya smoke testin parçası değildir.

### Canonical WorkOrder snapshot import

Önce zorunlu, write-free preflight çalıştırılır:

```powershell
dotnet run --no-build -c Release --project .\backend\SmartFacility.Api\SmartFacility.Api.csproj -- `
  --canonical-work-orders-preflight "C:\controlled-input\work-orders.xlsx"
```

`CanImport=true`, parse/header/identity collision sayıları sıfır ve source baseline açıklanabilir olmadan import çalıştırılmaz. Unresolved asset/location değerleri raw anahtarlarıyla korunur; placeholder dimension yaratılmaz ve nullable ilişkiler açıkça raporlanır.

Onaylı write window'da aynı preflight'ı tekrar yapan atomic import:

```powershell
dotnet run --no-build -c Release --project .\backend\SmartFacility.Api\SmartFacility.Api.csproj -- `
  --canonical-work-orders-import "C:\controlled-input\work-orders.xlsx"
```

Bu komut import-level SQL application lock ve tek snapshot transaction kullanır. Failure/cancellation core snapshot ve source records'ı rollback eder; batch failure audit'i korunur. Aynı export core duplicate üretmez. Legacy `analytics.HistoricalWorkOrders` tablosu bu prosedürde değiştirilmez.

## 5. Read-only database smoke

Yetkili read-only bağlantıyla:

```sql
SELECT COUNT_BIG(*) AS Assets FROM [core].[Assets];
SELECT COUNT_BIG(*) AS CanonicalWorkOrders FROM [core].[WorkOrders] WHERE [IsInCanonicalSnapshot] = 1;
SELECT COUNT_BIG(*) AS OpenWorkOrders FROM [core].[WorkOrders] WHERE [IsInCanonicalSnapshot] = 1 AND [RawStatusCode] = N'A';
SELECT COUNT_BIG(*) AS ClosedWorkOrders FROM [core].[WorkOrders] WHERE [IsInCanonicalSnapshot] = 1 AND [RawStatusCode] = N'K';
SELECT COUNT_BIG(*) AS LegacyHistoricalSnapshotRows FROM [analytics].[HistoricalWorkOrders];
SELECT COUNT_BIG(*) AS ScadaAlarmEvents FROM [core].[ScadaAlarmEvents];
```

25.08.2026 canonical acceptance için beklenen yaklaşık baseline `5.404 Assets / 171.136 canonical WorkOrders / 75 open / 171.054 closed / 7 other / 167.143 legacy snapshot / 1.950 SCADA` değeridir. Bunlar hard-coded product contract değildir.

## 6. Stale import batch kontrolü

Otomatik recovery yoktur. Salt-okunur inceleme:

```sql
SELECT [Id], [SourceType], [FileName], [StartedAt], [CompletedAt], [Status]
FROM [ingestion].[ImportBatches]
WHERE [Status] = N'InProgress'
ORDER BY [StartedAt];
```

Mevcut acceptance verisinde stale HistoricalWorkOrder `InProgress` kaydı bilinen limitation'dır. Bu runbook kapsamında update/delete yapma veya batch'i yeniden çalıştırma. Kaynak dosya, audit/source records ve operasyon sahibi incelendikten sonra ayrı incident/change kararı alınmalıdır; audit geçmişi korunmalıdır.

## 7. Reverse proxy ve static frontend

- [ ] Public/trusted entry point TLS kullanıyor.
- [ ] Kestrel yalnız private interface/network üzerinde erişilebilir.
- [ ] Vite `dist` statik olarak sunuluyor; Vite dev server production'da çalışmıyor.
- [ ] `/api/*` private backend'e route ediliyor.
- [ ] `/assets`, `/work-orders`, `/scada` ve `/data-quality` direct navigation için SPA fallback `index.html` döndürüyor.
- [ ] Same-origin ise CORS eklenmemiş. Separate-origin zorunluysa yalnız bilinen frontend origin/method/header allowlist'i var.
- [ ] Gateway access control mevcut trust modeline uygun.
- [ ] Public deployment planlanıyorsa authentication/authorization ve rate limiting için açık product/deployment kararı var.

## 8. Production smoke

Production bağlantı bilgisi ve token/headers gerekiyorsa gateway prosedürünü kullan. En az:

```text
GET /api/analytics/assets/overview
GET /api/analytics/assets/maintenance-activity-pareto?top=10
GET /api/analytics/work-orders/activity
GET /api/analytics/scada/clearance-interval
```

- [ ] Response status 200 ve JSON content type doğru.
- [ ] Invalid date range ve invalid Pareto Top validation response'u 400.
- [ ] Production `/swagger` ve `/swagger/index.html` 404.
- [ ] Browser ana route'ları açılıyor; console/network kritik hata yok.
- [ ] WorkOrder toplam/açık/kapalı değerleri canonical dataset'ten geliyor; legacy snapshot dahil değil.
- [ ] Pareto health/failure, clearance MTTR/repair duration olarak sunulmuyor.
- [ ] Green/Yellow reliability açıklamaları görünür.
- [ ] No-match filtre empty state üretir; NaN/Infinity yok.

Uygulamada dedicated health/readiness endpoint'i yoktur. Load balancer/readiness kontrolü gerekiyorsa mevcut read-only analytics smoke'un maliyeti ve database bağımlılığı dikkate alınmalı; yeni endpoint bu release runbook'unda varsayılmamalıdır.

## 9. Post-deployment observation

- [ ] Gateway/Kestrel 4xx/5xx ve timeout oranları kontrol edildi.
- [ ] SQL bağlantı/timeout hatası yok.
- [ ] Request loop veya beklenmeyen duplicate analytics çağrısı yok.
- [ ] Baseline tablo sayıları smoke öncesi/sonrası değişmedi; yalnız read-only çağrılar yapıldı.
- [ ] Swagger, Kestrel private binding ve TLS şartları tekrar doğrulandı.

SQL connection retry özel olarak yapılandırılmamıştır. Transient connection sorunlarında servisin platform supervision politikası ve kullanıcı retry davranışı gözlemlenmeli; bu release sırasında rastgele retry/cache/index değişikliği yapılmamalıdır.

## 10. Go/no-go

No-go koşulları:

- Migration history hedef release ile uyumsuz.
- Secret/config eksik veya yanlış database'e yöneliyor.
- Kestrel untrusted network'e doğrudan açık.
- TLS veya `/api` routing yok.
- Swagger Production'da açık.
- Ana analytics endpoint'i 5xx/timeout üretiyor.
- Canonical/source-state veya Pareto/Clearance semantiği UI'da bozulmuş.
- Read-only smoke sırasında beklenmeyen database değişikliği var.

Rollback veya incident sırasında import audit/history kayıtlarını silme. Schema/data rollback ayrı, açıkça onaylanmış prosedür gerektirir.
