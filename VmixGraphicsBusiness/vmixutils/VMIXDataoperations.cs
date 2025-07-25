﻿using System.Xml.Serialization;

namespace VmixGraphicsBusiness.vmixutils
{
    public class VMIXDataoperations
    {
        public async Task<string> GetXmlDataAsync(string apiUrl)
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
        public async Task<VmixData.Models.MatchModels.VmixData> GetVMIXData()
        {
            try
            {
                // URL of the vMix API
                string apiUrl = "http://127.0.0.1:8088/API/";

                // Make a GET request to the vMix API
                string xmlData = await GetXmlDataAsync(apiUrl);

                // Deserialize the XML data into an instance of VmixData
                VmixData.Models.MatchModels.VmixData vmixData;
                using (StringReader reader = new StringReader(xmlData))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(VmixData.Models.MatchModels.VmixData));
                    vmixData = (VmixData.Models.MatchModels.VmixData)serializer.Deserialize(reader)!;
                }
                return vmixData!;
            }
            catch (Exception ex) { throw; }
        }
    }
}
