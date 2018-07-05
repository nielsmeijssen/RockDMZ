using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace RockDMZ.Domain
{
    public class ApiDatatable : IEntity
    {
        public int Id { get; set; }

        [MaxLength(1024)]
        public string LocalFilePath { get; set; }

        [MaxLength(1024)]
        public string Url { get; set; }

        public int ServiceAccountId { get; set; }

        public DateTime? LastDownload { get; set; }

        public DateTime? LastDateDownloaded { get; set; } 

        [MaxLength(1024)]
        public string CsvViewIds { get; set; }

        [MaxLength(2048)]
        public string ApiQuery { get; set; }

        [MaxLength(50)]
        [Required]
        public string Name { get; set; }

        public DateTime FirstDate { get; set; }

        public DateTime? LastDate { get; set; }

        public bool IncludeDateOfDownload { get; set; }

        public int ReloadBufferSizeInDays { get; set; }

        public UpdateSchedule UpdateSchedule { get; set; }

        public int LookbackWindowInDays { get; set; }
        
        public ServiceAccount ServiceAccount { get; set; }

        public bool IsActive { get; set; }
    }

    public enum UpdateSchedule { None, Hourly, Daily4am, Daily6am, Daily8am, WeeklyMonday6am, Daily9am, Daily10am, Daily4pm, Daily5pm, Daily6pm, Daily7pm }
}
