# Weather Resilience, Multi-Provider Fallback and Offline Caching Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Provide rock-solid, uninterrupted weather data to Tarla Asistanı users by introducing WeatherAPI.com integration, a multi-provider fallback architecture in the backend (WeatherAPI + Open-Meteo), and offline-first local caching in the Flutter mobile app.

**Architecture:**
- **Backend:** Introduce `WeatherApiWeatherProvider` (WeatherAPI.com) alongside `OpenMeteoWeatherProvider`. Combine them inside a resilient `FallbackWeatherProvider` implementing `IWeatherProvider`. If the primary provider hits rate-limits (HTTP 429), timeouts, or network errors, it seamlessly switches to the secondary provider in milliseconds without propagating errors to the caller. Increase memory cache TTL to 30 minutes in `GetFarmWeatherQueryHandler`.
- **Mobile:** Introduce `LocalWeatherRepository` (SQLite `weather_cache` table) with stale-while-revalidate fallback in `BackendWeatherRepository`. If the network drops or the server is momentarily unreachable, the UI displays the last cached weather instead of a red error card.

**Tech Stack:** ASP.NET Core 9 Minimal APIs, C# 12, MediatR, Entity Framework Core / PostgreSQL, HttpClient, Flutter / Dart, SQLite (sqflite).

## Global Constraints
- Target Framework: .NET 9 (`TarlaAsistani.Application`, `TarlaAsistani.Infrastructure`, `TarlaAsistani.API`).
- Mobile: Flutter 3.29+, Dart 3.7+, null-safety.
- All existing tests in backend and mobile must continue to pass without regressions.
- No hardcoded API secrets: all API keys must be read from environment variables or `appsettings.json`.

---

### Task 1: Backend - WeatherApiWeatherProvider Implementation & Tests

**Files:**
- Create: `backend/src/TarlaAsistani.Infrastructure/Services/WeatherApiWeatherProvider.cs`
- Test: `backend/tests/TarlaAsistani.UnitTests/Features/Weather/WeatherApiWeatherProviderTests.cs`

**Interfaces:**
- Consumes: `IWeatherProvider`, `HttpClient`, `IConfiguration`, `WeatherPoint`, `WeatherForecastData`, `CurrentWeatherDto`, `DailyForecastDto`.
- Produces: `WeatherApiWeatherProvider : IWeatherProvider` with `Name => "weather_api"`.

- [ ] **Step 1: Write the failing unit tests for WeatherApiWeatherProvider**

