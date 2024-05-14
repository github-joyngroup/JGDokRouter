using PostSharp.Aspects;
using PostSharp.Serialization;
using Joyn.Timelog.Common.Models;
using Joyn.LLMDriver.Models;

namespace Joyn.LLMDriver.PSAspects
{
    [PSerializable]
    public class JGTimelogClientAspect : OnMethodBoundaryAspect
    {
        public int ModelParameterIndex { get; set; }
        public JGLogClientKnownModelTypes ExpectedModelType { get; set; }
        public LogLevel LogLevel { get; set; } = LogLevel.Information;
        public uint Domain { get; set; }
        public long? ClientTag { get; set; } = null;
        public int ExecutionIdParameterIndex { get; set; }

        public override void OnEntry(MethodExecutionArgs args)
        {
            var transactionId = ExtractTransactionIdFromMethodExecutioArgs(args);
            var executionId = (Guid)args.Arguments[ExecutionIdParameterIndex];
            var startMessage = Joyn.Timelog.Client.Logger.LogStart(LogLevel, Domain, transactionId, executionId, ClientTag);
            args.MethodExecutionTag = startMessage;
        }

        public override void OnExit(MethodExecutionArgs args)
        {
            Joyn.Timelog.Client.Logger.LogStop((LogMessage)args.MethodExecutionTag, ClientTag);
        }

        private Guid ExtractTransactionIdFromMethodExecutioArgs(MethodExecutionArgs args)
        {
            switch (ExpectedModelType)
            {
                case JGLogClientKnownModelTypes.ActivityModel:
                    return ((ActivityModel)args.Arguments[ModelParameterIndex]).TransactionIdentifier;

                default:
                    throw new ArgumentException($"Unexpected model type: {ExpectedModelType}");
            }
        }
    }

    public enum JGLogClientKnownModelTypes
    {
        ActivityModel
    }
}
