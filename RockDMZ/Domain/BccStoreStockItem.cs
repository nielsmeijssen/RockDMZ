using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace RockDMZ.Domain
{
    public class BccStoreStockItem
    {
        public int Id { get; set; }

        [MaxLength(50)]
        public string ClientProductId { get; set; }

        [MaxLength(50)]
        public string GmbStoreId { get; set; }

        public int StockQuantity { get; set; }

        [MaxLength(50)]
        public string Price { get; set; }
    }

    public sealed class BccStoreStockItemCsvMap : CsvHelper.Configuration.ClassMap<BccStoreStockItem>
    {
        public BccStoreStockItemCsvMap()
        {
            Map(m => m.Id).Ignore();

            Map(m => m.ClientProductId).Name("itemid");

            Map(m => m.GmbStoreId).Name("store code");

            Map(m => m.StockQuantity).Name("quantity");

            Map(m => m.Price).Name("price");
        }
    }
}
