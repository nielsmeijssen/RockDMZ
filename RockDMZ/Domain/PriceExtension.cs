using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Summary description for Class1
/// </summary>
namespace RockDMZ.Domain
{
    public class PriceExtension
    {
        private string _customId;

        public PriceExtension()
        {
            //
            // TODO: Add constructor logic here
            //
        }

        public string ItemId { get; set; }

        public string Action { get; set; }

        public string Campaign { get; set; }

        public string Adgroup { get; set; }

        public string Type { get; set; }

        public string PriceQualifier { get; set; }

        public string Language { get; set; }

        public string TrackingTemplate { get; set; }

        public List<Item> Items { get; set; }

        public string CustomId
        {
            get
            {
                return _customId;
            }
            set
            {
                using (MD5 md5Hash = MD5.Create())
                {
                    _customId = GetMd5Hash(md5Hash, value);
                }
            }
        }

        // methods
        static string GetMd5Hash(MD5 md5Hash, string input)
        {

            // Convert the input string to a byte array and compute the hash.
            byte[] data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(input));

            // Create a new Stringbuilder to collect the bytes
            // and create a string.
            StringBuilder sBuilder = new StringBuilder();

            // Loop through each byte of the hashed data 
            // and format each one as a hexadecimal string.
            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }

            // Return the hexadecimal string.
            return sBuilder.ToString();
        }
    }

    public class Item
    {
        public Item()
        {
            Header = Price = PriceUnit = Description = FinalUrl = FinalMobileUrl = ItemId = "";
        }

        public string ItemId { get; set; }

        public int Index { get; set; }

        public string Header { get; set; }

        public string Price { get; set; }

        public string PriceUnit { get; set; }

        public string Description { get; set; }

        public string FinalUrl { get; set; }

        public string FinalMobileUrl { get; set; }

        public double? FromPrice { get; set; }

        public string Account { get; set; }

    }
}
