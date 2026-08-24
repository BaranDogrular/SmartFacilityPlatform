# Akıllı Tesis Güvenilirlik ve Bakım Karar Destek Sistemi

**Repository:** `SmartFacilityPlatform`

**Yaklaşım:** Reliability-aware Maintenance Decision Support System

SmartFacilityPlatform; tesis yönetimi kaynaklarından alınan Excel verilerini doğrulayan, ham veri lineage ve import audit geçmişiyle SQL Server'a aktaran, güncel ve historical veri kümelerini ayrı anlamlarla analiz eden ve sonuçları REST API ile React arayüzünde sunan bir karar destek sistemidir. Sistem yalnızca bir dashboard değildir.

```text
Source data / Excel
  -> controlled workbook import
  -> structural and row-level data-quality validation
  -> raw lineage, audit and idempotency
  -> SQL Server
  -> reliability-aware analytics
  -> REST API
  -> React frontend
```

Mimari ayrıntılar için [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md), release ve production kontrol listesi için [docs/OPERATIONS.md](docs/OPERATIONS.md) kullanılmalıdır.

## Tamamlanmış ürün kapsamı

- Secure/import-aware workbook ingestion: yapılandırılmış profiller, read-only file access ve kontrollü caller
- Tüm configured worksheet ve header'lar için core write öncesi structural preflight
- Hücre değerleri ve formülleri için ham lineage kaydı
- Silinmeden korunan batch, source-record ve error audit geçmişi
- Row fingerprint ve source-specific versioned idempotency fingerprint'leri
- Aynı fingerprint'in eşzamanlı importlarında SQL Server transaction-owned application lock
- Building, Location ve AssetGroup oluşturulmasında dimension concurrency koruması
- Core entity, raw lineage ve row audit kaydını birlikte yöneten satır transaction'ı
- Current `WorkOrder`, ayrı `HistoricalWorkOrder`, asset ve SCADA analytics
- Asset Maintenance Activity Pareto
- Historical monthly activity trend ve raw Discipline dağılımı
- Quality-eligible SCADA Clearance Interval median/P90 analitiği
- Import/data-quality analytics
- Dashboard, Assets, WorkOrders, SCADA ve Data Quality ekranları
- Tarih, Discipline, status ve SourceSheet filtreleri
- Loading, error, empty/no-match ve retry durumları
- Green/Yellow reliability badge'leri ve güvenli semantik açıklamalar

Import pipeline uygulama katmanında ve DI container içinde kullanılabilir. Mevcut HTTP yüzeyi import başlatmaz; public API yalnızca read-only analytics GET endpoint'lerinden oluşur.

## Semantik güvenlik kuralları

Bu ayrımlar ürün sözleşmesinin parçasıdır:

- Current `WorkOrder` ve `HistoricalWorkOrder` ayrı veri kümeleridir; toplamları birleştirilmez.
- `WorkOrderNumber` global unique business identity değildir.
- SCADA clearance interval, MTTR veya repair duration değildir.
- SCADA kayıt sayıları source row/occurrence sayılarıdır; benzersiz fiziksel alarm iddiası taşımaz.
- Maintenance activity Pareto, asset health, failure probability veya failure rate değildir.
- Green reliability etiketi asset reliability anlamına gelmez; ilgili veri sözleşmesinin doğrulanmış olduğunu belirtir.
- Historical weekly forecasting, failure prediction veya predictive maintenance değildir.

## Teknoloji

### Backend

- .NET 10, ASP.NET Core Minimal API
- Entity Framework Core 10
- SQL Server
- ClosedXML
- Swagger/OpenAPI (yalnız Development)

### Frontend

- React 19, TypeScript 6, Vite 8
- TanStack Query, Axios
- React Router, Bootstrap 5
- Chart.js ve react-chartjs-2

### Test

- xUnit backend automated tests
- SQLite test altyapısı
- Ortam desteklediğinde gerçek SQL Server integration coverage
- Node test runner tabanlı frontend regression testleri

### Offline ML

- Proje-local, Git tarafından ignore edilen Python `.venv`
- numpy, pandas, scikit-learn, scipy ve joblib
- Offline ve read-only değerlendirme; production runtime dependency değildir

## Repository yapısı

```text
backend/
  SmartFacility.Domain/          Domain entities
  SmartFacility.Application/     Import ve analytics sözleşmeleri/iş akışları
  SmartFacility.Infrastructure/  EF Core, SQL Server, import ve analytics implementasyonu
  SmartFacility.Api/             Read-only analytics HTTP API
frontend/smart-facility-web/     React uygulaması
tests/SmartFacility.Application.Tests/
ml/historical-weekly-volume/     Offline forecasting feasibility çalışması
docs/                            Architecture ve operations belgeleri
```

