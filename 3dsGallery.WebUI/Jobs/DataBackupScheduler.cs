using Quartz;
using Quartz.Impl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace _3dsGallery.WebUI.Jobs
{
    public class DataBackupScheduler
    {
        public static void Start()
        {
            IScheduler scheduler = StdSchedulerFactory.GetDefaultScheduler();
            IJobDetail job = JobBuilder.Create<DataBackup>().Build();
            ITrigger trigger = TriggerBuilder.Create()
                                             .StartNow()
                                             .WithSimpleSchedule(x => x
                                                .WithInterval(new TimeSpan(7, 0, 0, 0))
                                                .RepeatForever())
                                             .Build();

            scheduler.Start();
            scheduler.ScheduleJob(job, trigger);
        }
    }
}