using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RockDMZ.Domain
{
    public abstract class BusinessDataItem
    {
        private string _startDate;
        private string _endDate;
        private string _price;
        private string _devicePreference;
        private string _scheduling;
        private string _customId;

        public abstract List<string> Header { get; }

        public abstract List<string> Row { get; }

        public string CampaignName { get; set; }

        public string AdgroupName { get; set; }

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

        public string Price {
            get
            {
                return _price;
            }
            set
            {
                _price = value;
                _price = _price.Replace("€", "");
                _price = _price.Replace(".", ",");
                _price = _price.Trim();
                _price = "€ " + _price;
            }
        }

        public string PrettyPrice {
            get
            {
                return PrettifyPrice(_price);
            }
        }

        public string ShortPrice {
            get
            {
                return ShortenPrice(_price);
            }
        }

        public string DevicePreference { get { return _devicePreference == null ? "all" : _devicePreference; } }

        public string StartDate
        {
            get
            {
                return _startDate == null ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") : _startDate;
            }
        }

        public string EndDate
        {
            get
            {
                return _endDate == null ? DateTime.Now.AddYears(10).ToString("yyyy-MM-dd HH:mm:ss") : _endDate;
            }
        }

        public string Scheduling
        {
            get
            {
                return _scheduling ?? "";
            }
        }


        // methods
        public static string GetMd5Hash(MD5 md5Hash, string input)
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

        public void SetStartDate(DateTime start)
        {
            _startDate = start.ToString("yyyy-MM-dd" + " 00:00:00");
        }

        public void SetEndDate(DateTime end)
        {
            _endDate = end.ToString("yyyy-MM-dd" + " 00:00:00");
        }

        public string PrettifyPrice(string price)
        {
            return price.Replace(",00", ",-");
        }

        public string ConvertToPriceExtensionPrice(string price)
        {
            var rtn = "";
            if (price.IndexOfAny(new[] { '.', ',' }) == -1) rtn = price + ",00 EUR";

            else if (price.IndexOf('.') != -1 && price.IndexOf(',') != -1 && price.IndexOf('.') > price.IndexOf(',')) // US format 1,000.95
            {
                rtn = price.Replace(",", "").Replace(".", ",") + " EUR";
            }

            else if (price.IndexOf('.') != -1 && price.IndexOf(',') != -1 && price.IndexOf('.') < price.IndexOf(',')) // EU format 1.000,95
            {
                rtn = price.Replace(".", "") + " EUR";
            }

            else if (price.IndexOf('.') != -1)
            {
                rtn = price.Replace(".", ",") + " EUR";
            }

            else rtn = price + " EUR";

            return rtn;
        }

        public string ConvertToPromotionExtensionPrice(string price) // promo extension uses decimal point
        {
            var rtn = "";
            if (price.IndexOfAny(new[] { '.', ',' }) == -1) rtn = price + ".00 EUR";

            else if (price.IndexOf('.') != -1 && price.IndexOf(',') != -1 && price.IndexOf('.') > price.IndexOf(',')) // US format 1,000.95
            {
                rtn = price.Replace(",", "") + " EUR";
            }

            else if (price.IndexOf('.') != -1 && price.IndexOf(',') != -1 && price.IndexOf('.') < price.IndexOf(',')) // EU format 1.000,95
            {
                rtn = price.Replace(".", "").Replace(",", ".") + " EUR";
            }

            else if (price.IndexOf(',') != -1)
            {
                rtn = price.Replace(",", ".") + " EUR";
            }

            else rtn = price + " EUR";

            return rtn;
        }

        public string MakePlural(string word)
        {
            word = word.ToLower();
            string laatsteLetter = word.Substring(word.Length - 1, 1);
            string eenNaLaatsteLetter = word.Length > 1 ? word.Substring(word.Length - 2, 1) : "";
            string laatsteTweeLetters = word.Substring(word.Length - 2, 2);
            string tweeNaLaatsteLetter = word.Length > 2 ? word.Substring(word.Length - 3, 1) : "";
            string klinkers = "aeiouy";
            string langeKlinkers = "aeiouy";
            string stommeE = "e";
            var onbeklemtoondeUitgangen = new List<string>() {"ie","el", "em", "en", "er", "erd", "aar", "aard", "um" };
            var onbeklemtoondeUitzonderingen = new List<string> { "eem", "eel", "een", "eer" };
            var beklemtoondeUitgangen = new List<string>() { "eur", "foon", "tron", "ion" };
            var tussenvoegsels = new List<string>() { "&", "-", "en", "met", "op", "in", "'" };
            var leenwoorden = new List<string>() { "laptop", "tablet", "desktop", "bar", "cam", "book", "ipad" };
            var uitzonderingen = new List<string>() { "drones", "apparatuur", "boven", "onder", "video", "geluid", "cooking", "media", "audio", "mp3", "health" };

            // uitzonderingen
            if (uitzonderingen.Exists(x => word.EndsWith(x)))
            {
                return PrettifyString(word);
            }

            // afkortingen
            if (word.Length <= 3 && !tussenvoegsels.Contains(word))
            {
                if (laatsteLetter == "s" || laatsteLetter == "x")
                {
                    return PrettifyString(word) + "'en";
                }
                else
                    return PrettifyString(word) + "'s";
            }

            // leenwoorden
            if (leenwoorden.Exists(x => word.EndsWith(x)))
            {
                return word + "s";
            }

            // 2) stomme e
            if (stommeE.IndexOf(laatsteLetter) != -1 && stommeE.IndexOf(eenNaLaatsteLetter) == -1)
            {
                return PrettifyString(word) + "s";
            }

            // 3) onbeklemtoonde uitgang
            if (onbeklemtoondeUitgangen.Exists(x => word.EndsWith(x)) && !onbeklemtoondeUitzonderingen.Exists(x => word.EndsWith(x)))
            {
                return PrettifyString(word) + "s";
            }

            // 3) beklemtoonde uitgang
            if (beklemtoondeUitgangen.Exists(x => word.EndsWith(x)))
            {
                return PrettifyString(word) + "s";
            }

            // 1) uitgang 's voor lange klinkers
            if (langeKlinkers.IndexOf(laatsteLetter) != -1 && langeKlinkers.IndexOf(eenNaLaatsteLetter) == -1)
            {
                return PrettifyString(word) + "'s";
            }
            // uitzondering
            if (laatsteLetter == "o" && eenNaLaatsteLetter == "i")
            {
                return PrettifyString(word) + "'s";
            }

            if (word.EndsWith("en") && klinkers.IndexOf(tweeNaLaatsteLetter) == -1)
            {
                return PrettifyString(word);
            }

            // else
            if (klinkers.IndexOf(laatsteLetter) == -1 && klinkers.IndexOf(eenNaLaatsteLetter) != -1)
            {
                if (eenNaLaatsteLetter == tweeNaLaatsteLetter) // haal 1 klinker weg vb apparaat -> apparaten
                {
                    word = word.Substring(0, word.Length - 2) + laatsteLetter + "en";
                    return PrettifyString(word);
                }
                else
                {
                    var szUitgangen = new List<string>() { "ens", "ans" };
                    var fvUitgangen = new List<string>() { "ijf", "ief", "oef", "eef", "aaf", "euf", "eif" };
                    
                    if (szUitgangen.Exists(x => word.EndsWith(x)) && !word.EndsWith("mens"))
                    {
                        return PrettifyString(word.Substring(0, word.Length - 1) + "z" + "en");
                    }

                    if (szUitgangen.Exists(x => word.EndsWith(x)))
                    {
                        return PrettifyString(word.Substring(0, word.Length - 1) + "v" + "en");
                    }

                    // uitzonderingen geen dubbele uitgang
                    if (word.EndsWith("tor")) laatsteLetter = "";

                    return PrettifyString(word) + laatsteLetter + "en";
                }
                
            }
            return PrettifyString(word) + "en";
        }

        public string PrettifyString(string s, bool capitalizeAcronyms = false, bool useTitleCasing = true)
        {
            string rtn = "";
            var l = s.Split(new[] { ' ' });
            var wordCount = 0;
            foreach(string word in l)
            {
                wordCount++;
                switch(word.Length)
                {
                    case 0:
                        break;
                    case 1:
                        rtn += word.ToUpper(); // no space after a one-letter word
                        break;
                    case 2:
                        if (capitalizeAcronyms) rtn += word.ToUpper() + " ";
                        else rtn += word + " ";
                        break;
                    case 3:
                        if (capitalizeAcronyms) rtn += word.ToUpper() + " ";
                        else rtn += word + " ";
                        break;
                    default:
                        // is it a type (has both letters and numbers)
                        if (word.IndexOfAny(new[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' }) != -1 && (Regex.Matches(word, @"[a-zA-Z]").Count > 0))
                        {
                            // if type has lower case and upper case letters -> leave as is iQ900
                            if (Regex.Matches(word, @"[A-Z]").Count > 0 && Regex.Matches(word, @"[a-z]").Count > 0)
                            {
                                rtn += word + " ";
                            }
                            else
                            {
                                rtn += word.ToUpper() + " ";
                            }
                            
                        }
                        else // not a type -> capitalize first letter --> titlecasing
                        {
                            var newWord = word.ToLower();
                            if (!useTitleCasing && wordCount > 1)
                                rtn += newWord + " ";
                            else
                                rtn += newWord.First().ToString().ToUpper() + newWord.Substring(1) + " ";
                        }
                        break;
                }
            }
            return rtn.Trim();
        }

        public string ShortenPrice(string price)
        {
            return price.Replace(" ", "").Replace(",00", ",-");
        }

        public enum DefaultSchedules { MonSat9To5 }

        public void SetScheduling(DefaultSchedules schedule)
        {
            switch(schedule)
            {
                case DefaultSchedules.MonSat9To5:
                    _scheduling = "Monday, 09:00 AM - 05:00 PM; Tuesday, 09:00 AM - 05:00 PM; Wednesday, 09:00 AM - 05:00 PM; Thursday, 09:00 AM - 05:00 PM; Friday, 09:00 AM - 05:00 PM; Saturday, 09:00 AM - 05:00 PM";
                    break;
                default:
                    break;
            }
        }
    }

    public class PromotionFeedDataItem : BusinessDataItem
    {
        public override List<string> Header
        {
            get
            {
                var l = new List<string>();
                l.Add("Action");
                l.Add("Promotion target");
                l.Add("Language");
                l.Add("Discount modifier");
                l.Add("Percentage off");
                l.Add("Money amount off");
                l.Add("Promotion start");
                l.Add("Promotion end");
                l.Add("Occasion");
                l.Add("Orders over amount");
                l.Add("Final URLs");
                return l;
            }
        }

        public override List<string> Row
        {
            get
            {
                var l = new List<string>();
                l.Add("Add");
                l.Add(PromotionText);
                l.Add(Language);
                l.Add(PromotionDiscountModifier);
                l.Add(PromotionPercentOff);
                l.Add(PromotionMoneyAmoutOff);
                l.Add(StartDate);
                l.Add(EndDate);
                l.Add(Occasion);
                l.Add(MinimumOrderValue);
                l.Add(FinalUrl);
                return l;
            }
        }

        public string PromotionText { get; set; }
        public string Language { get; set; }
        public string PromotionDiscountModifier { get; set; }
        public string PromotionMoneyAmoutOff { get; set; }
        public string Occasion { get;  set; }
        public string MinimumOrderValue { get; set; }
        public string FinalUrl { get; set; }
        public string PromotionPercentOff { get;  set; }
    }

    public class GenericLocationBusinessDataItem : BusinessDataItem
    {
        public override List<string> Header
        {
            get
            {
                var l = new List<string>();
                l.Add("Custom ID");
                l.Add("Target location");
                l.Add("Device preference");
                l.Add("Scheduling");
                l.Add("Start date");
                l.Add("End date");
                l.Add("Target location restriction");
                l.Add("LocationName (text)");
                l.Add("StoreName20 (text)");
                l.Add("StoreName25 (text)");
                l.Add("StoreName30 (text)");
                return l;
            }
        }

        public override List<string> Row
        {
            get
            {
                var l = new List<string>();
                CustomId = LocationId + "" + LocationName;
                l.Add(CustomId);
                l.Add(TargetLocation);
                l.Add(DevicePreference);
                l.Add(Scheduling);
                l.Add(StartDate);
                l.Add(EndDate);
                l.Add(TargetLocationRestriction);
                l.Add(LocationName);
                l.Add(StoreName20);
                l.Add(StoreName25);
                l.Add(StoreName30);
                return l;
            }
        }

        public string LocationId { get; set; }
        public string LocationName { get; set; }
        public string TargetLocation { get; set; }
        public string TargetLocationRestriction { get; set; }
        public string StoreName20 { get; set; }
        public string StoreName25 { get; set; }
        public string StoreName30 { get; set; }
    }

    public class ProductLevelBusinessDataItem : BusinessDataItem
    {
        private string _categoryLevel1;
        private string _categoryLevel2;
        private string _categoryLevel3;
        private string _productName;
        private string _brand;
        private string _type;

        public override List<string> Header
        {
            get
            {
                var l = new List<string>();
                l.Add("Custom ID");
                l.Add("Target campaign");
                l.Add("Target ad group");
                l.Add("Device preference");
                l.Add("Scheduling");
                l.Add("Start date");
                l.Add("End date");
                l.Add("CategoryLevel1SingularTC (text)");
                l.Add("CategoryLevel2SingularTC (text)");
                l.Add("CategoryLevel3SingularTC (text)");
                l.Add("CategoryLevel1SingularLC (text)");
                l.Add("CategoryLevel2SingularLC (text)");
                l.Add("CategoryLevel3SingularLC (text)");
                l.Add("CategoryLevel1PluralTC (text)");
                l.Add("CategoryLevel2PluralTC (text)");
                l.Add("CategoryLevel3PluralTC (text)");
                l.Add("CategoryLevel1PluralLC (text)");
                l.Add("CategoryLevel2PluralLC (text)");
                l.Add("CategoryLevel3PluralLC (text)");
                l.Add("Brand (text)");
                l.Add("Type (text)");
                l.Add("ProductName (text)");
                l.Add("StockQuantity (number)");
                l.Add("Price (price)");
                l.Add("PrettyPrice (text)");
                l.Add("ShortPrice (text)");
                l.Add("DiscountPercentage3 (text)");
                l.Add("DiscountAmountPretty8 (text)");
                l.Add("DiscountAmountShort4 (text)");
                l.Add("SourceFeedPromoLine (text)");
                l.Add("PromoLine30 (text)");
                l.Add("PromoLine50 (text)");
                l.Add("PromoLine80 (text)");
                l.Add("DiscountLine30 (text)");
                l.Add("DiscountLine50 (text)");
                l.Add("DiscountLine80 (text)");
                return l;
            }
        }

        public override List<string> Row
        {
            get
            {
                var l = new List<string>();
                if (String.IsNullOrEmpty(CustomId)) throw new Exception("Custom ID is missing");
                l.Add(CustomId);
                l.Add(CampaignName);
                l.Add(AdgroupName);
                l.Add(DevicePreference);
                l.Add(Scheduling);
                l.Add(StartDate);
                l.Add(EndDate);
                l.Add(CategoryLevel1(false, "TC"));
                l.Add(CategoryLevel2(false, "TC"));
                l.Add(CategoryLevel3(false, "TC"));
                l.Add(CategoryLevel1(false, "LC"));
                l.Add(CategoryLevel2(false, "LC"));
                l.Add(CategoryLevel3(false, "LC"));
                l.Add(CategoryLevel1(true, "TC"));
                l.Add(CategoryLevel2(true, "TC"));
                l.Add(CategoryLevel3(true, "TC"));
                l.Add(CategoryLevel1(true, "LC"));
                l.Add(CategoryLevel2(true, "LC"));
                l.Add(CategoryLevel3(true, "LC"));
                l.Add(Brand);
                l.Add(Type);
                l.Add(ProductName);
                l.Add(StockQuantity);
                l.Add(Price);
                l.Add(PrettyPrice);
                l.Add(ShortPrice);
                l.Add(DiscountPercentage3);
                l.Add(DiscountAmountPretty8);
                l.Add(DiscountAmountShort4);
                l.Add(SourceFeedPromoLine);
                l.Add(PromoLine30);
                l.Add(PromoLine50);
                l.Add(PromoLine80);
                l.Add(DiscountLine30);
                l.Add(DiscountLine50);
                l.Add(DiscountLine80);
                return l;
            }
        }

        public string ProductName
        {
            get
            {
                return _productName.Length > 30 ? PrettifyString(Brand + " " + Type, true) : PrettifyString(_productName, true);
            }
            set
            {
                _productName = PrettifyString(value, true);
            }
        }

        public string StockQuantity { get; set; }

        public string Brand
        {
            get
            {
                return PrettifyString(_brand, true);
            }
            set
            {
                _brand = PrettifyString(value, true);
            }
        }

        public string Type
        {
            get
            {
                return PrettifyString(_type, true);
            }
            set
            {
                _type = value;
            }
        }

        public string DiscountPercentage3 { get; set; }
        public string DiscountAmountPretty8 { get; set; }
        public string DiscountAmountShort4 { get; set; }
        public string SourceFeedPromoLine { get; set; }
        public string PromoLine50 { get; set; }
        public string PromoLine80 { get; set; }
        public string PromoLine30 { get; set; }
        public string DiscountLine30 { get; set; }
        public string DiscountLine50 { get; set; }
        public string DiscountLine80 { get; set; }

        public string CategoryLevel1(bool isPlural, string casing)
        {
            if (String.IsNullOrWhiteSpace(_categoryLevel1)) return "";

            string rtn = isPlural ? MakePlural(_categoryLevel1) : _categoryLevel1;

            rtn = PrettifyString(rtn);

            rtn = casing == "LC" ? rtn.ToLower() : rtn;

            return rtn;
        }

        public string CategoryLevel2(bool isPlural, string casing)
        {
            if (String.IsNullOrWhiteSpace(_categoryLevel2)) return "";

            string rtn = isPlural ? MakePlural(_categoryLevel2) : _categoryLevel2;

            rtn = PrettifyString(rtn);

            rtn = casing == "LC" ? rtn.ToLower() : rtn;

            return rtn;
        }

        public string CategoryLevel3(bool isPlural, string casing)
        {
            if (String.IsNullOrWhiteSpace(_categoryLevel3)) return "";

            string rtn = isPlural ? MakePlural(_categoryLevel3) : _categoryLevel3;

            rtn = PrettifyString(rtn);

            rtn = casing == "LC" ? rtn.ToLower() : rtn;

            return rtn;
        }

        public void SetCategoryLevel1(string v)
        {
            _categoryLevel1 = v;
        }

        public void SetCategoryLevel2(string v)
        {
            _categoryLevel2 = v;
        }

        public void SetCategoryLevel3(string v)
        {
            _categoryLevel3 = v;
        }
    }

    public class CategoryLevelBusinessDataItem : BusinessDataItem
    {
        private string _categoryLevel1;
        private string _categoryLevel2;
        private string _categoryLevel3;

        public override List<string> Header
        {
            get
            {
                var l = new List<string>();
                l.Add("Custom ID");
                l.Add("Target campaign");
                l.Add("Target ad group");
                l.Add("Device preference");
                l.Add("Scheduling");
                l.Add("Start date");
                l.Add("End date");
                l.Add("CategoryLevel1SingularTC (text)");
                l.Add("CategoryLevel2SingularTC (text)");
                l.Add("CategoryLevel3SingularTC (text)");
                l.Add("CategoryLevel1SingularLC (text)");
                l.Add("CategoryLevel2SingularLC (text)");
                l.Add("CategoryLevel3SingularLC (text)");
                l.Add("CategoryLevel1PluralTC (text)");
                l.Add("CategoryLevel2PluralTC (text)");
                l.Add("CategoryLevel3PluralTC (text)");
                l.Add("CategoryLevel1PluralLC (text)");
                l.Add("CategoryLevel2PluralLC (text)");
                l.Add("CategoryLevel3PluralLC (text)");
                l.Add("BrandAwarenessLine30 (text)");
                l.Add("BrandAwarenessLine50 (text)");
                l.Add("BrandAwarenessLine80 (text)");
                l.Add("PromoAwarenessLine30 (text)");
                l.Add("PromoAwarenessLine50 (text)");
                l.Add("PromoAwarenessLine80 (text)");
                l.Add("ActivationLine30 (text)");
                l.Add("ActivationLine50 (text)");
                l.Add("ActivationLine80 (text)");
                l.Add("FromPriceLevel2PrettyPrice10 (text)");
                l.Add("FromPriceLevel3PrettyPrice10 (text)");
                l.Add("FromPriceLevel2ShortPrice5 (text)");
                l.Add("FromPriceLevel3ShortPrice5 (text)");
                l.Add("ProductsInCategoryLevel2 (number)");
                l.Add("ProductsInCategoryLevel3 (number)");
                l.Add("Level2FromPriceLine30 (text)");
                l.Add("Level3FromPriceLine30 (text)");
                l.Add("Level2ProductChoiceLine30 (text)");
                l.Add("Level3ProductChoiceLine30 (text)");

                return l;
            }
        }

        public override List<string> Row
        {
            get
            {
                var l = new List<string>();
                if (String.IsNullOrEmpty(CustomId)) throw new Exception("Custom ID is missing");
                l.Add(CustomId);
                l.Add(CampaignName);
                l.Add(AdgroupName);
                l.Add(DevicePreference);
                l.Add(Scheduling);
                l.Add(StartDate);
                l.Add(EndDate);
                l.Add(CategoryLevel1(false, "TC"));
                l.Add(CategoryLevel2(false, "TC"));
                l.Add(CategoryLevel3(false, "TC"));
                l.Add(CategoryLevel1(false, "LC"));
                l.Add(CategoryLevel2(false, "LC"));
                l.Add(CategoryLevel3(false, "LC"));
                l.Add(CategoryLevel1(true, "TC"));
                l.Add(CategoryLevel2(true, "TC"));
                l.Add(CategoryLevel3(true, "TC"));
                l.Add(CategoryLevel1(true, "LC"));
                l.Add(CategoryLevel2(true, "LC"));
                l.Add(CategoryLevel3(true, "LC"));
                l.Add(BrandAwarenessLine30);
                l.Add(BrandAwarenessLine50);
                l.Add(BrandAwarenessLine80);
                l.Add(PromoAwarenessLine30);
                l.Add(PromoAwarenessLine50);
                l.Add(PromoAwarenessLine80);
                l.Add(ActivationLine30);
                l.Add(ActivationLine50);
                l.Add(ActivationLine80);
                l.Add(FromPriceLevel2PrettyPrice10);
                l.Add(FromPriceLevel3PrettyPrice10);
                l.Add(FromPriceLevel2ShortPrice5);
                l.Add(FromPriceLevel3ShortPrice5);
                l.Add(ProductsInCategoryLevel2);
                l.Add(ProductsInCategoryLevel3);
                l.Add(Level2FromPriceLine30);
                l.Add(Level3FromPriceLine30);
                l.Add(Level2ProductChoiceLine30);
                l.Add(Level3ProductChoiceLine30);
                return l;
            }
        }

        public string BrandAwarenessLine30 { get; set; }
        public string BrandAwarenessLine50 { get; set; }
        public string BrandAwarenessLine80 { get; set; }
        public string PromoAwarenessLine30 { get; set; }
        public string PromoAwarenessLine50 { get; set; }
        public string PromoAwarenessLine80 { get; set; }
        public string ActivationLine50 { get; set; }
        public string ActivationLine30 { get; set; }
        public string ActivationLine80 { get; set; }
        public string FromPriceLevel2PrettyPrice10 { get; set; }
        public string FromPriceLevel3PrettyPrice10 { get; set; }
        public string FromPriceLevel2ShortPrice5 { get; set; }
        public string FromPriceLevel3ShortPrice5 { get; set; }
        public string ProductsInCategoryLevel2 { get; set; }
        public string ProductsInCategoryLevel3 { get; set; }
        public string Level2FromPriceLine30 { get; set; }
        public string Level2ProductChoiceLine30 { get; set; }
        public string Level3ProductChoiceLine30 { get; set; }
        public string Level3FromPriceLine30 { get; set; }

        public string CategoryLevel1(bool isPlural, string casing)
        {
            if (String.IsNullOrWhiteSpace(_categoryLevel1)) return "";

            string rtn = isPlural ? MakePlural(_categoryLevel1) : _categoryLevel1;

            rtn = PrettifyString(rtn);

            rtn = casing == "LC" ? rtn.ToLower() : rtn;

            return rtn;
        }

        public string CategoryLevel2(bool isPlural, string casing)
        {
            if (String.IsNullOrWhiteSpace(_categoryLevel2)) return "";

            string rtn = isPlural ? MakePlural(_categoryLevel2) : _categoryLevel2;

            rtn = PrettifyString(rtn);

            rtn = casing == "LC" ? rtn.ToLower() : rtn;

            return rtn;
        }

        public string CategoryLevel3(bool isPlural, string casing)
        {
            if (String.IsNullOrWhiteSpace(_categoryLevel3)) return "";

            string rtn = isPlural ? MakePlural(_categoryLevel3) : _categoryLevel3;

            rtn = PrettifyString(rtn);

            rtn = casing == "LC" ? rtn.ToLower() : rtn;

            return rtn;
        }

        public void SetCategoryLevel1(string v)
        {
            _categoryLevel1 = v;
        }

        public void SetCategoryLevel2(string v)
        {
            _categoryLevel2 = v;
        }

        public void SetCategoryLevel3(string v)
        {
            _categoryLevel3 = v;
        }
    }

    public class LocalAdCustomizer : BusinessDataItem
    {
        private string _productName { get; set; }
        private string _storeName { get; set; }
        private string _localPrice { get; set; }
        private string _brand { get; set; }
        private string _type { get; set; }
        private string _categoryLevel1 { get; set; }
        private string _categoryLevel2 { get; set; }
        private string _categoryLevel3 { get; set; }

        public string TargetLocation { get; set; }
        
        public string TargetLocationRestriction { get; set; }

        public string StoreName {
            get
            {
                return PrettifyString(_storeName, false);
            }
            set
            {
                _storeName = value;
            }
        }

        public string LocationName
        {
            get
            {
                return StoreName == null ? "" : StoreName.Replace("BCC", "").Trim();
            }
        }

        public string Brand {
            get
            {
                return PrettifyString(_brand, true);
            }
            set
            {
                _brand = PrettifyString(value, true);
            }
        }

        public string Type
        {
            get
            {
                return PrettifyString(_type, true);
            }
            set
            {
                _type = value;
            }
        }

        public string CategoryLevel1
        {
            get
            {
                return PrettifyString(_categoryLevel1);
            }
            set
            {
                _categoryLevel1 = PrettifyString(value, false);
            }
        }

        public string CategoryLevel2
        {
            get
            {
                return PrettifyString(_categoryLevel2);
            }
            set
            {
                _categoryLevel2 = PrettifyString(value, false);
            }
        }

        public string CategoryLevel3
        {
            get
            {
                return PrettifyString(_categoryLevel3);
            }
            set
            {
                _categoryLevel3 = PrettifyString(value, false);
            }
        }

        public string ProductName
        {
            get
            {
                return _productName.Length > 30 ? PrettifyString(Brand + " " + Type, true) : PrettifyString(_productName, true);
            }
            set
            {
                _productName = PrettifyString(value, true);
            }
        }

        public string StockQuantity { get; set; }

        public string LocalPrice
        {
            get
            {
                return _localPrice;
            }
            set
            {
                _localPrice = value;
                _localPrice = _localPrice.Replace("€", "");
                _localPrice = _localPrice.Replace(".", ",");
                _localPrice = _localPrice.Trim();
                _localPrice = "€ " + _localPrice;
            }
        }

        public string LocalPrettyPrice {
            get
            {
                return PrettifyPrice(_localPrice);
            }
        }

        public string LocalShortPrice
        {
            get
            {
                return ShortenPrice(_localPrice);
            }
        }

        public override List<string> Header
        {
            get
            {
                var l = new List<string>();
                l.Add("Custom ID");
                l.Add("Target campaign");
                l.Add("Target ad group");
                l.Add("Target location");
                l.Add("Device preference");
                l.Add("Scheduling");
                l.Add("Start date");
                l.Add("End date");
                l.Add("Target location restriction");
                l.Add("Price (price)");
                l.Add("PrettyPrice (text)");
                l.Add("ShortPrice (text)");
                l.Add("StoreName (text)");
                l.Add("LocationName (text)");
                l.Add("Brand (text)");
                l.Add("Type (text)");
                l.Add("ProductName (text)");
                l.Add("StockQuantity (number)");
                l.Add("LocalPrice (price)");
                l.Add("LocalPrettyPrice (text)");
                l.Add("LocalShortPrice (text)");
                l.Add("CategoryLevel1 (text)");
                l.Add("CategoryLevel2 (text)");
                l.Add("CategoryLevel3 (text)");

                return l;
            }
        }

        public override List<string> Row
        {
            get
            {
                var l = new List<string>();
                if (String.IsNullOrEmpty(CustomId)) throw new Exception("Custom ID is missing");
                l.Add(CustomId);
                l.Add(CampaignName);
                l.Add(AdgroupName);
                l.Add(TargetLocation);
                l.Add(DevicePreference);
                l.Add(Scheduling);
                l.Add(StartDate);
                l.Add(EndDate);
                l.Add(TargetLocationRestriction);
                l.Add(Price);
                l.Add(PrettyPrice);
                l.Add(ShortPrice);
                l.Add(StoreName);
                l.Add(LocationName);
                l.Add(Brand);
                l.Add(Type);
                l.Add(ProductName);
                l.Add(StockQuantity);
                l.Add(LocalPrice);
                l.Add(LocalPrettyPrice);
                l.Add(LocalShortPrice);
                l.Add(CategoryLevel1);
                l.Add(CategoryLevel2);
                l.Add(CategoryLevel3);
                return l;
            }
        }
    }

    
}
