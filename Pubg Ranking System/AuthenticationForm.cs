
using VmixGraphicsBusiness;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;

namespace Pubg_Ranking_System
{
    public partial class AuthenticationForm : Form
    {
        private readonly GoogleSheetsAuthService _googleSheetsService;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AuthenticationForm> _logger;
        private string _validatedKey = string.Empty;

        public string ValidatedKey => _validatedKey;
        public bool IsAuthenticated { get; private set; } = false;

        public AuthenticationForm(IServiceProvider serviceProvider)
        {
            InitializeComponent();
            _serviceProvider = serviceProvider;
            _googleSheetsService = serviceProvider.GetRequiredService<GoogleSheetsAuthService>();
            _logger = serviceProvider.GetRequiredService<ILogger<AuthenticationForm>>();
        }

        private async void btnValidate_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtKey.Text))
            {
                MessageBox.Show("Please enter a valid key.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnValidate.Enabled = false;
            btnValidate.Text = "Validating...";

            try
            {
                bool isValid = await _googleSheetsService.ValidateKeyAsync(txtKey.Text.Trim());
                
                if (isValid)
                {
                    _validatedKey = txtKey.Text.Trim();
                    IsAuthenticated = true;

                    // Log the access
                    string ipAddress = GetLocalIPAddress();
                    await _googleSheetsService.LogUserAccessAsync(_validatedKey, ipAddress);

                    MessageBox.Show("Authentication successful!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else
                {
                    string ipAddress = GetLocalIPAddress();
                    await _googleSheetsService.LogUserAccessAsync(txtKey.Text.Trim(), ipAddress);
                    MessageBox.Show("Invalid key. Please check your key and try again.", "Authentication Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    txtKey.Clear();
                    txtKey.Focus();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during authentication");
                MessageBox.Show("An error occurred during authentication. Please try again.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnValidate.Enabled = true;
                btnValidate.Text = "Validate";
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private string GetLocalIPAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        return ip.ToString();
                    }
                }
                return "127.0.0.1";
            }
            catch
            {
                return "Unknown";
            }
        }

        private void txtKey_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                btnValidate_Click(sender, e);
            }
        }
    }
}
