# Smart Facility Maintenance & Analytics Platform

BEAM CMMS, Siemens Desigo CC ve benzeri tesis yönetim sistemlerinden dışa aktarılan verileri analiz etmeyi hedefleyen karar destek platformu.

Bu repository ilk sprint kapsamında yalnızca çalışabilir proje iskeletini içerir. Varlıklar, iş emirleri, alarmlar, kimlik doğrulama ve Excel içe aktarma özellikleri henüz geliştirilmemiştir.

## Teknoloji yığını

### Backend

- .NET 10 / ASP.NET Core Web API
- Entity Framework Core / SQL Server
- Swashbuckle (Swagger)
- ClosedXML

### Frontend

- React / TypeScript / Vite
- Axios
- React Router
- TanStack Query
- Bootstrap 5
- Chart.js / react-chartjs-2

## Mimari

Backend, Clean Architecture prensiplerine yakın ve sade bir katman yapısıyla başlatılmıştır:

- `SmartFacility.Domain`: İş alanının merkez katmanı; başka projeye bağımlı değildir.
- `SmartFacility.Application`: Uygulama kullanım senaryoları için ayrılmıştır; Domain'e bağımlıdır.
- `SmartFacility.Infrastructure`: Veri erişimi ve dış sistem entegrasyonları için ayrılmıştır; Application ve Domain'e bağımlıdır.
- `SmartFacility.Api`: HTTP sunum katmanıdır; Application ve Infrastructure'a bağımlıdır.

## Gereksinimler

- .NET SDK 10.0.302 veya uyumlu bir .NET 10 feature-band sürümü
- Node.js 24+
- npm 11+

Bu çalışma ortamında .NET SDK kullanıcı profiline kurulmuştur. Sistem `dotnet` komutu SDK'yı bulamıyorsa yeni PowerShell oturumunda önce şu komut çalıştırılmalıdır:

```powershell
$env:Path = "$env:LOCALAPPDATA\Microsoft\dotnet;$env:Path"
dotnet --version
```

## Kurulum ve çalıştırma

Repository kökünde:

```powershell
$env:Path = "$env:LOCALAPPDATA\Microsoft\dotnet;$env:Path"

# Backend
dotnet restore SmartFacilityPlatform.sln
dotnet build SmartFacilityPlatform.sln
dotnet run --project backend/SmartFacility.Api

# Frontend
cd frontend/smart-facility-web
npm install
npm run dev
```

## Build

```powershell
dotnet build SmartFacilityPlatform.sln --configuration Release

cd frontend/smart-facility-web
npm run build
```

## İlk sprint kapsamı

- Solution ve backend katmanları
- Katmanlar arası proje referansları
- React + TypeScript + Vite uygulama iskeleti
- Seçilen temel NuGet ve npm bağımlılıkları
- Git yapılandırması ve dokümantasyon

Sonraki geliştirme aşamasına geçilmeden önce onay beklenmektedir.