Create `backend/tests/TarlaAsistani.UnitTests/Features/Weather/WeatherApiWeatherProviderTests.cs`:
```csharp
using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using TarlaAsistani.Infrastructure.Services;
using Xunit;

namespace TarlaAsistani.UnitTests.Features.Weather;

public class WeatherApiWeatherProviderTests
{
    private const string SampleWeatherApiResponse = """
    {
      "location": {
        "name": "Konya",
        "lat": 37.87,
        "lon": 32.48,
        "localtime": "2026-09-04 15:00"
      },
      "current": {
        "temp_c": 28.5,
        "feelslike_c": 27.2,
        "humidity": 22,
        "wind_kph": 15.0,
        "gust_kph": 25.0,
        "precip_mm": 0.0,
        "condition": {
          "text": "Güneşli",
          "code": 1000
        }
      },
      "forecast": {
        "forecastday": [
          {
            "date": "2026-09-04",
            "day": {
              "maxtemp_c": 30.0,
              "mintemp_c": 16.0,
              "totalprecip_mm": 0.0,
              "daily_chance_of_rain": 10,
              "condition": {
                "text": "Güneşli",
                "code": 1000
              }
            },
            "hour": [
              {
                "time": "2026-09-04 12:00",
                "temp_c": 27.0,
                "chance_of_rain": 10,
                "precip_mm": 0.0,
                "wind_kph": 12.0,
                "humidity": 25,
                "condition": {
                  "text": "Açık",
                  "code": 1000
                }
              },
              {
                "time": "2026-09-04 13:00",
                "temp_c": 28.5,
                "chance_of_rain": 10,
                "precip_mm": 0.0,
                "wind_kph": 15.0,
                "humidity": 22,
                "condition": {
                  "text": "Güneşli",
                  "code": 1000
                }
              }
            ]
          }
        ]
      }
    }
    """;

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;
        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) => _handler = handler;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_handler(request));
    }

    [Fact]
    public async Task GetWeatherAsync_ParsesWeatherApiResponse_Correctly()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Weather:WeatherApiKey"] = "test-api-key"
            })
            .Build();

        var handler = new MockHttpMessageHandler(req =>
        {
            req.RequestUri!.ToString().Should().Contain("key=test-api-key");
            req.RequestUri.ToString().Should().Contain("q=37.87,32.48");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SampleWeatherApiResponse)
            };
        });

        var client = new HttpClient(handler);
        var provider = new WeatherApiWeatherProvider(client, config);

        var result = await provider.GetWeatherAsync(37.87, 32.48);

        result.Should().NotBeNull();
        result.Current.Should().NotBeNull();
        result.Current!.TemperatureC.Should().Be(28.5);
        result.Current.FeelsLikeC.Should().Be(27.2);
        result.Current.Condition.Should().Be("Güneşli");
        result.Current.HumidityPercent.Should().Be(22);
        result.Current.WindSpeedKmh.Should().Be(15.0);
        result.Points.Should().HaveCount(2);
        result.Daily.Should().HaveCount(1);
        result.Daily![0].MaxTempC.Should().Be(30.0);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/TarlaAsistani.UnitTests --filter "WeatherApiWeatherProviderTests"`
Expected: FAIL with compilation error (type `WeatherApiWeatherProvider` not found).

- [ ] **Step 3: Implement WeatherApiWeatherProvider**

