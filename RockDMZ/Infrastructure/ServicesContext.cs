using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RockDMZ.Infrastructure
{
    public class ServicesContext
    {
        public GoogleAnalyticsContext GoogleAnalytics;
    }

    public class GoogleAnalyticsContext
    {
        public string JsonSecret { get; set; }
    }
}
