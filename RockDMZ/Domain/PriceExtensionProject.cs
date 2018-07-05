using System;
using System.ComponentModel.DataAnnotations;

/// <summary>
/// Summary description for Class1
/// </summary>
namespace RockDMZ.Domain
{
    public class PriceExtensionProject
    {
        public PriceExtensionProject()
        {
            //
            // TODO: Add constructor logic here
            //
        }
        
        public int Id {get;set;}

        [MaxLength(50)]
        [Required]
        public string Name {get;set;}

        [MaxLength(255)]
        [Required]
        public string ProductPerformanceFeedLocation {get;set;}

        [MaxLength(255)]
        [Required]
        public string ProductFeedLocation {get;set;}

        [MaxLength(255)]
        [Required]
        public string PriceExtensionFeedDestinationLocation {get;set;}

        //public int AdWordsCampaignStructureId { get; set; }

        //public virtual AdWordsCampaignStructure CampaignStructure {get;set;}

        [MaxLength(4000)]
        public string ProcessingQuery {get;set;}
    }
}