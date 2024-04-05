//using DocDigitizer.Common.Logging;
//using Joyn.DokRouter.Models;
//using Joyn.DokRouter.Payloads;
//using Microsoft.Extensions.Configuration;
//using NetEscapades.Extensions.Logging.RollingFile.Internal;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace DokRouterTester.ChatGPTPipeline
//{
//    public class DetectWork
//    {
//        /// <summary>The cancellation token source to stop the listener - will be flagged on the stop method</summary>
//        private static CancellationTokenSource StoppingCancelationTokenSource;

//        public static void Start()
//        {
//            StoppingCancelationTokenSource = new CancellationTokenSource();
//            new Thread(() => LookingForFiles(StoppingCancelationTokenSource.Token)).Start();
//        }

//        public static void LookingForFiles(CancellationToken cancellationToken)
//        {
//            FileSystemWatcher watcher = new FileSystemWatcher();
//            watcher.Path = @"C:\Users\josep\Documents\GitHub\Code-Reviewer\CodeReviewer\CodeReviewer\CodeReviewer\bin\Debug\net5.0";
//            watcher.NotifyFilter = NotifyFilters.LastWrite;
//            while (!cancellationToken.IsCancellationRequested)
//            {
//                try
//                {
                    
//                    if (ViewersServer.LogViewersDirty)
//                    {
//                        currentLogViewers = ViewersServer.CloneCurrentLogViewers();
//                    }
//                    //Receive the data from the UDP port
//                    var receivedData = _server?.Receive(ref _clientEndPoint);

//                    if (receivedData is null) { continue; }

//                    //Deserialize the received data
//                    var receivedLogMessage = ProtoBufSerializer.Deserialize<LogMessage>(receivedData);
//                    //var receivedLogMessage = BinaronSerializer<LogMessage>.Deserialize(receivedData);

//                    //Check if the application key is authorized - if not, ignore the message by continuing the loop
//                    if (!AcceptedApplicationKeys.Contains(receivedLogMessage.ApplicationKey)) { continue; }

//                    //Stamp the received data with current UTC time
//                    receivedLogMessage.TimeServerTimeStamp = DateTime.UtcNow;

//                    //Flag filters interested in the message
//                    foreach (var logViewer in currentLogViewers)
//                    {
//                        if (logViewer.Filters != null && logViewer.Filters.Any())
//                        {
//                            foreach (var filter in logViewer.Filters)
//                            {
//                                //if Filter State is not ON, continue
//                                if (filter.State != FilterCriteriaState.On) { continue; }

//                                if (filter.Matches(receivedLogMessage))
//                                {
//                                    receivedLogMessage.FilterBitmask |= logViewer.Bitmask;
//                                    break;
//                                }
//                            }
//                        }
//                    }

//                    //Add the received data to the queue
//                    int auxCidx = ReceivedDataQueue.Add(receivedLogMessage);

//                    if ((auxCidx >= LogFileManager.LastDumpToFileIndex + LogFileManager.FlushItemsSize || //Already have more than FlushItemsSize items
//                        auxCidx < LogFileManager.LastDumpToFileIndex)) //Round robined - could calculate the difference between the two indexes but will just to an extra dump file when round robined
//                    {
//                        LogFileManager.Pulse();
//                    }

//                    if ((auxCidx >= ViewersServer.LastDumpToViewersIndex + ViewersServer.FlushItemsSize || //Already have more than FlushItemsSize items
//                        auxCidx < ViewersServer.LastDumpToViewersIndex)) //Round robined - could calculate the difference between the two indexes but will just to an extra dump file when round robined
//                    {
//                        ViewersServer.Pulse();
//                    }
//                }
//                catch (Exception e)
//                {
//                    _logger?.LogError($"Timelog.Server error occurred: {e.Message}");
//                }
//            }
//        }
//    }
//}
