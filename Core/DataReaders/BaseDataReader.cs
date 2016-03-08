﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DCS.Core.Helpers;
using log4net;
using OSIsoft.AF;
using OSIsoft.AF.Asset;

namespace DCS.Core.DataReaders
{
    /// <summary>
    /// This class is a base implementation of a data reader that gets its configuration from AF elements
    /// 
    /// </summary>
    public abstract class BaseDataReader
    {
        protected ILog Logger => LogManager.GetLogger(GetType());

        protected string _afServerName;
        protected string _afDatabaseName;
        protected string _afElementTemplateName;
        protected AFUpdater _afUpdater;
        protected ConcurrentQueue<AFElement> _afElementsQueue=new ConcurrentQueue<AFElement>();
        protected AFConnectionHelper _afConnectionHelper;


        protected BaseDataReader(string afServerName, string afDataBaseName, string elementTemplateName)
        {
            _afServerName = afServerName;
            _afDatabaseName = afDataBaseName;
            _afElementTemplateName = elementTemplateName;
        }

        /// <summary>
        /// Initializes BaseDataReader Parameters.
        /// Once initialized, the GetConfiguration method can be used to retrieve AF Elements from the database.
        /// </summary>
        public void Inititialize()
        {
            if(!GetConfiguration())
                throw new DataReaderCouldNotInitialize();

            _afUpdater = new AFUpdater(_afConnectionHelper.GetDatabase());
        }

        public void RefreshConfiguration()
        {
            _afUpdater.RefreshLoadedObjects();
        }

        /// <summary>
        /// This method gets the configuration from the AF Database
        /// </summary>
        private bool GetConfiguration()
        {
            try
            {
                Logger.InfoFormat("Starting configuration");
                AFDatabase database;
                
                _afConnectionHelper = AFConnectionHelper.ConnectAndGetDatabase(_afServerName, _afDatabaseName, out database);

                if(!database.ElementTemplates.Contains(_afElementTemplateName))
                    throw new AFElementTemplateDoNotExistException();

                AFSDKHelpers.LoadElementsByTemplate(database, database.ElementTemplates[_afElementTemplateName], _afElementsQueue);

            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return false;
            }

            return true;

        }

        public virtual void CollectData()
        {
            Logger.DebugFormat("Data collection started for {0} elements",_afElementsQueue.Count);
            if(_afElementsQueue.Count==0)
            { 
                Logger.InfoFormat("Data collection cannot proceed, there is not AF Element loaded yet.");
            }


            Parallel.ForEach(_afElementsQueue, new ParallelOptions() {MaxDegreeOfParallelism = 10 }, (element) =>
            {
               var values=ReadValues(element);
               SharedData.DataWriterQueue.Enqueue(values);
            });

            Logger.DebugFormat("Data collection cycle completed");
        }

        /// <summary>
        /// This method should be implemented and contain the logic for specific for the web service and to the asset
        /// 
        /// </summary>
        /// <returns>The list of values to be written back to the PI System</returns>
        public abstract List<AFValue> ReadValues(AFElement element);
        


    }


    public class DataReaderCouldNotInitialize : Exception
    {
        public DataReaderCouldNotInitialize() : base("Initialization failed.  You may verify the configuration and look at messages returned by the exception.") { }
    }

    public class AFElementTemplateDoNotExistException : Exception
    {
        public AFElementTemplateDoNotExistException() : base("The AF Element Template specified does not exist in the AF Database") { }
    }
}
