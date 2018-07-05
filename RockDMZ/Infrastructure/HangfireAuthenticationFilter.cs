using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hangfire.Annotations;
using Hangfire.Dashboard;

namespace RockDMZ.Infrastructure
{
    public class HangfireAuthorizationFilter : Hangfire.Dashboard.IDashboardAuthorizationFilter
    {
        public HangfireAuthorizationFilter()
        {
        }

        public bool Authorize(IDictionary<string, object> owinEnvironment)
        {
            return true;
        }

        public bool Authorize([NotNull] DashboardContext context)
        {
            return true;
        }
    }
}