## Gereksinimler

- `global.json` ile sabitlenen .NET SDK 10.0.302 veya uyumlu feature-band
- Node.js 24 ve npm 11 ile doğrulanmış frontend ortamı
- SQL Server
- Backend için erişilebilir `ConnectionStrings:SmartFacilityDatabase` configuration değeri

## Kurulum ve çalıştırma

Komutlar repository kökünden çalıştırılmalıdır.

### Backend

```powershell
dotnet tool restore
dotnet restore .\SmartFacilityPlatform.sln
dotnet build .\SmartFacilityPlatform.sln

# Development örneği; secret'ı repository'ye yazmayın.
$env:ConnectionStrings__SmartFacilityDatabase = '<development SQL Server connection string>'
dotnet run --project .\backend\SmartFacility.Api\SmartFacility.Api.csproj
```

Development launch profile varsayılan olarak `http://localhost:5092` adresini kullanır. Tracked `appsettings.json` içindeki local integrated-auth bağlantısı yalnızca geliştirme kolaylığı içindir; production secret/config contract'ı değildir.

Backend test ve Release build:

```powershell
dotnet test .\SmartFacilityPlatform.sln
dotnet build .\SmartFacilityPlatform.sln --configuration Release
```

### Frontend

```powershell
Set-Location .\frontend\smart-facility-web
npm ci

# Yalnız Vite development proxy hedefi.
$env:VITE_API_PROXY_TARGET = 'http://localhost:5092'
npm run dev
```

Frontend doğrulama komutları:

```powershell
npm test
npm exec -- tsc -b
npm run lint
npm run build
```

`npm run build`, TypeScript project build ve Vite production bundle işlemlerini birlikte çalıştırır. Production artifact `frontend/smart-facility-web/dist` altında oluşur.

`VITE_API_PROXY_TARGET` yalnız development server içindir. Production bundle için `VITE_API_BASE_URL` isteğe bağlı build-time public API origin değeridir; önerilen same-origin topology'de boş bırakılır. Frontend environment değerlerine secret konulmamalıdır.

## Analytics API

Mevcut HTTP yüzeyinin tamamı `GET /api/analytics` altındadır:

| Endpoint | Anlam |
|---|---|
| `/api/analytics/import-quality/overview` | Import batch, lineage, error ve fingerprint audit özeti |
| `/api/analytics/assets/overview` | Asset envanteri ve current WorkOrder ilişkisi |
| `/api/analytics/assets/maintenance-activity-pareto` | Asset bazında current WorkOrder aktivite yoğunluğu |
| `/api/analytics/work-orders/overview` | Yalnız current WorkOrder aggregations |
| `/api/analytics/work-orders/trend` | Yalnız current WorkOrder aylık trendi |
| `/api/analytics/historical-work-orders/activity` | Yalnız HistoricalWorkOrder trend ve Discipline dağılımı |
| `/api/analytics/scada/overview` | SCADA source-occurrence ve timestamp kalite özeti |
| `/api/analytics/scada/trend` | Geçerli ReceivedAt kayıtlarıyla aylık occurrence trendi |
| `/api/analytics/scada/clearance-interval` | Quality-eligible occurrence subset'i için clearance median/P90 |

Date range ve diğer query validation hataları RFC 7807/validation problem response olarak döner. Cancellation token'ları database async çağrılarına aktarılır.

## Database configuration ve migration

Zorunlu configuration key'i:

```text
ConnectionStrings:SmartFacilityDatabase
```

Environment variable karşılığı:

```text
ConnectionStrings__SmartFacilityDatabase
```

Production'da bağlantı bilgisi environment, secret store veya platform configuration provider üzerinden verilmelidir. Tracked local default'a güvenilmemeli; integrated authentication kullanılıyorsa Kestrel process/service identity'sine yalnız gerekli SQL izinleri verilmelidir. Remote SQL Server bağlantılarında server certificate doğrulanmalı, geliştirme amaçlı `TrustServerCertificate=True` production'a taşınmamalıdır.

EF migrations startup sırasında otomatik uygulanmaz. API startup'ı otomatik schema/data write yapmaz. Migration history release öncesinde kontrol edilmeli; schema update yalnız ayrıca onaylanmış bakım adımında manuel uygulanmalıdır. Ayrıntılı kontrol listesi [docs/OPERATIONS.md](docs/OPERATIONS.md) içindedir.

