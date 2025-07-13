
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VmixData.Models;

namespace VmixGraphicsBusiness
{
    public class AuthKeyService
    {
        private readonly vmix_graphicsContext _context;
        private readonly GoogleSheetsAuthService _googleSheetsService;
        private readonly ILogger<AuthKeyService> _logger;

        public AuthKeyService(vmix_graphicsContext context, GoogleSheetsAuthService googleSheetsService, ILogger<AuthKeyService> logger)
        {
            _context = context;
            _googleSheetsService = googleSheetsService;
            _logger = logger;
        }

        public async Task SyncKeysWithCloudAsync()
        {
            try
            {
                // Get keys from Google Sheets
                var cloudKeys = await _googleSheetsService.GetValidKeysAsync();
                
                // Get existing keys from database
                var dbKeys = await _context.AuthKeys.Select(k => k.KeyValue).ToListAsync();

                // Add new keys from cloud to database
                var newKeys = cloudKeys.Except(dbKeys).ToList();
                foreach (var key in newKeys)
                {
                    _context.AuthKeys.Add(new AuthKey 
                    { 
                        KeyValue = key, 
                        CreatedAt = DateTime.Now,
                        IsActive = true
                    });
                }

                // Mark keys as inactive if they're not in cloud anymore
                var keysToDeactivate = dbKeys.Except(cloudKeys).ToList();
                foreach (var key in keysToDeactivate)
                {
                    var authKey = await _context.AuthKeys.FirstOrDefaultAsync(k => k.KeyValue == key);
                    if (authKey != null)
                    {
                        authKey.IsActive = false;
                        authKey.UpdatedAt = DateTime.Now;
                    }
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation($"Synced {newKeys.Count} new keys and deactivated {keysToDeactivate.Count} keys");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing keys with cloud");
                throw;
            }
        }

        public async Task<bool> IsKeyValidAsync(string key)
        {
            try
            {
                return await _context.AuthKeys
                    .AnyAsync(k => k.KeyValue == key && k.IsActive);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking key validity");
                return false;
            }
        }

        public async Task SaveKeyToDbAsync(string key)
        {
            try
            {
                var existingKey = await _context.AuthKeys.FirstOrDefaultAsync(k => k.KeyValue == key);
                if (existingKey == null)
                {
                    _context.AuthKeys.Add(new AuthKey
                    {
                        KeyValue = key,
                        CreatedAt = DateTime.Now,
                        IsActive = true
                    });
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving key to database");
                throw;
            }
        }
    }

    // Add this model to your VmixData Models
    public class AuthKey
    {
        public int Id { get; set; }
        public string KeyValue { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsActive { get; set; }
    }
}
