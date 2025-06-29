namespace ApiTestingAgent.Http
{
    public class TypedHttpServiceClientFactory : ITypedHttpServiceClientFactory
    {
        IHttpClientFactory _httpClientFactory;

        public TypedHttpServiceClientFactory(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public TClient Create<TClient, TClientImplementation>() where TClient : class
        {
            var client = _httpClientFactory.CreateClient(typeof(TClient).Name);
            var instance = Activator.CreateInstance(typeof(TClientImplementation), client) as TClient;
            if (instance == null)
            {
                throw new InvalidOperationException($"Unable to create an instance of {typeof(TClient).FullName}.");
            }
            return instance;
        }
    }
}