Create `backend/src/TarlaAsistani.Infrastructure/Services/WeatherApiWeatherProvider.cs`:
```csharp
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Weather.DTOs;

namespace TarlaAsistani.Infrastructure.Services;

public class WeatherApiWeatherProvider : IWeatherProvider
{
    public string Name => "weather_api";

    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string? _apiKey;

    public WeatherApiWeatherProvider(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _baseUrl = config["Weather:WeatherApiBaseUrl"]
            ?? Environment.GetEnvironmentVariable("WEATHER_API_BASE_URL")
            ?? "https://api.weatherapi.com/v1";

        _apiKey = config["Weather:WeatherApiKey"]
            ?? Environment.GetEnvironmentVariable("WEATHER_API_KEY");
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public async Task<List<WeatherPoint>> ForecastAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        var data = await GetWeatherAsync(latitude, longitude, cancellationToken);
        return data.Points;
    }

    public async Task<WeatherForecastData> GetWeatherAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new InvalidOperationException("WeatherAPI anahtarı yapılandırılmamış.");
        }

        var latStr = latitude.ToString("F4", CultureInfo.InvariantCulture);
        var lonStr = longitude.ToString("F4", CultureInfo.InvariantCulture);
        var url = $"{_baseUrl.TrimEnd('/')}/forecast.json?key={_apiKey}&q={latStr},{lonStr}&days=3&aqi=no&alerts=no&lang=tr";

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        {
            throw new InvalidOperationException("WeatherAPI servis istek sınırına ulaştı (429).");
        }
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseResponse(json);
    }

    public async Task<List<WeatherForecastData>> GetWeatherBatchAsync(
        IReadOnlyList<(double Latitude, double Longitude)> coordinates,
        CancellationToken cancellationToken = default)
    {
        var results = new List<WeatherForecastData>(coordinates.Count);
        foreach (var (lat, lon) in coordinates)
        {
            results.Add(await GetWeatherAsync(lat, lon, cancellationToken));
        }
        return results;
    }

    private static WeatherForecastData ParseResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        CurrentWeatherDto? currentDto = null;
        if (root.TryGetProperty("current", out var currentEl))
        {
            var temp = currentEl.GetProperty("temp_c").GetDouble();
            var feelsLike = currentEl.TryGetProperty("feelslike_c", out var fl) ? fl.GetDouble() : temp;
            var humidity = currentEl.TryGetProperty("humidity", out var hum) ? hum.GetDouble() : (double?)null;
            var windKph = currentEl.TryGetProperty("wind_kph", out var wk) ? wk.GetDouble() : (double?)null;
            var gustKph = currentEl.TryGetProperty("gust_kph", out var gk) ? gk.GetDouble() : (double?)null;
            string? conditionText = null;
            int? code = null;
            if (currentEl.TryGetProperty("condition", out var condEl))
            {
                conditionText = condEl.TryGetProperty("text", out var t) ? t.GetString() : null;
                code = condEl.TryGetProperty("code", out var c) ? c.GetInt32() : (int?)null;
            }

            currentDto = new CurrentWeatherDto(
                ObservedAt: DateTime.UtcNow,
                TemperatureC: temp,
                FeelsLikeC: feelsLike,
                HumidityPercent: humidity,
                WindSpeedKmh: windKph,
                WindGustsKmh: gustKph,
                Condition: conditionText,
                WeatherCode: code
            );
        }

        var points = new List<WeatherPoint>();
        var dailyList = new List<DailyForecastDto>();

        if (root.TryGetProperty("forecast", out var forecastEl) &&
            forecastEl.TryGetProperty("forecastday", out var forecastDayArray))
        {
            foreach (var dayEl in forecastDayArray.EnumerateArray())
            {
                DateOnly? date = null;
                if (dayEl.TryGetProperty("date", out var dStr) && DateOnly.TryParse(dStr.GetString(), out var parsedDate))
                {
                    date = parsedDate;
                }

                double? maxTemp = null;
                double? minTemp = null;
                double? precipMm = null;
                int? rainChance = null;
                string? dayCond = null;

                if (dayEl.TryGetProperty("day", out var dayData))
                {
                    maxTemp = dayData.TryGetProperty("maxtemp_c", out var mt) ? mt.GetDouble() : (double?)null;
                    minTemp = dayData.TryGetProperty("mintemp_c", out var mnt) ? mnt.GetDouble() : (double?)null;
                    precipMm = dayData.TryGetProperty("totalprecip_mm", out var tp) ? tp.GetDouble() : (double?)null;
                    rainChance = dayData.TryGetProperty("daily_chance_of_rain", out var dc) ? dc.GetInt32() : (int?)null;
                    if (dayData.TryGetProperty("condition", out var dcEl) && dcEl.TryGetProperty("text", out var dt))
                    {
                        dayCond = dt.GetString();
                    }
                }

                if (date.HasValue)
                {
                    dailyList.Add(new DailyForecastDto(
                        Date: date.Value,
                        WeatherCode: null,
                        TemperatureMaxC: maxTemp,
                        TemperatureMinC: minTemp,
                        PrecipitationMm: precipMm,
                        PrecipitationProbabilityMax: rainChance,
                        Condition: dayCond
                    ));
                }

                if (dayEl.TryGetProperty("hour", out var hourArray))
                {
                    foreach (var h in hourArray.EnumerateArray())
                    {
                        DateTime observed = DateTime.UtcNow;
                        if (h.TryGetProperty("time", out var hTime) && DateTime.TryParse(hTime.GetString(), out var parsedObserved))
                        {
                            observed = parsedObserved.ToUniversalTime();
                        }

                        var hTemp = h.TryGetProperty("temp_c", out var ht) ? ht.GetDouble() : (double?)null;
                        var hChance = h.TryGetProperty("chance_of_rain", out var hc) ? hc.GetDouble() : (double?)null;
                        var hPrecip = h.TryGetProperty("precip_mm", out var hp) ? hp.GetDouble() : (double?)null;
                        var hWind = h.TryGetProperty("wind_kph", out var hw) ? hw.GetDouble() : (double?)null;
                        var hHum = h.TryGetProperty("humidity", out var hh) ? hh.GetDouble() : (double?)null;
                        int? hCode = null;
                        if (h.TryGetProperty("condition", out var hCond) && hCond.TryGetProperty("code", out var hcCode))
                        {
                            hCode = hcCode.GetInt32();
                        }

                        points.Add(new WeatherPoint(
                            ObservedAt: observed,
                            TemperatureC: hTemp,
                            PrecipitationProbability: hChance,
                            PrecipitationMm: hPrecip,
                            WindSpeedKmh: hWind,
                            HumidityPercent: hHum,
                            WeatherCode: hCode
                        ));
                    }
                }
            }
        }

        return new WeatherForecastData(points, currentDto, dailyList);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test backend/tests/TarlaAsistani.UnitTests --filter "WeatherApiWeatherProviderTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add backend/src/TarlaAsistani.Infrastructure/Services/WeatherApiWeatherProvider.cs backend/tests/TarlaAsistani.UnitTests/Features/Weather/WeatherApiWeatherProviderTests.cs
git commit -m "feat(weather): implement WeatherApiWeatherProvider with unit tests"
```

