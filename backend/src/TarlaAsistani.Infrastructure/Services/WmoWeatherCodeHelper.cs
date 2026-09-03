namespace TarlaAsistani.Infrastructure.Services;

public static class WmoWeatherCodeHelper
{
    public static string GetConditionDescription(int? code)
    {
        if (!code.HasValue) return "Bilinmiyor";

        return code.Value switch
        {
            0 => "Açık",
            1 => "Çoğunlukla Açık",
            2 => "Parçalı Bulutlu",
            3 => "Bulutlu",
            45 or 48 => "Sisli",
            51 or 53 or 55 => "Çiseleyen Yağmur",
            56 or 57 => "Dondurucu Çiseleme",
            61 => "Hafif Yağmurlu",
            63 => "Yağmurlu",
            65 => "Şiddetli Yağmurlu",
            66 or 67 => "Dondurucu Yağmur",
            71 => "Hafif Karlı",
            73 => "Karlı",
            75 => "Yoğun Karlı",
            77 => "Kar Taneli",
            80 => "Hafif Sağanak",
            81 => "Sağanak Yağışlı",
            82 => "Şiddetli Sağanak",
            85 or 86 => "Kar Sağanağı",
            95 => "Gök Gürültülü Fırtına",
            96 or 99 => "Dolu ile Karışık Fırtına",
            _ => "Bulutlu"
        };
    }
}
