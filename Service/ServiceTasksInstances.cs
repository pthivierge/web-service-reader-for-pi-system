﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Quartz;
using WSR.Core;
using WSR.Core.DataReaders;

namespace WSR.Service
{
    public static class ServiceTasksInstances
    {
        public static ConfigurationManager ConfigurationManager;
        public static DataWriter DataWriter;
        public static DataReadersManager DataReadersManager;
    }

    public class ConfigurationJob : IJob
    {
        public void Execute(IJobExecutionContext context)
        {
            ServiceTasksInstances.ConfigurationManager.RunOnce();
        }
    }

    public class DataWriterJob : IJob
    {
        public void Execute(IJobExecutionContext context)
        {
            ServiceTasksInstances.DataWriter.RunOnce();
        }
    }

    public class DataReadersManagerJob : IJob
    {
        public void Execute(IJobExecutionContext context)
        {
            ServiceTasksInstances.DataReadersManager.RunOnce();
        }
    }
}
