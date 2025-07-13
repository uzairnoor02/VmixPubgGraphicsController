
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;

namespace VmixGraphicsBusiness
{
    public class GoogleSheetsAuthService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<GoogleSheetsAuthService> _logger;
        private SheetsService _sheetsService;
        private readonly string _spreadsheetId;
        private readonly string _credentialsPath;

        public GoogleSheetsAuthService(IConfiguration configuration, ILogger<GoogleSheetsAuthService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _spreadsheetId = _configuration["GoogleSheets:SpreadsheetId"];
            _credentialsPath = _configuration["GoogleSheets:CredentialsPath"];
            InitializeService();
        }

        private void InitializeService()
        {
            try
            {
                var credential = GoogleCredential.FromFile(_credentialsPath)
                    .CreateScoped(new[] { SheetsService.Scope.Spreadsheets });

                _sheetsService = new SheetsService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "PUBG Ranking System Auth"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Google Sheets service");
                throw;
            }
        }

        public async Task<bool> ValidateKeyAsync(string inputKey)
        {
            try
            {
                var range = "AuthKeys!A:A"; // Sheet named "AuthKeys", column A
                var request = _sheetsService.Spreadsheets.Values.Get(_spreadsheetId, range);
                var response = await request.ExecuteAsync();

                if (response.Values != null)
                {
                    foreach (var row in response.Values)
                    {
                        if (row.Count > 0 && row[0]?.ToString() == inputKey)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating key against Google Sheets");
                return false;
            }
        }

        public async Task<List<string>> GetValidKeysAsync()
        {
            try
            {
                var range = "AuthKeys!A:A";
                var request = _sheetsService.Spreadsheets.Values.Get(_spreadsheetId, range);
                var response = await request.ExecuteAsync();

                var keys = new List<string>();
                if (response.Values != null)
                {
                    foreach (var row in response.Values)
                    {
                        if (row.Count > 0 && !string.IsNullOrEmpty(row[0]?.ToString()))
                        {
                            keys.Add(row[0].ToString());
                        }
                    }
                }
                return keys;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving keys from Google Sheets");
                return new List<string>();
            }
        }

        public async Task LogUserAccessAsync(string key, string ipAddress)
        {
            try
            {
                var range = "AccessLog!A:D"; // Sheet named "AccessLog"
                var values = new List<IList<object>>
                {
                    new List<object>
                    {
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        key,
                        ipAddress,
                        Environment.MachineName
                    }
                };

                var valueRange = new ValueRange
                {
                    Values = values
                };

                var appendRequest = _sheetsService.Spreadsheets.Values.Append(valueRange, _spreadsheetId, range);
                appendRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
                await appendRequest.ExecuteAsync();

                _logger.LogInformation($"Logged access for key: {key}, IP: {ipAddress}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging user access to Google Sheets");
            }
        }
    }
}
