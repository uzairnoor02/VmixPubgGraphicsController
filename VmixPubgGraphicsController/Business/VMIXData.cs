using System.Xml.Serialization;
using VmixPubgGraphicsController.Models;

namespace VmixPubgGraphicsController.Business
{
    public class VMIXDataoperations
    {
        public  async Task<string> GetXmlDataAsync(string apiUrl)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    HttpResponseMessage response = await client.GetAsync(apiUrl);
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStringAsync();
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }
        public async Task<VmixData> GetVMIXData()
        {
            try
            {
                // URL of the vMix API
                string apiUrl = "http://127.0.0.1:8088/API/";

                // Make a GET request to the vMix API
                string xmlData = await GetXmlDataAsync(apiUrl);

                // Deserialize the XML data into an instance of VmixData
                VmixData vmixData;
                using (StringReader reader = new StringReader(xmlData))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(VmixData));
                    vmixData = (VmixData)serializer.Deserialize(reader)!;
                }
            return vmixData!;
            }
            catch (Exception ex) { throw; }
        }
    }
}
