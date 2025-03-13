using Hangfire;
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
    public static class vmi_layerSetOnOff
    {


        private static readonly HttpClient _httpClient = new HttpClient();
        public static string GetSetTextApiCall(string input, string selectedName, string value)
        {
            String _vmixapibaseurl = ConfigGlobal.VmixUrl;
            return $"{_vmixapibaseurl}/?Function=SetText&Input={input}&SelectedName={selectedName}.Text&Value={Uri.EscapeDataString(value)}";
        }

        public static string GetSetImageApiCall(string input, string selectedName, string value)
        {
            String _vmixapibaseurl = ConfigGlobal.VmixUrl;
            return $"{_vmixapibaseurl}/?Function=SetImage&Input={input}&SelectedName={selectedName}.Source&Value={Uri.EscapeDataString(value)}";
        }
        [DisableConcurrentExecution(timeoutInSeconds: 6)] // Prevent concurrent execution
        public static async Task PushAnimationAsync(string input, int layer, bool isOn, int animationTimeMs, List<string> apiCalls)
        {
            //input = "3";
            bool _isAnimationActive = false;
            String _vmixapibaseurl = ConfigGlobal.VmixUrl;
            if (apiCalls.Any())
            {
                SetTexts setTexts = new SetTexts();
                await setTexts.CallApiAsync(apiCalls);
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

                _isAnimationActive = true;

                try
                {
                    // Turn the layer on or off
                    string function = isOn ? "slide" : "slide";
                    string Overlay = isOn ? $"OverlayInput{layer}In" : $"OverlayInput{layer}Out";
                     await SendCommandToVmixAsync($"function={Overlay}&input={input}");
                    if (isOn)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(4000));
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
        }

        public static async Task PushAnimationAsync(string input, int layer, bool isOn, int animationTimeMs)
        {
            bool _isAnimationActive = false;
            String _vmixapibaseurl = ConfigGlobal.VmixUrl;

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

            _isAnimationActive = true;

            try
            {
                // Turn the layer on or off
                string function = isOn ? "slide" : "slide";
                string Overlay = isOn ? $"OverlayInput{layer}In" : $"OverlayInput{layer}Out";
                await SendCommandToVmixAsync($"function={Overlay}"); //&input={input}");
                if (isOn)
                {
                    await Task.Delay(6000);
                    // Turn the layer off after the animation
                    await SendCommandToVmixAsync($"function=OverlayInput{layer}Out"); //&input={input}");
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
        public static async Task SendCommandToVmixAsync(string command)
        {
            String _vmixapibaseurl = ConfigGlobal.VmixUrl;
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