---

### Task 2: Backend - Resilient FallbackWeatherProvider & DI Wiring

**Files:**
- Create: `backend/src/TarlaAsistani.Infrastructure/Services/FallbackWeatherProvider.cs`
- Test: `backend/tests/TarlaAsistani.UnitTests/Features/Weather/FallbackWeatherProviderTests.cs`
- Modify: `backend/src/TarlaAsistani.Infrastructure/DependencyInjection.cs:70-75`
- Modify: `backend/src/TarlaAsistani.API/appsettings.json:31-36`

**Interfaces:**
- Consumes: `IWeatherProvider`, `ILogger<FallbackWeatherProvider>`, `WeatherApiWeatherProvider`, `OpenMeteoWeatherProvider`.
- Produces: `FallbackWeatherProvider : IWeatherProvider`.

- [ ] **Step 1: Write unit tests for FallbackWeatherProvider**

Create `backend/tests/TarlaAsistani.UnitTests/Features/Weather/FallbackWeatherProviderTests.cs`:
```csharp
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Weather.DTOs;
using TarlaAsistani.Infrastructure.Services;
using Xunit;

namespace TarlaAsistani.UnitTests.Features.Weather;

public class FallbackWeatherProviderTests
{
    [Fact]
    public async Task GetWeatherAsync_UsesPrimaryProvider_WhenSuccessful()
    {
        var primaryMock = new Mock<IWeatherProvider>();
        primaryMock.Setup(p => p.Name).Returns("primary");
        var expectedData = new WeatherForecastData(new List<WeatherPoint>());
        primaryMock.Setup(p => p.GetWeatherAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedData);

        var secondaryMock = new Mock<IWeatherProvider>();
        secondaryMock.Setup(p => p.Name).Returns("secondary");

        var fallbackProvider = new FallbackWeatherProvider(
            new[] { primaryMock.Object, secondaryMock.Object },
            NullLogger<FallbackWeatherProvider>.Instance);

        var result = await fallbackProvider.GetWeatherAsync(37.0, 32.0);

        result.Should().BeSameAs(expectedData);
        secondaryMock.Verify(s => s.GetWeatherAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetWeatherAsync_FallsBackToSecondaryProvider_WhenPrimaryThrows()
    {
        var primaryMock = new Mock<IWeatherProvider>();
        primaryMock.Setup(p => p.Name).Returns("primary");
        primaryMock.Setup(p => p.GetWeatherAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Primary rate limited (429)"));

        var fallbackData = new WeatherForecastData(new List<WeatherPoint>());
        var secondaryMock = new Mock<IWeatherProvider>();
        secondaryMock.Setup(p => p.Name).Returns("secondary");
        secondaryMock.Setup(p => p.GetWeatherAsync(It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fallbackData);

        var fallbackProvider = new FallbackWeatherProvider(
            new[] { primaryMock.Object, secondaryMock.Object },
            NullLogger<FallbackWeatherProvider>.Instance);

        var result = await fallbackProvider.GetWeatherAsync(37.0, 32.0);

        result.Should().BeSameAs(fallbackData);
        fallbackProvider.Name.Should().Be("secondary");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/TarlaAsistani.UnitTests --filter "FallbackWeatherProviderTests"`
