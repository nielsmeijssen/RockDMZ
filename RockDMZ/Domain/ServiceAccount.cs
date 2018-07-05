namespace RockDMZ.Domain
{
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    public class ServiceAccount : IEntity
    {
        public int Id { get; set; }

        [Display(Name = "Service")]
        public ServiceName ServiceName { get; set; }

        [Display(Name = "Credential type")]
        public CredentialType CredentialType { get; set; }

        [Display(Name = "Service Location")]
        [StringLength(255)]
        public string ServiceLocation { get; set; }

        [StringLength(50, MinimumLength = 5)]
        public string Email { get; set; }

        [MaxLength(50)]
        public string Password { get; set; }

        [StringLength(255, MinimumLength = 5)]
        public string KeyLocation { get; set; }

        [MaxLength(100)]
        public string FriendlyName { get; set; }

        // public virtual ICollection<ApiSuckDefinition> ApiSuckDefinitions { get; set; } = new List<ApiSuckDefinition>();
    }

    public enum ServiceName { GoogleAnalytics, CsvFeedAppend, BCCLocalInventoryFeed, CsvFeedOverwrite, BCCLocalProductFeed, BCCStoreStockTextAds, BCCCategoryLevelBusinessData, BCCProductLevelBusinessData, BCCPromotionFeed }

    public enum CredentialType { WebUser, Service, NotApplicable, FTP }
}