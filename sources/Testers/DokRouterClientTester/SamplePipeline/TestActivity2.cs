namespace DokRouterClientTester.SamplePipeline
{
    public class TestActivity1
    {
        public static void Execute(ActivityExecutionKey activityExecutionKey, object externalData)
        {
            var documentUuid = (Guid)externalData;
            DDLogger.LogInfo<TestActivity1>($"TestActivity1.Begin Execute : DocumentUuid: {documentUuid}");

            Thread.Sleep(Random.Shared.Next(5000, 10000));

            DDLogger.LogInfo<TestActivity1>($"TestActivity1.Finish Execute: DocumentUuid: {documentUuid}");

            //Trigger End Activity
            HelperEventTriggering.OnEndActivity(new Joyn.DokRouter.Payloads.EndActivity()
            {
                ActivityExecutionKey = activityExecutionKey,
                IsSuccess = true
            });
        }
    }
}
