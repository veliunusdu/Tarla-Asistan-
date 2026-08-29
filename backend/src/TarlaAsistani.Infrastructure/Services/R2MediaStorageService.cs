using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TarlaAsistani.Application.Common.Interfaces;

namespace TarlaAsistani.Infrastructure.Services;

public class R2MediaStorageService : IMediaStorageService
{
    private readonly HttpClient _httpClient;
    private readonly string _accountId;
    private readonly string _bucket;
    private readonly string _accessKeyId;
    private readonly string _secretAccessKey;
    private readonly ILogger<R2MediaStorageService> _logger;

    public R2MediaStorageService(HttpClient httpClient, IConfiguration configuration, ILogger<R2MediaStorageService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _accountId = configuration["R2:AccountId"]
            ?? configuration["R2_ACCOUNT_ID"]
            ?? Environment.GetEnvironmentVariable("R2_ACCOUNT_ID")
            ?? throw new InvalidOperationException("R2_ACCOUNT_ID is required for R2MediaStorageService.");

        _bucket = configuration["R2:Bucket"]
            ?? configuration["R2_BUCKET"]
            ?? Environment.GetEnvironmentVariable("R2_BUCKET")
            ?? throw new InvalidOperationException("R2_BUCKET is required for R2MediaStorageService.");

        _accessKeyId = configuration["R2:AccessKeyId"]
            ?? configuration["R2_ACCESS_KEY_ID"]
            ?? Environment.GetEnvironmentVariable("R2_ACCESS_KEY_ID")
            ?? throw new InvalidOperationException("R2_ACCESS_KEY_ID is required for R2MediaStorageService.");

        _secretAccessKey = configuration["R2:SecretAccessKey"]
            ?? configuration["R2_SECRET_ACCESS_KEY"]
            ?? Environment.GetEnvironmentVariable("R2_SECRET_ACCESS_KEY")
            ?? throw new InvalidOperationException("R2_SECRET_ACCESS_KEY is required for R2MediaStorageService.");
    }

    public async Task SaveAsync(string storageKey, byte[] data, string contentType, CancellationToken cancellationToken = default)
    {
        var url = GetObjectUrl(storageKey);
        using var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = new ByteArrayContent(data)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        SignRequest(request, "PUT", storageKey, data);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        _logger.LogInformation("Saved media object {Key} to R2 bucket {Bucket}", storageKey, _bucket);
    }

    public async Task<byte[]> LoadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var url = GetObjectUrl(storageKey);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        SignRequest(request, "GET", storageKey, Array.Empty<byte>());

        var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new FileNotFoundException($"Medya nesnesi '{storageKey}' R2 deposunda bulunamadı.");
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    public async Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var url = GetObjectUrl(storageKey);
        using var request = new HttpRequestMessage(HttpMethod.Delete, url);
        SignRequest(request, "DELETE", storageKey, Array.Empty<byte>());

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        _logger.LogInformation("Deleted media object {Key} from R2 bucket {Bucket}", storageKey, _bucket);
    }

    private string GetObjectUrl(string storageKey) =>
        $"https://{_accountId}.r2.cloudflarestorage.com/{_bucket}/{storageKey.TrimStart('/')}";

    private void SignRequest(HttpRequestMessage request, string method, string storageKey, byte[] payload)
    {
        var now = DateTimeOffset.UtcNow;
        var dateStamp = now.ToString("yyyyMMdd");
        var amzDate = now.ToString("yyyyMMddTHHmmssZ");
        var host = $"{_accountId}.r2.cloudflarestorage.com";
        var uriPath = $"/{_bucket}/{storageKey.TrimStart('/')}";

        request.Headers.Host = host;
        request.Headers.Add("x-amz-date", amzDate);
        request.Headers.Add("x-amz-content-sha256", ToHex(SHA256.HashData(payload)));

        // S3 SigV4
        var canonicalHeaders = $"host:{host}\nx-amz-content-sha256:{request.Headers.GetValues("x-amz-content-sha256").First()}\nx-amz-date:{amzDate}\n";
        var signedHeaders = "host;x-amz-content-sha256;x-amz-date";
        var payloadHash = request.Headers.GetValues("x-amz-content-sha256").First();

        var canonicalRequest = $"{method}\n{uriPath}\n\n{canonicalHeaders}\n{signedHeaders}\n{payloadHash}";
        var stringToSign = $"AWS4-HMAC-SHA256\n{amzDate}\n{dateStamp}/auto/s3/aws4_request\n{ToHex(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest)))}";

        var signingKey = GetSignatureKey(_secretAccessKey, dateStamp, "auto", "s3");
        var signature = ToHex(HMACSHA256.HashData(signingKey, Encoding.UTF8.GetBytes(stringToSign)));

        request.Headers.Authorization = new AuthenticationHeaderValue("AWS4-HMAC-SHA256",
            $"Credential={_accessKeyId}/{dateStamp}/auto/s3/aws4_request,SignedHeaders={signedHeaders},Signature={signature}");
    }

    private static byte[] GetSignatureKey(string key, string dateStamp, string regionName, string serviceName)
    {
        var kDate = HMACSHA256.HashData(Encoding.UTF8.GetBytes("AWS4" + key), Encoding.UTF8.GetBytes(dateStamp));
        var kRegion = HMACSHA256.HashData(kDate, Encoding.UTF8.GetBytes(regionName));
        var kService = HMACSHA256.HashData(kRegion, Encoding.UTF8.GetBytes(serviceName));
        return HMACSHA256.HashData(kService, Encoding.UTF8.GetBytes("aws4_request"));
    }

    private static string ToHex(byte[] bytes) =>
        Convert.ToHexString(bytes).ToLowerInvariant();
}
