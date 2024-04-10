namespace Joyn.DokRouter.Payloads
{
    public class StartActivityOut
    {
        public ActivityExecutionKey ActivityExecutionKey { get; set; }
        public byte[] MarshalledExternalData { get; set; }

        public string CallbackUrl { get; set; }
        public bool TestMode { get; set; }
    }
}
