using System.Text.Json.Serialization;

namespace AkilliMetinDuzenleyici.Models
{
    public class UsageData
    {
        [JsonPropertyName("tarih")]
        public string Tarih { get; set; } = System.DateTime.Now.ToString("yyyy-MM-dd");

        [JsonPropertyName("gunluk_istek_sayisi")]
        public int GunlukIstekSayisi { get; set; } = 0;

        [JsonPropertyName("gunluk_max_istek")]
        public int GunlukMaxIstek { get; set; } = 1000;

        [JsonPropertyName("toplam_islenen_kelime")]
        public int ToplamIslenanKelime { get; set; } = 0;

        [JsonPropertyName("toplam_harcanan_token")]
        public int ToplamHarcananToken { get; set; } = 0;
    }
}
