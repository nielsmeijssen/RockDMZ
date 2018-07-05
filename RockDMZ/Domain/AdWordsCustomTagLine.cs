using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace RockDMZ.Domain
{
    public class AdWordsCustomTagLine
    {
        public int Id { get; set; }
        [MaxLength(50), Required]
        public string ClientName { get; set; }
        [MaxLength(255)]
        public string TargetCategoryLevel1 { get; set; }
        [MaxLength(255)]
        public string TargetCategoryLevel2 { get; set; }
        [MaxLength(255)]
        public string TargetCategoryLevel3 { get; set; }
        [MaxLength(255)]
        public string TargetSourceFeedPromoLine { get; set; }
        [MaxLength(30)]
        public string ProductLevelPromoLine30 { get; set; }
        [MaxLength(50)]
        public string ProductLevelPromoLine50 { get; set; }
        [MaxLength(80)]
        public string ProductLevelPromoLine80 { get; set; }
        [MaxLength(30)]
        public string BrandAwarenessLine30 { get; set; }
        [MaxLength(50)]
        public string BrandAwarenessLine50 { get; set; }
        [MaxLength(80)]
        public string BrandAwarenessLine80 { get; set; }
        [MaxLength(30)]
        public string PromoAwarenessLine30 { get; set; }
        [MaxLength(50)]
        public string PromoAwarenessLine50 { get; set; }
        [MaxLength(80)]
        public string PromoAwarenessLine80 { get; set; }
        [MaxLength(30)]
        public string ActivationLine30 { get; set; }
        [MaxLength(50)]
        public string ActivationLine50 { get; set; }
        [MaxLength(80)]
        public string ActivationLine80 { get; set; }
        public DateTime CreationDate { get; set; }
    }
}
