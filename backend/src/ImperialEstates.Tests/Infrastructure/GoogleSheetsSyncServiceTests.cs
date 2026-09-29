using System.Net;
using System.Security.Cryptography;
using System.Text;
using ImperialEstates.Infrastructure.External;
using ImperialEstates.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

namespace ImperialEstates.Tests.Infrastructure;

public sealed class GoogleSheetsSyncServiceTests
{
    [Fact]
    public async Task Repeated_syncs_do_not_reuse_a_signer_backed_by_a_disposed_rsa_key()
    {
        using var rsa = RSA.Create(2048);
        using var httpClient = new HttpClient(new GoogleSheetsHandler());
        var service = new GoogleSheetsSyncService(
            httpClient,
            Options.Create(new GoogleSheetsOptions
            {
                SpreadsheetId = "spreadsheet-id",
                SheetId = 0,
                ClientEmail = "service-account@example.test",
                PrivateKey = rsa.ExportPkcs8PrivateKeyPem()
            }));

        await service.PublishAsync([], default);
        await service.PublishAsync([], default);
    }

    private sealed class GoogleSheetsHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.RequestUri?.Host switch
            {
                "oauth2.googleapis.com" => "{\"access_token\":\"test-token\"}",
                _ when request.Method == HttpMethod.Get =>
                    "{\"sheets\":[{\"properties\":{\"sheetId\":0,\"title\":\"Rent\"}}]}",
                _ => "{}"
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
