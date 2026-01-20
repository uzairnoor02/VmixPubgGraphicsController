using Hangfire;
using Microsoft.EntityFrameworkCore.Storage;
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

        // Simple flag to control execution
        private static volatile bool _shouldStop = false;

        // Method to stop all operations
        public static void StopAllOperations()
        {
            _shouldStop = true;
        }

        // Method to resume operations (call when starting new match)
        public static void ResumeOperations()
        {
            _shouldStop = false;
        }

        // Helper to check if should continue
        private static void ThrowIfShouldStop()
        {
            if (_shouldStop)
            {
                throw new OperationCanceledException("Operation stopped by user");
            }
        }

        public static string GetSetTextApiCall(string input, string selectedName, string value)
        {
            String _vmixapibaseurl = ConfigGlobal.VmixUrl;
            return $"{_vmixapibaseurl}/?Function=SetText&Input={input}&SelectedName={selectedName}.Text&Value={Uri.EscapeDataString(value)}";
        }

        public static string GetSetCountdownApiCall(string input, string selectedName, string value)
        {
            String _vmixapibaseurl = ConfigGlobal.VmixUrl;
            return $"{_vmixapibaseurl}/?Function=SetCountdown&Input={input}&SelectedName={selectedName}.Text&Value={Uri.EscapeDataString(value)}";
        }

        public static string GetSetImageApiCall(string input, string selectedName, string value)
        {
            String _vmixapibaseurl = ConfigGlobal.VmixUrl;
            return $"{_vmixapibaseurl}/?Function=SetImage&Input={input}&SelectedName={selectedName}.Source&Value={Uri.EscapeDataString(value)}";
        }

        [DisableConcurrentExecution(timeoutInSeconds: 6)]
        public static async Task PushAnimationAsync(string input, int layer, bool isOn, int animationTimeMs, List<string> apiCalls)
        {
            ThrowIfShouldStop();

            bool _isAnimationActive = false;
            String _vmixapibaseurl = ConfigGlobal.VmixUrl;

            if (apiCalls.Any())
            {
                SetTexts setTexts = new SetTexts();
                await setTexts.CallMultipleApiAsync(apiCalls);

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
                    ThrowIfShouldStop();

                    string function = isOn ? "slide" : "slide";
                    string Overlay = isOn ? $"OverlayInput{layer}In" : $"OverlayInput{layer}Out";
                    await SendCommandToVmixAsync($"function={Overlay}&input={input}");

                    if (isOn)
                    {
                        // Break delay into smaller chunks to check flag more frequently
                        int elapsed = 0;
                        int checkInterval = 100; // Check every 100ms
                        while (elapsed < 4000)
                        {
                            ThrowIfShouldStop();
                            await Task.Delay(checkInterval);
                            elapsed += checkInterval;
                        }

                        ThrowIfShouldStop();
                        await SendCommandToVmixAsync($"function=OverlayInput{layer}Out&input={input}");
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("Animation cancelled by user");
                    // Clean up - turn off the layer
                    try
                    {
                        await SendCommandToVmixAsync($"function=OverlayInput{layer}Out&input={input}");
                    }
                    catch { }
                    // Don't rethrow - just exit gracefully
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

        [DisableConcurrentExecution(timeoutInSeconds: 30)]
        public static async Task PushCircleAnimationAsync(string input, int layer, bool isOn, int counter)
        {
            ThrowIfShouldStop();

            var Tcounter = counter;
            SetTexts setTexts = new SetTexts();
            var apiCall = GetSetTextApiCall(input, "count", Tcounter.ToString());
            bool _isAnimationActive = false;
            String _vmixapibaseurl = ConfigGlobal.VmixUrl;

            _isAnimationActive = true;
            try
            {
                string function = isOn ? "slide" : "slide";
                string Overlay = $"OverlayInput{layer}In";
                await SendCommandToVmixAsync($"function={Overlay}&input={input}");

                for (var i = Tcounter; i >= 0; i--)
                {
                    ThrowIfShouldStop();

                    await setTexts.CallApiAsync(apiCall);
                    apiCall = GetSetTextApiCall(input, "count", i.ToString());

                    // Break delay into smaller chunks
                    int elapsed = 0;
                    while (elapsed < 1000)
                    {
                        ThrowIfShouldStop();
                        await Task.Delay(100);
                        elapsed += 100;
                    }
                }

                await SendCommandToVmixAsync($"function=OverlayInput{layer}Out&input={input}");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Circle animation cancelled by user");
                try
                {
                    await SendCommandToVmixAsync($"function=OverlayInput{layer}Out&input={input}");
                }
                catch { }
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

        public static async Task SendCommandToVmixAsync(string command)
        {
            ThrowIfShouldStop();

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
            catch (OperationCanceledException)
            {
                Console.WriteLine("Command to vMix cancelled");
                // Don't rethrow
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending command to vMix: {ex.Message}");
                throw;
            }
        }

        public static async Task PushAnimationAsync(string input, int layer, bool isOn, int animationTimeMs)
        {
            ThrowIfShouldStop();

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
                ThrowIfShouldStop();

                string function = isOn ? "slide" : "slide";
                string Overlay = isOn ? $"OverlayInput{layer}In" : $"OverlayInput{layer}Out";
                await SendCommandToVmixAsync($"function={Overlay}");

                if (isOn)
                {
                    // Break delay into smaller chunks
                    int elapsed = 0;
                    while (elapsed < 6000)
                    {
                        ThrowIfShouldStop();
                        await Task.Delay(100);
                        elapsed += 100;
                    }

                    ThrowIfShouldStop();
                    await SendCommandToVmixAsync($"function=OverlayInput{layer}Out");
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Animation cancelled by user");
                try
                {
                    await SendCommandToVmixAsync($"function=OverlayInput{layer}Out");
                }
                catch { }
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
}


//using Hangfire;
//using Microsoft.EntityFrameworkCore.Storage;
//using Microsoft.Extensions.Configuration;
//using System;
//using System.Collections.Generic;
//using System.Drawing;
//using System.Linq;
//using System.Net.Http;
//using System.Text;
//using System.Threading.Tasks;

//namespace VmixGraphicsBusiness.vmixutils
//{
//    public static class vmi_layerSetOnOff
//    {



//        private static readonly HttpClient _httpClient = new HttpClient();
//        public static string GetSetTextApiCall(string input, string selectedName, string value)
//        {
//            String _vmixapibaseurl = ConfigGlobal.VmixUrl;
//            return $"{_vmixapibaseurl}/?Function=SetText&Input={input}&SelectedName={selectedName}.Text&Value={Uri.EscapeDataString(value)}";
//        }
//        public static string GetSetCountdownApiCall(string input, string selectedName, string value)
//        {
//            String _vmixapibaseurl = ConfigGlobal.VmixUrl;
//            return $"{_vmixapibaseurl}/?Function=SetCountdown&Input={input}&SelectedName={selectedName}.Text&Value={Uri.EscapeDataString(value)}";
//        }

//        public static string GetSetImageApiCall(string input, string selectedName, string value)
//        {
//            String _vmixapibaseurl = ConfigGlobal.VmixUrl;
//            return $"{_vmixapibaseurl}/?Function=SetImage&Input={input}&SelectedName={selectedName}.Source&Value={Uri.EscapeDataString(value)}";
//        }
//        [DisableConcurrentExecution(timeoutInSeconds: 6)] // Prevent concurrent execution
//        public static async Task PushAnimationAsync(string input, int layer, bool isOn, int animationTimeMs, List<string> apiCalls)
//        {
//            //input = "3";
//            bool _isAnimationActive = false;
//            String _vmixapibaseurl = ConfigGlobal.VmixUrl;
//            if (apiCalls.Any())
//            {
//                SetTexts setTexts = new SetTexts();
//                await setTexts.CallMultipleApiAsync(apiCalls);
//                if (string.IsNullOrWhiteSpace(input))
//                {
//                    throw new ArgumentException("Input cannot be null or empty.", nameof(input));
//                }

//                if (layer < 1 || layer > 10)
//                {
//                    throw new ArgumentOutOfRangeException(nameof(layer), "Layer must be between 1 and 10.");
//                }

//                if (animationTimeMs <= 0)
//                {
//                    throw new ArgumentException("Animation time must be greater than zero.", nameof(animationTimeMs));
//                }

//                _isAnimationActive = true;

//                try
//                {
//                    // Turn the layer on or off
//                    string function = isOn ? "slide" : "slide";
//                    string Overlay = isOn ? $"OverlayInput{layer}In" : $"OverlayInput{layer}Out";
//                    await SendCommandToVmixAsync($"function={Overlay}&input={input}");
//                    if (isOn)
//                    {
//                        await Task.Delay(TimeSpan.FromMilliseconds(4000));
//                        // Turn the layer off after the animation
//                        await SendCommandToVmixAsync($"function=OverlayInput{layer}Out&input={input}");
//                    }
//                }
//                catch (Exception ex)
//                {
//                    Console.WriteLine($"Error: {ex.Message}");
//                    throw;
//                }
//                finally
//                {
//                    _isAnimationActive = false;
//                }
//            }
//        }

//        [DisableConcurrentExecution(timeoutInSeconds: 30)] // Prevent concurrent execution
//        public static async Task PushCircleAnimationAsync(string input, int layer, bool isOn, int counter)
//        {
//            var Tcounter = counter;
//            SetTexts setTexts = new SetTexts();
//            var apiCall = GetSetTextApiCall(input, "count", Tcounter.ToString());
//            bool _isAnimationActive = false;
//            String _vmixapibaseurl = ConfigGlobal.VmixUrl;

//            _isAnimationActive = true;
//            try
//            {
//                // Turn the layer on or off
//                string function = isOn ? "slide" : "slide";
//                string Overlay = $"OverlayInput{layer}In";
//                await SendCommandToVmixAsync($"function={Overlay}&input={input}");
//                for (var i = Tcounter; i >= 0; i--)
//                {
//                    await setTexts.CallApiAsync(apiCall);
//                    apiCall = GetSetTextApiCall(input, "count", i.ToString());
//                    await Task.Delay(1000);
//                }
//                await SendCommandToVmixAsync($"function=OverlayInput{layer}Out&input={input}");
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"Error: {ex.Message}");
//                throw;
//            }
//            finally
//            {
//                _isAnimationActive = false;
//            }
//        }
//        // Helper method to send a command to the vMix API
//        public static async Task SendCommandToVmixAsync(string command)
//        {
//            String _vmixapibaseurl = ConfigGlobal.VmixUrl;
//            string requestUrl = $"{_vmixapibaseurl}?{command}";

//            try
//            {
//                HttpResponseMessage response = await _httpClient.GetAsync(requestUrl);

//                if (response.IsSuccessStatusCode)
//                {
//                    Console.WriteLine($"Command sent successfully: {command}");
//                }
//                else
//                {
//                    Console.WriteLine($"Failed to send command. Status code: {response.StatusCode}");
//                }
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"Error sending command to vMix: {ex.Message}");
//                throw;
//            }

//        }
//        public static async Task PushAnimationAsync(string input, int layer, bool isOn, int animationTimeMs)
//        {
//            bool _isAnimationActive = false;
//            String _vmixapibaseurl = ConfigGlobal.VmixUrl;

//            if (string.IsNullOrWhiteSpace(input))
//            {
//                throw new ArgumentException("Input cannot be null or empty.", nameof(input));
//            }

//            if (layer < 1 || layer > 10)
//            {
//                throw new ArgumentOutOfRangeException(nameof(layer), "Layer must be between 1 and 10.");
//            }

//            if (animationTimeMs <= 0)
//            {
//                throw new ArgumentException("Animation time must be greater than zero.", nameof(animationTimeMs));
//            }

//            _isAnimationActive = true;

//            try
//            {
//                // Turn the layer on or off
//                string function = isOn ? "slide" : "slide";
//                string Overlay = isOn ? $"OverlayInput{layer}In" : $"OverlayInput{layer}Out";
//                await SendCommandToVmixAsync($"function={Overlay}"); //&input={input}");
//                if (isOn)
//                {
//                    await Task.Delay(6000);
//                    // Turn the layer off after the animation
//                    await SendCommandToVmixAsync($"function=OverlayInput{layer}Out"); //&input={input}");
//                }
//            }
//            catch (Exception ex)
//            {
//                Console.WriteLine($"Error: {ex.Message}");
//                throw;
//            }
//            finally
//            {
//                _isAnimationActive = false;
//            }
//        }

//    }
//}
