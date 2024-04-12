using DocDigitizer.Common.Logging;
using Joyn.DokRouter.Common.Models;
using Joyn.DokRouter.Common.Payloads;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DokRouterTester.SamplePipeline
{
    public class TestActivity2
    {
        public static void Execute(ActivityExecutionKey activityExecutionKey, object externalData)
        {
            var documentUuid = (Guid)externalData;

            DDLogger.LogInfo<TestActivity1>($"TestActivity2.Begin Execute : DocumentUuid: {documentUuid}");

            Thread.Sleep(Random.Shared.Next(10000, 20000));

            DDLogger.LogInfo<TestActivity1>($"TestActivity2.Finish Execute: DocumentUuid: {documentUuid}");


            //Trigger End Activity
            HelperEventTriggering.OnEndActivity(new EndActivity()
            {
                ActivityExecutionKey = activityExecutionKey,
                IsSuccess = true
            });
        }
    }
}
