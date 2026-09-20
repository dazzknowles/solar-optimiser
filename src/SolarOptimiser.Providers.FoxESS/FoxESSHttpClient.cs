using Microsoft.Extensions.Options;
using SolarOptimiser.Providers.FoxESS.Contracts;
using System.Net.Http.Json;
using System.Text.Json;

namespace SolarOptimiser.Providers.FoxESS
{
    /// <summary>
    /// The only HTTP surface exposed by this adapter (SOL-T-202: "No generic/pass-through HTTP client is exposed
    /// to Collection") — one method per allow-listed endpoint actually needed by
    /// <c>ITelemetryProvider</c>'s two methods.
    /// </summary>
    public sealed class FoxESSHttpClient
    {
        private readonly HttpClient _httpClient;
        private readonly FoxESSProviderOptions _options;
        private readonly FoxESSRequestSigner _signer;

        public FoxESSHttpClient(HttpClient httpClient, IOptions<FoxESSProviderOptions> options, FoxESSRequestSigner signer)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _signer = signer;

            if (_httpClient.BaseAddress == null)
            {
                _httpClient.BaseAddress = new Uri(_options.BaseUrl);
            }
        }

        public async Task<FoxESSApiCallResult<FoxESSDeviceListResponse>> GetDeviceListAsync(CancellationToken cancellationToken)
        {
            return await SendAsync<object?, FoxESSDeviceListResponse>("/op/v0/device/list", null, cancellationToken);
        }

        public async Task<FoxESSApiCallResult<FoxESSDeviceRealQueryResponse>> GetDeviceRealQueryAsync(string deviceSN, CancellationToken cancellationToken)
        {
            FoxESSDeviceRealQueryRequest requestBody = new FoxESSDeviceRealQueryRequest
            {
                Sns = new List<string> { deviceSN },
                Variables = null // omitted entirely: requests every variable the device currently exposes (SOL-T-203)
            };

            return await SendAsync<FoxESSDeviceRealQueryRequest, FoxESSDeviceRealQueryResponse>("/op/v1/device/real/query", requestBody, cancellationToken);
        }

        private async Task<FoxESSApiCallResult<TResponse>> SendAsync<TRequest, TResponse>(string path, TRequest? body, CancellationToken cancellationToken)
            where TResponse : class
        {
            string timestamp = FoxESSRequestSigner.CurrentTimestampMilliseconds();
            string signature = _signer.Sign(path, _options.ApiKey, timestamp);

            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, path))
            {
                request.Headers.Add("token", _options.ApiKey);
                request.Headers.Add("timestamp", timestamp);
                request.Headers.Add("signature", signature);
                request.Headers.Add("lang", "en");
                request.Headers.UserAgent.ParseAdd("SolarOptimiser/1.0");

                string requestBodyJson = body == null ? string.Empty : JsonSerializer.Serialize(body);
                if (body != null)
                {
                    request.Content = JsonContent.Create(body);
                }

                string requestSanitized = Redact($"POST {path}\n{requestBodyJson}", _options.ApiKey, signature);

                HttpResponseMessage response;
                try
                {
                    response = await _httpClient.SendAsync(request, cancellationToken);
                }
                catch (HttpRequestException)
                {
                    return new FoxESSApiCallResult<TResponse>
                    {
                        TransportFailed = true,
                        RequestSanitized = requestSanitized,
                        RawResponseSanitized = string.Empty
                    };
                }
                catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    return new FoxESSApiCallResult<TResponse>
                    {
                        TransportFailed = true,
                        RequestSanitized = requestSanitized,
                        RawResponseSanitized = string.Empty
                    };
                }

                using (response)
                {
                    string rawResponseText = await response.Content.ReadAsStringAsync(cancellationToken);
                    string rawResponseSanitized = Redact(rawResponseText, _options.ApiKey, signature);

                    TResponse? parsed;
                    bool parseFailed = false;
                    try
                    {
                        parsed = string.IsNullOrWhiteSpace(rawResponseText)
                            ? null
                            : JsonSerializer.Deserialize<TResponse>(rawResponseText);
                    }
                    catch (JsonException)
                    {
                        parsed = null;
                        parseFailed = true;
                    }

                    return new FoxESSApiCallResult<TResponse>
                    {
                        HttpStatusCode = response.StatusCode,
                        Parsed = parsed,
                        ParseFailed = parseFailed,
                        RequestSanitized = requestSanitized,
                        RawResponseSanitized = rawResponseSanitized
                    };
                }
            }
        }

        private static string Redact(string text, string apiKey, string signature)
        {
            string redacted = text;

            if (!string.IsNullOrEmpty(apiKey))
            {
                redacted = redacted.Replace(apiKey, "[REDACTED-API-KEY]");
            }

            if (!string.IsNullOrEmpty(signature))
            {
                redacted = redacted.Replace(signature, "[REDACTED-SIGNATURE]");
            }

            return redacted;
        }
    }
}