Expected: FAIL with compilation error (type `FallbackWeatherProvider` not found).

- [ ] **Step 3: Implement FallbackWeatherProvider**

Create `backend/src/TarlaAsistani.Infrastructure/Services/FallbackWeatherProvider.cs`:
```csharp
using Microsoft.Extensions.Logging;
using TarlaAsistani.Application.Common.Interfaces;
using TarlaAsistani.Application.Features.Weather.DTOs;

namespace TarlaAsistani.Infrastructure.Services;

public class FallbackWeatherProvider : IWeatherProvider
{
    private readonly IReadOnlyList<IWeatherProvider> _providers;
    private readonly ILogger<FallbackWeatherProvider> _logger;
    private string _activeProviderName = "composite_weather";

    public FallbackWeatherProvider(
        IEnumerable<IWeatherProvider> providers,
        ILogger<FallbackWeatherProvider> logger)
    {
        _providers = providers.ToList();
        _logger = logger;
        if (_providers.Count > 0)
        {
            _activeProviderName = _providers[0].Name;
        }
    }

    public string Name => _activeProviderName;

    public async Task<List<WeatherPoint>> ForecastAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        var data = await GetWeatherAsync(latitude, longitude, cancellationToken);
        return data.Points;
    }

    public async Task<WeatherForecastData> GetWeatherAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        Exception? lastException = null;

        foreach (var provider in _providers)
        {
            try
            {
                var result = await provider.GetWeatherAsync(latitude, longitude, cancellationToken);
                if (result?.Points != null && result.Points.Count > 0)
                {
                    _activeProviderName = provider.Name;
                    return result;
                }
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Weather provider {ProviderName} failed for ({Lat}, {Lon}). Trying next provider...", provider.Name, latitude, longitude);
            }
        }

        throw new InvalidOperationException("Tüm hava durumu sağlayıcıları yanıt vermedi.", lastException);
    }

    public async Task<List<WeatherForecastData>> GetWeatherBatchAsync(
        IReadOnlyList<(double Latitude, double Longitude)> coordinates,
        CancellationToken cancellationToken = default)
    {
        Exception? lastException = null;

        foreach (var provider in _providers)
        {
            try
            {
                var result = await provider.GetWeatherBatchAsync(coordinates, cancellationToken);
                if (result != null && result.Count == coordinates.Count && result.All(r => r.Points != null && r.Points.Count > 0))
                {
                    _activeProviderName = provider.Name;
                    return result;
                }
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Batch weather provider {ProviderName} failed. Trying next provider...", provider.Name);
            }
        }

        throw new InvalidOperationException("Tüm hava durumu sağlayıcıları toplu istekte başarısız oldu.", lastException);
    }
}
```

- [ ] **Step 4: Wire DependencyInjection.cs and appsettings.json**

In `backend/src/TarlaAsistani.Infrastructure/DependencyInjection.cs`:
Register both `OpenMeteoWeatherProvider` and `WeatherApiWeatherProvider` as typed HttpClients, and register `IWeatherProvider` with `FallbackWeatherProvider`.
Increase `CacheMinutes` default to 30 minutes in `backend/src/TarlaAsistani.API/appsettings.json`.

- [ ] **Step 5: Run tests and verify they pass**

Run: `dotnet test backend/tests/TarlaAsistani.UnitTests`
Expected: ALL PASS.

- [ ] **Step 6: Commit**

