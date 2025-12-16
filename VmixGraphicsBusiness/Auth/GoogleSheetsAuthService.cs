using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace VmixGraphicsBusiness.Auth
{
    public class GoogleSheetsAuthService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<GoogleSheetsAuthService> _logger;
        private SheetsService _sheetsService;
        private readonly string _spreadsheetId;

        public GoogleSheetsAuthService(IConfiguration configuration, ILogger<GoogleSheetsAuthService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _spreadsheetId = _configuration["GoogleSheets:SpreadsheetId"];
            InitializeService();
        }

        private void InitializeService()
        {
            try
            {
                GoogleCredential credential = GetCredential();

                _sheetsService = new SheetsService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "PUBG Ranking System Auth"
                });

                _logger.LogInformation("Google Sheets service initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Google Sheets service");
                throw;
            }
        }

        private GoogleCredential GetCredential()
        {
            // Try encrypted credentials from appsettings
            string encryptedCreds = _configuration["GoogleSheets:EncryptedCredentials"];

            if (!string.IsNullOrEmpty(encryptedCreds))
            {
                _logger.LogInformation("Loading encrypted credentials from appsettings");
                string decryptedJson = Pubg_Ranking_System.GoogleCredentialsEncryption.Decrypt(encryptedCreds);

                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(decryptedJson)))
                {
                    return GoogleCredential.FromStream(stream)
                        .CreateScoped(new[] { SheetsService.Scope.Spreadsheets });
                }
            }

            // Fallback: Load from file (development only)
            string jsonPath = Path.Combine(AppContext.BaseDirectory, "pubg-vmix-app.json");

            if (File.Exists(jsonPath))
            {
                _logger.LogWarning("Using unencrypted file (development mode)");

                using (var stream = new FileStream(jsonPath, FileMode.Open, FileAccess.Read))
                {
                    return GoogleCredential.FromStream(stream)
                        .CreateScoped(new[] { SheetsService.Scope.Spreadsheets });
                }
            }

            throw new FileNotFoundException("Google credentials not found!");
        }

        public async Task<bool> ValidateKeyAsync(string inputKey)
        {
            try
            {
                var range = "Sheet1!A:A";
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
                _logger.LogError(ex, "Error validating key");
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
                _logger.LogError(ex, "Error retrieving keys");
                return new List<string>();
            }
        }

        public async Task LogUserAccessAsync(string key, string ipAddress)
        {
            try
            {
                var range = "AccessLog!A:D";
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

                var valueRange = new ValueRange { Values = values };

                var appendRequest = _sheetsService.Spreadsheets.Values.Append(valueRange, _spreadsheetId, range);
                appendRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
                await appendRequest.ExecuteAsync();

                _logger.LogInformation($"Logged access for key: {key}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging access");
            }
        }
    }
}