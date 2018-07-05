using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace RockDMZ.Domain
{
    public class BccSourceFeedItem
    {
        public int Id { get; set; }

        [MaxLength(50)]
        public string ClientProductId { get; set; }

        [MaxLength(50)]
        public string Price { get; set; }

        [MaxLength(50)]
        public string Brand { get; set; }

        [MaxLength(50)]
        public string Type { get; set; }

        [MaxLength(255)]
        public string ProductName { get; set; }

        [MaxLength(50)]
        public string CategoryLevel1 { get; set; }

        [MaxLength(50)]
        public string CategoryLevel2 { get; set; }

        [MaxLength(50)]
        public string CategoryLevel3 { get; set; }

        [MaxLength(50)]
        public string AccountId { get; set; }

        [MaxLength(50)]
        public string StockQuantity { get; set; }

        [MaxLength(255)]
        public string SourceFeedPromoLine { get; set; }

        [MaxLength(50)]
        public string PricePreviously { get; set; }

        [MaxLength(1024)]
        public string Link { get; set; }
    }

    public sealed class BccSourceFeedItemCsvMap : CsvHelper.Configuration.ClassMap<BccSourceFeedItem>
    {
        public BccSourceFeedItemCsvMap()
        {
            Map(m => m.Id).Ignore();

            Map(m => m.ClientProductId).Name("id");

            Map(m => m.Price).Name("price");

            Map(m => m.Brand).Name("brand");

            Map(m => m.Type).Name("type");

            Map(m => m.ProductName).Name("title");

            Map(m => m.CategoryLevel1).Name("cat_level1");

            Map(m => m.CategoryLevel2).Name("cat_level2");

            Map(m => m.CategoryLevel3).Name("cat_level3");

            Map(m => m.StockQuantity).Name("products_in_stock");

            Map(m => m.SourceFeedPromoLine).Name("promo_tagline");

            Map(m => m.PricePreviously).Name("price_old");

            Map(m => m.AccountId).Name("extgg");

            Map(m => m.Link).Name("link");
        }
    }
}