```bash
git add backend/src/TarlaAsistani.Infrastructure/ backend/src/TarlaAsistani.API/appsettings.json backend/tests/TarlaAsistani.UnitTests/
git commit -m "feat(weather): register FallbackWeatherProvider with WeatherAPI and Open-Meteo"
```

---

### Task 3: Mobile - LocalWeatherRepository with SQLite Caching

**Files:**
- Create: `mobile/lib/features/weather/data/local_weather_repository.dart`
- Test: `mobile/test/features/weather/data/local_weather_repository_test.dart`

**Interfaces:**
- Consumes: `DatabaseHelper`, `WeatherSummary`.
- Produces: `LocalWeatherRepository` with `getCachedWeather({String? farmId})`, `cacheWeather({String? farmId, required WeatherSummary weather})`.

- [ ] **Step 1: Write failing test for LocalWeatherRepository**

Create `mobile/test/features/weather/data/local_weather_repository_test.dart`:
```dart
import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/features/weather/data/local_weather_repository.dart';
import 'package:mobile/features/weather/domain/weather_summary.dart';
import 'package:sqflite/sqflite.dart';
import 'package:sqflite_common_ffi/sqflite_ffi.dart';

void main() {
  setUpAll(() {
    sqfliteFfiInit();
    databaseFactory = databaseFactoryFfi;
  });

  group('LocalWeatherRepository', () {
    late Database db;
    late LocalWeatherRepository repo;

    setUp(() async {
      db = await databaseFactoryFfi.openDatabase(inMemoryDatabasePath);
      repo = LocalWeatherRepository(databaseProvider: () async => db);
    });

    tearDown(() async {
      await db.close();
    });

    test('cacheWeather and getCachedWeather roundtrips data correctly', () async {
      final initial = await repo.getCachedWeather(farmId: 'farm-1');
      expect(initial, isNull);

      const summary = WeatherSummary(temperature: 24, description: 'Güneşli');
      await repo.cacheWeather(farmId: 'farm-1', weather: summary);

      final cached = await repo.getCachedWeather(farmId: 'farm-1');
      expect(cached, isNotNull);
      expect(cached!.temperature, equals(24));
      expect(cached.description, equals('Güneşli'));
    });
  });
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `flutter test test/features/weather/data/local_weather_repository_test.dart`
Expected: FAIL (file doesn't exist).

- [ ] **Step 3: Implement LocalWeatherRepository**

Create `mobile/lib/features/weather/data/local_weather_repository.dart`:
```dart
import 'package:flutter/foundation.dart';
import 'package:sqflite/sqflite.dart';
import '../../../services/database_helper.dart';
import '../domain/weather_summary.dart';

class LocalWeatherRepository {
  const LocalWeatherRepository({Future<Database> Function()? databaseProvider})
      : _dbProvider = databaseProvider;

  final Future<Database> Function()? _dbProvider;

  Future<Database> get _database async {
    if (_dbProvider != null) return _dbProvider!();
    return DatabaseHelper.instance.database;
  }

  static const String tableName = 'weather_cache';

  static const String sqlCreateTable = '''
    CREATE TABLE IF NOT EXISTS $tableName (
      farm_id TEXT PRIMARY KEY,
      temperature INTEGER NOT NULL,
      description TEXT NOT NULL,
      updated_at_utc TEXT NOT NULL
    )
  ''';

  Future<void> _ensureTable(Database db) async {
    try {
      await db.execute(sqlCreateTable);
    } catch (e) {
      debugPrint('LocalWeatherRepository: _ensureTable error: $e');
    }
  }

  Future<WeatherSummary?> getCachedWeather({String? farmId}) async {
    final key = farmId ?? 'default';
    try {
      final db = await _database;
      await _ensureTable(db);

      final maps = await db.query(
        tableName,
        where: 'farm_id = ?',
        whereArgs: [key],
        limit: 1,
      );

      if (maps.isEmpty) return null;

      final row = maps.first;
      return WeatherSummary(
        temperature: (row['temperature'] as num?)?.toInt() ?? 0,
        description: row['description']?.toString() ?? '',
      );
    } catch (e) {
      debugPrint('LocalWeatherRepository: getCachedWeather error: $e');
      return null;
    }
  }

