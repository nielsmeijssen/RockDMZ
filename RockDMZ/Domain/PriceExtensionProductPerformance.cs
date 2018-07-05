using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace RockDMZ.Domain
{
    public class PriceExtensionProductPerformance
    {
        public int Id { get; set; }

        public int GaView { get; set; }

        [MaxLength(250), Required]
        public string GaSourceMedium { get; set; }

        [MaxLength(500), Required]
        public string GaProductName { get; set; }

        public int GaDate { get; set; }

        [MaxLength(500), Required]
        public string GaChannelGrouping { get; set; }

        [MaxLength(500), Required]
        public string GaProductBrand { get; set; }

        [MaxLength(500), Required]
        public string GaProductCategoryHierarchy { get; set; }

        [MaxLength(500)]
        public string GaCustomCategory { get; set; }
        
        public int GaUniquePurchases { get; set; }

        public double GaItemRevenue { get; set; }

        public int PriceExtensionProjectId { get; set; }

    }

    public sealed class PriceExtensionProductPerformanceCsvMap : CsvHelper.Configuration.ClassMap<PriceExtensionProductPerformance>
    {
        public PriceExtensionProductPerformanceCsvMap()
        {
            Map(m => m.Id).Ignore();

            Map(m => m.GaView).Name("GA View");

            Map(m => m.GaSourceMedium).Name("ga:sourceMedium");

            Map(m => m.GaProductName).Name("ga:productName");

            Map(m => m.GaDate).Name("ga:date");

            Map(m => m.GaChannelGrouping).Name("ga:channelGrouping");
            Map(m => m.GaProductBrand).Name("ga:productBrand");
            Map(m => m.GaProductCategoryHierarchy).Name("ga:productCategoryHierarchy");
            Map(m => m.GaCustomCategory).Name("ga:dimension46");
            Map(m => m.GaUniquePurchases).Name("ga:uniquePurchases");
            Map(m => m.GaItemRevenue).ConvertUsing(row => Double.Parse(row.GetField("ga:itemRevenue").Replace(".", ",")));
            //Map(m => m.GaItemRevenue).Name("ga:itemRevenue");

            Map(m => m.PriceExtensionProjectId).Ignore();
        }
    }
}
