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

## Historical Intervention data layer

`core.HistoricalInterventions`, BEAM `Varlık Tarihçesi` raporunun 2022-2026 yıllık
partition'larında gözlenen geçmiş müdahaleleri canonical `core.WorkOrders` kayıtlarına bağlar.
Bu veri bir öneri, garanti edilen çözüm veya prescriptive maintenance talimatı değildir.

- Link yalnız `WorkOrderNumber + ReportedDateTime + AssetCode` strict canonical identity ile kurulur.
- `Yapılan İşin Açıklaması`, problem/açıklama ve arıza nedeni ayrı alanlardır; metinler birleştirilmez.
- Raw business text audit için korunur. DTO-safe paralel metinlerde email ve Türk mobil telefon
  kalıpları redacted edilir. Talep eden, iletişim ve sorumlu personel kolonları yeni entity veya
  import audit JSON'una alınmaz.
- Action quality `Informative`, `Generic` veya `NoAction` olarak deterministic ve conservative
  sınıflandırılır. Generic/no-action satırlar silinmez.
- `historical-intervention/v1` SHA-256 fingerprint canonical identity, source year ve stabil iş
  alanlarını kapsar. Unique fingerprint index, serializable transaction ve SQL application lock
  birlikte concurrent/retry duplicate oluşumunu engeller.
- `analytics.HistoricalWorkOrders` ayrı legacy analytics dataset'idir; bu import onu değiştirmez.
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

### Inspection Priority v1

`GET /api/analytics/assets/inspection-priority`, canonical `core.WorkOrders` ve `core.Assets` üzerinde read-only çalışır. Default analysis date canonical dataset'in maksimum `ReportedDateTime` günüdür; `asOf` ile deterministik cutoff verilebilir. Future rows cutoff dışında kalır.

Query, yalnız son 90 günlük veya source-state açık (`RawStatusCode=A`) linked WorkOrder kayıtlarını SQL tarafında `AssetId` bazında conditional aggregate eder. Sonuçta en fazla asset sayısı kadar aggregate materialize edilir; WorkOrder satırları memory'ye alınmaz ve asset başına query çalıştırılmaz. Metadata coverage hesabı cutoff'a kadar olan linked/unlinked canonical kayıtları ayrı gösterir.

Bounded score component'leri: last30 `30/21`, pozitif activity delta `20/8`, open workload `25/4`, last90 recurrence `15/50`, last7 `10/7`. Her component kendi cap'inde doyar; toplam 0–100 aralığındadır. `HIGH >= 50`, `MEDIUM >= 25`, diğerleri `LOW` olur. Tie order: score, open count ve last30 descending; asset code ve asset id ascending.

Bu sonuç ML, predictive maintenance, failure probability, risk probability veya asset health değildir. SCADA, legacy `HistoricalWorkOrders`, unmatched raw asset code, all-time recurrence, NLP, cost ve invented criticality V1 score'a dahil değildir.

### Early Warning v1

`GET /api/analytics/assets/early-warning`, yalnız canonical `core.WorkOrders` ve `core.Assets` üzerinde request-time read-only hesaplanır. Default `asOf` dataset'in maksimum `ReportedDateTime` günüdür. Current 30 günlük analysis window baseline'a girmez; baseline bu pencerenin başladığı ayın öncesindeki 12 tam takvim ayıdır.

SQL tarafında linked WorkOrders asset bazında conditional aggregate edilir; baseline aylık sayımları en fazla asset × 12 bounded aggregate satırı üretir. WorkOrder satırları client memory'ye çekilmez, asset başına query yoktur ve cancellation tüm async sorgulara aktarılır. Asset metadata sorgusu materialized ID parametre listesi yerine server-side subquery kullanır.

En az 6 aktif baseline ayı olmayan asset `INSUFFICIENT_BASELINE` olur ve score üretilmez. Yeterli asset'lerde median ve MAD temelli robust kişisel baseline; 30/7 günlük relative artış, 90 günlük yakın dönem kümelenmesi ve düşük ağırlıklı open-emergence sinyali kullanılır. Component'ler bounded olup toplam 0–100'dür. `HIGH >= 60`, `MEDIUM >= 30`, diğerleri `NORMAL` olur.

