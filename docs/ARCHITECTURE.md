# SmartFacilityPlatform Architecture

## Amaç ve sınırlar

SmartFacilityPlatform, Excel tabanlı tesis verisini kontrollü biçimde SQL Server'a alan ve read-only analytics sonuçlarını React arayüzüne sunan reliability-aware maintenance decision support sistemidir.

Mevcut HTTP API yalnız analytics GET endpoint'lerini yayınlar. Import pipeline application service olarak mevcuttur; public HTTP import endpoint'i yoktur. Offline ML çalışması production request veya deployment zincirine dahil değildir.

## High-level architecture

```text
Controlled import caller
  -> IImportService / ExcelImportService
  -> ClosedXML workbook reader
  -> validation + fingerprint + processors
  -> generic row transaction veya canonical WorkOrder snapshot transaction
  -> SQL Server: ingestion / core / analytics schemas

Browser
  -> React + TanStack Query
  -> GET /api/analytics/*
  -> ASP.NET Core Minimal API
  -> EfAnalyticsQueryService
  -> SQL Server read-only aggregation
```

## Backend katmanları

- `SmartFacility.Domain`: Core ve ingestion entity'leri; başka proje bağımlılığı yoktur.
- `SmartFacility.Application`: Import profilleri, processors, fingerprint hesapları, analytics query/response sözleşmeleri ve abstraction'lar.
- `SmartFacility.Infrastructure`: EF Core DbContext/mappings/migrations, SQL Server locks, import data store, ClosedXML reader ve analytics query implementasyonu.
- `SmartFacility.Api`: Minimal API endpoint mapping, validation problem responses, JSON enum contract ve composition root.

Başlangıçta DbContext oluşturulur fakat `Migrate`, `EnsureCreated` veya import çağrılmaz. Schema değişikliği startup sorumluluğu değildir.

## Database sınırları

| Schema | Sorumluluk | Örnek tablolar |
|---|---|---|
| `core` | Canonical operasyonel entity'ler ve SCADA occurrence kayıtları | `Assets`, `WorkOrders`, `ScadaAlarmEvents` |
| `analytics` | Legacy dated snapshot (business analytics kaynağı değil) | `HistoricalWorkOrders` |
| `ingestion` | Import audit, raw lineage ve error geçmişi | `ImportBatches`, `ImportSourceRecords`, `ImportErrors` |

`WorkOrders`, toplam canonical source dataset'idir. Açık/kapalı ayrımı sırasıyla `RawStatusCode=A/K` ile yapılır; workflow `Status` ayrı semantiktir. `HistoricalWorkOrders` physical olarak audit/lineage için korunur, union edilmez ve business analytics tarafından sorgulanmaz.

## Import flow

1. Controlled caller bir profile key ve workbook path ile `IImportService.ImportAsync` çağırır.
2. `ImportBatch`, `InProgress` audit kaydı olarak oluşturulur.
3. `ClosedXmlWorkbookReader` workbook'u read-only açar ve tüm configured worksheet'lerin varlığını toplar.
4. Reader önce tüm header satırlarını yield eder. `ExcelImportService`, configured header'ların tamamını doğrulamadan data row işlemeye başlamaz.
5. Her data row için raw cell/formula lineage serialize edilir ve fingerprint'ler hesaplanır.
6. SQL Server import idempotency application lock aynı source/sheet/algorithm/fingerprint için transaction kapsamında alınır.
7. Lock içinde ikinci database fingerprint kontrolü yapılır.
8. Row processor core entity kararını üretir. Gerekirse Building, Location ve AssetGroup identity'leri için transaction-owned dimension lock alınır.
9. Core entity, `ImportSourceRecord` ve varsa `ImportError` aynı row transaction'ında kaydedilir veya birlikte rollback edilir.
10. Batch terminal status ve sayaçlarla tamamlanır. Cancellation/failure audit'e yazılır ve exception caller'a aktarılır.

