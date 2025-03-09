using Hangfire;
using VmixGraphicsBusiness;
using VmixGraphicsBusiness.vmixutils;

public class ApiCallProcessor
{
    [AutomaticRetry(Attempts =0)]
    public async Task ProcessApiCalls(List<string> apiCalls)
    {
        SetTexts setTexts = new SetTexts();
        await setTexts.CallApiAsync(apiCalls);
    }
}
