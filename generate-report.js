const puppeteer = require('puppeteer-core');
const path = require('path');

const html = `<!DOCTYPE html>
<html lang="bg">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>EnterpriseDataManager — Пълен Одит на Проекта</title>
<style>
  * { box-sizing: border-box; margin: 0; padding: 0; }
  body {
    font-family: 'Segoe UI', Arial, sans-serif;
    font-size: 11px;
    color: #1a1a2e;
    background: #fff;
    padding: 0;
  }
  .cover {
    background: linear-gradient(135deg, #1a1a2e 0%, #16213e 50%, #0f3460 100%);
    color: white;
    height: 100vh;
    display: flex;
    flex-direction: column;
    justify-content: center;
    align-items: center;
    text-align: center;
    padding: 60px;
    page-break-after: always;
  }
  .cover-logo {
    font-size: 52px;
    margin-bottom: 30px;
  }
  .cover h1 {
    font-size: 32px;
    font-weight: 700;
    letter-spacing: 2px;
    margin-bottom: 12px;
    color: #e2e8f0;
  }
  .cover h2 {
    font-size: 18px;
    font-weight: 400;
    color: #a0aec0;
    margin-bottom: 50px;
  }
  .cover .meta {
    border-top: 1px solid rgba(255,255,255,0.2);
    padding-top: 30px;
    color: #cbd5e0;
    font-size: 12px;
    line-height: 2;
  }
  .cover .badge {
    display: inline-block;
    background: #e94560;
    color: white;
    padding: 6px 18px;
    border-radius: 20px;
    font-size: 12px;
    font-weight: 600;
    margin-top: 20px;
    letter-spacing: 1px;
  }
  .page {
    padding: 50px 55px;
    page-break-after: always;
  }
  .page:last-child { page-break-after: auto; }
  h2.section-title {
    font-size: 20px;
    font-weight: 700;
    color: #0f3460;
    border-bottom: 3px solid #e94560;
    padding-bottom: 8px;
    margin-bottom: 20px;
    margin-top: 10px;
  }
  h3.sub-title {
    font-size: 14px;
    font-weight: 700;
    color: #16213e;
    margin: 20px 0 10px 0;
    padding-left: 10px;
    border-left: 4px solid #0f3460;
  }
  h4 {
    font-size: 12px;
    font-weight: 600;
    color: #2d3748;
    margin: 15px 0 6px 0;
  }
  p { line-height: 1.7; margin-bottom: 10px; color: #2d3748; }
  table {
    width: 100%;
    border-collapse: collapse;
    margin-bottom: 20px;
    font-size: 10.5px;
  }
  thead tr {
    background: #0f3460;
    color: white;
  }
  thead th {
    padding: 9px 12px;
    text-align: left;
    font-weight: 600;
    letter-spacing: 0.3px;
  }
  tbody tr:nth-child(even) { background: #f7f9fc; }
  tbody tr:nth-child(odd) { background: #ffffff; }
  tbody tr:hover { background: #ebf4ff; }
  td {
    padding: 7px 12px;
    border-bottom: 1px solid #e2e8f0;
    vertical-align: top;
    line-height: 1.5;
  }
  .status-ok {
    color: #276749;
    font-weight: 600;
    background: #f0fff4;
    padding: 2px 8px;
    border-radius: 10px;
    white-space: nowrap;
  }
  .status-stub {
    color: #c05621;
    font-weight: 600;
    background: #fffaf0;
    padding: 2px 8px;
    border-radius: 10px;
    white-space: nowrap;
  }
  .status-missing {
    color: #9b2c2c;
    font-weight: 600;
    background: #fff5f5;
    padding: 2px 8px;
    border-radius: 10px;
    white-space: nowrap;
  }
  .status-partial {
    color: #744210;
    font-weight: 600;
    background: #fffff0;
    padding: 2px 8px;
    border-radius: 10px;
    white-space: nowrap;
  }
  .callout {
    background: #ebf8ff;
    border-left: 5px solid #3182ce;
    padding: 14px 18px;
    border-radius: 4px;
    margin: 16px 0;
    font-size: 11px;
    line-height: 1.7;
  }
  .callout.warning {
    background: #fffbeb;
    border-color: #d97706;
  }
  .callout.danger {
    background: #fff5f5;
    border-color: #e53e3e;
  }
  .callout.success {
    background: #f0fff4;
    border-color: #38a169;
  }
  .phase-box {
    border: 1px solid #e2e8f0;
    border-radius: 8px;
    padding: 16px 20px;
    margin-bottom: 16px;
    background: #fafbff;
  }
  .phase-box .phase-header {
    font-size: 13px;
    font-weight: 700;
    color: #0f3460;
    margin-bottom: 10px;
    display: flex;
    align-items: center;
    gap: 8px;
  }
  .phase-num {
    background: #0f3460;
    color: white;
    width: 24px;
    height: 24px;
    border-radius: 50%;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    font-size: 11px;
    font-weight: 700;
    flex-shrink: 0;
  }
  ol { padding-left: 20px; }
  ol li { margin-bottom: 6px; line-height: 1.6; color: #2d3748; }
  .price-highlight {
    font-weight: 700;
    color: #0f3460;
    font-size: 12px;
  }
  .total-row td {
    background: #0f3460 !important;
    color: white !important;
    font-weight: 700;
    font-size: 11.5px;
  }
  .footer {
    position: fixed;
    bottom: 0;
    left: 0;
    right: 0;
    background: #f7f9fc;
    border-top: 1px solid #e2e8f0;
    padding: 8px 55px;
    font-size: 9px;
    color: #718096;
    display: flex;
    justify-content: space-between;
  }
  .toc-item {
    display: flex;
    justify-content: space-between;
    padding: 7px 0;
    border-bottom: 1px dotted #e2e8f0;
    font-size: 12px;
  }
  .toc-item .toc-page { color: #718096; }
  .progress-bar {
    background: #e2e8f0;
    border-radius: 10px;
    height: 12px;
    margin: 4px 0 12px 0;
    overflow: hidden;
  }
  .progress-fill {
    height: 100%;
    border-radius: 10px;
    background: linear-gradient(90deg, #0f3460, #e94560);
  }
</style>
</head>
<body>

<!-- COVER PAGE -->
<div class="cover">
  <div class="cover-logo">🏢</div>
  <h1>EnterpriseDataManager</h1>
  <h2>Пълен Технически Одит на Проекта</h2>
  <div class="meta">
    <div><strong>Тип документ:</strong> Анализ на покритие и план за изпълнение</div>
    <div><strong>Дата:</strong> 19 Април 2026 г.</div>
    <div><strong>Версия:</strong> 1.0</div>
    <div><strong>Платформа:</strong> ASP.NET Core 8 · PostgreSQL · Docker</div>
  </div>
  <div class="badge">ПОВЕРИТЕЛНО</div>
</div>

<!-- TABLE OF CONTENTS -->
<div class="page">
  <h2 class="section-title">Съдържание</h2>
  <div class="toc-item"><span>1. Контекст и изисквания</span><span class="toc-page">3</span></div>
  <div class="toc-item"><span>2. Общо покритие на проекта</span><span class="toc-page">3</span></div>
  <div class="toc-item"><span>3. Напълно имплементирани функционалности</span><span class="toc-page">4</span></div>
  <div class="toc-item"><span>4. Критични пропуски и stubs</span><span class="toc-page">5</span></div>
  <div class="toc-item"><span>5. Липсващи функционалности по спецификация</span><span class="toc-page">6</span></div>
  <div class="toc-item"><span>6. Строг план за изпълнение</span><span class="toc-page">7</span></div>
  <div class="toc-item"><span>7. Цени за внедряване и поддръжка</span><span class="toc-page">9</span></div>
  <div class="toc-item"><span>8. Обобщение</span><span class="toc-page">11</span></div>

  <br><br>
  <h3 class="sub-title">Обща оценка на завършеност</h3>
  <p>Проектът е на приблизително <strong>60–65% завършеност</strong>. Архитектурната основа е добре изградена, но критични компоненти са заглушени (stubbed) или липсват изцяло.</p>
  <div class="progress-bar"><div class="progress-fill" style="width:63%"></div></div>
  <p style="text-align:center; color:#718096; font-size:10px;">~63% завършен</p>

  <div class="callout warning">
    <strong>⚠ Важно:</strong> Проектът не е готов за продукционно внедряване в текущото си състояние. Критичните stubs в ArchivalJobScheduler, RetentionPolicyEnforcer и HealthCheckMonitor блокират реалната функционалност.
  </div>
</div>

<!-- PAGE 3: Context -->
<div class="page">
  <h2 class="section-title">1. Контекст и изисквания</h2>
  <p>Документът анализира проекта спрямо следните две задължителни системи:</p>

  <h3 class="sub-title">Система за архивиране на информация</h3>
  <table>
    <thead><tr><th>Изискване</th><th>Статус</th></tr></thead>
    <tbody>
      <tr><td>Анализ и изготвяне на план за архивиране</td><td><span class="status-ok">✔ Покрито</span></td></tr>
      <tr><td>Автоматично архивиране от сървъри</td><td><span class="status-partial">⚡ Частично</span></td></tr>
      <tr><td>Автоматично архивиране от работни станции</td><td><span class="status-missing">✗ Липсва</span></td></tr>
      <tr><td>Автоматично архивиране от мобилни устройства</td><td><span class="status-missing">✗ Липсва</span></td></tr>
      <tr><td>Вградена защита от ransomware</td><td><span class="status-ok">✔ Покрито</span></td></tr>
      <tr><td>Интеграция с централизирано управление на достъпа</td><td><span class="status-ok">✔ Покрито (LDAP + OIDC)</span></td></tr>
      <tr><td>Процедури за възстановяване от архив</td><td><span class="status-ok">✔ Покрито</span></td></tr>
      <tr><td>Минимум едно пробно възстановяване</td><td><span class="status-missing">✗ Липсва (UI wizard)</span></td></tr>
      <tr><td>Виртуален/физически сървър за съхранение</td><td><span class="status-ok">✔ Docker + Local/Azure/S3</span></td></tr>
      <tr><td>Лентово устройство/робот за архивиране</td><td><span class="status-partial">⚡ Частично (Mock + CLI)</span></td></tr>
    </tbody>
  </table>

  <h3 class="sub-title">Система за управление на съхранението и споделянето</h3>
  <table>
    <thead><tr><th>Изискване</th><th>Статус</th></tr></thead>
    <tbody>
      <tr><td>Централизирано управление на достъпа на служителите</td><td><span class="status-partial">⚡ Частично (Auth налично, ACL липсва)</span></td></tr>
      <tr><td>Интеграция с останалите системи на организацията</td><td><span class="status-ok">✔ LDAP + OIDC + MFA</span></td></tr>
      <tr><td>Журнален запис на действията на потребителите</td><td><span class="status-ok">✔ Покрито</span></td></tr>
      <tr><td>Виртуален/физически сървър</td><td><span class="status-ok">✔ Docker Compose</span></td></tr>
    </tbody>
  </table>
</div>

<!-- PAGE 4: Implemented -->
<div class="page">
  <h2 class="section-title">2. Напълно имплементирани функционалности</h2>

  <table>
    <thead><tr><th>Функционалност</th><th>Компонент</th><th>Статус</th></tr></thead>
    <tbody>
      <tr><td>Създаване и управление на план за архивиране</td><td>ArchivePlanService</td><td><span class="status-ok">✔ Реален код</span></td></tr>
      <tr><td>Архивни задачи с изпълнение и проследяване</td><td>ArchivalService, ArchiveJob</td><td><span class="status-ok">✔ Реален код</span></td></tr>
      <tr><td>AES-256-GCM криптиране с PBKDF2</td><td>AesEncryptionService</td><td><span class="status-ok">✔ Реален код</span></td></tr>
      <tr><td>Ransomware детекция (аномалии, WORM)</td><td>AnomalyDetector, WormSimulator, ImmutableStorageService</td><td><span class="status-ok">✔ Реален код</span></td></tr>
      <tr><td>LDAP / Active Directory интеграция</td><td>LdapConnector</td><td><span class="status-ok">✔ Реален код</span></td></tr>
      <tr><td>OIDC / OAuth2 / SSO интеграция</td><td>OidcConnector</td><td><span class="status-ok">✔ Реален код</span></td></tr>
      <tr><td>TOTP MFA (Google Authenticator)</td><td>TotpMfaProvider</td><td><span class="status-ok">✔ Реален код</span></td></tr>
      <tr><td>JWT автентикация + refresh token</td><td>IdentityService</td><td><span class="status-ok">✔ Реален код</span></td></tr>
      <tr><td>Задачи за възстановяване + SHA-256 верификация</td><td>RecoveryService</td><td><span class="status-ok">✔ Реален код</span></td></tr>
      <tr><td>Политики за съхранение + правна забрана</td><td>PolicyEngine, RetentionPolicy</td><td><span class="status-ok">✔ Реален код</span></td></tr>
      <tr><td>Журнален запис (Audit Log)</td><td>AuditService, AuditRecord</td><td><span class="status-ok">✔ Реален код</span></td></tr>
      <tr><td>Локална файлова система (path traversal защита)</td><td>LocalFilesystemProvider</td><td><span class="status-ok">✔ Реален код</span></td></tr>
      <tr><td>Azure Blob Storage</td><td>AzureBlobProvider</td><td><span class="status-ok">✔ Реален код</span></td></tr>
      <tr><td>S3/S3-compatible хранилище</td><td>S3CompatibleProvider</td><td><span class="status-ok">✔ Реален код</span></td></tr>
      <tr><td>EF Core + PostgreSQL + миграции</td><td>EnterpriseDataManagerDbContext</td><td><span class="status-ok">✔ Реален код</span></td></tr>
      <tr><td>Security headers (HSTS, CSP, CORS)</td><td>SecurityHeadersMiddleware</td><td><span class="status-ok">✔ Реален код</span></td></tr>
      <tr><td>Структурирано логване (Serilog)</td><td>StructuredLogEnricher</td><td><span class="status-ok">✔ Реален код</span></td></tr>
      <tr><td>Docker Compose (postgres + web)</td><td>docker-compose.yml</td><td><span class="status-ok">✔ Реален код</span></td></tr>
      <tr><td>Локализация (BG + EN)</td><td>Resources/, LanguageController</td><td><span class="status-ok">✔ Реален код</span></td></tr>
      <tr><td>Domain events, Unit of Work, Repository pattern</td><td>Core layer</td><td><span class="status-ok">✔ Реален код</span></td></tr>
    </tbody>
  </table>

  <div class="callout success">
    <strong>✔ Солидна основа:</strong> Всички основни инфраструктурни компоненти са реално имплементирани — криптиране, идентичност, одит, съхранение и domain логика. Проблемите са в нивото на оркестрация и липсващи крайни функционалности.
  </div>
</div>

<!-- PAGE 5: Gaps -->
<div class="page">
  <h2 class="section-title">3. Критични пропуски и Stubs</h2>
  <p>Следните компоненти са налице в кода, но съдържат <strong>фиктивна/заглушена имплементация</strong>, която трябва да бъде заменена преди продукционно пускане:</p>

  <table>
    <thead><tr><th>Компонент</th><th>Файл</th><th>Проблем</th><th>Риск</th></tr></thead>
    <tbody>
      <tr>
        <td><strong>Scheduler не изпълнява реални задачи</strong></td>
        <td>ArchivalJobScheduler.cs · RunJobAsync()</td>
        <td>Връща фиктивни метрики; реалният IArchivalService не се извиква</td>
        <td><span class="status-missing">КРИТИЧЕН</span></td>
      </tr>
      <tr>
        <td><strong>Retention enforcer — твърдо кодирани данни</strong></td>
        <td>RetentionPolicyEnforcer.cs · GetAffectedItemsAsync()</td>
        <td>Връща List{"item1","item2"} — никога не пита базата данни</td>
        <td><span class="status-missing">КРИТИЧЕН</span></td>
      </tr>
      <tr>
        <td><strong>Health checks са симулирани</strong></td>
        <td>HealthCheckMonitor.cs</td>
        <td>DB, cache, мрежови проверки връщат твърдо кодирано true</td>
        <td><span class="status-missing">КРИТИЧЕН</span></td>
      </tr>
      <tr>
        <td><strong>MFA не е персистентно</strong></td>
        <td>InMemoryMfaStateStore</td>
        <td>Всички MFA сесии се губят при рестарт на приложението</td>
        <td><span class="status-stub">ВИСОК</span></td>
      </tr>
      <tr>
        <td><strong>SIEM Forwarders непотвърдени</strong></td>
        <td>CefForwarder.cs, SyslogForwarder.cs</td>
        <td>Вероятно stubs — SIEM интеграцията е под въпрос</td>
        <td><span class="status-stub">ВИСОК</span></td>
      </tr>
      <tr>
        <td><strong>Лентово устройство изисква mt CLI</strong></td>
        <td>TapeDeviceAdapter.cs</td>
        <td>Срива се на Windows без tape driver инструменти</td>
        <td><span class="status-partial">СРЕДЕН</span></td>
      </tr>
    </tbody>
  </table>

  <h2 class="section-title" style="margin-top:25px;">4. Липсващи функционалности по спецификация</h2>
  <table>
    <thead><tr><th>Функционалност</th><th>Изисква се от</th><th>Статус</th></tr></thead>
    <tbody>
      <tr>
        <td><strong>Агент за работни станции</strong> — Windows Service за автоматично архивиране</td>
        <td>Система за архивиране</td>
        <td><span class="status-missing">✗ Напълно липсва</span></td>
      </tr>
      <tr>
        <td><strong>Мобилно архивиране</strong> — REST API или MDM (Intune) конектор</td>
        <td>Система за архивиране</td>
        <td><span class="status-missing">✗ Напълно липсва</span></td>
      </tr>
      <tr>
        <td><strong>Тестово пробно възстановяване</strong> — UI wizard в симулационен режим</td>
        <td>Система за архивиране</td>
        <td><span class="status-missing">✗ Напълно липсва</span></td>
      </tr>
      <tr>
        <td><strong>ACL права на ниво файл/ресурс</strong> — FilePermission entity + управление</td>
        <td>Система за управление на съхранение</td>
        <td><span class="status-missing">✗ Напълно липсва</span></td>
      </tr>
      <tr>
        <td><strong>Споделяне на файлове</strong> — генериране на споделени връзки с права</td>
        <td>Система за управление на съхранение</td>
        <td><span class="status-missing">✗ Напълно липсва</span></td>
      </tr>
      <tr>
        <td><strong>Табло с реални метрики</strong> — Dashboard с живи данни</td>
        <td>Обща платформа</td>
        <td><span class="status-partial">⚡ Scaffold само</span></td>
      </tr>
      <tr>
        <td><strong>Модул за отчети</strong> — PDF/Excel експорт на одит и история</td>
        <td>Обща платформа</td>
        <td><span class="status-partial">⚡ Scaffold само</span></td>
      </tr>
    </tbody>
  </table>
</div>

<!-- PAGE 6: Plan -->
<div class="page">
  <h2 class="section-title">5. Строг план за изпълнение</h2>

  <div class="phase-box">
    <div class="phase-header"><span class="phase-num">1</span> Фаза 1 — Поправяне на критичните Stubs &nbsp;|&nbsp; 2–3 седмици</div>
    <ol>
      <li>Свързване на <code>ArchivalJobScheduler.RunJobAsync()</code> → извикване на реалния <code>IArchivalService.CreateAndStartJobAsync()</code></li>
      <li>Поправяне на <code>RetentionPolicyEnforcer.GetAffectedItemsAsync()</code> → заявка към <code>IArchiveItemRepository</code> с филтър по план/политика</li>
      <li>Замяна на всички твърдо кодирани проверки в <code>HealthCheckMonitor</code> с реални: <code>DbContext.Database.CanConnectAsync()</code>, <code>IStorageProviderAdapter.HealthCheckAsync()</code> и т.н.</li>
      <li>Имплементиране на DB-базиран <code>IMfaStateStore</code> (EF Core таблица: <code>MfaStates</code>)</li>
      <li>Верификация и завършване на <code>CefForwarder</code> / <code>SyslogForwarder</code> — реално TCP/UDP syslog предаване</li>
    </ol>
  </div>

  <div class="phase-box">
    <div class="phase-header"><span class="phase-num">2</span> Фаза 2 — Персистентност и надеждност &nbsp;|&nbsp; 1–2 седмици</div>
    <ol>
      <li>Добавяне на персистентно проследяване на <code>LastRun</code> в <code>ArchivalJobScheduler</code> (запис в БД)</li>
      <li>Добавяне на поддръжка на Windows Driver за <code>TapeDeviceAdapter</code> или интеграция с vendor SDK (LTO Ultrium, HP StoreEver)</li>
      <li>Добавяне на retry механизъм + опашка за неуспешни архивни задачи</li>
      <li>Свързване на <code>EmailService</code> / <code>NotificationService</code> към събитията за завършване и неуспех на задачи</li>
    </ol>
  </div>

  <div class="phase-box">
    <div class="phase-header"><span class="phase-num">3</span> Фаза 3 — Липсващи функционалности по спецификация &nbsp;|&nbsp; 3–4 седмици</div>
    <ol>
      <li><strong>Агент за работни станции</strong> — лека Windows Service (.NET Worker), която извиква Archive API-то автоматично по разписание</li>
      <li><strong>Архивиране от мобилни устройства</strong> — REST API endpoint приемащ качване на файлове от iOS/Android или интеграция с MDM (Microsoft Intune) webhook</li>
      <li><strong>Тестово пробно възстановяване</strong> — UI wizard, който клонира реален <code>RecoveryJob</code> в симулационен режим, отчита успех/неуспех без презаписване на продукционни данни</li>
      <li><strong>Споделяне на файлове и ACL</strong> — добавяне на entity <code>FilePermission</code> (UserId, ResourceId, AccessLevel), <code>IShareService</code>, интерфейс за управление</li>
    </ol>
  </div>

  <div class="phase-box">
    <div class="phase-header"><span class="phase-num">4</span> Фаза 4 — Завършване на интерфейса &nbsp;|&nbsp; 2–3 седмици</div>
    <ol>
      <li>Табло — свързване на <code>DashboardApiController</code> с реални агрегирани заявки (задачи днес, използвано хранилище, последни одит събития, брой аномалии)</li>
      <li>Отчети — PDF/Excel експорт на одит логове, история на задачи, използване на хранилище (FastReport или NPOI)</li>
      <li>Интерфейс за управление на доставчици на съхранение (в момента само API)</li>
      <li>Одит лог UI — пълен екран за търсене, филтриране и експорт</li>
    </ol>
  </div>

  <div class="phase-box">
    <div class="phase-header"><span class="phase-num">5</span> Фаза 5 — Подготовка за продукция &nbsp;|&nbsp; 1–2 седмици</div>
    <ol>
      <li>Продукционен Docker Compose профил (secrets, TLS терминация чрез nginx/traefik)</li>
      <li>CI/CD pipeline — завършване на GitHub Actions: build → test → SAST сканиране → Docker push → deploy</li>
      <li>Натоварващо/стрес тестване на архивните задачи (симулация с 10 000 файла)</li>
      <li>Контролен списък за penetration тест спрямо OWASP Top 10</li>
    </ol>
  </div>
</div>

<!-- PAGE 7: Pricing -->
<div class="page">
  <h2 class="section-title">6. Цени за внедряване и поддръжка</h2>
  <p style="color:#718096; font-size:10px; margin-bottom:16px;">* Всички цени са приблизителни оценки в лева (BGN), базирани на пазарни нива в България.</p>

  <h3 class="sub-title">Еднократни разходи за завършване на разработката</h3>
  <table>
    <thead><tr><th>Фаза</th><th>Усилие</th><th>Минимална цена</th><th>Максимална цена</th></tr></thead>
    <tbody>
      <tr><td>Фаза 1 — Поправка на stubs</td><td>2–3 седмици × 1 старши разработчик</td><td>5 800 лв.</td><td>9 700 лв.</td></tr>
      <tr><td>Фаза 2 — Надеждност и персистентност</td><td>1–2 седмици</td><td>2 900 лв.</td><td>5 800 лв.</td></tr>
      <tr><td>Фаза 3 — Липсващи функционалности</td><td>3–4 седмици</td><td>9 700 лв.</td><td>17 500 лв.</td></tr>
      <tr><td>Фаза 4 — Завършване на UI</td><td>2–3 седмици</td><td>5 800 лв.</td><td>11 700 лв.</td></tr>
      <tr><td>Фаза 5 — Втвърдяване + QA</td><td>1–2 седмици</td><td>3 900 лв.</td><td>7 800 лв.</td></tr>
      <tr class="total-row"><td><strong>ОБЩО оставаща разработка</strong></td><td><strong>~3–4 месеца</strong></td><td><strong>28 100 лв.</strong></td><td><strong>52 500 лв.</strong></td></tr>
    </tbody>
  </table>

  <h3 class="sub-title">Инфраструктура — Вариант А: Облак (препоръчително)</h3>
  <table>
    <thead><tr><th>Елемент</th><th>Описание</th><th>Месечна цена</th></tr></thead>
    <tbody>
      <tr><td>Виртуален сървър</td><td>4 vCPU, 16 GB RAM (Azure/AWS)</td><td>230 – 390 лв.</td></tr>
      <tr><td>Управлявана PostgreSQL БД</td><td>Azure Database / RDS</td><td>155 – 290 лв.</td></tr>
      <tr><td>Blob/S3 хранилище</td><td>1–5 TB данни</td><td>100 – 390 лв.</td></tr>
      <tr><td>Load balancer + TLS</td><td>HTTPS терминация</td><td>60 – 115 лв.</td></tr>
      <tr><td>Мониторинг</td><td>Grafana Cloud / Azure Monitor</td><td>0 – 100 лв.</td></tr>
      <tr class="total-row"><td><strong>ОБЩО / месец</strong></td><td></td><td><strong>545 – 1 285 лв.</strong></td></tr>
    </tbody>
  </table>

  <h3 class="sub-title">Инфраструктура — Вариант Б: На място (при изискване за лентово устройство)</h3>
  <table>
    <thead><tr><th>Елемент</th><th>Описание</th><th>Еднократна цена</th></tr></thead>
    <tbody>
      <tr><td>Виртуален сървър</td><td>Dell/HPE, 32 GB RAM</td><td>5 800 – 11 700 лв.</td></tr>
      <tr><td>LTO-8 лентово устройство</td><td>HPE StoreEver или еквивалент</td><td>4 900 – 9 700 лв.</td></tr>
      <tr><td>Лентови касети</td><td>20 броя LTO-8 (по 12 TB)</td><td>1 200 – 2 300 лв.</td></tr>
      <tr><td>UPS + стойка (rack)</td><td>Физическа инфраструктура</td><td>1 600 – 2 900 лв.</td></tr>
      <tr><td>Настройка на ОС и платформа</td><td>Windows Server или Linux</td><td>1 000 – 2 000 лв.</td></tr>
      <tr class="total-row"><td><strong>ОБЩО еднократно</strong></td><td></td><td><strong>14 500 – 28 600 лв.</strong></td></tr>
    </tbody>
  </table>
</div>

<!-- PAGE 8: Maintenance + Summary -->
<div class="page">
  <h3 class="sub-title">Годишни разходи за поддръжка</h3>
  <table>
    <thead><tr><th>Елемент</th><th>Описание</th><th>Цена / Година</th></tr></thead>
    <tbody>
      <tr><td>Облачна инфраструктура</td><td>12 месеца × месечна цена</td><td>6 500 – 15 400 лв.</td></tr>
      <tr><td>Актуализации за сигурност и кръпки</td><td>Месечни patch cycles</td><td>2 000 – 4 000 лв.</td></tr>
      <tr><td>Подновяване на лицензи</td><td>Vendor SDKs, ако е приложимо</td><td>0 – 1 000 лв.</td></tr>
      <tr><td>Договор за поддръжка</td><td>Разработчик при нужда, 8 ч/месец</td><td>5 800 – 11 700 лв.</td></tr>
      <tr><td>Годишно тестово пробно възстановяване</td><td>Drill + отчет до ръководство</td><td>1 000 – 2 000 лв.</td></tr>
      <tr class="total-row"><td><strong>ОБЩО годишна поддръжка</strong></td><td></td><td><strong>15 300 – 34 100 лв.</strong></td></tr>
    </tbody>
  </table>

  <h2 class="section-title" style="margin-top:30px;">7. Обобщение</h2>
  <table>
    <thead><tr><th>Категория</th><th>Минимална оценка</th><th>Максимална оценка</th></tr></thead>
    <tbody>
      <tr><td>Оставаща разработка</td><td class="price-highlight">28 100 лв.</td><td class="price-highlight">52 500 лв.</td></tr>
      <tr><td>Инфраструктура — облак (еднократно)</td><td class="price-highlight">0 лв.</td><td class="price-highlight">3 900 лв.</td></tr>
      <tr><td>Инфраструктура — на място (еднократно)</td><td class="price-highlight">14 500 лв.</td><td class="price-highlight">28 600 лв.</td></tr>
      <tr><td>Годишна поддръжка</td><td class="price-highlight">15 300 лв.</td><td class="price-highlight">34 100 лв.</td></tr>
    </tbody>
  </table>

  <div class="callout">
    <strong>Общ извод:</strong> Проектът разполага с <strong>добре архитектирана основа (~60–65% завършен)</strong>. Всички критични инфраструктурни компоненти — криптиране, идентичност, одит, съхранение — са реално имплементирани. Най-спешните задачи са поправката на стабовете в background job слоя, добавянето на агенти за работни станции и мобилни устройства, и имплементацията на ACL/споделяне — всички изрично изисквани от спецификацията.
  </div>

  <div class="callout warning">
    <strong>Препоръчителна следваща стъпка:</strong> Стартирайте с Фаза 1 (2–3 седмици), за да направите системата функционално коректна. Само след нея приложението може да се счита за минимално работещо (MVP) за тестова среда.
  </div>

  <br>
  <p style="text-align:center; color:#a0aec0; font-size:10px; margin-top: 40px;">
    EnterpriseDataManager · Технически одит · Версия 1.0 · Април 2026 г.<br>
    Документът е генериран автоматично въз основа на анализ на изходния код.
  </p>
</div>

<div class="footer">
  <span>EnterpriseDataManager — Технически Одит v1.0</span>
  <span>Поверително · Април 2026</span>
</div>

</body>
</html>`;

(async () => {
  const browser = await puppeteer.launch({
    executablePath: 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe',
    headless: true,
    args: ['--no-sandbox', '--disable-setuid-sandbox']
  });

  const page = await browser.newPage();
  await page.setContent(html, { waitUntil: 'networkidle0' });

  const outputPath = path.resolve('C:\\WORK_PLACE\\EnterpriseDataManager\\EDM_Project_Audit_Report_BG.pdf');

  await page.pdf({
    path: outputPath,
    format: 'A4',
    printBackground: true,
    margin: { top: '0', right: '0', bottom: '20mm', left: '0' },
    displayHeaderFooter: false
  });

  await browser.close();
  console.log('PDF generated:', outputPath);
})();