Early Warning kişisel davranış sapmasıdır; Inspection Priority'nin mutlak aktivite/workload sıralamasından bağımsızdır. ML, failure probability, asset health, SCADA, legacy `HistoricalWorkOrders`, workflow completion, NLP, cost veya downtime kullanmaz.

### Similar Historical Cases v2

`GET /api/analytics/work-orders/{id}/similar-cases`, yalnız canonical `core.WorkOrders` üzerinde read-only çalışır. Target identity database `Id` alanıdır; `WorkOrderNumber` unique kabul edilmez. Candidate kayıtlar target'tan kesin olarak daha eski olmalı, target ID ve canonical fingerprint ile self-match dışlanmalıdır.

Retrieval iki aşamalıdır: primary pool aynı `AssetId` + aynı `Discipline`, kontrollü widening ise yalnız primary havuz yetersiz olduğunda aynı `AssetGroupId` + aynı `Discipline` kullanır. SQL `AsNoTracking` sorgusu en fazla 500 projection materialize eder; N+1 veya tüm WorkOrder tablosunun client evaluation'ı yoktur. Bounded adaylar Türkçe-aware deterministic normalization, token Jaccard ve açıklanabilir structured bonuslarla rerank edilir. ML, embedding ve vector store kullanılmaz.

Data-driven normalized-description frequency penalty generic template'lerin etkisini azaltır; aynı normalized description sonuç listesinde tek temsilciye collapse edilir. Response description'ın bounded, HTML-free ve temel email/telefon redaction uygulanmış snippet'ini taşır; requester/personel alanlarını taşımaz ve description text loglanmaz. Legacy `analytics.HistoricalWorkOrders`, SCADA ve future records hiçbir retrieval aşamasında kullanılmaz.

V2, bu V1 retrieval/ranking motorunu değiştirmeden dönen bounded adayların `WorkOrderId` değerleriyle `core.HistoricalInterventions` tablosuna tek ek sorgu yapar. Intervention metni similarity input'u değildir; yalnız çıktı olarak sunulur. Eşit similarity score ve text similarity durumunda `Informative > Generic > NoAction > missing` kalite sırası deterministic tie-break'tir; görüntülenen base similarity score değişmez. Bir WorkOrder için birden fazla intervention oluşursa aynı kalite sırası, completion/source zamanı ve kayıt ID'siyle tek temsilci seçilir; vaka çoğaltılmaz.

Public DTO yalnız `RequestDescriptionSanitized`, `FailureReasonDescriptionSanitized` ve `WorkPerformedDescriptionSanitized` kaynaklı alanları taşır. Raw metinler, requester/personel, iletişim, source file/sheet/row ve audit alanları API sözleşmesine dahil değildir. Intervention olmayan vaka geçerli benzer vaka olarak kalır. `Generic` ve `NoAction` kalite durumları UI'da nötr ve açık biçimde gösterilir.

Bu feature solution recommendation değildir. Intervention bilgisi, benzer bir geçmiş vakada gözlenmiş işlemdir; önerilen veya garanti edilen onarım değildir.

## Reliability semantics

- Green: İlgili record-count/trend veri sözleşmesi doğrulanmıştır; asset reliability değildir.
- Yellow: Kaynak kapsamı, eşleştirme veya timestamp kalitesi nedeniyle yorum uyarısı gerekir.
- Pareto: Canonical WorkOrder kayıt yoğunluğudur; asset health/failure rate değildir.
- Clearance interval: Quality-eligible source occurrence için `ClearedAt - ReceivedAt` dağılımıdır; MTTR/repair duration değildir.
- SCADA occurrence: Source row'dur; benzersiz fiziksel alarm değildir.
- WorkOrder activity: Canonical record activity/volume'dür; açık workload veya failure prediction değildir.
- Inspection Priority: Yakın dönem WorkOrder aktivitesine dayalı açıklanabilir inceleme sırasıdır; failure probability veya asset health değildir.
- Early Warning: Asset'in kendi tarihsel WorkOrder davranışından açıklanabilir sapmasıdır; prediction veya risk probability değildir.

## Frontend sınırı

React uygulamasının route'ları:

- `/`
- `/assets`
- `/work-orders`
- `/work-orders/:id/similar-cases`
- `/inspection-priority`
- `/early-warning`
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
