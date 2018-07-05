using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Summary description for Class1
/// </summary>
namespace RockDMZ.Domain
{
    public class AdWordsCampaignStructure
    {
        public AdWordsCampaignStructure()
        {
            //
            // TODO: Add constructor logic here
            //
        }

        public int Id { get; set; }

        public string Name { get; set; }

        public int AdWordsCampaignTemplateId { get; set; }

        public virtual List<AdWordsCampaignTemplate> CampaignTemplates { get; set; }
    }

    public class AdWordsCampaignTemplate
    {
        public AdWordsCampaignTemplate()
        {
            AdgroupTemplates = new List<AdWordsAdgroupTemplate>();
        }

        public int Id { get; set; }

        public string NameTemplate { get; set; }

        public int AdWordsAdgroupTemplateId { get; set; }

        public virtual List<AdWordsAdgroupTemplate> AdgroupTemplates { get; set; }
    }

    public class AdWordsAdgroupTemplate
    {
        public int Id { get; set; }

        public string NameTemplate { get; set; }
    }
}
