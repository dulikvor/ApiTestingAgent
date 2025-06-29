using Microsoft.SemanticKernel;

namespace ApiTestingAgent.Data.Stream
{
    public class StreamReporter
    {
        private readonly IResponseStreamWriter<CopilotServerSentEventsStreamWriter> _streamWriter;

        public StreamReporter(
            IResponseStreamWriter<CopilotServerSentEventsStreamWriter> streamWriter)
        {
            _streamWriter = streamWriter;
        }

        public async Task ReportAsync(IEnumerable<ChatMessageContent> messages, HttpContext? httpContext = null)
        {
            var objectList = messages.Cast<object>().ToList();
            var context = httpContext ?? CallContext.GetData("HttpContext") as HttpContext;
            await _streamWriter.WriteToStreamAsync(context!, objectList);
        }
    }
}
