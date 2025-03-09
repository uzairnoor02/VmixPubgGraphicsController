public class ApiCallProcessor
{
    private readonly vmi_layerSetOnOff _vmiLayerSetOnOff;

    public ApiCallProcessor(vmi_layerSetOnOff vmiLayerSetOnOff)
    {
        _vmiLayerSetOnOff = vmiLayerSetOnOff;
    }

    public async Task ProcessApiCalls(List<string> apiCalls)
    {
        SetTexts setTexts = new SetTexts();
        await setTexts.CallApiAsync(apiCalls);
    }
}