## Development ve production ayrımı

Development zinciri:

```text
Browser -> Vite dev server/proxy -> local Kestrel -> SQL Server
```

Production için beklenen zincir:

```text
Browser -> TLS reverse proxy/gateway
        -> static Vite dist + SPA fallback
        -> /api/* route -> private Kestrel
        -> SQL Server
```

Vite development proxy production deployment çözümü değildir. Repository hazır reverse-proxy veya infrastructure-as-code artifact'i içermez.

## Security ve deployment gereksinimleri

- API'de application-level authentication/authorization yoktur.
- Kestrel doğrudan untrusted/public network'e açılmamalıdır.
- Uygulama trusted/internal network veya access-controlled gateway arkasında çalıştırılmalıdır.
- TLS, reverse proxy/gateway katmanında zorunlu olmalıdır.
- Public deployment için authentication, authorization ve rate limiting ayrıca alınması gereken product/deployment kararlarıdır.
- Swagger/OpenAPI UI yalnız Development ortamında açıktır; Production'da `/swagger` erişimi 404 olmalıdır.
- Same-origin production topology'de CORS gerekli değildir. Separate-origin deployment gerekiyorsa dar bir origin/method/header allowlist tanımlanmalıdır.

## Performance ve acceptance referansı

Representative local production-mode measurements on the current acceptance dataset:

- Asset Pareto default median: yaklaşık 78 ms
- Historical Activity default median: yaklaşık 296 ms
- SCADA Clearance default median: yaklaşık 120 ms

Bunlar SLA değildir; donanım, SQL Server, veri hacmi ve eşzamanlı yükle birlikte değişir.

Son acceptance sırasında gözlenen referans veri büyüklükleri: 5.404 Assets, 54.823 current WorkOrders, 167.143 HistoricalWorkOrders ve 1.950 ScadaAlarmEvents. Bu sayılar product contract veya sabit seed değildir.

## Offline ML sonucu

İncelenen problem **Historical WorkOrder Weekly Volume Forecasting** problemidir. Hedef failure, asset health veya bakım sonucu değildir.

- Baseline winner: 4-week moving average
- Validation: MAE 118,47; WAPE `%10,10`
- Test baseline: MAE 120,96; WAPE `%11,67`
- Ridge ve kontrollü Python model adayları validation baseline'ını geçemedi.
- `ML MODEL ACCEPTED = NO`
- `OFFLINE ML FEASIBILITY = NO-GO`
- `PRODUCTION INFERENCE = NO-GO`

Bu sonuç ana ürünün başarısız olduğu anlamına gelmez. Import, data quality, analytics, API ve frontend sistemi ML olmadan çalışır. Reproducible çalışma ayrıntıları [ml/historical-weekly-volume/README.md](ml/historical-weekly-volume/README.md) içindedir.

## Known limitations

- Application-level authentication/authorization mevcut değildir; deployment network/gateway kontrolüne dayanır.
- Production reverse proxy, TLS ve static-host configuration repository'de hazır artifact olarak bulunmaz.
- Readiness/health endpoint'i yoktur; release smoke kontrolü analytics GET endpoint'iyle yapılır.
- SQL connection retry özel olarak yapılandırılmamıştır.
- Mevcut acceptance verisindeki stale `InProgress` HistoricalWorkOrder batch için otomatik recovery yoktur; audit geçmişi korunarak operasyonel inceleme gerekir.
- Frontend'in dev-transitive `nanoid` P3 advisory'si ayrı dependency-maintenance işidir; runtime feature blocker değildir.
- Offline ML production inference olarak kullanılmaz; predictive maintenance/failure prediction mevcut scope değildir.
- Historical as-of availability tam olarak yeniden kurulamamaktadır; event-time haftalık değerlendirme import-time availability'nin eksiksiz simülasyonu değildir.
- SCADA dataset'i sınırlıdır ve occurrence kayıtları benzersiz fiziksel olay değildir.
- Frontend'de özel favicon asset'i yoktur; browser acceptance sırasında `/favicon.ico` için cosmetic 404 görülebilir.
- Production deployment henüz gerçek infrastructure üzerinde uygulanmamıştır.

## Future roadmap — v1.0 kapsamında değil

1. Early warning / inspection priority
2. Similar historical cases / suggested past actions
3. Predictive maintenance ML feasibility round 2

Bu maddeler tamamlanmış özellik veya mevcut ürün taahhüdü değildir.
