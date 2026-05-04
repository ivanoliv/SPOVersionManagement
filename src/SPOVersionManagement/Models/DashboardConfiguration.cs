using Newtonsoft.Json;

namespace SPOVersionManagement.Models
{
    public class DashboardConfiguration
    {
        [JsonProperty("Language")]
        public string Language { get; set; }

        [JsonProperty("Currency")]
        public CurrencyConfig Currency { get; set; }

        [JsonProperty("CostPerGBMonth")]
        public decimal CostPerGBMonth { get; set; }

        [JsonProperty("CostPerTBMonth")]
        public decimal CostPerTBMonth { get; set; }

        [JsonProperty("CostPerGBYear")]
        public decimal CostPerGBYear { get; set; }

        [JsonProperty("CostPerTBYear")]
        public decimal CostPerTBYear { get; set; }

        [JsonProperty("ExchangeRate")]
        public ExchangeRateConfig ExchangeRate { get; set; }

        [JsonProperty("DateFormat")]
        public string DateFormat { get; set; }

        [JsonProperty("TimeFormat")]
        public string TimeFormat { get; set; }

        [JsonProperty("RefreshIntervalSeconds")]
        public int RefreshIntervalSeconds { get; set; }

        [JsonProperty("ReexecutionDays")]
        public object ReexecutionDays { get; set; }

        [JsonProperty("LookBackDays")]
        public int LookBackDays { get; set; } = 7;

        [JsonProperty("ZeroVersionAction")]
        public string ZeroVersionAction { get; set; }

        [JsonProperty("DashboardPort")]
        public int DashboardPort { get; set; }

        [JsonProperty("DashboardLaunchMode")]
        public string DashboardLaunchMode { get; set; }

        [JsonProperty("LastModified")]
        public string LastModified { get; set; }

        [JsonProperty("Version")]
        public string Version { get; set; }
    }

    public class CurrencyConfig
    {
        [JsonProperty("Symbol")]
        public string Symbol { get; set; }

        [JsonProperty("Code")]
        public string Code { get; set; }

        [JsonProperty("Position")]
        public string Position { get; set; }

        [JsonProperty("DecimalSeparator")]
        public string DecimalSeparator { get; set; }

        [JsonProperty("ThousandsSeparator")]
        public string ThousandsSeparator { get; set; }
    }

    public class ExchangeRateConfig
    {
        [JsonProperty("Enabled")]
        public bool Enabled { get; set; }

        [JsonProperty("USDToLocal")]
        public decimal USDToLocal { get; set; }

        [JsonProperty("LastUpdated")]
        public string LastUpdated { get; set; }
    }
}
