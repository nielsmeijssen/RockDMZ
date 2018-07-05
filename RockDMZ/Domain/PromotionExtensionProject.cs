using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace RockDMZ.Domain
{
    public class PromotionExtensionProject
    {
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string Name { get; set; }

        [MaxLength(255)]
        [Required]
        public string ProductFeedLocation { get; set; }

        [Required, MaxLength(255)]
        public string PromotionExtensionFeedDestinationLocation { get; set; }

        public int DefaultPromoDurationInDays { get; set; }

        public bool UsePercentages { get; set; }

        public int MinimumPercentage { get; set; }

        public bool UseAmounts { get; set; }

        public int MinimumAmout { get; set; }

        public int OrMinimumAmountPercentage { get; set; }

    }
}
