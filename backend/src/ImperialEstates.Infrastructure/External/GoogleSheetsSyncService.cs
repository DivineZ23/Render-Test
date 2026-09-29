using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using ImperialEstates.Application.Interfaces;
using ImperialEstates.Domain.Entities;
using ImperialEstates.Infrastructure.Persistence;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ImperialEstates.Infrastructure.External;

public sealed class GoogleSheetsSyncService(HttpClient httpClient, IOptions<GoogleSheetsOptions> options)
    : IGoogleSheetsSyncService
{
    private const string SheetsScope = "https://www.googleapis.com/auth/spreadsheets";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private readonly GoogleSheetsOptions _options = options.Value;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.SpreadsheetId) &&
        !string.IsNullOrWhiteSpace(_options.ClientEmail) &&
        !string.IsNullOrWhiteSpace(_options.PrivateKey);

    public string? SpreadsheetUrl => string.IsNullOrWhiteSpace(_options.SpreadsheetId)
        ? null
        : $"https://docs.google.com/spreadsheets/d/{_options.SpreadsheetId}/edit?gid={_options.SheetId}#gid={_options.SheetId}";

    public async Task PublishAsync(IReadOnlyList<RentSyncRecord> records, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Google Sheets sync is not configured.");

        var accessToken = await GetAccessTokenAsync(cancellationToken);
        var sheetTitle = await GetSheetTitleAsync(accessToken, cancellationToken);
        var escapedTitle = sheetTitle.Replace("'", "''", StringComparison.Ordinal);
        var writeRange = Uri.EscapeDataString($"'{escapedTitle}'!A:H");

        var values = new List<IReadOnlyList<object?>>
        {
            new object?[] { "Status", "Address", "Interior", "Renter CID", "Renter Name", "Phone", "Income", "Cost" }
        };
        values.AddRange(records.OrderBy(record => record.RowNumber).Select(ToRow));

        using var updateRequest = AuthorizedRequest(HttpMethod.Put,
            $"https://sheets.googleapis.com/v4/spreadsheets/{Uri.EscapeDataString(_options.SpreadsheetId)}/values/{writeRange}?valueInputOption=RAW",
            accessToken);
        updateRequest.Content = JsonContent.Create(new { majorDimension = "ROWS", values });
        await SendAsync(updateRequest, "write the latest rent data", cancellationToken);

        var trailingRange = Uri.EscapeDataString($"'{escapedTitle}'!A{values.Count + 1}:H");
        using var clearRequest = AuthorizedRequest(HttpMethod.Post,
            $"https://sheets.googleapis.com/v4/spreadsheets/{Uri.EscapeDataString(_options.SpreadsheetId)}/values/{trailingRange}:clear",
            accessToken);
        clearRequest.Content = JsonContent.Create(new { });
        await SendAsync(clearRequest, "remove obsolete trailing rows", cancellationToken);
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(_options.PrivateKey.Replace("\\n", "\n", StringComparison.Ordinal));
        var signingKey = new RsaSecurityKey(rsa)
        {
            // The RSA instance only lives for this token request. IdentityModel's shared
            // provider cache must not retain a signer backed by it after it is disposed.
            CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false }
        };
        var now = DateTime.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.ClientEmail,
            Audience = TokenEndpoint,
            Subject = new ClaimsIdentity([new Claim("scope", SheetsScope)]),
            NotBefore = now.AddSeconds(-30),
            IssuedAt = now,
            Expires = now.AddMinutes(55),
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256)
        };
        var assertion = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityTokenHandler().CreateToken(descriptor));
        using var response = await httpClient.PostAsync(TokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
            ["assertion"] = assertion
        }), cancellationToken);
        if (!response.IsSuccessStatusCode)
            await ThrowGoogleApiErrorAsync(response, "authenticate the Google service account", cancellationToken);
        var token = await response.Content.ReadFromJsonAsync<AccessTokenResponse>(cancellationToken: cancellationToken);
        return token?.AccessToken ?? throw new InvalidOperationException("Google did not return an access token.");
    }

    private async Task<string> GetSheetTitleAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = AuthorizedRequest(HttpMethod.Get,
            $"https://sheets.googleapis.com/v4/spreadsheets/{Uri.EscapeDataString(_options.SpreadsheetId)}?fields=sheets.properties(sheetId,title)",
            accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            await ThrowGoogleApiErrorAsync(response, "read the configured spreadsheet", cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        foreach (var sheet in document.RootElement.GetProperty("sheets").EnumerateArray())
        {
            var properties = sheet.GetProperty("properties");
            if (properties.GetProperty("sheetId").GetInt32() == _options.SheetId)
                return properties.GetProperty("title").GetString()
                    ?? throw new InvalidOperationException("The configured Google Sheet tab has no title.");
        }
        throw new InvalidOperationException($"Google Sheet tab {_options.SheetId} was not found.");
    }

    private async Task SendAsync(HttpRequestMessage request, string operation, CancellationToken cancellationToken)
    {
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            await ThrowGoogleApiErrorAsync(response, operation, cancellationToken);
    }

    private static HttpRequestMessage AuthorizedRequest(HttpMethod method, string uri, string accessToken)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private static IReadOnlyList<object?> ToRow(RentSyncRecord record) =>
        new object?[]
        {
            FormatStatus(record),
            record.Address,
            record.Interior,
            record.Cid?.ToString(CultureInfo.InvariantCulture) ?? "N/A",
            record.RenterName ?? "N/A",
            record.Phone ?? "N/A",
            FormatCurrency(record.Income),
            FormatCurrency(record.Cost)
        };

    private static string FormatStatus(RentSyncRecord record) => record.Status switch
    {
        "paid" when record.PaidThrough.HasValue => $"Paid {record.PaidThrough.Value.ToString("M/d/yyyy", CultureInfo.InvariantCulture)}",
        "paid" => "Paid",
        "overdue" => "Overdue",
        "evictable" => "Evictable",
        "empty" => "Empty",
        _ => record.Status
    };

    private static string FormatCurrency(decimal amount) => $"${amount.ToString("N0", CultureInfo.GetCultureInfo("en-US"))}";

    private static async Task ThrowGoogleApiErrorAsync(HttpResponseMessage response, string operation, CancellationToken cancellationToken)
    {
        var details = await response.Content.ReadAsStringAsync(cancellationToken);
        if (details.Length > 600) details = details[..600];
        throw new InvalidOperationException($"Google Sheets could not {operation} ({(int)response.StatusCode}): {details}");
    }

    private sealed record AccessTokenResponse([property: JsonPropertyName("access_token")] string AccessToken);
}
