namespace Joyn.DokRouter.Payloads
{
    public class EndActivity
    {
        public ActivityExecutionKey ActivityExecutionKey { get; set; }
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public byte[]? MarshalledExternalData { get; set; }
    }
}
