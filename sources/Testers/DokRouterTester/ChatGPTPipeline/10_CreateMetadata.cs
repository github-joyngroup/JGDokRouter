//using DocDigitizer.Common.Logging;
//using Joyn.DokRouter.Models;
//using Joyn.DokRouter.Payloads;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace DokRouterTester.ChatGPTPipeline
//{
//    public class CreateMetadata : BasePipelineActivity
//    {
//        //public string TestKey1 { get; set; }

//        public CreateMetadata(string name, Guid identifier, int orderNumber) : base(name, identifier, orderNumber)
//        {
//        }

//        public override void SetupFromConfigurationDictionary(Dictionary<string, object> configurations)
//        {
//            //if (configurations.TryGetValue("TestKey1", out var testKey1)) { TestKey1 = testKey1?.ToString(); }
//        }

//        public override void Execute(ActivityExecutionKey activityExecutionKey, object externalData)
//        {
//            var model = (ChatGPTModel)externalData;

//            //TO DO

//            //Trigger End Activity
//            HelperEventTriggering.OnEndActivity(new Joyn.DokRouter.Payloads.EndActivity()
//            {
//                ActivityExecutionKey = activityExecutionKey,
//                IsSuccess = true
//            });
//        }
//    }
//}
