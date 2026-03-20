  TaskFlow API

  Gorev ve takim yonetimi icin gelistirilmis, .NET 10 ve Clean Architecture prensipleriyle insa edilmis moduler monolit
  bir SaaS backend.

  ▎ Not: Bu proje ogrenme ve portfolyo amaciyla gelistirilmis bir MVP'dir — uretim ortami icin tasarlanmamistir.

  ---
  Icindekiler

  - #genel-bakis
  - #mimari
  - #teknoloji-yigini
  - #moduller
  - #proje-yapisi
  - #abonelik-planlari
  - #baslangiç
  - #api-endpointleri
  - #testler

  ---
  Genel Bakis

  TaskFlow asagidaki ozellikleri sunan bir SaaS MVP'dir:

  - Gorev ve proje yonetimi — oncelik, deadline ve alt gorev destegi
  - Gercek zamanli mesajlasma — bire bir ve grup sohbeti, sifreleme destegi
  - Gercek zamanli bildirimler — SignalR uzerinden
  - Yapay zeka destekli asistan — RAG (Retrieval-Augmented Generation) ile
  - Coklu kiracillik (Multi-tenancy) — plan bazli ozellik acma/kapama
  - Stripe entegrasyonu — abonelik faturalandirma
  - Departman ve takim yonetimi — rol tabanli erisim kontrolu

  ---
  Mimari

  Proje, her modul icin Clean Architecture katmanlarina sahip Moduler Monolit desenini takip eder:

  ┌─────────────────────────────────────────────────┐
  │                 Presentation                     │
  │          (ASP.NET Core Web API)                  │
  ├─────────────────────────────────────────────────┤
  │  Assistant │ Chat │ Identity │ Notification │ ...│
  │  ┌───────────────────────────┐                   │
  │  │  Application  (CQRS)     │                   │
  │  │  Domain      (Entities)  │                   │
  │  │  Infrastructure (Servisler)│                  │
  │  │  Persistence (EF Core)   │                   │
  │  └───────────────────────────┘                   │
  ├─────────────────────────────────────────────────┤
  │              BuildingBlocks                      │
  │  (Paylasilan sozlesmeler, davranislar, arayuzler)│
  └─────────────────────────────────────────────────┘

  Temel Desenler:
  - CQRS — FlashMediator ile
  - Repository & Unit of Work
  - Olay Gudumlu Mimari — RabbitMQ + DotNetCore.CAP
  - MediatR Pipeline Davranislari — onbellekleme, hiz sinirlandirma, dogrulama
  - Domain-Driven Design — zengin domain varliklari

  ---
  Teknoloji Yigini

  Cekirdek

  ┌──────────────┬──────────┬──────────────────┐
  │  Teknoloji   │ Versiyon │       Amac       │
  ├──────────────┼──────────┼──────────────────┤
  │ .NET         │ 10.0     │ Calisma zamani   │
  ├──────────────┼──────────┼──────────────────┤
  │ C#           │ 14       │ Programlama dili │
  ├──────────────┼──────────┼──────────────────┤
  │ ASP.NET Core │ 10.0     │ Web framework    │
  └──────────────┴──────────┴──────────────────┘

  Veri & Kalicilik

  ┌───────────────────────┬──────────┬──────────────────────────────────────────────────┐
  │       Teknoloji       │ Versiyon │                       Amac                       │
  ├───────────────────────┼──────────┼──────────────────────────────────────────────────┤
  │ PostgreSQL            │ 18       │ Birincil iliskisel veritabani                    │
  ├───────────────────────┼──────────┼──────────────────────────────────────────────────┤
  │ pgvector              │ pg18     │ Yapay zeka embedding'leri icin vektor veritabani │
  ├───────────────────────┼──────────┼──────────────────────────────────────────────────┤
  │ Entity Framework Core │ 10.0     │ ORM                                              │
  ├───────────────────────┼──────────┼──────────────────────────────────────────────────┤
  │ Npgsql                │ 10.0     │ PostgreSQL .NET saglayicisi                      │
  └───────────────────────┴──────────┴──────────────────────────────────────────────────┘

  Mesajlasma & Onbellekleme

  ┌────────────────┬──────────┬─────────────────────────────────────────┐
  │   Teknoloji    │ Versiyon │                  Amac                   │
  ├────────────────┼──────────┼─────────────────────────────────────────┤
  │ RabbitMQ       │ 4.2.4    │ Asenkron olaylar icin mesaj arabulucusu │
  ├────────────────┼──────────┼─────────────────────────────────────────┤
  │ DotNetCore.CAP │ 10.0.1   │ Olay yolu & dagitik islemler            │
  ├────────────────┼──────────┼─────────────────────────────────────────┤
  │ Redis          │ 8.6      │ Bellek ici onbellek & oturum deposu     │
  └────────────────┴──────────┴─────────────────────────────────────────┘

  Gercek Zamanli Iletisim

  ┌───────────┬───────────────────────────────────────────────────────────┐
  │ Teknoloji │                           Amac                            │
  ├───────────┼───────────────────────────────────────────────────────────┤
  │ SignalR   │ WebSocket tabanli gercek zamanli mesajlasma & bildirimler │
  └───────────┴───────────────────────────────────────────────────────────┘

  Kimlik Dogrulama & Guvenlik

  ┌───────────────────────┬────────────────────────────────┐
  │       Teknoloji       │              Amac              │
  ├───────────────────────┼────────────────────────────────┤
  │ ASP.NET Core Identity │ Kullanici & rol yonetimi       │
  ├───────────────────────┼────────────────────────────────┤
  │ JWT Bearer            │ Token tabanli kimlik dogrulama │
  ├───────────────────────┼────────────────────────────────┤
  │ Ozel sifreleme        │ Sohbet mesaji sifreleme        │
  └───────────────────────┴────────────────────────────────┘

  Yapay Zeka & Makine Ogrenimi

  ┌───────────────────────────┬────────────────────────┬───────────────────────────────────┐
  │         Teknoloji         │        Versiyon        │               Amac                │
  ├───────────────────────────┼────────────────────────┼───────────────────────────────────┤
  │ Microsoft Semantic Kernel │ 1.73.0                 │ Yapay zeka orkestrasyon           │
  ├───────────────────────────┼────────────────────────┼───────────────────────────────────┤
  │ Google Gemini API         │ gemini-3-flash-preview │ LLM tamamlamalari                 │
  ├───────────────────────────┼────────────────────────┼───────────────────────────────────┤
  │ Gemini Embeddings         │ gemini-embedding-001   │ Metin embedding'leri (1536 boyut) │
  ├───────────────────────────┼────────────────────────┼───────────────────────────────────┤
  │ OpenRouter                │ google/gemma-3-27b-it  │ Yedek LLM                         │
  ├───────────────────────────┼────────────────────────┼───────────────────────────────────┤
  │ pgvector                  │ 0.3.2                  │ RAG icin vektor benzerlik aramasi │
  └───────────────────────────┴────────────────────────┴───────────────────────────────────┘

  Dogrulama & Loglama

  ┌────────────────────┬──────────┬─────────────────────────┐
  │     Teknoloji      │ Versiyon │          Amac           │
  ├────────────────────┼──────────┼─────────────────────────┤
  │ FluentValidation   │ 12.1.1   │ Istek dogrulama         │
  ├────────────────────┼──────────┼─────────────────────────┤
  │ Serilog.AspNetCore │ 10.0.0   │ Yapilandirilmis loglama │
  └────────────────────┴──────────┴─────────────────────────┘

  Serilog Yapilandirmasi:
  - Console Sink — Standart cikisa log yazar
  - File Sink — /logs/logs.txt konumunda gunluk donus yapan log dosyalari
  - builder.Host.UseSerilog() ile entegre edilmistir
  - ILogger<T> tum modullerde DI uzerinden enjekte edilir

  Odeme

  ┌────────────┬────────────────────────────────────────┐
  │ Teknoloji  │                  Amac                  │
  ├────────────┼────────────────────────────────────────┤
  │ Stripe API │ Abonelik faturalandirma & odeme isleme │
  └────────────┴────────────────────────────────────────┘

  DevOps & Altyapi

  ┌────────────────┬───────────────────────────────────┐
  │   Teknoloji    │               Amac                │
  ├────────────────┼───────────────────────────────────┤
  │ Docker         │ Cok asamali konteyner derlemeleri │
  ├────────────────┼───────────────────────────────────┤
  │ Docker Compose │ Yerel gelistirme orkestrasyonu    │
  ├────────────────┼───────────────────────────────────┤
  │ Docker Secrets │ Guvenli kimlik bilgisi yonetimi   │
  └────────────────┴───────────────────────────────────┘

  Test

  ┌───────────┬──────────┬────────────────────────┐
  │ Teknoloji │ Versiyon │          Amac          │
  ├───────────┼──────────┼────────────────────────┤
  │ xUnit     │ 2.9.3    │ Birim test framework'u │
  ├───────────┼──────────┼────────────────────────┤
  │ Moq       │ —        │ Mocklama framework'u   │
  ├───────────┼──────────┼────────────────────────┤
  │ coverlet  │ 6.0.4    │ Kod kapsami            │
  └───────────┴──────────┴────────────────────────┘

  ---
  Moduller

  Identity

  Kullanici yonetimi, kimlik dogrulama (JWT), yetkilendirme (RBAC), sirket, departman ve grup yonetimi. Roller: Admin,
  Company, Worker.

  ProjectManagement

  Oncelik (Acil, Oncelikli, Siradan), durum (Atandi, Yapim Asamasinda, Onay Bekliyor, Tamamlandi) ve deadline takibi ile
   gorev ve alt gorev yonetimi.

  Chat

  SignalR uzerinden uctan uca sifreleme ile gercek zamanli bire bir ve grup mesajlasma.
  Okundu/iletildi/duzenlendi/silindi durumlarini takip eder.

  Notification

  SignalR uzerinden gercek zamanli push bildirimleri. RabbitMQ'dan entegrasyon olaylarini tuketir.

  Tenant

  Abonelik planlari (Startup, Business, Enterprise), Stripe odeme entegrasyonu, kullanim kotasi zorlama ve plan bazli
  ozellik acma/kapama ile coklu kiracillik.

  Report

  Durum takibi (Bildirildi, Isleme Alindi, Cozuldu, Reddedildi) ve departman bildirimleri ile sorun raporlama sistemi.

  Stats

  Kullanici ve zaman donemine gore takip edilen calisma verimliligi ve performans metrikleri.

  Assistant

  RAG (Retrieval-Augmented Generation) kullanan yapay zeka destekli sohbet botu. Bilgi tabanini vektor embedding'lerine
  indeksler, benzerlik aramasi yapar ve Gemini/OpenRouter uzerinden baglama duyarli yanitlar uretir.

  ---
  Proje Yapisi

  TaskFlowAPI/
  ├── src/
  │   ├── BuildingBlocks/
  │   │   └── TaskFlow.BuildingBlocks/        # Paylasilan sozlesmeler, davranislar, arayuzler
  │   ├── Modules/
  │   │   ├── Assistant/                      # RAG ile yapay zeka sohbet botu
  │   │   │   ├── Assistant.Domain/
  │   │   │   ├── Assistant.Application/
  │   │   │   ├── Assistant.Infrastructure/
  │   │   │   └── Assistant.Persistence/
  │   │   ├── Chat/                           # Gercek zamanli mesajlasma
  │   │   ├── Identity/                       # Kimlik dogrulama & kullanici yonetimi
  │   │   ├── Notification/                   # Gercek zamanli bildirimler
  │   │   ├── ProjectManagement/              # Gorev yonetimi
  │   │   ├── Report/                         # Sorun raporlama
  │   │   ├── Stats/                          # Calisan istatistikleri
  │   │   └── Tenant/                         # Coklu kiracillik & faturalandirma
  │   └── Taskflow.API/
  │       └── Taskflow.Presentation/          # API giris noktasi
  ├── tests/                                  # Birim testleri (modul basina bir proje)
  ├── secrets/                                # Docker secret'lari (repo'da yok)
  ├── docker-compose.yml
  └── TaskFlow.slnx

  Toplam: 8 modul + BuildingBlocks + Presentation + 8 test projesi olmak uzere 33 proje

  ---
  Abonelik Planlari

  ┌───────────────────────┬─────────┬──────────┬────────────┐
  │        Ozellik        │ Startup │ Business │ Enterprise │
  ├───────────────────────┼─────────┼──────────┼────────────┤
  │ Kisi Limiti           │ 50      │ 250      │ 10.000     │
  ├───────────────────────┼─────────┼──────────┼────────────┤
  │ Takim Limiti          │ 10      │ 50       │ 500        │
  ├───────────────────────┼─────────┼──────────┼────────────┤
  │ Bireysel Gorev Limiti │ 1.000   │ 10.000   │ 100.000    │
  ├───────────────────────┼─────────┼──────────┼────────────┤
  │ Dahili Raporlama      │ Evet    │ Evet     │ Evet       │
  └───────────────────────┴─────────┴──────────┴────────────┘

  ---
  Baslangiç

  On Kosullar

  - Docker & Docker Compose
  - .NET 10 SDK (yerel gelistirme icin)

  1. Secret Dosyalarini Olusturun

  secrets/ dizinini olusturun ve gerekli secret dosyalarini ekleyin:

  New-Item -ItemType Directory -Path .\secrets -Force | Out-Null

  Set-Content -Path .\secrets\postgres_user       -Value 'taskflow' -NoNewline
  Set-Content -Path .\secrets\postgres_password    -Value 'sifreniz' -NoNewline
  Set-Content -Path .\secrets\rabbitmq_user        -Value 'taskflow' -NoNewline
  Set-Content -Path .\secrets\rabbitmq_password    -Value 'sifreniz' -NoNewline
  Set-Content -Path .\secrets\redis_password       -Value 'sifreniz' -NoNewline
  Set-Content -Path .\secrets\jwt_secret_key       -Value 'jwt_secret_min_32_karakter' -NoNewline
  Set-Content -Path .\secrets\stripe_secret_key    -Value 'sk_test_anahtariniz' -NoNewline
  Set-Content -Path .\secrets\google_api_key       -Value 'gemini_api_anahtariniz' -NoNewline
  Set-Content -Path .\secrets\openrouter_api_key   -Value 'openrouter_anahtariniz' -NoNewline
  Set-Content -Path .\secrets\chat_message_encryption_key -Value 'sifreleme_anahtariniz' -NoNewline

  Docker Compose bu dosyalari konteynera /run/secrets/<secret_adi> olarak baglar.

  2. Docker Compose ile Calistirin

  docker compose up --build

  Bu komut tum servisleri baslatir:
  - TaskFlow API → http://localhost:8080
  - PostgreSQL → localhost:5432
  - pgvector → localhost:5433
  - Redis → localhost:6379
  - RabbitMQ → localhost:5672

  API otomatik olarak:
  - Yeniden deneme mantigi ile tum EF Core migration'larini uygular
  - Referans verileri tohumlar (gorev durumlari, oncelikler, rapor durumlari, planlar)
  - Yapay zeka bilgi tabanini baslatir
  - Development modunda demo hesap olusturur (demo@taskflow.dev)

  3. pgvector Kurulumu

  - pgvector/pgvector:pg18 imaji ile ayri bir servis olarak calisir
  - Baglanti PgVector__Host, PgVector__Port, PgVector__Database uzerinden yapilandirilir
  - Ayri kimlik bilgisi verilmezse ana PostgreSQL kimlik bilgilerini kullanir
  - Baslangiçta CREATE EXTENSION IF NOT EXISTS vector; komutunu calistirir

  ---
  API Endpoint'leri

  REST API

  ┌────────────────────────┬───────────────────┬──────────────────────────────────────────────────────────────────┐
  │         Prefix         │       Modul       │                             Aciklama                             │
  ├────────────────────────┼───────────────────┼──────────────────────────────────────────────────────────────────┤
  │ /api/Identity          │ Identity          │ Kimlik dogrulama, kullanicilar, sirketler, departmanlar, gruplar │
  ├────────────────────────┼───────────────────┼──────────────────────────────────────────────────────────────────┤
  │ /api/Chat              │ Chat              │ Mesajlasma islemleri                                             │
  ├────────────────────────┼───────────────────┼──────────────────────────────────────────────────────────────────┤
  │ /api/ProjectManagement │ ProjectManagement │ Gorev & alt gorev CRUD                                           │
  ├────────────────────────┼───────────────────┼──────────────────────────────────────────────────────────────────┤
  │ /api/Notification      │ Notification      │ Bildirim sorgulari                                               │
  ├────────────────────────┼───────────────────┼──────────────────────────────────────────────────────────────────┤
  │ /api/Report            │ Report            │ Sorun rapor yonetimi                                             │
  ├────────────────────────┼───────────────────┼──────────────────────────────────────────────────────────────────┤
  │ /api/Tenant            │ Tenant            │ Plan & abonelik yonetimi                                         │
  ├────────────────────────┼───────────────────┼──────────────────────────────────────────────────────────────────┤
  │ /api/Stats             │ Stats             │ Calisan istatistikleri                                           │
  ├────────────────────────┼───────────────────┼──────────────────────────────────────────────────────────────────┤
  │ /api/Ai                │ Assistant         │ Yapay zeka sohbet botu & gunluk ozetler                          │
  └────────────────────────┴───────────────────┴──────────────────────────────────────────────────────────────────┘

  SignalR Hub'lari

  ┌──────────────────┬──────────────────────────────────┐
  │     Endpoint     │               Amac               │
  ├──────────────────┼──────────────────────────────────┤
  │ /chatHub         │ Gercek zamanli sohbet mesajlasma │
  ├──────────────────┼──────────────────────────────────┤
  │ /notificationHub │ Gercek zamanli push bildirimler  │
  └──────────────────┴──────────────────────────────────┘

  Guvenlik

  - Tum endpoint'lerde JWT Bearer kimlik dogrulama
  - Yetkilendirme politikalari: AdminPolicy, CompanyPolicy, WorkerPolicy, SubscribedCompanyPolicy,
  SubscribedWorkerPolicy
  - Hiz sinirlandirma: IP basina dakikada 100 istek (sabit pencere)
  - CORS: Yapilandiirlabilir izin verilen kaynaklar (varsayilan: http://localhost:5173)

  ---
  Testler

  dotnet test

  Birim testleri tests/ altinda modul basina duzenlenmistir:

  ┌─────────────────────────┬────────────────────────────────┐
  │      Test Projesi       │             Kapsam             │
  ├─────────────────────────┼────────────────────────────────┤
  │ BuildingBlocks.Tests    │ Davranislar, ortak yardimcilar │
  ├─────────────────────────┼────────────────────────────────┤
  │ Chat.Tests              │ Domain, servisler              │
  ├─────────────────────────┼────────────────────────────────┤
  │ Identity.Tests          │ CQRS islemleri                 │
  ├─────────────────────────┼────────────────────────────────┤
  │ Notification.Tests      │ CQRS, domain                   │
  ├─────────────────────────┼────────────────────────────────┤
  │ ProjectManagement.Tests │ CQRS, domain                   │
  ├─────────────────────────┼────────────────────────────────┤
  │ Report.Tests            │ CQRS, domain                   │
  ├─────────────────────────┼────────────────────────────────┤
  │ Stats.Tests             │ Domain                         │
  ├─────────────────────────┼────────────────────────────────┤
  │ Tenant.Tests            │ Domain                         │
  └─────────────────────────┴────────────────────────────────┘

  ---
  Lisans

  Bu proje egitim ve portfolyo amaciyla gelistirilmistir.