Structural validation failure core data başlamadan oluşur; daha önce yaratılmış batch audit kaydı `Failed` olur. Audit geçmişi ve raw lineage otomatik olarak silinmez.

WorkOrder profili özel canonical snapshot yoluna yönlendirilir. Tüm workbook önce memory+database preflight'tan geçer; versioned teknik identity `WorkOrderNumber + ReportedDateTime + AssetCode` üzerinden hesaplanır. Bu candidate kalıcı business unique key sayılmaz ve `WorkOrderNumber` üzerinde unique constraint yoktur. SQL Server import-level application lock ve tek serializable transaction içinde eşleşen satırlar güncellenir, yeni satırlar eklenir, kaynakta artık bulunmayan eski sürümler silinmeden inactive tutulur. Core snapshot, source records ve batch terminal durumu birlikte commit/rollback olur. Çözülemeyen asset/location ilişkileri placeholder üretmez; raw anahtarlar korunur ve nullable FK kullanılır.

## Fingerprint ve duplicate sınırı

- Raw row fingerprint source type, source sheet ve normalized cell değerlerinden SHA-256 ile hesaplanır.
- Canonical WorkOrder, Historical WorkOrder ve belirli SCADA kaynakları versioned idempotency fingerprint kullanır.
- Algorithm identity fingerprint ile birlikte duplicate kapsamının parçasıdır; legacy row fingerprint kayıtları ayrı davranışı korur.
- Correlation key duplicate identity değildir.
- `WorkOrderNumber` global unique business identity değildir; database mapping yalnız non-unique index taşır.
- In-memory known fingerprint set hızlı path sağlar; correctness SQL transaction lock ve lock içindeki database kontrolünden gelir.

## Analytics flow

```text
React page/filter
  -> typed analytics client
  -> TanStack Query cache/retry
  -> Minimal API validation
  -> EF/parameterized SQL aggregate
  -> JSON response + reliability metadata
  -> chart/KPI/empty/error state
```

Analytics sorguları `AsNoTracking`, server-side filtering/grouping ve async cancellation kullanır. SCADA clearance percentile hesabı parameterized SQL Server sorgusunda yapılır. API DML veya import endpoint'i yayınlamaz.

## Reliability semantics

- Green: İlgili record-count/trend veri sözleşmesi doğrulanmıştır; asset reliability değildir.
- Yellow: Kaynak kapsamı, eşleştirme veya timestamp kalitesi nedeniyle yorum uyarısı gerekir.
- Pareto: Canonical WorkOrder kayıt yoğunluğudur; asset health/failure rate değildir.
- Clearance interval: Quality-eligible source occurrence için `ClearedAt - ReceivedAt` dağılımıdır; MTTR/repair duration değildir.
- SCADA occurrence: Source row'dur; benzersiz fiziksel alarm değildir.
- WorkOrder activity: Canonical record activity/volume'dür; açık workload veya failure prediction değildir.

## Frontend sınırı

React uygulamasının route'ları:

- `/`
- `/assets`
- `/work-orders`
- `/scada`
- `/data-quality`

Development'ta Vite `/api` isteklerini local backend'e proxy eder. Production'da Vite server kullanılmaz; `dist` statik sunulur ve SPA fallback static host/reverse proxy tarafından sağlanır.

## Production topology

```text
Untrusted client
  -> TLS + access-controlled reverse proxy/gateway
     -> static frontend and SPA fallback
     -> /api/* to private Kestrel
        -> SQL Server using authorized service identity/secret provider
```

Kestrel doğrudan untrusted network'e bind edilmemelidir. API'de application-level authentication/authorization olmadığından trust boundary reverse proxy/gateway ve network katmanındadır. Same-origin topology CORS gerektirmez; separate-origin kurulumda dar allowlist gerekir.

Swagger yalnız Development'ta etkinleştirilir. Repository production reverse-proxy veya infrastructure artifact'i içermez; deployment sahibi TLS, routing, access control, static hosting ve service supervision sağlamalıdır.
