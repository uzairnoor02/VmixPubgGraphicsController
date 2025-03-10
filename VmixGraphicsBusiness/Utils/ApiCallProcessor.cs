using Hangfire;
using VmixGraphicsBusiness.vmixutils;

public class ApiCallProcessor
{
    public async Task ProcessApiCalls(List<string> apiCalls)
    {
        SetTexts setTexts = new SetTexts();
        await setTexts.CallApiAsync(apiCalls);
    }
}
