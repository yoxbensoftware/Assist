namespace Assist.Services;

using System.Net.NetworkInformation;
using System.Text.Json;

/// <summary>
/// Fetches and caches all dashboard data: weather, currency, crypto, IP,
/// system stats, battery, connectivity, uptime.
/// </summary>
internal static class DashboardService
{
    #region Cache fields

    private static string? _weatherCache;
    private static string? _currencyCache;
    private static string? _cryptoCache;
    private static string? _ipCache;
    private static string? _detectedCity;
    private const double FallbackUsdTryRate = 34.0;
    private static double _cachedUsdTryRate = FallbackUsdTryRate;
    private static DateTime _lastWeatherFetch;
    private static DateTime _lastCurrencyFetch;
    private static DateTime _lastCryptoFetch;
    private static DateTime _lastIpFetch;

    private static readonly TimeSpan WeatherCacheDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan CurrencyCacheDuration = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan CryptoCacheDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan IpCacheDuration = TimeSpan.FromMinutes(30);

    #endregion

    #region Online data

    /// <summary>
    /// Current weather for Istanbul via wttr.in.
    /// </summary>
    public static async Task<string> GetWeatherAsync()
    {
        if (_weatherCache is not null && DateTime.UtcNow - _lastWeatherFetch < WeatherCacheDuration)
            return _weatherCache;

        try
        {
            var city = _detectedCity ?? "Istanbul";
            var url = $"https://wttr.in/{Uri.EscapeDataString(city)}?format=%C+%t+%h+%w&lang=tr";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "Assist/1.0");

            var response = await AppConstants.SharedHttpClient.SendAsync(request).ConfigureAwait(false);
            var result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            _weatherCache = $"🌤 {city}: {result.Trim()}";
            _lastWeatherFetch = DateTime.UtcNow;
            return _weatherCache;
        }
        catch
        {
            return _weatherCache ?? "🌤 Hava durumu alınamadı";
        }
    }

    /// <summary>
    /// USD/EUR → TRY exchange rates via open.er-api.com.
    /// </summary>
    public static async Task<string> GetCurrencyAsync()
    {
        if (_currencyCache is not null && DateTime.UtcNow - _lastCurrencyFetch < CurrencyCacheDuration)
            return _currencyCache;

        try
        {
            var url = "https://open.er-api.com/v6/latest/USD";
            var json = await AppConstants.SharedHttpClient.GetStringAsync(url).ConfigureAwait(false);
            var doc = JsonDocument.Parse(json);
            var rates = doc.RootElement.GetProperty("rates");
            var tryRate = rates.GetProperty("TRY").GetDouble();
            _cachedUsdTryRate = tryRate;
            var eurUsd = rates.GetProperty("EUR").GetDouble();
            var eurTry = tryRate / eurUsd;
            var gbpUsd = rates.GetProperty("GBP").GetDouble();
            var gbpTry = tryRate / gbpUsd;

            _currencyCache = $"💱 USD: {tryRate:F2} ₺  EUR: {eurTry:F2} ₺  GBP: {gbpTry:F2} ₺";
            _lastCurrencyFetch = DateTime.UtcNow;
            return _currencyCache;
        }
        catch
        {
            return _currencyCache ?? "💱 Döviz bilgisi alınamadı";
        }
    }

    /// <summary>
    /// BTC/ETH prices — tries CoinGecko first, falls back to CoinCap.
    /// </summary>
    public static async Task<string> GetCryptoAsync()
    {
        if (_cryptoCache is not null && DateTime.UtcNow - _lastCryptoFetch < CryptoCacheDuration)
            return _cryptoCache;

        var result = await TryFetchCryptoFromCoinGeckoAsync().ConfigureAwait(false)
                  ?? await TryFetchCryptoFromCoinCapAsync().ConfigureAwait(false);

        if (result is not null)
        {
            _cryptoCache = result;
            _lastCryptoFetch = DateTime.UtcNow;
            return _cryptoCache;
        }

        return _cryptoCache ?? "₿ Kripto bilgisi alınamadı";
    }

    /// <summary>
    /// Fetches BTC/ETH prices from the CoinGecko API.
    /// </summary>
    private static async Task<string?> TryFetchCryptoFromCoinGeckoAsync()
    {
        try
        {
            var url = "https://api.coingecko.com/api/v3/simple/price?ids=bitcoin,ethereum&vs_currencies=usd,try&include_24hr_change=true";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "Assist/1.0");
            request.Headers.Add("Accept", "application/json");

            var response = await AppConstants.SharedHttpClient.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);

            var btc = doc.RootElement.GetProperty("bitcoin");
            var eth = doc.RootElement.GetProperty("ethereum");

            var btcUsd = btc.GetProperty("usd").GetDouble();
            var btcTry = btc.GetProperty("try").GetDouble();
            var btcChange = btc.TryGetProperty("usd_24h_change", out var btcCh) ? btcCh.GetDouble() : 0;

            var ethUsd = eth.GetProperty("usd").GetDouble();
            var ethTry = eth.GetProperty("try").GetDouble();
            var ethChange = eth.TryGetProperty("usd_24h_change", out var ethCh) ? ethCh.GetDouble() : 0;

            return FormatCryptoResult(btcUsd, btcTry, btcChange, ethUsd, ethTry, ethChange);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Fetches BTC/ETH prices from the CoinCap API as a fallback source.
    /// </summary>
    private static async Task<string?> TryFetchCryptoFromCoinCapAsync()
    {
        try
        {
            var url = "https://api.coincap.io/v2/assets?ids=bitcoin,ethereum";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "Assist/1.0");
            request.Headers.Add("Accept", "application/json");

            var response = await AppConstants.SharedHttpClient.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);

            double btcUsd = 0, ethUsd = 0, btcChange = 0, ethChange = 0;

            foreach (var asset in doc.RootElement.GetProperty("data").EnumerateArray())
            {
                var id = asset.GetProperty("id").GetString();
                var price = double.Parse(
                    asset.GetProperty("priceUsd").GetString()!,
                    System.Globalization.CultureInfo.InvariantCulture);
                var change = double.Parse(
                    asset.GetProperty("changePercent24Hr").GetString()!,
                    System.Globalization.CultureInfo.InvariantCulture);

                if (id == "bitcoin") { btcUsd = price; btcChange = change; }
                else if (id == "ethereum") { ethUsd = price; ethChange = change; }
            }

            var tryRate = _cachedUsdTryRate;
            return FormatCryptoResult(btcUsd, btcUsd * tryRate, btcChange, ethUsd, ethUsd * tryRate, ethChange);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Formats BTC and ETH price data into a displayable string with TRY equivalents and 24h change.
    /// </summary>
    private static string FormatCryptoResult(
        double btcUsd, double btcTry, double btcChange,
        double ethUsd, double ethTry, double ethChange)
    {
        var btcArrow = btcChange >= 0 ? "▲" : "▼";
        var ethArrow = ethChange >= 0 ? "▲" : "▼";
        return $"₿ BTC: ${btcUsd:N0} ({btcTry:N0} ₺) {btcArrow}{Math.Abs(btcChange):F1}%  Ξ ETH: ${ethUsd:N0} ({ethTry:N0} ₺) {ethArrow}{Math.Abs(ethChange):F1}%";
    }

    /// <summary>
    /// Public IP + geolocation via ip-api.com.
    /// </summary>
    public static async Task<string> GetIpInfoAsync()
    {
        if (_ipCache is not null && DateTime.UtcNow - _lastIpFetch < IpCacheDuration)
            return _ipCache;

        try
        {
            var url = "https://ip-api.com/json/?fields=query,city,country,isp";
            var json = await AppConstants.SharedHttpClient.GetStringAsync(url).ConfigureAwait(false);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var ip = root.GetProperty("query").GetString();
            var city = root.GetProperty("city").GetString();
            var country = root.GetProperty("country").GetString();
            var isp = root.GetProperty("isp").GetString();

            _detectedCity = city;

            _ipCache = $"🌐 {ip} — {city}, {country} ({isp})";
            _lastIpFetch = DateTime.UtcNow;
            return _ipCache;
        }
        catch
        {
            return _ipCache ?? "🌐 IP bilgisi alınamadı";
        }
    }

    /// <summary>
    /// Ping to 8.8.8.8 to check internet connectivity and latency.
    /// </summary>
    public static async Task<string> GetPingAsync()
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync("8.8.8.8", 3000).ConfigureAwait(false);

            return reply.Status == IPStatus.Success
                ? $"📶 Online — {reply.RoundtripTime} ms"
                : "📶 Offline";
        }
        catch
        {
            return "📶 Bağlantı kontrol edilemedi";
        }
    }

    #endregion

    #region Local system data (synchronous — called from UI thread)

    /// <summary>
    /// CPU usage percentage (snapshot via PerformanceCounter-like approach).
    /// </summary>
    public static string GetCpuRam()
    {
        try
        {
            var cpuPercent = GetCpuUsage();
            var ramInfo = GetRamInfo();
            return $"💻 CPU: {cpuPercent}%  RAM: {ramInfo}";
        }
        catch
        {
            return "💻 CPU/RAM bilgisi alınamadı";
        }
    }

    /// <summary>
    /// Primary drive usage.
    /// </summary>
    public static string GetDiskUsage()
    {
        try
        {
            var drive = new DriveInfo("C");
            if (!drive.IsReady) return "💾 C: sürücü hazır değil";

            var totalGb = drive.TotalSize / (1024.0 * 1024.0 * 1024.0);
            var freeGb = drive.TotalFreeSpace / (1024.0 * 1024.0 * 1024.0);
            var usedPercent = (int)((1.0 - (double)drive.TotalFreeSpace / drive.TotalSize) * 100);

            return $"💾 C: {freeGb:F1} GB boş / {totalGb:F0} GB ({usedPercent}%)";
        }
        catch
        {
            return "💾 Disk bilgisi alınamadı";
        }
    }

    /// <summary>
    /// Battery status (laptop) or AC power note.
    /// </summary>
    public static string GetBatteryStatus()
    {
        try
        {
            var power = System.Windows.Forms.SystemInformation.PowerStatus;

            if (power.BatteryChargeStatus == System.Windows.Forms.BatteryChargeStatus.NoSystemBattery)
                return "🔌 AC Güç (pil yok)";

            var percent = (int)(power.BatteryLifePercent * 100);
            var charging = power.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Online;
            var icon = percent switch
            {
                > 80 => "🔋",
                > 30 => "🔋",
                > 10 => "🪫",
                _ => "⚠"
            };

            var status = charging ? "Şarj oluyor" : "Pil";
            return $"{icon} {status}: {percent}%";
        }
        catch
        {
            return "🔋 Pil bilgisi alınamadı";
        }
    }

    /// <summary>
    /// System uptime.
    /// </summary>
    public static string GetUptime()
    {
        try
        {
            var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
            return $"⬆ Uptime: {uptime.Days}g {uptime.Hours}s {uptime.Minutes}dk";
        }
        catch
        {
            return "⬆ Uptime bilgisi alınamadı";
        }
    }

    /// <summary>
    /// Password count and clipboard history count.
    /// </summary>
    public static string GetAppStats()
    {
        var pwdCount = PasswordStore.Entries.Count;
        var clipCount = ClipboardHistoryService.Instance?.GetAll().Count ?? 0;
        return $"🔑 Şifre: {pwdCount}  📋 Pano: {clipCount}";
    }

    #endregion

    #region Helpers

    // Cached WMI CPU/RAM reading — avoids creating ManagementObjectSearcher on every fast timer tick
    private static int _lastCpuPercent;
    private static DateTime _lastCpuFetch;
    private static readonly TimeSpan CpuCacheDuration = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Reads current CPU usage percentage using Environment.ProcessorCount and kernel idle time (no WMI).
    /// </summary>
    private static int GetCpuUsage()
    {
        if (DateTime.UtcNow - _lastCpuFetch < CpuCacheDuration)
            return _lastCpuPercent;

        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(
                "select PercentProcessorTime from Win32_PerfFormattedData_PerfOS_Processor where Name='_Total'");
            foreach (var obj in searcher.Get())
            {
                var val = obj["PercentProcessorTime"]?.ToString();
                if (int.TryParse(val, out var cpu))
                {
                    _lastCpuPercent = cpu;
                    _lastCpuFetch = DateTime.UtcNow;
                    return cpu;
                }
            }
        }
        catch { }
        return _lastCpuPercent;
    }

    // Cached RAM values to avoid WMI every second
    private static string _lastRamInfo = "N/A";
    private static DateTime _lastRamFetch;
    private static readonly TimeSpan RamCacheDuration = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Reads used and total physical memory via WMI with 5-second cache to reduce allocations.
    /// </summary>
    private static string GetRamInfo()
    {
        if (DateTime.UtcNow - _lastRamFetch < RamCacheDuration)
            return _lastRamInfo;

        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(
                "select FreePhysicalMemory, TotalVisibleMemorySize from Win32_OperatingSystem");
            foreach (var obj in searcher.Get())
            {
                var freeKb = obj["FreePhysicalMemory"]?.ToString();
                var totalKb = obj["TotalVisibleMemorySize"]?.ToString();
                if (long.TryParse(freeKb, out var free) && long.TryParse(totalKb, out var total))
                {
                    var usedMb = (total - free) / 1024;
                    var totalMb = total / 1024;
                    var percent = (int)((1.0 - (double)free / total) * 100);
                    _lastRamInfo = $"{usedMb:N0}/{totalMb:N0} MB ({percent}%)";
                    _lastRamFetch = DateTime.UtcNow;
                    return _lastRamInfo;
                }
            }
        }
        catch { }
        return _lastRamInfo;
    }

    #endregion
}
