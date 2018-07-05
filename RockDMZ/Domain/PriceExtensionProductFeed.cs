using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

/// <summary>
/// Summary description for Class1
/// </summary>

namespace RockDMZ.Domain
{
    public class PriceExtensionProductFeed
    {
        public PriceExtensionProductFeed()
        {
            //
            // TODO: Add constructor logic here
            //
        }

        public int Id { get;set; }

        [MaxLength(50), Required]
        public string Brand { get; set; }

        [MaxLength(50), Required]
        public string ItemId { get; set; }

        [MaxLength(50), Required]
        public string ProductType { get; set; }

        [MaxLength(50), Required]
        public string SubProductType { get; set; }

        [Required]
        public double Price { get; set; }

        [MaxLength(50)]
        public string ItemHeader { get; set; }

        [MaxLength(50)]
        public string ItemDescription { get; set; }

        [MaxLength(50)]
        public string ItemPrice { get; set; }

        [MaxLength(50)]
        public string ItemPriceUnit { get; set; }

        [MaxLength(255), Required]
        public string Link { get; set; }

        [MaxLength(255)]
        public string LinkLevel2 { get; set; }

        [MaxLength(255)]
        public string LinkLevel3 { get; set; }

        [MaxLength(100), Required]
        public string ProductName { get; set; }

        [MaxLength(50)]
        public string Transactions { get; set; }

        [MaxLength(50)]
        public string Revenue { get; set; }

        [Required]
        public int ProductsInStock { get; set; }

        [Required]
        public DateTime ProductCreationDate { get; set; }

        [MaxLength(50)]
        public string ItemDescriptionLevel3 { get; set; }

        public bool? IgnoreLevel2 { get; set; }

        public bool? IgnoreLevel3 { get; set; }

        public double? FromPriceLevel2 { get; set;}

        public double? FromPriceLevel3 { get; set; }

        public double? AvgPriceLevel2 { get; set; }

        public double? AvgPriceLevel3 { get; set; }

        [MaxLength(50)]
        public string Account { get; set; }

        public int PriceExtensionProjectId { get; set; }

    }

    public sealed class PriceExtensionProductFeedCsvMap : CsvHelper.Configuration.ClassMap<PriceExtensionProductFeed>
    {
        public PriceExtensionProductFeedCsvMap()
        {
            Map(m => m.Id).Ignore();

            Map(m => m.Brand).Name("brand");

            Map(m => m.ItemId).Name("id");

            Map(m => m.ProductType).Name("cat_level2");

            Map(m => m.SubProductType).Name("cat_level3");

            Map(m => m.Price).ConvertUsing(row => Double.Parse(row.GetField("price").Replace(".",",")));
            // Map(m => m.Price).Name("price");
            Map(m => m.Link).Name("link");
            Map(m => m.ProductName).Name("title");
            Map(m => m.ProductsInStock).Name("products_in_stock");
            // Map(m => m.ProductCreationDate).Name("product_creationdate");
            Map(m => m.ProductCreationDate).ConvertUsing(row => DateTime.ParseExact(String.IsNullOrWhiteSpace(row.GetField("product_creationdate")) ? "2000/01/01" : row.GetField("product_creationdate"), "yyyy/MM/dd", CultureInfo.InvariantCulture));
            Map(m => m.Account).Name("extgg_description");

            Map(m => m.ItemHeader).Ignore();
            Map(m => m.ItemDescription).Ignore();
            Map(m => m.ItemDescriptionLevel3).Ignore();
            Map(m => m.ItemPrice).Ignore();
            Map(m => m.ItemPriceUnit).Ignore();
            Map(m => m.LinkLevel2).Ignore();
            Map(m => m.LinkLevel3).Ignore();
            Map(m => m.Transactions).Ignore();
            Map(m => m.Revenue).Ignore();
            Map(m => m.IgnoreLevel2).Ignore();
            Map(m => m.IgnoreLevel3).Ignore();
            Map(m => m.FromPriceLevel2).Ignore();
            Map(m => m.FromPriceLevel3).Ignore();
            Map(m => m.AvgPriceLevel2).Ignore();
            Map(m => m.AvgPriceLevel3).Ignore();
            Map(m => m.PriceExtensionProjectId).Ignore();
        }
    }
}
