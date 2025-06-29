namespace ApiTestingAgent.Http
{
    public interface ITypedHttpServiceClientFactory
    {
        TClient Create<TClient, TClientImplementation>() where TClient : class;
    }
}
