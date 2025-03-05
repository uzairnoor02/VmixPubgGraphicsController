using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace VmixGraphicsBusiness.vmixutils
{
    public class vmi_layerSetOnOff
    {
        String _vmixapibaseurl;
        public vmi_layerSetOnOff(IConfiguration configuration)
        {
            _vmixapibaseurl = configuration["vmixUrl"];

        }


        private static readonly HttpClient _httpClient = new HttpClient();
        private bool _isAnimationActive = false;
        public string GetSetTextApiCall(string input, string selectedName, string value)
        {
            return $"{_vmixapibaseurl}/?Function=SetText&Input={input}&SelectedName={selectedName}.Text&Value={Uri.EscapeDataString(value)}";
        }

        public string GetSetImageApiCall(string input, string selectedName, string value)
        {
            return $"{_vmixapibaseurl}/?Function=SetImage&Input={input}&SelectedName={selectedName}.Source&Value={Uri.EscapeDataString(value)}";
        }
        public async Task PushAnimationAsync(string input, int layer, bool isOn, int animationTimeMs)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                throw new ArgumentException("Input cannot be null or empty.", nameof(input));
            }

            if (layer < 1 || layer > 10)
            {
                throw new ArgumentOutOfRangeException(nameof(layer), "Layer must be between 1 and 10.");
            }

            if (animationTimeMs <= 0)
            {
                throw new ArgumentException("Animation time must be greater than zero.", nameof(animationTimeMs));
            }

            // Prevent overlapping animations
            if (_isAnimationActive)
            {
                Console.WriteLine("Animation already in progress. Please wait.");
                return;
            }

            _isAnimationActive = true;

            try
            {
                // Turn the layer on or off
                string function = isOn ? "wipe" : "wipe";
                string Overlay = isOn ? $"OverlayInput{layer}In" : $"OverlayInput{layer}Out";
                await SendCommandToVmixAsync($"function={Overlay}&input={input}");
                await Task.Delay(10);
                if (isOn)
                {

                    await Task.Delay(animationTimeMs);
                    // Turn the layer off after the animation
                    await SendCommandToVmixAsync($"function=OverlayInput{layer}Out&input={input}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                throw;
            }
            finally
            {
                _isAnimationActive = false;
            }
        }

        // Helper method to send a command to the vMix API
        private async Task SendCommandToVmixAsync(string command)
        {
            string requestUrl = $"{_vmixapibaseurl}?{command}";

            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync(requestUrl);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Command sent successfully: {command}");
                }
                else
                {
                    Console.WriteLine($"Failed to send command. Status code: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending command to vMix: {ex.Message}");
                throw;
            }

        }
    }
}