  Future<void> cacheWeather({String? farmId, required WeatherSummary weather}) async {
    final key = farmId ?? 'default';
    try {
      final db = await _database;
      await _ensureTable(db);

      await db.insert(
        tableName,
        {
          'farm_id': key,
          'temperature': weather.temperature,
          'description': weather.description,
          'updated_at_utc': DateTime.now().toUtc().toIso8601String(),
        },
        conflictAlgorithm: ConflictAlgorithm.replace,
      );
    } catch (e) {
      debugPrint('LocalWeatherRepository: cacheWeather error: $e');
    }
  }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `flutter test test/features/weather/data/local_weather_repository_test.dart`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add mobile/lib/features/weather/data/local_weather_repository.dart mobile/test/features/weather/data/local_weather_repository_test.dart
git commit -m "feat(weather): add LocalWeatherRepository for SQLite weather caching"
```

---

### Task 4: Mobile - BackendWeatherRepository Stale-While-Revalidate Integration

**Files:**
- Modify: `mobile/lib/features/weather/data/backend_weather_repository.dart`
- Modify: `mobile/test/features/weather/data/backend_weather_repository_test.dart`

**Interfaces:**
- Consumes: `LocalWeatherRepository`.
- Produces: `BackendWeatherRepository` returning cached `WeatherSummary` on network/server errors.

- [ ] **Step 1: Write test for offline fallback in backend_weather_repository_test.dart**

Add test in `mobile/test/features/weather/data/backend_weather_repository_test.dart`:
Verify that when `_client.getJson` throws 503 or network error, if `localRepo` has cached weather, it returns that cached weather instead of throwing `WeatherUnavailableException`.

- [ ] **Step 2: Run test to verify it fails**

Run: `flutter test test/features/weather/data/backend_weather_repository_test.dart`
Expected: FAIL.

- [ ] **Step 3: Update BackendWeatherRepository**

In `mobile/lib/features/weather/data/backend_weather_repository.dart`:
- Inject `LocalWeatherRepository localRepo = const LocalWeatherRepository()`.
- On successful fetch, call `await _localRepo.cacheWeather(farmId: targetFarmId, weather: summary)`.
- In `catch (e)` on 503, timeout or network error:
  - Query `final cached = await _localRepo.getCachedWeather(farmId: targetFarmId);`
  - If cached != null, return `WeatherSummary(temperature: cached.temperature, description: '${cached.description} (önbellek)')`.
  - If cached == null, rethrow the typed exception.

- [ ] **Step 4: Run test to verify it passes**

Run: `flutter test test/features/weather/`
Expected: ALL PASS.

- [ ] **Step 5: Commit**

```bash
git add mobile/lib/features/weather/data/backend_weather_repository.dart mobile/test/features/weather/data/backend_weather_repository_test.dart
git commit -m "feat(weather): add resilient offline fallback to BackendWeatherRepository"
```

---

### Task 5: End-to-End Verification and Deployment

**Files:**
- Run full backend tests: `dotnet test backend/TarlaAsistani.sln`
- Run full mobile weather & screen tests: `flutter test test/features/weather/ test/screens/ana_sayfa_ekrani_test.dart`

- [ ] **Step 1: Run backend tests**
Run: `dotnet test backend/TarlaAsistani.sln`
Expected: All backend unit and integration tests pass.

- [ ] **Step 2: Run mobile tests**
Run: `flutter test test/features/weather/ test/screens/ana_sayfa_ekrani_test.dart`
Expected: All mobile tests pass.

- [ ] **Step 3: Commit & Push to origin main**
```bash
git push origin main
```
- [ ] **Step 4: Verify production deployment**
Verify API readiness and weather endpoints.
